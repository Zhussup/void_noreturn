using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VOID.EnvModels
{
    public static class ModelGlass
    {
        // Гладкий стакан Ø0.07 × 0.11 с толстым дном, MAT_Glass.
        const float R = 0.035f, H = 0.11f, Wall = 0.0025f, Bottom = 0.012f;
        static readonly Quaternion Up = Quaternion.Euler(-90f, 0f, 0f);

        [MenuItem("VOID/Env/Models/Glass")]
        public static void Build()
        {
            var b = new PBKit.Builder();
            var p = new PBKit.Poly();

            p.Lathe(Vector3.zero, new List<Vector2> { new Vector2(0f, 0f), new Vector2(R - 0.004f, 0f) }, 20, Up, 0);
            p.Lathe(Vector3.zero, new List<Vector2>
            {
                new Vector2(R - 0.004f, 0f), new Vector2(R - 0.0025f, 0.002f), new Vector2(R - 0.0015f, 0.01f), new Vector2(R, H),
            }, 20, Up, 1);
            p.Lathe(Vector3.zero, new List<Vector2>
            {
                new Vector2(R, H), new Vector2(R - Wall * 0.5f, H + 0.0008f), new Vector2(R - Wall, H),
            }, 20, Up, 2);
            p.Lathe(Vector3.zero, new List<Vector2>
            {
                new Vector2(R - Wall, H), new Vector2(R - Wall - 0.0015f, Bottom + 0.003f), new Vector2(R - Wall - 0.006f, Bottom),
            }, 20, Up, 3);
            p.Lathe(Vector3.zero, new List<Vector2> { new Vector2(R - Wall - 0.006f, Bottom), new Vector2(0f, Bottom) }, 20, Up, 0);
            b.Raw(p, 0);

            Mesh mesh = b.Bake("SM_Glass", 1);
            PBKit.Log("Glass", mesh);

            PBKit.EditPrefab("PRB_Glass", root =>
            {
                GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", mesh, PBKit.GlassMat());
                PBKit.FitBox(root.GetComponent<BoxCollider>(), mesh, m.transform, root.transform);
            });
        }
    }
}
