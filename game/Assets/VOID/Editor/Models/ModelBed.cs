using UnityEditor;
using UnityEngine;

namespace VOID.EnvModels
{
    public static class ModelBed
    {
        // Кровать 0.9 × 2.0, изголовье к +Z (к южной стене). Матрас сверху на y = 0.40.
        const int Wood = 0, Sheet = 1, Blanket = 2;
        const float MattressTop = 0.40f;

        [MenuItem("VOID/Env/Models/Bed")]
        public static void Build()
        {
            var b = new PBKit.Builder();

            // ---------- каркас: ножки, царги, изножье и невысокое изголовье (в пределах greybox)
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    b.Box(new Vector3(sx * 0.42f, 0.125f, sz * 0.97f), new Vector3(0.06f, 0.25f, 0.06f), 0.004f, Wood);
            for (int sx = -1; sx <= 1; sx += 2)
                b.Box(new Vector3(sx * 0.43f, 0.165f, 0f), new Vector3(0.035f, 0.17f, 1.88f), 0.004f, Wood);
            b.Box(new Vector3(0f, 0.165f, -0.975f), new Vector3(0.79f, 0.17f, 0.035f), 0.004f, Wood);
            b.Box(new Vector3(0f, 0.315f, 0.98f), new Vector3(0.9f, 0.47f, 0.04f), 0.006f, Wood);

            // ---------- матрас со скруглёнными краями
            b.Box(new Vector3(0f, 0.325f, -0.01f), new Vector3(0.86f, 0.15f, 1.92f), 0.025f, Sheet);

            // ---------- подушка: не по центру, повёрнута, с вмятиной от головы
            var pillow = new PBKit.Poly();
            Vector3 pc = new Vector3(0.12f, MattressTop + 0.002f, 0.72f);
            Quaternion pr = Quaternion.Euler(0f, -12f, 0f);
            pillow.Grid(8, 6, (u, v) =>
            {
                // sin(π) даёт −1e−8, а Pow от отрицательного — NaN: обрезаем до нуля
                float su = Mathf.Max(0f, Mathf.Sin(Mathf.PI * u)), sv = Mathf.Max(0f, Mathf.Sin(Mathf.PI * v));
                float h = 0.11f * Mathf.Sqrt(su) * Mathf.Sqrt(sv);
                h -= 0.03f * Gauss(u - 0.42f, v - 0.55f, 0.18f, 0.22f) * su;
                return pc + pr * new Vector3((u - 0.5f) * 0.58f, Mathf.Max(0f, h), (v - 0.5f) * 0.34f);
            }, Vector3.up);
            b.Raw(pillow, Sheet);

            // ---------- смятое одеяло: верх + свес с бока комнаты (−X)
            const int nx = 10, nz = 14;
            const float x0 = -0.45f, x1 = 0.44f, z0 = -0.97f, z1 = 0.42f;
            System.Func<float, float, Vector3> top = (u, v) =>
            {
                float x = Mathf.Lerp(x0, x1, u), z = Mathf.Lerp(z0, z1, v);
                float y = MattressTop + 0.03f + Wrinkle(x, z);
                // у стены (+X) одеяло заправлено под матрас
                y = Mathf.Lerp(y, MattressTop + 0.004f, Mathf.SmoothStep(0f, 1f, (u - 0.85f) / 0.15f));
                // отогнутый край у изголовья
                y += 0.05f * Mathf.Exp(-Mathf.Pow((z - 0.33f) / 0.06f, 2f));
                return new Vector3(x, y, z);
            };
            var blanket = new PBKit.Poly();
            blanket.Grid(nx, nz, top, Vector3.up);
            // свес: от края верха вниз до y ≈ 0.22, с волной по подолу
            blanket.Grid(3, nz, (u, v) =>
            {
                Vector3 edge = top(0f, v);
                float z = edge.z;
                float hem = 0.22f + 0.025f * Mathf.Sin(z * 11f);
                float y = Mathf.Lerp(edge.y, hem, u);
                float x = x0 - 0.018f * Mathf.Sin(Mathf.PI * 0.5f * u) - 0.006f * Mathf.Sin(z * 7f) * u;
                return new Vector3(x, y, z);
            }, Vector3.left);
            b.Raw(blanket, Blanket);

            Mesh mesh = b.Bake("SM_Bed", 3);
            PBKit.Log("Bed", mesh);

            PBKit.EditPrefab("PRB_Bed", root =>
            {
                GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", mesh,
                    PBKit.Mat("MAT_Desk_Wood"), PBKit.Mat("MAT_Paper"), PBKit.Mat("MAT_Fabric"));
                PBKit.FitBox(root.GetComponent<BoxCollider>(), mesh, m.transform, root.transform);
            });
        }

        static float Gauss(float dx, float dz, float sx, float sz)
        {
            return Mathf.Exp(-(dx * dx) / (2f * sx * sx) - (dz * dz) / (2f * sz * sz));
        }

        /// <summary>Складки и комки: бугры на местах greybox-«комков» плюс мелкая рябь.</summary>
        static float Wrinkle(float x, float z)
        {
            float w = 0.012f * Mathf.Sin(9f * x + 3f * z) + 0.008f * Mathf.Sin(13f * z - 5f * x + 1.3f);
            w += 0.07f * Gauss(x - 0.12f, z + 0.25f, 0.16f, 0.2f);
            w += 0.05f * Gauss(x + 0.18f, z + 0.62f, 0.13f, 0.13f);
            w += 0.03f * Gauss(x + 0.25f, z - 0.05f, 0.12f, 0.25f);
            return Mathf.Max(-0.02f, w);
        }
    }
}
