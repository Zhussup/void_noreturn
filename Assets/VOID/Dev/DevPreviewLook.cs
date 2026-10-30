// Dev-инструмент окружения: осмотр комнаты мышью из точки глаз.
// Это НЕ игровая камера (её делает Жусуп). Когда появится настоящий контроллер,
// удалить объект DevPreview из сцены Env_Room.
// Работает только в редакторе и development-сборках.
using UnityEngine;
using UnityEngine.InputSystem;

namespace VOID.Dev
{
    public class DevPreviewLook : MonoBehaviour
    {
        [SerializeField] Camera targetCamera;
        [Tooltip("Клавиши 1, 2, 3... по порядку")]
        [SerializeField] Transform[] anchors = new Transform[4];
        [SerializeField] float sensitivity = 0.12f; // градусов на пиксель
        [SerializeField] float pitchLimit = 80f;
        [SerializeField] float snapTime = 0.15f;
        [SerializeField] bool showHint = true;

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
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null) return;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
            if (mouse.leftButton.wasPressedThisFrame) SetCursorLocked(true);

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude > 0f)
                {
                    snapping = false;
                    yaw += delta.x * sensitivity;
                    pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -pitchLimit, pitchLimit);
                }
            }

            if (keyboard != null)
            {
                for (int i = 0; i < anchors.Length && i < 9; i++)
                    if (anchors[i] != null && keyboard[Key.Digit1 + i].wasPressedThisFrame) SnapTo(anchors[i].rotation);
                if (keyboard.digit0Key.wasPressedThisFrame) SnapTo(startRotation);
            }

            if (snapping)
            {
                yaw = Mathf.SmoothDampAngle(yaw, snapYaw, ref yawVelocity, snapTime);
                pitch = Mathf.SmoothDamp(pitch, snapPitch, ref pitchVelocity, snapTime);
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
