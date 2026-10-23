using UnityEditor;
using UnityEngine;

namespace VOID.EnvModels
{
    public static class ModelDoor
    {
        // Проём 0.77 × 2.01, коробка 0.1 в глубину (z −0.1…0). Сторона комнаты — +Z:
        // там наличники и ручка, как у greybox.
        const int Paint = 0, Wood = 1, Metal = 2;

        [MenuItem("VOID/Env/Models/Door")]
        public static void Build()
        {
            var b = new PBKit.Builder();

            // ---------- дверная коробка и наличники
            b.Box(new Vector3(-0.405f, 1.025f, -0.05f), new Vector3(0.04f, 2.05f, 0.1f), 0.003f, Paint);
            b.Box(new Vector3(0.405f, 1.025f, -0.05f), new Vector3(0.04f, 2.05f, 0.1f), 0.003f, Paint);
            b.Box(new Vector3(0f, 2.03f, -0.05f), new Vector3(0.77f, 0.04f, 0.1f), 0.003f, Paint);
            b.Box(new Vector3(-0.46f, 1.06f, 0.01f), new Vector3(0.07f, 2.12f, 0.02f), 0.005f, Paint);
            b.Box(new Vector3(0.46f, 1.06f, 0.01f), new Vector3(0.07f, 2.12f, 0.02f), 0.005f, Paint);
            b.Box(new Vector3(0f, 2.085f, 0.01f), new Vector3(0.99f, 0.07f, 0.02f), 0.005f, Paint);

            // ---------- полотно с двумя филёнками со стороны комнаты
            b.Box(new Vector3(0f, 1.005f, -0.05f), new Vector3(0.77f, 2.01f, 0.04f), 0.004f, Wood);
            b.Box(new Vector3(0f, 1.47f, -0.027f), new Vector3(0.56f, 0.86f, 0.008f), 0.004f, Wood);
            b.Box(new Vector3(0f, 0.47f, -0.027f), new Vector3(0.56f, 0.7f, 0.008f), 0.004f, Wood);

            // ---------- ручка: накладка, шток и нажимной рычаг
            b.Box(new Vector3(-0.318f, 1.0f, -0.025f), new Vector3(0.035f, 0.16f, 0.01f), 0.003f, Metal);
            b.Cylinder(new Vector3(-0.318f, 1.04f, -0.02f), 0.008f, 0.05f, 8, Metal, Quaternion.Euler(90f, 0f, 0f));
            b.Beam(new Vector3(-0.326f, 1.04f, 0.033f), new Vector3(-0.2f, 1.036f, 0.036f), 0.016f, 0.012f, 0.003f, Metal, Vector3.up);

            // ---------- петли на стороне без ручки
            foreach (float y in new[] { 0.25f, 1.75f })
                b.Cylinder(new Vector3(0.384f, y, -0.028f), 0.007f, 0.1f, 8, Metal, Quaternion.identity, 0.001f);

            Mesh mesh = b.Bake("SM_Door", 3);
            PBKit.Log("Door", mesh);

            PBKit.EditPrefab("PRB_Door", root =>
            {
                GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", mesh,
                    PBKit.Mat("MAT_Ceramic"), PBKit.Mat("MAT_Desk_Wood"), PBKit.Mat("MAT_Metal"));
                PBKit.FitBox(root.GetComponent<BoxCollider>(), mesh, m.transform, root.transform);
            });
        }
    }
}
