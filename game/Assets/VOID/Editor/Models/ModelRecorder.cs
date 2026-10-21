using UnityEditor;
using UnityEngine;

namespace VOID.EnvModels
{
    public static class ModelRecorder
    {
        // Цифровой диктофон лежит плашмя: 56 × 19 × 106 мм. Решётка — к игроку (+Z).
        const float H = 0.019f;

        [MenuItem("VOID/Env/Models/Recorder")]
        public static void Build()
        {
            var b = new PBKit.Builder();
            Quaternion flat = Quaternion.identity;

            b.Box(new Vector3(0f, H * 0.5f, 0f), new Vector3(0.056f, H, 0.106f), 0.004f, 0);

            // решётка динамика
            for (int i = 0; i < 5; i++)
                b.Cap(new Vector3(0f, H - 0.0003f, 0.028f + i * 0.004f), 0.036f, 0.0025f, 0.0012f, 0.0004f, flat, 1);

            // окно дисплея: металлическая рамка и тёмное стекло
            b.Cap(new Vector3(0f, H - 0.0002f, 0.006f), 0.038f, 0.022f, 0.0006f, 0.0005f, flat, 1);
            b.Cap(new Vector3(0f, H - 0.0002f, 0.006f), 0.032f, 0.016f, 0.0009f, 0.0003f, flat, 0);

            // кнопки транспорта
            for (int i = 0; i < 4; i++)
                b.Cap(new Vector3(-0.015f + i * 0.01f, H - 0.0003f, -0.016f), 0.008f, 0.006f, 0.0025f, 0.0012f, flat, 1);

            // запись — светлая круглая кнопка
            b.Cylinder(new Vector3(0f, H - 0.0003f, -0.033f), 0.0045f, 0.0022f, 10, 2, flat);

            // регулятор громкости на боку (справа для игрока — это −X)
            b.Cap(new Vector3(-0.028f + 0.0003f, H * 0.5f, -0.01f), 0.008f, 0.014f, 0.0016f, 0.0006f,
                Quaternion.Euler(0f, 0f, 90f), 1);

            // микрофон на торце
            b.Cylinder(new Vector3(0f, 0.012f, 0.053f - 0.0002f), 0.003f, 0.0008f, 8, 1,
                Quaternion.FromToRotation(Vector3.up, Vector3.forward));

            Mesh mesh = b.Bake("SM_Recorder", 3);
            PBKit.Log("Recorder", mesh);

            PBKit.EditPrefab("PRB_Recorder", root =>
            {
                GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", mesh,
                    PBKit.Mat("MAT_Plastic"), PBKit.Mat("MAT_Metal"), PBKit.Mat("MAT_Ceramic"));
                PBKit.FitBox(root.GetComponent<BoxCollider>(), mesh, m.transform, root.transform);
            });
        }
    }
}
