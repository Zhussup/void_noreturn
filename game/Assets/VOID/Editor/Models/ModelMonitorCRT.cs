using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VOID.EnvModels
{
    public static class ModelMonitorCRT
    {
        const string BaseName = "PRB_Monitor_CRT";
        static readonly string[] Variants = { "PRB_Monitor_Terminal", "PRB_Monitor_Chat" };
        const string EnvRoom = "Assets/VOID/Scenes/Env_Room.unity";

        // Screen из greybox: центр (0, 0.19, 0.2008), 0.32 × 0.24, смотрит в +Z.
        const float ScreenY = 0.19f;
        const float FrontZ = 0.214f;     // передняя плоскость рамки — экран утоплен на 13 мм
        const float NicheZ = 0.196f;     // дно ниши за экраном
        const float OutHX = 0.2f, OutY0 = 0.03f, OutY1 = 0.35f;
        const float InHX = 0.165f, InHY = 0.125f;   // проём 0.33 × 0.25

        [MenuItem("VOID/Env/Models/Monitor_CRT")]
        public static void Build()
        {
            Mesh mesh = BuildMesh();
            PBKit.Log("Monitor_CRT", mesh);
            BuildPrefabs(mesh);
        }

        static Mesh BuildMesh()
        {
            var b = new PBKit.Builder();

            // ---------- рамка с нишей под Screen
            float oc = (OutY0 + OutY1) * 0.5f, ohy = (OutY1 - OutY0) * 0.5f;
            const float ch = 0.006f;
            var rings = new[]
            {
                Rect(0f, oc, OutHX, ohy, 0.15f),
                Rect(0f, oc, OutHX, ohy, FrontZ - ch),
                Rect(0f, oc, OutHX - ch, ohy - ch, FrontZ),
                Rect(0f, ScreenY, InHX + ch, InHY + ch, FrontZ),
                Rect(0f, ScreenY, InHX, InHY, FrontZ - ch),
                Rect(0f, ScreenY, InHX, InHY, NicheZ),
            };
            // подсказки нормалей по сегментам: (наружу, вперёд)
            var hints = new[] { (1f, 0f), (1f, 1f), (0f, 1f), (-1f, 1f), (-1f, 0f) };
            var poly = new PBKit.Poly();
            for (int s = 0; s < rings.Length - 1; s++)
                for (int k = 0; k < 4; k++)
                {
                    int k1 = (k + 1) % 4;
                    Vector3 a = rings[s][k], bb = rings[s][k1], c = rings[s + 1][k1], d = rings[s + 1][k];
                    Vector3 mid = (a + bb + c + d) * 0.25f;
                    Vector3 side = SideDir(mid, s < 2 ? oc : ScreenY, s < 2 ? OutHX : InHX, s < 2 ? ohy : InHY);
                    poly.Quad(a, bb, c, d, side * hints[s].Item1 + Vector3.forward * hints[s].Item2);
                }
            Vector3[] back = rings[rings.Length - 1];
            poly.Quad(back[0], back[1], back[2], back[3], Vector3.forward);
            b.Raw(poly, 0);

            // ---------- корпус за рамкой и сужающийся кинескоп
            b.Hex(Taper(0f, oc, OutHX, ohy, 0.15f, 0f, 0.195f, 0.17f, 0.135f, 0.06f), 0.004f, 0);
            b.Hex(Taper(0f, 0.195f, 0.155f, 0.12f, 0.06f, 0f, 0.2f, 0.085f, 0.07f, -0.18f), 0.006f, 0);
            b.Box(new Vector3(0f, 0.2f, -0.186f), new Vector3(0.15f, 0.12f, 0.012f), 0.003f, 0);

            // вентиляционные прорези на верхней скошенной грани кинескопа
            // верх кинескопа: y = 0.27 на z = −0.18 … 0.315 на z = 0.06
            float topSlope = Mathf.Atan2(0.315f - 0.27f, 0.24f) * Mathf.Rad2Deg;
            for (int i = 0; i < 5; i++)
            {
                float z = -0.13f + i * 0.025f;
                float y = 0.27f + (z + 0.18f) * (0.045f / 0.24f);
                b.Cap(new Vector3(0f, y - 0.0008f, z), 0.11f, 0.007f, 0.0025f, 0.001f, Quaternion.Euler(-topSlope, 0f, 0f), 0);
            }

            // ---------- подставка: поворотный диск и шея
            b.Cylinder(new Vector3(0f, 0f, 0.05f), 0.1f, 0.012f, 20, 0, Quaternion.identity, 0.003f);
            b.Hex(Taper(0f, 0.03f, 0.05f, 0.02f, 0.14f, 0f, 0.035f, 0.035f, 0.03f, 0.05f), 0.003f, 0);

            // ---------- кнопки и шильдик на нижней планке (питание справа для игрока, т.е. на −X)
            float by = OutY0 + (ScreenY - InHY - OutY0) * 0.5f;
            b.Box(new Vector3(-0.15f, by, FrontZ + 0.0015f), new Vector3(0.018f, 0.009f, 0.005f), 0.0012f, 0);
            for (int i = 0; i < 3; i++)
                b.Box(new Vector3(-0.115f + i * 0.014f, by, FrontZ + 0.001f), new Vector3(0.009f, 0.005f, 0.003f), 0.001f, 0);
            b.Box(new Vector3(0f, by, FrontZ + 0.0004f), new Vector3(0.05f, 0.008f, 0.0008f), 0f, 1);

            return b.Bake("SM_Monitor_CRT", 2);
        }

        static Vector3[] Rect(float cx, float cy, float hx, float hy, float z)
        {
            return new[]
            {
                new Vector3(cx - hx, cy - hy, z), new Vector3(cx + hx, cy - hy, z),
                new Vector3(cx + hx, cy + hy, z), new Vector3(cx - hx, cy + hy, z),
            };
        }

        static Vector3 SideDir(Vector3 p, float cy, float hx, float hy)
        {
            float nx = p.x / hx, ny = (p.y - cy) / hy;
            return Mathf.Abs(nx) > Mathf.Abs(ny) ? new Vector3(Mathf.Sign(nx), 0f, 0f) : new Vector3(0f, Mathf.Sign(ny), 0f);
        }

        /// <summary>Усечённая призма вдоль Z: передний и задний прямоугольники.</summary>
        static Vector3[] Taper(float fx, float fy, float fhx, float fhy, float fz,
            float bx, float byc, float bhx, float bhy, float bz)
        {
            var c = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                bool front = (i & 4) != 0;
                float hx = front ? fhx : bhx, hy = front ? fhy : bhy;
                float cx = front ? fx : bx, cy = front ? fy : byc;
                c[i] = new Vector3(cx + ((i & 1) == 0 ? -hx : hx), cy + ((i & 2) == 0 ? -hy : hy), front ? fz : bz);
            }
            return c;
        }

        // ------------------------------------------------------------ префабы

        static void BuildPrefabs(Mesh mesh)
        {
            string basePath = PBKit.PrefabsDir + "/" + BaseName + ".prefab";
            Material plastic = PBKit.Mat("MAT_Plastic");
            Material metal = PBKit.Mat("MAT_Metal");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(basePath) == null)
            {
                // Базовый префаб собираем из Screen и отсвета greybox-монитора.
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(PBKit.PrefabsDir + "/" + Variants[0] + ".prefab");
                var go = new GameObject(BaseName);
                go.AddComponent<BoxCollider>();
                PBKit.SetMeshChild(go.transform, "Mesh", mesh, plastic, metal);
                foreach (string child in new[] { "Screen", "Light_ScreenGlow" })
                {
                    Transform s = src.transform.Find(child);
                    var copy = Object.Instantiate(s.gameObject, go.transform, false);
                    copy.name = child;
                }
                PBKit.FitBox(go.GetComponent<BoxCollider>(), mesh, go.transform.Find("Mesh"), go.transform);
                PrefabUtility.SaveAsPrefabAsset(go, basePath);
                Object.DestroyImmediate(go);
            }
            else
            {
                PBKit.EditPrefab(BaseName, root =>
                {
                    GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", mesh, plastic, metal);
                    PBKit.FitBox(root.GetComponent<BoxCollider>(), mesh, m.transform, root.transform);
                });
            }

            var baseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
            var pending = new List<string>();
            foreach (string v in Variants)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PBKit.PrefabsDir + "/" + v + ".prefab");
                if (PrefabUtility.GetCorrespondingObjectFromSource(asset) != baseAsset) pending.Add(v);
            }
            if (pending.Count == 0) return;

            // Превращение greybox-префабов в варианты меняет их внутренние fileID,
            // и оверрайды экземпляров в Env_Room (имя, позиция) теряются.
            // Запоминаем их до конверсии и возвращаем после.
            var scene = EditorSceneManager.OpenScene(EnvRoom, OpenSceneMode.Single);
            var saved = new Dictionary<string, List<Placement>>();
            foreach (string v in pending) saved[v] = Collect(v);

            foreach (string v in pending)
            {
                string path = PBKit.PrefabsDir + "/" + v + ".prefab";
                string guid = AssetDatabase.AssetPathToGUID(path);
                var old = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Material screenMat = old.transform.Find("Screen").GetComponent<MeshRenderer>().sharedMaterial;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(baseAsset);
                inst.name = v;
                inst.transform.Find("Screen").GetComponent<MeshRenderer>().sharedMaterial = screenMat;
                PrefabUtility.SaveAsPrefabAsset(inst, path);
                Object.DestroyImmediate(inst);

                if (AssetDatabase.AssetPathToGUID(path) != guid)
                    Debug.LogError("[VOID] GUID " + v + " изменился!");
            }

            foreach (string v in pending) Restore(v, saved[v]);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[VOID] Monitor_CRT: варианты " + string.Join(", ", pending) + " готовы, Env_Room обновлён");
        }

        struct Placement
        {
            public string name;
            public Transform parent;
            public int sibling;
            public Vector3 pos, scale;
            public Quaternion rot;
        }

        static IEnumerable<GameObject> Instances(string prefab)
        {
            string path = PBKit.PrefabsDir + "/" + prefab + ".prefab";
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) &&
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == path)
                    yield return t.gameObject;
        }

        static List<Placement> Collect(string prefab)
        {
            var list = new List<Placement>();
            foreach (var g in Instances(prefab))
            {
                Transform t = g.transform;
                list.Add(new Placement
                {
                    name = g.name, parent = t.parent, sibling = t.GetSiblingIndex(),
                    pos = t.localPosition, rot = t.localRotation, scale = t.localScale,
                });
            }
            return list;
        }

        static void Restore(string prefab, List<Placement> saved)
        {
            var found = new List<GameObject>(Instances(prefab));
            if (found.Count != saved.Count)
            {
                Debug.LogError("[VOID] " + prefab + ": было " + saved.Count + " экз., стало " + found.Count);
                return;
            }
            for (int i = 0; i < saved.Count; i++)
            {
                Placement p = saved[i];
                Transform t = found[i].transform;
                found[i].name = p.name;
                if (t.parent != p.parent) t.SetParent(p.parent, false);
                t.SetSiblingIndex(p.sibling);
                t.localPosition = p.pos;
                t.localRotation = p.rot;
                t.localScale = p.scale;
            }
        }
    }
}
