using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SB = Overworked.EditorTools.PlayerSandboxBuilder;

namespace Overworked.EditorTools
{
    /// <summary>
    /// Шкафчик для пряток (HideLocker) в коридоре открытой сцены: в рост Дэнни, правее его старта,
    /// в свободном от других объектов месте. Задняя стенка тёмная, дверца из двух частей — между ними щель
    /// на уровне глаз: через неё видно Дэнни внутри. Повторный запуск пересобирает шкафчик. Отмена — Ctrl+Z.
    /// </summary>
    public static class LockerBuilder
    {
        const string LockerName = "Locker";
        const string FeetCoverName = "~LockerFeetCover";
        const float FloorY = -4.2f;   // верх пола относительно коридора (как в GrayboxTool)
        static readonly Color BodyColor = new Color(0.36f, 0.4f, 0.42f);
        static readonly Color BackColor = new Color(0.05f, 0.05f, 0.06f);
        const string UnlitPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        [MenuItem("Overworked/P1/Objects/Add Locker In Open Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var corridor = GameObject.Find("Environment/Corridor");
            var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (corridor == null || player == null)
            {
                Debug.LogWarning($"[Locker] {scene.name}: нет Environment/Corridor или Player");
                return;
            }
            Undo.SetCurrentGroupName("Add Locker");
            int undoGroup = Undo.GetCurrentGroup();

            var old = corridor.transform.Find(LockerName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var oldCover = corridor.transform.Find(FeetCoverName);
            if (oldCover != null) Undo.DestroyObjectImmediate(oldCover.gameObject);

            // Размер — по спрайту Дэнни с запасом.
            var body = player.transform.Find("Sprite")?.GetComponent<SpriteRenderer>();
            Bounds pb = body != null ? body.bounds : new Bounds(player.transform.position, new Vector3(0.8f, 2.9f, 0f));
            float floor = corridor.transform.position.y + FloorY;
            // Ступни Дэнни уходят ниже линии пола — их закроет полоса цвета пола под шкафчиком.
            float feet = Mathf.Min(floor - 0.1f, pb.min.y - 0.05f);
            float w = Mathf.Max(1.1f, pb.size.x + 0.4f);
            float h = pb.max.y - floor + 0.3f;
            float x = FindFreeX(corridor, player.transform.position.x + 3f, w);

            var root = new GameObject(LockerName);
            Undo.RegisterCreatedObjectUndo(root, "Add Locker");
            root.transform.SetParent(corridor.transform, true);
            root.transform.position = new Vector3(x, floor + h / 2f, 0f);
            var col = root.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(w, h);
            root.AddComponent<HideLocker>();

            // Материал как у стены коридора (Lit под лампами), иначе Unlit.
            var wall = corridor.transform.Find("~GrayBackdrop/Wall")?.GetComponent<SpriteRenderer>();
            var mat = wall != null ? wall.sharedMaterial : AssetDatabase.LoadAssetAtPath<Material>(UnlitPath);

            var square = SB.EnsureSquareSprite();
            var art = new GameObject("~LockerArt");
            art.transform.SetParent(root.transform, false);
            float left = x - w / 2f, right = x + w / 2f, top = floor + h;
            // Щель — на уровне глаз Дэнни (верхняя часть головы).
            float slitMid = pb.max.y - pb.size.y * 0.14f, slitHalf = 0.07f;
            // Задняя стенка ниже Дэнни (у него 10..13), дверца — выше спрятанного Дэнни (10..13 − 15).
            Part(art, "Back", square, mat, left, right, floor, top, BackColor, -20);
            Part(art, "DoorLow", square, mat, left, right, floor, slitMid - slitHalf, BodyColor, 0);
            // Полоса под ступнями — не в шкафчике, а рядом в коридоре: иначе жёлтая обводка фокуса уходит ниже пола.
            var cover = new GameObject(FeetCoverName);
            Undo.RegisterCreatedObjectUndo(cover, "Add Locker");
            cover.transform.SetParent(corridor.transform, false);
            // Шире шкафчика: закрывает и края жёлтой обводки, которые уходят ниже пола.
            Part(cover, "FeetCover", square, mat, left - 0.15f, right + 0.15f, feet, floor, Color.black, 0);
            Part(art, "DoorHigh", square, mat, left, right, slitMid + slitHalf, top, BodyColor, 0);
            // Рамка вокруг щели: боковины, чтобы щель не шла на всю ширину.
            Part(art, "SlitL", square, mat, left, left + 0.12f, slitMid - slitHalf, slitMid + slitHalf, BodyColor, 0);
            Part(art, "SlitR", square, mat, right - 0.12f, right, slitMid - slitHalf, slitMid + slitHalf, BodyColor, 0);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log($"[Locker] {scene.name}: шкафчик в x={x:0.0}, {w:0.00}×{h:0.00}");
        }

        // Правее from: первое место, где рядом нет другого объекта для E и дверей.
        static float FindFreeX(GameObject corridor, float from, float width)
        {
            var bounds = corridor.transform.Find("CameraBounds")?.GetComponent<Collider2D>()?.bounds;
            float maxX = bounds.HasValue ? bounds.Value.max.x - width : from + 50f;
            float y = corridor.transform.position.y;
            Physics2D.SyncTransforms();
            for (float x = from; x < maxX; x += 0.5f)
            {
                bool busy = false;
                foreach (var c in Physics2D.OverlapBoxAll(new Vector2(x, y), new Vector2(width + 2f, 8f), 0f))
                    if (c.GetComponent<IInteractable>() != null || c.GetComponent<Inspectable>() != null) { busy = true; break; }
                if (!busy) return x;
            }
            return from;
        }

        static void Part(GameObject parent, string name, Sprite square, Material mat,
                         float xMin, float xMax, float yMin, float yMax, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, true);
            go.transform.position = new Vector3((xMin + xMax) / 2f, (yMin + yMax) / 2f, 0f);
            var s = square.bounds.size;
            go.transform.localScale = new Vector3((xMax - xMin) / s.x, (yMax - yMin) / s.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = color;
            sr.sortingOrder = order;
            if (mat != null) sr.sharedMaterial = mat;
        }
    }
}
