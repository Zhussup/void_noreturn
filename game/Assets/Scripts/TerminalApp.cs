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

public class NoteEntry
{
    public readonly string title;
    public readonly string[] body;

    public NoteEntry(string title, params string[] body)
    {
        this.title = title;
        this.body = body;
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

    enum Mode
    {
        Login, LoginOk, Desktop, Browser, Notes, OsMessenger, Logs,
        Tasks, Sort, Match, Approve, Sabotage, Locked
    }

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

    // Рабочий стол: шесть приложений.
    static readonly string[] AppNames =
        { "БРАУЗЕР", "ЧАТЫ", "ЗАМЕТКИ", "ЛОГИ", "РАБОТА", "ТЕРМ." };
    static readonly string[] AppHints =
        { "теневой интернет", "мессенджер", "заметки и лор", "след и очистка", "задачи смены", "серверы" };

    static readonly NoteEntry[] Notes =
    {
        new NoteEntry("ГАНДЕКС · 2087",
            "Корпорация контролирует сеть,",
            "фильтрует запросы и пишет",
            "доносы на неудобных людей.",
            "Ночная смена — единственное",
            "окно, за которым нет глаз.",
            "Но логи помнят всё."),
        new NoteEntry("БЕССОННИЦА",
            "С каждой ночью опечаток",
            "больше. Тревога растёт сама,",
            "а при 100% сердце не выдержит.",
            "Таблетка снижает тревогу,",
            "сон — тоже, но крадёт время.",
            "Не доводи до края."),
        new NoteEntry("VOID",
            "Вторая личность Брика.",
            "Считает, что пустота честнее",
            "корпорации. Просыпается после",
            "звонка. Его сообщения могут",
            "появиться в твоём же чате.",
            "Не верь всему, что видишь."),
        new NoteEntry("ДЦ-7",
            "Дата-центр, который нужно уронить.",
            "Три кода доступа: Джоэл, Дэлайла,",
            "Куплинов. Файлы лежат на их",
            "серверах. Ищи упоминания ДЦ-7",
            "через grep, скачивай и продавай",
            "на теневом рынке."),
        new NoteEntry("ЧИСТЫЕ ЛОГИ",
            "Подключение к серверу и запрос",
            "кодов пачкают логи. Приложение",
            "ЛОГИ снимает флаг грязных логов",
            "и снижает след на 10%.",
            "Делай это до проверки кибербезов.",
            "Иначе тревога вырастет."),
        new NoteEntry("ТАБЛЕТКИ",
            "Клавиша 1 за столом (или E, наведя",
            "прицел на блистер). Одна таблетка",
            "снимает 30% тревоги.",
            "",
            "Принимай, когда тревога 55% и выше:",
            "строка ТРЕВОГА на экране чата",
            "желтеет и подсказывает 1 — ПРИНЯТЬ.",
            "Не жди 100% — сердце остановится.",
            "Остаток виден в чате: ТАБЛ n."),
        new NoteEntry("УПРАВЛЕНИЕ",
            "МЫШЬ — обзор вокруг рабочего места.",
            "ПЕЧАТЬ — за столом: прицел ближе к",
            "  монитору — печать идёт в него",
            "  (терминал или чат).",
            "F1–F4 — станция напрямую, повторное",
            "  нажатие — взгляд в центр.",
            "ALT (удерживать) — зум, работает",
            "  и во время ввода текста.",
            "B — кровать; там E (удерживать) —",
            "  отдых, B — вернуться к терминалу.",
            "1 — таблетка, 2 — диктофон (за",
            "  столом; то же по E с прицелом).",
            "ESC — настройки (размер текста",
            "  экранов), повторно — закрыть.",
            "В чате: ←→ каналы, ↑↓ прокрутка.",
            "В браузере: ↑↓ + ENTER.",
            "R — переиграть смену после провала",
            "  или отчёта; E на отчёте —",
            "  следующая ночь.",
            "F9 — дев-меню читов (только",
            "  в сборке разработки)."),
    };

    int connected = -1;
    string downloadFile;
    float downloadProgress;
    int downloadSpeed = 2;
    float downloadHeat;
    readonly HashSet<string> downloaded = new HashSet<string>();

    int desktopSel;
    int browserPage;       // 0 — старт, 1 — рынок, 2 — справка, 3 — форум ТЕРРА,
                           // 4 — статус ДЦ и 5 — новости (ночь 3)
    int browserSel;
    string browserMsg = "";
    float browserMsgUntil;
    int noteSel;
    int noteOpen = -1;
    int noteScroll;
    int osChannel;
    int osScroll;
    string logsMsg = "";
    float logsMsgUntil;

    public bool Downloading => downloadFile != null;
    public int DownloadPct =>
        downloadFile != null ? Mathf.RoundToInt(downloadProgress * 100f) : 0;
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
        if (dir.TypingStation != StationIndex) return;

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
                    mode = Mode.Desktop;
                    Flash("Рабочий стол загружен.", C);
                }
                break;

            case Mode.Desktop:
                if (Input.GetKeyDown(KeyCode.LeftArrow)) desktopSel = WrapIndex(desktopSel - 1, AppNames.Length);
                if (Input.GetKeyDown(KeyCode.RightArrow)) desktopSel = WrapIndex(desktopSel + 1, AppNames.Length);
                if (Input.GetKeyDown(KeyCode.UpArrow)) desktopSel = WrapIndex(desktopSel - 3, AppNames.Length);
                if (Input.GetKeyDown(KeyCode.DownArrow)) desktopSel = WrapIndex(desktopSel + 3, AppNames.Length);
                if (enter) OpenDesktopApp();
                break;

            case Mode.Browser:
                // Форум принимает реплику сразу: выбор перекрывает навигацию.
                if (browserPage != 3 && dir.ChoiceActive) browserPage = 3;
                if (back)
                {
                    if (browserPage != 0) browserPage = 0;
                    else mode = Mode.Desktop;
                    break;
                }
                if (browserPage == 0)
                {
                    List<int> pages = BrowserPages();
                    if (Input.GetKeyDown(KeyCode.UpArrow))
                        browserSel = WrapIndex(browserSel - 1, pages.Count);
                    if (Input.GetKeyDown(KeyCode.DownArrow))
                        browserSel = WrapIndex(browserSel + 1, pages.Count);
                    if (enter)
                    {
                        browserPage = pages[Mathf.Clamp(browserSel, 0, pages.Count - 1)];
                        browserSel = 0;
                    }
                }
                else if (browserPage == 1)
                {
                    int files = dir.State.filesTaken.Count;
                    if (files > 0)
                    {
                        if (Input.GetKeyDown(KeyCode.UpArrow)) browserSel = WrapIndex(browserSel - 1, files);
                        if (Input.GetKeyDown(KeyCode.DownArrow)) browserSel = WrapIndex(browserSel + 1, files);
                        if (enter) SellSelectedData();
                    }
                    else browserSel = 0;
                }
                else if (browserPage == 3)
                {
                    if (dir.ChoiceActive)
                    {
                        int n = dir.ChoiceOptions.Length;
                        if (Input.GetKeyDown(KeyCode.UpArrow)) dir.ChoiceIndex = WrapIndex(dir.ChoiceIndex - 1, n);
                        if (Input.GetKeyDown(KeyCode.DownArrow)) dir.ChoiceIndex = WrapIndex(dir.ChoiceIndex + 1, n);
                        // цифры 1..9 — прямой выбор реплики
                        for (int k = 0; k < Mathf.Min(n, 9); k++)
                            if (Input.GetKeyDown(KeyCode.Alpha1 + k)) dir.ChoiceIndex = k;
                        if (enter) dir.SubmitChoice();
                        break; // CTRL+C во время выбора не работает: ТЕРРА ждёт
                    }
                    int offers = dir.TerraOffers.Count;
                    if (offers > 0)
                    {
                        if (Input.GetKeyDown(KeyCode.UpArrow)) browserSel = WrapIndex(browserSel - 1, offers);
                        if (Input.GetKeyDown(KeyCode.DownArrow)) browserSel = WrapIndex(browserSel + 1, offers);
                        if (enter) SellTerraSelected();
                    }
                    else browserSel = 0;
                }
                else if (enter) browserPage = 0;
                break;

            case Mode.Notes:
                if (noteOpen < 0)
                {
                    if (back) { mode = Mode.Desktop; break; }
                    if (Input.GetKeyDown(KeyCode.UpArrow)) noteSel = WrapIndex(noteSel - 1, Notes.Length);
                    if (Input.GetKeyDown(KeyCode.DownArrow)) noteSel = WrapIndex(noteSel + 1, Notes.Length);
                    if (enter) { noteOpen = noteSel; noteScroll = 0; }
                }
                else
                {
                    if (back || enter) { noteOpen = -1; break; }
                    if (Input.GetKeyDown(KeyCode.UpArrow)) noteScroll = Mathf.Max(0, noteScroll - 1);
                    if (Input.GetKeyDown(KeyCode.DownArrow)) noteScroll++;
                }
                break;

            case Mode.OsMessenger:
                if (back) { mode = Mode.Desktop; break; }
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    osChannel = WrapIndex(osChannel - 1, ChatChannels.Names.Length);
                    osScroll = 0;
                }
                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    osChannel = WrapIndex(osChannel + 1, ChatChannels.Names.Length);
                    osScroll = 0;
                }
                if (Input.GetKeyDown(KeyCode.UpArrow)) osScroll++;
                if (Input.GetKeyDown(KeyCode.DownArrow)) osScroll = Mathf.Max(0, osScroll - 1);
                dir.MarkRead((ChatChannel)osChannel);
                break;

            case Mode.Logs:
                if (back) { mode = Mode.Desktop; break; }
                if (enter)
                {
                    if (dir.CleanLogs())
                    {
                        logsMsg = "ЛОГИ ОЧИЩЕНЫ. СЛЕД: " + dir.State.suspicion + "%";
                        logsMsgUntil = Time.time + 4f;
                    }
                    else
                    {
                        logsMsg = "КД ОЧИСТКИ: " + Mathf.CeilToInt(dir.LogCleanCooldown) + " c.";
                        logsMsgUntil = Time.time + 2f;
                    }
                }
                break;

            case Mode.Tasks:
                if (back) { mode = Mode.Desktop; break; }
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
                    mode = Mode.Desktop;
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
            else mode = Mode.Desktop;
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
                outLog.Add("clear           очистить логи (КД 30 с)");
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
            // После взрыва серверов больше нет.
            if (dir.exploded)
            {
                outLog.Add("СЕТЬ ПОГИБЛА. СОЕДИНЕНИЙ НЕТ.");
                while (outLog.Count > 9) outLog.RemoveAt(0);
                return;
            }

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
            if (dir.CleanLogs())
                outLog.Add("ЛОГИ ОЧИЩЕНЫ. СЛЕД: " + dir.State.suspicion + "%");
            else
                outLog.Add("ОЧИСТКА НЕДОСТУПНА: КД " + Mathf.CeilToInt(dir.LogCleanCooldown) + " c.");
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

        // После взрыва сети нет: загрузка обрывается сама.
        if (dir.exploded)
        {
            string dead = downloadFile;
            downloadFile = null;
            downloadProgress = 0f;
            downloadHeat = 0f;
            outLog.Add("СЕТЬ ПОГИБЛА: " + dead + " недоступен.");
            while (outLog.Count > 9) outLog.RemoveAt(0);
            return;
        }

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
            if (dir.HasSoldFile(name))
            {
                // Уже продан (ночь 2+): перепродажа того же файла невозможна.
                outLog.Add("ФАЙЛ УЖЕ ПРОДАН. ТЕРМИНАЛ МОЛЧИТ.");
            }
            else
            {
                dir.RegisterStolenFile(name);
                outLog.Add("ЦЕЛЕВОЙ ФАЙЛ ПОЛУЧЕН.");
                dir.ShowSubtitle("Данные получены: " + name, 2.5f);
            }
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

        // Главное меню и настройки — дизгетические: на экране терминала,
        // вместо панелей поверх кадра (те обрезались окном Game view).
        if (dir.Modal == ModalKind.MainMenu) { RenderMainMenu(L); s.Set(L); return; }
        if (dir.Modal == ModalKind.Settings) { RenderSettings(L); s.Set(L); return; }
        // Отчёт смены и проигрыши — тоже дизгетические: на терминальном
        // мониторе, а не панелью IMGUI поверх кадра.
        if (dir.Modal == ModalKind.End || dir.Modal == ModalKind.Death)
        { RenderReport(L); s.Set(L); return; }

        string header = "GANDEX-OS · ДИУ-2   " + dir.Clock.Display +
                        "   ЗАДАЧ " + dir.TasksDone + "/" + dir.Tasks.Count;
        L.Add(new ScreenLine(header, dir.State.anxiety > 55f ? Warn : Dim));
        L.Add(new ScreenLine(new string('-', s.Cols), Lines));

        switch (mode)
        {
            case Mode.Login: RenderLogin(L); break;
            case Mode.LoginOk: RenderLoginOk(L); break;
            case Mode.Desktop: RenderDesktop(L); break;
            case Mode.Browser: RenderBrowser(L); break;
            case Mode.Notes: RenderNotes(L); break;
            case Mode.OsMessenger: RenderOsMessenger(L); break;
            case Mode.Logs: RenderLogs(L); break;
            case Mode.Tasks: RenderTasks(L); break;
            case Mode.Sort: RenderTask(L, 0); break;
            case Mode.Match: RenderTask(L, 1); break;
            case Mode.Approve: RenderTask(L, 2); break;
            case Mode.Sabotage: RenderSabotage(L); break;
            case Mode.Locked: L.Add(new ScreenLine("СМЕНА ЗАКРЫТА.", Warn)); break;
        }

        if (!string.IsNullOrEmpty(message) && Time.time < messageUntil)
            L.Add(new ScreenLine(message, messageColor));

        // Подсказка станции — строкой на мониторе вместо полосы внизу кадра.
        if (dir.Modal == ModalKind.None && !string.IsNullOrEmpty(dir.Hint))
            L.Add(new ScreenLine(dir.Hint, Dim));

        s.Set(L);
    }

    // ------------------------------------------------------------------
    // Главное меню и настройки — прямо в GANDEX-OS (как всё остальное UI).
    // ------------------------------------------------------------------

    void RenderMainMenu(List<ScreenLine> L)
    {
        L.Add(new ScreenLine("VOID: NO RETURN", White, 1.7f));
        L.Add(new ScreenLine("ночная смена · ГАНДЕКС · ДИУ-2", Dim));
        L.Add(new ScreenLine("", C));
        string[] items = dir.MainMenuItems;
        for (int i = 0; i < items.Length; i++)
        {
            bool sel = i == dir.MenuIndex;
            L.Add(new ScreenLine((sel ? "▸ " : "   ") + items[i], sel ? White : C));
        }
        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("↑↓ — выбрать   E или ENTER — подтвердить", Dim));
    }

    void RenderSettings(List<ScreenLine> L)
    {
        L.Add(new ScreenLine("НАСТРОЙКИ", White, 1.4f));
        L.Add(new ScreenLine(new string('-', s.Cols), Lines));
        L.Add(new ScreenLine(
            "▸ РАЗМЕР ТЕКСТА ЭКРАНОВ   ‹" +
            Mathf.RoundToInt(dir.UiScale * 100f) + "%›", White));
        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("←→ — размер   ESC — " +
            (dir.SettingsFromMenu ? "в главное меню" : "вернуться в смену"), Dim));
    }

    /// <summary>Отчёт смены / проигрыш — на мониторе, как главное меню.</summary>
    void RenderReport(List<ScreenLine> L)
    {
        L.Add(new ScreenLine(dir.EndTitle, White, 1.4f));
        L.Add(new ScreenLine(new string('-', s.Cols), Lines));
        for (int i = 0; i < dir.EndLines.Length; i++)
        {
            string t = dir.EndLines[i];
            if (t.Length == 0) { L.Add(new ScreenLine("", C)); continue; }
            // строки-клавиши внизу отчёта приглушаем, тело — обычным цветом
            bool hint = t.StartsWith("R —") || t.StartsWith("E —");
            L.Add(new ScreenLine(t, hint ? Dim : C));
        }
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
        // План смены по ночам — у игрока должен быть ориентир с первого экрана.
        L.Add(new ScreenLine(dir.State.night == 2
            ? "ПЛАН: в 00:15 ТЕРРА выйдет на связь — браузер"
            : dir.State.night >= 3
                ? "ПЛАН: до 06:00 продать всё ТЕРРА (браузер)"
                : "ПЛАН: РАЗВЕДКА ДЦ-7", Dim));
        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("07:00 — конец смены. Не закрытые", Dim));
        L.Add(new ScreenLine("задачи — выговор и падение доверия.", Dim));
        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("[ENTER] рабочий стол", C));
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
        L.Add(new ScreenLine("[↑↓] выбрать   [ENTER] открыть   [CTRL+C] стол", Dim));
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
            L.Add(new ScreenLine("[connect <код>]  [exit]/[CTRL+C] стол", Dim));
        }
    }

    // ------------------------------------------------------------ рабочий стол и приложения

    void RenderDesktop(List<ScreenLine> L)
    {
        string status = "ТРЕВОГА " + Mathf.RoundToInt(dir.State.anxiety) + "%" +
                        "   СЛЕД " + dir.State.suspicion + "%" +
                        "   КР " + dir.State.credits +
                        "   ЛОГИ: " + (dir.State.dirtyLogs ? "ГРЯЗНЫЕ" : "ЧИСТО");
        // С ночи 2 ТЕРРА — часть смены: в столе виден счёт целевых файлов.
        if (dir.State.night >= 2) status += "   ПРОДАНО " + dir.SoldTargets() + "/3";
        L.Add(new ScreenLine(status, dir.State.dirtyLogs ? Warn : Dim));

        int tileW = Mathf.Max(12, (s.Cols - 2) / 3);
        string top = "┌" + new string('─', tileW - 2) + "┐";
        string bot = "└" + new string('─', tileW - 2) + "┘";

        for (int row = 0; row < 2; row++)
        {
            L.Add(new ScreenLine(top + " " + top + " " + top, Lines));

            string mid = "";
            for (int col = 0; col < 3; col++)
            {
                int i = row * 3 + col;
                mid += "│" + Pad((desktopSel == i ? "▸ " : "  ") + AppNames[i], tileW - 2) + "│";
                if (col < 2) mid += " ";
            }
            L.Add(new ScreenLine(mid, (desktopSel / 3) == row ? White : C));

            L.Add(new ScreenLine(bot + " " + bot + " " + bot, Lines));
        }

        L.Add(new ScreenLine("▸ " + AppNames[desktopSel] + " — " + AppHints[desktopSel], White));
        L.Add(new ScreenLine("↑↓←→ — выбор   ENTER — открыть", Dim));
    }

    void RenderBrowser(List<ScreenLine> L)
    {
        if (browserPage == 0)
        {
            List<int> pages = BrowserPages();
            L.Add(new ScreenLine("БРАУЗЕР · GANDEX NET", White));
            L.Add(new ScreenLine("", C));
            for (int i = 0; i < pages.Count; i++)
            {
                bool sel = browserSel == i || browserSel >= pages.Count;
                L.Add(new ScreenLine((sel ? "▸ " : "  ") +
                                     "[" + pages[i] + "] " + BrowserPageTitle(pages[i]),
                                     sel ? White : C));
            }
            L.Add(new ScreenLine("", C));
            L.Add(new ScreenLine("ENTER — открыть   CTRL+C — назад", Dim));
            return;
        }

        if (browserPage == 3)
        {
            RenderTerra(L);
            return;
        }

        if (browserPage == 1)
        {
            L.Add(new ScreenLine("БРАУЗЕР · ТЕНЕВОЙ РЫНОК   КР " + dir.State.credits, White));
            L.Add(new ScreenLine("", C));

            int n = dir.State.filesTaken.Count;
            if (n == 0)
            {
                L.Add(new ScreenLine("НЕТ ДАННЫХ НА ПРОДАЖУ.", Dim));
                L.Add(new ScreenLine("Скачайте целевые файлы через ТЕРМ.", Dim));
            }
            else
            {
                int maxRows = Mathf.Max(1, s.Rows - 6);
                int start = Mathf.Max(0, browserSel - maxRows + 1);
                for (int i = start; i < n && i < start + maxRows; i++)
                {
                    string name = dir.State.filesTaken[i];
                    string line = (i == browserSel ? "▸ " : "  ") + name +
                                  "   " + MarketPrice(name) + " кр.";
                    L.Add(new ScreenLine(line, i == browserSel ? White : C));
                }
            }

            if (!string.IsNullOrEmpty(browserMsg) && Time.time < browserMsgUntil)
                L.Add(new ScreenLine(browserMsg, C));
            else
                L.Add(new ScreenLine("", C));
            L.Add(new ScreenLine("ENTER — продать   CTRL+C — назад", Dim));
            return;
        }

        // Ночь 3: статус дата-центров и новости.
        if (browserPage == 4)
        {
            RenderDCStatus(L);
            return;
        }
        if (browserPage == 5)
        {
            RenderNews(L);
            return;
        }

        L.Add(new ScreenLine("БРАУЗЕР · КАК ЭТО РАБОТАЕТ", White));
        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("1. ТЕРМ. → connect <код>", C));
        L.Add(new ScreenLine("2. grep \"дц-7\" → целевой файл", C));
        L.Add(new ScreenLine("3. download <файл>", C));
        L.Add(new ScreenLine("4. Здесь продайте данные.", C));
        L.Add(new ScreenLine("Скорость выше 2 увеличивает след.", Dim));
        L.Add(new ScreenLine("Не попадитесь на проверке.", Dim));
        L.Add(new ScreenLine("ENTER / CTRL+C — назад", Dim));
    }

    /// <summary>Страница 3: теневой форум ТЕРРА — лента, офферы, выбор реплики.</summary>
    void RenderTerra(List<ScreenLine> L)
    {
        bool choice = dir.ChoiceActive;
        L.Add(new ScreenLine("БРАУЗЕР · ТЕНЕВОЙ ФОРУМ «ТЕРРА»   КР " + dir.State.credits, White));
        L.Add(new ScreenLine("", C));

        // Лента: последние посты, сколько влезает.
        int maxPosts = choice ? 3 : Mathf.Max(2, (s.Rows - 9) / 2);
        int shown = 0;
        for (int i = dir.TerraPosts.Count - 1; i >= 0 && shown < maxPosts; i--, shown++)
        {
            ForumPost p = dir.TerraPosts[i];
            L.Add(new ScreenLine("  " + p.sender + " · " + p.time, Dim));
            L.Add(new ScreenLine("  " + p.text, C));
        }
        if (dir.TerraPosts.Count == 0)
            L.Add(new ScreenLine("  лента пуста. пока никто не пишет.", Dim));

        L.Add(new ScreenLine(new string('─', Mathf.Max(2, s.Cols - 1)), Lines));

        if (choice)
        {
            L.Add(new ScreenLine("ТЕРРА ЖДЁТ ОТВЕТА:", White));
            for (int i = 0; i < dir.ChoiceOptions.Length; i++)
            {
                bool sel = dir.ChoiceIndex == i;
                L.Add(new ScreenLine((sel ? "▸ " : "  ") + "[" + (i + 1) + "] " + dir.ChoiceOptions[i],
                                     sel ? White : C));
            }
            L.Add(new ScreenLine("↑↓ / 1.." + dir.ChoiceOptions.Length + " — реплика   ENTER — ответить", Dim));
            return;
        }

        int offers = dir.TerraOffers.Count;
        if (offers == 0)
        {
            L.Add(new ScreenLine("ОФФЕРОВ НЕТ. ЖДИ ПОКУПАТЕЛЕЙ.", Dim));
        }
        else
        {
            int maxRows = Mathf.Max(1, s.Rows - 9);
            int start = Mathf.Max(0, browserSel - maxRows + 1);
            for (int i = start; i < offers && i < start + maxRows; i++)
            {
                TerraOffer o = dir.TerraOffers[i];
                string name = string.IsNullOrEmpty(o.file) ? "любой файл" : o.file;
                string line = (i == browserSel ? "▸ " : "  ") + o.buyer + " · " + name +
                              " · " + o.price + " кр." + (o.requiresClean ? " · нужен чистый" : "");
                L.Add(new ScreenLine(line, o.done ? Dim : (i == browserSel ? White : C)));
            }
        }

        if (!string.IsNullOrEmpty(browserMsg) && Time.time < browserMsgUntil)
            L.Add(new ScreenLine(browserMsg, C));
        else
            L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("↑↓ офферы   ENTER — продать   CTRL+C — назад", Dim));
    }

    class DcLine
    {
        public int minute; // игровая минута смены, с которой строка видна
        public string text;
        public bool alarm;
        public DcLine(int minute, string text, bool alarm = false)
        {
            this.minute = minute;
            this.text = text;
            this.alarm = alarm;
        }
    }

    // СТАТУС ДЦ (ночь 3, страница 4). ДЦ-7 — «свой»: строки зависят от того,
    // сколько целевых файлов продано (dir.SoldTargets).
    static readonly DcLine[] Dc5Rows =
    {
        new DcLine(5, "СЕКТОР A: норма · 62 ТБ/ч"),
        new DcLine(80, "СЕКТОР B: +11% нагрузки"),
        new DcLine(170, "ВЕНТИЛЯЦИЯ: предупреждение"),
        new DcLine(260, "ВЕНТИЛЯЦИЯ: аварийный контур"),
        new DcLine(355, "ВЛАЖНОСТЬ 43% · РАСХОЖДЕНИЕ", true),
        new DcLine(405, "ПОЖАРНЫЙ КОНТУР: ЗАПРОС К СОСЕДЯМ", true),
    };

    static readonly DcLine[] Dc11Rows =
    {
        new DcLine(10, "ВЕНТИЛЯЦИЯ: шум жалоб"),
        new DcLine(150, "ЗАМЕНА ВЕНТИЛЯЦИИ: ОТКЛОНЕНА"),
        new DcLine(240, "ВЛАЖНОСТЬ: в норме"),
        new DcLine(330, "ШУМ: «будто умирает»"),
        new DcLine(390, "ПРОСЛУШКА: сирены внутри", true),
    };

    /// <summary>Страница 4: три дата-центра; строки появляются под время смены.</summary>
    void RenderDCStatus(List<ScreenLine> L)
    {
        L.Add(new ScreenLine("БРАУЗЕР · СТАТУС ДЦ   " + dir.Clock.Display, White));
        L.Add(new ScreenLine("", C));

        int m = dir.Clock.WholeMinutes;
        bool down = dir.exploded;

        RenderDc(L, "ДЦ-5 · ЕКАТЕРИНБУРГ", Dc5Rows, m, down);
        RenderDc7(L, m, down);
        RenderDc(L, "ДЦ-11 · МАГНИТОГОРСК", Dc11Rows, m, down);

        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine(down ? "СВЯЗЬ С ПОЛИГОНОМ ПОТЕРЯНА." : "обновление: раз в минуту", Dim));
        L.Add(new ScreenLine("CTRL+C — к списку страниц", Dim));
    }

    static void RenderDc(List<ScreenLine> L, string title, DcLine[] rows, int m, bool down)
    {
        if (down)
        {
            L.Add(new ScreenLine(title, Err));
            L.Add(new ScreenLine("  ███ НЕДОСТУПНО ███", Err));
            return;
        }
        L.Add(new ScreenLine(title, White));
        for (int i = 0; i < rows.Length; i++)
            if (rows[i].minute <= m)
                L.Add(new ScreenLine("  " + rows[i].text, rows[i].alarm ? Warn : C));
    }

    void RenderDc7(List<ScreenLine> L, int m, bool down)
    {
        if (down)
        {
            L.Add(new ScreenLine("ДЦ-7 · ДИУ-2", Err));
            L.Add(new ScreenLine("  ███ НЕДОСТУПНО ███", Err));
            return;
        }

        int sold = dir.SoldTargets();
        L.Add(new ScreenLine("ДЦ-7 · ДИУ-2 · ПРОСРОЧЕНА", White));
        if (m >= 5) L.Add(new ScreenLine("  СЕКТОР 7: шлюз B · активен", C));
        if (m >= 100)
            L.Add(new ScreenLine(sold > 0 ? "  ВНИМАНИЕ: ЧУЖОЙ ТРАФИК В ШЛЮЗЕ" : "  ТРАФИК: в норме",
                                 sold > 0 ? Warn : C));
        if (m >= 200 && sold >= 2)
            L.Add(new ScreenLine("  КОНТУР: ДЕРЕЙСТВОВАН НА 2/3", Warn));
        if (m >= 260 && sold >= 3)
            L.Add(new ScreenLine("  КОНТУР ОТКРЫТ. УПРАВЛЕНИЕ: ЧУЖОЕ.", Warn));
        if (m >= 340)
            L.Add(new ScreenLine("  ЛИЦЕНЗИЯ: ПРОСРОЧЕНА · ПРОДЛЕНИЕ ОТКЛОНЕНО", Warn));
        if (m >= 400)
            L.Add(new ScreenLine("  ТЕХ ПЕРСОНАЛ: НЕ ЯВКА", Warn));
    }

    class NewsRow
    {
        public int minute;
        public string text;
        public NewsRow(int minute, string text)
        {
            this.minute = minute;
            this.text = text;
        }
    }

    // НОВОСТИ (ночь 3, страница 5). Функция времени: минута пришла — новость есть.
    static readonly NewsRow[] NewsRows =
    {
        new NewsRow(8, "ГАНДЕКС НЕЙРО: «ночь — время чистых логов»"),
        new NewsRow(50, "КУРС ДОШИРАКА +4% · ГАНДЕКС-ИНДЕКС"),
        new NewsRow(110, "ГАНДЕКС ГОТОВИТ СПИСКИ «ТЕРРА»"),
        new NewsRow(160, "ВИКТОР Ч. ПРОДАЛ ДОЛЮ В ДЦ-5 ЗА ДОШИРАК"),
        new NewsRow(210, "ДЦ-7: ПЕРЕРАСЧЁТ ЛИЦЕНЗИЙ (ПРОСРОЧКА)"),
        new NewsRow(270, "ПОЖАРНЫЕ ВЕРТОЛЁТЫ ПАТРУЛИРУЮТ МАГНИТОГОРСК"),
        new NewsRow(340, "СТАЖЁРЫ ДИУ-2: «МИГАЮЩИЙ СВЕТ»"),
        new NewsRow(390, "СЕТЬ СБОИТ В ТРЁХ УЗЛАХ"),
        new NewsRow(411, "ВСПЫШКА НАД ДИУ-2. ДЦ-7 НЕДОСТУПЕН"),
        new NewsRow(415, "ГАНДЕКС: «ВРЕМЕННЫЙ СБОЙ. ДОШИРАК ПОДОРОЖАЛ»"),
    };

    /// <summary>Страница 5: лента новостей; видны строки, чья минута прошла.</summary>
    void RenderNews(List<ScreenLine> L)
    {
        L.Add(new ScreenLine("БРАУЗЕР · НОВОСТИ · " + dir.Clock.Display, White));
        L.Add(new ScreenLine("", C));

        int m = dir.Clock.WholeMinutes;
        var visible = new List<string>();
        for (int i = 0; i < NewsRows.Length; i++)
        {
            if (NewsRows[i].minute <= m)
                visible.Add(MinText(NewsRows[i].minute) + " · " + NewsRows[i].text);
        }
        if (dir.exploded)
            visible.Add(dir.Clock.Display + " · ВСПЫШКА НАД ДИУ-2. ДЦ-5/7/11 НЕДОСТУПНЫ");

        int maxRows = Mathf.Max(1, s.Rows - 6);
        int start = Mathf.Max(0, visible.Count - maxRows);
        for (int i = start; i < visible.Count && i < start + maxRows; i++)
            L.Add(new ScreenLine(visible[i], C));

        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine(dir.exploded ? "лента замерла." : "лента обновляется сама.", Dim));
        L.Add(new ScreenLine("CTRL+C — к списку страниц", Dim));
    }

    static string MinText(int minute)
    {
        return (minute / 60).ToString("00") + ":" + (minute % 60).ToString("00");
    }

    void RenderNotes(List<ScreenLine> L)
    {
        if (noteOpen < 0)
        {
            L.Add(new ScreenLine("ЗАМЕТКИ", White));
            L.Add(new ScreenLine("", C));
            for (int i = 0; i < Notes.Length; i++)
                L.Add(new ScreenLine((i == noteSel ? "▸ " : "  ") + Notes[i].title,
                                     i == noteSel ? White : C));
            L.Add(new ScreenLine("", C));
            L.Add(new ScreenLine("↑↓ выбор   ENTER открыть   CTRL+C стол", Dim));
            return;
        }

        NoteEntry note = Notes[Mathf.Clamp(noteOpen, 0, Notes.Length - 1)];
        L.Add(new ScreenLine(note.title, White));
        L.Add(new ScreenLine("", C));

        int maxRows = Mathf.Max(1, s.Rows - 6);
        int maxScroll = Mathf.Max(0, note.body.Length - maxRows);
        noteScroll = Mathf.Clamp(noteScroll, 0, maxScroll);
        for (int i = noteScroll; i < note.body.Length && i < noteScroll + maxRows; i++)
            L.Add(new ScreenLine(note.body[i], C));

        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("↑↓ прокрутка   ENTER/CTRL+C назад", Dim));
    }

    void RenderOsMessenger(List<ScreenLine> L)
    {
        L.Add(new ScreenLine("ЧАТЫ · КОРПОРАТИВНЫЙ МЕССЕНДЖЕР", White));

        string tabs = "";
        for (int i = 0; i < ChatChannels.Names.Length; i++)
        {
            string name = ChatChannels.Names[i];
            if (i < dir.Unread.Length && dir.Unread[i]) name += "*";
            tabs += (i == osChannel ? "▸" : " ") + name + (i == osChannel ? "◂ " : "  ");
        }
        L.Add(new ScreenLine(tabs, White));
        L.Add(new ScreenLine(new string('─', s.Cols), Lines));

        var feed = new List<ChatMessage>();
        for (int i = 0; i < dir.Messages.Count; i++)
            if ((int)dir.Messages[i].channel == osChannel)
                feed.Add(dir.Messages[i]);

        int maxRows = Mathf.Max(1, s.Rows - 7);
        int maxScroll = Mathf.Max(0, feed.Count - maxRows);
        osScroll = Mathf.Clamp(osScroll, 0, maxScroll);
        int start = Mathf.Max(0, feed.Count - maxRows - osScroll);
        for (int i = start; i < feed.Count && i < start + maxRows; i++)
        {
            ChatMessage m = feed[i];
            string line = m.time + " " + m.sender + ": " + m.text;
            L.Add(new ScreenLine(line, i == feed.Count - 1 ? White : C));
        }

        L.Add(new ScreenLine("", C));
        L.Add(new ScreenLine("←→ каналы   ↑↓ прокрутка   CTRL+C стол", Dim));
    }

    void RenderLogs(List<ScreenLine> L)
    {
        float cd = dir.LogCleanCooldown;
        L.Add(new ScreenLine("ЛОГИ · СЛЕД " + dir.State.suspicion + "%   КИБЕРБЕЗЫ " + dir.CyberCdText,
                             dir.CyberDanger ? Err : Warn));
        L.Add(new ScreenLine("СОСТОЯНИЕ: " + (dir.State.dirtyLogs ? "ГРЯЗНЫЕ" : "ЧИСТО") +
                             "   ОЧИСТКА: " + (cd > 0f ? Mathf.CeilToInt(cd) + " c" : "ГОТОВО"),
                             dir.State.dirtyLogs ? Warn : C));
        L.Add(new ScreenLine(new string('─', s.Cols), Lines));
        L.Add(new ScreenLine("Очистка снимает флаг грязных логов", C));
        L.Add(new ScreenLine("и снижает след на 10%. Пока логи", C));
        L.Add(new ScreenLine("чистые, проверка не поднимает тревогу.", C));
        L.Add(new ScreenLine("[ENTER] ОЧИСТИТЬ ЛОГИ", cd > 0f ? Dim : White));
        if (!string.IsNullOrEmpty(logsMsg) && Time.time < logsMsgUntil)
            L.Add(new ScreenLine(logsMsg, C));
        L.Add(new ScreenLine("CTRL+C — назад", Dim));
    }

    void OpenDesktopApp()
    {
        switch (desktopSel)
        {
            case 0:
                mode = Mode.Browser;
                browserPage = 0;
                browserSel = 0;
                break;
            case 1:
                mode = Mode.OsMessenger;
                osChannel = 0;
                osScroll = 0;
                break;
            case 2:
                mode = Mode.Notes;
                noteOpen = -1;
                noteSel = 0;
                noteScroll = 0;
                break;
            case 3:
                mode = Mode.Logs;
                logsMsg = "";
                break;
            case 4:
                mode = Mode.Tasks;
                selected = 0;
                break;
            case 5:
                OpenSabotage();
                break;
        }
    }

    void SellSelectedData()
    {
        if (browserSel < 0 || browserSel >= dir.State.filesTaken.Count) return;
        string name = dir.State.filesTaken[browserSel];
        int price = MarketPrice(name);
        if (!dir.SellStolenFile(name, price)) return;

        browserMsg = "ПРОДАНО: " + name + "  +" + price + " кр.";
        browserMsgUntil = Time.time + 3f;
        browserSel = Mathf.Clamp(browserSel, 0, Mathf.Max(0, dir.State.filesTaken.Count - 1));
    }

    /// <summary>Прямая продажа офферу ТЕРРА на странице форума.</summary>
    void SellTerraSelected()
    {
        if (browserSel < 0 || browserSel >= dir.TerraOffers.Count) return;
        TerraOffer o = dir.TerraOffers[browserSel];
        if (dir.SellToTerra(o))
        {
            browserMsg = "СДЕЛКА ЗАКРЫТА: +" + o.price + " кр.";
            browserMsgUntil = Time.time + 3f;
        }
        else
        {
            browserMsg = "ОТКАЗ. СМОТРИ ЛЕНТУ ФОРУМА.";
            browserMsgUntil = Time.time + 3f;
        }
    }

    /// <summary>Список страниц стартового экрана браузера по текущей ночи.</summary>
    List<int> BrowserPages()
    {
        var pages = new List<int> { 1, 2 };
        if (dir.State.night >= 3 || dir.TerraUnlocked) pages.Add(3);
        if (dir.State.night >= 3) { pages.Add(4); pages.Add(5); }
        return pages;
    }

    static string BrowserPageTitle(int page)
    {
        switch (page)
        {
            case 1: return "ТЕНЕВОЙ РЫНОК";
            case 2: return "КАК ЭТО РАБОТАЕТ";
            case 3: return "ТЕНЕВОЙ ФОРУМ · ТЕРРА";
            case 4: return "СТАТУС ДЦ";
            default: return "НОВОСТИ";
        }
    }

    static int MarketPrice(string name)
    {
        switch (name)
        {
            case "dc7_route.log": return 120;
            case "dc7_backup.key": return 200;
            case "dc7_final.key": return 350;
            default: return 50;
        }
    }

    static string Pad(string text, int width)
    {
        if (width <= 0) return "";
        if (text == null) text = "";
        if (text.Length >= width) return text.Substring(0, width);
        return text + new string(' ', width - text.Length);
    }

    static int WrapIndex(int value, int count)
    {
        if (count <= 0) return 0;
        return ((value % count) + count) % count;
    }
}
