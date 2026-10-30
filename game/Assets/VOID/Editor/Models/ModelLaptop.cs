using UnityEditor;
using UnityEngine;

namespace VOID.EnvModels
{
    public static class ModelLaptop
    {
        // Закрытый ноутбук 0.27 × 0.19 × 0.02, как у greybox (декор). Крышка — отдельный
        // объект Lid с пивотом на петле (задняя кромка, верх корпуса). Экрана нет:
        // NightDirector ищет Laptop/Lid/Screen, не находит и надпись не рисует.
        const float BaseTop = 0.012f;
        static readonly Vector3 Hinge = new Vector3(0f, BaseTop, -0.093f);

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
            Mesh baseMesh = b.Bake("SM_Laptop", 1);

            // ---------- крышка в координатах петли
            var l = new PBKit.Builder();
            l.Box(new Vector3(0f, 0.004f, 0.095f), new Vector3(0.27f, 0.008f, 0.19f), 0.0025f, 0);
            l.Cylinder(new Vector3(0f, 0.008f, 0.1f), 0.012f, 0.0004f, 16, 1, Quaternion.identity, 0f, false, false);
            Mesh lidMesh = l.Bake("SM_Laptop_Lid", 2);

            PBKit.Log("Laptop", baseMesh, lidMesh);

            PBKit.EditPrefab("PRB_Laptop", root =>
            {
                GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", baseMesh, PBKit.Mat("MAT_Plastic"));
                GameObject lid = PBKit.SetMeshChild(root.transform, "Lid", lidMesh, PBKit.Mat("MAT_Metal"), PBKit.Mat("MAT_Ceramic"));
                lid.transform.localPosition = Hinge;

                // один BoxCollider на весь закрытый ноутбук (второй, от открытой крышки, убираем)
                var cols = root.GetComponents<BoxCollider>();
                for (int i = cols.Length - 1; i >= 1; i--) Object.DestroyImmediate(cols[i]);
                cols[0].center = new Vector3(0f, 0.01f, 0f);
                cols[0].size = new Vector3(0.27f, 0.02f, 0.19f);
            });
        }
    }
}
