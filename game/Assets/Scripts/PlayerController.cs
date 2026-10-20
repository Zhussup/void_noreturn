using UnityEngine;

/// <summary>
/// Управление «по станциям»: свободного перемещения нет.
/// Герой сидит за столом и переводит взгляд между тремя экранами:
/// 0 — ноутбук («окно» на глубину), 1 — монитор 1 (терминал), 2 — монитор 2 (ИНЛАЙН).
/// Переключение моментальное: тело не двигается, камера-«голова» сразу
/// наводится в центр выбранного экрана по его живым координатам.
/// Клавиши: A/D или ←/→ — соседний экран (по кругу), 1–3 — прямой выбор.
/// Мыши в игре нет (концепт).
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

    [Header("Клавиши")]
    public KeyCode nextKey = KeyCode.RightArrow;
    public KeyCode nextKeyAlt = KeyCode.D;
    public KeyCode prevKey = KeyCode.LeftArrow;
    public KeyCode prevKeyAlt = KeyCode.A;

    public Transform cameraTransform;

    CharacterController cc;
    Vector3 camLocalPos;
    int current;

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

        if (stations == null || stations.Length == 0) return;

        int next = current;

        if (Input.GetKeyDown(nextKey) || Input.GetKeyDown(nextKeyAlt))
            next = (current + 1) % stations.Length;
        else if (Input.GetKeyDown(prevKey) || Input.GetKeyDown(prevKeyAlt))
            next = (current - 1 + stations.Length) % stations.Length;
        else
        {
            // 1–3 — прямо на станцию
            for (int i = 0; i < stations.Length && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    next = i;
                    break;
                }
            }
        }

        if (next != current && stations[next] != null)
        {
            current = next;
            PoseAt(current); // моментально, без анимации
        }
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
        if (cameraTransform != null)
            cameraTransform.localRotation =
                Quaternion.Inverse(transform.rotation) * GazeTo(i, stations[i].position);
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