using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VOID.EnvModels
{
    public static class ModelTrashBin
    {
        // Урна-конус Ø0.26 → Ø0.30, высота 0.4, почти полная: мешок завёрнут
        // за край, сверху — мятые бумажки (как у greybox: Inside и PaperBall).
        const int Bin = 0, Bag = 1, Paper = 2;
        const float H = 0.4f, R0 = 0.13f, R1 = 0.15f, FillY = 0.378f;
        static readonly Quaternion Up = Quaternion.Euler(-90f, 0f, 0f);

        [MenuItem("VOID/Env/Models/TrashBin")]
        public static void Build()
        {
            var b = new PBKit.Builder();

            // ---------- корпус: дно, коническая стенка, внутренняя стенка до уровня мусора
            var body = new PBKit.Poly();
            body.Lathe(Vector3.zero, new List<Vector2> { new Vector2(0f, 0f), new Vector2(R0 - 0.004f, 0f) }, 20, Up, 0);
            body.Lathe(Vector3.zero, new List<Vector2> { new Vector2(R0 - 0.004f, 0f), new Vector2(R0, 0.005f), new Vector2(R1, H) }, 20, Up, 1);
            body.Lathe(Vector3.zero, new List<Vector2> { new Vector2(R1 - 0.004f, H), new Vector2(R1 - 0.0055f, FillY - 0.01f) }, 20, Up, 2);
            b.Raw(body, Bin);

            // завальцованный край
            var rim = new PBKit.Poly();
            var ring = new List<Vector3>();
            for (int i = 0; i <= 20; i++)
            {
                float a = i * Mathf.PI * 2f / 20f;
                ring.Add(new Vector3(Mathf.Cos(a) * (R1 - 0.001f), H, Mathf.Sin(a) * (R1 - 0.001f)));
            }
            rim.Tube(ring, 0.004f, 6, 3);
            b.Raw(rim, Bin);

            // ---------- мешок: верх завёрнут через край, внутри — неровная поверхность мусора
            var bag = new PBKit.Poly();
            bag.Lathe(Vector3.zero, new List<Vector2>
            {
                new Vector2(R1 + 0.008f, H - 0.045f), new Vector2(R1 + 0.007f, H + 0.004f),
                new Vector2(R1 - 0.003f, H + 0.007f), new Vector2(R1 - 0.009f, FillY),
            }, 20, Up, 1);
            Fill(bag, 4, 16, R1 - 0.008f);
            b.Raw(bag, Bag);

            // ---------- мятые бумажки сверху
            var paper = new PBKit.Poly();
            Crumple(paper, new Vector3(0.03f, FillY + 0.03f, -0.02f), 0.036f, 11);
            Crumple(paper, new Vector3(-0.06f, FillY + 0.018f, 0.045f), 0.026f, 23);
            Crumple(paper, new Vector3(0.075f, FillY + 0.014f, 0.06f), 0.022f, 37);
            b.Raw(paper, Paper);

            Mesh mesh = b.Bake("SM_TrashBin", 3);
            PBKit.Log("TrashBin", mesh);

            PBKit.EditPrefab("PRB_TrashBin", root =>
            {
                GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", mesh,
                    PBKit.Mat("MAT_TrashBin"), PBKit.Mat("MAT_Plastic"), PBKit.Mat("MAT_Paper"));
                PBKit.FitBox(root.GetComponent<BoxCollider>(), mesh, m.transform, root.transform);
            });
        }

        /// <summary>Круглая «крышка» мусора: кольца с лёгкими буграми, центр — веер треугольников.</summary>
        static void Fill(PBKit.Poly p, int rings, int sectors, float radius)
        {
            var rnd = new System.Random(5);
            var h = new float[rings + 1, sectors];
            for (int j = 1; j <= rings; j++)
                for (int k = 0; k < sectors; k++)
                    h[j, k] = j == rings ? 0f : (float)rnd.NextDouble() * 0.012f;
            float hc = 0.01f;

            System.Func<int, int, Vector3> pt = (j, k) =>
            {
                float a = (k % sectors) * Mathf.PI * 2f / sectors, r = radius * j / rings;
                return new Vector3(Mathf.Cos(a) * r, FillY + h[j, k % sectors], Mathf.Sin(a) * r);
            };
            var c = new Vector3(0f, FillY + hc, 0f);
            for (int k = 0; k < sectors; k++)
            {
                p.Tri(c, pt(1, k), pt(1, k + 1), Vector3.up, 1);
                for (int j = 1; j < rings; j++)
                    p.Quad(pt(j, k), pt(j + 1, k), pt(j + 1, k + 1), pt(j, k + 1), Vector3.up, 1);
            }
        }

        /// <summary>Мятый бумажный ком: сфера 5 × 8 со случайным радиусом вершин.</summary>
        static void Crumple(PBKit.Poly p, Vector3 c, float r, int seed)
        {
            const int lat = 5, lon = 8;
            var rnd = new System.Random(seed);
            var v = new Vector3[lat + 1, lon];
            for (int i = 0; i <= lat; i++)
            {
                float th = Mathf.PI * i / lat;
                for (int k = 0; k < lon; k++)
                {
                    float ph = Mathf.PI * 2f * k / lon;
                    float rr = r * (0.78f + 0.36f * (float)rnd.NextDouble());
                    var dir = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    v[i, k] = c + dir * rr;
                }
            }
            for (int k = 0; k < lon; k++) v[lat, k] = v[lat, 0];
            for (int k = 0; k < lon; k++) v[0, k] = v[0, 0];

            for (int i = 0; i < lat; i++)
                for (int k = 0; k < lon; k++)
                {
                    int k1 = (k + 1) % lon;
                    Vector3 a = v[i, k], bb = v[i, k1], cc = v[i + 1, k1], d = v[i + 1, k];
                    Vector3 hint = (a + bb + cc + d) * 0.25f - c;
                    if (i == 0) p.Tri(a, cc, d, hint);
                    else if (i == lat - 1) p.Tri(a, bb, cc, hint);
                    else p.Quad(a, bb, cc, d, hint);
                }
        }
    }
}
