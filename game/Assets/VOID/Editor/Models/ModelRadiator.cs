using UnityEditor;
using UnityEngine;

namespace VOID.EnvModels
{
    public static class ModelRadiator
    {
        // Чугунная батарея на 10 секций, 1.0 × 0.5 (y 0.1…0.6), глубина 0.08.
        // Стена — за −Z (зазор ~5.5 см): туда уходят подводки и кронштейны.
        const float WallZ = -0.055f;

        [MenuItem("VOID/Env/Models/Radiator")]
        public static void Build()
        {
            var b = new PBKit.Builder();

            // ---------- секции: по две колонки со скруглёнными краями
            for (int i = 0; i < 10; i++)
            {
                float x = -0.45f + i * 0.1f;
                b.Box(new Vector3(x, 0.35f, 0.021f), new Vector3(0.085f, 0.48f, 0.034f), 0.009f, 0);
                b.Box(new Vector3(x, 0.35f, -0.021f), new Vector3(0.085f, 0.48f, 0.034f), 0.009f, 0);
            }

            // ---------- коллекторы сверху и снизу, сквозь все секции
            Quaternion alongX = Quaternion.Euler(0f, 0f, -90f);
            b.Cylinder(new Vector3(-0.5f, 0.545f, 0f), 0.022f, 1.0f, 10, 0, alongX, 0f, true);
            b.Cylinder(new Vector3(-0.5f, 0.155f, 0f), 0.022f, 1.0f, 10, 0, alongX, 0f, true);

            // ---------- подводки слева: колено в стену, на верхней — кран
            Quaternion toWall = Quaternion.Euler(-90f, 0f, 0f);
            foreach (float y in new[] { 0.545f, 0.155f })
            {
                b.Cylinder(new Vector3(-0.56f, y, 0f), 0.013f, 0.06f, 8, 0, alongX, 0f, true);
                // от колена до стены и чуть в неё, чтобы не было видно торца
                b.Cylinder(new Vector3(-0.56f, y, 0.013f), 0.013f, 0.013f - WallZ + 0.02f, 8, 0, toWall, 0f, true);
            }
            b.Cylinder(new Vector3(-0.53f, 0.545f, 0f), 0.016f, 0.03f, 8, 0, Quaternion.identity, 0.002f, true);
            b.Cylinder(new Vector3(-0.53f, 0.575f, 0f), 0.004f, 0.02f, 6, 0, Quaternion.identity);
            b.Box(new Vector3(-0.53f, 0.597f, 0f), new Vector3(0.05f, 0.006f, 0.012f), 0.002f, 0);

            // ---------- кронштейны под верхним коллектором, в зазорах между секциями
            foreach (float x in new[] { -0.3f, 0.3f })
                b.Beam(new Vector3(x, 0.52f, WallZ), new Vector3(x, 0.52f, -0.005f), 0.012f, 0.012f, 0.002f, 0, Vector3.up);

            Mesh mesh = b.Bake("SM_Radiator", 1);
            PBKit.Log("Radiator", mesh);

            PBKit.EditPrefab("PRB_Radiator", root =>
            {
                GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", mesh, PBKit.Mat("MAT_Metal"));
                PBKit.FitBox(root.GetComponent<BoxCollider>(), mesh, m.transform, root.transform);
            });
        }
    }
}
