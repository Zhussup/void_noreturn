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
        if (dir.TypingStation != StationIndex) return;

        bool typing = dir.MoodPending && channel == (int)ChatChannel.Harmony;

        // Пока строка ввода активна, стрелки принадлежат ей, а не каналам.
        if (!typing)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { channel = (channel + 3) % 4; scroll = 0; }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { channel = (channel + 1) % 4; scroll = 0; }
            if (Input.GetKeyDown(KeyCode.UpArrow)) scroll++;
            if (Input.GetKeyDown(KeyCode.DownArrow)) scroll = Mathf.Max(0, scroll - 1);
        }

        dir.MarkRead((ChatChannel)channel);

        if (typing)
        {
            moodInput.Tick(dir.TypoChance);
            // в оценке настроения допустимы только цифры
            string digits = "";
            for (int i = 0; i < moodInput.text.Length; i++)
                if (char.IsDigit(moodInput.text[i])) digits += moodInput.text[i];
            if (digits.Length > 2) digits = digits.Substring(0, 2);
            if (digits != moodInput.text) moodInput.SetText(digits);
            else moodInput.ClampCaret();

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

    // ------------------------------------------------------------------
    // Статус смены (бывший HUD) — две строки на самом экране мессенджера.
    // По одной строке — вся смена: тревога/бессонница, след/кибербезы/
    // таблетки/загрузка. У границ строка желтеет или краснеет.
    // ------------------------------------------------------------------

    ScreenLine HudAnxiety()
    {
        float a = Mathf.Clamp(dir.State.anxiety, 0f, 100f);
        int p = Mathf.RoundToInt(a);
        string t = "ТРЕВОГА " + Bar(a / 100f, 10) + " " + p + "%";
        if (p >= 75) t += "  100 — КОНЕЦ";
        else if (p >= 55 && dir.State.pills > 0) t += "  1 — ПРИНЯТЬ";
        t += "  БЕССОННИЦА " + Bar(dir.State.insomnia / 5f, 5) + " " + dir.State.insomnia + "/5";
        return new ScreenLine(t, p >= 75 ? Err : p >= 55 ? Warn : Dim);
    }

    ScreenLine HudLog()
    {
        string t = "СЛЕД " + Bar(Mathf.Clamp(dir.State.suspicion, 0f, 100f) / 100f, 10) +
                   " " + Mathf.RoundToInt(dir.State.suspicion) + "%";
        if (dir.State.suspicion >= 75f) t += "  100 — ОРДЕР";
        else if (dir.State.dirtyLogs) t += " ГРЯЗНО";
        t += "  КИБЕРБЕЗЫ " + dir.CyberCdText;
        if (dir.CyberDanger) t += " СЕЙЧАС";
        // Пока идёт загрузка — показываем её; таблетки в это время видны в хинте.
        t += dir.TerminalDownloading
            ? "  ЗАГР " + dir.TerminalDownloadPct + "%"
            : "  ТАБЛ " + dir.State.pills;
        Color c = dir.CyberDanger || dir.State.suspicion >= 75f ? Err
                : dir.State.dirtyLogs ? Warn : Dim;
        return new ScreenLine(t, c);
    }

    /// <summary>Полоска-градусник из блоков: █ — заполнено, ░ — пусто.</summary>
    static string Bar(float frac, int width)
    {
        int fill = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(frac) * width), 0, width);
        return new string('█', fill) + new string('░', width - fill);
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

        // HUD переехал на правый монитор: панель в углу кадра обрезалась
        // окном Game view на 1x, а экран чата виден всегда.
        L.Add(HudAnxiety());
        L.Add(HudLog());

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
        int visible = Mathf.Max(1, s.Rows - 7); // 2 строки съел HUD-статус
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
            bool showCaret = moodInput.RejectedRecently || Time.time % 1f < 0.55f;
            string cursor = moodInput.RejectedRecently ? "▌" : "_";
            string shown = "ОЦЕНКА 1–10: " + moodInput.WithCursor(showCaret, cursor);
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

        // Подсказки станции — на мониторах вместо полосы внизу кадра;
        // пока хинта нет (модалка) — локальная подсказка самого чата.
        L.Add(new ScreenLine(
            dir.Modal == ModalKind.None && !string.IsNullOrEmpty(dir.Hint)
                ? dir.Hint
                : "←→ каналы  ↑↓ прокрутка  B кровать", Dim));
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
