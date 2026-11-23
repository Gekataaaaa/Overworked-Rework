using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace Overworked.EditorTools
{
    /// <summary>
    /// Общие помощники билдеров: белый квадрат-заглушка, префаб камеры игрока и прямоугольник из квадрата.
    /// </summary>
    public static class PlayerSandboxBuilder
    {
        const string SquarePath = "Assets/Art/Placeholder_Square.png";
        const string CameraPrefabPath = "Assets/Prefabs/Player/PlayerCamera.prefab";

        // Половина высоты кадра: камера ортографическая 5.4 → видно 19.2 × 10.8.
        const float HalfHeight = 5.4f;

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
    }
}
