using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SB = Overworked.EditorTools.PlayerSandboxBuilder;

namespace Overworked.EditorTools
{
    /// <summary>
    /// «Серая коробка» для открытой сцены (рабочие копии вроде Work_P1/Floor_03_Work):
    /// фон и декорации прячутся (их SpriteRenderer выключаются, коллайдеры и свет остаются),
    /// а каждый спрайт объекта для взаимодействия заменяется квадратом того же размера и места.
    /// Объекты для взаимодействия — всё, на чём есть IInteractable или Inspectable (и их дочерние спрайты).
    /// Если у такого объекта нет спрайтов — квадрат по размеру его коллайдера.
    /// Вместо фона в коридоре и каждой комнате — серая стена, чёрный пол ниже горизонта, тёмно-серый потолок (Unlit).
    /// Повторный запуск безопасен: пересобирает серый фон, уже заменённое не трогает.
    /// Игрок не трогается. Отменяется через Ctrl+Z или копированием сцены заново.
    /// Запуск: открыть сцену → меню Overworked → P1 → Graybox → Graybox Open Scene.
    /// </summary>
    public static class GrayboxTool
    {
        const string GrayName = "~Graybox";
        static readonly Color InteractColor = new Color(0.78f, 0.74f, 0.62f);

        /// <summary>
        /// Без вступления: сценариям, которые запускаются сами при старте сцены (1.0 Opening — Дэнни сидит, реплики,
        /// HUD по частям, подсказка управления), выключается playOnStart. Сцена стартует с управлением сразу.
        /// Остальные сценарии (зоны, турникет) не трогаются. Отмена — Ctrl+Z.
        /// </summary>
        [MenuItem("Overworked/P1/Scene Setup/Skip Opening In Open Scene")]
        public static void SkipOpening()
        {
            var scene = EditorSceneManager.GetActiveScene();
            int count = 0;
            foreach (var seq in Object.FindObjectsByType<ScriptedSequence>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(seq);
                var prop = so.FindProperty("playOnStart");
                if (prop == null || !prop.boolValue) continue;
                Undo.RecordObject(seq, "Skip Opening");
                prop.boolValue = false;
                so.ApplyModifiedProperties();
                Debug.Log($"[Graybox] {scene.name}: «{seq.name}» больше не запускается на старте");
                count++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (count == 0) Debug.Log($"[Graybox] {scene.name}: сценариев на старте нет");
        }

        /// <summary>
        /// Удлиняет коридор этажа в 3 раза вперёд (вправо): правая стена и граница камеры уезжают, а всё, что стояло
        /// в конце коридора (турникет, лестница, лампы у них), переносится в новый конец. Начало коридора, кухня и
        /// объекты в середине остаются на месте. Серый фон пересобирается под новую длину. Отмена — Ctrl+Z.
        /// </summary>
        [MenuItem("Overworked/P1/Graybox/Extend Corridor x3 In Open Scene")]
        public static void ExtendCorridor()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var corridor = GameObject.Find("Environment/Corridor");
            var bounds = corridor != null ? corridor.transform.Find("CameraBounds")?.GetComponent<PolygonCollider2D>() : null;
            var walls = corridor != null ? corridor.transform.Find("Walls") : null;
            Transform left = null, right = null;
            if (walls != null)
                foreach (Transform w in walls)
                {
                    if (w.name.Contains("Left")) left = w;
                    if (w.name.Contains("Right")) right = w;
                }
            if (bounds == null || left == null || right == null)
            {
                Debug.LogWarning($"[Graybox] {scene.name}: нет Environment/Corridor с CameraBounds и Walls/WallLeft, WallRight");
                return;
            }

            Undo.SetCurrentGroupName("Extend Corridor");
            int group = Undo.GetCurrentGroup();

            float length = right.position.x - left.position.x;
            float delta = length * 2f;                 // было L, станет 3L
            float endZone = right.position.x - 8f;     // всё, что правее, — «конец коридора»

            // 1. Правая стена и граница камеры.
            Undo.RecordObject(right, "Extend Corridor");
            right.position += Vector3.right * delta;
            Undo.RecordObject(bounds, "Extend Corridor");
            float maxLocal = float.MinValue;
            foreach (var p in bounds.points) maxLocal = Mathf.Max(maxLocal, p.x);
            var pts = bounds.points;
            for (int i = 0; i < pts.Length; i++)
                if (pts[i].x >= maxLocal - 0.01f) pts[i].x += delta / bounds.transform.lossyScale.x;
            bounds.points = pts;

            // 2. Конец коридора — в новый конец: турникет, лестница, лампы и прочее справа, кроме стен и фона.
            int moved = 0;
            var toMove = new List<Transform>();
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if ((mb is TurnstileInteractable || mb is StairInteractable) && mb.gameObject.scene == scene)
                    toMove.Add(mb.transform);
            var lights = corridor.transform.Find("Lights");
            if (lights != null)
                foreach (Transform l in lights)
                    if (l.position.x > endZone) toMove.Add(l);
            foreach (var t in toMove)
            {
                Undo.RecordObject(t, "Extend Corridor");
                t.position += Vector3.right * delta;
                moved++;
            }

            // 3. Серый фон под новую длину.
            BuildBackdrops(SB.EnsureSquareSprite());

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Graybox] {scene.name}: коридор удлинён ×3 (+{delta:0.#} юнитов вправо), перенесено в конец: {moved}");
        }

        static readonly Color PassageColor = new Color(0.06f, 0.06f, 0.07f);

        /// <summary>
        /// Второй коридор: проход шириной в две двери (тёмный прямоугольник) в центре основного коридора ведёт
        /// в пустой коридор длиной 2/3 основного, который стоит в сцене над этажом. Обратно — такой же проход
        /// в начале нового коридора (у левой стены), Дэнни появляется перед ним. Работает на механике комнат P2 (Room + DoorInteractable): затемнение,
        /// телепорт, своя камера с границами. Повторный запуск пересоздаёт второй коридор. Отмена — Ctrl+Z.
        /// </summary>
        [MenuItem("Overworked/P1/Graybox/Add Side Corridor In Open Scene")]
        public static void AddSideCorridor() => AddSide("Corridor_B", 2f / 3f, 0.5f, 1);

        /// <summary>
        /// Третий коридор: как второй, но длиной 1/3 основного, проход — на 3/4 основного коридора
        /// (между центром и турникетом), в сцене стоит над вторым коридором.
        /// </summary>
        [MenuItem("Overworked/P1/Graybox/Add Third Corridor In Open Scene")]
        public static void AddThirdCorridor() => AddSide("Corridor_C", 1f / 3f, 0.75f, 2);

        /// <param name="sideName">имя коридора (корень в Rooms, камера "CM имя", проход "Door_имя")</param>
        /// <param name="lengthFraction">длина относительно основного коридора</param>
        /// <param name="passageAt">где проход в основном коридоре: 0 — левая стена, 1 — правая</param>
        /// <param name="level">какой по счёту над этажом (1 — сразу над ним)</param>
        static void AddSide(string sideName, float lengthFraction, float passageAt, int level)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var corridor = GameObject.Find("Environment/Corridor");
            var rooms = GameObject.Find("Environment/Rooms");
            var cameras = GameObject.Find("Cameras");
            var corridorCam = cameras != null ? cameras.transform.Find("CM Corridor") : null;
            var mainBounds = corridor != null ? corridor.transform.Find("CameraBounds")?.GetComponent<PolygonCollider2D>() : null;
            var walls = corridor != null ? corridor.transform.Find("Walls") : null;
            Transform left = null, right = null;
            if (walls != null)
                foreach (Transform w in walls)
                {
                    if (w.name.Contains("Left")) left = w;
                    if (w.name.Contains("Right")) right = w;
                }
            if (corridor == null || rooms == null || corridorCam == null || mainBounds == null || left == null || right == null)
            {
                Debug.LogWarning($"[Graybox] {scene.name}: нужен каркас этажа (Environment/Corridor, Rooms, Cameras/CM Corridor, стены)");
                return;
            }

            Undo.SetCurrentGroupName("Add " + sideName);
            int group = Undo.GetCurrentGroup();
            var square = SB.EnsureSquareSprite();
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitPath);

            // Старый второй коридор, его камеру, проход и точку возврата — убираем (пересборка).
            foreach (var old in new[] { rooms.transform.Find(sideName), cameras.transform.Find("CM " + sideName),
                         corridor.transform.Find("Doors/Door_" + sideName), corridor.transform.Find("ReturnPoints/" + sideName) })
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            float mainLength = right.position.x - left.position.x;
            float centerX = left.position.x + mainLength * passageAt;   // x прохода в основном коридоре
            float halfB = mainLength * lengthFraction / 2f;
            float bottom = float.MaxValue, top = float.MinValue;
            foreach (var p in mainBounds.points) { bottom = Mathf.Min(bottom, p.y); top = Mathf.Max(top, p.y); }
            // В сцене все коридоры выровнены по левой стене: левая стена нового — над левой стеной основного.
            float rootX = left.position.x + halfB + 0.5f;
            float originY = corridor.transform.position.y + level * ((top - bottom) + 10f);   // над этажом, с запасом

            // 1. Сам коридор: корень-«комната», стены, границы камеры, точка появления.
            var root = new GameObject(sideName);
            Undo.RegisterCreatedObjectUndo(root, "Add " + sideName);
            root.transform.SetParent(rooms.transform, false);
            root.transform.position = new Vector3(rootX, originY, 0f);

            var wallsB = new GameObject("Walls");
            wallsB.transform.SetParent(root.transform, false);
            foreach (var (name, x) in new[] { ("WallLeft", -halfB - 0.5f), ("WallRight", halfB + 0.5f) })
            {
                var w = new GameObject(name);
                w.transform.SetParent(wallsB.transform, false);
                w.transform.localPosition = new Vector3(x, 0f, 0f);
                w.AddComponent<BoxCollider2D>().size = new Vector2(1f, top - bottom);
            }

            var boundsGo = new GameObject("CameraBounds");
            boundsGo.transform.SetParent(root.transform, false);
            var bounds = boundsGo.AddComponent<PolygonCollider2D>();
            bounds.isTrigger = true;
            float bx = halfB + 1f;
            bounds.points = new[] { new Vector2(-bx, bottom), new Vector2(bx, bottom), new Vector2(bx, top), new Vector2(-bx, top) };

            // Вход и обратный проход — в начале нового коридора (у левой стены): Дэнни появляется перед проходом.
            float startX = -halfB + 2.5f;
            var entry = new GameObject("EntryPoint").transform;
            entry.SetParent(root.transform, false);
            entry.localPosition = new Vector3(startX, FloorY + 0.9f, 0f);

            // 2. Камера коридора B — как у комнат: префаб камеры игрока, свои границы, выключена до входа.
            var camPrefab = SB.EnsureCameraPrefab();
            var cam = (GameObject)PrefabUtility.InstantiatePrefab(camPrefab, scene);
            Undo.RegisterCreatedObjectUndo(cam, "Add " + sideName);
            cam.name = "CM " + sideName;
            cam.transform.SetParent(cameras.transform, false);
            cam.transform.position = new Vector3(rootX, originY, cam.transform.position.z);
            cam.GetComponent<Unity.Cinemachine.CinemachineConfiner2D>().BoundingShape2D = bounds;
            cam.SetActive(false);

            // 3. Room + точка возврата в основном коридоре.
            var back = new GameObject(sideName).transform;
            var returnParent = corridor.transform.Find("ReturnPoints");
            if (returnParent == null)
            {
                returnParent = new GameObject("ReturnPoints").transform;
                returnParent.SetParent(corridor.transform, false);
            }
            back.SetParent(returnParent, false);
            back.position = new Vector3(centerX, corridor.transform.position.y + FloorY + 0.9f, 0f);
            Undo.RegisterCreatedObjectUndo(back.gameObject, "Add " + sideName);

            var room = root.AddComponent<Room>();
            RoomWiring.Set(room, ("entryPoint", entry), ("returnPoint", back), ("roomCamera", cam), ("corridorCamera", corridorCam.gameObject));

            // 4. Проходы: туда — в центре основного коридора, обратно — в начале нового.
            // Размер — по двери в кухню: та же высота, ширина в две такие двери.
            var size = KitchenDoorSize();
            var passageSize = new Vector2(size.x * 2f, size.y);
            var doorsParent = corridor.transform.Find("Doors");
            Passage(doorsParent != null ? doorsParent : corridor.transform, "Door_" + sideName,
                new Vector3(centerX, corridor.transform.position.y + FloorY, 0f), room,
                DoorInteractable.Direction.IntoRoom, "Go through", passageSize, square, unlit);
            Passage(root.transform, "ExitPassage", new Vector3(rootX + startX, originY + FloorY, 0f), room,
                DoorInteractable.Direction.OutToCorridor, "Go back", passageSize, square, unlit);

            // 5. Серый фон — и в новом коридоре.
            BuildBackdrops(square);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Graybox] {scene.name}: коридор {sideName} ({halfB * 2f:0.#} юнитов) над этажом, проход в основном коридоре x={centerX:0.#}");
        }

        /// <summary>
        /// Размер двери в кухню по её спрайтам (в серой коробке — по квадрату). Нет кухни — дверь каркаса 1.4 × 2.5.
        /// Высота — от пола (FloorY) до верха двери: проход тоже стоит на полу.
        /// </summary>
        static Vector2 KitchenDoorSize()
        {
            foreach (var door in Object.FindObjectsByType<DoorInteractable>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(door);
                var room = so.FindProperty("room").objectReferenceValue as Component;
                if (room == null || !room.name.Contains("Kitchen") ||
                    so.FindProperty("direction").enumValueIndex != (int)DoorInteractable.Direction.IntoRoom) continue;
                bool any = false;
                var b = new Bounds();
                foreach (var sr in door.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr.sprite == null) continue;
                    if (!any) { b = sr.bounds; any = true; } else b.Encapsulate(sr.bounds);
                }
                if (any) return new Vector2(b.size.x, b.max.y - (door.transform.root.position.y + FloorY));
            }
            return new Vector2(1.4f, 2.5f);
        }

        /// <summary>Проход шириной в две двери: тёмный прямоугольник от пола, триггер, DoorInteractable.</summary>
        static void Passage(Transform parent, string name, Vector3 floorPos, Room room, DoorInteractable.Direction dir,
                            string prompt, Vector2 size, Sprite square, Material unlit)
        {
            float width = size.x, height = size.y;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Add Side Corridor");
            go.transform.SetParent(parent, true);
            go.transform.position = floorPos + Vector3.up * (height / 2f);

            var visual = new GameObject("~Graybox");
            visual.transform.SetParent(go.transform, false);
            var s = square.bounds.size;
            visual.transform.localScale = new Vector3(width / s.x, height / s.y, 1f);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = PassageColor;
            sr.sortingOrder = -8;
            if (unlit != null) sr.sharedMaterial = unlit;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(width, height);
            var door = go.AddComponent<DoorInteractable>();
            RoomWiring.Set(door, ("room", room), ("direction", dir), ("prompt", prompt));
        }

        // Компоненты сюжета: сценарии, зоны-триггеры, галлюцинации, вступление HUD.
        static readonly HashSet<string> StoryComponents = new HashSet<string>
        {
            "ScriptedSequence", "SequenceTrigger", "RoomSequenceTrigger", "FlashlightSequenceTrigger",
            "NoReturnTrigger", "HudOnboarding", "ObjectiveTrigger", "ObjectiveGiver", "MessageTrigger",
            "HallucinationTrigger", "HallucinationEvent", "SilhouetteBehaviour", "Inspectable",
        };

        /// <summary>
        /// Голая сцена: убирает весь сюжет — сценарии, зоны-триггеры, сообщения в телефон, реплики и описания,
        /// галлюцинации. Остаются интерактивные объекты (двери, пропуск, записка, турникет, лестница) и HUD.
        /// У объектов вычищаются ссылки на реплики/сообщения и подписки на сценарии, снимаются замки и условия
        /// по заданиям (их раньше открывал сюжет) — всё доступно сразу. Отмена — Ctrl+Z.
        /// </summary>
        [MenuItem("Overworked/P1/Scene Setup/Strip Story In Open Scene")]
        public static void StripStory()
        {
            var scene = EditorSceneManager.GetActiveScene();
            Undo.SetCurrentGroupName("Strip Story");
            int group = Undo.GetCurrentGroup();
            int removed = 0, cleaned = 0;

            // 1. Компоненты сюжета — удаляем (выключенный скрипт всё равно получает OnTriggerEnter2D).
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (mb == null || mb.gameObject.scene != scene || !StoryComponents.Contains(mb.GetType().Name)) continue;
                Undo.DestroyObjectImmediate(mb);
                removed++;
            }
            // Пустые группы сюжета прячем целиком (силуэты, лампы галлюцинаций).
            foreach (var root in scene.GetRootGameObjects())
                if ((root.name == "Story" || root.name == "Hallucinations") && root.activeSelf)
                {
                    Undo.RecordObject(root, "Strip Story");
                    root.SetActive(false);
                }

            // 2. Интерактивные объекты: без реплик и сообщений, без сюжетных замков.
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (mb == null || mb.gameObject.scene != scene) continue;
                if (!(mb is IInteractable)) continue;
                var so = new SerializedObject(mb);
                bool changed = false;
                var calls = new List<string>();
                var it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType == SerializedPropertyType.ObjectReference &&
                        (it.objectReferenceValue is DialogueData || it.objectReferenceValue is MessageData))
                    {
                        it.objectReferenceValue = null;
                        changed = true;
                    }
                    if (it.isArray && it.propertyPath.EndsWith("m_PersistentCalls.m_Calls")) calls.Add(it.propertyPath);
                }
                // Подписки событий (onDenied турникета и т. п.) на удалённые сценарии — пустые, выкидываем.
                foreach (var path in calls)
                {
                    var arr = so.FindProperty(path);
                    for (int i = arr.arraySize - 1; i >= 0; i--)
                    {
                        var target = arr.GetArrayElementAtIndex(i).FindPropertyRelative("m_Target");
                        if (target != null && target.objectReferenceValue == null)
                        {
                            arr.DeleteArrayElementAtIndex(i);
                            changed = true;
                        }
                    }
                }
                foreach (var name in new[] { "startLocked", "doorLocked" })
                {
                    var p = so.FindProperty(name);
                    if (p != null && p.boolValue) { p.boolValue = false; changed = true; }
                }
                var req = so.FindProperty("requiresObjective");
                if (req != null && req.objectReferenceValue != null) { req.objectReferenceValue = null; changed = true; }
                if (changed) { Undo.RecordObject(mb, "Strip Story"); so.ApplyModifiedProperties(); cleaned++; }
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Graybox] {scene.name}: сюжет убран — компонентов удалено {removed}, объектов очищено {cleaned}");
        }

        /// <summary>
        /// Бег со стаминой (Shift) на игроке открытой сцены: allowRun + материал полоски (Unlit — видна в темноте).
        /// Пока механика не утверждена — только в рабочих сценах. Переопределение префаба Player в этой сцене.
        /// </summary>
        [MenuItem("Overworked/P1/Scene Setup/Enable Running In Open Scene")]
        public static void EnableRunning()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (player == null) { Debug.LogWarning($"[Graybox] {scene.name}: нет PlayerController"); return; }
            var so = new SerializedObject(player);
            Undo.RecordObject(player, "Enable Running");
            so.FindProperty("allowRun").boolValue = true;
            so.FindProperty("staminaHudMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(UnlitPath);
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Graybox] {scene.name}: бег на Shift со стаминой включён");
        }

        [MenuItem("Overworked/P1/Graybox/Graybox Open Scene")]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path.EndsWith("/Floor_03.unity") || scene.path.EndsWith("/Floor_02.unity") ||
                scene.path.EndsWith("/Floor_01.unity") || scene.path.EndsWith("/WhiteRoom.unity"))
            {
                // Основные сцены общие — серую коробку делаем только в рабочих копиях.
                if (!EditorUtility.DisplayDialog("Graybox", $"Это основная сцена {scene.name}. Всё равно сделать серую коробку?",
                        "Да", "Отмена")) return;
            }

            var square = SB.EnsureSquareSprite();
            Undo.SetCurrentGroupName("Graybox Open Scene");
            int group = Undo.GetCurrentGroup();

            // 1. Корни объектов для взаимодействия.
            var interactRoots = new HashSet<GameObject>();
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (mb is IInteractable || mb is Inspectable) interactRoots.Add(mb.gameObject);

            int hidden = 0, boxes = 0;
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
            {
                if (IsGraybox(sr.transform)) continue;   // наши квадраты и серый фон при повторном запуске
                if (sr.GetComponentInParent<PlayerController>(true) != null) continue;
                bool interactive = IsInside(sr.transform, interactRoots);
                if (!sr.enabled) continue;
                // Фон — только то, что в Environment (стены, мебель, лампы, комнаты). Силуэты галлюцинаций и прочее не трогаем.
                if (!interactive && sr.transform.root.name != "Environment") continue;

                Undo.RecordObject(sr, "Graybox");
                sr.enabled = false;
                if (!interactive) { hidden++; continue; }
                AddBox(sr.transform, LocalBounds(sr), sr.sortingLayerID, sr.sortingOrder, square);
                boxes++;
            }

            // 2. Объекты для взаимодействия без спрайтов — квадрат по коллайдеру.
            foreach (var root in interactRoots)
            {
                // Зона описания (Inspectable без E) — это область подхода, а не предмет: квадрат не нужен.
                if (root.GetComponent<IInteractable>() == null) continue;
                if (root.GetComponentInChildren<SpriteRenderer>(true) != null) continue;
                var col = root.GetComponent<Collider2D>();
                if (col == null) continue;
                var b = new Bounds(root.transform.InverseTransformPoint(col.bounds.center),
                    Divide(col.bounds.size, root.transform.lossyScale));
                AddBox(root.transform, b, 0, 5, square);
                boxes++;
            }

            // 3. Свой простой фон вместо спрятанного: серая стена, чёрный пол ниже горизонта, тёмно-серый потолок.
            int areas = BuildBackdrops(square);

            // 4. Мебель, без которой объект «висит в воздухе»: записка Чарльза — на мониторе, монитор — на столе.
            BuildCharlesDesk(square);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Graybox] {scene.name}: фон спрятан ({hidden} спрайтов), квадратов для взаимодействия: {boxes}, " +
                      $"серый фон в зонах: {areas}");
        }

        // ------------------------------------------------------------ лампы

        const string LampsName = "~GrayLamps";
        const string LitPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
        const float LampSpacing = 16f;        // шаг ламп по коридору
        const int FaultyEvery = 3;             // примерно каждая третья лампа неисправна
        const float AmbientIntensity = 0.3f;   // общий свет сцены (Global Light 2D): между лампами полумрак
        // Стены в коридорах с лампами — темнее обычной серой коробки, чтобы пятна света читались.
        static readonly Color LitWallColor = new Color(0.3f, 0.31f, 0.3f);
        static readonly Color LampColor = new Color(0.9f, 0.95f, 0.86f);
        static readonly Color TubeColor = new Color(0.86f, 0.9f, 0.84f);
        const float TubeWidth = 1.6f;          // ширина трубки = ширина начала конуса света
        const float LampInnerAngle = 60f;      // яркая часть конуса (градусы), края — до 140°

        /// <summary>
        /// Лампы в основном коридоре и боковых (Rooms/Corridor_*): под потолком светлая трубка, под ней мягкий
        /// Light2D — конус света от трубки вниз до пола, соседние не сливаются. Примерно каждая третья — неисправная:
        /// раз в 2–6 с коротко мигает очередью (LightFlicker.idleBlinkEvery), трубка темнеет вместе со светом (LampLight).
        /// Чтобы свет был виден, серый фон и квадраты в этих коридорах переводятся на Lit-материал, общий свет
        /// сцены приглушается до AmbientIntensity, стены темнеют до LitWallColor, старые лампы с арта (Corridor/Lights) выключаются.
        /// Повторный запуск пересобирает лампы. Отмена — Ctrl+Z.
        /// </summary>
        [MenuItem("Overworked/P1/Graybox/Graybox Lamps In Open Scene")]
        public static void AddLamps()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var corridor = GameObject.Find("Environment/Corridor");
            var rooms = GameObject.Find("Environment/Rooms");
            if (corridor == null)
            {
                Debug.LogWarning($"[Graybox] {scene.name}: нет Environment/Corridor");
                return;
            }
            Undo.SetCurrentGroupName("Graybox Lamps");
            int undoGroup = Undo.GetCurrentGroup();
            var square = SB.EnsureSquareSprite();
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitPath);
            var lit = AssetDatabase.LoadAssetAtPath<Material>(LitPath);

            // Зоны с лампами: основной коридор и боковые коридоры.
            var areas = new List<Transform> { corridor.transform };
            if (rooms != null)
                foreach (Transform r in rooms.transform)
                    if (r.name.StartsWith("Corridor_")) areas.Add(r);

            // Старые лампы с арта — выключить (их картинки и так спрятаны серой коробкой).
            var artLights = corridor.transform.Find("Lights");
            if (artLights != null && artLights.gameObject.activeSelf)
            {
                Undo.RecordObject(artLights.gameObject, "Graybox Lamps");
                artLights.gameObject.SetActive(false);
            }

            var litBounds = new List<Bounds>();
            int lamps = 0, faulty = 0;
            foreach (var area in areas)
            {
                var col = area.Find("CameraBounds")?.GetComponent<Collider2D>();
                if (col == null) continue;
                var old = area.Find(LampsName);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

                var b = col.bounds;
                litBounds.Add(b);
                var group = new GameObject(LampsName);
                Undo.RegisterCreatedObjectUndo(group, "Graybox Lamps");
                group.transform.SetParent(area, false);

                // По всей длине с отступом от краёв, лампы примерно через LampSpacing.
                float xMin = b.min.x + 3f, xMax = b.max.x - 3f;
                int count = Mathf.Max(1, Mathf.RoundToInt((xMax - xMin) / LampSpacing) + 1);
                // Своё смещение неисправных ламп в каждом коридоре; при пересборке раскладка та же.
                int firstFaulty = (area.name.Length * 7 + count) % FaultyEvery;
                for (int i = 0; i < count; i++)
                {
                    float x = count == 1 ? (xMin + xMax) / 2f : Mathf.Lerp(xMin, xMax, i / (float)(count - 1));
                    bool broken = i % FaultyEvery == firstFaulty;
                    BuildLamp(group.transform, $"Lamp_{i + 1}", x, area.position.y, broken, square, unlit);
                    lamps++;
                    if (broken) faulty++;
                }
            }

            // Серый фон, квадраты и мебель в этих коридорах — на Lit, чтобы их освещали лампы.
            int relit = 0;
            if (lit != null)
                foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
                {
                    if (!IsGrayRenderer(sr.transform)) continue;
                    var c = sr.bounds.center;
                    bool inside = false;
                    foreach (var lb in litBounds)
                        if (c.x >= lb.min.x && c.x <= lb.max.x && c.y >= lb.min.y && c.y <= lb.max.y) inside = true;
                    if (!inside) continue;
                    Undo.RecordObject(sr, "Graybox Lamps");
                    bool backdrop = sr.transform.parent != null && sr.transform.parent.name == BackdropName;
                    // Потолок остаётся Unlit: вершина конуса света — над трубкой, потолок не должен подсвечиваться.
                    sr.sharedMaterial = backdrop && sr.name == "Ceiling" && unlit != null ? unlit : lit;
                    if (backdrop && sr.name == "Wall") sr.color = LitWallColor;
                    relit++;
                }

            // Общий свет — полумрак, лампы дают пятна света.
            foreach (var l in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include))
                if (l.lightType == Light2D.LightType.Global && l.gameObject.scene == scene)
                {
                    Undo.RecordObject(l, "Graybox Lamps");
                    l.intensity = AmbientIntensity;
                }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Graybox] {scene.name}: ламп {lamps} (мигают {faulty}) в {litBounds.Count} коридорах, на Lit переведено {relit} квадратов");
        }

        // Квадраты серой коробки: сам объект или его родитель называется "~…" (фон, квадраты, мебель), но не трубки ламп.
        static bool IsGrayRenderer(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (p.name == LampsName) return false;
                if (p.name.StartsWith("~")) return true;
            }
            return false;
        }

        static void BuildLamp(Transform parent, string name, float x, float areaY, bool broken, Sprite square, Material unlit)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            // Свет повёрнут вниз (у Light2D конус смотрит вдоль локальной оси Y). Вершина конуса — выше трубки,
            // на такой высоте, чтобы яркая часть конуса у трубки была шириной с саму трубку.
            float tubeY = areaY + CeilingY - 0.12f;
            float apexUp = TubeWidth / 2f / Mathf.Tan(LampInnerAngle / 2f * Mathf.Deg2Rad);
            go.transform.position = new Vector3(x, tubeY + apexUp, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, 180f);

            // Трубка под потолком — Unlit: горит сама, LampLight темнит её, когда свет гаснет.
            var tube = new GameObject("Tube");
            tube.transform.SetParent(go.transform, false);
            tube.transform.position = new Vector3(x, tubeY, 0f);
            tube.transform.rotation = Quaternion.identity;
            var s = square.bounds.size;
            tube.transform.localScale = new Vector3(TubeWidth / s.x, 0.16f / s.y, 1f);
            var tubeSr = tube.AddComponent<SpriteRenderer>();
            tubeSr.sprite = square;
            tubeSr.color = TubeColor;
            tubeSr.sortingOrder = -90;
            if (unlit != null) tubeSr.sharedMaterial = unlit;

            // Конус света вниз: от потолка до пола, расходится к полу, края мягкие; соседние конусы не сливаются.
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = LampColor;
            light.intensity = 1.1f;
            light.pointLightInnerRadius = apexUp + 0.3f;
            light.pointLightOuterRadius = apexUp + CeilingY - FloorY + 0.5f;
            light.pointLightInnerAngle = LampInnerAngle;
            light.pointLightOuterAngle = 140f;
            light.falloffIntensity = 0.5f;

            var link = new SerializedObject(go.AddComponent<LampLight>());
            link.FindProperty("tube").objectReferenceValue = tubeSr;
            link.ApplyModifiedPropertiesWithoutUndo();

            if (!broken) return;
            // Неисправная: редкие короткие очереди миганий при любом рассудке, от рассудка сама не мерцает.
            var flicker = new SerializedObject(go.AddComponent<LightFlicker>());
            flicker.FindProperty("flickerBelowSanity").floatValue = 0f;
            flicker.FindProperty("idleBlinkEvery").vector2Value = new Vector2(2f, 6f);
            flicker.FindProperty("idleBlinkLength").vector2Value = new Vector2(0.15f, 0.6f);
            flicker.FindProperty("dropoutLevel").floatValue = 0.1f;
            flicker.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------ свой фон

        const string BackdropName = "~GrayBackdrop";
        const float FloorY = -4.2f;    // верх пола относительно коридора/комнаты (как в FloorSkeletonBuilder)
        const float CeilingY = 3.6f;   // низ потолка
        static readonly Color WallColor = new Color(0.45f, 0.46f, 0.45f);
        static readonly Color FloorColor = Color.black;
        static readonly Color CeilingColor = new Color(0.18f, 0.18f, 0.19f);

        /// <summary>
        /// В коридоре и в каждой комнате (по их CameraBounds) — три полосы на всю ширину: пол, стена, потолок.
        /// Материал Unlit: на тёмных этажах (2D-свет) серая коробка видна без ламп.
        /// </summary>
        static int BuildBackdrops(Sprite square)
        {
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitPath);
            int count = 0;
            foreach (var col in Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include))
            {
                if (col.name != "CameraBounds" || col.transform.root.name != "Environment") continue;
                var area = col.transform.parent;               // Corridor или комната
                var old = area.Find(BackdropName);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

                var group = new GameObject(BackdropName);
                Undo.RegisterCreatedObjectUndo(group, "Graybox");
                group.transform.SetParent(area, false);

                var b = col.bounds;
                float originY = area.position.y;
                float floorTop = originY + FloorY, ceiling = originY + CeilingY;
                Strip(group, "Floor", square, unlit, b.min.x, b.max.x, b.min.y, floorTop, FloorColor);
                Strip(group, "Wall", square, unlit, b.min.x, b.max.x, floorTop, ceiling, WallColor);
                Strip(group, "Ceiling", square, unlit, b.min.x, b.max.x, ceiling, b.max.y, CeilingColor);
                count++;
            }
            return count;
        }

        static void Strip(GameObject parent, string name, Sprite square, Material mat,
                          float xMin, float xMax, float yMin, float yMax, Color color, int order = -100)
        {
            if (yMax <= yMin) return;
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, true);
            go.transform.position = new Vector3((xMin + xMax) / 2f, (yMin + yMax) / 2f, 0f);
            var s = square.bounds.size;
            var ps = parent.transform.lossyScale;
            go.transform.localScale = new Vector3((xMax - xMin) / s.x / ps.x, (yMax - yMin) / s.y / ps.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = color;
            sr.sortingOrder = order;
            if (mat != null) sr.sharedMaterial = mat;
        }

        // ------------------------------------------------------------ мебель

        const string PropsName = "~GrayProps";
        static readonly Color DeskColor = new Color(0.32f, 0.28f, 0.24f);
        static readonly Color MonitorColor = new Color(0.12f, 0.13f, 0.14f);
        static readonly Color ScreenColor = new Color(0.22f, 0.3f, 0.32f);

        /// <summary>
        /// Стол с компьютером под запиской Чарльза (нарратив 1.1: «на мониторе прикреплена записка»).
        /// Монитор — по центру записки, под ним подставка и стол до пола. Всё за запиской, чтобы её было видно и брать по E.
        /// </summary>
        static void BuildCharlesDesk(Sprite square)
        {
            var old = GameObject.Find(PropsName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var note = GameObject.Find("CharlesNote");
            if (note == null) return;

            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitPath);
            var root = new GameObject(PropsName);
            Undo.RegisterCreatedObjectUndo(root, "Graybox");

            Vector3 n = note.transform.position;
            float floorTop = FloorY;                 // записка в коридоре: пол коридора
            const float deskTop = -2.9f, deskWidth = 3.2f;
            const float monitorW = 1.6f, monitorH = 1.15f;
            // Монитор центрируем по записке, но не ниже стола.
            float monitorCy = Mathf.Max(n.y, deskTop + 0.35f + monitorH / 2f);

            Strip(root, "Desk", square, unlit, n.x - deskWidth / 2f, n.x + deskWidth / 2f, floorTop, deskTop, DeskColor, -60);
            Strip(root, "MonitorStand", square, unlit, n.x - 0.12f, n.x + 0.12f, deskTop, monitorCy - monitorH / 2f, MonitorColor, -55);
            Strip(root, "Monitor", square, unlit, n.x - monitorW / 2f, n.x + monitorW / 2f,
                monitorCy - monitorH / 2f, monitorCy + monitorH / 2f, MonitorColor, -55);
            Strip(root, "Screen", square, unlit, n.x - monitorW / 2f + 0.1f, n.x + monitorW / 2f - 0.1f,
                monitorCy - monitorH / 2f + 0.1f, monitorCy + monitorH / 2f - 0.1f, ScreenColor, -54);
            // Записка должна быть на мониторе: если она ниже/выше — подвинем её на экран.
            if (Mathf.Abs(n.y - monitorCy) > 0.01f)
            {
                Undo.RecordObject(note.transform, "Graybox");
                note.transform.position = new Vector3(n.x, monitorCy, n.z);
            }
        }

        const string UnlitPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        static bool IsGraybox(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
                if (p.name.StartsWith("~")) return true;
            return false;
        }

        static bool IsInside(Transform t, HashSet<GameObject> roots)
        {
            for (var p = t; p != null; p = p.parent)
                if (roots.Contains(p.gameObject)) return true;
            return false;
        }

        /// <summary>Границы спрайта в локальных координатах его объекта (с учётом pivot, режима отрисовки и flip).</summary>
        static Bounds LocalBounds(SpriteRenderer sr)
        {
            if (sr.drawMode != SpriteDrawMode.Simple) return new Bounds(Vector3.zero, sr.size);
            if (sr.sprite == null) return new Bounds(Vector3.zero, Vector3.one);
            var b = sr.sprite.bounds;
            var c = b.center;
            if (sr.flipX) c.x = -c.x;
            if (sr.flipY) c.y = -c.y;
            return new Bounds(c, b.size);
        }

        static void AddBox(Transform parent, Bounds local, int sortingLayer, int order, Sprite square)
        {
            var go = new GameObject(GrayName);
            Undo.RegisterCreatedObjectUndo(go, "Graybox");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(local.center.x, local.center.y, 0f);
            var box = go.AddComponent<SpriteRenderer>();
            box.sprite = square;
            // Квадрат растягиваем масштабом до размера спрайта, который он заменяет (в координатах родителя).
            var s = square.bounds.size;
            go.transform.localScale = new Vector3(Mathf.Max(0.05f, local.size.x) / s.x, Mathf.Max(0.05f, local.size.y) / s.y, 1f);
            box.color = InteractColor;
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitPath);
            if (unlit != null) box.sharedMaterial = unlit;   // видно и на тёмном этаже
            box.sortingLayerID = sortingLayer;
            box.sortingOrder = order;
        }

        static Vector3 Divide(Vector3 a, Vector3 b) =>
            new Vector3(a.x / (Mathf.Approximately(b.x, 0f) ? 1f : b.x), a.y / (Mathf.Approximately(b.y, 0f) ? 1f : b.y), 1f);
    }
}
