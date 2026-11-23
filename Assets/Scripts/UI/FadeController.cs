using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Overworked
{
    /// <summary>
    /// Затемнение экрана: чёрная FadePanel поверх всего HUD (префаб _Systems → HUD).
    /// Работает в unscaled time, поэтому не ломается на паузе.
    /// Повторный вызов прерывает текущее затемнение и продолжает с текущей прозрачности.
    /// </summary>
    public class FadeController : SingletonBehaviour<FadeController>
    {
        [Tooltip("Чёрная панель на весь экран. Пусто — берётся дочерний объект FadePanel")]
        [SerializeField] Image panel;

        /// <summary>Экран сейчас полностью чёрный.</summary>
        public bool IsBlack => panel != null && panel.color.a >= 0.999f;

        Coroutine running;

        protected override void Awake()
        {
            base.Awake();
            if (panel == null)
            {
                var child = transform.Find("FadePanel");
                if (child != null) panel = child.GetComponent<Image>();
            }
            if (panel == null) Debug.LogError("[UI] FadeController: не найдена FadePanel");
            SetAlpha(0f);
        }

        /// <summary>Экран уходит в чёрное за sec секунд, затем done.</summary>
        public void FadeOut(float sec, Action done) => FadeTo(1f, sec, done);

        /// <summary>Экран проявляется из чёрного за sec секунд, затем done.</summary>
        public void FadeIn(float sec, Action done) => FadeTo(0f, sec, done);

        /// <summary>Мгновенно чёрный (true) или прозрачный (false) экран.</summary>
        public void SetBlack(bool black)
        {
            if (running != null) StopCoroutine(running);
            running = null;
            SetAlpha(black ? 1f : 0f);
        }

        void FadeTo(float target, float sec, Action done)
        {
            if (panel == null)
            {
                done?.Invoke();
                return;
            }
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(FadeRoutine(target, sec, done));
        }

        IEnumerator FadeRoutine(float target, float sec, Action done)
        {
            float start = panel.color.a;
            // Длительность пропорциональна оставшемуся пути: прерванный fade не растягивается.
            float duration = sec * Mathf.Abs(target - start);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(start, target, t / duration));
                yield return null;
            }
            SetAlpha(target);
            running = null;
            done?.Invoke();
        }

        void SetAlpha(float a)
        {
            if (panel == null) return;
            var c = panel.color;
            c.a = a;
            panel.color = c;
            // Пока экран (почти) чёрный, панель глотает клики — нельзя нажать кнопку меню посреди перехода.
            panel.raycastTarget = a > 0.01f;
        }
    }
}
