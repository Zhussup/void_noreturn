using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VOID.EnvModels
{
    public static class ModelDeskLamp
    {
        // Шарнирная лампа по greybox: основание Ø0.14, нижнее плечо под 15°,
        // верхнее — под 70°, плафон смотрит туда же, куда Light_DeskLamp (наклон 60°).
        // Свет и оба BoxCollider префаба не трогаем — габариты те же.
        const int Plastic = 0, Metal = 1, Inner = 2, Bulb = 3;

        static readonly Vector3 BasePivot = new Vector3(0f, 0.025f, -0.02f);
        static readonly Vector3 Elbow = new Vector3(0f, 0.315f, 0.058f);
        static readonly Vector3 ShadeCenter = new Vector3(0f, 0.365f, 0.313f);

        [MenuItem("VOID/Env/Models/DeskLamp")]
        public static void Build()
        {
            var b = new PBKit.Builder();
            Vector3 axis = Quaternion.Euler(60f, 0f, 0f) * Vector3.forward; // куда светит плафон
            Vector3 shadeBack = ShadeCenter - axis * 0.045f;
            Quaternion alongX = Quaternion.Euler(0f, 0f, -90f);

            // ---------- утяжелённое основание с клавишей
            b.Cylinder(Vector3.zero, 0.07f, 0.022f, 16, Plastic, Quaternion.identity, 0.005f, true, false);
            b.Box(new Vector3(0.035f, 0.024f, 0.03f), new Vector3(0.018f, 0.006f, 0.012f), 0.002f, Plastic);
            b.Box(new Vector3(0f, 0.03f, -0.02f), new Vector3(0.03f, 0.018f, 0.022f), 0.003f, Plastic);

            // ---------- шарниры
            b.Cylinder(BasePivot + new Vector3(-0.016f, 0f, 0f), 0.009f, 0.032f, 8, Metal, alongX, 0.001f);
            b.Cylinder(Elbow + new Vector3(-0.015f, 0f, 0f), 0.011f, 0.03f, 8, Metal, alongX, 0.001f);
            b.Cylinder(shadeBack + new Vector3(-0.013f, 0f, 0f), 0.008f, 0.026f, 8, Metal, alongX, 0.001f);

            // ---------- плечи: парные тяги
            foreach (float x in new[] { -0.008f, 0.008f })
            {
                b.Beam(BasePivot + Vector3.right * x, Elbow + Vector3.right * x, 0.006f, 0.006f, 0.001f, Metal, Vector3.up);
                b.Beam(Elbow + Vector3.right * x, shadeBack + Vector3.right * x, 0.006f, 0.006f, 0.001f, Metal, Vector3.up);
            }

            // ---------- плафон: снаружи металл, внутри светлая эмаль, в нём лампочка
            Quaternion rot = Quaternion.LookRotation(axis, Vector3.up);
            var outer = new PBKit.Poly();
            outer.Lathe(ShadeCenter, new List<Vector2>
            {
                new Vector2(0f, -0.05f), new Vector2(0.022f, -0.047f), new Vector2(0.03f, -0.02f),
                new Vector2(0.045f, 0.01f), new Vector2(0.06f, 0.04f), new Vector2(0.057f, 0.04f),
            }, 16, rot, 1);
            b.Raw(outer, Metal);

            var inner = new PBKit.Poly();
            inner.Lathe(ShadeCenter, new List<Vector2>
            {
                new Vector2(0.057f, 0.04f), new Vector2(0.042f, 0.011f), new Vector2(0.027f, -0.019f),
                new Vector2(0.019f, -0.044f), new Vector2(0f, -0.046f),
            }, 16, rot, 2);
            b.Raw(inner, Inner);

            var bulb = new PBKit.Poly();
            var prof = new List<Vector2>();
            for (int i = 0; i <= 6; i++)
            {
                float a = Mathf.PI * i / 6f;
                prof.Add(new Vector2(0.018f * Mathf.Sin(a), -0.012f - 0.018f * Mathf.Cos(a)));
            }
            bulb.Lathe(ShadeCenter, prof, 10, rot, 3);
            b.Raw(bulb, Bulb);

            Mesh mesh = b.Bake("SM_DeskLamp", 4);
            PBKit.Log("DeskLamp", mesh);

            PBKit.EditPrefab("PRB_DeskLamp", root =>
            {
                PBKit.SetMeshChild(root.transform, "Mesh", mesh,
                    PBKit.Mat("MAT_Plastic"), PBKit.Mat("MAT_Metal"), PBKit.Mat("MAT_Ceramic"), PBKit.Mat("MAT_Paper"));
            });
        }
    }
}
