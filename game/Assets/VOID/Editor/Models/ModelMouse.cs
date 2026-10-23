using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VOID.EnvModels
{
    public static class ModelMouse
    {
        // Бежевая шариковая мышь 0.062 × 0.11 × 0.034, кнопки к −Z (к мониторам).
        // Шнур дальше по столу — часть PRB_Cables, здесь только короткий хвостик.
        const int Body = 0, Dark = 1;
        const float L = 0.11f, HalfW = 0.031f, Edge = 0.004f;

        [MenuItem("VOID/Env/Models/Mouse")]
        public static void Build()
        {
            var b = new PBKit.Builder();

            // ---------- корпус: горбатая верхняя поверхность и юбка до стола
            var top = new PBKit.Poly();
            const int nx = 8, nz = 14;
            top.Grid(nx, nz, Top, Vector3.up, 1);
            for (int i = 0; i < nz; i++)
            {
                float v0 = i / (float)nz, v1 = (i + 1) / (float)nz;
                Skirt(top, Top(0f, v0), Top(0f, v1), Vector3.left);
                Skirt(top, Top(1f, v0), Top(1f, v1), Vector3.right);
            }
            for (int i = 0; i < nx; i++)
            {
                float u0 = i / (float)nx, u1 = (i + 1) / (float)nx;
                Skirt(top, Top(u0, 0f), Top(u1, 0f), Vector3.forward);
                Skirt(top, Top(u0, 1f), Top(u1, 1f), Vector3.back);
            }
            b.Raw(top, Body);

            // ---------- разрез между кнопками и колесо
            var lines = new PBKit.Poly();
            var split = new List<Vector3>();
            for (int i = 0; i <= 6; i++) split.Add(Top(0.5f, 0.55f + 0.45f * i / 6f) + Vector3.up * 0.0003f);
            lines.Tube(split, 0.0006f, 4, 0);
            var cross = new List<Vector3>();
            for (int i = 0; i <= 8; i++) cross.Add(Top(0.08f + 0.84f * i / 8f, 0.55f) + Vector3.up * 0.0003f);
            lines.Tube(cross, 0.0006f, 4, 0);
            b.Raw(lines, Dark);

            Vector3 wheel = Top(0.5f, 0.76f);
            b.Cylinder(new Vector3(-0.003f, wheel.y - 0.004f, wheel.z), 0.0065f, 0.006f, 10, Dark,
                Quaternion.Euler(0f, 0f, -90f), 0f, true);

            // ---------- хвостик шнура из передней стенки
            var cord = new PBKit.Poly();
            cord.Tube(new List<Vector3> { new Vector3(0f, 0.006f, -L * 0.5f + 0.006f), new Vector3(0f, 0.004f, -L * 0.5f - 0.012f) }, 0.0018f, 5, 2);
            b.Raw(cord, Dark);

            Mesh mesh = b.Bake("SM_Mouse", 2);
            PBKit.Log("Mouse", mesh);

            PBKit.EnsurePrefab("PRB_Mouse", true);
            PBKit.EditPrefab("PRB_Mouse", root =>
            {
                GameObject m = PBKit.SetMeshChild(root.transform, "Mesh", mesh, PBKit.Mat("MAT_Ceramic"), PBKit.Mat("MAT_Plastic"));
                PBKit.FitBox(root.GetComponent<BoxCollider>(), mesh, m.transform, root.transform);
            });
        }

        /// <summary>Верх корпуса: u — поперёк (−X…+X), v — от хвоста (+Z) к кнопкам (−Z).</summary>
        static Vector3 Top(float u, float v)
        {
            // в плане — скруглённые торцы: к хвосту и к кнопкам корпус сужается
            float e = 2f * v - 1f;
            float round = Mathf.Sqrt(Mathf.Max(0.04f, 1f - e * e * e * e * e * e));
            float hw = HalfW * (0.86f + 0.14f * Mathf.Sin(Mathf.PI * v)) * round;
            float x = (2f * u - 1f);
            float s = Mathf.Sin(Mathf.PI * (0.03f + 0.94f * v)); // > 0 на всём отрезке
            float h = 0.009f + 0.025f * Mathf.Pow(s, 0.7f) * (1f - 0.25f * v);
            float y = Edge + (h - Edge) * Mathf.Sqrt(Mathf.Max(0f, 1f - x * x));
            return new Vector3(x * hw, y, L * 0.5f - L * v);
        }

        /// <summary>Вертикальная полоса от кромки верха до стола.</summary>
        static void Skirt(PBKit.Poly p, Vector3 a, Vector3 b, Vector3 outward)
        {
            p.Quad(a, b, new Vector3(b.x, 0f, b.z), new Vector3(a.x, 0f, a.z), outward, 0);
        }
    }
}
