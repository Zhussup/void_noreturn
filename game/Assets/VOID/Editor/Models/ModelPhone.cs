using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VOID.EnvModels
{
    public static class ModelPhone
    {
        // Корпус 170 × 220 мм: спереди (+Z, к игроку) 45 мм, сзади 65 мм.
        const float HalfW = 0.085f, HalfD = 0.11f, FrontH = 0.045f, BackH = 0.065f, Inset = 0.006f;
        // Трубка лежит поперёк задней части; пивот Handset как у greybox.
        static readonly Vector3 HandsetPos = new Vector3(0f, 0.085f, -0.05f);

        static float Slope => Mathf.Atan2(BackH - FrontH, 2f * (HalfD - Inset)) * Mathf.Rad2Deg;
        static float TopY(float z) => FrontH + (HalfD - Inset - z) / (2f * (HalfD - Inset)) * (BackH - FrontH);

        [MenuItem("VOID/Env/Models/Phone")]
        public static void Build()
        {
            Quaternion panel = Quaternion.Euler(Slope, 0f, 0f);

            // ---------- корпус
            var b = new PBKit.Builder();
            var c = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                bool top = (i & 2) != 0, front = (i & 4) != 0;
                float hx = top ? HalfW - Inset : HalfW;
                float hz = top ? HalfD - Inset : HalfD;
                c[i] = new Vector3((i & 1) == 0 ? -hx : hx, top ? (front ? FrontH : BackH) : 0f, front ? hz : -hz);
            }
            b.Hex(c, 0.006f, 0);

            // ложементы под концы трубки
            for (int s = -1; s <= 1; s += 2)
                b.Box(new Vector3(s * 0.0725f, 0.0735f, -0.05f), new Vector3(0.032f, 0.025f, 0.06f), 0.004f, 0);

            // клавиши 3 × 4 на наклонной панели
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 3; col++)
                {
                    float z = 0.012f + row * 0.02f;
                    b.Cap(new Vector3((col - 1) * 0.022f, TopY(z) - 0.0005f, z), 0.016f, 0.012f, 0.005f, 0.0015f, panel, 1);
                }
            // ряд служебных клавиш у переднего края
            for (int col = 0; col < 3; col++)
            {
                const float z = 0.09f;
                b.Cap(new Vector3((col - 1) * 0.022f, TopY(z) - 0.0005f, z), 0.016f, 0.007f, 0.004f, 0.0012f, panel, 0);
            }
            // решётка звонка справа (для игрока справа — это −X)
            for (int i = 0; i < 4; i++)
            {
                float z = 0.025f + i * 0.008f;
                b.Cap(new Vector3(-0.06f, TopY(z) - 0.0003f, z), 0.026f, 0.003f, 0.0012f, 0.0004f, panel, 0);
            }
            Mesh body = b.Bake("SM_Phone", 2);

            // ---------- трубка, в координатах Handset
            var h = new PBKit.Builder();
            h.Box(new Vector3(0f, 0.03f, 0f), new Vector3(0.12f, 0.018f, 0.04f), 0.005f, 0);
            for (int s = -1; s <= 1; s += 2)
            {
                var e = new Vector3[8];
                for (int i = 0; i < 8; i++)
                {
                    bool top = (i & 2) != 0;
                    float cx = s * (top ? 0.078f : 0.083f);
                    float hx = top ? 0.02f : 0.024f;
                    float hz = top ? 0.022f : 0.028f;
                    e[i] = new Vector3(cx + ((i & 1) == 0 ? -hx : hx), top ? 0.039f : 0f, (i & 4) == 0 ? -hz : hz);
                }
                h.Hex(e, 0.005f, 0);
            }
            Mesh handset = h.Bake("SM_Phone_Handset", 1);

            // ---------- витой шнур: от левого борта корпуса к микрофонному концу трубки
            Mesh cord = Cord();

            // ---------- индикатор звонка на панели (его ищет NightDirector, билдер свой не создаёт)
            var l = new PBKit.Builder();
            l.Cap(Vector3.zero, 0.012f, 0.008f, 0.003f, 0.001f, Quaternion.identity, 0);
            Mesh led = l.Bake("SM_Phone_LED", 1);

            PBKit.Log("Phone", body, handset, cord, led);

            PBKit.EditPrefab("PRB_Phone", root =>
            {
                Material plastic = PBKit.Mat("MAT_Plastic");
                GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", body, plastic, PBKit.Mat("MAT_Ceramic"));
                PBKit.FitBox(root.GetComponent<BoxCollider>(), body, m.transform, root.transform);

                Transform hs = root.transform.Find("Handset");
                hs.localPosition = HandsetPos;
                hs.localRotation = Quaternion.identity;
                GameObject hm = PBKit.SetMeshChild(hs, "Mesh", handset, plastic);
                PBKit.FitBox(hs.GetComponent<BoxCollider>(), handset, hm.transform, hs);

                PBKit.SetMeshChild(root.transform, "Cord", cord, plastic);

                const float ledZ = 0.08f;
                GameObject lo = PBKit.SetMeshChild(root.transform, "Phone_LED", led,
                    AssetDatabase.LoadAssetAtPath<Material>("Assets/VOID/Materials/phone_led.mat"));
                lo.transform.localPosition = new Vector3(0.058f, TopY(ledZ) - 0.0003f, ledZ);
                lo.transform.localRotation = Quaternion.Euler(Slope, 0f, 0f);
            });
        }

        // 12 витков по 4 сегмента, сечение — треугольник: 48 × 3 квада = 288 треугольников.
        static Mesh Cord()
        {
            const int turns = 12, segPerTurn = 4, sides = 3;
            const float coilR = 0.0055f, wireR = 0.0016f;

            Vector3 p0 = new Vector3(-HalfW + 0.002f, 0.018f, -0.075f);
            Vector3 p1 = new Vector3(-0.15f, 0.008f, -0.04f);
            Vector3 p2 = new Vector3(-0.15f, 0.012f, -0.075f);
            Vector3 p3 = HandsetPos + new Vector3(-0.104f, 0.006f, 0f);

            int n = turns * segPerTurn;
            var centers = new Vector3[n + 1];
            Vector3 normal = Vector3.up;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                Vector3 pt = Bezier(p0, p1, p2, p3, t);
                Vector3 tan = BezierTangent(p0, p1, p2, p3, t).normalized;
                // перенос рамки вдоль кривой без скручивания
                normal = Vector3.ProjectOnPlane(normal, tan).normalized;
                if (normal.sqrMagnitude < 1e-6f) normal = Vector3.ProjectOnPlane(Vector3.forward, tan).normalized;
                Vector3 bin = Vector3.Cross(tan, normal);
                float th = t * turns * Mathf.PI * 2f;
                centers[i] = pt + coilR * (Mathf.Cos(th) * normal + Mathf.Sin(th) * bin);
            }

            var rings = new List<Vector3[]>();
            for (int i = 0; i <= n; i++)
            {
                Vector3 a = centers[Mathf.Max(0, i - 1)], c = centers[Mathf.Min(n, i + 1)];
                Vector3 tan = (c - a).normalized;
                Vector3 u = Vector3.ProjectOnPlane(Vector3.up, tan).normalized;
                if (u.sqrMagnitude < 1e-6f) u = Vector3.ProjectOnPlane(Vector3.right, tan).normalized;
                Vector3 v = Vector3.Cross(tan, u);
                var ring = new Vector3[sides];
                for (int k = 0; k < sides; k++)
                {
                    float a2 = k * Mathf.PI * 2f / sides;
                    ring[k] = centers[i] + wireR * (Mathf.Cos(a2) * u + Mathf.Sin(a2) * v);
                }
                rings.Add(ring);
            }

            var poly = new PBKit.Poly();
            for (int i = 0; i < n; i++)
                for (int k = 0; k < sides; k++)
                {
                    int k1 = (k + 1) % sides;
                    Vector3 a = rings[i][k], b = rings[i][k1], c = rings[i + 1][k1], d = rings[i + 1][k];
                    Vector3 mid = (centers[i] + centers[i + 1]) * 0.5f;
                    poly.Quad(a, b, c, d, (a + b + c + d) * 0.25f - mid, 1);
                }

            var builder = new PBKit.Builder();
            builder.Raw(poly, 0);
            return builder.Bake("SM_Phone_Cord", 1);
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        static Vector3 BezierTangent(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return 3f * u * u * (b - a) + 6f * u * t * (c - b) + 3f * t * t * (d - c);
        }
    }
}
