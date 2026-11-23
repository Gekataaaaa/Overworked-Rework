using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

namespace Overworked.EditorTools
{
    /// <summary>
    /// P3, рабочая сцена Work_P1/Floor_03_Work: столовая для персонала за кухней.
    /// На кухне справа — дверь «[E] Enter the staff canteen» (копия двери выхода кухни — тот же размер); столовая — своя комната (Room) под кухней (y −20 от неё)
    /// той же ширины, в стиле graybox (серая стена, чёрный пол, тёмный потолок), со своей камерой CM Canteen и дверью
    /// обратно на кухню. Для столовой «коридор» — это кухня: её камера и точка у двери. При выходе из столовой кухня
    /// снова становится текущей комнатой (Room.RestoreInside — без повторных сценариев входа кухни).
    /// Повторный запуск пересоздаёт столовую, её камеру и дверь на кухне. Работает в открытой сцене (сохранить — Ctrl+S).
    /// Меню Overworked → P3 → Floor_03_Work → Build Staff Canteen.
    /// </summary>
    public static class WorkCanteenBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Work_P1/Floor_03_Work.unity";
        const string RoomName = "Canteen";
        const string DoorName = "Door_Canteen";
        const string ReturnName = "CanteenReturn";
        const string CameraName = "CM Canteen";
        const string UnlitPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        // Как у кухни (FloorSkeletonBuilder / GrayboxTool): комната 16 юнитов, пол на 4.2 ниже центра.
        const float HalfWidth = 8f, FloorY = -4.2f, CeilingY = 3.6f, HalfHeight = 5.65f;
        const float BelowKitchen = 20f;      // столовая под кухней, как кухня под коридором
        const float DoorX = 6.5f;            // дверь на кухне — у правой стены, за столом с пропуском
        static readonly Color WallColor = new Color(0.45f, 0.46f, 0.45f);
        static readonly Color CeilingColor = new Color(0.18f, 0.18f, 0.19f);
        static readonly Color SideWallColor = new Color(0.15f, 0.16f, 0.15f);

        [MenuItem("Overworked/P3/Floor_03_Work/Build Staff Canteen")]
        public static void Run()
        {
            if (!EnsureScene()) return;

            var kitchenGo = GameObject.Find("Environment/Rooms/Kitchen");
            var kitchen = kitchenGo != null ? kitchenGo.GetComponent<Room>() : null;
            var kitchenExit = GameObject.Find("Environment/Rooms/Kitchen/ExitDoor");
            if (kitchen == null || kitchenExit == null || kitchenExit.transform.Find("Door") == null || kitchen.RoomCamera == null)
            {
                Debug.LogError("[Setup] Floor_03_Work: нет кухни (Room, ExitDoor/Door, камера)");
                return;
            }
            var rooms = kitchenGo.transform.parent;
            var square = PlayerSandboxBuilder.EnsureSquareSprite();
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitPath);

            // Старое — долой (повторный запуск).
            Remove(rooms.Find(RoomName));
            Remove(kitchenGo.transform.Find(DoorName));
            Remove(kitchenGo.transform.Find(ReturnName));
            Remove(kitchen.RoomCamera.transform.parent != null ? kitchen.RoomCamera.transform.parent.Find(CameraName) : null);

            // ---------------- комната
            var canteenGo = new GameObject(RoomName);
            Undo.RegisterCreatedObjectUndo(canteenGo, "Canteen");
            canteenGo.transform.SetParent(rooms, false);
            canteenGo.transform.position = kitchenGo.transform.position + Vector3.down * BelowKitchen;
            var origin = canteenGo.transform.position;

            // Стены: Дэнни не выходит за комнату.
            var walls = Child(canteenGo, "Walls", Vector3.zero);
            foreach (var x in new[] { -HalfWidth - 0.5f, HalfWidth + 0.5f })
            {
                var w = PlayerSandboxBuilder.Rect(walls, x < 0 ? "WallLeft" : "WallRight", square, new Vector2(x, 0f),
                    new Vector2(1f, HalfHeight * 2f), SideWallColor, -4);
                w.AddComponent<BoxCollider2D>();
            }

            // Границы камеры — как у кухни, по высоте больше кадра (иначе Confiner2D прибивает камеру).
            var bounds = Child(canteenGo, "CameraBounds", Vector3.zero);
            var poly = bounds.AddComponent<PolygonCollider2D>();
            poly.isTrigger = true;
            float bw = HalfWidth + 1f;
            poly.points = new[]
            {
                new Vector2(-bw, -HalfHeight), new Vector2(bw, -HalfHeight),
                new Vector2(bw, HalfHeight), new Vector2(-bw, HalfHeight),
            };

            // Серая коробка — те же полосы и цвета, что GrayboxTool (P1) ставит в кухне.
            var backdrop = Child(canteenGo, "~GrayBackdrop", Vector3.zero);
            float floorTop = origin.y + FloorY, ceiling = origin.y + CeilingY;
            Strip(backdrop, "Floor", square, unlit, origin.x - bw, origin.x + bw, origin.y - HalfHeight, floorTop, Color.black);
            Strip(backdrop, "Wall", square, unlit, origin.x - bw, origin.x + bw, floorTop, ceiling, WallColor);
            Strip(backdrop, "Ceiling", square, unlit, origin.x - bw, origin.x + bw, ceiling, origin.y + HalfHeight, CeilingColor);

            // Вход и дверь обратно на кухню — копия ExitDoor кухни (дверь, серый квадрат, подпись), слева.
            var kitchenEntry = kitchenGo.transform.Find("EntryPoint");
            var entry = Child(canteenGo, "EntryPoint",
                kitchenEntry != null ? kitchenEntry.localPosition : new Vector3(-7.5f, FloorY + 0.9f, 0f));
            var exit = (GameObject)Object.Instantiate(kitchenExit, canteenGo.transform, false);
            exit.name = "ExitDoor";
            Undo.RegisterCreatedObjectUndo(exit, "Canteen");
            var exitDoor = exit.GetComponentInChildren<DoorInteractable>(true);
            foreach (var label in exit.GetComponentsInChildren<TMPro.TMP_Text>(true)) label.text = "KITCHEN";

            // ---------------- камера столовой — копия камеры кухни со своими границами
            var cam = Object.Instantiate(kitchen.RoomCamera, kitchen.RoomCamera.transform.parent);
            cam.name = CameraName;
            Undo.RegisterCreatedObjectUndo(cam, "Canteen");
            cam.transform.position = kitchen.RoomCamera.transform.position + Vector3.down * BelowKitchen;
            var confiner = cam.GetComponent<CinemachineConfiner2D>();
            if (confiner != null) confiner.BoundingShape2D = poly;
            cam.SetActive(false);

            // ---------------- кухня: точка у двери и сама дверь в столовую (копия двери коридора на кухню)
            // Дверь в столовую — копия двери выхода кухни (ExitDoor/Door): тот же размер и высота от пола,
            // что у входа в кухню изнутри. Отличается только место (справа) и направление (в комнату).
            var kitchenExitDoor = kitchenExit.transform.Find("Door");
            var ret = Child(kitchenGo, ReturnName,
                new Vector3(DoorX, kitchenEntry != null ? kitchenEntry.localPosition.y : FloorY + 0.9f, 0f));
            var door = (GameObject)Object.Instantiate(kitchenExitDoor.gameObject, kitchenGo.transform, false);
            door.name = DoorName;
            Undo.RegisterCreatedObjectUndo(door, "Canteen");
            var exitDoorLocal = kitchenGo.transform.InverseTransformPoint(kitchenExitDoor.position);
            door.transform.localPosition = new Vector3(DoorX, exitDoorLocal.y, 0f);

            // ---------------- Room столовой и связи
            var canteen = canteenGo.AddComponent<Room>();
            Set(canteen, ("entryPoint", entry.transform), ("returnPoint", ret.transform),
                ("roomCamera", cam), ("corridorCamera", kitchen.RoomCamera));
            // Вышли из столовой — снова на кухне: текущая комната кухня, без её сценариев входа.
            UnityEventTools.AddPersistentListener(canteen.onExit, new UnityAction(kitchen.RestoreInside));

            var doorIn = door.GetComponent<DoorInteractable>();
            Set(doorIn, ("room", canteen), ("requiresObjective", null), ("completesObjective", null));
            SetString(doorIn, "prompt", "Enter the staff canteen");
            // Копия двери выхода: направление меняем на «в комнату», условий и замков нет — столовая открыта.
            SetString(doorIn, "requiresFlag", "");
            SetString(doorIn, "setsFlag", "");
            var dso = new SerializedObject(doorIn);
            dso.FindProperty("direction").enumValueIndex = (int)DoorInteractable.Direction.IntoRoom;
            dso.FindProperty("startLocked").boolValue = false;
            dso.FindProperty("doorLocked").boolValue = false;
            dso.ApplyModifiedPropertiesWithoutUndo();
            if (exitDoor != null)
            {
                Set(exitDoor, ("room", canteen));
                SetString(exitDoor, "prompt", "Back to the kitchen");
            }

            EditorSceneManager.MarkSceneDirty(canteenGo.scene);
            if (Application.isBatchMode) EditorSceneManager.SaveScene(canteenGo.scene);
            Debug.Log("[Setup] Floor_03_Work: столовая для персонала за кухней (дверь на кухне справа, CM Canteen)");
        }

        // ------------------------------------------------------------ помощники

        static GameObject Child(GameObject parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            return go;
        }

        static void Strip(GameObject parent, string name, Sprite square, Material mat,
                          float xMin, float xMax, float yMin, float yMax, Color color)
        {
            if (yMax <= yMin) return;
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, true);
            go.transform.position = new Vector3((xMin + xMax) / 2f, (yMin + yMax) / 2f, 0f);
            var s = square.bounds.size;
            go.transform.localScale = new Vector3((xMax - xMin) / s.x, (yMax - yMin) / s.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = color;
            sr.sortingOrder = -100;
            if (mat != null) sr.sharedMaterial = mat;
        }

        static void Remove(Transform t)
        {
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }

        static void Set(Object target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs) so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static bool EnsureScene()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path == ScenePath) return true;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return true;
        }
    }
}
