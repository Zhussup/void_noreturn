using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VOID.EnvModels
{
    public static class ModelMug
    {
        // Кружка Ø0.085 × 0.096 с недопитым кофе, ручка на +X (как у greybox).
        const int Ceramic = 0, Coffee = 1;
        const float R = 0.0425f, H = 0.096f, Wall = 0.0035f, CoffeeY = 0.072f;
        static readonly Quaternion Up = Quaternion.Euler(-90f, 0f, 0f);

        [MenuItem("VOID/Env/Models/Mug")]
        public static void Build()
        {
            var b = new PBKit.Builder();
            var body = new PBKit.Poly();

            // дно, стенка, кромка и внутренняя стенка — разные группы сглаживания
            body.Lathe(Vector3.zero, new List<Vector2> { new Vector2(0f, 0f), new Vector2(R - 0.004f, 0f) }, 20, Up, 0);
            body.Lathe(Vector3.zero, new List<Vector2>
            {
                new Vector2(R - 0.004f, 0f), new Vector2(R - 0.0008f, 0.0025f), new Vector2(R, 0.008f), new Vector2(R, H),
            }, 20, Up, 1);
            body.Lathe(Vector3.zero, new List<Vector2>
            {
                new Vector2(R, H), new Vector2(R - Wall * 0.5f, H + 0.001f), new Vector2(R - Wall, H),
            }, 20, Up, 2);
            body.Lathe(Vector3.zero, new List<Vector2>
            {
                new Vector2(R - Wall, H), new Vector2(R - Wall, CoffeeY - 0.002f),
            }, 20, Up, 3);
            b.Raw(body, Ceramic);

            var coffee = new PBKit.Poly();
            coffee.Lathe(Vector3.zero, new List<Vector2> { new Vector2(R - Wall + 0.0005f, CoffeeY), new Vector2(0f, CoffeeY) }, 20, Up, 0);
            b.Raw(coffee, Coffee);

            // ---------- ручка: дуга, концы утоплены в стенку
            var handle = new PBKit.Poly();
            var path = PBKit.Spline(new List<Vector3>
            {
                new Vector3(R - 0.004f, 0.078f, 0f), new Vector3(R + 0.016f, 0.08f, 0f), new Vector3(R + 0.028f, 0.066f, 0f),
                new Vector3(R + 0.029f, 0.038f, 0f), new Vector3(R + 0.017f, 0.022f, 0f), new Vector3(R - 0.004f, 0.022f, 0f),
            }, 4);
            handle.Tube(path, 0.0055f, 8, 4);
            b.Raw(handle, Ceramic);

            Mesh mesh = b.Bake("SM_Mug", 2);
            PBKit.Log("Mug", mesh);

            PBKit.EditPrefab("PRB_Mug", root =>
            {
                GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", mesh, PBKit.Mat("MAT_Ceramic"), PBKit.Mat("MAT_Plastic"));
                PBKit.FitBox(root.GetComponent<BoxCollider>(), mesh, m.transform, root.transform);
            });
        }
    }
}
