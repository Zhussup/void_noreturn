using UnityEditor;
using UnityEngine;

namespace VOID.EnvModels
{
    public static class ModelWallClock
    {
        // Корень часов висит на стене, циферблат смотрит в +Z.
        const float R = 0.15f;
        const float FaceR = 0.136f;
        const float FaceZ = 0.0375f;   // позиция объекта Face — по ней код ставит цифровое время
        const float HandsZ = 0.0425f;  // позиция Hand_Hour / Hand_Minute

        [MenuItem("VOID/Env/Models/WallClock")]
        public static void Build()
        {
            Quaternion toFront = Quaternion.FromToRotation(Vector3.up, Vector3.forward);

            // корпус — тело вращения: борт, скос, обод перед циферблатом, внутренний скос.
            // Задней крышки нет — часы висят на стене.
            const float rim = 0.045f;
            var profile = new[]
            {
                new Vector2(R, 0f),
                new Vector2(R, rim - 0.008f),
                new Vector2(R - 0.006f, rim),
                new Vector2(FaceR + 0.002f, rim),
                new Vector2(FaceR, rim - 0.002f),
                new Vector2(FaceR, FaceZ),
            };
            var poly = new PBKit.Poly();
            poly.Lathe(Vector3.zero, profile, 24);
            var b = new PBKit.Builder();
            b.Raw(poly, 0);
            Mesh body = b.Bake("SM_WallClock", 1);

            // циферблат с 12 делениями; меш в координатах объекта Face
            var f = new PBKit.Builder();
            f.Cylinder(new Vector3(0f, 0f, -0.0025f), FaceR, 0.005f, 24, 0, toFront);
            for (int i = 0; i < 12; i++)
            {
                // по часовой стрелке для зрителя: «3» на −X
                float a = i * 30f * Mathf.Deg2Rad;
                var dir = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f);
                bool bold = i % 3 == 0;
                float len = bold ? 0.026f : 0.016f;
                float r = 0.125f - len * 0.5f;
                f.Cap(dir * r + new Vector3(0f, 0f, 0.0025f), bold ? 0.008f : 0.004f, len, 0.0012f, 0.0005f,
                    Quaternion.LookRotation(dir, Vector3.forward), 1);
            }
            Mesh face = f.Bake("SM_WallClock_Face", 2);

            Mesh hour = Hand("SM_WallClock_Hand_Hour", -0.012f, 0.065f, 0.0035f, 0.0005f, false);
            Mesh minute = Hand("SM_WallClock_Hand_Minute", -0.016f, 0.095f, 0.0025f, 0.003f, true);

            PBKit.Log("WallClock", body, face, hour, minute);

            PBKit.EditPrefab("PRB_WallClock", root =>
            {
                Material plastic = PBKit.Mat("MAT_Plastic");
                Material paper = PBKit.Mat("MAT_Paper");
                Material metal = PBKit.Mat("MAT_Metal");

                GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", body, plastic);

                // Face — прямой ребёнок WallClock: NightDirector ищет transform.Find("Face").
                GameObject fo = PBKit.SetMeshChild(root.transform, "Face", face, paper, plastic);
                fo.transform.localPosition = new Vector3(0f, 0f, FaceZ);

                SetHand(root.transform, "Hand_Hour", hour, plastic);
                SetHand(root.transform, "Hand_Minute", minute, plastic, metal);

                PBKit.FitBox(root.GetComponent<BoxCollider>(), body, m.transform, root.transform);
            });
        }

        static void SetHand(Transform root, string name, Mesh mesh, params Material[] mats)
        {
            // Пивот стрелки — ось в центре циферблата; поворот из greybox сохраняем.
            Transform hand = root.Find(name);
            Quaternion keep = hand != null ? hand.localRotation : Quaternion.identity;
            if (hand == null)
            {
                hand = new GameObject(name).transform;
                hand.SetParent(root, false);
            }
            hand.localPosition = new Vector3(0f, 0f, HandsZ);
            hand.localRotation = keep;
            PBKit.SetMeshChild(hand, "Mesh", mesh, mats);
        }

        /// <summary>Стрелка в плоскости XY, остриё по +Y, толщина 2 мм от z0.</summary>
        static Mesh Hand(string name, float tail, float tip, float halfW, float z0, bool cap)
        {
            var b = new PBKit.Builder();
            float mid = tail + (tip - tail) * 0.3f;
            const float t = 0.002f;
            b.Hex(Corners(tail, halfW * 0.6f, mid, halfW, z0, t), 0f, 0);
            b.Hex(Corners(mid, halfW, tip, halfW * 0.3f, z0, t), 0f, 0);
            int subs = 1;
            if (cap)
            {
                b.Cylinder(Vector3.zero, 0.006f, 0.006f, 10, 1,
                    Quaternion.FromToRotation(Vector3.up, Vector3.forward));
                subs = 2;
            }
            return b.Bake(name, subs);
        }

        static Vector3[] Corners(float y0, float w0, float y1, float w1, float z0, float t)
        {
            var c = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                bool far = (i & 2) != 0;
                float w = far ? w1 : w0;
                c[i] = new Vector3((i & 1) == 0 ? -w : w, far ? y1 : y0, (i & 4) == 0 ? z0 : z0 + t);
            }
            return c;
        }
    }
}
