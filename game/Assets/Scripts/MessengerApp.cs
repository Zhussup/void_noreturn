using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Монитор 2 — корпоративный мессенджер ГАНДЕКСА: ГАРМОНИЯ, шеф, сотрудники,
/// кибербезы. Здесь же задаётся вопрос про настроение и появляются чужие
/// сообщения от имени Брика.
/// </summary>
public class MessengerApp
{
    public const int StationIndex = 1;

    // цвета — из общей палитры экранов (ScreenStyle / ScreenPalette)
    static Color C => ScreenPalette.Messenger.Text;
    static Color Dim => ScreenPalette.Messenger.Dim;
    static Color Accent => ScreenPalette.Messenger.Accent;
    static Color Warn => ScreenPalette.Messenger.Warn;
    static Color Err => ScreenPalette.Messenger.Alarm;
    static Color White => ScreenPalette.Messenger.Bright;

    readonly NightDirector dir;
    readonly ScreenSurface s;
    readonly InputField moodInput = new InputField();

    int channel;
    int scroll;
    int lastMessageCount = -1;
    string message = "";
    Color messageColor = C;
    float messageUntil;

    public MessengerApp(NightDirector dir, ScreenSurface surface)
    {
        this.dir = dir;
        s = surface;
    }

    /// <summary>Сейчас приложение ждёт оценку настроения: E — часть ввода.</summary>
    public bool CapturingInput => dir.MoodPending && channel == (int)ChatChannel.Harmony;

    public void Tick()
    {
        if (!dir.Running || dir.ModalActive) return;
        if (dir.ActiveStation != StationIndex) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow)) { channel = (channel + 3) % 4; scroll = 0; }
        if (Input.GetKeyDown(KeyCode.RightArrow)) { channel = (channel + 1) % 4; scroll = 0; }
        if (Input.GetKeyDown(KeyCode.UpArrow)) scroll++;
        if (Input.GetKeyDown(KeyCode.DownArrow)) scroll = Mathf.Max(0, scroll - 1);

        dir.MarkRead((ChatChannel)channel);

        if (dir.MoodPending && channel == (int)ChatChannel.Harmony)
        {
            moodInput.Tick(dir.TypoChance);
            // в оценке настроения допустимы только цифры
            string digits = "";
            for (int i = 0; i < moodInput.text.Length; i++)
                if (char.IsDigit(moodInput.text[i])) digits += moodInput.text[i];
            if (digits.Length > 2) digits = digits.Substring(0, 2);
            moodInput.text = digits;

            bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (enter)
            {
                int v;
                if (int.TryParse(moodInput.text, out v) && v >= 1 && v <= 10)
                {
                    dir.SubmitMood(v);
                    moodInput.Clear();
                    scroll = 0;
                }
                else
                {
                    Flash("ОЦЕНКА: ЦЕЛОЕ ЧИСЛО 1–10.", Err);
                }
            }
        }
        else
        {
            moodInput.Clear();
        }
    }

    void Flash(string text, Color color)
    {
        message = text;
        messageColor = color;
        messageUntil = Time.time + 2.4f;
    }

    public void Render()
    {
        var L = new List<ScreenLine>();

        string header = "ГАНДЕКС · МЕССЕНДЖЕР   " + dir.Clock.Display;
        if (dir.PhoneRinging) header += "   ВХОДЯЩИЙ ЗВОНОК";
        L.Add(new ScreenLine(header, dir.PhoneRinging ? Warn : Dim));

        string tabs = "";
        for (int i = 0; i < 4; i++)
        {
            string name = ChatChannels.Names[i];
            if (dir.Unread[i]) name += "*";
            tabs += (i == channel ? "▸" : " ") + name + (i == channel ? "◂ " : "  ");
        }
        L.Add(new ScreenLine(tabs, White));
        L.Add(new ScreenLine(new string('-', s.Cols), Dim));

        var lines = new List<ScreenLine>();
        var msgs = dir.Messages;
        for (int i = 0; i < msgs.Count; i++)
        {
            if ((int)msgs[i].channel != channel) continue;
            lines.Add(new ScreenLine(msgs[i].sender + " · " + msgs[i].time, Accent));
            List<string> wrapped = Wrap(msgs[i].text, s.Cols - 2);
            for (int w = 0; w < wrapped.Count; w++)
                lines.Add(new ScreenLine("  " + wrapped[w], C));
            lines.Add(new ScreenLine("", C));
        }

        if (msgs.Count != lastMessageCount)
        {
            lastMessageCount = msgs.Count;
            scroll = 0;
        }

        // Нижние две строки зарезервированы под ввод и подсказку; остальное —
        // лента. Считаем от фактического числа строк экрана: при крупном
        // тексте строк меньше, и жёсткая константа обрезала бы ввод.
        int visible = Mathf.Max(1, s.Rows - 5);
        int maxScroll = Mathf.Max(0, lines.Count - visible);
        scroll = Mathf.Clamp(scroll, 0, maxScroll);
        int start = Mathf.Max(0, lines.Count - visible - scroll);
        for (int i = 0; i < visible; i++)
        {
            int idx = start + i;
            L.Add(idx < lines.Count
                ? lines[idx]
                : new ScreenLine("", C));
        }

        // строка вопросов/оценки настроения
        if (dir.MoodPending && channel == (int)ChatChannel.Harmony)
        {
            string shown = moodInput.RejectedRecently
                ? "ОЦЕНКА 1–10: " + moodInput.text + " ▌"
                : "ОЦЕНКА 1–10: " + moodInput.text + (Time.time % 1f < 0.55f ? "_" : " ");
            L.Add(new ScreenLine(shown, moodInput.RejectedRecently ? Err : White, dir.TypeInScale));
        }
        else if (!string.IsNullOrEmpty(message) && Time.time < messageUntil)
        {
            L.Add(new ScreenLine(message, messageColor));
        }
        else
        {
            L.Add(new ScreenLine("", C));
        }

        L.Add(new ScreenLine("←→ каналы  ↑↓ прокрутка  TAB экран", Dim));
        s.Set(L);
    }

    static List<string> Wrap(string text, int width)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(text)) { result.Add(""); return result; }
        int i = 0;
        while (i < text.Length)
        {
            int take = Mathf.Min(width, text.Length - i);
            if (i + take < text.Length)
            {
                int space = text.LastIndexOf(' ', i + take - 1, take);
                if (space > i) take = space - i + 1;
            }
            result.Add(text.Substring(i, take).TrimEnd());
            i += take;
            while (i < text.Length && text[i] == ' ') i++;
        }
        return result;
    }
}
