using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VOID.EnvModels
{
    public static class ModelWindowFrame
    {
        // Рама 1.6 × 1.3 (пивот — низ проёма, y = 0 у подоконника), сторона комнаты — +Z.
        // Импост по центру, над створками — фрамуги. Нижние створки открывающиеся,
        // с ручками у импоста. Стёкла — отдельный объект Glazing без теней.
        const int Paint = 0, Metal = 1;
        const float FrameZ = -0.06f, SashZ = -0.058f, GlassZ = -0.06f;

        [MenuItem("VOID/Env/Models/WindowFrame")]
        public static void Build()
        {
            var b = new PBKit.Builder();

            // ---------- коробка рамы, импост и горбыльки фрамуг (как у greybox)
            b.Box(new Vector3(-0.77f, 0.65f, FrameZ), new Vector3(0.06f, 1.3f, 0.05f), 0.004f, Paint);
            b.Box(new Vector3(0.77f, 0.65f, FrameZ), new Vector3(0.06f, 1.3f, 0.05f), 0.004f, Paint);
            b.Box(new Vector3(0f, 0.03f, FrameZ), new Vector3(1.48f, 0.06f, 0.05f), 0.004f, Paint);
            b.Box(new Vector3(0f, 1.27f, FrameZ), new Vector3(1.48f, 0.06f, 0.05f), 0.004f, Paint);
            b.Box(new Vector3(0f, 0.65f, FrameZ), new Vector3(0.05f, 1.18f, 0.03f), 0.003f, Paint);
            b.Box(new Vector3(-0.383f, 0.885f, FrameZ), new Vector3(0.715f, 0.05f, 0.03f), 0.003f, Paint);
            b.Box(new Vector3(0.383f, 0.885f, FrameZ), new Vector3(0.715f, 0.05f, 0.03f), 0.003f, Paint);

            // ---------- подоконник со свесом в комнату
            b.Box(new Vector3(0f, -0.015f, 0.035f), new Vector3(1.72f, 0.03f, 0.07f), 0.004f, Paint);

            // ---------- створки: рамка-обвязка внутри нижних проёмов
            foreach (float sx in new[] { -1f, 1f })
            {
                float cx = sx * 0.3825f;
                const float w = 0.715f, h = 0.8f, cy = 0.46f, t = 0.045f;
                b.Box(new Vector3(cx, cy - h * 0.5f + t * 0.5f, SashZ), new Vector3(w, t, 0.04f), 0.003f, Paint);
                b.Box(new Vector3(cx, cy + h * 0.5f - t * 0.5f, SashZ), new Vector3(w, t, 0.04f), 0.003f, Paint);
                b.Box(new Vector3(cx - w * 0.5f + t * 0.5f, cy, SashZ), new Vector3(t, h - 2f * t, 0.04f), 0.003f, Paint);
                b.Box(new Vector3(cx + w * 0.5f - t * 0.5f, cy, SashZ), new Vector3(t, h - 2f * t, 0.04f), 0.003f, Paint);

                // оконная ручка у импоста: розетка и рычаг вниз
                float hx = sx * 0.05f;
                b.Box(new Vector3(hx, 0.5f, -0.034f), new Vector3(0.026f, 0.06f, 0.008f), 0.002f, Metal);
                b.Beam(new Vector3(hx, 0.51f, -0.026f), new Vector3(hx, 0.41f, -0.02f), 0.014f, 0.01f, 0.003f, Metal, Vector3.forward);
            }

            // ---------- штапик по стёклам фрамуг
            foreach (float sx in new[] { -1f, 1f })
            {
                float cx = sx * 0.3825f;
                b.Box(new Vector3(cx, 1.235f, -0.045f), new Vector3(0.715f, 0.012f, 0.012f), 0f, Paint);
                b.Box(new Vector3(cx, 0.915f, -0.045f), new Vector3(0.715f, 0.012f, 0.012f), 0f, Paint);
            }

            Mesh frame = b.Bake("SM_WindowFrame", 2);

            // ---------- стёкла: две фрамуги и две створки
            var g = new PBKit.Builder();
            foreach (float sx in new[] { -1f, 1f })
            {
                float cx = sx * 0.3825f;
                g.Box(new Vector3(cx, 1.075f, GlassZ), new Vector3(0.715f, 0.33f, 0.004f), 0f, 0);
                g.Box(new Vector3(cx, 0.46f, GlassZ), new Vector3(0.63f, 0.715f, 0.004f), 0f, 0);
            }
            Mesh glass = g.Bake("SM_WindowFrame_Glass", 1);

            PBKit.Log("WindowFrame", frame, glass);

            PBKit.EditPrefab("PRB_WindowFrame", root =>
            {
                GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", frame,
                    PBKit.Mat("MAT_Ceramic"), PBKit.Mat("MAT_Metal"));
                GameObject gl = PBKit.SetMeshChild(root.transform, "Glazing", glass, PBKit.GlassMat());
                gl.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                PBKit.FitBox(root.GetComponent<BoxCollider>(), frame, m.transform, root.transform);
            });
        }
    }
}
