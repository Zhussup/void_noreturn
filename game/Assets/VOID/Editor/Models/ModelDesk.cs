using UnityEditor;
using UnityEngine;

namespace VOID.EnvModels
{
    public static class ModelDesk
    {
        // Стол 1.8 × 0.75, столешница y = 0.74. Перед — к игроку (+Z).
        public const float TopY = 0.74f;
        // Отверстие для проводов в задней части столешницы, сюда сходятся кабели.
        public static readonly Vector3 Grommet = new Vector3(0f, TopY, -0.31f);
        const float GrommetR = 0.03f;

        [MenuItem("VOID/Env/Models/Desk")]
        public static void Build()
        {
            var b = new PBKit.Builder();

            // столешница ЛДСП с кромкой
            b.Box(new Vector3(0f, 0.72f, 0f), new Vector3(1.8f, 0.04f, 0.75f), 0.004f, 0);

            // ножки как у greybox; внизу — металлические регулируемые опоры
            float[] lx = { -0.85f, 0.85f };
            float[] lz = { -0.28f, 0.34f };
            foreach (float x in lx)
                foreach (float z in lz)
                {
                    b.Box(new Vector3(x, 0.358f, z), new Vector3(0.04f, 0.684f, 0.04f), 0.003f, 0);
                    b.Cylinder(new Vector3(x, 0f, z), 0.014f, 0.016f, 8, 1, Quaternion.identity, 0f, false, false);
                }

            // царги по бокам и задняя панель (как в greybox)
            foreach (float x in lx)
                b.Box(new Vector3(x, 0.66f, 0.03f), new Vector3(0.02f, 0.08f, 0.58f), 0f, 0);
            b.Box(new Vector3(0f, 0.5f, -0.29f), new Vector3(1.66f, 0.4f, 0.02f), 0.003f, 0);

            // пластиковая заглушка кабельного отверстия
            b.Cylinder(Grommet + new Vector3(0f, -0.0005f, 0f), GrommetR, 0.0025f, 12, 2, Quaternion.identity, 0f, false, false);
            b.Cylinder(Grommet + new Vector3(0f, 0.0015f, 0f), GrommetR * 0.6f, 0.0005f, 10, 3, Quaternion.identity, 0f, false, false);

            Mesh mesh = b.Bake("SM_Desk", 4);
            PBKit.Log("Desk", mesh);

            PBKit.EditPrefab("PRB_Desk", root =>
            {
                GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", mesh,
                    PBKit.Mat("MAT_Desk_Wood"), PBKit.Mat("MAT_Metal"), PBKit.Mat("MAT_TrashBin"), PBKit.Mat("MAT_Plastic"));
                PBKit.FitBox(root.GetComponent<BoxCollider>(), mesh, m.transform, root.transform);
            });
        }
    }
}
