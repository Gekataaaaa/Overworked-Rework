using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Overworked.EditorTools
{
    /// <summary>
    /// Кофе в открытой сцене рядом с выделенным объектом: кружка, которую можно подобрать в хотбар.
    /// Сам создаёт спрайт кружки (Art/Props/Coffee.png, рисуется в коде) и предмет Data/Items/Coffee.asset.
    /// Повторный запуск переставляет кружку. После — сохранить сцену. Отмена — Ctrl+Z.
    /// </summary>
    public static class CoffeeBuilder
    {
        const string PickupName = "Coffee_Pickup";
        const string SpritePath = "Assets/_Project/Art/Props/Coffee.png";
        const string ItemPath = "Assets/_Project/Data/Items/Coffee.asset";
        const float Ppu = 80f;   // кружка 28 px ≈ 0.35 юнита — по руке Дэнни

        /// <summary>
        /// Кофе рядом с выделенным в Hierarchy объектом (например, телевизором): справа от него,
        /// низ кружки — на уровне низа объекта. Триггер тянется до пола под Дэнни — кружку можно взять,
        /// даже если она выше его головы.
        /// </summary>
        [MenuItem("Overworked/P1/Objects/Add Coffee Next To Selected")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var target = Selection.activeGameObject;
            var targetSprite = target != null ? target.GetComponentInChildren<SpriteRenderer>() : null;
            if (targetSprite == null)
            {
                Debug.LogWarning($"[Coffee] {scene.name}: выделите в Hierarchy объект со спрайтом (телевизор), рядом с которым поставить кофе");
                return;
            }

            var sprite = EnsureSprite();
            var item = EnsureItem(sprite);

            Undo.SetCurrentGroupName("Add Coffee");
            // Прошлая кружка — где бы она ни стояла (в т.ч. от старого меню у стола).
            var old = GameObject.Find(PickupName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var b = targetSprite.bounds;
            float w = sprite.rect.width / Ppu, h = sprite.rect.height / Ppu;
            var go = new GameObject(PickupName);
            Undo.RegisterCreatedObjectUndo(go, "Add Coffee");
            go.transform.SetParent(target.transform.parent, false);
            go.transform.position = new Vector3(b.max.x + 0.1f + w / 2f, b.min.y + h / 2f, target.transform.position.z);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = targetSprite.sharedMaterial;   // тот же свет, что у соседа
            sr.sortingLayerID = targetSprite.sortingLayerID;
            sr.sortingOrder = targetSprite.sortingOrder + 1;

            // Триггер шире кружки и вниз до пола под Дэнни: InteractionSystem ищет объекты в радиусе от его ног.
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            float bottom = player != null ? Mathf.Min(player.transform.position.y, b.min.y) - 0.3f : b.min.y - 1f;
            float top = go.transform.position.y + h / 2f + 0.1f;
            col.size = new Vector2(1f, top - bottom);
            col.offset = new Vector2(0f, (top + bottom) / 2f - go.transform.position.y);

            var pickup = go.AddComponent<HotbarPickupInteractable>();
            var so = new SerializedObject(pickup);
            so.FindProperty("prompt").stringValue = "Take coffee";
            so.FindProperty("item").objectReferenceValue = item;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = go;
            Debug.Log($"[Coffee] {scene.name}: кофе рядом с {target.name} — сохраните сцену");
        }

        static CoffeeItemData EnsureItem(Sprite icon)
        {
            var item = AssetDatabase.LoadAssetAtPath<CoffeeItemData>(ItemPath);
            if (item == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ItemPath));
                item = ScriptableObject.CreateInstance<CoffeeItemData>();
                item.id = "Coffee";
                item.displayName = "Coffee";
                AssetDatabase.CreateAsset(item, ItemPath);
            }
            item.icon = icon;
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            return item;
        }

        // Пиксельная кружка 24×28: белая кружка с ручкой справа, тёмный кофе сверху, пар.
        static Sprite EnsureSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (existing != null) return existing;

            const int w = 24, h = 28;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var clear = new Color(0, 0, 0, 0);
            var outline = new Color(0.08f, 0.07f, 0.07f);
            var cup = new Color(0.9f, 0.88f, 0.82f);
            var shade = new Color(0.7f, 0.68f, 0.63f);
            var coffee = new Color(0.32f, 0.18f, 0.1f);
            var steam = new Color(0.85f, 0.85f, 0.85f, 0.55f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, clear);

            // Тело кружки: x 2..17, y 0..17.
            for (int y = 0; y <= 17; y++)
                for (int x = 2; x <= 17; x++)
                {
                    bool edge = x == 2 || x == 17 || y == 0 || y == 17;
                    tex.SetPixel(x, y, edge ? outline : x >= 14 ? shade : cup);
                }
            // Кофе у края.
            for (int x = 3; x <= 16; x++) tex.SetPixel(x, 16, coffee);
            // Ручка справа: контурная петля x 17..21, y 5..13, внутри — пусто.
            for (int y = 5; y <= 13; y++) tex.SetPixel(21, y, outline);
            for (int x = 18; x <= 21; x++) { tex.SetPixel(x, 5, outline); tex.SetPixel(x, 13, outline); }
            for (int y = 6; y <= 12; y++) tex.SetPixel(20, y, shade);
            // Пар — две волнистые струйки.
            int[] wave = { 0, 1, 1, 0, -1, -1, 0, 1, 1 };
            for (int i = 0; i < wave.Length && 19 + i < h; i++)
            {
                tex.SetPixel(7 + wave[i], 19 + i, steam);
                tex.SetPixel(12 - wave[i], 19 + i, steam);
            }
            tex.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));
            File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SpritePath);

            var imp = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = Ppu;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }
    }
}
