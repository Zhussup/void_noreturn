using System;
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
    /// <summary>После сдачи этой задачи игрок узнаёт коды всех трёх сотрудников.</summary>
    public bool revealsCodes;

    public TaskDef(TaskKind kind, string title, string prompt, string answer, params string[] body)
    {
        this.kind = kind;
        this.title = title;
        this.prompt = prompt;
        this.answer = answer;
        this.body = body;
    }
}

public class ServerFile
{
    public readonly string name;
    public readonly string text;
    public readonly bool target;

    public ServerFile(string name, string text, bool target = false)
    {
        this.name = name;
        this.text = text;
        this.target = target;
    }
}

public class ServerDef
{
    public readonly string owner;
    public readonly string code;
    public readonly ServerFile[] files;

    public ServerDef(string owner, string code, params ServerFile[] files)
    {
        this.owner = owner;
        this.code = code;
        this.files = files;
    }
}

/// <summary>
/// Монитор 1 — GANDEX-OS: логин, официальные мини-игры смены и служебный терминал,
/// через который Брик делает шаги саботажа.
/// Управление только клавиатурой: печать команд, Enter, Ctrl+C, стрелки.
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
    readonly List<string> history = new List<string>();
    int historyPos = -1;       // -1 — текущий черновик, иначе индекс в history
    string historyDraft = "";

    // Серверы сотрудников. Код из мини-задачи открывает соответствующий сервер.
    static readonly ServerDef[] Servers =
    {
        new ServerDef("ДЖОЭЛ", "a-5120",
            new ServerFile("mail_archive.txt", "Почта: отпуск, море, билеты."),
            new ServerFile("printer_log.txt", "Печать отчётов, 14 страниц."),
            new ServerFile("vacation_plan.txt", "План отпуска на две недели."),
            new ServerFile("team_notes.txt", "Смена 2, задачи, кофе."),
            new ServerFile("dc7_route.log", "Маршрут до ДЦ-7: сектор 7, шлюз B.", true),
            new ServerFile("old_backup.txt", "Резервная копия за прошлый месяц."),
            new ServerFile("access_list.txt", "Список пропусков, без изменений."),
            new ServerFile("readme.txt", "Сервер ДЖОЭЛА. Не трогать.")),

        new ServerDef("ДЭЛАЙЛА", "a-4471",
            new ServerFile("photos.txt", "Список фотографий с корпоратива."),
            new ServerFile("meeting_notes.txt", "Заметки встречи, среда."),
            new ServerFile("budget_q3.txt", "Бюджет квартала, черновик."),
            new ServerFile("dc7_backup.key", "Резервный ключ ДЦ-7: копия доступа.", true),
            new ServerFile("vacation_request.txt", "Запрос на отпуск, отклонён."),
            new ServerFile("chat_export.txt", "Экспорт рабочего чата."),
            new ServerFile("camera_log.txt", "Журнал камер, без происшествий."),
            new ServerFile("readme.txt", "Сервер ДЭЛАЙЛЫ. Личное.")),

        new ServerDef("КУПЛИНОВ", "a-6643",
            new ServerFile("games.txt", "Список игр на выходные."),
            new ServerFile("notes.txt", "Заметки, пароли не хранить."),
            new ServerFile("archive_2025.txt", "Архив за 2025 год."),
            new ServerFile("dc7_final.key", "Финальный ключ ДЦ-7: полный доступ.", true),
            new ServerFile("printer_queue.txt", "Очередь печати, 3 задания."),
            new ServerFile("backup_plan.txt", "План резервного копирования."),
            new ServerFile("incident_log.txt", "Инцидент 12.03: ложное срабатывание."),
            new ServerFile("readme.txt", "Сервер КУПЛИНОВА. Не лезть.")),
    };

    int connected = -1;
    string downloadFile;
    float downloadProgress;
    int downloadSpeed = 2;
    float downloadHeat;
    readonly HashSet<string> downloaded = new HashSet<string>();

    public bool Downloading => downloadFile != null;
    public string CurrentDownload => downloadFile;
    public string DownloadStatus =>
        downloadFile != null
            ? downloadFile + "  " + Mathf.RoundToInt(downloadProgress * 100f) + "% · x" + downloadSpeed
            : "";

    ServerDef ConnectedServer =>
        connected >= 0 && connected < Servers.Length ? Servers[connected] : null;

    Mode mode = Mode.Login;
    int selected;
    string message = "";
    Color messageColor = C;
    float messageUntil;

    public bool LoggedIn { get; private set; }

    /// <summary>Задача, открытая сейчас (или выбранная в списке).</summary>
    TaskDef CurrentTask =>
        selected >= 0 && selected < dir.Tasks.Count ? dir.Tasks[selected] : null;

    /// <summary>Сейчас приложение ждёт текст: буква E — часть ввода, не приближение.</summary>
    public bool CapturingInput =>
        mode == Mode.Login || mode == Mode.Sabotage ||
        ((mode == Mode.Sort || mode == Mode.Match || mode == Mode.Approve) &&
         CurrentTask != null && !CurrentTask.done);

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
        // Esc занят глобальными настройками (NightDirector), поэтому «назад» —
        // как в настоящем терминале: Ctrl+C.
        bool back = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    && Input.GetKeyDown(KeyCode.C);

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
                // Выполненную задачу можно открыть и прочитать, но не сдать заново.
                if (CurrentTask != null && CurrentTask.done)
                {
                    if (back) { mode = Mode.Tasks; input.Clear(); }
                    break;
                }
                input.Tick(dir.TypoChance);
                if (back) { mode = Mode.Tasks; input.Clear(); }
                else if (enter) SubmitTask();
                break;

            case Mode.Sabotage:
            {
                string before = input.text;
                input.Tick(dir.TypoChance);
                if (input.text != before) ResetHistoryNav(); // правим черновик
                if (Input.GetKeyDown(KeyCode.UpArrow)) HistoryPrev();
                if (Input.GetKeyDown(KeyCode.DownArrow)) HistoryNext();
                if (back)
                {
                    AbortDownload();
                    connected = -1;
                    mode = Mode.Tasks;
                    input.Clear();
                    ResetHistoryNav();
                }
                else if (enter) RunCommand();
                break;
            }
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
        input.Clear();
        mode = t.kind == TaskKind.Sort ? Mode.Sort
             : t.kind == TaskKind.Match ? Mode.Match
             : Mode.Approve;
    }

    void SubmitTask()
    {
        if (selected < 0 || selected >= dir.Tasks.Count) return;
        TaskDef t = dir.Tasks[selected];
        if (t.done) return; // закрытую задачу только читаем
        string answer = input.text.Trim();
        input.Clear();

        if (string.Equals(answer, t.answer, System.StringComparison.OrdinalIgnoreCase))
        {
            t.done = true;
            if (t.revealsCodes)
            {
                for (int i = 0; i < dir.State.codes.Length; i++)
                    dir.State.codes[i] = true;
                dir.AddChat(ChatChannel.Team, "СИСТЕМА",
                    "КОДЫ ДОСТУПА: a-4471 · a-5120 · a-6643", dir.Clock.Display);
            }
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
        ResetHistoryNav();
        AbortDownload();
        connected = -1;
        outLog.Clear();
        outLog.Add("Служебный терминал. Доступ: BRICK.E");
        outLog.Add("Цель: дата-центр 7. help — команды.");
        message = "";
    }

    void RunCommand()
    {
        string cmd = input.text.Trim().ToLowerInvariant();
        input.Clear();
        ResetHistoryNav();
        if (cmd.Length == 0) return;

        RememberCommand(cmd);
        outLog.Add("> " + cmd);

        if (cmd == "exit")
        {
            if (connected >= 0)
            {
                AbortDownload();
                connected = -1;
                outLog.Add("ОТКЛЮЧЕНО ОТ СЕРВЕРА.");
                while (outLog.Count > 9) outLog.RemoveAt(0);
            }
            else mode = Mode.Tasks;
            return;
        }
        if (cmd == "help")
        {
            if (connected >= 0)
            {
                outLog.Add("ls              файлы сервера");
                outLog.Add("grep \"слово\"    поиск по файлам");
                outLog.Add("download <файл> скачать файл");
                outLog.Add("speed 1-5       скорость загрузки");
                outLog.Add("stop            остановить загрузку");
                outLog.Add("exit            отключиться");
            }
            else
            {
                outLog.Add("scan            сканировать регион");
                outLog.Add("connect <код>   подключиться к серверу");
                outLog.Add("devs --all      коды сотрудников");
                outLog.Add("logs            состояние логов");
                outLog.Add("clear           очистить логи (оставляет след)");
                outLog.Add("exit            выйти");
            }
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
            dir.State.dirtyLogs = true;
            outLog.Add("СОТРУДНИКИ: ДЖОЭЛ — " + CodeState(0));
            outLog.Add("             ДЭЛАЙЛА — " + CodeState(1));
            outLog.Add("             КУПЛИНОВ — " + CodeState(2));
        }
        else if (cmd.StartsWith("connect"))
        {
            string arg = cmd.Substring("connect".Length).Trim();
            int idx = -1;
            for (int i = 0; i < Servers.Length; i++)
                if (string.Equals(Servers[i].code, arg, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }

            if (arg.Length == 0)
                outLog.Add("ИСПОЛЬЗОВАНИЕ: connect <код>  (a-4471, a-5120, a-6643)");
            else if (idx < 0)
                outLog.Add("СЕРВЕР НЕ НАЙДЕН. КОДЫ: a-4471, a-5120, a-6643.");
            else if (dir.State.codes == null || idx >= dir.State.codes.Length || !dir.State.codes[idx])
                outLog.Add("КОД " + Servers[idx].code + " НЕ ПОЛУЧЕН.");
            else
            {
                AbortDownload();
                connected = idx;
                dir.State.dirtyLogs = true;
                outLog.Add("ПОДКЛЮЧЕНО: " + Servers[idx].owner + " · " + Servers[idx].code);
                outLog.Add("ФАЙЛОВ: " + Servers[idx].files.Length + ". Ищите упоминания ДЦ-7.");
            }
        }
        else if (connected >= 0 && (cmd == "ls" || cmd == "files"))
        {
            for (int i = 0; i < Servers[connected].files.Length; i++)
                outLog.Add("  " + Servers[connected].files[i].name);
        }
        else if (connected >= 0 && cmd.StartsWith("grep"))
        {
            string kw = cmd.Substring("grep".Length).Trim().Trim('"', '\'');
            if (kw.Length == 0)
            {
                outLog.Add("ИСПОЛЬЗОВАНИЕ: grep \"слово\"");
            }
            else
            {
                outLog.Add("ПОИСК: \"" + kw + "\"");
                int found = 0;
                for (int i = 0; i < Servers[connected].files.Length; i++)
                {
                    ServerFile f = Servers[connected].files[i];
                    if (f.name.ToLowerInvariant().IndexOf(kw) >= 0 ||
                        f.text.ToLowerInvariant().IndexOf(kw) >= 0)
                    {
                        outLog.Add("  " + f.name);
                        found++;
                    }
                }
                if (found == 0) outLog.Add("НЕТ СОВПАДЕНИЙ.");
                else outLog.Add("СОВПАДЕНИЙ: " + found);
            }
        }
        else if (connected >= 0 && cmd.StartsWith("download"))
        {
            string name = cmd.Substring("download".Length).Trim();
            if (name.Length == 0)
            {
                outLog.Add("ИСПОЛЬЗОВАНИЕ: download <файл>");
            }
            else if (downloadFile != null)
            {
                outLog.Add("УЖЕ СКАЧИВАЕТСЯ: " + downloadFile);
            }
            else
            {
                ServerFile f = FindFile(Servers[connected], name);
                if (f == null) outLog.Add("ФАЙЛ НЕ НАЙДЕН. Используйте ls.");
                else if (downloaded.Contains(connected + ":" + f.name)) outLog.Add("УЖЕ СКАЧАНО: " + f.name);
                else
                {
                    downloadFile = f.name;
                    downloadProgress = 0f;
                    downloadHeat = 0f;
                    outLog.Add("ЗАГРУЗКА: " + f.name + "  СКОРОСТЬ " + downloadSpeed);
                    outLog.Add("КД КИБЕРБЕЗОВ: " + dir.CyberCdText);
                }
            }
        }
        else if (connected >= 0 && cmd == "stop")
        {
            if (downloadFile == null) outLog.Add("НЕЧЕГО ОСТАНАВЛИВАТЬ.");
            else AbortDownload();
        }
        else if (connected >= 0 && cmd.StartsWith("speed"))
        {
            string arg = cmd.Substring("speed".Length).Trim();
            int v;
            if (int.TryParse(arg, out v) && v >= 1 && v <= 5)
            {
                downloadSpeed = v;
                outLog.Add("СКОРОСТЬ ЗАГРУЗКИ: " + v);
            }
            else outLog.Add("ИСПОЛЬЗОВАНИЕ: speed 1-5");
        }
        else if (cmd == "logs")
        {
            outLog.Add("ПРОВЕРКА КИБЕРБЕЗОВ: " + dir.CyberCdText);
            outLog.Add("СЛЕДОВ В ЛОГАХ: " + dir.State.suspicion + "%");
            outLog.Add("СОСТОЯНИЕ: " + (dir.State.dirtyLogs ? "ГРЯЗНЫЕ" : "ЧИСТО"));
        }
        else if (cmd == "clear")
        {
            dir.State.dirtyLogs = true;
            dir.State.suspicion = Mathf.Max(0, dir.State.suspicion - 5);
            outLog.Add("ЛОГИ ОЧИЩЕНЫ. СЛЕД ОСТАЛСЯ: " + dir.State.suspicion + "%");
        }
        else if (cmd == "whoami")
        {
            outLog.Add("BRICK.E · ДИУ-2 · НОЧНАЯ СМЕНА");
        }
        else if (cmd == "ls" || cmd == "files" || cmd.StartsWith("grep") ||
                 cmd.StartsWith("download") || cmd == "stop" || cmd.StartsWith("speed"))
        {
            outLog.Add("СЕРВЕР НЕ ПОДКЛЮЧЕН. Используйте connect <код>.");
        }
        else
        {
            outLog.Add("КОМАНДА НЕ РАСПОЗНАНА.");
        }

        while (outLog.Count > 9) outLog.RemoveAt(0);
        dir.AddAnxiety(0.5f);
    }

    /// <summary>Тик загрузки; вызывается из NightDirector, поэтому идёт даже вне терминала.</summary>
    public void TickDownload(float dt)
    {
        if (downloadFile == null || dir == null) return;

        float rate = 0.045f * downloadSpeed;
        downloadProgress = Mathf.Min(1f, downloadProgress + rate * dt);

        // Высокая скорость оставляет больше следов.
        if (downloadSpeed > 2)
        {
            downloadHeat += (downloadSpeed - 2) * 0.6f * dt;
            int add = Mathf.FloorToInt(downloadHeat);
            if (add > 0)
            {
                downloadHeat -= add;
                dir.State.suspicion = Mathf.Min(100, dir.State.suspicion + add);
            }
        }

        if (downloadProgress >= 1f) CompleteDownload();
    }

    void CompleteDownload()
    {
        string name = downloadFile;
        ServerFile file = FindFile(ConnectedServer, name);
        downloadFile = null;
        downloadProgress = 0f;
        downloadHeat = 0f;
        downloaded.Add(connected + ":" + name);
        outLog.Add("СКАЧАНО: " + name);
        if (file != null && file.target)
        {
            outLog.Add("ЦЕЛЕВОЙ ФАЙЛ ПОЛУЧЕН.");
            dir.ShowSubtitle("Данные получены: " + name, 2.5f);
        }
        while (outLog.Count > 9) outLog.RemoveAt(0);
    }

    public void AbortDownload()
    {
        if (downloadFile == null) return;
        string name = downloadFile;
        downloadFile = null;
        downloadProgress = 0f;
        downloadHeat = 0f;
        outLog.Add("ЗАГРУЗКА ОСТАНОВЛЕНА: " + name);
        while (outLog.Count > 9) outLog.RemoveAt(0);
    }

    static ServerFile FindFile(ServerDef srv, string name)
    {
        if (srv == null || string.IsNullOrEmpty(name)) return null;
        for (int i = 0; i < srv.files.Length; i++)
            if (string.Equals(srv.files[i].name, name, StringComparison.OrdinalIgnoreCase))
                return srv.files[i];
        return null;
    }

    string CodeState(int i)
    {
        bool known = dir.State.codes != null && i < dir.State.codes.Length && dir.State.codes[i];
        return known ? GameState.EmployeeCodes[i] : "НЕТ";
    }

    /// <summary>Запомнить введённую команду (без повторов подряд, не больше 50).</summary>
    void RememberCommand(string cmd)
    {
        if (history.Count == 0 || history[history.Count - 1] != cmd)
            history.Add(cmd);
        if (history.Count > 50) history.RemoveAt(0);
    }

    /// <summary>Сбросить навигацию по истории: текущий текст становится черновиком.</summary>
    void ResetHistoryNav()
    {
        historyPos = -1;
        historyDraft = input.text;
    }

    /// <summary>↑ — предыдущая команда, как в шелле.</summary>
    void HistoryPrev()
    {
        if (history.Count == 0) return;
        if (historyPos == -1)
        {
            historyDraft = input.text;
            historyPos = history.Count - 1;
        }
        else if (historyPos > 0)
        {
            historyPos--;
        }
        input.SetText(history[historyPos]);
    }

    /// <summary>↓ — следующая команда; после последней возвращает черновик.</summary>
    void HistoryNext()
    {
        if (historyPos == -1) return;
        historyPos++;
        if (historyPos >= history.Count)
        {
            historyPos = -1;
            input.SetText(historyDraft);
        }
        else
        {
            input.SetText(history[historyPos]);
        }
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
        bool showCaret = input.RejectedRecently || Time.time % 1f < 0.55f;
        string cursor = input.RejectedRecently ? "▌" : "_";
        string shown = "> " + input.MaskedWithCursor(showCaret, cursor);
        L.Add(new ScreenLine(shown, input.RejectedRecently ? Err : White, dir.TypeInScale));
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

        if (t.done)
        {
            // Закрытая задача открывается только для чтения.
            L.Add(new ScreenLine("ВЫПОЛНЕНО.", Dim));
            L.Add(new ScreenLine("", C));
            L.Add(new ScreenLine("[CTRL+C] назад", Dim));
            return;
        }

        bool showCaret = input.RejectedRecently || Time.time % 1f < 0.55f;
        string cursor = input.RejectedRecently ? "▌" : "_";
        string shown = "> " + input.WithCursor(showCaret, cursor);
        L.Add(new ScreenLine(shown, input.RejectedRecently ? Err : White, dir.TypeInScale));
        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("[ENTER] принять   [CTRL+C] назад", Dim));
    }

    void RenderSabotage(List<ScreenLine> L)
    {
        L.Add(new ScreenLine("СЛУЖЕБНЫЙ ТЕРМИНАЛ · BRICK.E", White));
        L.Add(new ScreenLine("КД КИБЕРБЕЗОВ: " + dir.CyberCdText +
                             "   СЛЕД: " + dir.State.suspicion + "%",
                             dir.CyberDanger ? Err : Warn));

        string server = connected >= 0
            ? "СЕРВЕР: " + Servers[connected].owner + " · " + Servers[connected].code
            : "СЕРВЕР: НЕ ПОДКЛЮЧЕН";
        if (downloadFile != null)
            server += "   ЗАГРУЗКА " + Mathf.RoundToInt(downloadProgress * 100f) + "% x" + downloadSpeed;
        L.Add(new ScreenLine(server, downloadFile != null ? Warn : Dim));
        L.Add(new ScreenLine(new string('-', s.Cols), Lines));

        // Хвост лога держим в рамках экрана: при крупном тексте строк меньше.
        int maxLog = Mathf.Min(8, Mathf.Max(2, s.Rows - 10));
        int start = Mathf.Max(0, outLog.Count - maxLog);
        for (int i = start; i < outLog.Count; i++)
            L.Add(new ScreenLine(outLog[i], i == outLog.Count - 1 ? White : C));

        L.Add(new ScreenLine("", C));
        bool showCaret = input.RejectedRecently || Time.time % 1f < 0.55f;
        string cursor = input.RejectedRecently ? "▌" : "_";
        string shown = "> " + input.WithCursor(showCaret, cursor);
        L.Add(new ScreenLine(shown, input.RejectedRecently ? Err : White, dir.TypeInScale));

        if (connected >= 0)
        {
            L.Add(new ScreenLine("[ls] файлы  [grep \"дц-7\"]  [download <файл>]", Dim));
            L.Add(new ScreenLine("[speed 1-5]  [stop]  [exit] — отключиться", Dim));
        }
        else
        {
            L.Add(new ScreenLine("[connect <код>]  [exit]/[CTRL+C] назад", Dim));
        }
    }
}
