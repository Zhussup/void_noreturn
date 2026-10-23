using System.Collections.Generic;
using UnityEngine;

public enum TaskKind { Sort, Match, Approve }

public class TaskDef
{
    public TaskKind kind;
    public string title;
    public string prompt;
    public string answer;
    public string[] body;
    public bool done;

    public TaskDef(TaskKind kind, string title, string prompt, string answer, params string[] body)
    {
        this.kind = kind;
        this.title = title;
        this.prompt = prompt;
        this.answer = answer;
        this.body = body;
    }
}

/// <summary>
/// Монитор 1 — GANDEX-OS: логин, официальные мини-игры смены и служебный терминал,
/// через который Брик делает шаги саботажа.
/// Управление только клавиатурой: печать команд, Enter, Esc, стрелки.
/// </summary>
public class TerminalApp
{
    public const int StationIndex = 0;

    enum Mode { Login, LoginOk, Tasks, Sort, Match, Approve, Sabotage, Locked }

    // цвета — из общей палитры экранов (ScreenStyle / ScreenPalette)
    static Color C => ScreenPalette.Terminal.Text;
    static Color Dim => ScreenPalette.Terminal.Dim;
    static Color Lines => ScreenPalette.Terminal.Lines;
    static Color Warn => ScreenPalette.Terminal.Warn;
    static Color Err => ScreenPalette.Terminal.Alarm;
    static Color White => ScreenPalette.Terminal.Void;

    readonly NightDirector dir;
    readonly ScreenSurface s;
    readonly InputField input = new InputField();
    readonly List<string> outLog = new List<string>();

    Mode mode = Mode.Login;
    int selected;
    string message = "";
    Color messageColor = C;
    float messageUntil;

    public bool LoggedIn { get; private set; }

    /// <summary>Сейчас приложение ждёт текст: буква E — часть ввода, не приближение.</summary>
    public bool CapturingInput =>
        mode == Mode.Login || mode == Mode.Sort || mode == Mode.Match ||
        mode == Mode.Approve || mode == Mode.Sabotage;

    public TerminalApp(NightDirector dir, ScreenSurface surface)
    {
        this.dir = dir;
        s = surface;
        outLog.Add("GANDEX-OS 6.2 · служебная оболочка");
        outLog.Add("help — список команд");
    }

    // ------------------------------------------------------------ ввод

    public void Tick()
    {
        if (!dir.Running || dir.ModalActive) return;
        if (dir.ActiveStation != StationIndex) return;

        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        bool esc = Input.GetKeyDown(KeyCode.Escape);

        switch (mode)
        {
            case Mode.Login:
                input.Tick(dir.TypoChance);
                if (enter) SubmitLogin();
                break;

            case Mode.LoginOk:
                if (enter)
                {
                    mode = Mode.Tasks;
                    Flash("Смена открыта.", C);
                }
                break;

            case Mode.Tasks:
                if (Input.GetKeyDown(KeyCode.UpArrow)) { selected--; WrapSel(); }
                if (Input.GetKeyDown(KeyCode.DownArrow)) { selected++; WrapSel(); }
                if (Input.GetKeyDown(KeyCode.T)) OpenSabotage();
                if (enter) OpenSelectedTask();
                break;

            case Mode.Sort:
            case Mode.Match:
            case Mode.Approve:
                input.Tick(dir.TypoChance);
                if (esc) { mode = Mode.Tasks; input.Clear(); }
                else if (enter) SubmitTask();
                break;

            case Mode.Sabotage:
                input.Tick(dir.TypoChance);
                if (esc) { mode = Mode.Tasks; input.Clear(); }
                else if (enter) RunCommand();
                break;
        }
    }

    void WrapSel()
    {
        int n = dir.Tasks.Count;
        if (n == 0) { selected = 0; return; }
        if (selected < 0) selected = n - 1;
        if (selected >= n) selected = 0;
    }

    void SubmitLogin()
    {
        string pass = input.text.Trim().ToLowerInvariant();
        input.Clear();
        if (pass == "brick")
        {
            LoggedIn = true;
            mode = Mode.LoginOk;
            dir.OnLogin();
            Flash("ДОСТУП РАЗРЕШЁН.", C);
        }
        else
        {
            Flash("НЕВЕРНЫЙ ПАРОЛЬ.", Err);
            dir.AddAnxiety(1f);
        }
    }

    void OpenSelectedTask()
    {
        if (selected < 0 || selected >= dir.Tasks.Count) return;
        TaskDef t = dir.Tasks[selected];
        if (t.done) { Flash("Задача уже закрыта.", Dim); return; }
        input.Clear();
        mode = t.kind == TaskKind.Sort ? Mode.Sort
             : t.kind == TaskKind.Match ? Mode.Match
             : Mode.Approve;
    }

    void SubmitTask()
    {
        if (selected < 0 || selected >= dir.Tasks.Count) return;
        TaskDef t = dir.Tasks[selected];
        string answer = input.text.Trim();
        input.Clear();

        if (string.Equals(answer, t.answer, System.StringComparison.OrdinalIgnoreCase))
        {
            t.done = true;
            mode = Mode.Tasks;
            dir.OnTaskCompleted(t);
            Flash("ПРИНЯТО. ЗАДАЧА ЗАКРЫТА.", C);
        }
        else
        {
            dir.AddAnxiety(2f);
            Flash("НЕВЕРНО. ПОВТОРИТЕ.", Err);
        }
    }

    void OpenSabotage()
    {
        mode = Mode.Sabotage;
        input.Clear();
        outLog.Clear();
        outLog.Add("Служебный терминал. Доступ: BRICK.E");
        outLog.Add("Цель: дата-центр 7. help — команды.");
        message = "";
    }

    void RunCommand()
    {
        string cmd = input.text.Trim().ToLowerInvariant();
        input.Clear();
        if (cmd.Length == 0) return;

        outLog.Add("> " + cmd);

        if (cmd == "exit")
        {
            mode = Mode.Tasks;
            return;
        }
        if (cmd == "help")
        {
            outLog.Add("scan            сканировать регион");
            outLog.Add("connect <srv>   подключиться к серверу");
            outLog.Add("devs --all      коды сотрудников");
            outLog.Add("logs            состояние логов");
            outLog.Add("exit            выйти");
        }
        else if (cmd == "scan")
        {
            outLog.Add("СКАНИРОВАНИЕ РЕГИОНА...");
            outLog.Add("УЗЛОВ: 4. ФИЛЬТРАЦИЯ: ДЦ-7.");
            outLog.Add("ДОСТУП: УРОВЕНЬ 3 + 3 КОДА.");
            if (dir.State.planStep < 1)
            {
                dir.State.planStep = 1;
                dir.OnPlanStep("recon");
            }
        }
        else if (cmd == "devs --all")
        {
            outLog.Add("СОТРУДНИКИ: ДЖОЭЛ — " + CodeState(0));
            outLog.Add("             ДЭЛАЙЛА — " + CodeState(1));
            outLog.Add("             КУПЛИНОВ — " + CodeState(2));
        }
        else if (cmd.StartsWith("connect"))
        {
            outLog.Add("ПОДКЛЮЧЕНИЕ...");
            outLog.Add("ОТКАЗАНО. НУЖЕН ДОСТУП ДЦ-7 (3 КОДА).");
        }
        else if (cmd == "logs")
        {
            outLog.Add("ПРОВЕРКА КИБЕРБЕЗОВ: " + dir.CyberCdText);
            outLog.Add("СЛЕДОВ В ЛОГАХ: " + dir.State.suspicion + "%");
        }
        else if (cmd == "clear")
        {
            outLog.Add("НЕЧЕГО ЧИСТИТЬ.");
        }
        else if (cmd == "whoami")
        {
            outLog.Add("BRICK.E · ДИУ-2 · НОЧНАЯ СМЕНА");
        }
        else
        {
            outLog.Add("КОМАНДА НЕ РАСПОЗНАНА.");
        }

        while (outLog.Count > 9) outLog.RemoveAt(0);
        dir.AddAnxiety(0.5f);
    }

    string CodeState(int i)
    {
        return dir.State.codes != null && i < dir.State.codes.Length && dir.State.codes[i]
            ? "ПОЛУЧЕН" : "НЕТ";
    }

    void Flash(string text, Color color)
    {
        message = text;
        messageColor = color;
        messageUntil = Time.time + 2.6f;
    }

    // ------------------------------------------------------------ отрисовка

    public void Render()
    {
        var L = new List<ScreenLine>();
        string header = "GANDEX-OS · ДИУ-2   " + dir.Clock.Display +
                        "   ЗАДАЧ " + dir.TasksDone + "/" + dir.Tasks.Count +
                        "   ПЛАН " + (dir.State.planStep > 0 ? "1/1" : "0/1");
        L.Add(new ScreenLine(header, dir.State.anxiety > 55f ? Warn : Dim));
        L.Add(new ScreenLine(new string('-', s.Cols), Lines));

        switch (mode)
        {
            case Mode.Login: RenderLogin(L); break;
            case Mode.LoginOk: RenderLoginOk(L); break;
            case Mode.Tasks: RenderTasks(L); break;
            case Mode.Sort: RenderTask(L, 0); break;
            case Mode.Match: RenderTask(L, 1); break;
            case Mode.Approve: RenderTask(L, 2); break;
            case Mode.Sabotage: RenderSabotage(L); break;
            case Mode.Locked: L.Add(new ScreenLine("СМЕНА ЗАКРЫТА.", Warn)); break;
        }

        if (!string.IsNullOrEmpty(message) && Time.time < messageUntil)
            L.Add(new ScreenLine(message, messageColor));

        s.Set(L);
    }

    void Blank(List<ScreenLine> L) => L.Add(new ScreenLine("", C));

    void RenderLogin(List<ScreenLine> L)
    {
        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("ОПЕРАТОР: BRICK.E", White));
        L.Add(new ScreenLine("НОЧНАЯ СМЕНА · ДИУ-2", Dim));
        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("ПАРОЛЬ:", C));
        string shown = input.RejectedRecently
            ? "> " + input.Masked() + " ▌"
            : "> " + input.Masked() + (Time.time % 1f < 0.55f ? "_" : " ");
        L.Add(new ScreenLine(shown, input.RejectedRecently ? Err : White));
        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("смена 1 — вход разрешён по паролю", Dim));
        L.Add(new ScreenLine("подсказка: brick", Dim));
        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("[ENTER] войти", C));
    }

    void RenderLoginOk(List<ScreenLine> L)
    {
        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("ДОСТУП РАЗРЕШЁН.", White));
        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("BRICK.E · ДИУ-2 · НОЧНАЯ СМЕНА", C));
        L.Add(new ScreenLine("ЗАДАЧ НА НОЧЬ: " + dir.Tasks.Count, C));
        L.Add(new ScreenLine("ПЛАН: РАЗВЕДКА ДЦ-7", Dim));
        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("07:00 — конец смены. Не закрытые", Dim));
        L.Add(new ScreenLine("задачи — выговор и падение доверия.", Dim));
        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("[ENTER] начать смену", C));
    }

    void RenderTasks(List<ScreenLine> L)
    {
        L.Add(new ScreenLine("ЗАДАЧИ СМЕНЫ", White));
        L.Add(new ScreenLine("", C));
        for (int i = 0; i < dir.Tasks.Count; i++)
        {
            TaskDef t = dir.Tasks[i];
            string mark = i == selected ? "> " : "  ";
            string status = t.done ? "ВЫПОЛНЕНО" : "НЕ ВЫП.";
            string left = mark + (i + 1) + ". " + t.title;
            int pad = Mathf.Max(1, s.Cols - left.Length - status.Length);
            L.Add(new ScreenLine(left + new string(' ', pad) + status,
                                 t.done ? Dim : (i == selected ? White : C)));
        }
        L.Add(new ScreenLine("", C));
        string plan = dir.State.planStep > 0
            ? "ПЛАН: ДЦ-7 НАЙДЕН [1/1]"
            : "ПЛАН: РАЗВЕДКА ДЦ-7 [0/1]";
        L.Add(new ScreenLine(plan, dir.State.planStep > 0 ? C : Warn));
        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("СЛУЖЕБНЫЙ ТЕРМИНАЛ — [T]", Dim));
        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("[↑↓] выбрать   [ENTER] открыть", Dim));
    }

    void RenderTask(List<ScreenLine> L, int kindIndex)
    {
        if (selected < 0 || selected >= dir.Tasks.Count) return;
        TaskDef t = dir.Tasks[selected];
        string num = (selected + 1).ToString();
        L.Add(new ScreenLine("ЗАДАЧА " + num + "/" + dir.Tasks.Count + " · " + t.title, White));
        L.Add(new ScreenLine("", C));
        if (t.body != null)
        {
            for (int i = 0; i < t.body.Length; i++)
                L.Add(new ScreenLine(t.body[i], i == 0 ? C : Dim));
        }
        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine(t.prompt, Warn));
        string shown = input.RejectedRecently
            ? "> " + input.text + " ▌"
            : "> " + input.text + (Time.time % 1f < 0.55f ? "_" : " ");
        L.Add(new ScreenLine(shown, input.RejectedRecently ? Err : White));
        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("[ENTER] принять   [ESC] назад", Dim));
    }

    void RenderSabotage(List<ScreenLine> L)
    {
        L.Add(new ScreenLine("СЛУЖЕБНЫЙ ТЕРМИНАЛ · BRICK.E", White));
        L.Add(new ScreenLine("КД КИБЕРБЕЗОВ: " + dir.CyberCdText +
                             "   СЛЕД: " + dir.State.suspicion + "%",
                             dir.CyberDanger ? Err : Warn));
        L.Add(new ScreenLine(new string('-', s.Cols), Lines));

        // Хвост лога держим в рамках экрана: при крупном тексте строк меньше.
        int maxLog = Mathf.Min(8, Mathf.Max(2, s.Rows - 8));
        int start = Mathf.Max(0, outLog.Count - maxLog);
        for (int i = start; i < outLog.Count; i++)
            L.Add(new ScreenLine(outLog[i], i == outLog.Count - 1 ? White : C));

        L.Add(new ScreenLine("", C));
        string shown = input.RejectedRecently
            ? "> " + input.text + " ▌"
            : "> " + input.text + (Time.time % 1f < 0.55f ? "_" : " ");
        L.Add(new ScreenLine(shown, input.RejectedRecently ? Err : White));
        L.Add(new ScreenLine("[help] команды   [exit] назад", Dim));
    }
}
