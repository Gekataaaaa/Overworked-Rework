using System;
using System.Collections;
using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Одна галлюцинация (H1–H4, финальная): звук, визуал, спайк пульса, новое значение рассудка.
    /// Лежит в сцене (папка Hallucinations), запускается через HallucinationManager.
    /// Визуал — дочерние HallucinationVisual (силуэт, скример, вспышки, помехи); они идут по порядку
    /// в иерархии, «С предыдущим» запускает визуал одновременно с соседом сверху.
    /// Рассудок падает в момент, когда Дэнни ВИДИТ галлюцинацию (силуэт попал в кадр, вспышка) — ApplySanity.
    /// «Удар» (звук + спайк пульса до 140) — в резкий момент по нарративу (резкое появление, резкий звук
    /// при подходе, скример) — Hit. Если визуалы их не отметили — оба в конце.
    /// </summary>
    public class HallucinationEvent : MonoBehaviour
    {
        [Header("Рассудок и пульс")]
        [Tooltip("Рассудок после галлюцинации (паспорт, раздел 11). Меньше 0 — рассудок не меняется")]
        public float SanityAfter = 90f;

        [Tooltip("Насколько подскакивает пульс")]
        public float HeartbeatSpike = 30f;

        [Tooltip("Длительность спайка пульса, секунд (пик спайка — не выше 140, HeartbeatManager)")]
        public float Duration = 3f;

        [Tooltip("Вместо спайка — испуг скримера: пульс 160–180 и медленное восстановление (финальный скример)")]
        public bool ScreamerHeartbeat;

        [Header("Звук")]
        [Tooltip("Звуковой удар (пусто — без звука, пока нет ассетов)")]
        public AudioClip Sound;

        [Tooltip("Громкость звука")]
        [Range(0f, 1f)] public float SoundVolume = 1f;

        [Tooltip("Звук в начале галлюцинации (иначе — в момент удара)")]
        public bool SoundOnStart;

        [Header("Управление")]
        [Tooltip("Забрать управление у игрока на время галлюцинации")]
        public bool LockPlayer;

        [Tooltip("Не возвращать управление после конца (финальный скример: дальше Дэнни падает)")]
        public bool KeepLocked;

        /// <summary>Галлюцинация идёт.</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>Удар уже был (звук и пульс).</summary>
        public bool HasHit { get; private set; }

        /// <summary>Рассудок уже упал (Дэнни увидел галлюцинацию).</summary>
        public bool SanityApplied { get; private set; }

        /// <summary>Сыграна хотя бы раз (для триггеров «после H4»).</summary>
        public bool HasPlayed { get; private set; }

        /// <summary>Галлюцинация закончилась (финальный скример → падение и WhiteRoom).</summary>
        public event Action Finished;

        /// <summary>Момент удара — для света, камеры и т. п.</summary>
        public event Action OnHit;

        /// <summary>Запускает галлюцинацию; done — по окончании всех визуалов. Повторный вызов во время игры игнорируется.</summary>
        public void Play(Action done = null)
        {
            if (IsPlaying)
            {
                Debug.LogWarning($"[Hallucinations] {name} уже идёт");
                return;
            }
            if (!isActiveAndEnabled)
            {
                // Корутина на выключенном объекте не стартует — применяем только эффекты, чтобы сюжет не встал.
                Debug.LogWarning($"[Hallucinations] {name} выключен — только удар без визуала");
                HasHit = SanityApplied = false;
                Hit();
                HasPlayed = true;
                done?.Invoke();
                return;
            }
            StartCoroutine(Run(done));
        }

        /// <summary>Рассудок → SanityAfter: Дэнни увидел галлюцинацию. Один раз за проигрывание.</summary>
        public void ApplySanity()
        {
            if (SanityApplied) return;
            SanityApplied = true;
            if (SanityAfter < 0f) return;   // только эффект (мигание при появлении enemy01) — рассудок не трогаем
            if (SanityManager.Instance != null) SanityManager.Instance.SetSanity(SanityAfter);
            Debug.Log($"[Hallucinations] {name}: увидел, рассудок → {SanityAfter}");
        }

        /// <summary>
        /// Удар — резкий момент: звук и спайк пульса (или испуг скримера). Рассудок к этому моменту
        /// уже должен упасть — если нет, падает сейчас. Один раз за проигрывание.
        /// </summary>
        public void Hit()
        {
            if (HasHit) return;
            HasHit = true;
            ApplySanity();
            if (!SoundOnStart) PlaySound();
            var heart = HeartbeatManager.Instance;
            if (heart != null)
            {
                if (ScreamerHeartbeat) heart.PlayScreamer();
                else heart.Spike(HeartbeatSpike, Duration);
            }
            Debug.Log($"[Hallucinations] {name}: удар");
            OnHit?.Invoke();
        }

        IEnumerator Run(Action done)
        {
            IsPlaying = true;
            HasHit = SanityApplied = false;
            var player = PlayerController.Instance;
            if (LockPlayer && player != null) player.SetInputLocked(true);
            if (SoundOnStart) PlaySound();

            var visuals = GetComponentsInChildren<HallucinationVisual>(true);
            int i = 0;
            while (i < visuals.Length)
            {
                // Группа: визуал и все следующие за ним с «С предыдущим» идут одновременно.
                int running = 0;
                do
                {
                    running++;
                    StartCoroutine(RunVisual(visuals[i], () => running--));
                    i++;
                } while (i < visuals.Length && visuals[i].WithPrevious);
                while (running > 0) yield return null;
            }

            Hit();
            foreach (var v in visuals) v.Cleanup();
            if (LockPlayer && !KeepLocked && player != null) player.SetInputLocked(false);
            IsPlaying = false;
            HasPlayed = true;
            done?.Invoke();
            Finished?.Invoke();
        }

        IEnumerator RunVisual(HallucinationVisual visual, Action finished)
        {
            if (visual.Delay > 0f) yield return new WaitForSeconds(visual.Delay);
            yield return visual.Play(this);
            finished();
        }

        void PlaySound()
        {
            if (Sound != null && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(Sound, SoundVolume);
        }
    }
}
