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
    /// Собирает базовую сцену: квартира Брика ночью, рабочий стол с двумя
    /// мониторами и ноутбуком-декором, телефон, таблетки, диктофон, кровать,
    /// четыре станции игрока и игровой контроллер NightDirector.
    /// Запускается автоматически при первом открытии проекта
    /// либо вручную: меню VOID → «Пересобрать базовую сцену».
    /// </summary>
    public static class VoidSceneBuilder
    {
        const string ScenePath  = "Assets/Scenes/Office.unity";
        const string MarkerPath = "Assets/VOID/.initialized_home1";

        [MenuItem("VOID/Пересобрать базовую сцену (квартира)")]
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

            var root = new GameObject("VOID_Home");
            BuildRoom(root.transform);
            BuildBed(root.transform);
            BuildWardrobe(root.transform);
            var desk = BuildDesk(root.transform);
            BuildChair(root.transform);
            BuildTrashBin(root.transform);
            BuildStations(root.transform, desk, out Transform[] stations, out Transform[] lookTargets);
            BuildPlayer(stations, lookTargets);
            BuildGame();

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
            RenderSettings.ambientLight = new Color(0.10f, 0.11f, 0.13f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 8f;
            RenderSettings.fogEndDistance = 26f;
            RenderSettings.fogColor = new Color(0.035f, 0.04f, 0.05f);

            Light sun = new GameObject("Night Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.55f, 0.66f, 0.90f);
            sun.intensity = 0.22f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40f, 140f, 0f);
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

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m,
                              Vector3 rot = new Vector3())
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

        static GameObject Cyl(Transform parent, string name, Vector3 pos, Vector3 scale, Material m,
                              Vector3 rot = new Vector3())
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = rot;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        static GameObject Quad(Transform parent, string name, Vector3 pos, Vector3 size, Material m,
                               Vector3 rot = new Vector3())
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
            Material floor    = Mat("floor",     new Color(0.22f, 0.18f, 0.15f), 0.30f);
            Material wall     = Mat("wall",      new Color(0.34f, 0.35f, 0.32f), 0.20f);
            Material wallDark = Mat("wall_dark", new Color(0.20f, 0.22f, 0.21f), 0.20f);
            Material ceiling  = Mat("ceiling",   new Color(0.36f, 0.36f, 0.34f));
            Material metal    = Mat("metal",     new Color(0.45f, 0.45f, 0.48f), 0.6f, 0.9f);
            Material rug      = Mat("rug",       new Color(0.20f, 0.11f, 0.10f), 0.05f);

            var room = new GameObject("Room");
            room.transform.SetParent(root, false);
            Transform r = room.transform;

            Box(r, "Floor",   new Vector3(0f, -0.05f, 0f), new Vector3(6.8f, 0.10f, 5.4f), floor);
            Box(r, "Ceiling", new Vector3(0f, 3.05f, 0f),  new Vector3(6.8f, 0.10f, 5.4f), ceiling);
            Box(r, "Rug",     new Vector3(-0.2f, 0.004f, 0.35f), new Vector3(2.7f, 0.008f, 2.2f), rug);

            // северная стена: стол, постер, часы
            Box(r, "Wall_North", new Vector3(0f, 1.5f, -2.65f), new Vector3(6.8f, 3.0f, 0.1f), wall);
            Box(r, "Poster",     new Vector3(1.45f, 1.85f, -2.59f), new Vector3(0.62f, 0.86f, 0.012f), wallDark);
            Box(r, "Clock_Body", new Vector3(0f, 2.22f, -2.585f), new Vector3(0.34f, 0.17f, 0.02f), wallDark);
            Box(r, "Clock_Face", new Vector3(0f, 2.22f, -2.573f), new Vector3(0.30f, 0.135f, 0.004f),
                Unlit("clock_face", new Color(0.12f, 0.13f, 0.12f)));

            // южная стена: дверь
            Box(r, "Wall_South", new Vector3(0f, 1.5f, 2.65f), new Vector3(6.8f, 3.0f, 0.1f), wall);
            Box(r, "Door",       new Vector3(-1.2f, 1.05f, 2.59f), new Vector3(0.95f, 2.1f, 0.02f), wallDark);
            Box(r, "Door_Handle", new Vector3(-0.85f, 1.02f, 2.56f), new Vector3(0.03f, 0.10f, 0.06f), metal);

            // западная стена
            Box(r, "Wall_West", new Vector3(-3.35f, 1.5f, 0f), new Vector3(0.1f, 3.0f, 5.4f), wall);

            // восточная стена с окном (ночной город — холодная пустота)
            var east = new GameObject("Wall_East");
            east.transform.SetParent(r, false);
            Transform e = east.transform;
            Box(e, "Below", new Vector3(3.35f, 0.45f, 0f),  new Vector3(0.1f, 0.9f, 5.4f), wall);
            Box(e, "Above", new Vector3(3.35f, 2.60f, 0f),  new Vector3(0.1f, 0.8f, 5.4f), wall);
            Box(e, "Left",  new Vector3(3.35f, 1.55f, -2.0f), new Vector3(0.1f, 1.3f, 1.4f), wall);
            Box(e, "Right", new Vector3(3.35f, 1.55f, 2.0f),  new Vector3(0.1f, 1.3f, 1.4f), wall);

            // фронт квада — местная −Z; поворачиваем на 90°, чтобы окно смотрело в комнату
            Quad(e, "Window", new Vector3(3.29f, 1.55f, 0f), new Vector3(2.6f, 1.3f, 1f),
                 Unlit("window_glass", new Color(0.015f, 0.03f, 0.05f)), new Vector3(0f, 90f, 0f));
            Box(e, "Frame_Top",    new Vector3(3.29f, 2.22f, 0f),   new Vector3(0.04f, 0.06f, 2.64f), metal);
            Box(e, "Frame_Bottom", new Vector3(3.29f, 0.88f, 0f),   new Vector3(0.04f, 0.06f, 2.64f), metal);
            Box(e, "Frame_L",      new Vector3(3.29f, 1.55f, -1.32f), new Vector3(0.04f, 1.4f, 0.06f), metal);
            Box(e, "Frame_R",      new Vector3(3.29f, 1.55f, 1.32f),  new Vector3(0.04f, 1.4f, 0.06f), metal);

            // штора
            Material curtain = Mat("curtain", new Color(0.14f, 0.14f, 0.16f), 0.05f);
            Box(e, "Curtain_L", new Vector3(3.20f, 1.55f, -1.46f), new Vector3(0.06f, 1.5f, 0.5f), curtain);
            Box(e, "Curtain_R", new Vector3(3.20f, 1.55f, 1.46f),  new Vector3(0.06f, 1.5f, 0.5f), curtain);

            MakeLight(e, "Window_Glow", LightType.Point, new Vector3(2.5f, 1.7f, 0f),
                      new Color(0.38f, 0.52f, 0.78f), 0.22f, 5f);

            // потолочная лампа — почти выключена, ночь живёт на свете мониторов
            Material fixture = Unlit("fixture", new Color(1f, 0.94f, 0.82f));
            Box(r, "Lamp_1", new Vector3(0f, 2.99f, -1.0f), new Vector3(1.1f, 0.04f, 0.28f), fixture);
            MakeLight(r, "Lamp_Light_1", LightType.Point, new Vector3(0f, 2.82f, -1.0f),
                      new Color(1f, 0.88f, 0.72f), 0.28f, 7f);
        }

        // ------------------------------------------------------------ кровать и шкаф

        static void BuildBed(Transform root)
        {
            Material wood    = Mat("desk_wood",  new Color(0.29f, 0.22f, 0.17f), 0.4f);
            Material sheet   = Mat("bed_sheet",  new Color(0.52f, 0.52f, 0.48f), 0.05f);
            Material blanket = Mat("bed_blanket", new Color(0.16f, 0.18f, 0.21f), 0.03f);
            Material pillow  = Mat("bed_pillow", new Color(0.66f, 0.65f, 0.60f), 0.03f);

            var bed = new GameObject("Bed");
            bed.transform.SetParent(root, false);
            bed.transform.localPosition = new Vector3(-2.55f, 0f, 0.35f);
            Transform b = bed.transform;

            Box(b, "Base",      new Vector3(0f, 0.24f, 0f),  new Vector3(1.35f, 0.48f, 2.05f), wood);
            Box(b, "Headboard", new Vector3(-0.63f, 0.75f, 0f), new Vector3(0.06f, 1.05f, 2.05f), wood);
            Box(b, "Mattress",  new Vector3(0f, 0.52f, 0f),  new Vector3(1.24f, 0.14f, 1.92f), sheet);
            Box(b, "Blanket",   new Vector3(0f, 0.60f, 0.38f), new Vector3(1.26f, 0.07f, 1.28f), blanket);
            Box(b, "Pillow",    new Vector3(-0.34f, 0.62f, -0.72f), new Vector3(0.50f, 0.11f, 0.32f), pillow);

            // тумбочка
            var table = new GameObject("BedsideTable");
            table.transform.SetParent(root, false);
            table.transform.localPosition = new Vector3(-1.62f, 0f, -0.55f);
            Box(table.transform, "Body", new Vector3(0f, 0.25f, 0f), new Vector3(0.42f, 0.5f, 0.42f), wood);
            Box(table.transform, "Top",  new Vector3(0f, 0.51f, 0f), new Vector3(0.46f, 0.02f, 0.46f), wood);

            // настольная лампа у кровати
            var lamp = new GameObject("BedLamp");
            lamp.transform.SetParent(table.transform, false);
            lamp.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            Cyl(lamp.transform, "Stand", new Vector3(0f, 0.08f, 0f), new Vector3(0.02f, 0.08f, 0.02f), Mat("metal", new Color(0.45f, 0.45f, 0.48f), 0.6f, 0.9f));
            Cyl(lamp.transform, "Shade", new Vector3(0f, 0.20f, 0f), new Vector3(0.10f, 0.08f, 0.10f),
                Unlit("lamp_shade", new Color(0.35f, 0.29f, 0.20f)));
            MakeLight(lamp.transform, "BedLamp_Light", LightType.Point, new Vector3(0f, 0.22f, 0f),
                      new Color(1f, 0.72f, 0.45f), 0.35f, 2.2f);
        }

        static void BuildWardrobe(Transform root)
        {
            Material wood = Mat("desk_wood", new Color(0.29f, 0.22f, 0.17f), 0.4f);
            var w = new GameObject("Wardrobe");
            w.transform.SetParent(root, false);
            w.transform.localPosition = new Vector3(2.0f, 0f, 2.33f);
            Transform t = w.transform;
            Box(t, "Body", new Vector3(0f, 1.1f, 0f), new Vector3(1.3f, 2.2f, 0.55f), wood);
            Box(t, "Door_L", new Vector3(-0.31f, 1.1f, -0.285f), new Vector3(0.58f, 2.06f, 0.02f), wood);
            Box(t, "Door_R", new Vector3(0.31f, 1.1f, -0.285f), new Vector3(0.58f, 2.06f, 0.02f), wood);
        }

        // ------------------------------------------------------------ стол

        class DeskRefs
        {
            public GameObject laptopScreen, monitorTerminalScreen, monitorChatScreen;
        }

        static DeskRefs BuildDesk(Transform root)
        {
            Material wood    = Mat("desk_wood", new Color(0.29f, 0.22f, 0.17f), 0.45f);
            Material metal   = Mat("metal",     new Color(0.45f, 0.45f, 0.48f), 0.6f, 0.9f);
            Material plastic = Mat("plastic",   new Color(0.13f, 0.13f, 0.14f), 0.3f);
            Material paper   = Mat("paper",     new Color(0.85f, 0.84f, 0.79f), 0.1f);
            Material screenT = Unlit("screen_terminal", new Color(0.030f, 0.085f, 0.050f));
            Material screenC = Unlit("screen_chat",     new Color(0.030f, 0.060f, 0.065f));
            Material screenL = Unlit("screen_laptop",   new Color(0.018f, 0.060f, 0.055f));

            var refs = new DeskRefs();

            var desk = new GameObject("Desk");
            desk.transform.SetParent(root, false);
            desk.transform.localPosition = new Vector3(0f, 0f, -1.55f);
            Transform t = desk.transform;

            Box(t, "Top",        new Vector3(0f, 0.7175f, 0f), new Vector3(1.90f, 0.045f, 0.80f), wood);
            Box(t, "Leg_L",      new Vector3(-0.92f, 0.35f, 0f), new Vector3(0.050f, 0.70f, 0.78f), metal);
            Box(t, "Leg_R",      new Vector3(0.92f, 0.35f, 0f),  new Vector3(0.050f, 0.70f, 0.78f), metal);
            Box(t, "Back_Panel", new Vector3(0f, 0.50f, -0.36f), new Vector3(1.86f, 0.42f, 0.035f), wood);

            refs.monitorTerminalScreen = BuildMonitor(t, "Monitor_Terminal", -0.42f, -7f, plastic, metal, screenT,
                                                       new Color(0.35f, 1f, 0.62f));
            refs.monitorChatScreen     = BuildMonitor(t, "Monitor_Chat",      0.42f,  7f, plastic, metal, screenC,
                                                       new Color(0.45f, 0.85f, 1f));

            refs.laptopScreen = BuildLaptop(t, metal, plastic, screenL);

            // периферия
            Box(t, "Pad",      new Vector3(0f, 0.7415f, 0.27f), new Vector3(0.16f, 0.003f, 0.20f),
                Mat("pad", new Color(0.08f, 0.08f, 0.09f)));
            Box(t, "Mouse",    new Vector3(0.27f, 0.755f, 0.27f), new Vector3(0.055f, 0.030f, 0.095f), plastic);
            Box(t, "Keyboard", new Vector3(0f, 0.7535f, 0.27f), new Vector3(0.37f, 0.025f, 0.13f), plastic);

            Box(t, "Paper_1", new Vector3(-0.76f, 0.744f, 0.20f), new Vector3(0.21f, 0.004f, 0.297f), paper, new Vector3(0f, 8f, 0f));
            Box(t, "Paper_2", new Vector3(-0.66f, 0.747f, 0.12f), new Vector3(0.21f, 0.004f, 0.297f), paper, new Vector3(0f, -11f, 0f));
            Cyl(t, "Mug", new Vector3(-0.50f, 0.79f, 0.34f), new Vector3(0.085f, 0.05f, 0.085f),
                Mat("ceramic", new Color(0.78f, 0.76f, 0.72f), 0.4f));

            // стационарный телефон — триггер звонка
            var phone = new GameObject("Phone");
            phone.transform.SetParent(t, false);
            phone.transform.localPosition = new Vector3(-0.66f, 0.74f, 0.02f);
            phone.transform.localRotation = Quaternion.Euler(0f, 8f, 0f);
            Box(phone.transform, "Body",    new Vector3(0f, 0.030f, 0f), new Vector3(0.17f, 0.060f, 0.22f), plastic);
            Box(phone.transform, "Handset", new Vector3(0f, 0.080f, -0.02f), new Vector3(0.05f, 0.040f, 0.18f), plastic);
            Box(phone.transform, "Keys",    new Vector3(0f, 0.061f, 0.05f), new Vector3(0.10f, 0.004f, 0.08f),
                Mat("paper", new Color(0.85f, 0.84f, 0.79f), 0.1f));
            var led = Box(phone.transform, "Phone_LED", new Vector3(0.065f, 0.062f, 0.075f),
                          new Vector3(0.018f, 0.006f, 0.018f), Unlit("phone_led", new Color(0.15f, 0.02f, 0.02f)));
            Object.DestroyImmediate(led.GetComponent<Collider>());

            // прицел + печать PICK UP / HANG UP (см. ActionSystem)
            var inter = phone.AddComponent<Interactable>();
            inter.prompt = "PICK UP";
            inter.available = false;
            inter.maxDistance = 2.6f;

            // успокоительное: блистер и стакан воды
            var pills = new GameObject("Pills");
            pills.transform.SetParent(t, false);
            pills.transform.localPosition = new Vector3(-0.32f, 0.742f, 0.20f);
            pills.transform.localRotation = Quaternion.Euler(0f, -18f, 0f);
            Box(pills.transform, "Blister", new Vector3(0f, 0f, 0f), new Vector3(0.105f, 0.007f, 0.058f),
                Mat("blister", new Color(0.86f, 0.88f, 0.91f), 0.7f));
            Box(pills.transform, "Foil", new Vector3(0f, -0.004f, 0f), new Vector3(0.098f, 0.002f, 0.052f),
                Mat("metal", new Color(0.45f, 0.45f, 0.48f), 0.6f, 0.9f));
            for (int i = 0; i < 4; i++)
            {
                Box(pills.transform, "Cell_" + i, new Vector3(-0.036f + i * 0.024f, 0.005f, 0f),
                    new Vector3(0.014f, 0.002f, 0.014f), Mat("pill", new Color(0.92f, 0.90f, 0.84f), 0.5f));
            }

            Cyl(t, "Glass", new Vector3(-0.19f, 0.768f, 0.245f), new Vector3(0.045f, 0.032f, 0.045f),
                Mat("glass", new Color(0.55f, 0.65f, 0.68f), 0.9f));

            // диктофон — квестовый предмет
            var dict = new GameObject("Dictaphone");
            dict.transform.SetParent(t, false);
            dict.transform.localPosition = new Vector3(0.06f, 0.755f, 0.10f);
            dict.transform.localRotation = Quaternion.Euler(0f, -6f, 0f);
            Box(dict.transform, "Body", new Vector3(0f, 0f, 0f), new Vector3(0.105f, 0.030f, 0.058f), metal);
            Box(dict.transform, "Rec", new Vector3(0.035f, 0.017f, 0.020f), new Vector3(0.014f, 0.003f, 0.014f),
                Unlit("dict_led", new Color(0.55f, 0.10f, 0.08f)));

            return refs;
        }

        // ------------------------------------------------------------ станции

        /// <summary>
        /// Четыре фиксированные станции: терминал, мессенджер, стол с реквизитом
        /// и кровать. Свободного перемещения нет — меняется только взгляд/поза.
        /// </summary>
        static void BuildStations(Transform root, DeskRefs refs,
                                  out Transform[] stations, out Transform[] lookTargets)
        {
            var group = new GameObject("Stations");
            group.transform.SetParent(root, false);

            Vector3 seatEye = new Vector3(0f, 1.25f, -0.75f);

            var deskTarget = new GameObject("Desk_LookTarget");
            deskTarget.transform.SetParent(group.transform, false);
            deskTarget.transform.position = new Vector3(0.02f, 0.92f, -1.34f);

            var bedTarget = new GameObject("Bed_LookTarget");
            bedTarget.transform.SetParent(group.transform, false);
            bedTarget.transform.position = new Vector3(-2.72f, 0.62f, -0.34f);

            Transform terminal   = Station(group.transform, "Station_Terminal", seatEye, refs.monitorTerminalScreen.transform.position);
            Transform messenger  = Station(group.transform, "Station_Messenger", seatEye, refs.monitorChatScreen.transform.position);
            Transform desk       = Station(group.transform, "Station_Desk", seatEye, deskTarget.transform.position);
            Transform bed        = Station(group.transform, "Station_Bed", new Vector3(-1.62f, 1.02f, 0.55f), bedTarget.transform.position);

            stations = new[] { terminal, messenger, desk, bed };
            lookTargets = new[]
            {
                refs.monitorTerminalScreen.transform,
                refs.monitorChatScreen.transform,
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

        static GameObject BuildMonitor(Transform t, string name, float x, float rotY,
                                       Material plastic, Material metal, Material screen, Color glow)
        {
            var m = new GameObject(name);
            m.transform.SetParent(t, false);
            m.transform.localPosition = new Vector3(x, 0.74f, -0.14f);
            m.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            Transform mt = m.transform;

            Box(mt, "Base", new Vector3(0f, 0.012f, 0f), new Vector3(0.22f, 0.022f, 0.17f), metal);
            Cyl(mt, "Pole", new Vector3(0f, 0.105f, 0f), new Vector3(0.060f, 0.100f, 0.060f), metal);
            Box(mt, "Bezel", new Vector3(0f, 0.395f, 0f), new Vector3(0.55f, 0.36f, 0.05f), plastic);
            // квад смотрит местной −Z; разворот 180° — экран лицом в комнату
            GameObject screenQuad = Quad(mt, "Screen", new Vector3(0f, 0.395f, 0.027f),
                new Vector3(0.50f, 0.31f, 1f), screen, new Vector3(0f, 180f, 0f));
            MakeLight(mt, "Glow", LightType.Point, new Vector3(0f, 0.38f, 0.28f), glow, 0.55f, 1.5f);
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
            GameObject screenQuad = Quad(lid.transform, "Screen", new Vector3(0f, 0.108f, 0.008f),
                new Vector3(0.30f, 0.18f, 1f), screen, new Vector3(0f, 180f, 0f));
            MakeLight(lt, "Glow", LightType.Point, new Vector3(0f, 0.14f, 0.22f),
                      new Color(0.30f, 0.90f, 0.80f), 0.30f, 1.1f);
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
