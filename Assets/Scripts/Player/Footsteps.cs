using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Шаги Дэнни: на кадрах ходьбы, где нога встаёт на пол (footfallFrames), звучит один шаг — звук идёт
    /// ровно в такт картинке, с какой бы скоростью ни шла анимация. Отдельные шаги вырезаются из длинной
    /// записи (stepsRecording) один раз за игру: в ней ищутся удары по громкости, каждый становится коротким
    /// клипом с одинаковой громкостью; запись потом выгружается из памяти. Шаги чередуются случайно, без
    /// повтора подряд, с небольшим разбросом тона. Лежит на префабе Player рядом с PlayerAnimator.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class Footsteps : MonoBehaviour
    {
        [Tooltip("Запись шагов (Audio/SFX/SFX_Footsteps), из неё вырезаются отдельные шаги. " +
                 "Импорт: Decompress On Load, Preload Audio Data выключен — запись грузится только на время нарезки")]
        [SerializeField] AudioClip stepsRecording;

        [Tooltip("Кадры ходьбы, на которых нога встаёт на пол (Danny_Walk_2, Danny_Walk_5)")]
        [SerializeField] Sprite[] footfallFrames;

        [Tooltip("SpriteRenderer Дэнни, чьи кадры смотрим. Пусто — тот, что с Animator в детях")]
        [SerializeField] SpriteRenderer body;

        [Tooltip("Громкость шага 0..1 (дальше умножается на громкость канала SFX)")]
        [Range(0f, 1f)] [SerializeField] float volume = 0.35f;

        [Tooltip("Разброс высоты тона ±, чтобы одинаковые шаги не звучали механически")]
        [Range(0f, 0.2f)] [SerializeField] float pitchJitter = 0.04f;

        [Header("Нарезка записи")]
        [Tooltip("Сколько шагов вырезать из записи (берутся равномерно по всей записи)")]
        [SerializeField] int maxSteps = 12;

        [Tooltip("Максимальная длина одного шага, секунд")]
        [SerializeField] float stepLength = 0.35f;

        [Tooltip("Минимум между соседними ударами в записи, секунд (отсекает отзвук того же шага)")]
        [SerializeField] float minGap = 0.3f;

        [Tooltip("Порог удара: доля пути от тишины до самых громких мест записи (0..1)")]
        [Range(0.05f, 0.9f)] [SerializeField] float threshold = 0.3f;

        [Tooltip("Пик каждого вырезанного шага 0..1: запись тихая (мягкая подошва, гулкая комната) — шаги поднимаются до этого уровня")]
        [Range(0.1f, 1f)] [SerializeField] float stepPeak = 0.9f;

        // Нарезанные шаги по записи: Player есть в каждой сцене этажа, а режем один раз за игру.
        static readonly Dictionary<AudioClip, AudioClip[]> cache = new Dictionary<AudioClip, AudioClip[]>();
        static bool loggedFirstStep;

        // Enter Play Mode без перезагрузки домена: статика живёт между запусками, а созданные в игре клипы
        // при выходе из Play Mode уничтожаются — без сброса со второго запуска шаги были бы пустыми.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
            loggedFirstStep = false;
        }

        float lastMedianPeak;
        PlayerController controller;
        AudioClip[] steps;
        Sprite lastSprite;
        int lastStep = -1;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            if (body == null)
            {
                var animator = GetComponentInChildren<Animator>();
                if (animator != null) body = animator.GetComponent<SpriteRenderer>();
            }
        }

        IEnumerator Start()
        {
            if (stepsRecording == null) yield break;
            if (cache.TryGetValue(stepsRecording, out steps) && steps.Length > 0 && steps[0] != null) yield break;

            // Preload выключен: данные записи грузим сами и только на время нарезки.
            if (stepsRecording.loadState != AudioDataLoadState.Loaded) stepsRecording.LoadAudioData();
            while (stepsRecording.loadState == AudioDataLoadState.Loading) yield return null;
            if (stepsRecording.loadState != AudioDataLoadState.Loaded)
            {
                Debug.LogWarning($"[Footsteps] не удалось загрузить {stepsRecording.name}");
                yield break;
            }
            steps = Slice(stepsRecording);
            cache[stepsRecording] = steps;
            stepsRecording.UnloadAudioData();
            Debug.Log($"[Footsteps] из {stepsRecording.name} вырезано шагов: {steps.Length}, пик записи до подъёма — {lastMedianPeak:0.000}");
        }

        // После Animator (он ставит кадр в Update) — видим тот кадр, который сейчас нарисуется.
        void LateUpdate()
        {
            if (body == null) return;
            var sprite = body.sprite;
            if (sprite == lastSprite) return;
            lastSprite = sprite;
            if (controller.IsMoving && IsFootfall(sprite)) PlayStep();
        }

        bool IsFootfall(Sprite sprite)
        {
            if (sprite == null || footfallFrames == null) return false;
            foreach (var f in footfallFrames)
                if (f == sprite) return true;
            return false;
        }

        void PlayStep()
        {
            if (steps == null || steps.Length == 0 || AudioManager.Instance == null) return;
            int i = Random.Range(0, steps.Length);
            if (steps.Length > 1 && i == lastStep) i = (i + 1) % steps.Length;
            lastStep = i;
            if (!loggedFirstStep)
            {
                loggedFirstStep = true;
                Debug.Log($"[Footsteps] первый шаг: кадр {lastSprite.name}, клип {steps[i].name}, громкость {volume}");
            }
            AudioManager.Instance.PlaySfxPitched(steps[i], volume, 1f + Random.Range(-pitchJitter, pitchJitter));
        }

        // ------------------------------------------------------------ нарезка

        AudioClip[] Slice(AudioClip clip)
        {
            int ch = clip.channels, rate = clip.frequency, n = clip.samples;
            var raw = new float[n * ch];
            clip.GetData(raw, 0);
            var mono = new float[n];
            for (int i = 0; i < n; i++)
            {
                float s = 0f;
                for (int c = 0; c < ch; c++) s += raw[i * ch + c];
                mono[i] = s / ch;
            }

            // Огибающая — пик модуля в окнах по 10 мс.
            int win = Mathf.Max(1, rate / 100);
            int windows = n / win;
            var env = new float[windows];
            for (int w = 0; w < windows; w++)
            {
                float m = 0f;
                for (int i = w * win, end = i + win; i < end; i++) m = Mathf.Max(m, Mathf.Abs(mono[i]));
                env[w] = m;
            }
            var sorted = (float[])env.Clone();
            System.Array.Sort(sorted);
            float floor = sorted[sorted.Length / 2];
            float loud = sorted[Mathf.Min(sorted.Length - 1, (int)(sorted.Length * 0.995f))];
            float thr = floor + threshold * (loud - floor);

            // Удар — окно, где огибающая перешла порог снизу вверх, не ближе minGap к прошлому удару.
            var onsets = new List<int>();
            int gap = Mathf.CeilToInt(minGap * 100f);
            for (int w = 1; w < windows; w++)
                if (env[w] >= thr && env[w - 1] < thr && (onsets.Count == 0 || w - onsets[onsets.Count - 1] >= gap))
                    onsets.Add(w);

            // Кусок шага: чуть до удара (атака не срезана) и до следующего удара, не длиннее stepLength.
            int pre = rate / 100 * 2;
            int maxLen = Mathf.RoundToInt(stepLength * rate);
            var cuts = new List<(int start, int len, float peak)>();
            for (int k = 0; k < onsets.Count; k++)
            {
                int start = Mathf.Max(0, onsets[k] * win - pre);
                int end = k + 1 < onsets.Count ? onsets[k + 1] * win - pre : n;
                int len = Mathf.Min(maxLen, end - start);
                if (len < rate / 8) continue;   // короче 125 мс — не шаг, а щелчок/отзвук
                float peak = 0f;
                for (int i = start; i < start + len; i++) peak = Mathf.Max(peak, Mathf.Abs(mono[i]));
                cuts.Add((start, len, peak));
            }
            if (cuts.Count == 0) return new AudioClip[0];

            // Выбросы по громкости (шарканье, стук) не берём; оставшиеся — равномерно по записи.
            var peaks = cuts.ConvertAll(c => c.peak);
            peaks.Sort();
            float median = peaks[peaks.Count / 2];
            lastMedianPeak = median;
            cuts.RemoveAll(c => c.peak < median * 0.5f || c.peak > median * 2f);
            int take = Mathf.Min(maxSteps, cuts.Count);
            var result = new AudioClip[take];
            for (int k = 0; k < take; k++)
            {
                var c = cuts[k * cuts.Count / take];
                var data = new float[c.len];
                float gain = c.peak > 0f ? stepPeak / c.peak : 1f;   // все шаги — одной громкости, громко
                int fadeIn = rate / 200, fadeOut = Mathf.Min(c.len / 3, rate / 12);
                for (int i = 0; i < c.len; i++)
                {
                    float g = gain;
                    if (i < fadeIn) g *= (float)i / fadeIn;
                    int fromEnd = c.len - 1 - i;
                    if (fromEnd < fadeOut) g *= (float)fromEnd / fadeOut;
                    data[i] = mono[c.start + i] * g;
                }
                var step = AudioClip.Create($"{clip.name}_Step{k}", c.len, 1, rate, false);
                step.SetData(data, 0);
                result[k] = step;
            }
            return result;
        }
    }
}
