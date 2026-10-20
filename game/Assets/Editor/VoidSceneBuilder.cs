using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace VNR.EditorTools
{
    /// <summary>
    /// Собирает базовую сцену Office: комната, стол со стулом, ноутбук,
    /// два монитора, служебный телефон, игрок от первого лица.
    /// Запускается автоматически при первом открытии проекта
    /// либо вручную: меню VOID → «Пересобрать базовую сцену (Office)».
    /// </summary>
    public static class VoidSceneBuilder
    {
        const string ScenePath  = "Assets/Scenes/Office.unity";
        const string MarkerPath = "Assets/VOID/.initialized";

        [MenuItem("VOID/Пересобрать базовую сцену (Office)")]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlaying) return;
            BuildAndSaveScene();
            Debug.Log("[VOID] Сцена пересобрана: " + ScenePath);
        }

        public static void BuildAndSaveScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupEnvironment();

            var root = new GameObject("VOID_Office");
            BuildRoom(root.transform);
            var desk = BuildDesk(root.transform);
            BuildChair(root.transform);
            BuildTrashBin(root.transform);
            BuildStations(root.transform, desk, out Transform[] stations, out Transform[] lookTargets);
            BuildPlayer(stations, lookTargets);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            Directory.CreateDirectory("Assets/VOID");
            File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("u"));
            AssetDatabase.Refresh();
        }

        // ---------------------------------------------------------------- окружение

        static void SetupEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.16f, 0.19f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 9f;
            RenderSettings.fogEndDistance = 30f;
            RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.07f);

            Light sun = new GameObject("Night Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.93f, 0.82f);
            sun.intensity = 0.55f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, 30f, 0f);
        }

        // ------------------------------------------------------------ материалы

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

        // ------------------------------------------------------------ примитивы

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m, Vector3 rot = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = rot;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        static GameObject Cyl(Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        static GameObject Quad(Transform parent, string name, Vector3 pos, Vector3 size, Material m, Vector3 rot = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = rot;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        static Light MakeLight(Transform parent, string name, LightType type, Vector3 pos,
                               Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            Light l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        // ------------------------------------------------------------ комната

        static void BuildRoom(Transform root)
        {
            Material floor    = Mat("floor",    new Color(0.32f, 0.30f, 0.27f), 0.35f);
            Material wall     = Mat("wall",     new Color(0.42f, 0.44f, 0.39f), 0.25f);
            Material wallDark = Mat("wall_dark", new Color(0.31f, 0.33f, 0.30f), 0.25f);
            Material ceiling  = Mat("ceiling",  new Color(0.55f, 0.55f, 0.53f));
            Material metal    = Mat("metal",    new Color(0.45f, 0.45f, 0.48f), 0.6f, 0.9f);

            var room = new GameObject("Room");
            room.transform.SetParent(root, false);
            Transform r = room.transform;

            Box(r, "Floor",   new Vector3(0f, -0.05f, 0f),  new Vector3(6.8f, 0.10f, 5.4f), floor);
            Box(r, "Ceiling", new Vector3(0f, 3.05f, 0f),   new Vector3(6.8f, 0.10f, 5.4f), ceiling);

            // стена за столом + декоративный постер
            Box(r, "Wall_North", new Vector3(0f, 1.5f, -2.65f), new Vector3(6.8f, 3.0f, 0.1f), wall);
            Box(r, "Poster",     new Vector3(1.6f, 1.75f, -2.59f), new Vector3(0.65f, 0.9f, 0.01f), wallDark);

            // южная стена + «дверь» (временно декоративный прямоугольник)
            Box(r, "Wall_South", new Vector3(0f, 1.5f, 2.65f), new Vector3(6.8f, 3.0f, 0.1f), wall);
            Box(r, "Door",       new Vector3(-1.2f, 1.05f, 2.59f), new Vector3(0.95f, 2.1f, 0.02f), wallDark);

            // западная стена
            var west = new GameObject("Wall_West");
            west.transform.SetParent(r, false);
            Box(west.transform, "Segment", new Vector3(-3.35f, 1.5f, 0f), new Vector3(0.1f, 3.0f, 5.4f), wall);

            // восточная стена с окном (в окне — ночная пустота)
            var east = new GameObject("Wall_East");
            east.transform.SetParent(r, false);
            Transform e = east.transform;
            Box(e, "Below",  new Vector3(3.35f, 0.45f, 0f),   new Vector3(0.1f, 0.9f, 5.4f), wall);
            Box(e, "Above",  new Vector3(3.35f, 2.60f, 0f),   new Vector3(0.1f, 0.8f, 5.4f), wall);
            Box(e, "Left",   new Vector3(3.35f, 1.55f, -2.0f), new Vector3(0.1f, 1.3f, 1.4f), wall);
            Box(e, "Right",  new Vector3(3.35f, 1.55f, 2.0f),  new Vector3(0.1f, 1.3f, 1.4f), wall);

            Quad(e, "Window", new Vector3(3.29f, 1.55f, 0f), new Vector3(2.6f, 1.3f, 1f),
                 Unlit("window_glass", new Color(0.02f, 0.04f, 0.06f)), new Vector3(0f, -90f, 0f));
            Box(e, "Frame_Top",    new Vector3(3.29f, 2.22f, 0f),  new Vector3(0.04f, 0.06f, 2.64f), metal);
            Box(e, "Frame_Bottom", new Vector3(3.29f, 0.88f, 0f),  new Vector3(0.04f, 0.06f, 2.64f), metal);
            Box(e, "Frame_L",      new Vector3(3.29f, 1.55f, -1.32f), new Vector3(0.04f, 1.4f, 0.06f), metal);
            Box(e, "Frame_R",      new Vector3(3.29f, 1.55f, 1.32f),  new Vector3(0.04f, 1.4f, 0.06f), metal);
            MakeLight(e, "Window_Glow", LightType.Point, new Vector3(2.6f, 1.8f, 0f),
                      new Color(0.45f, 0.60f, 0.85f), 0.3f, 5f);

            // потолочные лампы
            Material fixture = Unlit("fixture", new Color(1f, 0.96f, 0.88f));
            Box(r, "Lamp_1", new Vector3(0f, 2.99f, -1.2f), new Vector3(1.3f, 0.04f, 0.3f), fixture);
            Box(r, "Lamp_2", new Vector3(0f, 2.99f, 1.2f),  new Vector3(1.3f, 0.04f, 0.3f), fixture);
            MakeLight(r, "Lamp_Light_1", LightType.Point, new Vector3(0f, 2.82f, -1.2f),
                      new Color(1f, 0.92f, 0.80f), 0.85f, 8.5f);
            MakeLight(r, "Lamp_Light_2", LightType.Point, new Vector3(0f, 2.82f, 1.2f),
                      new Color(1f, 0.92f, 0.80f), 0.60f, 8.5f);
        }

        // ------------------------------------------------------------ стол

        class DeskRefs
        {
            public GameObject laptopScreen, monitorTerminalScreen, monitorChatScreen;
        }

        static DeskRefs BuildDesk(Transform root)
        {
            Material wood    = Mat("desk_wood", new Color(0.35f, 0.27f, 0.20f), 0.45f);
            Material metal   = Mat("metal",     new Color(0.45f, 0.45f, 0.48f), 0.6f, 0.9f);
            Material plastic = Mat("plastic",   new Color(0.13f, 0.13f, 0.14f), 0.3f);
            Material paper   = Mat("paper",     new Color(0.85f, 0.84f, 0.79f), 0.1f);
            Material screenT = Unlit("screen_terminal", new Color(0.04f, 0.26f, 0.15f));
            Material screenL = Unlit("screen_laptop",   new Color(0.06f, 0.27f, 0.24f));

            var refs = new DeskRefs();

            var desk = new GameObject("Desk");
            desk.transform.SetParent(root, false);
            desk.transform.localPosition = new Vector3(0f, 0f, -1.55f);
            Transform t = desk.transform;

            Box(t, "Top",        new Vector3(0f, 0.7175f, 0f),     new Vector3(1.90f, 0.045f, 0.80f), wood);
            Box(t, "Leg_L",      new Vector3(-0.92f, 0.35f, 0f),   new Vector3(0.050f, 0.70f, 0.78f), metal);
            Box(t, "Leg_R",      new Vector3(0.92f, 0.35f, 0f),    new Vector3(0.050f, 0.70f, 0.78f), metal);
            Box(t, "Back_Panel", new Vector3(0f, 0.50f, -0.36f),   new Vector3(1.86f, 0.42f, 0.035f), wood);

            refs.monitorTerminalScreen = BuildMonitor(t, "Monitor_Terminal", -0.42f, -7f, plastic, metal, screenT);
            refs.monitorChatScreen     = BuildMonitor(t, "Monitor_Chat",      0.42f,  7f, plastic, metal, screenT);

            refs.laptopScreen = BuildLaptop(t, metal, plastic, screenL);

            // периферия стола
            Box(t, "Pad",      new Vector3(0f, 0.7415f, 0.27f),   new Vector3(0.16f, 0.003f, 0.20f), Mat("pad", new Color(0.08f, 0.08f, 0.09f)));
            Box(t, "Mouse",    new Vector3(0.27f, 0.755f, 0.27f), new Vector3(0.055f, 0.030f, 0.095f), plastic);
            Box(t, "Keyboard", new Vector3(0f, 0.7535f, 0.27f),   new Vector3(0.37f, 0.025f, 0.13f), plastic);

            Box(t, "Paper_1", new Vector3(-0.70f, 0.744f, 0.22f), new Vector3(0.21f, 0.004f, 0.297f), paper, new Vector3(0f, 6f, 0f));
            Box(t, "Paper_2", new Vector3(-0.60f, 0.747f, 0.15f), new Vector3(0.21f, 0.004f, 0.297f), paper, new Vector3(0f, -9f, 0f));
            Cyl(t, "Mug",     new Vector3(-0.50f, 0.79f, 0.33f),  new Vector3(0.085f, 0.05f, 0.085f), Mat("ceramic", new Color(0.80f, 0.78f, 0.74f), 0.4f));

            // рабочий телефон (его сюжет — из 01_story.md)
            var phone = new GameObject("Phone");
            phone.transform.SetParent(t, false);
            phone.transform.localPosition = new Vector3(-0.60f, 0.74f, 0.03f);
            Box(phone.transform, "Body",    new Vector3(0f, 0.035f, 0f),    new Vector3(0.17f, 0.070f, 0.22f), plastic);
            Box(phone.transform, "Handset", new Vector3(0f, 0.085f, -0.02f), new Vector3(0.05f, 0.040f, 0.18f), plastic);

            // диктофон (квестовый предмет)
            Box(t, "Dictaphone", new Vector3(0.05f, 0.755f, 0.10f), new Vector3(0.10f, 0.030f, 0.055f), metal);

            return refs;
        }

        // ------------------------------------------------------------ станции

        /// <summary>
        /// Три фиксированные позиции героя за столом: ноутбук («окно» на глубину),
        /// монитор 1 (терминал), монитор 2 (ИНЛАЙН).
        /// Все станции — одна точка сидения: герой не ходит и не разворачивается,
        /// меняется только направление взгляда (камера-«голова»).
        /// Взгляд наводится на lookTargets — квады экранов.
        /// </summary>
        static void BuildStations(Transform root, DeskRefs refs,
                                  out Transform[] stations, out Transform[] lookTargets)
        {
            var group = new GameObject("Stations");
            group.transform.SetParent(root, false);

            Vector3 seatEye = new Vector3(0f, 1.25f, -0.75f); // глаза сидящего на стуле

            Transform laptop   = Station(group.transform, "Station_Laptop",   seatEye, refs.laptopScreen.transform.position);
            Transform monitor1 = Station(group.transform, "Station_Monitor1", seatEye, refs.monitorTerminalScreen.transform.position);
            Transform monitor2 = Station(group.transform, "Station_Monitor2", seatEye, refs.monitorChatScreen.transform.position);

            stations = new[] { laptop, monitor1, monitor2 };
            lookTargets = new[]
            {
                refs.laptopScreen.transform,
                refs.monitorTerminalScreen.transform,
                refs.monitorChatScreen.transform,
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

        static GameObject BuildMonitor(Transform t, string name, float x, float rotY,
                                       Material plastic, Material metal, Material screen)
        {
            var m = new GameObject(name);
            m.transform.SetParent(t, false);
            m.transform.localPosition = new Vector3(x, 0.74f, -0.14f);
            m.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            Transform mt = m.transform;

            Box(mt, "Base", new Vector3(0f, 0.012f, 0f),    new Vector3(0.22f, 0.022f, 0.17f), metal);
            Cyl(mt, "Pole", new Vector3(0f, 0.105f, 0f),    new Vector3(0.060f, 0.100f, 0.060f), metal);
            Box(mt, "Bezel", new Vector3(0f, 0.395f, 0f),   new Vector3(0.55f, 0.36f, 0.05f), plastic);
            GameObject screenQuad = Quad(mt, "Screen", new Vector3(0f, 0.395f, 0.027f), new Vector3(0.50f, 0.31f, 1f), screen);
            MakeLight(mt, "Glow", LightType.Point, new Vector3(0f, 0.38f, 0.28f),
                      new Color(0.35f, 1f, 0.62f), 0.55f, 1.5f);
            return screenQuad;
        }

        static GameObject BuildLaptop(Transform t, Material metal, Material plastic, Material screen)
        {
            var lap = new GameObject("Laptop");
            lap.transform.SetParent(t, false);
            lap.transform.localPosition = new Vector3(0.72f, 0.74f, -0.02f);
            lap.transform.localRotation = Quaternion.Euler(0f, -24f, 0f);
            Transform lt = lap.transform;

            Box(lt, "Base", new Vector3(0f, 0.011f, 0f), new Vector3(0.34f, 0.022f, 0.24f), metal);

            var lid = new GameObject("Lid");
            lid.transform.SetParent(lt, false);
            lid.transform.localPosition = new Vector3(0f, 0.02f, -0.117f);
            lid.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            Box(lid.transform, "Lid", new Vector3(0f, 0.108f, 0f), new Vector3(0.34f, 0.215f, 0.014f), plastic);
            GameObject screenQuad = Quad(lid.transform, "Screen", new Vector3(0f, 0.108f, 0.008f), new Vector3(0.30f, 0.18f, 1f), screen);
            MakeLight(lt, "Glow", LightType.Point, new Vector3(0f, 0.14f, 0.22f),
                      new Color(0.30f, 0.90f, 0.80f), 0.4f, 1.1f);
            return screenQuad;
        }

        // ------------------------------------------------------------ остальное

        static void BuildChair(Transform root)
        {
            Material fabric = Mat("fabric", new Color(0.17f, 0.18f, 0.22f), 0.15f);
            Material metal  = Mat("metal",  new Color(0.45f, 0.45f, 0.48f), 0.6f, 0.9f);

            var chair = new GameObject("Chair");
            chair.transform.SetParent(root, false);
            chair.transform.localPosition = new Vector3(0f, 0f, -0.72f);
            Transform c = chair.transform;

            Cyl(c, "Base",     new Vector3(0f, 0.033f, 0f), new Vector3(0.34f, 0.033f, 0.34f), metal);
            Cyl(c, "Post",     new Vector3(0f, 0.20f, 0f),  new Vector3(0.060f, 0.20f, 0.060f), metal);
            Box(c, "Seat",     new Vector3(0f, 0.44f, 0f),  new Vector3(0.48f, 0.08f, 0.46f), fabric);
            Box(c, "Backrest", new Vector3(0f, 0.83f, 0.235f), new Vector3(0.46f, 0.55f, 0.07f), fabric, new Vector3(8f, 0f, 0f));
        }

        static void BuildTrashBin(Transform root)
        {
            var bin = new GameObject("TrashBin");
            bin.transform.SetParent(root, false);
            bin.transform.localPosition = new Vector3(1.55f, 0f, -1.85f);
            Cyl(bin.transform, "Body", new Vector3(0f, 0.3f, 0f), new Vector3(0.30f, 0.30f, 0.30f),
                Mat("bin", new Color(0.20f, 0.21f, 0.23f), 0.4f));
        }

        static void BuildPlayer(Transform[] stations, Transform[] lookTargets)
        {
            Material body = Mat("player_body", new Color(0.20f, 0.21f, 0.23f), 0.3f);

            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 1.0f, 0.9f);
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Body";
            Object.DestroyImmediate(capsule.GetComponent<CapsuleCollider>());
            capsule.transform.SetParent(player.transform, false);
            capsule.transform.localScale = new Vector3(0.55f, 1f, 0.55f);
            capsule.GetComponent<MeshRenderer>().sharedMaterial = body;

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
            cam.fieldOfView = 45f; // ближе к фокусному взгляду на экран за рабочим столом
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.03f, 0.04f);

            PlayerController ctrl = player.AddComponent<PlayerController>();
            ctrl.cameraTransform = camGo.transform;
            ctrl.stations = stations;
            ctrl.lookTargets = lookTargets;
            // все три станции — сидя за столом
            ctrl.stationSeated = new[] { true, true, true };
            ctrl.startStation = 1; // смена начинается лицом к терминалу
            player.tag = "Player";
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