using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace VNR.EditorTools
{
    /// <summary>
    /// Собирает игровую сцену из комнаты, которую сделал друг (Assets/VOID/Scenes/Env_Room.unity).
    /// Комната и её префабы/материалы — его работа; билдер только убирает dev-камеры,
    /// добавляет игрока, четыре станции и NightDirector и сохраняет копию в Office.unity.
    /// Исходная сцена не изменяется.
    /// Запускается автоматически при первом открытии проекта
    /// либо вручную: меню VOID → «Пересобрать базовую сцену (квартира)».
    /// </summary>
    public static class VoidSceneBuilder
    {
        const string ScenePath      = "Assets/Scenes/Office.unity";
        const string FriendRoomPath = "Assets/VOID/Scenes/Env_Room.unity";
        const string MarkerPath     = "Assets/VOID/.initialized_home2";

        [MenuItem("VOID/Пересобрать базовую сцену (квартира)")]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlaying) return;
            if (!File.Exists(FriendRoomPath))
            {
                Debug.LogError("[VOID] Нет комнаты " + FriendRoomPath +
                               " — скопируй ассеты из ветки Alauddinpart.");
                return;
            }
            BuildAndSaveScene();
            Debug.Log("[VOID] Сцена пересобрана: " + ScenePath);
        }

        public static void BuildAndSaveScene()
        {
            Scene scene = EditorSceneManager.OpenScene(FriendRoomPath, OpenSceneMode.Single);

            // В комнате друга есть dev-камера с превью-скриптом (Input System).
            // Игре он не нужен: остаётся ровно одна камера — на игроке.
            RemoveAllCameras();
            RemoveObject("DevPreview");
            RemoveObject("Cameras");

            BuildPhoneLed();

            Transform terminal = FindScreen("Monitor_Terminal");
            Transform chat     = FindScreen("Monitor_Chat");
            BuildStations(terminal, chat,
                          out Transform[] stations, out Transform[] lookTargets);
            BuildPlayer(stations, lookTargets);
            BuildGame();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            Directory.CreateDirectory("Assets/VOID");
            File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("u"));
            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------ очистка сцены

        static void RemoveAllCameras()
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
                if (cameras[i] != null) Object.DestroyImmediate(cameras[i].gameObject);

            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            for (int i = 0; i < listeners.Length; i++)
                if (listeners[i] != null) Object.DestroyImmediate(listeners[i].gameObject);
        }

        static void RemoveObject(string name)
        {
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform[] all = roots[r].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].name == name)
                    {
                        Object.DestroyImmediate(all[i].gameObject);
                        return;
                    }
                }
            }
        }

        // ------------------------------------------------------------ материалы

        static Material Unlit(string name, Color c)
        {
            string path = "Assets/VOID/Materials/" + name + ".mat";
            Material cached = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (cached != null) return cached;

            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
            var m = new Material(shader) { name = name, color = c };
            Directory.CreateDirectory("Assets/VOID/Materials");
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material Mat(string name, Color c, float smooth = 0.5f, float metal = 0f)
        {
            string path = "Assets/VOID/Materials/" + name + ".mat";
            Material cached = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (cached != null) return cached;

            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
            var m = new Material(shader) { name = name };
            if (shader.name == "Standard")
            {
                m.SetColor("_Color", c);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", metal);
            }
            else
            {
                m.color = c;
            }
            Directory.CreateDirectory("Assets/VOID/Materials");
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ------------------------------------------------------------ телефон

        /// <summary>
        /// В префабе телефона друга нет LED, а игра мигает им на звонке.
        /// Добавляем маленький кубик на корпус: NightDirector ищет Phone_LED.
        /// </summary>
        static void BuildPhoneLed()
        {
            GameObject phone = GameObject.Find("Phone");
            if (phone == null) return;
            if (phone.transform.Find("Phone_LED") != null) return;

            var led = GameObject.CreatePrimitive(PrimitiveType.Cube);
            led.name = "Phone_LED";
            led.transform.SetParent(phone.transform, false);
            led.transform.localPosition = new Vector3(0.065f, 0.092f, 0.085f);
            led.transform.localScale = new Vector3(0.018f, 0.006f, 0.018f);
            Object.DestroyImmediate(led.GetComponent<Collider>());

            MeshRenderer mr = led.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = Unlit("phone_led", new Color(0.15f, 0.02f, 0.02f));
        }

        // ------------------------------------------------------------ станции

        static Transform FindScreen(string monitorName)
        {
            GameObject m = GameObject.Find(monitorName);
            if (m == null) return null;
            Transform t = m.transform.Find("Screen");
            return t != null ? t : m.transform;
        }

        /// <summary>
        /// Четыре фиксированные станции: терминал, мессенджер, стол с реквизитом
        /// и кровать. Свободного перемещения нет — меняется только взгляд/поза.
        /// Координаты — из комнаты друга: глаз сидящего (0, 1.25, −0.55).
        /// </summary>
        static void BuildStations(Transform terminalScreen, Transform chatScreen,
                                  out Transform[] stations, out Transform[] lookTargets)
        {
            var group = new GameObject("Stations");
            Transform g = group.transform;

            Vector3 seatEye = new Vector3(0f, 1.25f, -0.55f);

            // рабочий стол: телефон, таблетки, диктофон в одном кадре
            var deskTarget = new GameObject("Desk_LookTarget");
            deskTarget.transform.SetParent(g, false);
            deskTarget.transform.position = new Vector3(0.55f, 0.80f, -1.45f);

            // кровать: изголовье у южной стены (подушка z ≈ +1.38)
            var bedTarget = new GameObject("Bed_LookTarget");
            bedTarget.transform.SetParent(g, false);
            bedTarget.transform.position = new Vector3(1.75f, 0.55f, 1.38f);

            Vector3 terminalPoint = terminalScreen != null
                ? terminalScreen.position
                : new Vector3(0.38f, 0.93f, -1.42f);
            Vector3 chatPoint = chatScreen != null
                ? chatScreen.position
                : new Vector3(-0.38f, 0.93f, -1.42f);

            Transform terminal  = Station(g, "Station_Terminal", seatEye, terminalPoint);
            Transform messenger = Station(g, "Station_Messenger", seatEye, chatPoint);
            Transform desk      = Station(g, "Station_Desk", seatEye, deskTarget.transform.position);
            Transform bed       = Station(g, "Station_Bed", new Vector3(1.75f, 1.02f, 0.15f),
                                          bedTarget.transform.position);

            stations = new[] { terminal, messenger, desk, bed };
            lookTargets = new[]
            {
                terminalScreen != null ? terminalScreen : terminal,
                chatScreen != null ? chatScreen : messenger,
                deskTarget.transform,
                bedTarget.transform,
            };
        }

        static Transform Station(Transform parent, string name, Vector3 eye, Vector3 lookAt)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = eye;
            go.transform.rotation = Quaternion.LookRotation(lookAt - eye, Vector3.up);
            return go.transform;
        }

        // ------------------------------------------------------------ игрок и игра

        static void BuildPlayer(Transform[] stations, Transform[] lookTargets)
        {
            Material body = Mat("player_body", new Color(0.20f, 0.21f, 0.23f), 0.3f);

            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0.53f, -0.55f);
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Body";
            Object.DestroyImmediate(capsule.GetComponent<CapsuleCollider>());
            capsule.transform.SetParent(player.transform, false);
            capsule.transform.localScale = new Vector3(0.55f, 1f, 0.55f);
            capsule.GetComponent<MeshRenderer>().sharedMaterial = body;
            // вид от первого лица: тело не должно попадать в кадр
            capsule.GetComponent<MeshRenderer>().enabled = false;

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = Vector3.zero;
            cc.skinWidth = 0.02f;

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            Camera cam = camGo.GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            cam.fieldOfView = 45f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.015f, 0.02f);

            PlayerController ctrl = player.AddComponent<PlayerController>();
            ctrl.cameraTransform = camGo.transform;
            ctrl.stations = stations;
            ctrl.lookTargets = lookTargets;
            ctrl.stationSeated = new[] { true, true, true, true };
            ctrl.startStation = 0; // смена начинается лицом к терминалу
            player.tag = "Player";
        }

        static void BuildGame()
        {
            var go = new GameObject("Game");
            go.AddComponent<NightDirector>();
        }

        // ------------------------------------------------------------ автозапуск

        [InitializeOnLoad]
        static class AutoSetup
        {
            static AutoSetup()
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                EditorApplication.delayCall += () =>
                {
                    if (File.Exists(MarkerPath)) return;
                    if (EditorApplication.isPlaying) return;
                    if (!File.Exists(FriendRoomPath)) return;
                    try
                    {
                        BuildAndSaveScene();
                        Debug.Log("[VOID] Базовая сцена собрана автоматически: " + ScenePath +
                                  " (переиграть: VOID → Пересобрать базовую сцену)");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("[VOID] Автосборка сцены не удалась — запусти меню VOID → «Пересобрать базовую сцену». Ошибка: " + e.Message);
                    }
                };
            }
        }
    }
}
