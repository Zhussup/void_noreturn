using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum ModalKind { None, Intro, Call, Dictaphone, Rest, Death, End, Settings }

/// <summary>
/// Ночь 1 «Смена»: часы, события, звонок шефа, тревога, бессонница,
/// таблетки, отдых, диктофон, конец смены. Собирает экраны и приложения.
/// </summary>
public class NightDirector : MonoBehaviour
{
    public const int StationTerminal = 0;
    public const int StationMessenger = 1;
    public const int StationDesk = 2;
    public const int StationBed = 3;

    public GameState State;
    // Часы — рантайм-состояние: в сцене не храним, поэтому вне сериализации.
    [System.NonSerialized] public GameClock Clock = new GameClock();
    public readonly List<TaskDef> Tasks = new List<TaskDef>();
    public readonly List<ChatMessage> Messages = new List<ChatMessage>();
    public readonly bool[] Unread = new bool[4];
    /// <summary>Целевые файлы, уже скачанные с серверов сотрудников.</summary>
    public readonly List<string> StolenFiles = new List<string>();

    public Font UiFont;
    public PlayerController Player;
    public int ActiveStation;

    public bool Running;
    public bool ModalActive => modal != ModalKind.None;
    public ModalKind Modal => modal;

    // ------------------------------------------------------------ интерфейс

    /// <summary>Варианты размера текста: HUD и надписи на мониторах.</summary>
    public static readonly float[] UiScaleSteps = { 1.0f, 1.1f, 1.2f, 1.35f, 1.5f };
    const string UiScalePref = "vnr.uiScaleIndex";

    /// <summary>Выбранный шаг размера; по умолчанию чуть крупнее базового.</summary>
    public int UiScaleIndex { get; private set; } = 2;
    public float UiScale =>
        UiScaleSteps[Mathf.Clamp(UiScaleIndex, 0, UiScaleSteps.Length - 1)];

    /// <summary>Размер того, что игрок печатает: подсказка действия и ответ на звонок.</summary>
    public static readonly float[] TypeInScaleSteps = { 0.8f, 1.0f, 1.2f, 1.4f, 1.6f };
    const string TypeInScalePref = "vnr.typeInScaleIndex";

    /// <summary>Множитель к базовому кеглю печатаемого текста; 1.0 — как было.</summary>
    public int TypeInScaleIndex { get; private set; } = 1;
    public float TypeInScale =>
        TypeInScaleSteps[Mathf.Clamp(TypeInScaleIndex, 0, TypeInScaleSteps.Length - 1)];

    /// <summary>Выбранная строка настроек: 0 — интерфейс, 1 — текст ввода.</summary>
    public int SettingsRow { get; private set; }

    /// <summary>Приложение принимает текст: E отдаётся ему, а не приближению монитора.</summary>
    public bool TextInputActive =>
        ModalActive ||
        (ActiveStation == StationTerminal && terminal != null && terminal.CapturingInput) ||
        (ActiveStation == StationMessenger && messenger != null && messenger.CapturingInput);

    // прицел и система действий
    public bool ActionVisible => action != null && action.HasTarget
                                 && !string.IsNullOrEmpty(action.Prompt);
    public string ActionPrompt => action != null ? action.Prompt : "";
    public int ActionTyped => action != null ? action.TypedCount : 0;
    public bool ActionRejected => action != null && action.RejectedRecently;

    public string CyberCdText = "--:--";
    public bool CyberDanger;
    public float TypoChance;
    public bool MoodPending;

    public bool PhoneRinging => phone == PhoneState.Ringing;
    public bool PhoneInCall => phone == PhoneState.InCall;

    public string Subtitle;
    public float VignetteAlpha;
    public float FlashAlpha;
    public Color FlashColor = Color.white;
    public float IntroAlpha;
    public float RestProgress;
    public string Hint = "";

    public string EndTitle = "";
    public string[] EndLines = new string[0];

    // звонок
    public string CallLine = "";
    public string CallAnswer => callField.text;
    /// <summary>Ответ с курсором в позиции редактирования (а не только в конце).</summary>
    public string CallAnswerView
    {
        get
        {
            bool show = callField.RejectedRecently || Time.time % 1f < 0.55f;
            string cursor = callField.RejectedRecently ? "▌" : "_";
            return callField.WithCursor(show, cursor);
        }
    }
    public bool CallAwaitingAnswer => callStage == 0;
    public bool CallTypo => callField.RejectedRecently;

    /// <summary>Идёт загрузка файла с сервера сотрудника (для HUD и проверки кибербезов).</summary>
    public bool TerminalDownloading => terminal != null && terminal.Downloading;
    public string TerminalDownloadText => terminal != null ? terminal.DownloadStatus : "";

    // ------------------------------------------------------------ внутреннее

    ModalKind modal = ModalKind.Intro;
    static int retryNight = 1; // какая ночь перезапустится после R
    float introStart;
    const float IntroDuration = 2.8f;
    bool settingsPrevInput;

    TerminalApp terminal;
    MessengerApp messenger;
    ActionSystem action;
    Interactable phoneInteractable;
    ScreenSurface terminalSurface, messengerSurface, laptopSurface;
    List<ScreenLine> laptopLines;

    float flashUntil;
    float subtitleUntil;
    Transform clockHourHand, clockMinuteHand;

    enum PhoneState { Idle, Ringing, InCall }
    PhoneState phone = PhoneState.Idle;
    float ringUntil;
    int callStage;
    float callEndAt;
    readonly InputField callField = new InputField();
    AudioSource ringSource;
    Renderer phoneLed;
    GameObject pillsObj, dictaphoneObj;
    Vector3 dictaphoneHome;
    bool recordingAvailable;
    float dictStart;
    float restStart = -1f;
    const float RestSeconds = 3.5f;

    // Звонок шефа через несколько секунд после входа в терминал; < 0 — не запланирован.
    float callDelay = -1f;
    const float CallDelayAfterLogin = 3f;

    float cyberCd = 300f;
    float logCleanReady;
    int lastMinute = -1;

    class Evt
    {
        public int minute;
        public bool done;
        public Action action;
        public Evt(int minute, Action action) { this.minute = minute; this.action = action; }
    }
    readonly List<Evt> events = new List<Evt>();

    // ------------------------------------------------------------ запуск

    void Start()
    {
        int night = Mathf.Clamp(retryNight, 1, 5);
        retryNight = 1;
        State = GameState.NewGame();
        State.ResetForNight(night);
        Clock.Reset();

        Player = FindAnyObjectByType<PlayerController>();
        UiFont = MakeFont();

        UiScaleIndex = Mathf.Clamp(PlayerPrefs.GetInt(UiScalePref, UiScaleIndex),
                                   0, UiScaleSteps.Length - 1);
        TypeInScaleIndex = Mathf.Clamp(PlayerPrefs.GetInt(TypeInScalePref, TypeInScaleIndex),
                                       0, TypeInScaleSteps.Length - 1);

        if (Player != null)
        {
            Player.startStation = StationTerminal;
            Player.inputEnabled = false;
            Player.lookEnabled = false; // интро: камера ещё не слушается мыши
        }

        BuildSurfaces();
        BuildTasks();
        BuildSchedule();
        BuildWorldBits();
        BuildActionSystem();
        BuildOverlay();

        ActiveStation = StationTerminal;
        introStart = Time.time;
        IntroAlpha = 1f;
        Debug.Log("[VNR] Entry point: NightDirector.Start · объект Game · Assets/Scenes/Office.unity · ночь " + night);
    }

    /// <summary>ENTRY POINT смены: интро закончилось, управление отдано игроку.</summary>
    public void BeginShift()
    {
        modal = ModalKind.None;
        Running = true;
        if (Player != null)
        {
            Player.inputEnabled = true;
            Player.lookEnabled = true;
        }
        ShowSubtitle("Смена началась. Войдите в терминал.", 3.5f);
    }

    static Font MakeFont()
    {
        Font f = Font.CreateDynamicFontFromOSFont(
            new[] { "DejaVu Sans Mono", "Liberation Mono", "Noto Sans Mono", "Courier New" }, 128);
        if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return f;
    }

    // Эталон текста — экран 0.32 × 0.24 м. Монитор в сцене увеличен до 1.35×,
    // но текст в ОС специально мельче расчётного — 0.85 от масштаба монитора.
    const float RefScreenHeight = 0.24f;
    const float RefCharSize = 0.00064f;
    const float RefLineHeight = 0.0115f;
    const float LineSpacingBoost = 1.12f; // чуть больше воздуха между строками
    const float OsTextScale = 0.85f;      // текст ОС мельче, монитор — больше

    /// <summary>Кегль и шаг строк под физическую высоту экрана.</summary>
    static void TextMetrics(Transform screen, out float charSize, out float lineHeight)
    {
        float k = 1f;
        if (screen != null && screen.lossyScale.y > 0.001f)
            k = Mathf.Max(0.5f, screen.lossyScale.y / RefScreenHeight);
        k *= OsTextScale;
        charSize = RefCharSize * k;
        lineHeight = RefLineHeight * k * LineSpacingBoost;
    }

    void BuildSurfaces()
    {
        Transform term = FindScreen("Monitor_Terminal");
        Transform chat = FindScreen("Monitor_Chat");
        Transform laptop = FindLaptopScreen();

        if (term != null)
        {
            float cs, lh;
            TextMetrics(term, out cs, out lh);
            terminalSurface = ScreenSurface.Create(term, UiFont, cs, lh);
        }
        if (chat != null)
        {
            float cs, lh;
            TextMetrics(chat, out cs, out lh);
            messengerSurface = ScreenSurface.Create(chat, UiFont, cs, lh);
        }
        if (laptop != null)
        {
            float cs, lh;
            TextMetrics(laptop, out cs, out lh);
            laptopSurface = ScreenSurface.Create(laptop, UiFont, cs, lh);
            laptopLines = new List<ScreenLine>
            {
                new ScreenLine("", Color.white),
                new ScreenLine("  GANDEX-OS", new Color(0.30f, 0.70f, 0.62f)),
                new ScreenLine("  локальная машина", new Color(0.12f, 0.28f, 0.26f)),
                new ScreenLine("  экран заблокирован", new Color(0.12f, 0.28f, 0.26f)),
            };
            laptopSurface.Set(laptopLines);
        }

        if (terminalSurface != null)
            terminal = new TerminalApp(this, terminalSurface);
        if (messengerSurface != null)
            messenger = new MessengerApp(this, messengerSurface);

        ApplyTextScale();
    }

    /// <summary>Размер текста на физических экранах следует за настройкой HUD.</summary>
    void ApplyTextScale()
    {
        if (terminalSurface != null) terminalSurface.SetScale(UiScale);
        if (messengerSurface != null) messengerSurface.SetScale(UiScale);
        if (laptopSurface != null)
        {
            laptopSurface.SetScale(UiScale);
            // Экран ноутбука статичный: после пересборки возвращаем его надпись.
            if (laptopLines != null) laptopSurface.Set(laptopLines);
        }
    }

    static Transform FindScreen(string monitorName)
    {
        GameObject m = GameObject.Find(monitorName);
        if (m == null) return null;
        Transform t = m.transform.Find("Screen");
        return t != null ? t : m.transform;
    }

    static Transform FindLaptopScreen()
    {
        // В комнате друга ноутбук закрыт и своего экрана не имеет.
        // Тогда декоративную надпись не рисуем вовсе.
        GameObject lap = GameObject.Find("Laptop");
        if (lap == null) return null;
        Transform lid = lap.transform.Find("Lid");
        return lid != null ? lid.Find("Screen") : null;
    }

    void BuildTasks()
    {
        Tasks.Clear();

        // Мини-задача в начале смены: засвечивает коды сотрудников.
        TaskDef profiles = new TaskDef(
            TaskKind.Match,
            "СВЕРКА ПРОФИЛЕЙ",
            "ВВЕДИТЕ ID:",
            "A-4471",
            "ФРАГМЕНТ: «Л. Дэлайла, ДИУ-2»",
            "КАНДИДАТЫ (коды доступа):",
            "  A-4471  Л. ДЭЛАЙЛА   код a-4471",
            "  A-5120  Д. ДЖОЭЛ     код a-5120",
            "  A-6643  К. КУПЛИНОВ  код a-6643");
        profiles.revealsCodes = true;
        Tasks.Add(profiles);

        Tasks.Add(new TaskDef(
            TaskKind.Sort,
            "СОРТИРОВКА ПОСТОВ",
            "РЕШЕНИЕ: PASS | PURGE | FLAG",
            "FLAG",
            "ПОСТ #4471 · 00:41 · 8823",
            "«В поезде №14 нашли сумку без хозяина.",
            " Внутри — документы и старый телефон.»",
            "ПРИМЕЧАНИЕ: упоминание ДЦ-7?"));

        Tasks.Add(new TaskDef(
            TaskKind.Approve,
            "АВТОДОНОС",
            "ПОДТВЕРДИТЕ: APPROVE",
            "APPROVE",
            "СУБЪЕКТ: пользователь 8823",
            "ОСНОВАНИЕ: запрос «дата-центр 7»",
            "СТАТУС: автодонос сформирован",
            "ОТПРАВИТЬ: APPROVE"));
    }

    void BuildSchedule()
    {
        events.Clear();

        events.Add(new Evt(20, () => AddChat(ChatChannel.Team, "ДЖОЭЛ",
            "Ухожу в отпуск. Если что — не пишите, я в море.", Clock.Display)));

        events.Add(new Evt(70, () => AddChat(ChatChannel.Team, "ДЭЛАЙЛА",
            "Ночь длинная. Держись, Брик.", Clock.Display)));

        events.Add(new Evt(140, () => AddChat(ChatChannel.Team, "КУПЛИНОВ",
            "Кто-нибудь, выключите уведомления. Ночью спать надо.", Clock.Display)));

        // Звонок шефа больше не привязан к часам: он идёт через три секунды
        // после того, как игрок войдёт в терминал (см. OnLogin).

        events.Add(new Evt(252, () => // 04:12 — чужая строка в чате и провал
        {
            AddChat(ChatChannel.Boss, "БРИК", "всё под контролем", "04:12");
            DoFlash(Color.black, 0.55f, 0.35f);
            AddAnxiety(8f);
            Clock.Advance(6f); // часы действительно прыгают
            if (State.pills > 0) State.pills--;
            ShowSubtitle("Часы дёрнулись. В блистере на одну таблетку меньше.", 3.5f);
            TickEvents();
        }));

        events.Add(new Evt(300, () => AddChat(ChatChannel.Security, "КИБЕРБЕЗОПАСНОСТЬ",
            "ПЛАНОВАЯ ПРОВЕРКА ЛОГОВ: ЧИСТО.", Clock.Display)));

        events.Add(new Evt(330, MoveDictaphone)); // ~05:30

        events.Add(new Evt(360, () => AddChat(ChatChannel.Harmony, "ГАРМОНИЯ",
            "Вы давно не отдыхали. Рекомендую паузу.", Clock.Display)));

        events.Add(new Evt(400, () => // ГАРМОНИЯ возвращает ответ как факт
        {
            string text = State.moodAnswered
                ? "Ваше состояние в начале смены: " + State.moodStart + "/10. Данные учтены."
                : "Оценка настроения не получена. Это отражено в отчёте.";
            AddChat(ChatChannel.Harmony, "ГАРМОНИЯ", text, Clock.Display);
        }));
    }

    void BuildWorldBits()
    {
        pillsObj = GameObject.Find("Pills");
        // в комнате друга диктофон называется Recorder
        dictaphoneObj = GameObject.Find("Recorder");
        if (dictaphoneObj == null) dictaphoneObj = GameObject.Find("Dictaphone");
        if (dictaphoneObj != null) dictaphoneHome = dictaphoneObj.transform.localPosition;

        BindPhoneInteractable();
        BuildPhoneAudio();
        BuildWallClock();

        GameObject led = GameObject.Find("Phone_LED");
        if (led != null)
        {
            phoneLed = led.GetComponent<Renderer>();
            if (phoneLed != null) phoneLed.material.color = new Color(0.15f, 0.02f, 0.02f);
        }
    }

    void BindPhoneInteractable()
    {
        GameObject phoneGo = GameObject.Find("Phone");
        if (phoneGo == null) return;

        phoneInteractable = phoneGo.GetComponent<Interactable>();
        if (phoneInteractable == null)
            phoneInteractable = phoneGo.AddComponent<Interactable>();

        phoneInteractable.prompt = "PICK UP";
        phoneInteractable.available = false;
        phoneInteractable.maxDistance = 2.6f;
        phoneInteractable.Activated += OnPhoneAction;
    }

    void BuildActionSystem()
    {
        Camera cam = Camera.main;
        if (cam == null && Player != null && Player.cameraTransform != null)
            cam = Player.cameraTransform.GetComponent<Camera>();
        if (cam == null) return;

        var go = new GameObject("ActionSystem");
        go.transform.SetParent(transform, false);
        action = go.AddComponent<ActionSystem>();
        action.Init(this, cam);
    }

    void BuildPhoneAudio()
    {
        GameObject phoneGo = GameObject.Find("Phone");
        if (phoneGo == null) return;

        const int rate = 44100;
        float len = 1.1f;
        int n = Mathf.RoundToInt(rate * len);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float v = 0f;
            if (t < 0.16f) v = Mathf.Sin(2f * Mathf.PI * 880f * t);
            else if (t > 0.24f && t < 0.40f) v = Mathf.Sin(2f * Mathf.PI * 1046f * t);
            float fade = Mathf.Clamp01(Mathf.Min(t * 40f, (len - t) * 20f));
            data[i] = v * 0.35f * fade;
        }

        AudioClip clip = AudioClip.Create("VNR_PhoneRing", n, 1, rate, false);
        clip.SetData(data, 0);

        ringSource = phoneGo.AddComponent<AudioSource>();
        ringSource.clip = clip;
        ringSource.loop = true;
        ringSource.playOnAwake = false;
        ringSource.volume = 0.35f;
        ringSource.spatialBlend = 0.35f;
    }

    void BuildWallClock()
    {
        // Часы друга — аналоговые: циферблат статичный, стрелки крутим кодом
        // по игровому времени. Цифровую накладку не кладём — она перекрывала бы стрелки.
        GameObject clock = GameObject.Find("WallClock");
        if (clock == null) return;

        // Стрелки — прямые дочерние объекты корня префаба (рядом с Mesh/Face).
        clockHourHand = clock.transform.Find("Hand_Hour");
        clockMinuteHand = clock.transform.Find("Hand_Minute");
        UpdateClockHands();
    }

    void BuildOverlay()
    {
        var overlay = gameObject.AddComponent<OverlayUI>();
        overlay.Init(this, UiFont);
    }

    // ------------------------------------------------------------ главный цикл

    void Update()
    {
        if (Player != null)
        {
            ActiveStation = Player.Current;
            Player.TextInputActive = TextInputActive;
            if (!Running || ModalActive) Player.ClearFocus();
        }

        if (modal == ModalKind.Intro)
        {
            float t = Time.time - introStart;
            IntroAlpha = t < 1.6f ? 1f : Mathf.Clamp01(1f - (t - 1.6f) / 1.1f);
            if (t >= IntroDuration) BeginShift();
            UpdateVisuals();
            return;
        }

        if (Input.GetKeyDown(KeyCode.R) &&
            (modal == ModalKind.Death || modal == ModalKind.End))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        // ESC — пауза с настройками; в приближении монитора ESC сначала
        // закрывает его (это уже сделал PlayerController).
        if (Input.GetKeyDown(KeyCode.Escape) &&
            (Player == null || !Player.EscapeClearedFocus))
        {
            if (modal == ModalKind.Settings) CloseSettings();
            else if (modal == ModalKind.None && Running) OpenSettings();
        }
        if (modal == ModalKind.Settings)
        {
            ringUntil += Time.deltaTime; // пауза не должна съедать время звонка
            HandleSettingsInput();
        }

        bool timeRuns = Running && modal != ModalKind.Death &&
                        modal != ModalKind.End && modal != ModalKind.Settings;
        if (timeRuns)
        {
            Clock.Tick(Time.deltaTime);
            // Загрузка идёт даже вне терминала: за ней надо следить.
            if (terminal != null) terminal.TickDownload(Time.deltaTime);
            TickCyberbez(Time.deltaTime);
            TickEvents();
            TickAnxiety(Time.deltaTime);
            TickCallDelay(Time.deltaTime);
        }

        if (Player != null)
            Player.lookEnabled = Running &&
                                 (modal == ModalKind.None || modal == ModalKind.Call);

        // телефон обрабатываем раньше действий: в кадре PICK UP не должен
        // попасть в поле ответа на звонок
        TickPhone();
        UpdateActionPrompt();
        if (action != null) action.Tick();

        if (Running && modal == ModalKind.None)
        {
            if (!ActionVisible)
            {
                if (terminal != null) terminal.Tick();
                if (messenger != null) messenger.Tick();
            }

            // 1/2 не участвуют в подписи действия, поэтому работают и при прицеле
            HandleDeskInput();
        }

        TickDictaphone();
        TickRest();

        if (terminalSurface != null && terminal != null) terminal.Render();
        if (messengerSurface != null && messenger != null) messenger.Render();

        UpdateClockVisual();
        UpdatePhoneLed();
        UpdateHint();
        UpdateVisuals();
    }

    // ------------------------------------------------------------ настройки

    /// <summary>Выбрать шаг размера интерфейса (клик по варианту в меню).</summary>
    public void SetUiScale(int index)
    {
        int next = Mathf.Clamp(index, 0, UiScaleSteps.Length - 1);
        if (next == UiScaleIndex) return;

        UiScaleIndex = next;
        PlayerPrefs.SetInt(UiScalePref, UiScaleIndex);
        PlayerPrefs.Save();
        ApplyTextScale(); // текст на мониторах перестраивается сразу
    }

    /// <summary>Выбрать шаг размера печатаемого текста (карточка в настройках).</summary>
    public void SetTypeInScale(int index)
    {
        int next = Mathf.Clamp(index, 0, TypeInScaleSteps.Length - 1);
        if (next == TypeInScaleIndex) return;

        TypeInScaleIndex = next;
        PlayerPrefs.SetInt(TypeInScalePref, TypeInScaleIndex);
        PlayerPrefs.Save();
    }

    /// <summary>Строка настроек, которую сейчас крутят стрелками.</summary>
    public void SelectSettingsRow(int row)
    {
        SettingsRow = Mathf.Clamp(row, 0, 1);
    }

    void OpenSettings()
    {
        settingsPrevInput = Player != null && Player.inputEnabled;
        SettingsRow = 0;
        modal = ModalKind.Settings;
        if (Player == null) return;
        Player.inputEnabled = false;   // станции и действия ждут
        Player.SetCursorLocked(false); // курсор нужен, чтобы выйти из окна
    }

    void CloseSettings()
    {
        modal = ModalKind.None;
        if (Player == null) return;
        Player.inputEnabled = settingsPrevInput;
        Player.SetCursorLocked(true);
    }

    void HandleSettingsInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow)) SelectSettingsRow(SettingsRow - 1);
        if (Input.GetKeyDown(KeyCode.DownArrow)) SelectSettingsRow(SettingsRow + 1);
        if (Input.GetKeyDown(KeyCode.LeftArrow)) AdjustSetting(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow)) AdjustSetting(1);
    }

    void AdjustSetting(int delta)
    {
        if (SettingsRow == 0) SetUiScale(UiScaleIndex + delta);
        else SetTypeInScale(TypeInScaleIndex + delta);
    }

    /// <summary>Отсчёт до звонка шефа после успешного входа в терминал.</summary>
    void TickCallDelay(float dt)
    {
        if (callDelay < 0f) return;

        callDelay -= dt;
        if (callDelay > 0f) return;

        callDelay = -1f;
        StartRinging();
    }

    void TickEvents()
    {
        int m = Clock.WholeMinutes;
        if (m == lastMinute) return;
        lastMinute = m;

        for (int i = 0; i < events.Count; i++)
        {
            Evt e = events[i];
            if (!e.done && e.minute <= m)
            {
                e.done = true;
                if (e.action != null) e.action();
            }
        }

        if (Clock.Finished) EndNight();
    }

    void TickCyberbez(float dt)
    {
        cyberCd -= dt;
        if (cyberCd <= 0f)
        {
            cyberCd = 280f;

            // Застукали за скачиванием — ловят сразу.
            if (terminal != null && terminal.Downloading)
            {
                string file = terminal.CurrentDownload;
                terminal.AbortDownload();
                AddChat(ChatChannel.Security, "КИБЕРБЕЗОПАСНОСТЬ",
                    "ОБНАРУЖЕНА УТЕЧКА. ИСТОЧНИК: " + file + ".", Clock.Display);
                StartCaught(
                    "Во время проверки кибербезов вы скачивали файл \"" + file + "\". " +
                    "Нужно было остановить загрузку командой stop или дождаться её окончания " +
                    "до нуля таймера проверки.");
                return;
            }

            // Плановая проверка: грязные логи бьют по тревоге.
            if (State.dirtyLogs)
            {
                AddAnxiety(4f + 0.05f * State.suspicion);
                AddChat(ChatChannel.Security, "КИБЕРБЕЗОПАСНОСТЬ",
                    "ПЛАНОВАЯ ПРОВЕРКА ЛОГОВ: ГРЯЗНЫЕ, СЛЕД " + State.suspicion + "%.", Clock.Display);
            }
            else
            {
                AddChat(ChatChannel.Security, "КИБЕРБЕЗОПАСНОСТЬ",
                    "ПЛАНОВАЯ ПРОВЕРКА ЛОГОВ: НАРУШЕНИЙ НЕТ.", Clock.Display);
            }
            if (State.suspicion >= 100) StartCyberDeath();
            else State.suspicion = Mathf.Min(100, State.suspicion + 1);
        }
        int sec = Mathf.Max(0, Mathf.CeilToInt(cyberCd));
        CyberCdText = (sec / 60).ToString("00") + ":" + (sec % 60).ToString("00");
        CyberDanger = cyberCd < 60f;
    }

    void TickAnxiety(float dt)
    {
        float rate = 0.030f + 0.015f * Mathf.Max(0, State.insomnia - 1);
        // Кибербезы подходят к проверке, а в логах след — тревога растёт быстрее.
        if (State.dirtyLogs && CyberDanger) rate += 0.055f;
        State.anxiety = Mathf.Clamp(State.anxiety + rate * dt, 0f, 100f);
        if (State.anxiety >= 100f) StartDeath();
    }

    void UpdateVisuals()
    {
        VignetteAlpha = Mathf.Clamp01((State.anxiety - 25f) / 75f) * 0.85f;
        TypoChance = 0.010f + 0.015f * Mathf.Max(0, State.insomnia - 1)
                   + 0.00012f * State.anxiety;

        if (Time.time > flashUntil) FlashAlpha = 0f;
        if (Time.time > subtitleUntil) Subtitle = null;

        if (pillsObj != null) pillsObj.SetActive(State.pills > 0);
    }

    void UpdateHint()
    {
        Hint = "";
        if (!Running || ModalActive) return;

        switch (ActiveStation)
        {
            case StationTerminal:
                Hint = "[T] терминал   ·   TAB — экран   ·   мышь — обзор";
                break;
            case StationMessenger:
                Hint = "←→ каналы   ↑↓ прокрутка   ·   TAB — экран   ·   мышь — обзор";
                break;
            case StationDesk:
                // Подсказку PICK UP / HANG UP не дублируем в нижней строке:
                // серая подпись появляется только когда прицел на телефоне.
                string h = "1 — таблетка (" + State.pills + ")   ·   2 — диктофон";
                if (recordingAvailable && !State.records[0]) h += " (новая запись)";
                h += "   ·   TAB — экран   ·   мышь — обзор";
                Hint = h;
                break;
            case StationBed:
                Hint = "E (удерживать) — отдых   ·   TAB — экран   ·   мышь — обзор";
                break;
        }

        if (Player != null)
        {
            if (Player.MonitorFocused)
                Hint = TextInputActive
                    ? "ESC — вернуться к комнате"
                    : "E — вернуться к комнате";
            else if (Player.CanFocusMonitor)
                Hint += "   ·   E — приблизить";
            else if (!TextInputActive)
                Hint += "   ·   Z — зум";
        }
    }

    void UpdateClockVisual()
    {
        UpdateClockHands();
    }

    /// <summary>
    /// Стрелки вращаются вокруг локальной Z. 0° — на 12 часов,
    /// положительный угол — по часовой стрелке (вид с лицевой стороны).
    /// </summary>
    void UpdateClockHands()
    {
        float minutes = Clock.Minutes;

        if (clockMinuteHand != null)
            clockMinuteHand.localRotation = Quaternion.Euler(0f, 0f, (minutes % 60f) * 6f);

        if (clockHourHand != null)
            clockHourHand.localRotation = Quaternion.Euler(0f, 0f, (minutes % 720f) * 0.5f);
    }

    void UpdatePhoneLed()
    {
        if (phoneLed == null) return;
        if (PhoneRinging)
        {
            float p = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f);
            phoneLed.material.color = Color.Lerp(new Color(0.35f, 0.01f, 0.01f),
                                                 new Color(1f, 0.15f, 0.10f), p);
        }
        else if (PhoneInCall) phoneLed.material.color = new Color(0.9f, 0.35f, 0.1f);
        else phoneLed.material.color = new Color(0.15f, 0.02f, 0.02f);
    }

    // ------------------------------------------------------------ действия

    void UpdateActionPrompt()
    {
        if (phoneInteractable == null) return;

        if (PhoneRinging)
        {
            phoneInteractable.prompt = "PICK UP";
            phoneInteractable.available = true;
        }
        else if (PhoneInCall && callStage == 1)
        {
            phoneInteractable.prompt = "HANG UP";
            phoneInteractable.available = true;
        }
        else
        {
            phoneInteractable.available = false;
        }
    }

    void OnPhoneAction()
    {
        if (PhoneRinging) { AnswerCall(); return; }
        if (PhoneInCall && callStage == 1) EndCall(false);
    }

    // ------------------------------------------------------------ ввод за столом

    void HandleDeskInput()
    {
        if (ActiveStation != StationDesk) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) TakePill();
        if (Input.GetKeyDown(KeyCode.Alpha2)) PlayDictaphone();
    }

    void TakePill()
    {
        if (State.pills <= 0) { ShowSubtitle("Блистер пуст.", 2f); return; }
        State.pills--;
        State.anxiety = Mathf.Max(0f, State.anxiety - 30f);
        ShowSubtitle("Таблетка. Тревога отступает.", 2.5f);
    }

    // ------------------------------------------------------------ звонок

    void StartRinging()
    {
        if (phone != PhoneState.Idle) return;
        phone = PhoneState.Ringing;
        ringUntil = Time.time + 45f;
        if (ringSource != null) ringSource.Play();
        AddChat(ChatChannel.Boss, "ВИКТОР", "Звонок на стационарный.", Clock.Display);
        ShowSubtitle("Телефон звонит. Наведите прицел и напечатайте PICK UP.", 3.5f);
        DoFlash(Color.black, 0.3f, 0.2f);
    }

    void TickPhone()
    {
        if (!Running && modal != ModalKind.Call) return;
        if (modal == ModalKind.Settings) return; // пауза не «съедает» звонок

        if (phone == PhoneState.Ringing && Time.time > ringUntil)
        {
            MissedCall();
            return;
        }

        if (phone == PhoneState.InCall)
        {
            if (callStage == 0)
            {
                callField.Tick(TypoChance);
                bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
                if (enter) SubmitCallAnswer();
            }
            else if (Time.time >= callEndAt)
            {
                EndCall();
            }
        }
    }

    void AnswerCall()
    {
        if (phone != PhoneState.Ringing) return;
        phone = PhoneState.InCall;
        if (ringSource != null) ringSource.Stop();
        modal = ModalKind.Call;
        callStage = 0;
        callField.Clear();
        CallLine = "Брик. Это Виктор. Как смена?";
        AddChat(ChatChannel.Boss, "ВИКТОР", "Звонок принят.", Clock.Display);
        if (Player != null) Player.inputEnabled = false;
    }

    void SubmitCallAnswer()
    {
        if (callStage != 0) return;
        callStage = 1;
        bool done = AllTasksDone();
        if (done)
        {
            State.trust[0] = Mathf.Min(100, State.trust[0] + 4);
            State.trust[1] = Mathf.Min(100, State.trust[1] + 4);
            State.trust[2] = Mathf.Min(100, State.trust[2] + 4);
            CallLine = "Хорошо. Задачи закрыты. Продолжай.";
        }
        else
        {
            State.trust[0] = Mathf.Max(0, State.trust[0] - 6);
            State.trust[1] = Mathf.Max(0, State.trust[1] - 6);
            State.trust[2] = Mathf.Max(0, State.trust[2] - 6);
            CallLine = "Задачи не закрыты. К семи всё должно быть готово.";
        }
        callEndAt = Time.time + 4.5f;
    }

    void EndCall(bool polite = true)
    {
        phone = PhoneState.Idle;
        modal = ModalKind.None;
        if (Player != null) Player.inputEnabled = true;
        AddChat(ChatChannel.Boss, "ВИКТОР",
            polite ? "Спасибо, Брик." : "Брик? Ты здесь?", Clock.Display);
        ShowSubtitle(polite ? "Звонок завершён." : "Вы положили трубку.", 2.5f);
    }

    void MissedCall()
    {
        phone = PhoneState.Idle;
        if (ringSource != null) ringSource.Stop();
        for (int i = 0; i < State.trust.Length; i++)
            State.trust[i] = Mathf.Max(0, State.trust[i] - 8);
        AddChat(ChatChannel.Boss, "ВИКТОР",
            "Брик, ты не выходишь на связь. Позвони.", Clock.Display);
        AddAnxiety(6f);
        ShowSubtitle("Звонок пропущен. Доверие упало.", 3f);
    }

    // ------------------------------------------------------------ диктофон

    void MoveDictaphone()
    {
        recordingAvailable = true;
        if (dictaphoneObj != null)
        {
            dictaphoneObj.transform.localPosition = dictaphoneHome +
                new Vector3(-0.22f, 0.002f, 0.17f);
        }
        ShowSubtitle("Диктофон лежит не там, где вы его оставили.", 3.5f);
        AddAnxiety(4f);
    }

    void PlayDictaphone()
    {
        if (!recordingAvailable) { ShowSubtitle("Диктофон молчит.", 2f); return; }
        if (State.records[0]) { ShowSubtitle("Больше записей нет.", 2f); return; }

        modal = ModalKind.Dictaphone;
        dictStart = Time.time;
        if (Player != null) Player.inputEnabled = false;
    }

    void TickDictaphone()
    {
        if (modal != ModalKind.Dictaphone) return;
        float t = Time.time - dictStart;

        if (t < 2.4f)
            Subtitle = "ЗАПИСЬ 1 · 06:37";
        else if (t < 7.0f)
            Subtitle = "«Зачем ты это делаешь? У тебя есть работа. Нормальная работа».";
        else if (t < 8.0f)
            Subtitle = "Запись обрывается.";
        else
        {
            State.records[0] = true;
            modal = ModalKind.None;
            if (Player != null) Player.inputEnabled = true;
            AddAnxiety(4f);
            ShowSubtitle("Запись прослушана.", 2f);
        }
        subtitleUntil = Time.time + 0.5f;
    }

    // ------------------------------------------------------------ отдых

    void TickRest()
    {
        if (ActiveStation == StationBed && modal == ModalKind.None &&
            Input.GetKeyDown(KeyCode.E))
        {
            modal = ModalKind.Rest;
            restStart = Time.time;
            RestProgress = 0f;
            if (Player != null) Player.inputEnabled = false;
            return;
        }

        if (modal != ModalKind.Rest) return;

        if (!Input.GetKey(KeyCode.E))
        {
            modal = ModalKind.None;
            RestProgress = 0f;
            if (Player != null) Player.inputEnabled = true;
            return;
        }

        RestProgress = Mathf.Clamp01((Time.time - restStart) / RestSeconds);
        if (RestProgress >= 1f)
        {
            modal = ModalKind.None;
            RestProgress = 0f;
            if (Player != null) Player.inputEnabled = true;
            Clock.Advance(40f);
            TickEvents();
            State.anxiety = Mathf.Max(0f, State.anxiety - 25f);
            ShowSubtitle("Вы отдохнули. Прошло 40 минут.", 3f);
        }
    }

    // ------------------------------------------------------------ события и сообщения

    public void OnLogin()
    {
        MoodPending = true;
        callDelay = CallDelayAfterLogin; // терминал открыт — через 3 с звонит шеф
        AddChat(ChatChannel.Harmony, "ГАРМОНИЯ",
            "Здравствуйте, Брик. Оцените настроение от 1 до 10.", Clock.Display);
        ShowSubtitle("Смена открыта. Проверьте мессенджер.", 3f);
    }

    public void SubmitMood(int value)
    {
        if (!MoodPending) return;
        MoodPending = false;
        State.moodAnswered = true;
        State.moodStart = value;
        AddChat(ChatChannel.Harmony, "ГАРМОНИЯ",
            "Спасибо. Ваше состояние: " + value + "/10.", Clock.Display);
        if (value <= 3)
            AddChat(ChatChannel.Harmony, "ГАРМОНИЯ",
                "Рекомендую паузу и успокоительное.", Clock.Display);
        else if (value >= 8)
            AddChat(ChatChannel.Harmony, "ГАРМОНИЯ",
                "Рады, что вы в порядке.", Clock.Display);
    }

    public void OnTaskCompleted(TaskDef task)
    {
        State.tasksDone = TasksDone;
        if (AllTasksDone())
            AddChat(ChatChannel.Boss, "ВИКТОР", "Задачи закрыты. Хорошая работа.", Clock.Display);
    }

    public void OnPlanStep(string id)
    {
        if (id == "recon")
        {
            AddAnxiety(2f);
            ShowSubtitle("План: дата-центр 7 найден.", 3f);
        }
    }

    public int TasksDone
    {
        get
        {
            int n = 0;
            for (int i = 0; i < Tasks.Count; i++) if (Tasks[i].done) n++;
            return n;
        }
    }

    bool AllTasksDone() => Tasks.Count > 0 && TasksDone == Tasks.Count;

    public void AddChat(ChatChannel channel, string sender, string text, string time)
    {
        Messages.Add(new ChatMessage(channel, sender, text, string.IsNullOrEmpty(time) ? Clock.Display : time));
        Unread[(int)channel] = true;
    }

    public void MarkRead(ChatChannel channel)
    {
        Unread[(int)channel] = false;
    }

    public void AddAnxiety(float amount)
    {
        State.anxiety = Mathf.Clamp(State.anxiety + amount, 0f, 100f);
        if (State.anxiety >= 100f) StartDeath();
    }

    // ------------------------------------------------------------ данные и логи

    /// <summary>Целевой файл скачан — его можно продать на теневом рынке.</summary>
    public void RegisterStolenFile(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        if (!StolenFiles.Contains(name)) StolenFiles.Add(name);
    }

    /// <summary>Продать файл; false, если такого файла нет.</summary>
    public bool SellStolenFile(string name, int price)
    {
        if (!StolenFiles.Remove(name)) return false;
        State.credits += Mathf.Max(0, price);
        return true;
    }

    /// <summary>Кулдаун очистки логов, секунды.</summary>
    public float LogCleanCooldown => Mathf.Max(0f, logCleanReady - Time.time);

    /// <summary>Очистка логов: снимает dirtyLogs и снижает след на 10%.</summary>
    public bool CleanLogs()
    {
        if (Time.time < logCleanReady) return false;
        State.dirtyLogs = false;
        State.suspicion = Mathf.Max(0, State.suspicion - 10);
        logCleanReady = Time.time + 30f;
        return true;
    }

    public void ShowSubtitle(string text, float seconds)
    {
        Subtitle = text;
        subtitleUntil = Time.time + seconds;
    }

    void DoFlash(Color color, float alpha, float seconds)
    {
        FlashColor = color;
        FlashAlpha = alpha;
        flashUntil = Time.time + seconds;
    }

    // ------------------------------------------------------------ проигрыш / конец

    void StartDeath()
    {
        if (modal == ModalKind.Death) return;
        retryNight = State.night;
        if (ringSource != null) ringSource.Stop();
        phone = PhoneState.Idle;
        modal = ModalKind.Death;
        Running = false;
        Clock.running = false;
        if (Player != null) Player.inputEnabled = false;
        EndTitle = "СЕРДЦЕ НЕ ВЫДЕРЖАЛО.";
        EndLines = new[]
        {
            "Тревога дошла до предела.",
            "",
            "R — начать ночь заново",
        };
    }

    void StartCyberDeath()
    {
        if (modal == ModalKind.Death) return;
        retryNight = State.night;
        if (ringSource != null) ringSource.Stop();
        phone = PhoneState.Idle;
        modal = ModalKind.Death;
        Running = false;
        Clock.running = false;
        if (Player != null) Player.inputEnabled = false;
        EndTitle = "ПРИШЛИ В 04:12.";
        EndLines = new[]
        {
            "Кибербезы дошли до адреса.",
            "Вы ещё успели допечатать.",
            "",
            "R — начать ночь заново",
        };
    }

    /// <summary>Поймали на скачивании: экран ошибки и перезапуск текущей (последней) ночи.</summary>
    void StartCaught(string reason)
    {
        if (modal == ModalKind.Death || modal == ModalKind.End) return;
        retryNight = State.night;
        if (ringSource != null) ringSource.Stop();
        phone = PhoneState.Idle;
        modal = ModalKind.Death;
        Running = false;
        Clock.running = false;
        if (Player != null) Player.inputEnabled = false;
        EndTitle = "ВЫ СОВЕРШИЛИ ОШИБКУ";
        EndLines = new[]
        {
            reason,
            "",
            "Вас поймали кибербезы.",
            "R — начать ночь заново",
        };
    }

    void EndNight()
    {
        if (modal == ModalKind.End) return;
        if (ringSource != null) ringSource.Stop();
        phone = PhoneState.Idle;
        modal = ModalKind.End;
        Running = false;
        Clock.running = false;
        if (Player != null) Player.inputEnabled = false;

        if (AllTasksDone())
            for (int i = 0; i < State.trust.Length; i++)
                State.trust[i] = Mathf.Min(100, State.trust[i] + 3);
        else
            for (int i = 0; i < State.trust.Length; i++)
                State.trust[i] = Mathf.Max(0, State.trust[i] - 5);

        EndTitle = "07:00 · КОНЕЦ СМЕНЫ";
        EndLines = new[]
        {
            "НОЧЬ 1 · «СМЕНА»",
            "",
            "Задачи: " + TasksDone + "/" + Tasks.Count,
            "План: разведка ДЦ-7 — " + (State.planStep > 0 ? "выполнена" : "не выполнена"),
            "Тревога: " + Mathf.RoundToInt(State.anxiety) + "%",
            "Таблеток осталось: " + State.pills,
            "Записей прослушано: " + (State.records[0] ? "1" : "0"),
            "Бессонница: " + State.insomnia + "/5",
            State.moodAnswered
                ? "ГАРМОНИЯ записала настроение: " + State.moodStart + "/10"
                : "ГАРМОНИЯ не получила оценку настроения",
            "",
            "Доверие: Джоэл " + State.trust[0] +
                " · Дэлайла " + State.trust[1] +
                " · Куплинов " + State.trust[2],
            "",
            AllTasksDone()
                ? "Смена закрыта. Виктор доволен."
                : "Задачи не закрыты. Утром — выговор.",
            "",
            "R — переиграть ночь",
            "Ночь 2 «Первый код» — в следующей сборке.",
        };
    }
}
