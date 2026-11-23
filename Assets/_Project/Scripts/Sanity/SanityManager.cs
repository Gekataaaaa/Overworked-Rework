using System;
using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Рассудок 0..100. Меняется только сценарными событиями (галлюцинации задают целевое
    /// значение, паспорт, раздел 11). Шкала плавно «стекает» к цели: чем больше падение,
    /// тем дольше переход (smoothTime + extraTimePer100 за каждые 100 пунктов).
    /// Живёт в префабе _Systems, переживает смену сцен.
    /// </summary>
    public class SanityManager : SingletonBehaviour<SanityManager>
    {
        [Tooltip("Минимальная длительность перехода к новому значению, сек")]
        [SerializeField] float smoothTime = 1f;

        [Tooltip("Сколько секунд добавить к переходу за падение на все 100 пунктов (25 → 0 ≈ smoothTime + четверть этого)")]
        [SerializeField] float extraTimePer100 = 2f;

        /// <summary>
        /// Рассудок изменился; аргумент — текущее значение 0..1.
        /// Во время плавного перехода вызывается каждый кадр.
        /// </summary>
        public event Action<float> OnSanityChanged;

        /// <summary>
        /// Значение выставлено мгновенно (загрузка этажа, Continue, отладка) — шкале не нужно
        /// показывать анимацию потери рассудка.
        /// </summary>
        public event Action<float> OnSanitySnapped;

        /// <summary>Текущее (анимированное) значение 0..100.</summary>
        public float Current { get; private set; } = 100f;

        /// <summary>Текущее значение 0..1 — для шкалы, пульса, эффектов.</summary>
        public float Current01 => Current / 100f;

        /// <summary>Целевое значение 0..100.</summary>
        public float Target { get; private set; } = 100f;

        float from;
        float elapsed;
        float duration;

        /// <summary>Новое значение рассудка 0..100, шкала плавно придёт к нему (см. smoothTime).</summary>
        public void SetSanity(float value)
        {
            Target = Mathf.Clamp(value, 0f, 100f);
            // Переход всегда от текущего видимого значения: новая галлюцинация посреди старой не даёт скачка.
            from = Current;
            elapsed = 0f;
            duration = smoothTime + Mathf.Abs(Target - Current) / 100f * extraTimePer100;
            Debug.Log($"[Sanity] → {Target}");
        }

        /// <summary>Без анимации — при загрузке этажа (FloorSettings) и в отладке.</summary>
        public void SetSanityInstant(float value)
        {
            Target = Current = Mathf.Clamp(value, 0f, 100f);
            OnSanitySnapped?.Invoke(Current01);
            OnSanityChanged?.Invoke(Current01);
        }

        void Update()
        {
            if (Current == Target) return;
            elapsed += Time.deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            // Ease-in-out: шкала сначала медленно «трогается», потом стекает и мягко останавливается.
            float eased = t * t * (3f - 2f * t);
            Current = t >= 1f ? Target : Mathf.Lerp(from, Target, eased);
            OnSanityChanged?.Invoke(Current01);
        }
    }
}
