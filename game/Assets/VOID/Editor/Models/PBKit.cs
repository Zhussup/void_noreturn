using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

namespace VOID.EnvModels
{
    /// <summary>
    /// Общий набор для моделей окружения. Детали строятся как временные
    /// ProBuilderMesh (скосы, автоматические UV), затем запекаются в обычный
    /// UnityEngine.Mesh в Art/Models, а заготовки удаляются. В префабах
    /// компонентов ProBuilder не остаётся.
    /// </summary>
    public static class PBKit
    {
        public const string ModelsDir = "Assets/VOID/Art/Models";
        public const string MaterialsDir = "Assets/VOID/Art/Materials";
        public const string PrefabsDir = "Assets/VOID/Prefabs";

        public class Builder
        {
            readonly List<(ProBuilderMesh pb, int sub)> parts = new List<(ProBuilderMesh, int)>();

            public ProBuilderMesh Add(ProBuilderMesh pb, int sub)
            {
                // NaN в вершинах подвешивает развёртку UV — падаем сразу и понятно
                foreach (Vector3 p in pb.positions)
                    if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z))
                    {
                        Object.DestroyImmediate(pb.gameObject);
                        throw new System.ArgumentException("[VOID] NaN в вершинах детали");
                    }
                parts.Add((pb, sub));
                return pb;
            }

            /// <summary>Коробка по центру и размеру, со скосом всех рёбер.</summary>
            public ProBuilderMesh Box(Vector3 center, Vector3 size, float bevel, int sub)
            {
                return Box(center, size, bevel, sub, Quaternion.identity);
            }

            public ProBuilderMesh Box(Vector3 center, Vector3 size, float bevel, int sub, Quaternion rot)
            {
                Vector3 h = size * 0.5f;
                var c = new Vector3[8];
                for (int i = 0; i < 8; i++)
                {
                    var p = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                    c[i] = center + rot * p;
                }
                return Hex(c, bevel, sub);
            }

            /// <summary>Брусок сечением w × h между точками a и b (рёбра со скосом).</summary>
            public ProBuilderMesh Beam(Vector3 a, Vector3 b, float w, float h, float bevel, int sub, Vector3 up)
            {
                Vector3 dir = b - a;
                if (Mathf.Abs(Vector3.Dot(dir.normalized, up.normalized)) > 0.99f) up = Vector3.forward;
                return Box((a + b) * 0.5f, new Vector3(w, h, dir.magnitude), bevel, sub, Quaternion.LookRotation(dir, up));
            }

            /// <summary>
            /// Колпачок клавиши/кнопки: основание w×d, верх сужен на inset,
            /// без нижней грани. base — центр основания, rot — наклон панели.
            /// </summary>
            public ProBuilderMesh Cap(Vector3 basePoint, float w, float d, float h, float inset, Quaternion rot, int sub)
            {
                var c = new Vector3[8];
                for (int i = 0; i < 8; i++)
                {
                    bool top = (i & 2) != 0;
                    float hx = w * 0.5f - (top ? inset : 0f);
                    float hz = d * 0.5f - (top ? inset : 0f);
                    var p = new Vector3((i & 1) == 0 ? -hx : hx, top ? h : 0f, (i & 4) == 0 ? -hz : hz);
                    c[i] = basePoint + rot * p;
                }
                return Hex(c, 0f, sub, false);
            }

            /// <summary>
            /// Шестигранник по 8 углам. Индекс угла: бит0 — +X, бит1 — +Y, бит2 — +Z.
            /// Подходит для клиньев, усечённых пирамид, наклонных корпусов.
            /// </summary>
            public ProBuilderMesh Hex(Vector3[] c, float bevel, int sub, bool bottom = true)
            {
                int[][] quads =
                {
                    new[] { 0, 2, 3, 1 }, // -Z
                    new[] { 5, 7, 6, 4 }, // +Z
                    new[] { 4, 6, 2, 0 }, // -X
                    new[] { 1, 3, 7, 5 }, // +X
                    new[] { 2, 6, 7, 3 }, // +Y
                    new[] { 4, 0, 1, 5 }, // -Y
                };
                var pos = new List<Vector3>();
                var faces = new List<Face>();
                for (int qi = 0; qi < quads.Length; qi++)
                {
                    if (!bottom && qi == 5) continue;
                    int[] q = quads[qi];
                    int b = pos.Count;
                    foreach (int i in q) pos.Add(c[i]);
                    faces.Add(new Face(new[] { b, b + 1, b + 2, b, b + 2, b + 3 }));
                }
                var pb = ProBuilderMesh.Create(pos, faces);
                if (bevel > 0f) BevelAll(pb, bevel);
                return Add(pb, sub);
            }

            /// <summary>Цилиндр: основание в base, ось — rot * up.</summary>
            public ProBuilderMesh Cylinder(Vector3 basePoint, float radius, float height, int sides, int sub,
                Quaternion rot, float bevel = 0f, bool smooth = false, bool bottomCap = true)
            {
                var pb = ShapeGenerator.GenerateCylinder(PivotLocation.FirstVertex, sides, radius, height, 0, smooth ? 1 : -1);
                // FirstVertex — неудобный пивот: переносим в центр основания.
                Bounds b = Bounds(pb);
                Vector3 shift = new Vector3(-b.center.x, -b.min.y, -b.center.z);
                pb.positions = pb.positions.Select(p => basePoint + rot * (p + shift)).ToArray();
                if (!bottomCap)
                {
                    // нижняя крышка не видна (лежит на поверхности) — убираем ради бюджета
                    Vector3 down = rot * Vector3.down;
                    var hidden = pb.faces.Where(f => Vector3.Dot(Math.Normal(pb, f), down) > 0.9f).ToList();
                    pb.DeleteFaces(hidden);
                    pb.ToMesh();
                }
                if (bevel > 0f)
                {
                    // скашиваем только рёбра крышек
                    var capEdges = new List<Edge>();
                    foreach (var f in pb.faces)
                    {
                        Vector3 n = Math.Normal(pb, f);
                        if (Mathf.Abs(Vector3.Dot(n, rot * Vector3.up)) > 0.9f) capEdges.AddRange(f.edges);
                    }
                    Bevel.BevelEdges(pb, capEdges, bevel);
                }
                pb.Refresh();
                return Add(pb, sub);
            }

            /// <summary>Труба (кольцо) с основанием в base, ось — rot * up.</summary>
            public ProBuilderMesh Pipe(Vector3 basePoint, float radius, float thickness, float height, int sides, int sub,
                Quaternion rot)
            {
                var pb = ShapeGenerator.GeneratePipe(PivotLocation.FirstVertex, radius, height, thickness, sides, 0);
                Bounds b = Bounds(pb);
                Vector3 shift = new Vector3(-b.center.x, -b.min.y, -b.center.z);
                pb.positions = pb.positions.Select(p => basePoint + rot * (p + shift)).ToArray();
                pb.Refresh();
                return Add(pb, sub);
            }

            /// <summary>Сетка, собранная вручную из квадов (см. Poly).</summary>
            public ProBuilderMesh Raw(Poly poly, int sub)
            {
                var pb = ProBuilderMesh.Create(poly.P, poly.F);
                return Add(pb, sub);
            }

            /// <summary>
            /// Запекает детали в Mesh-ассет (по submesh на материал), удаляет
            /// временные ProBuilderMesh. Существующий ассет обновляется на месте,
            /// чтобы GUID и ссылки в префабах не менялись.
            /// </summary>
            public Mesh Bake(string name, int submeshCount)
            {
                var verts = new List<Vector3>();
                var norms = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>[submeshCount];
                for (int i = 0; i < submeshCount; i++) tris[i] = new List<int>();

                foreach (var (pb, sub) in parts)
                {
                    pb.ToMesh();
                    pb.Refresh();
                    Mesh m = pb.GetComponent<MeshFilter>().sharedMesh;
                    int b = verts.Count;
                    verts.AddRange(m.vertices);
                    norms.AddRange(m.normals);
                    Vector2[] uv = m.uv;
                    uvs.AddRange(uv.Length == m.vertexCount ? uv : new Vector2[m.vertexCount]);
                    for (int s = 0; s < m.subMeshCount; s++)
                        tris[sub].AddRange(m.GetTriangles(s).Select(t => t + b));
                    Object.DestroyImmediate(m);
                    Object.DestroyImmediate(pb.gameObject);
                }
                parts.Clear();

                var mesh = new Mesh { name = name };
                mesh.indexFormat = verts.Count > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetVertices(verts);
                mesh.SetNormals(norms);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = submeshCount;
                for (int s = 0; s < submeshCount; s++) mesh.SetTriangles(tris[s], s);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                Unwrapping.GenerateSecondaryUVSet(mesh);
                return SaveMesh(mesh);
            }
        }

        /// <summary>Ручная сборка квадов; порядок вершин выравнивается по подсказке нормали.</summary>
        public class Poly
        {
            public readonly List<Vector3> P = new List<Vector3>();
            public readonly List<Face> F = new List<Face>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 hint, int smoothing = 0)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), hint) < 0f)
                {
                    var t = b; b = d; d = t;
                }
                int i = P.Count;
                P.Add(a); P.Add(b); P.Add(c); P.Add(d);
                F.Add(new Face(new[] { i, i + 1, i + 2, i, i + 2, i + 3 }) { smoothingGroup = smoothing });
            }

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 hint, int smoothing = 0)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), hint) < 0f)
                {
                    var t = b; b = c; c = t;
                }
                int i = P.Count;
                P.Add(a); P.Add(b); P.Add(c);
                F.Add(new Face(new[] { i, i + 1, i + 2 }) { smoothingGroup = smoothing });
            }

            /// <summary>
            /// Тело вращения вокруг оси Z (центр — center). profile — точки (радиус, z);
            /// нормаль сегмента (dz, −dr) должна смотреть наружу при обходе профиля.
            /// </summary>
            public void Lathe(Vector3 center, IList<Vector2> profile, int sides)
            {
                Lathe(center, profile, sides, Quaternion.identity, 0);
            }

            /// <summary>
            /// То же, но ось Z профиля поворачивается rot (например, к +Y для кружки),
            /// smoothing — группа сглаживания боковых граней. Точки с радиусом 0 дают треугольники.
            /// </summary>
            public void Lathe(Vector3 center, IList<Vector2> profile, int sides, Quaternion rot, int smoothing)
            {
                for (int s = 0; s < sides; s++)
                {
                    float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
                    var d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f);
                    var d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                    for (int j = 0; j < profile.Count - 1; j++)
                    {
                        Vector2 p = profile[j], q = profile[j + 1];
                        Vector3 a = center + rot * (d0 * p.x + Vector3.forward * p.y);
                        Vector3 b = center + rot * (d1 * p.x + Vector3.forward * p.y);
                        Vector3 c = center + rot * (d1 * q.x + Vector3.forward * q.y);
                        Vector3 d = center + rot * (d0 * q.x + Vector3.forward * q.y);
                        Vector3 radial = (d0 + d1).normalized;
                        Vector3 hint = rot * (radial * (q.y - p.y) + Vector3.forward * (p.x - q.x));
                        if (p.x < 1e-6f) Tri(a, c, d, hint, smoothing);
                        else if (q.x < 1e-6f) Tri(a, b, c, hint, smoothing);
                        else Quad(a, b, c, d, hint, smoothing);
                    }
                }
            }

            /// <summary>Трубка постоянного радиуса по ломаной; сечение — sides-угольник.</summary>
            public void Tube(IList<Vector3> path, float radius, int sides, int smoothing = 1)
            {
                var rings = new List<Vector3[]>();
                Vector3 prevU = Vector3.zero;
                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 tan = (path[Mathf.Min(path.Count - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                    Vector3 u = i == 0 ? Vector3.ProjectOnPlane(Vector3.up, tan) : Vector3.ProjectOnPlane(prevU, tan);
                    if (u.sqrMagnitude < 1e-8f) u = Vector3.ProjectOnPlane(Vector3.right, tan);
                    u.Normalize();
                    prevU = u;
                    Vector3 v = Vector3.Cross(tan, u);
                    var ring = new Vector3[sides];
                    for (int k = 0; k < sides; k++)
                    {
                        float a = k * Mathf.PI * 2f / sides;
                        ring[k] = path[i] + radius * (Mathf.Cos(a) * u + Mathf.Sin(a) * v);
                    }
                    rings.Add(ring);
                }
                for (int i = 0; i < path.Count - 1; i++)
                    for (int k = 0; k < sides; k++)
                    {
                        int k1 = (k + 1) % sides;
                        Vector3 a = rings[i][k], b = rings[i][k1], c = rings[i + 1][k1], d = rings[i + 1][k];
                        Vector3 mid = (path[i] + path[i + 1]) * 0.5f;
                        Quad(a, b, c, d, (a + b + c + d) * 0.25f - mid, smoothing);
                    }
            }

            /// <summary>
            /// Поверхность-сетка nx × nz над прямоугольником [x0..x1]×[z0..z1];
            /// pos(u, v) возвращает точку по нормированным координатам 0..1. Нормаль — по hint.
            /// </summary>
            public void Grid(int nx, int nz, System.Func<float, float, Vector3> pos, Vector3 hint, int smoothing = 1)
            {
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < nz; j++)
                    {
                        float u0 = i / (float)nx, u1 = (i + 1) / (float)nx;
                        float v0 = j / (float)nz, v1 = (j + 1) / (float)nz;
                        Quad(pos(u0, v0), pos(u1, v0), pos(u1, v1), pos(u0, v1), hint, smoothing);
                    }
            }
        }

        /// <summary>Сглаженная кривая Catmull-Rom через опорные точки, seg отрезков на участок.</summary>
        public static List<Vector3> Spline(IList<Vector3> pts, int seg)
        {
            var res = new List<Vector3>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Count - 1, i + 2)];
                for (int s = 0; s < seg; s++)
                {
                    float t = s / (float)seg, t2 = t * t, t3 = t2 * t;
                    res.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            res.Add(pts[pts.Count - 1]);
            return res;
        }

        static Bounds Bounds(ProBuilderMesh pb)
        {
            var ps = pb.positions;
            var b = new Bounds(ps[0], Vector3.zero);
            foreach (var p in ps) b.Encapsulate(p);
            return b;
        }

        static void BevelAll(ProBuilderMesh pb, float amount)
        {
            var edges = pb.faces.SelectMany(f => f.edges).ToList();
            Bevel.BevelEdges(pb, edges, amount);
            pb.Refresh();
        }

        static Mesh SaveMesh(Mesh mesh)
        {
            if (!AssetDatabase.IsValidFolder(ModelsDir))
                AssetDatabase.CreateFolder("Assets/VOID/Art", "Models");
            string path = ModelsDir + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                existing = mesh;
            }
            else
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = mesh.name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
            }
            AssetDatabase.SaveAssets();
            return existing;
        }

        public static int Triangles(Mesh m)
        {
            int n = 0;
            for (int s = 0; s < m.subMeshCount; s++) n += (int)m.GetIndexCount(s) / 3;
            return n;
        }

        public static Material Mat(string name)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/" + name + ".mat");
            if (m == null) Debug.LogError("[VOID] нет материала " + name);
            return m;
        }

        /// <summary>
        /// MAT_Glass — прозрачный Standard (режим Transparent) для стёкол окна
        /// и стакана. Создаётся при первом вызове.
        /// </summary>
        public static Material GlassMat()
        {
            string path = MaterialsDir + "/MAT_Glass.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;

            m = new Material(Shader.Find("Standard")) { name = "MAT_Glass" };
            m.SetColor("_Color", new Color(0.78f, 0.86f, 0.9f, 0.16f));
            m.SetFloat("_Glossiness", 0.95f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ------------------------------------------------------------ префабы

        /// <summary>Открывает префаб, отдаёт корень в правку и сохраняет.</summary>
        public static void EditPrefab(string prefabName, System.Action<GameObject> edit)
        {
            string path = PrefabsDir + "/" + prefabName + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Делает у parent дочерний объект name с мешем и материалами.
        /// Старые greybox-дети этого объекта удаляются.
        /// </summary>
        public static GameObject SetMeshChild(Transform parent, string name, Mesh mesh, params Material[] mats)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
            }
            for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null) mf = t.gameObject.AddComponent<MeshFilter>();
            var mr = t.GetComponent<MeshRenderer>();
            if (mr == null) mr = t.gameObject.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh;
            mr.sharedMaterials = mats;
            return t.gameObject;
        }

        public static void FitBox(BoxCollider bc, Mesh mesh, Transform meshT, Transform colT)
        {
            Bounds b = mesh.bounds;
            Matrix4x4 m = colT.worldToLocalMatrix * meshT.localToWorldMatrix;
            var nb = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                nb.Encapsulate(m.MultiplyPoint3x4(p));
            }
            bc.center = nb.center;
            bc.size = nb.size;
        }

        public static void Log(string model, params Mesh[] meshes)
        {
            Debug.Log("[VOID] " + model + ": " + string.Join(", ",
                meshes.Select(m => m.name + " " + Triangles(m) + " tris, " + m.bounds.size.ToString("F3"))));
        }
    }
}
