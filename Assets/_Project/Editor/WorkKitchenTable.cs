using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Overworked.EditorTools
{
    /// <summary>
    /// P3, рабочая сцена Work_P1/Floor_03_Work: стол на кухне под пропуском Макса (MaxPass) — чтобы карточка
    /// лежала на столе, а не висела в воздухе (фон кухни в graybox спрятан). Стиль — как стол Чарльза в GrayboxTool (P1):
    /// тот же цвет, Unlit-материал, порядок отрисовки за объектами. Верх стола — по низу карточки, низ — по полу кухни.
    /// Объект ~KitchenTable в комнате Kitchen (имя с «~» — GrayboxTool его не прячет). Повторный запуск пересоздаёт стол.
    /// Меню Overworked → P3 → Floor_03_Work → Kitchen Table For Max's Pass. Работает в открытой сцене (сохранить — Ctrl+S).
    /// </summary>
    public static class WorkKitchenTable
    {
        const string ScenePath = "Assets/_Project/Scenes/Work_P1/Floor_03_Work.unity";
        const string TableName = "~KitchenTable";
        const string UnlitPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        // Как в GrayboxTool: стол Чарльза, пол комнаты на 4.2 ниже её центра.
        static readonly Color DeskColor = new Color(0.32f, 0.28f, 0.24f);
        const float FloorY = -4.2f;
        const float TableWidth = 2.2f;

        [MenuItem("Overworked/P3/Floor_03_Work/Kitchen Table For Max's Pass")]
        public static void Run()
        {
            if (!EnsureScene()) return;

            var kitchen = GameObject.Find("Environment/Rooms/Kitchen");
            var pass = GameObject.Find("Environment/Rooms/Kitchen/Props/MaxPass");
            if (kitchen == null || pass == null)
            {
                Debug.LogError("[Setup] Floor_03_Work: нет комнаты Kitchen или пропуска MaxPass");
                return;
            }

            // Низ карточки — по видимым спрайтам пропуска (в graybox это квадрат ~Graybox).
            var renderers = pass.GetComponentsInChildren<SpriteRenderer>(false);
            if (renderers.Length == 0)
            {
                Debug.LogError("[Setup] у MaxPass нет видимого спрайта — не по чему ставить стол");
                return;
            }
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);

            float floorTop = kitchen.transform.position.y + FloorY;
            float tableTop = b.min.y;
            if (tableTop - floorTop < 0.3f)
            {
                Debug.LogError("[Setup] пропуск лежит почти на полу — стол не нужен");
                return;
            }

            var old = kitchen.transform.Find(TableName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var square = PlayerSandboxBuilder.EnsureSquareSprite();
            var table = new GameObject(TableName);
            Undo.RegisterCreatedObjectUndo(table, "Kitchen table");
            table.transform.SetParent(kitchen.transform, false);
            table.transform.position = new Vector3(b.center.x, (floorTop + tableTop) / 2f, 0f);
            var s = square.bounds.size;
            table.transform.localScale = new Vector3(TableWidth / s.x, (tableTop - floorTop) / s.y, 1f);
            var sr = table.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = DeskColor;
            sr.sortingOrder = -60;       // за пропуском и Дэнни, перед стеной (как стол Чарльза)
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitPath);
            if (unlit != null) sr.sharedMaterial = unlit;

            EditorSceneManager.MarkSceneDirty(table.scene);
            if (Application.isBatchMode) EditorSceneManager.SaveScene(table.scene);
            Debug.Log($"[Setup] Floor_03_Work: стол под пропуском Макса (x {b.center.x:0.##}, высота {tableTop - floorTop:0.##})");
        }

        /// <summary>Рабочая сцена должна быть открыта; если открыта другая — предложить сохранить и открыть нужную.</summary>
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
