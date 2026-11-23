using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SB = Overworked.EditorTools.PlayerSandboxBuilder;

namespace Overworked.EditorTools
{
    /// <summary>
    /// enemy01 в коридоре: чёрный квадрат у правого конца (Enemy01) и триггер-запуск по центру коридора (Enemy01Trigger).
    /// Коридор — открытый префаб (Prefab Mode, например Corridor_C) или выделенный объект коридора в сцене;
    /// концы пути берутся по Walls/WallLeft и Walls/WallRight. Повторный запуск пересобирает. После — сохранить префаб/сцену.
    /// </summary>
    public static class Enemy01Builder
    {
        const string EnemyName = "enemy01";
        const string TriggerName = "enemy01_Trigger";
        const string EffectName = "enemy01_Flicker";
        const float FlickerSeconds = 1f;   // сколько мигают лампы при появлении врага
        const float FloorY = -4.2f;      // верх пола относительно коридора (как в GrayboxTool)
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player.prefab";
        const float EndInset = 1f;       // отступ концов пути от стен
        const int SortingOrder = 20;     // поверх Дэнни и шкафчика
        const string UnlitPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        [MenuItem("Overworked/P1/Objects/Add Enemy01 To Open Corridor")]
        public static void Build()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            var corridor = stage != null ? stage.prefabContentsRoot : Selection.activeGameObject;
            var left = corridor != null ? corridor.transform.Find("Walls/WallLeft") : null;
            var right = corridor != null ? corridor.transform.Find("Walls/WallRight") : null;
            if (left == null || right == null)
            {
                Debug.LogWarning("[Enemy01] откройте префаб коридора (Corridor_C) или выделите коридор в сцене — нужны Walls/WallLeft и Walls/WallRight");
                return;
            }
            Undo.SetCurrentGroupName("Add Enemy01");
            var root = corridor.transform;
            foreach (var n in new[] { EnemyName, TriggerName, EffectName })
            {
                var old = root.Find(n);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            }

            float leftX = left.localPosition.x + EndInset;
            float rightX = right.localPosition.x - EndInset;

            // Размер — как хитбокс Дэнни (BoxCollider2D префаба Player); корень — на высоте корня Дэнни
            // (EntryPoint коридора), квадрат смещён как его коллайдер.
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var hitbox = playerPrefab != null ? playerPrefab.GetComponent<BoxCollider2D>() : null;
            Vector2 size = hitbox != null ? hitbox.size : new Vector2(0.8f, 2.9f);
            Vector2 offset = hitbox != null ? hitbox.offset : new Vector2(0f, 0.2f);
            var entry = root.Find("EntryPoint");
            float rootY = entry != null ? entry.localPosition.y : FloorY + size.y / 2f - offset.y - 0.35f;

            // Враг: корень у правого конца, внутри — чёрный прямоугольник (прячется до запуска).
            var enemyGo = new GameObject(EnemyName);
            Undo.RegisterCreatedObjectUndo(enemyGo, "Add Enemy01");
            enemyGo.transform.SetParent(root, false);
            enemyGo.transform.localPosition = new Vector3(rightX, rootY, 0f);

            var body = new GameObject("Body");
            body.transform.SetParent(enemyGo.transform, false);
            body.transform.localPosition = new Vector3(0f, offset.y, 0f);
            body.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = SB.EnsureSquareSprite();
            sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitPath);
            sr.color = Color.black;
            sr.sortingOrder = SortingOrder;

            var enemy = enemyGo.AddComponent<Enemy01>();
            var so = new SerializedObject(enemy);
            so.FindProperty("leftEndX").floatValue = leftX;
            so.FindProperty("rightEndX").floatValue = rightX;
            so.FindProperty("body").objectReferenceValue = body;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Триггер по центру коридора, в рост Дэнни.
            var trigger = new GameObject(TriggerName);
            Undo.RegisterCreatedObjectUndo(trigger, "Add Enemy01");
            trigger.transform.SetParent(root, false);
            trigger.transform.localPosition = new Vector3((leftX + rightX) / 2f, FloorY + 2f, 0f);
            var col = trigger.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1f, 4f);
            // Эффект появления — только мигание ламп, как в H1 на турникете, без вспышек экрана:
            // лампы гаснут почти полностью, общий свет сцены — до 40 %; включаются — обратно 100 %. Рассудок не меняется.
            var fx = new GameObject(EffectName);
            Undo.RegisterCreatedObjectUndo(fx, "Add Enemy01");
            fx.transform.SetParent(root, false);
            var effect = fx.AddComponent<HallucinationEvent>();
            effect.SanityAfter = -1f;
            effect.HeartbeatSpike = 30f;
            effect.Duration = 4f;
            var lampsGo = new GameObject("LampLights");
            lampsGo.transform.SetParent(fx.transform, false);
            var lso = new SerializedObject(lampsGo.AddComponent<LightFlickerVisual>());
            lso.FindProperty("duration").floatValue = FlickerSeconds;
            lso.FindProperty("rate").floatValue = 10f;
            lso.FindProperty("darkLevel").floatValue = 0f;             // все лампы разом вырубаются полностью
            lso.FindProperty("globalDarkLevel").floatValue = 0.4f;     // общий свет — до 40 %
            lso.FindProperty("brightLevel").floatValue = 1f;
            lso.ApplyModifiedPropertiesWithoutUndo();

            var tso = new SerializedObject(trigger.AddComponent<Enemy01Trigger>());
            tso.FindProperty("enemy").objectReferenceValue = enemy;
            tso.FindProperty("effect").objectReferenceValue = effect;
            tso.ApplyModifiedPropertiesWithoutUndo();

            if (stage != null) EditorSceneManager.MarkSceneDirty(stage.scene);
            else EditorSceneManager.MarkSceneDirty(corridor.scene);
            Selection.activeGameObject = enemyGo;
            Debug.Log($"[Enemy01] {corridor.name}: враг и триггер добавлены — сохраните");
        }
    }
}
