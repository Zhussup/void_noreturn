using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VOID.EnvModels
{
    public static class ModelLaptop
    {
        // Открытый ноутбук: корпус 0.27 × 0.19 × 0.012 с клавиатурой и тачпадом,
        // крышка — отдельный объект Lid с пивотом на петле (задняя кромка, верх
        // корпуса), открыта на OpenAngle. В крышке — квад экрана, устроенный как
        // Screen у мониторов (масштаб = размер экрана, forward — внутрь крышки).
        //
        // Назван Display, а не Screen, намеренно: NightDirector ищет
        // Laptop/Lid/Screen и пишет на нём TextMesh, а шрифт TextMesh рисуется
        // поверх всего (GUI/Text Shader без проверки глубины). Со станций
        // ноутбук частично закрыт монитором чата — надпись проступила бы сквозь
        // него. Когда у ScreenSurface будет шрифт с ZTest, переименовать в Screen.
        const float BaseTop = 0.012f;
        const float OpenAngle = 110f;
        const string ScreenName = "Display";
        static readonly Vector3 Hinge = new Vector3(0f, BaseTop, -0.093f);
        static readonly Vector2 ScreenSize = new Vector2(0.24f, 0.15f);

        [MenuItem("VOID/Env/Models/Laptop")]
        public static void Build()
        {
            // ---------- корпус: резиновые ножки, нижняя половина, петли сзади
            var b = new PBKit.Builder();
            b.Box(new Vector3(0f, 0.0065f, 0f), new Vector3(0.27f, 0.011f, 0.19f), 0.0025f, 0);
            foreach (float x in new[] { -0.11f, 0.11f })
                foreach (float z in new[] { -0.075f, 0.075f })
                    b.Cylinder(new Vector3(x, 0f, z), 0.006f, 0.0012f, 8, 0, Quaternion.identity, 0f, false, false);
            Quaternion alongX = Quaternion.Euler(0f, 0f, -90f);
            b.Cylinder(Hinge + new Vector3(-0.115f, 0f, 0f), 0.0045f, 0.23f, 8, 0, alongX, 0f, true);

            // клавиатура 14 × 4 и нижний ряд с пробелом, перед ней — тачпад
            const float pitch = 0.0165f, key = 0.014f;
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 14; c++)
                    b.Cap(new Vector3((c - 6.5f) * pitch, BaseTop, -0.066f + r * pitch), key, key, 0.0012f, 0.001f, Quaternion.identity, 1);
            for (int c = 0; c < 14; c++)
            {
                if (c >= 5 && c <= 9) continue;
                b.Cap(new Vector3((c - 6.5f) * pitch, BaseTop, -0.066f + 4 * pitch), key, key, 0.0012f, 0.001f, Quaternion.identity, 1);
            }
            b.Cap(new Vector3(0.5f * pitch, BaseTop, -0.066f + 4 * pitch), 5 * pitch - (pitch - key), key, 0.0012f, 0.001f, Quaternion.identity, 1);
            b.Box(new Vector3(0f, BaseTop + 0.0002f, 0.058f), new Vector3(0.075f, 0.0004f, 0.046f), 0f, 1);
            Mesh baseMesh = b.Bake("SM_Laptop", 2);

            // ---------- крышка в координатах петли (закрытое положение: экран смотрит вниз, −Y)
            var l = new PBKit.Builder();
            l.Box(new Vector3(0f, 0.004f, 0.095f), new Vector3(0.27f, 0.008f, 0.19f), 0.0025f, 0);
            l.Cylinder(new Vector3(0f, 0.008f, 0.1f), 0.012f, 0.0004f, 16, 1, Quaternion.identity, 0f, false, false);
            l.Box(new Vector3(0f, -0.0002f, 0.096f), new Vector3(0.262f, 0.0004f, 0.182f), 0f, 2); // рамка экрана
            Mesh lidMesh = l.Bake("SM_Laptop_Lid", 3);

            PBKit.Log("Laptop", baseMesh, lidMesh);

            PBKit.EditPrefab("PRB_Laptop", root =>
            {
                GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", baseMesh,
                    PBKit.Mat("MAT_Plastic"), PBKit.Mat("MAT_Plastic_Keys"));
                GameObject lid = PBKit.SetMeshChild(root.transform, "Lid", lidMesh,
                    PBKit.Mat("MAT_Metal"), PBKit.Mat("MAT_Ceramic"), PBKit.Mat("MAT_Plastic"));
                lid.transform.localPosition = Hinge;
                lid.transform.localRotation = Quaternion.Euler(-OpenAngle, 0f, 0f);

                // экран: квад на внутренней стороне крышки, лицом к пользователю
                var screen = new GameObject(ScreenName);
                screen.transform.SetParent(lid.transform, false);
                screen.transform.localPosition = new Vector3(0f, -0.0009f, 0.1f);
                screen.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
                screen.transform.localScale = new Vector3(ScreenSize.x, ScreenSize.y, 1f);
                screen.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var sr = screen.AddComponent<MeshRenderer>();
                sr.sharedMaterial = PBKit.Mat("MAT_Screen_Laptop");
                sr.shadowCastingMode = ShadowCastingMode.Off;

                // два BoxCollider: корпус и поднятая крышка
                var cols = root.GetComponents<BoxCollider>();
                BoxCollider lidCol = cols.Length > 1 ? cols[1] : root.AddComponent<BoxCollider>();
                PBKit.FitBox(cols[0], baseMesh, m.transform, root.transform);
                PBKit.FitBox(lidCol, lidMesh, lid.transform, root.transform);
            });
        }
    }
}
