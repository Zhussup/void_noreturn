using UnityEditor;
using UnityEngine;

namespace VOID.EnvModels
{
    public static class ModelKeyboard
    {
        const float Pitch = 0.0185f;   // шаг клавиш
        const float Gap = 0.0025f;     // зазор между колпачками
        const float KeyH = 0.007f;

        // Корпус: 0.45 × 0.15, спереди 18 мм, сзади 30 мм.
        const float HalfW = 0.225f, HalfD = 0.075f, FrontH = 0.018f, BackH = 0.030f;

        [MenuItem("VOID/Env/Models/Keyboard")]
        public static void Build()
        {
            var b = new PBKit.Builder();

            var c = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float x = (i & 1) == 0 ? -HalfW : HalfW;
                float z = (i & 4) == 0 ? -HalfD : HalfD;
                float y = (i & 2) == 0 ? 0f : (z > 0f ? FrontH : BackH);
                c[i] = new Vector3(x, y, z);
            }
            b.Hex(c, 0.005f, 0);

            float slope = Mathf.Atan2(BackH - FrontH, 2f * HalfD) * Mathf.Rad2Deg;
            Quaternion rot = Quaternion.Euler(slope, 0f, 0f);

            // Ряды сзади вперёд. Ширины в «юнитах», 0 — пропуск.
            float[][] main =
            {
                new[] { 1f, -1f, 1f, 1f, 1f, 1f, -0.5f, 1f, 1f, 1f, 1f, -0.5f, 1f, 1f, 1f, 1f },
                new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 2f },
                new[] { 1.5f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1.5f },
                new[] { 1.75f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 2.25f },
                new[] { 2.25f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 2.75f },
                new[] { 1.25f, 1.25f, 1.25f, 6.25f, 1.25f, 1.25f, 1.25f, 1.25f },
            };
            float[][] nav =
            {
                new[] { 1f, 1f, 1f },
                new[] { 1f, 1f, 1f },
                new[] { 1f, 1f, 1f },
                new float[0],
                new[] { -1f, 1f },
                new[] { 1f, 1f, 1f },
            };
            float[][] pad =
            {
                new float[0],
                new[] { 1f, 1f, 1f, 1f },
                new[] { 1f, 1f, 1f },
                new[] { 1f, 1f, 1f },
                new[] { 1f, 1f, 1f },
                new[] { 2f, 1f },
            };

            const float blockGap = 0.008f;
            float x0 = -(22f * Pitch + 2f * blockGap) * 0.5f;
            float navX = x0 + 15f * Pitch + blockGap;
            float padX = navX + 3f * Pitch + blockGap;

            for (int r = 0; r < 6; r++)
            {
                Row(b, main[r], x0, r, 1f, rot);
                Row(b, nav[r], navX, r, 1f, rot);
                Row(b, pad[r], padX, r, 1f, rot);
            }
            // высокие «+» и Enter на цифровом блоке
            Key(b, padX + 3f * Pitch, 2, 1f, 2f, rot);
            Key(b, padX + 3f * Pitch, 4, 1f, 2f, rot);

            // индикаторы над цифровым блоком
            for (int i = 0; i < 3; i++)
            {
                float x = -(padX + (0.6f + i * 1.4f) * Pitch);
                float z = RowZ(0);
                b.Cap(new Vector3(x, TopY(z), z), 0.006f, 0.004f, 0.0012f, 0.0006f, rot, 1);
            }

            Mesh mesh = b.Bake("SM_Keyboard", 2);
            PBKit.Log("Keyboard", mesh);

            PBKit.EditPrefab("PRB_Keyboard", root =>
            {
                GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", mesh,
                    PBKit.Mat("MAT_Plastic"), PBKit.Mat("MAT_Plastic_Keys"));
                PBKit.FitBox(root.GetComponent<BoxCollider>(), mesh, m.transform, root.transform);
            });
        }

        static float TopY(float z) => FrontH + (HalfD - z) / (2f * HalfD) * (BackH - FrontH);

        // Ряд 0 — функциональный, дальше отступ 5 мм и пять рядов.
        static float RowZ(int r)
        {
            float back = -0.062f;
            float z = back + Pitch * (r + 0.5f) + (r > 0 ? 0.005f : 0f);
            return z;
        }

        static void Row(PBKit.Builder b, float[] widths, float x, int r, float depth, Quaternion rot)
        {
            foreach (float w in widths)
            {
                float u = Mathf.Abs(w);
                if (w > 0f) Key(b, x, r, u, depth, rot);
                x += u * Pitch;
            }
        }

        static void Key(PBKit.Builder b, float x, int r, float units, float rows, Quaternion rot)
        {
            float w = units * Pitch - Gap;
            float d = rows * Pitch - Gap;
            float z = RowZ(r) + (rows - 1f) * Pitch * 0.5f;
            // Игрок смотрит в −Z, поэтому его правая сторона — −X: раскладку отражаем.
            float cx = -(x + units * Pitch * 0.5f);
            b.Cap(new Vector3(cx, TopY(z) - 0.0005f, z), w, d, KeyH, 0.0018f, rot, 1);
        }
    }
}
