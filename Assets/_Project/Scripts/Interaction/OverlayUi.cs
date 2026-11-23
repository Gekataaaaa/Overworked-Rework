using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overworked
{
    /// <summary>
    /// Мелкие помощники для UI, который системы P2 строят сами из кода: подсказка [E],
    /// карточка задания, оверлеи мини-игр. Так их не надо собирать руками в чужом HUD-префабе;
    /// когда P3 сделает свои места в HUD, системы можно перевести на них (поля с Tooltip у каждой).
    /// </summary>
    public static class OverlayUi
    {
        /// <summary>Экранный Canvas поверх сцены, масштаб под 1920×1080.</summary>
        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>Прямоугольник-картинка. anchor — точка привязки (0..1), pos — смещение от неё.</summary>
        public static Image Box(Transform parent, string name, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Картинка на весь родитель (затемнение фона).</summary>
        public static Image Fill(Transform parent, string name, Color color)
        {
            var img = Box(parent, name, color, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            return img;
        }

        /// <summary>Надпись TextMeshPro. Шрифт — по умолчанию из TMP Settings.</summary>
        public static TextMeshProUGUI Label(Transform parent, string name, string text, float fontSize,
            Vector2 anchor, Vector2 pos, Vector2 size, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = new Color(0.9f, 0.9f, 0.85f);
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
