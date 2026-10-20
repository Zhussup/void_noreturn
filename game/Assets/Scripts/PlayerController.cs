using UnityEngine;

/// <summary>
/// Управление «по станциям»: свободного перемещения нет.
/// Герой сидит за столом и переводит взгляд между физическими точками:
/// 0 — монитор 1 (терминал), 1 — монитор 2 (мессенджер),
/// 2 — стол (телефон, таблетки, диктофон), 3 — кровать (отдых).
/// Переключение моментальное: тело не двигается, камера-«голова» сразу
/// наводится в центр выбранной точки по её живым координатам.
///
/// Мышь — свободный обзор вокруг станции (в пределах ±82° по горизонтали
/// и ±50° по вертикали), чтобы наводить прицел на предметы. Кликов нет.
/// Повторное нажатие F1–F4 на текущей станции возвращает взгляд в центр.
/// Клавиши: TAB / Shift+TAB — соседняя станция, F1–F4 — прямой выбор.
/// Буквы и цифры не заняты переключением: на экранах игрок печатает команды.
/// </summary>
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

    [Header("Блокировка (диалоги, провалы, конец ночи)")]
    public bool inputEnabled = true;   // переключение станций
    public bool lookEnabled = true;    // обзор мышью

    public Transform cameraTransform;

    /// <summary>Индекс текущей станции: 0 терминал, 1 мессенджер, 2 стол, 3 кровать.</summary>
    public int Current => current;

    CharacterController cc;
    Vector3 camLocalPos;
    int current;

    float baseYaw, basePitch;
    float lookYaw, lookPitch;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }
        camLocalPos = cameraTransform != null
            ? cameraTransform.localPosition
            : new Vector3(0f, 0.72f, 0f);

        if (stations == null || stations.Length == 0 || stations[0] == null)
        {
            Debug.LogWarning("[VNR] PlayerController: станции не назначены — игрок стоит на месте.", this);
            return;
        }

        current = Mathf.Clamp(startStation, 0, stations.Length - 1);
        PoseAt(current);
        SetCursorLocked(true);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);

        HandleMouseLook();

        if (!inputEnabled) return;
        if (stations == null || stations.Length == 0) return;

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
            lookYaw = 0f;
            lookPitch = 0f;
            ApplyLook();
            return;
        }

        if (next != current && stations[next] != null)
        {
            current = next;
            PoseAt(current); // моментально, без анимации
        }
    }

    // ---------------------------------------------------------------- мышь

    void HandleMouseLook()
    {
        if (!lookEnabled || cameraTransform == null) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float mx = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float my = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;
        if (Mathf.Abs(mx) < 0.0001f && Mathf.Abs(my) < 0.0001f) return;

        lookYaw = Mathf.Clamp(lookYaw + mx, -maxYaw, maxYaw);
        lookPitch = Mathf.Clamp(lookPitch + my, -maxPitch, maxPitch);
        ApplyLook();
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

    void PoseAt(int i)
    {
        ApplyCapsule(IsSeated(i));
        transform.position = stations[i].position - camLocalPos;

        Vector3 e = GazeTo(i, stations[i].position).eulerAngles;
        basePitch = e.x;
        baseYaw = e.y;
        lookPitch = 0f;
        lookYaw = 0f;
        ApplyLook();
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
