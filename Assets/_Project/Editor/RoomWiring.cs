using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overworked.EditorTools
{
    /// <summary>
    /// Разводка комнат на этажах из каркаса P1 (FloorSkeletonBuilder): каждой Environment/Rooms/Room_X —
    /// компонент Room (точка входа, камера, точка возврата), двери Corridor/Doors/Door_Room_X — вход,
    /// Room_X/ExitDoor — выход. Остальные двери коридора — просто картинки, без E.
    /// Уже настроенные двери не перенастраиваются, так что запускать можно повторно.
    /// Меню: Overworked → P2 → All Floors → Wire Rooms On All Floors (Floor_03 кухня, Floor_02 Room_201, Floor_01 Room_101/102)
    /// и Wire Rooms In Open Scene (только открытая сцена).
    /// </summary>
    [InitializeOnLoad]
    public static class RoomWiring
    {
        static readonly string[] Floors =
        {
            "Assets/_Project/Scenes/Floor_03.unity",
            "Assets/_Project/Scenes/Floor_02.unity",
            "Assets/_Project/Scenes/Floor_01.unity",
        };

        /// <summary>
        /// Файл-запрос: если он есть при загрузке редактора, разводка всех этажей запускается сама один раз
        /// (так её можно попросить, не открывая меню). Файл удаляется сразу.
        /// </summary>
        const string RequestFile = "Temp/p2_wire_rooms.request";

        static RoomWiring()
        {
            if (!System.IO.File.Exists(RequestFile)) return;
            EditorApplication.delayCall += RunRequest;
        }

        static void RunRequest()
        {
            // В Play Mode сцены сохранять нельзя — ждём выхода в редактор.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= OnPlayMode;
                EditorApplication.playModeStateChanged += OnPlayMode;
                return;
            }
            if (!System.IO.File.Exists(RequestFile)) return;
            System.IO.File.Delete(RequestFile);
            WireAllFloors();
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            RunRequest();
        }

        [MenuItem("Overworked/P2/All Floors/Wire Rooms On All Floors")]
        public static void WireAllFloors()
        {
            // Несохранённые правки в открытой сцене — спросить, иначе OpenScene их выбросит.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = SceneManager.GetActiveScene().path;

            foreach (var path in Floors)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Wire(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            Debug.Log("[Setup] Комнаты разведены на всех этажах: Floor_03 кухня, Floor_02 Room_201, Floor_01 Room_101 и Room_102");
        }

        [MenuItem("Overworked/P2/All Floors/Wire Rooms In Open Scene")]
        public static void WireOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            Wire(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>Разводит комнаты и двери сцены.</summary>
        public static void Wire(Scene scene)
        {
            var corridor = Find(scene, "Environment/Corridor");
            var rooms = Find(scene, "Environment/Rooms");
            var cameras = Find(scene, "Cameras");
            if (corridor == null || cameras == null)
            {
                Debug.LogError($"[Setup] {scene.name}: нет Environment/Corridor или Cameras — это не каркас этажа");
                return;
            }
            var doors = corridor.transform.Find("Doors");
            var corridorCam = cameras.transform.Find("CM Corridor");
            int wired = 0;

            if (rooms != null)
            {
                foreach (Transform roomT in rooms.transform)
                {
                    // Не через ??: в редакторе GetComponent возвращает «фальшивый null», и ?? его не видит.
                    if (!roomT.TryGetComponent(out Room room)) room = roomT.gameObject.AddComponent<Room>();
                    var entry = roomT.Find("EntryPoint");
                    var roomCam = cameras.transform.Find("CM " + roomT.name);
                    var corridorDoor = doors != null ? doors.Find("Door_" + roomT.name) : null;
                    if (entry == null || roomCam == null || corridorDoor == null)
                    {
                        Debug.LogWarning($"[Setup] {scene.name}/{roomT.name}: нет EntryPoint, камеры CM {roomT.name} или двери Door_{roomT.name}");
                        continue;
                    }

                    CenterExit(roomT, entry);
                    var back = ReturnPoint(corridor.transform, roomT.name, corridorDoor.position.x, entry.localPosition.y);
                    Set(room, ("entryPoint", entry), ("returnPoint", back), ("roomCamera", roomCam.gameObject),
                        ("corridorCamera", corridorCam != null ? corridorCam.gameObject : null));

                    WireDoor(corridorDoor.gameObject, room, DoorInteractable.Direction.IntoRoom, "Enter " + Pretty(roomT.name));
                    var exit = roomT.Find("ExitDoor/Door");
                    if (exit == null) exit = roomT.Find("ExitDoor");
                    if (exit != null) WireDoor(exit.gameObject, room, DoorInteractable.Direction.OutToCorridor, "Exit");
                    wired++;
                }
            }

            // Декоративные двери (Door_<число>) — без E. Убираем взаимодействие, если его ставила старая версия.
            if (doors != null)
            {
                foreach (Transform d in doors)
                {
                    if (!Regex.IsMatch(d.name, @"^Door_-?\d+$")) continue;
                    if (d.TryGetComponent(out DoorInteractable old)) Object.DestroyImmediate(old);
                }
            }
            Debug.Log($"[Setup] {scene.name}: комнат разведено {wired}");
        }

        // Дверь выхода и её подпись — по центру комнаты (X = 0), Дэнни появляется перед ней:
        // будто вошёл через эту дверь. Высоту не трогаем.
        static void CenterExit(Transform roomT, Transform entry)
        {
            var exitRoot = roomT.Find("ExitDoor");
            if (exitRoot != null)
                foreach (Transform child in exitRoot)
                {
                    // Подпись EXIT — RectTransform (TextMeshPro): её место задаёт anchoredPosition, не localPosition.
                    if (child is RectTransform rt) rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
                    else child.localPosition = new Vector3(0f, child.localPosition.y, child.localPosition.z);
                }
            entry.localPosition = new Vector3(0f, entry.localPosition.y, entry.localPosition.z);
        }

        static void WireDoor(GameObject go, Room room, DoorInteractable.Direction direction, string prompt)
        {
            Trigger(go);
            if (go.TryGetComponent(out DoorInteractable door))
            {
                // Уже стоит: подсказку не трогаем, комнату и направление выставляем.
                // Дверь на ключ (unlockFlag задан, Room_102) не отпираем — замок поставлен сценой этажа.
                Set(door, ("room", room), ("direction", direction));
                if (string.IsNullOrEmpty(new SerializedObject(door).FindProperty("unlockFlag").stringValue))
                    Set(door, ("doorLocked", false));
                return;
            }
            Set(go.AddComponent<DoorInteractable>(), ("room", room), ("direction", direction), ("prompt", prompt));
        }

        // Точка возврата: в коридоре напротив двери, на высоте ног Дэнни (как EntryPoint относительно пола комнаты).
        static Transform ReturnPoint(Transform corridor, string roomName, float x, float y)
        {
            var parent = corridor.Find("ReturnPoints");
            if (parent == null)
            {
                parent = new GameObject("ReturnPoints").transform;
                parent.SetParent(corridor, false);
            }
            var point = parent.Find(roomName);
            if (point == null)
            {
                point = new GameObject(roomName).transform;
                point.SetParent(parent, false);
            }
            point.position = new Vector3(x, corridor.position.y + y, 0f);
            return point;
        }

        static void Trigger(GameObject go)
        {
            if (!go.TryGetComponent(out Collider2D col)) col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
        }

        // Room_101 → "room 101", Kitchen → "the kitchen".
        static string Pretty(string roomName) =>
            roomName.StartsWith("Room_") ? "room " + roomName.Substring(5) : "the " + roomName.ToLowerInvariant();

        static GameObject Find(Scene scene, string path)
        {
            var parts = path.Split('/');
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != parts[0]) continue;
                if (parts.Length == 1) return root;
                var t = root.transform.Find(string.Join("/", parts, 1, parts.Length - 1));
                if (t != null) return t.gameObject;
            }
            return null;
        }

        /// <summary>Заполняет [SerializeField]-поля по имени: ссылки, строки, bool, enum.</summary>
        internal static void Set(Object target, params (string field, object value)[] values)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            foreach (var (field, value) in values)
            {
                var p = so.FindProperty(field);
                if (p == null)
                {
                    Debug.LogError($"[Setup] {target.GetType().Name}: нет поля {field}");
                    continue;
                }
                switch (value)
                {
                    case null: p.objectReferenceValue = null; break;
                    case Object o: p.objectReferenceValue = o; break;
                    case string s: p.stringValue = s; break;
                    case bool b: p.boolValue = b; break;
                    case float f: p.floatValue = f; break;
                    case System.Enum e: p.enumValueIndex = System.Convert.ToInt32(e); break;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
