// Dev-инструмент окружения: осмотр комнаты мышью из точки глаз.
// Это НЕ игровая камера (её делает PlayerController). Когда дев-осмотр
// станет не нужен, удалить объект DevPreview из сцены Env_Room.
// Работает только в редакторе и development-сборках.
//
// Проект использует старый Input Manager (activeInputHandler = 0),
// поэтому здесь Input.*, а не UnityEngine.InputSystem.
using UnityEngine;

namespace VOID.Dev
{
    public class DevPreviewLook : MonoBehaviour
    {
        [SerializeField] Camera targetCamera;
        [Tooltip("Клавиши 1, 2, 3... по порядку")]
        [SerializeField] Transform[] anchors = new Transform[4];
        [Tooltip("Градусов на пиксель движения мыши")]
        [SerializeField] float sensitivity = 0.12f; // градусов на пиксель
        [SerializeField] float pitchLimit = 80f;
        [SerializeField] float snapTime = 0.15f;
        [SerializeField] bool showHint = true;

        // Ось мыши Unity масштабирует пиксели чувствительностью из InputManager
        // (Mouse X/Y, по умолчанию 0.1). Переводим обратно в пиксели, чтобы
        // sensitivity оставалась в градусах на пиксель.
        const float AxisToPixels = 10f;

        float yaw, pitch, yawVelocity, pitchVelocity;
        float snapYaw, snapPitch;
        bool snapping;
        Quaternion startRotation;

        void Awake()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) enabled = false;
        }

        void Start()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) { enabled = false; return; }
            startRotation = targetCamera.transform.rotation;
            ReadAngles(startRotation, out yaw, out pitch);
            SetCursorLocked(true);
        }

        void OnDisable()
        {
            SetCursorLocked(false);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLocked(false);
            if (Input.GetMouseButtonDown(0)) SetCursorLocked(true);

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                float dx = Input.GetAxisRaw("Mouse X");
                float dy = Input.GetAxisRaw("Mouse Y");
                if (Mathf.Abs(dx) > 0f || Mathf.Abs(dy) > 0f)
                {
                    snapping = false;
                    yaw += dx * sensitivity * AxisToPixels;
                    pitch = Mathf.Clamp(pitch - dy * sensitivity * AxisToPixels, -pitchLimit, pitchLimit);
                }
            }

            for (int i = 0; i < anchors.Length && i < 9; i++)
                if (anchors[i] != null && Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                    SnapTo(anchors[i].rotation);
            if (Input.GetKeyDown(KeyCode.Alpha0)) SnapTo(startRotation);

            if (snapping)
            {
                yaw = Mathf.SmoothDampAngle(yaw, snapYaw, ref yawVelocity, snapTime);
                pitch = Mathf.SmoothDampAngle(pitch, snapPitch, ref pitchVelocity, snapTime);
                if (Mathf.Abs(Mathf.DeltaAngle(yaw, snapYaw)) < 0.05f && Mathf.Abs(pitch - snapPitch) < 0.05f) snapping = false;
            }

            targetCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        void OnGUI()
        {
            if (!showHint) return;
            GUI.Label(new Rect(12, 8, 900, 24),
                "Мышь — обзор   1–3 станции, 4 комната, 0 исходный вид   Esc — отпустить курсор, ЛКМ — снова захватить");
        }

        void SnapTo(Quaternion rotation)
        {
            ReadAngles(rotation, out snapYaw, out snapPitch);
            yawVelocity = pitchVelocity = 0f;
            snapping = true;
        }

        static void ReadAngles(Quaternion rotation, out float yawDeg, out float pitchDeg)
        {
            Vector3 e = rotation.eulerAngles;
            yawDeg = e.y;
            pitchDeg = e.x > 180f ? e.x - 360f : e.x;
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
