using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VOID.EnvModels
{
    public static class ModelCeilingLamp
    {
        // Подвес от потолка (пивот на потолке, всё ниже y = 0): чашка, шнур,
        // патрон и бумажный абажур-конус с открытым низом. Light_Ceiling не трогаем.
        const int Plastic = 0, Paper = 1;
        static readonly Quaternion Up = Quaternion.Euler(-90f, 0f, 0f); // ось Z профиля → +Y

        [MenuItem("VOID/Env/Models/CeilingLamp")]
        public static void Build()
        {
            var b = new PBKit.Builder();

            // ---------- потолочная чашка, шнур и патрон
            b.Cylinder(new Vector3(0f, -0.014f, 0f), 0.05f, 0.014f, 16, Plastic, Quaternion.identity, 0.004f, true, false);
            b.Cylinder(new Vector3(0f, -0.09f, 0f), 0.0035f, 0.077f, 6, Plastic, Quaternion.identity, 0f, true, false);
            b.Cylinder(new Vector3(0f, -0.118f, 0f), 0.017f, 0.03f, 10, Plastic, Quaternion.identity, 0.002f, true);

            // ---------- абажур: снаружи снизу вверх, затем внутренняя сторона обратно
            var shade = new PBKit.Poly();
            shade.Lathe(Vector3.zero, new List<Vector2>
            {
                new Vector2(0.15f, -0.152f), new Vector2(0.135f, -0.125f), new Vector2(0.09f, -0.1f),
                new Vector2(0.035f, -0.088f), new Vector2(0.02f, -0.087f),
            }, 20, Up, 1);
            shade.Lathe(Vector3.zero, new List<Vector2>
            {
                new Vector2(0.02f, -0.087f), new Vector2(0.018f, -0.09f),
            }, 20, Up, 0);
            shade.Lathe(Vector3.zero, new List<Vector2>
            {
                new Vector2(0.018f, -0.09f), new Vector2(0.034f, -0.091f), new Vector2(0.088f, -0.103f),
                new Vector2(0.132f, -0.127f), new Vector2(0.147f, -0.152f),
            }, 20, Up, 2);
            shade.Lathe(Vector3.zero, new List<Vector2>
            {
                new Vector2(0.147f, -0.152f), new Vector2(0.15f, -0.152f),
            }, 20, Up, 0);
            b.Raw(shade, Paper);

            // ---------- лампочка под патроном
            var bulb = new PBKit.Poly();
            var prof = new List<Vector2>();
            for (int i = 0; i <= 6; i++)
            {
                float a = Mathf.PI * i / 6f;
                prof.Add(new Vector2(0.027f * Mathf.Sin(a), -0.145f - 0.027f * Mathf.Cos(a)));
            }
            bulb.Lathe(Vector3.zero, prof, 10, Up, 3);
            b.Raw(bulb, Paper);

            Mesh mesh = b.Bake("SM_CeilingLamp", 2);
            PBKit.Log("CeilingLamp", mesh);

            PBKit.EditPrefab("PRB_CeilingLamp", root =>
            {
                GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", mesh,
                    PBKit.Mat("MAT_Plastic"), PBKit.Mat("MAT_Paper"));
                PBKit.FitBox(root.GetComponent<BoxCollider>(), mesh, m.transform, root.transform);
            });
        }
    }
}
