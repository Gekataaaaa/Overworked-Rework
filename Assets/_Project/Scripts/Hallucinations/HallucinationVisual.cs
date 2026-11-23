using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Overworked
{
    /// <summary>
    /// Часть визуала галлюцинации (дочерний объект HallucinationEvent): силуэт, скример, вспышки, помехи.
    /// Сейчас всё рисуется квадратами-заглушками; арт подменяется в спрайтах/цветах префабов.
    /// </summary>
    public abstract class HallucinationVisual : MonoBehaviour
    {
        [Header("Порядок")]
        [Tooltip("Запускать одновременно с визуалом выше по иерархии (иначе — после него)")]
        public bool WithPrevious;

        [Tooltip("Задержка перед стартом, секунд")]
        public float Delay;

        [Tooltip("Этот визуал отмечает резкий момент — удар: звук и спайк пульса (до 140)")]
        public bool TriggersHit;

        [Tooltip("Рассудок падает, как только Дэнни видит этот визуал (силуэт попал в кадр, вспышка на экране)")]
        public bool SanityOnSeen;

        /// <summary>Проигрывает визуал; корутина идёт на HallucinationEvent, закончилась — визуал закончился.</summary>
        public abstract IEnumerator Play(HallucinationEvent hallucination);

        /// <summary>Конец всей галлюцинации: спрятать то, что визуал оставил на экране (силуэт двойника под скримером).</summary>
        public virtual void Cleanup() { }

        /// <summary>Визуал стал виден — рассудок падает, если этот визуал за него отвечает.</summary>
        protected void SeenIfNeeded(HallucinationEvent hallucination)
        {
            if (SanityOnSeen) hallucination.ApplySanity();
        }

        /// <summary>Отмечает удар, если этот визуал за него отвечает.</summary>
        protected void HitIfNeeded(HallucinationEvent hallucination)
        {
            if (TriggersHit) hallucination.Hit();
        }

        static Canvas overlay;

        /// <summary>
        /// Полноэкранный слой поверх всего (и HUD): квадраты скримера, вспышки, помехи.
        /// Один на сцену, создаётся при первом запросе.
        /// </summary>
        protected static RectTransform Overlay()
        {
            if (overlay == null)
            {
                var go = new GameObject("HallucinationOverlay", typeof(RectTransform));
                overlay = go.AddComponent<Canvas>();
                overlay.renderMode = RenderMode.ScreenSpaceOverlay;
                // Выше HUD и телефона: скример должен закрывать всё.
                overlay.sortingOrder = 900;
            }
            return (RectTransform)overlay.transform;
        }

        /// <summary>Квадрат на оверлее: Image без спрайта — сплошной цвет; лучи не ловит.</summary>
        protected static Image OverlaySquare(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(Overlay(), false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Растягивает элемент на весь экран.</summary>
        protected static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
