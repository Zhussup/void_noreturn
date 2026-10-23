using UnityEditor;
using UnityEngine;

namespace VOID.EnvModels
{
    public static class ModelChair
    {
        // Офисное кресло на пятилучевой крестовине. Сиденье смотрит в +Z (к столу при повороте 180°).
        const int Fabric = 0, Plastic = 1, Metal = 2;

        [MenuItem("VOID/Env/Models/Chair")]
        public static void Build()
        {
            var b = new PBKit.Builder();
            Quaternion side = Quaternion.FromToRotation(Vector3.up, Vector3.right);

            // ---------- крестовина: 5 лучей, ролики на радиусе 0.30 (как у greybox)
            b.Cylinder(new Vector3(0f, 0.07f, 0f), 0.05f, 0.04f, 10, Plastic, Quaternion.identity);
            for (int i = 0; i < 5; i++)
            {
                Quaternion ry = Quaternion.Euler(0f, 36f + i * 72f, 0f);
                Vector3 dir = ry * Vector3.forward;
                var c = new Vector3[8];
                for (int k = 0; k < 8; k++)
                {
                    bool outer = (k & 4) != 0, top = (k & 2) != 0;
                    float r = outer ? 0.285f : 0.04f;
                    float hw = outer ? 0.016f : 0.024f;
                    float y = outer ? (top ? 0.075f : 0.05f) : (top ? 0.11f : 0.07f);
                    Vector3 lateral = ry * Vector3.right * ((k & 1) == 0 ? -hw : hw);
                    c[k] = dir * r + lateral + Vector3.up * y;
                }
                b.Hex(c, 0.004f, Plastic);

                // ролик: вилка и колесо поперёк луча
                Vector3 cp = dir * 0.3f;
                b.Box(cp + new Vector3(0f, 0.045f, 0f), new Vector3(0.03f, 0.018f, 0.03f), 0f, Plastic, ry);
                Quaternion wheel = ry * side;
                b.Cylinder(cp + new Vector3(0f, 0.025f, 0f) + ry * Vector3.left * 0.011f, 0.025f, 0.022f, 10, Plastic, wheel);
            }

            // ---------- газлифт: хромированный шток и пластиковый кожух
            b.Cylinder(new Vector3(0f, 0.1f, 0f), 0.033f, 0.12f, 12, Plastic, Quaternion.identity, 0f, true, false);
            b.Cylinder(new Vector3(0f, 0.22f, 0f), 0.022f, 0.16f, 12, Metal, Quaternion.identity, 0f, true, false);

            // ---------- механизм качания и рычаг
            b.Box(new Vector3(0f, 0.385f, 0.01f), new Vector3(0.2f, 0.03f, 0.22f), 0.004f, Plastic);
            b.Beam(new Vector3(0.1f, 0.38f, 0.06f), new Vector3(0.2f, 0.37f, 0.1f), 0.012f, 0.008f, 0f, Plastic, Vector3.up);

            // ---------- сиденье: пластиковая чаша и продавленная подушка
            b.Box(new Vector3(0f, 0.407f, 0f), new Vector3(0.45f, 0.016f, 0.43f), 0.004f, Plastic);
            b.Hex(Cushion(0.48f, 0.46f, 0.415f, 0.07f, 0.012f), 0.012f, Fabric);

            // ---------- спинка: кронштейн, пластиковая скорлупа, подушка (наклон 8° как у greybox)
            b.Beam(new Vector3(0f, 0.385f, -0.08f), new Vector3(0f, 0.395f, -0.27f), 0.06f, 0.02f, 0.003f, Plastic, Vector3.up);
            b.Beam(new Vector3(0f, 0.385f, -0.27f), new Vector3(0f, 0.6f, -0.285f), 0.06f, 0.022f, 0.003f, Plastic, Vector3.forward);
            Quaternion tilt = Quaternion.Euler(-8f, 0f, 0f);
            b.Box(new Vector3(0f, 0.78f, -0.29f), new Vector3(0.42f, 0.42f, 0.018f), 0.005f, Plastic, tilt);
            b.Box(new Vector3(0f, 0.78f, -0.255f), new Vector3(0.44f, 0.44f, 0.06f), 0.014f, Fabric, tilt);

            // ---------- подлокотники
            for (int s = -1; s <= 1; s += 2)
            {
                b.Box(new Vector3(s * 0.2f, 0.395f, 0f), new Vector3(0.1f, 0.012f, 0.04f), 0.002f, Plastic);
                b.Beam(new Vector3(s * 0.25f, 0.39f, 0f), new Vector3(s * 0.255f, 0.62f, 0.01f), 0.03f, 0.045f, 0.004f, Plastic, Vector3.forward);
                b.Box(new Vector3(s * 0.255f, 0.635f, 0.02f), new Vector3(0.065f, 0.025f, 0.24f), 0.008f, Plastic);
            }

            Mesh mesh = b.Bake("SM_Chair", 3);
            PBKit.Log("Chair", mesh);

            PBKit.EditPrefab("PRB_Chair", root =>
            {
                GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", mesh,
                    PBKit.Mat("MAT_Fabric"), PBKit.Mat("MAT_Plastic"), PBKit.Mat("MAT_Metal"));
                PBKit.FitBox(root.GetComponent<BoxCollider>(), mesh, m.transform, root.transform);
            });
        }

        /// <summary>Подушка сиденья: верх чуть провален к середине спереди (просиженное место).</summary>
        static Vector3[] Cushion(float w, float d, float y0, float h, float sag)
        {
            var c = new Vector3[8];
            for (int k = 0; k < 8; k++)
            {
                bool top = (k & 2) != 0, front = (k & 4) != 0;
                float y = y0 + (top ? h - (front ? sag : 0f) : 0f);
                c[k] = new Vector3((k & 1) == 0 ? -w * 0.5f : w * 0.5f, y, front ? d * 0.5f : -d * 0.5f);
            }
            return c;
        }
    }
}
