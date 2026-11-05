using UnityEditor;
using UnityEngine;

namespace VOID.EnvModels
{
    /// <summary>
    /// Подготовка комнаты к текстурам. Стены, пол и потолок были кубами Unity
    /// с масштабом в Transform — UV 0…1 на грань, текстура растягивалась на всю
    /// стену. Здесь каждый сегмент запекается в свой меш с UV в метрах
    /// (1 UV = 1 м, как у SM_*), масштаб сегмента становится 1. Геометрия
    /// строится в координатах родителя сегмента, поэтому UV соседних сегментов
    /// одной стены стыкуются без шва. Бумаги и постер — одна лицевая плоскость
    /// с UV 0…1 под уникальную картинку.
    /// Коллайдеры на корнях префабов и имена объектов не меняются.
    /// </summary>
    public static class ModelRoomShell
    {
        static readonly string[] ShellPrefabs =
        {
            "PRB_Wall_North", "PRB_Wall_South", "PRB_Wall_East", "PRB_Wall_West",
            "PRB_Floor", "PRB_Ceiling",
        };

        [MenuItem("VOID/Env/Models/Стены, пол, потолок — UV в метрах")]
        public static void BuildShell()
        {
            foreach (string prefab in ShellPrefabs)
            {
                PBKit.EditPrefab(prefab, root =>
                {
                    foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (!IsUnitCube(mf)) continue;
                        BakeSegment(prefab, mf);
                    }
                });
            }
        }

        [MenuItem("VOID/Env/Models/Бумаги и постер — плоскости")]
        public static void BuildFlats()
        {
            // A4 альбомно: лицевая сторона вверх, верх листа на 2 мм над столом
            Mesh paper = Flat("SM_Paper", new Vector3(0f, 0.002f, 0f),
                              Vector3.right * 0.297f, Vector3.forward * 0.21f, Vector3.up);
            // постер 0.5 × 0.7: лицом в комнату (+Z), в 3 мм от стены;
            // зритель смотрит вдоль −Z, его «право» — −X, поэтому U идёт по −X
            Mesh poster = Flat("SM_Poster", new Vector3(0f, 0f, 0.003f),
                               Vector3.left * 0.5f, Vector3.up * 0.7f, Vector3.forward);

            SetFlat("PRB_Paper", paper);
            SetFlat("PRB_Poster", poster);
            PBKit.Log("Flats", paper, poster);
        }

        // ------------------------------------------------------------ сегменты

        static bool IsUnitCube(MeshFilter mf)
        {
            Mesh m = mf.sharedMesh;
            return m != null && m.name == "Cube" && m.vertexCount == 24 &&
                   AssetDatabase.GetAssetPath(m) == "Library/unity default resources";
        }

        static void BakeSegment(string prefab, MeshFilter mf)
        {
            Transform t = mf.transform;
            if (t.localRotation != Quaternion.identity)
            {
                Debug.LogWarning("[VOID] " + prefab + "/" + t.name + ": повёрнут — пропущен");
                return;
            }

            // имя ассета: SM_Wall_North_Seg_Left; у одиночного Mesh — SM_Floor
            string baseName = "SM_" + prefab.Substring("PRB_".Length);
            string meshName = t.name == "Mesh" ? baseName : baseName + "_" + t.name;

            var b = new PBKit.Builder();
            b.Box(t.localPosition, t.localScale, 0f, 0);
            Mesh mesh = b.Bake(meshName, 1);

            t.localPosition = Vector3.zero;
            t.localScale = Vector3.one;
            mf.sharedMesh = mesh;
            PBKit.Log(prefab + "/" + t.name, mesh);
        }

        // ------------------------------------------------------------ плоскости

        /// <summary>Прямоугольник: центр, оси U и V (длина = размер), нормаль лица.</summary>
        static Mesh Flat(string name, Vector3 center, Vector3 u, Vector3 v, Vector3 normal)
        {
            Vector3 hu = u * 0.5f, hv = v * 0.5f;
            var verts = new[] { center - hu - hv, center + hu - hv, center + hu + hv, center - hu + hv };
            var uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };

            // Unity рисует лицо, у которого cross(b − a, c − a) смотрит на зрителя
            int[] tris = Vector3.Dot(Vector3.Cross(verts[1] - verts[0], verts[2] - verts[0]), normal) > 0f
                ? new[] { 0, 1, 2, 0, 2, 3 }
                : new[] { 0, 2, 1, 0, 3, 2 };

            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.normals = new[] { normal, normal, normal, normal };
            mesh.uv = uv;
            mesh.uv2 = uv;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return PBKit.SaveMesh(mesh);
        }

        static void SetFlat(string prefab, Mesh mesh)
        {
            PBKit.EditPrefab(prefab, root =>
            {
                Transform t = root.transform.Find("Mesh");
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
                t.GetComponent<MeshFilter>().sharedMesh = mesh;
            });
        }
    }
}
