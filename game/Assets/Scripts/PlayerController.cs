using UnityEngine;

/// <summary>
/// Управление «по станциям»: свободного перемещения нет.
/// Герой сидит за столом и переводит взгляд между физическими точками:
/// 0 — монитор 1 (терминал), 1 — монитор 2 (мессенджер),
/// 2 — стол (телефон, таблетки, диктофон), 3 — кровать (отдых).
/// Переключение сглаженное: камера-«голова» за доли секунды перелетает
/// в центр выбранной точки по её живым координатам, а обзор мышью
/// догоняет ввод с мягкой инерцией.
///
/// Мышь — свободный обзор вокруг станции (в пределах ±82° по горизонтали
/// и ±50° по вертикали), чтобы наводить прицел на предметы. Кликов нет.
/// Повторное нажатие F1–F4 на текущей станции возвращает взгляд в центр.
/// Клавиши: TAB / Shift+TAB — соседняя станция, F1–F4 — прямой выбор,
/// Z (удерживать) — плавный зум камеры.
/// Буквы и цифры не заняты переключением: на экранах игрок печатает команды,
/// поэтому зум отключается, пока приложение принимает текст.
/// </summary>
[DefaultExecutionOrder(-100)] // раньше NightDirector: E успевает забрать фокус до строк ввода
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Станции: позиция глаза героя")]
    public Transform[] stations = new Transform[0];

    [Header("Точки взгляда (экраны) — длина = длине stations")]
    public Transform[] lookTargets = new Transform[0];

    [Header("Сидит ли герой на станции (высота капсулы)")]
    public bool[] stationSeated = new bool[0];

    [Header("Стартовая станция")]
    public int startStation = 1;

    [Header("Клавиши станций")]
    public KeyCode cycleKey = KeyCode.Tab;                 // Shift+Tab — назад
    public KeyCode[] directKeys =
    {
        KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4,
    };

    [Header("Мышь — обзор вокруг станции")]
    public float mouseSensitivity = 2.0f;
    public float maxYaw = 82f;
    public float maxPitch = 50f;

    [Header("Сглаживание камеры")]
    [Tooltip("Скорость перелёта взгляда между станциями")]
    public float moveLerp = 7f;
    [Tooltip("Сглаживание мыши: меньше — плавнее и с запаздыванием")]
    public float lookLerp = 20f;

    [Header("Зум камеры (Z, удерживать)")]
    public KeyCode zoomKey = KeyCode.Z;
    [Tooltip("Во сколько раз сужается FOV в максимальном зуме")]
    [Range(0.2f, 0.9f)] public float zoomFovScale = 0.55f;
    public float zoomLerp = 8f;
    public float zoomMinFov = 8f;

    [Header("Приближение монитора (E)")]
    public KeyCode focusKey = KeyCode.E;
    [Range(0.4f, 0.95f)] public float focusScreenFill = 0.70f; // доля ширины кадра
    public float focusLerp = 9f;
    public float focusMaxDistance = 3f;

    [Header("Магнит экрана в приближении")]
    [Tooltip("Чувствительность мыши в фокусе, градусов на единицу оси")]
    public float focusMouseSensitivity = 0.65f;
    [Tooltip("Мягкий предел ухода от центра экрана, градусы")]
    public float focusMaxYaw = 12f;
    public float focusMaxPitch = 8f;
    [Tooltip("Скорость возврата к центру экрана, 1/с")]
    public float focusReturn = 2.4f;
    public float focusLookLerp = 12f;

    [Header("Блокировка (диалоги, провалы, конец ночи)")]
    public bool inputEnabled = true;   // переключение станций
    public bool lookEnabled = true;    // обзор мышью

    public Transform cameraTransform;

    /// <summary>Приложение сейчас принимает текст: E должна печататься, а не приближать.</summary>
    public bool TextInputActive { get; set; }

    public bool MonitorFocused => focusScreen != null;
    public bool CanFocusMonitor => focusScreen == null && aimMonitor != null && !TextInputActive;

    /// <summary>Esc в этом кадре уже закрыла приближение монитора.</summary>
    public bool EscapeClearedFocus { get; private set; }

    /// <summary>Z удерживается: камера плавно сужает кадр.</summary>
    public bool Zooming =>
        lookEnabled && inputEnabled && !TextInputActive && Input.GetKey(zoomKey);

    /// <summary>Индекс текущей станции: 0 терминал, 1 мессенджер, 2 стол, 3 кровать.</summary>
    public int Current => current;

    CharacterController cc;
    Vector3 camLocalPos;
    int current;

    Camera cam;
    float baseFov = 45f;
    float focusFov = 45f;
    float focusBlend;
    float zoomBlend;
    Quaternion focusWorldRot;
    float focusLookYaw, focusLookPitch;     // текущий мягкий сдвиг взгляда в фокусе
    float focusTargetYaw, focusTargetPitch; // цель сдвига от мыши
    Transform aimMonitor;
    Transform focusScreen;
    readonly RaycastHit[] aimHits = new RaycastHit[16];

    // E, которую забрал фокус, не должна попасть в строку ввода приложения.
    static bool focusKeyCharPending;
    // Z, которую забрал зум, не должна стать «неверной буквой» в подписи действия.
    static bool zoomKeyCharPending;

    float baseYaw, basePitch;
    float lookYaw, lookPitch;

    // Куда камера едет и куда доворачивается; мышь задаёт цель, а не кадр.
    Vector3 poseTarget;
    float baseTargetYaw, baseTargetPitch;
    float lookTargetYaw, lookTargetPitch;
    bool posed;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        if (cameraTransform == null)
        {
            Camera foundCam = GetComponentInChildren<Camera>();
            if (foundCam != null) cameraTransform = foundCam.transform;
        }
        camLocalPos = cameraTransform != null
            ? cameraTransform.localPosition
            : new Vector3(0f, 0.72f, 0f);

        cam = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
        if (cam == null) cam = GetComponentInChildren<Camera>();
        if (cam != null)
        {
            baseFov = cam.fieldOfView;
            focusFov = baseFov;
        }

        if (stations == null || stations.Length == 0 || stations[0] == null)
        {
            Debug.LogWarning("[VNR] PlayerController: станции не назначены — игрок стоит на месте.", this);
            return;
        }

        current = Mathf.Clamp(startStation, 0, stations.Length - 1);
        PoseAt(current, true);
        SetCursorLocked(true);
    }

    void Update()
    {
        focusKeyCharPending = false;
        zoomKeyCharPending = false;
        EscapeClearedFocus = false;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (MonitorFocused)
            {
                ClearFocus();
                EscapeClearedFocus = true;
            }
            else SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);
        }

        UpdateAim();
        HandleMonitorFocus();
        HandleZoom();
        HandleMouseLook();

        if (inputEnabled && stations != null && stations.Length > 0)
            HandleStations();

        UpdateSmoothPose(Time.deltaTime);
        UpdateFocus(Time.deltaTime);
    }

    void HandleStations()
    {
        int next = current;
        bool direct = false;

        if (Input.GetKeyDown(cycleKey))
        {
            bool back = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            next = back
                ? (current - 1 + stations.Length) % stations.Length
                : (current + 1) % stations.Length;
        }
        else
        {
            // F1–F4 — напрямую на станцию (цифры заняты вводом)
            for (int i = 0; i < stations.Length && i < directKeys.Length; i++)
            {
                if (Input.GetKeyDown(directKeys[i]))
                {
                    next = i;
                    direct = true;
                    break;
                }
            }
        }

        if (direct && next == current)
        {
            // повторное нажатие клавиши станции — вернуть взгляд в центр
            lookTargetYaw = 0f;
            lookTargetPitch = 0f;
            return;
        }

        if (next != current && stations[next] != null)
        {
            ClearFocus(true); // смена станции — сразу возвращаем кадр
            current = next;
            PoseAt(current);  // камера плавно перелетает к новой точке
        }
    }

    // ---------------------------------------------------------------- монитор

    /// <summary>Луч из центра кадра: наведён ли прицел на один из мониторов.</summary>
    void UpdateAim()
    {
        aimMonitor = null;
        if (cameraTransform == null || cam == null) return;
        if (MonitorFocused || !inputEnabled || !lookEnabled || TextInputActive) return;
        if (current != 0 && current != 1) return; // мониторы стоят на станциях 0 и 1

        Vector3 origin = cameraTransform.position + cameraTransform.forward * 0.05f;
        int count = Physics.RaycastNonAlloc(origin, cameraTransform.forward, aimHits,
                                            focusMaxDistance, ~0,
                                            QueryTriggerInteraction.Ignore);

        // ближайший объект, который не принадлежит самому игроку
        Transform nearest = null;
        float nearestDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Transform t = aimHits[i].collider.transform;
            if (t.IsChildOf(transform)) continue;
            if (aimHits[i].distance < nearestDist)
            {
                nearest = t;
                nearestDist = aimHits[i].distance;
            }
        }

        if (nearest != null &&
            (nearest.name == "Monitor_Terminal" || nearest.name == "Monitor_Chat"))
            aimMonitor = nearest;
    }

    void HandleMonitorFocus()
    {
        if (!inputEnabled || !Input.GetKeyDown(focusKey)) return;

        if (MonitorFocused)
        {
            // пока приложение ждёт текст, E — обычная буква (APPROVE, help, exit);
            // выйти из приближения можно ESC или сменой станции
            if (TextInputActive) return;

            focusKeyCharPending = true; // E не должна напечататься в приложении
            ClearFocus();
            return;
        }

        if (aimMonitor == null || TextInputActive) return;

        focusKeyCharPending = true; // E отдана приближению, а не строке ввода
        EnterFocus(aimMonitor);
    }

    void EnterFocus(Transform monitor)
    {
        Transform screen = monitor.Find("Screen");
        if (screen == null) screen = monitor;
        focusScreen = screen;

        Vector3 eye = cameraTransform.position;
        Vector3 dir = screen.position - eye;
        if (dir.sqrMagnitude < 0.0001f) dir = cameraTransform.forward;

        focusWorldRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        focusFov = FocusFov(Mathf.Abs(screen.lossyScale.x), dir.magnitude);
        focusLookYaw = focusLookPitch = 0f;
        focusTargetYaw = focusTargetPitch = 0f;
    }

    /// <summary>FOV, при котором экран занимает focusScreenFill ширины кадра.</summary>
    float FocusFov(float screenWidth, float distance)
    {
        if (cam == null) return baseFov;

        float halfAngle = Mathf.Atan(Mathf.Max(0.02f, screenWidth) * 0.5f /
                                     Mathf.Max(0.05f, distance));
        float fill = Mathf.Clamp(focusScreenFill, 0.4f, 0.95f);
        float hFov = 2f * halfAngle / fill;
        float vFov = 2f * Mathf.Atan(Mathf.Tan(hFov * 0.5f) /
                                     Mathf.Max(0.1f, cam.aspect));
        return Mathf.Clamp(vFov * Mathf.Rad2Deg, 8f, 30f);
    }

    /// <summary>Выйти из приближения. immediate — без плавного возврата (смена станции).</summary>
    public void ClearFocus(bool immediate = false)
    {
        focusScreen = null;
        focusTargetYaw = focusTargetPitch = 0f;
        focusLookYaw = focusLookPitch = 0f;
        if (!immediate) return;

        focusBlend = 0f;
        ApplyLook();
        if (cam != null) cam.fieldOfView = baseFov;
    }

    void UpdateFocus(float dt)
    {
        if (cameraTransform == null) return;

        float target = MonitorFocused ? 1f : 0f;
        focusBlend = Mathf.Lerp(focusBlend, target, 1f - Mathf.Exp(-focusLerp * dt));
        if (Mathf.Abs(focusBlend - target) < 0.001f) focusBlend = target;

        float targetZoom = Zooming ? 1f : 0f;
        zoomBlend = Mathf.Lerp(zoomBlend, targetZoom, 1f - Mathf.Exp(-zoomLerp * dt));
        if (Mathf.Abs(zoomBlend - targetZoom) < 0.001f) zoomBlend = targetZoom;

        // Магнит: без активного ввода цель плавно тянется к центру экрана.
        if (MonitorFocused)
        {
            float pull = TextInputActive ? focusReturn * 2.5f : focusReturn;
            float kp = 1f - Mathf.Exp(-pull * dt);
            focusTargetYaw = Mathf.LerpAngle(focusTargetYaw, 0f, kp);
            focusTargetPitch = Mathf.LerpAngle(focusTargetPitch, 0f, kp);

            float kl = 1f - Mathf.Exp(-focusLookLerp * dt);
            focusLookYaw = Mathf.LerpAngle(focusLookYaw, focusTargetYaw, kl);
            focusLookPitch = Mathf.LerpAngle(focusLookPitch, focusTargetPitch, kl);
        }
        else
        {
            focusTargetYaw = focusTargetPitch = 0f;
            focusLookYaw = focusLookPitch = 0f;
        }

        Quaternion normal = Quaternion.Euler(basePitch - lookPitch, baseYaw + lookYaw, 0f);
        Quaternion focusRot = focusWorldRot * Quaternion.Euler(-focusLookPitch, focusLookYaw, 0f);
        Quaternion world = focusBlend > 0f
            ? Quaternion.Slerp(normal, focusRot, focusBlend)
            : normal;
        cameraTransform.localRotation = Quaternion.Inverse(transform.rotation) * world;

        if (cam != null)
        {
            float fov = Mathf.Lerp(baseFov, focusFov, focusBlend);
            fov *= Mathf.Lerp(1f, zoomFovScale, zoomBlend);
            cam.fieldOfView = Mathf.Max(zoomMinFov, fov);
        }
    }

    /// <summary>Съесть E, которую фокус забрал у строки ввода.</summary>
    public static bool ConsumeFocusKeyChar(char c)
    {
        if (!focusKeyCharPending) return false;
        if (c != 'e' && c != 'E') return false;
        focusKeyCharPending = false;
        return true;
    }

    // ---------------------------------------------------------------- зум

    /// <summary>
    /// Z отдана зуму, а не подписи действия: иначе при прицеле на предмет
    /// она мигала бы как «неверная буква».
    /// </summary>
    void HandleZoom()
    {
        if (!Zooming) return;
        if (Input.GetKeyDown(zoomKey)) zoomKeyCharPending = true;
    }

    /// <summary>Съесть Z, которую зум забрал у подписи действия / строки ввода.</summary>
    public static bool ConsumeZoomKeyChar(char c)
    {
        if (!zoomKeyCharPending) return false;
        if (c != 'z' && c != 'Z') return false;
        zoomKeyCharPending = false;
        return true;
    }

    // ---------------------------------------------------------------- мышь

    void HandleMouseLook()
    {
        if (!lookEnabled || cameraTransform == null) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float rawX = Input.GetAxisRaw("Mouse X");
        float rawY = Input.GetAxisRaw("Mouse Y");

        if (MonitorFocused)
        {
            // В приближении взгляд не заморожен: мышь двигает его в маленьком
            // диапазоне, а мягкий предел и возврат к центру не дают уйти далеко.
            if (TextInputActive) return; // печатаем — держим экран ровно
            if (Mathf.Abs(rawX) > 0.0001f) focusTargetYaw += rawX * focusMouseSensitivity;
            if (Mathf.Abs(rawY) > 0.0001f) focusTargetPitch += rawY * focusMouseSensitivity;
            focusTargetYaw = SoftLimit(focusTargetYaw, focusMaxYaw);
            focusTargetPitch = SoftLimit(focusTargetPitch, focusMaxPitch);
            return;
        }

        float mx = rawX * mouseSensitivity;
        float my = rawY * mouseSensitivity;
        if (Mathf.Abs(mx) < 0.0001f && Mathf.Abs(my) < 0.0001f) return;

        lookTargetYaw = Mathf.Clamp(lookTargetYaw + mx, -maxYaw, maxYaw);
        lookTargetPitch = Mathf.Clamp(lookTargetPitch + my, -maxPitch, maxPitch);
    }

    /// <summary>Мягкий предел: вблизи центра почти линейно, к краю асимптотически.</summary>
    static float SoftLimit(float value, float limit)
    {
        if (limit <= 0.0001f) return 0f;
        return limit * (float)System.Math.Tanh(value / limit);
    }

    /// <summary>Плавно подтягивает позицию и углы к целям (станция и мышь).</summary>
    void UpdateSmoothPose(float dt)
    {
        if (!posed || cameraTransform == null) return;

        float mk = 1f - Mathf.Exp(-moveLerp * dt);
        transform.position = Vector3.Lerp(transform.position, poseTarget, mk);
        baseYaw = Mathf.LerpAngle(baseYaw, baseTargetYaw, mk);
        basePitch = Mathf.LerpAngle(basePitch, baseTargetPitch, mk);

        float lk = 1f - Mathf.Exp(-lookLerp * dt);
        lookYaw = Mathf.LerpAngle(lookYaw, lookTargetYaw, lk);
        lookPitch = Mathf.LerpAngle(lookPitch, lookTargetPitch, lk);
    }

    void ApplyLook()
    {
        if (cameraTransform == null) return;
        Quaternion world = Quaternion.Euler(basePitch - lookPitch, baseYaw + lookYaw, 0f);
        cameraTransform.localRotation = Quaternion.Inverse(transform.rotation) * world;
    }

    // ---------------------------------------------------------------- поза

    /// <summary>Мировой поворот «глаз → центр цели».</summary>
    Quaternion GazeTo(int i, Vector3 eye)
    {
        Vector3 dir = FocusPoint(i) - eye;
        if (dir.sqrMagnitude < 1e-6f) dir = stations[i].forward;
        return Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    /// <summary>Точка фокуса взгляда станции (экран); null — смотреть по forward станции.</summary>
    Transform FocusOf(int i) =>
        lookTargets != null && i < lookTargets.Length ? lookTargets[i] : null;

    Vector3 FocusPoint(int i)
    {
        Transform f = FocusOf(i);
        return (f != null ? f.position : stations[i].position + stations[i].forward * 2f);
    }

    /// <summary>Поза станции. snap — поставить камеру сразу, без перелёта.</summary>
    void PoseAt(int i, bool snap = false)
    {
        ApplyCapsule(IsSeated(i));
        poseTarget = stations[i].position - camLocalPos;

        Vector3 e = GazeTo(i, stations[i].position).eulerAngles;
        baseTargetPitch = e.x;
        baseTargetYaw = e.y;
        lookTargetYaw = 0f;
        lookTargetPitch = 0f;

        if (snap)
        {
            transform.position = poseTarget;
            basePitch = baseTargetPitch;
            baseYaw = baseTargetYaw;
            lookYaw = 0f;
            lookPitch = 0f;
            ApplyLook();
        }

        posed = true;
    }

    // ---------------------------------------------------------------- капсула

    bool IsSeated(int i) => i < stationSeated.Length && stationSeated[i];

    /// <summary>Сидя капсула ниже (герой на стуле), стоя — в полный рост.</summary>
    void ApplyCapsule(bool seated)
    {
        if (cc == null) return;
        if (seated)
        {
            cc.height = 1.2f;
            cc.center = new Vector3(0f, 0.1f, 0f);
        }
        else
        {
            cc.height = 1.8f;
            cc.center = Vector3.zero;
        }
    }

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
