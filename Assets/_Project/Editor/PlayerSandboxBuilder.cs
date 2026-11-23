using TMPro;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Overworked.EditorTools
{
    /// <summary>
    /// Собирает префаб игрока-заглушки и тестовый коридор в Sandbox_P1 (серые прямоугольники).
    /// Запуск: меню Overworked → P1 → Player → Build Player Sandbox,
    /// или batchmode -executeMethod Overworked.EditorTools.PlayerSandboxBuilder.Run.
    /// Префаб не перезаписывается, если уже есть; объекты коридора в Sandbox_P1 пересоздаются.
    /// </summary>
    public static class PlayerSandboxBuilder
    {
        const string SquarePath = "Assets/_Project/Art/Placeholder_Square.png";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player.prefab";
        const string CameraPrefabPath = "Assets/_Project/Prefabs/Player/PlayerCamera.prefab";
        const string StairsPrefabPath = "Assets/_Project/Prefabs/Environment/Staircase.prefab";
        const string ScenePath = "Assets/_Project/Scenes/Sandbox/Sandbox_P1.unity";

        // Размеры коридора в юнитах. Камера ортографическая 5.4 → видно 19.2 × 10.8.
        const float HalfWidth = 30f;
        const float HalfHeight = 5.4f;
        // Линия пола (горизонт): пол занимает нижние 1.2 юнита кадра.
        const float FloorY = -4.2f;
        // Запас границ камеры по вертикали. Если границы ровно равны кадру, Confiner2D
        // сжимает их в отрезок и прибивает камеру к центру — она перестаёт ехать за игроком.
        const float BoundsMargin = 0.25f;

        [MenuItem("Overworked/P1/Player/Build Player Sandbox")]
        public static void Run()
        {
            var square = EnsureSquareSprite();
            var prefab = EnsurePlayerPrefab(square);
            BuildScene(square, prefab, EnsureStairsPrefab(square), EnsureCameraPrefab());
            Debug.Log("[Setup] Sandbox_P1: коридор и игрок готовы");
        }

        /// <summary>Белый квадрат 1×1 юнит — заглушка для всех прямоугольников до прихода арта.</summary>
        internal static Sprite EnsureSquareSprite()
        {
            if (!File.Exists(SquarePath))
            {
                var tex = new Texture2D(4, 4);
                var pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                tex.SetPixels(pixels);
                File.WriteAllBytes(SquarePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(SquarePath);

                var importer = (TextureImporter)AssetImporter.GetAtPath(SquarePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 4;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        }

        internal static GameObject EnsurePlayerPrefab(Sprite square)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("Player");
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionY;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var col = root.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 1.8f);
            root.AddComponent<PlayerController>();
            root.AddComponent<PlayerAnimator>();

            var visual = new GameObject("Sprite");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = new Vector3(0.8f, 1.8f, 1f);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0.85f, 0.82f, 0.7f);
            sr.sortingOrder = 10;

            // Маркер «нос» — видно, куда смотрит Дэнни, пока нет арта.
            var nose = new GameObject("FacingMarker");
            nose.transform.SetParent(visual.transform, false);
            nose.transform.localPosition = new Vector3(0.35f, 0.3f, 0f);
            nose.transform.localScale = new Vector3(0.3f, 0.1f, 1f);
            var nsr = nose.AddComponent<SpriteRenderer>();
            nsr.sprite = square;
            nsr.color = new Color(0.2f, 0.2f, 0.2f);
            nsr.sortingOrder = 11;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void BuildScene(Sprite square, GameObject playerPrefab, GameObject stairsPrefab, GameObject cameraPrefab)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
                if (go.name is "Environment" or "Player" or "Cameras" or "Staircase") Object.DestroyImmediate(go);

            var env = new GameObject("Environment");
            float top = HalfHeight + BoundsMargin, bottom = -HalfHeight - BoundsMargin;
            Rect(env, "BackWall", square, new Vector2(0, (top + FloorY) / 2), new Vector2(HalfWidth * 2, top - FloorY), new Color(0.32f, 0.36f, 0.33f), -10);
            Rect(env, "Floor", square, new Vector2(0, (FloorY + bottom) / 2), new Vector2(HalfWidth * 2, FloorY - bottom), new Color(0.2f, 0.22f, 0.21f), -5);
            // Двери-ориентиры: видно, что игрок движется и камера едет.
            for (int i = -4; i <= 4; i++)
                Rect(env, $"Door_{i + 5}", square, new Vector2(i * 6f, FloorY + 1.25f), new Vector2(1.4f, 2.5f), new Color(0.45f, 0.4f, 0.3f), -8);

            Wall(env, "WallLeft", square, -HalfWidth - 0.5f);
            Wall(env, "WallRight", square, HalfWidth + 0.5f);

            // Границы камеры: Confiner2D не выпускает камеру за коридор.
            var bounds = new GameObject("CameraBounds");
            bounds.transform.SetParent(env.transform, false);
            var poly = bounds.AddComponent<PolygonCollider2D>();
            poly.isTrigger = true;
            poly.points = new[]
            {
                new Vector2(-HalfWidth - 1, bottom), new Vector2(HalfWidth + 1, bottom),
                new Vector2(HalfWidth + 1, top), new Vector2(-HalfWidth - 1, top),
            };

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.position = new Vector3(0, FloorY + 0.9f, 0);

            // Лестница в правом конце коридора (в песочнице InteractionSystem ещё нет —
            // проверять через контекстное меню компонента StairInteractable → Interact).
            var stairsRoot = new GameObject("Staircase");
            var stairs = (GameObject)PrefabUtility.InstantiatePrefab(stairsPrefab, scene);
            stairs.transform.SetParent(stairsRoot.transform, false);
            stairs.transform.position = new Vector3(HalfWidth - 2.5f, FloorY + 1.5f, 0);

            var cameras = new GameObject("Cameras");
            var vcamGo = (GameObject)PrefabUtility.InstantiatePrefab(cameraPrefab, scene);
            vcamGo.name = "CM Corridor";
            vcamGo.transform.SetParent(cameras.transform, false);
            vcamGo.GetComponent<CinemachineConfiner2D>().BoundingShape2D = poly;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Камера игрока для коридоров и комнат: следует за Дэнни со смещением в сторону движения.
        /// В сцене остаётся только назначить границы в CinemachineConfiner2D.
        /// </summary>
        internal static GameObject EnsureCameraPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPrefabPath);
            if (existing != null) return existing;

            var go = new GameObject("PlayerCamera");
            go.transform.position = new Vector3(0, 0, -10);
            var vcam = go.AddComponent<CinemachineCamera>();
            var lens = vcam.Lens;
            lens.OrthographicSize = HalfHeight;
            vcam.Lens = lens;

            var composer = go.AddComponent<CinemachinePositionComposer>();
            composer.CameraDistance = 10f;
            composer.Damping = new Vector3(0.8f, 0f, 0f);
            // Смещение в сторону движения (паспорт, раздел 27).
            composer.Lookahead = new LookaheadSettings { Enabled = true, Time = 0.6f, Smoothing = 8f, IgnoreY = true };

            go.AddComponent<CinemachineConfiner2D>();
            go.AddComponent<FollowPlayer>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, CameraPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>
        /// Лестница-заглушка: серый прямоугольник + триггер + StairInteractable + подсказка «[E] Go down».
        /// Старый префаб без подсказки пересобирается (путь тот же — GUID и ссылки в сценах сохраняются).
        /// </summary>
        internal static GameObject EnsureStairsPrefab(Sprite square)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StairsPrefabPath);
            if (existing != null && existing.transform.Find("PromptLabel") != null) return existing;

            var go = new GameObject("Staircase");
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2f, 3f);
            var stairs = go.AddComponent<StairInteractable>();
            Rect(go, "Sprite", square, Vector2.zero, new Vector2(2f, 3f), new Color(0.16f, 0.17f, 0.2f), -7);
            // Ступеньки-полоски, чтобы было понятно, что это лестница.
            for (int i = 0; i < 4; i++)
                Rect(go, $"Step_{i}", square, new Vector2(-0.6f + i * 0.4f, -1.2f + i * 0.6f), new Vector2(0.8f, 0.12f), new Color(0.4f, 0.42f, 0.45f), -6);

            var label = new GameObject("PromptLabel");
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0, 2.2f, 0);
            var tmp = label.AddComponent<TextMeshPro>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/PTSans SDF.asset");
            if (font != null) tmp.font = font;
            tmp.text = "[E] Go down";
            tmp.fontSize = 3f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.92f, 0.92f, 0.86f);
            tmp.sortingOrder = 20;
            tmp.rectTransform.sizeDelta = new Vector2(6f, 1f);
            label.SetActive(false);

            var so = new SerializedObject(stairs);
            so.FindProperty("promptLabel").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, StairsPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        internal static GameObject Rect(GameObject parent, string name, Sprite square, Vector2 pos, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        static void Wall(GameObject parent, string name, Sprite square, float x)
        {
            var wall = Rect(parent, name, square, new Vector2(x, 0f), new Vector2(1f, (HalfHeight + BoundsMargin) * 2), new Color(0.15f, 0.16f, 0.15f), -4);
            wall.AddComponent<BoxCollider2D>();
        }
    }
}
