using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VOID.EnvModels
{
    /// <summary>
    /// Провода на столе: от каждого монитора сигнальный и сетевой кабель уходят
    /// по столешнице к задней кромке, свешиваются за столом и ложатся на пол;
    /// шнур мыши тянется к подставке терминала. Пивот PRB_Cables — центр
    /// столешницы (как у Desk, но на высоте её верха), поэтому координаты
    /// мониторов и мыши ниже — те же, что в Env_Room относительно стола.
    /// Коллайдера нет: тонкие провода не должны перехватывать прицел.
    /// </summary>
    public static class ModelCables
    {
        const float TopY = 0.74f;       // верх стола — пивот Cables
        const float EdgeZ = -0.375f;    // задняя кромка столешницы
        const float FloorY = -TopY;
        const float DropZ = -0.39f;     // между столом и стеной (стена в −0.405)
        const float DropX = 0.63f;      // спуск — в стороне от батареи и её подводок (|x| ≤ 0.573)

        // расстановка относительно стола (Desk стоит в (0, 0, −1.58) без поворота)
        public static readonly Vector3 DeskPos = new Vector3(0f, 0f, -1.58f);
        static readonly Vector3 TerminalPos = new Vector3(0.38f, 0f, -0.04f);
        static readonly Vector3 ChatPos = new Vector3(-0.38f, 0f, -0.04f);
        const float TerminalYaw = -10f, ChatYaw = 10f;
        // мышь и её шнур лежат на стопке Paper_01/Paper_02 (верх листов на 4 мм выше стола)
        const float PaperTop = 0.004f;
        public static readonly Vector3 MousePos = new Vector3(0.33f, PaperTop, 0.27f);
        public const float MouseYaw = -6f;

        [MenuItem("VOID/Env/Models/Cables")]
        public static void Build()
        {
            var b = new PBKit.Builder();
            var p = new PBKit.Poly();

            MonitorCable(p, TerminalPos, TerminalYaw, 0.025f, DropX, 0.0035f);
            MonitorCable(p, TerminalPos, TerminalYaw, 0.06f, DropX + 0.025f, 0.003f);
            MonitorCable(p, ChatPos, ChatYaw, -0.025f, -DropX, 0.0035f);
            MonitorCable(p, ChatPos, ChatYaw, -0.06f, -DropX - 0.025f, 0.003f);
            MouseCord(p);
            b.Raw(p, 0);

            Mesh mesh = b.Bake("SM_Cables", 1);
            PBKit.Log("Cables", mesh);

            PBKit.EnsurePrefab("PRB_Cables", false);
            PBKit.EditPrefab("PRB_Cables", root =>
            {
                PBKit.SetMeshChild(root.transform, "Mesh", mesh, PBKit.Mat("MAT_Plastic"));
            });
        }

        static Vector3 At(Vector3 pos, float yaw, Vector3 local)
        {
            return pos + Quaternion.Euler(0f, yaw, 0f) * local;
        }

        /// <summary>Кабель из задней панели CRT (x0 — смещение по панели) до пола за столом.</summary>
        static void MonitorCable(PBKit.Poly p, Vector3 mon, float yaw, float x0, float dropX, float r)
        {
            float side = Mathf.Sign(dropX);
            Vector3 onDesk = At(mon, yaw, new Vector3(x0, r, -0.27f));
            var pts = new List<Vector3>
            {
                // внутри корпуса → наружу из задней панели (z ≈ −0.19) → вниз на стол
                At(mon, yaw, new Vector3(x0, 0.17f, -0.16f)),
                At(mon, yaw, new Vector3(x0, 0.165f, -0.21f)),
                At(mon, yaw, new Vector3(x0, 0.105f, -0.237f)),
                At(mon, yaw, new Vector3(x0, 0.025f, -0.25f)),
                onDesk,
                // по столу к задней кромке и через неё
                new Vector3(Mathf.Lerp(onDesk.x, dropX, 0.55f), r, -0.335f),
                new Vector3(dropX, r, EdgeZ + 0.003f),
                new Vector3(dropX, -0.006f, EdgeZ - 0.0075f),
                // свободно висит за столом и ложится на пол вдоль стены
                new Vector3(dropX, -0.12f, DropZ),
                new Vector3(dropX + side * 0.015f, -0.45f, DropZ),
                new Vector3(dropX + side * 0.04f, FloorY + 0.04f, DropZ + 0.003f),
                // +2 мм: сплайн чуть проседает между опорными точками, а пол — ровно в FloorY
                new Vector3(dropX + side * 0.1f, FloorY + r + 0.002f, DropZ + 0.012f),
                new Vector3(dropX + side * 0.3f, FloorY + r + 0.002f, DropZ + 0.006f),
            };
            p.Tube(PBKit.Spline(pts, 3), r, 6, 1);
        }

        /// <summary>Шнур мыши: от её передней стенки к подставке терминального монитора.</summary>
        static void MouseCord(PBKit.Poly p)
        {
            const float r = 0.0018f;
            // до подставки шнур идёт по бумаге, подставка (высотой 1 см) его и прячет
            Vector3 foot = At(TerminalPos, TerminalYaw, new Vector3(0.06f, PaperTop + r, 0.14f));
            var pts = new List<Vector3>
            {
                At(MousePos, MouseYaw, new Vector3(0f, 0.004f, -0.066f)),
                At(MousePos, MouseYaw, new Vector3(0f, r, -0.085f)),
                At(MousePos, MouseYaw, new Vector3(0.008f, r, -0.125f)),
                new Vector3(Mathf.Lerp(MousePos.x, foot.x, 0.6f) + 0.01f, PaperTop + r, Mathf.Lerp(MousePos.z - 0.13f, foot.z, 0.5f)),
                foot + new Vector3(0f, 0f, 0.02f),
                foot + new Vector3(0.003f, 0f, -0.02f),
            };
            p.Tube(PBKit.Spline(pts, 4), r, 5, 2);
        }

        /// <summary>
        /// Ставит PRB_Mouse и PRB_Cables в Env_Room под DeskItems (если их там нет)
        /// и сохраняет сцену. Office пересобирается из неё билдером.
        /// </summary>
        [MenuItem("VOID/Env/Models/Расставить мышь и кабели в Env_Room")]
        public static void PlaceInEnvRoom()
        {
            var scene = EditorSceneManager.OpenScene("Assets/VOID/Scenes/Env_Room.unity", OpenSceneMode.Single);
            Transform items = GameObject.Find("DeskItems").transform;

            Place(items, "Cables", "PRB_Cables", DeskPos + new Vector3(0f, TopY, 0f), 0f);
            Place(items, "Mouse", "PRB_Mouse", DeskPos + MousePos + new Vector3(0f, TopY, 0f), MouseYaw);

            EditorSceneManager.SaveScene(scene);
        }

        static void Place(Transform parent, string name, string prefab, Vector3 pos, float yaw)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PBKit.PrefabsDir + "/" + prefab + ".prefab");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent.gameObject.scene);
                go.name = name;
                t = go.transform;
                t.SetParent(parent, true);
            }
            t.position = pos;
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
