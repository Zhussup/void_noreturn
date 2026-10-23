using UnityEditor;
using UnityEngine;

namespace VOID.EnvModels
{
    public static class ModelPills
    {
        const float CardT = 0.0015f;

        [MenuItem("VOID/Env/Models/Pills")]
        public static void Build()
        {
            // Блистер: карта из фольги и 10 выдавленных ячеек 2 × 5.
            var b = new PBKit.Builder();
            b.Box(new Vector3(0f, CardT * 0.5f, 0f), new Vector3(0.055f, CardT, 0.095f), 0f, 0);
            for (int i = 0; i < 10; i++)
                b.Cylinder(CellPos(i), 0.0078f, 0.0008f, 8, 0, Quaternion.identity, 0f, false, false);
            Mesh blister = b.Bake("SM_Pills", 1);

            // Таблетка — общий меш для всех десяти, пивот в центре основания.
            var p = new PBKit.Builder();
            p.Cylinder(Vector3.zero, 0.0055f, 0.004f, 8, 0, Quaternion.identity, 0f, true);
            Mesh pill = p.Bake("SM_Pill", 1);

            PBKit.Log("Pills", blister, pill);

            PBKit.EditPrefab("PRB_Pills", root =>
            {
                GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", blister, PBKit.Mat("MAT_Metal"));
                Material ceramic = PBKit.Mat("MAT_Ceramic");
                for (int i = 0; i < 10; i++)
                {
                    // Pill_01…Pill_10 — прямые дети Pills, чтобы код находил их Find("Pill_XX").
                    GameObject g = PBKit.SetMeshChild(root.transform, "Pill_" + (i + 1).ToString("00"), pill, ceramic);
                    g.transform.localPosition = CellPos(i);
                }
                PBKit.FitBox(root.GetComponent<BoxCollider>(), blister, m.transform, root.transform);
            });
        }

        // Те же места, что у greybox: x ±0.012, z от −0.036 до 0.036 шагом 0.018.
        static Vector3 CellPos(int i)
        {
            float x = (i % 2 == 0) ? -0.012f : 0.012f;
            float z = -0.036f + (i / 2) * 0.018f;
            return new Vector3(x, CardT, z);
        }
    }
}
