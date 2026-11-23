using System;
using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Пульс — индикатор напряжения, не здоровье (паспорт, раздел 13). Поведение приближено к человеку:
    /// пульс растёт быстрее, чем спадает; спад экспоненциальный (быстро в начале, долгий хвост);
    /// пульс всегда слегка «гуляет» (±wobbleAmount); удары чуть неровные.
    /// Режимы: от рассудка (70…120 + спайки галлюцинаций), скример (испуг: за пару секунд
    /// разгон до 160…180, удержание, медленное восстановление), сценарный (WhiteRoom: SetBpm / FadeBpm).
    /// Каждый удар играет звук на канале Heartbeat и вызывает OnBeat. Живёт в префабе _Systems.
    /// </summary>
    public class HeartbeatManager : SingletonBehaviour<HeartbeatManager>
    {
        [Header("Диапазон")]
        [Tooltip("BPM при рассудке 100 — минимальный пульс на этажах; с ним Дэнни приходит в WhiteRoom")]
        [SerializeField] float minBpm = 70f;
        [Tooltip("BPM при рассудке 0 — максимальный пульс от рассудка")]
        [SerializeField] float maxBpm = 120f;
        [Tooltip("Нижняя граница пульса на пике скримера")]
        [SerializeField] float screamerMinBpm = 160f;
        [Tooltip("Верхняя граница пульса на пике скримера — абсолютный потолок, выше не поднимают и спайки")]
        [SerializeField] float screamerMaxBpm = 180f;
        [Tooltip("Потолок спайка галлюцинации (резкое появление силуэта, вспышка): выше спайк пульс не поднимает. " +
                 "Скример (PlayScreamer) — отдельно, до screamerMaxBpm")]
        [SerializeField] float spikeMaxBpm = 140f;

        [Header("Как у человека")]
        [Tooltip("Постоянная времени роста пульса, сек: за это время проходится ~63 % пути вверх")]
        [SerializeField] float riseTime = 1f;
        [Tooltip("Постоянная времени спада пульса, сек: сердце успокаивается заметно медленнее, чем разгоняется")]
        [SerializeField] float fallTime = 3.5f;
        [Tooltip("Спайк галлюцинации: за сколько секунд пульс доходит до пика")]
        [SerializeField] float spikeRise = 0.6f;
        [Tooltip("Форма спада после скримера и на кровати: больше — резче в начале и длиннее «хвост». 3–6 выглядят естественно")]
        [Range(1f, 10f)] [SerializeField] float fallSharpness = 4f;
        [Tooltip("Неровность ритма: на сколько (доля интервала) случайно сдвигается каждый удар")]
        [Range(0f, 0.15f)] [SerializeField] float beatVariability = 0.04f;

        [Header("Колебания")]
        [Tooltip("Пульс всегда плавно «гуляет» вокруг своего значения на ± столько BPM (и на этажах, и на скримере, и в WhiteRoom)")]
        [SerializeField] float wobbleAmount = 5f;
        [Tooltip("Скорость колебаний: больше — чаще меняется")]
        [SerializeField] float wobbleSpeed = 0.35f;

        [Header("Скример (испуг)")]
        [Tooltip("За сколько секунд пульс разгоняется до пика при испуге (у человека — 2–4 с)")]
        [SerializeField] float screamerRiseTime = 2.5f;
        [Tooltip("Сколько секунд пульс держится на пике в PlayScreamer() без параметров")]
        [SerializeField] float screamerHold = 3f;
        [Tooltip("За сколько секунд пульс восстанавливается после испуга (PlayScreamer() и SetScreamer(false))")]
        [SerializeField] float screamerRecovery = 10f;
        [Tooltip("За сколько секунд пульс гаснет до нуля, когда Дэнни лёг на кровать (LieDownInBed)")]
        [SerializeField] float bedFadeTime = 12f;

        [Header("Звук")]
        [Tooltip("Звук одного удара. Пусто — генерируется простой «тук-тук»")]
        [SerializeField] AudioClip beatClip;
        [Tooltip("Громкость удара при рассудке 100")]
        [Range(0f, 1f)] [SerializeField] float volumeAtFullSanity = 0.25f;
        [Tooltip("Громкость удара при рассудке 0")]
        [Range(0f, 1f)] [SerializeField] float volumeAtZeroSanity = 0.85f;
        [Tooltip("Громкость удара на пике скримера («оглушительный» пульс)")]
        [Range(0f, 1f)] [SerializeField] float screamerVolume = 1f;
        [Tooltip("Громкость удара в WhiteRoom (там слышен только пульс)")]
        [Range(0f, 1f)] [SerializeField] float scriptedVolume = 0.6f;

        /// <summary>Удар сердца (иконка в HUD, лампы в Room_101 мигают в такт).</summary>
        public event Action OnBeat;

        /// <summary>Текущий BPM для UI.</summary>
        public float Bpm { get; private set; } = 70f;

        /// <summary>Минимальный пульс этажей (рассудок 100) — пульс в WhiteRoom.</summary>
        public float MinBpm => minBpm;

        /// <summary>Максимальный пульс от рассудка (рассудок 0).</summary>
        public float MaxBpm => maxBpm;

        /// <summary>Потолок спайка галлюцинации (140).</summary>
        public float SpikeMaxBpm => spikeMaxBpm;

        /// <summary>Абсолютный потолок пульса (пик скримера).</summary>
        public float ScreamerMaxBpm => screamerMaxBpm;

        /// <summary>Включён ли сценарный режим (WhiteRoom).</summary>
        public bool Scripted { get; private set; }

        /// <summary>Идёт ли испуг (разгон или пик скримера; восстановление сюда не входит).</summary>
        public bool Screamer => screamer == ScreamerPhase.Rise || screamer == ScreamerPhase.Hold;

        enum ScreamerPhase { None, Rise, Hold, Recover }

        ScreamerPhase screamer;
        float phaseStart, phaseFrom, screamerPeak, holdEnd, recoverDuration;

        float scriptedBpm;
        bool fading;
        float fadeFrom, fadeTo, fadeStart, fadeDuration;

        float spikeAdd, spikeStart, spikeDuration;
        float beatPhase;
        bool wasSilent;
        float noiseSeed;
        // Уровень пульса без колебаний: вся логика ведёт его, а Bpm = level + колебания.
        float level = 70f;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;
            Bpm = level = minBpm;
            noiseSeed = UnityEngine.Random.value * 100f;
            if (beatClip == null) beatClip = CreateFallbackBeat();
        }

        // ------------------------------------------------------------ API

        /// <summary>
        /// Временно поднять пульс на addBpm (галлюцинация): за spikeRise до пика, дальше
        /// по кривой успокоения спадает к концу seconds. Выше spikeMaxBpm (140) спайк не поднимает.
        /// Новый спайк не ослабляет более сильный текущий.
        /// </summary>
        public void Spike(float addBpm, float seconds)
        {
            if (CurrentSpike() > addBpm) return;
            spikeAdd = addBpm;
            spikeStart = Time.time;
            spikeDuration = Mathf.Max(spikeRise + 0.1f, seconds);
            Debug.Log($"[Heartbeat] Spike +{addBpm} for {seconds}s");
        }

        /// <summary>
        /// Скример без ограничения по времени: как при сильном испуге пульс за screamerRiseTime
        /// разгоняется до 160…180 и держится там, слегка колеблясь. Выкл — восстановление
        /// за screamerRecovery секунд к значению от рассудка.
        /// </summary>
        public void SetScreamer(bool on)
        {
            if (on) StartScreamer(float.PositiveInfinity, screamerRecovery);
            else if (Screamer) StartRecovery();
            Debug.Log($"[Heartbeat] Screamer={on}");
        }

        /// <summary>Скример одним вызовом с настройками из инспектора (screamerHold, screamerRecovery).</summary>
        public void PlayScreamer() => PlayScreamer(screamerHold, screamerRecovery);

        /// <summary>
        /// Скример одним вызовом: разгон до 160…180, пик держится holdSeconds,
        /// затем за recoverySeconds пульс медленно, по-человечески, возвращается к значению от рассудка.
        /// </summary>
        public void PlayScreamer(float holdSeconds, float recoverySeconds)
        {
            StartScreamer(holdSeconds, recoverySeconds);
            Debug.Log($"[Heartbeat] PlayScreamer hold {holdSeconds}s, recovery {recoverySeconds}s");
        }

        /// <summary>Сценарный режим: BPM задаётся только через SetBpm / FadeBpm. Выкл — снова от рассудка.</summary>
        public void SetScripted(bool on)
        {
            Scripted = on;
            scriptedBpm = level;
            fading = false;
            Debug.Log($"[Heartbeat] Scripted={on}");
        }

        /// <summary>Задать BPM (сценарный режим); пульс придёт к нему с естественной скоростью. 0 — сердце останавливается.</summary>
        public void SetBpm(float bpm)
        {
            scriptedBpm = Mathf.Max(0f, bpm);
            fading = false;
            Debug.Log($"[Heartbeat] Bpm={bpm}");
        }

        /// <summary>
        /// Вести пульс от текущего значения к target за seconds (сценарный режим) по кривой успокоения.
        /// Кровать в WhiteRoom: <c>FadeBpm(0, seconds)</c> — удары всё реже, ровно к концу — остановка.
        /// </summary>
        public void FadeBpm(float target, float seconds)
        {
            fadeFrom = level;
            fadeTo = Mathf.Max(0f, target);
            fadeStart = Time.time;
            fadeDuration = Mathf.Max(0.01f, seconds);
            fading = true;
            Debug.Log($"[Heartbeat] Fade {Bpm:0} → {target} за {seconds}s");
        }

        /// <summary>Дэнни лёг на кровать в WhiteRoom: пульс гаснет до нуля за bedFadeTime.</summary>
        public void LieDownInBed()
        {
            if (!Scripted) SetScripted(true);
            FadeBpm(0f, bedFadeTime);
        }

        /// <summary>Вход в WhiteRoom: сценарный режим, скример и спайки сброшены, пульс — минимальный этажей (MinBpm).</summary>
        public void EnterWhiteRoom()
        {
            screamer = ScreamerPhase.None;
            spikeAdd = 0f;
            SetScripted(true);
            SetBpm(minBpm);
        }

        // ------------------------------------------------------------ расчёт

        void StartScreamer(float holdSeconds, float recoverySeconds)
        {
            screamer = ScreamerPhase.Rise;
            phaseStart = Time.time;
            phaseFrom = level;
            // Пик — с запасом от краёв: колебания вокруг него не выйдут за 160…180.
            float margin = Mathf.Min(wobbleAmount, (screamerMaxBpm - screamerMinBpm) / 2f);
            screamerPeak = UnityEngine.Random.Range(screamerMinBpm + margin, screamerMaxBpm - margin);
            holdEnd = holdSeconds;
            recoverDuration = Mathf.Max(0.01f, recoverySeconds);
            // Первый удар испуга — почти сразу, не дожидаясь конца текущего интервала.
            beatPhase = Mathf.Max(beatPhase, 0.9f);
        }

        void StartRecovery()
        {
            screamer = ScreamerPhase.Recover;
            phaseStart = Time.time;
            phaseFrom = level;
        }

        void Update()
        {
            if (Scripted) UpdateScripted();
            else if (screamer != ScreamerPhase.None) UpdateScreamer();
            else
            {
                Approach(SanityTarget());
                // Вне скримера пульс не выше потолка спайка (140) — даже с колебаниями.
                Bpm = Mathf.Min(Mathf.Min(screamerMaxBpm, spikeMaxBpm), level + Wobble());
            }

            if (Bpm <= 0f) { beatPhase = 0f; return; }
            bool silent = Silent;
            // Вышли в меню — обрываем недозвучавший удар, чтобы он не «хвостом» ушёл в меню.
            if (silent && !wasSilent && PlayerController.Instance == null && AudioManager.Instance != null)
                AudioManager.Instance.StopHeartbeat();
            wasSilent = silent;
            if (silent) return;
            beatPhase += Time.deltaTime * Bpm / 60f;
            if (beatPhase >= 1f)
            {
                // Живое сердце не бьёт как метроном: следующий удар чуть раньше или позже.
                beatPhase = UnityEngine.Random.Range(-beatVariability, beatVariability);
                Beat();
            }
        }

        void UpdateScripted()
        {
            if (!fading)
            {
                Approach(scriptedBpm);
                // Иначе экспонента «тикает» к нулю вечно; остановившееся сердце не «гуляет».
                if (scriptedBpm <= 0f && level < 0.5f) { level = Bpm = 0f; return; }
                Bpm = Mathf.Max(0f, level + Wobble());
                return;
            }
            float t = Mathf.Clamp01((Time.time - fadeStart) / fadeDuration);
            float fall = NaturalFall(t);
            level = Mathf.Lerp(fadeFrom, fadeTo, fall);
            // Колебания затухают вместе с пульсом, чтобы на кровати он пришёл ровно к нулю.
            float wobbleLeft = fadeTo <= 0f ? 1f - fall : 1f;
            Bpm = Mathf.Max(0f, level + Wobble() * wobbleLeft);
            if (t >= 1f)
            {
                fading = false;
                scriptedBpm = fadeTo;
            }
        }

        void UpdateScreamer()
        {
            float now = Time.time;
            switch (screamer)
            {
                case ScreamerPhase.Rise:
                {
                    // Испуг: выброс адреналина — быстро в начале, у пика замедляется (ease-out), без скачка.
                    float t = Mathf.Clamp01((now - phaseStart) / screamerRiseTime);
                    float ease = 1f - Mathf.Pow(1f - t, 3f);
                    level = Mathf.Lerp(phaseFrom, screamerPeak, ease);
                    Bpm = Mathf.Min(screamerMaxBpm, level + Wobble() * ease);
                    if (t >= 1f)
                    {
                        screamer = ScreamerPhase.Hold;
                        phaseStart = now;
                    }
                    break;
                }
                case ScreamerPhase.Hold:
                    level = screamerPeak;
                    Bpm = Mathf.Clamp(level + Wobble(), screamerMinBpm, screamerMaxBpm);
                    if (now - phaseStart >= holdEnd) StartRecovery();
                    break;
                case ScreamerPhase.Recover:
                {
                    float t = Mathf.Clamp01((now - phaseStart) / recoverDuration);
                    level = Mathf.Lerp(phaseFrom, SanityTarget(), NaturalFall(t));
                    Bpm = Mathf.Min(screamerMaxBpm, level + Wobble());
                    if (t >= 1f) screamer = ScreamerPhase.None;
                    break;
                }
            }
        }

        /// <summary>Экспоненциальное приближение к цели: вверх быстрее (riseTime), вниз медленнее (fallTime).</summary>
        void Approach(float target)
        {
            float tau = target > level ? riseTime : fallTime;
            level += (target - level) * (1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, tau)));
        }

        /// <summary>
        /// Ударов не слышно: в главном меню и прологе (в сцене нет игрока — как и HUD у P3)
        /// и на паузе (PauseMenuUI ставит timeScale 0 и AudioListener.pause). Цифра пульса при этом считается дальше.
        /// </summary>
        public bool Silent => PlayerController.Instance == null || Time.timeScale <= 0f || AudioListener.pause;

        float Sanity01 => SanityManager.Instance != null ? SanityManager.Instance.Current01 : 1f;

        float SanityTarget()
        {
            float baseBpm = Mathf.Lerp(maxBpm, minBpm, Sanity01);
            // Спайк галлюцинации упирается в spikeMaxBpm, но не опускает пульс, если он уже выше.
            float spiked = Mathf.Min(baseBpm + CurrentSpike(), Mathf.Max(baseBpm, spikeMaxBpm));
            return Mathf.Min(screamerMaxBpm, spiked);
        }

        /// <summary>
        /// Плавные колебания в пределах ±wobbleAmount. Perlin редко доходит до краёв,
        /// поэтому усиливаем в 1,6 раза и обрезаем — так размах реально доходит до ±5.
        /// </summary>
        float Wobble() => wobbleAmount * Mathf.Clamp(Noise(wobbleSpeed) * 1.6f, -1f, 1f);

        /// <summary>Гладкий шум −1..1 (Perlin): медленно «плавает», без скачков.</summary>
        float Noise(float speed) => Mathf.PerlinNoise(Time.time * speed, noiseSeed) * 2f - 1f;

        float CurrentSpike()
        {
            if (spikeAdd <= 0f) return 0f;
            float elapsed = Time.time - spikeStart;
            if (elapsed >= spikeDuration) { spikeAdd = 0f; return 0f; }
            if (elapsed < spikeRise) return spikeAdd * Mathf.SmoothStep(0f, 1f, elapsed / spikeRise);
            // После пика — та же кривая успокоения, что после скримера.
            float t = (elapsed - spikeRise) / (spikeDuration - spikeRise);
            return spikeAdd * (1f - NaturalFall(t));
        }

        /// <summary>
        /// Доля пройденного пути спада 0..1 за нормированное время t 0..1.
        /// Кривая успокоения «без перелёта» (как у затухающего маятника): мягкий старт без рывка,
        /// основное падение — в первой половине, дальше длинный пологий хвост. Нормирована,
        /// чтобы при t = 1 ровно прийти к цели (на кровати — ровно к нулю).
        /// </summary>
        float NaturalFall(float t)
        {
            float k = fallSharpness;
            float end = 1f - (1f + k) * Mathf.Exp(-k);
            return Mathf.Clamp01((1f - (1f + k * t) * Mathf.Exp(-k * t)) / end);
        }

        void Beat()
        {
            float volume;
            if (Scripted) volume = scriptedVolume;
            else if (Screamer) volume = screamerVolume;
            else volume = Mathf.Lerp(volumeAtZeroSanity, volumeAtFullSanity, Sanity01);
            // При восстановлении после скримера громкость тоже спадает постепенно.
            if (screamer == ScreamerPhase.Recover)
                volume = Mathf.Lerp(screamerVolume, volume, NaturalFall(Mathf.Clamp01((Time.time - phaseStart) / recoverDuration)));

            // Во время мини-игры (Tiny UFO на автомате) пульс Дэнни не слышен — у игры свой звук.
            bool miniGame = MiniGameHost.Instance != null && MiniGameHost.Instance.IsOpen;
            if (!miniGame && AudioManager.Instance != null) AudioManager.Instance.PlayHeartbeat(beatClip, volume);
            OnBeat?.Invoke();
        }

        /// <summary>
        /// Заглушка до прихода настоящего звука: два глухих низких удара («тук-тук»),
        /// синус с затуханием. Генерируется один раз при старте.
        /// </summary>
        static AudioClip CreateFallbackBeat()
        {
            const int rate = 44100;
            const float length = 0.45f;
            var data = new float[(int)(rate * length)];
            AddThump(data, rate, 0f, 58f, 1f);
            AddThump(data, rate, 0.16f, 48f, 0.7f);
            var clip = AudioClip.Create("Heartbeat_Fallback", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void AddThump(float[] data, int rate, float start, float freq, float amp)
        {
            int from = (int)(start * rate);
            for (int i = from; i < data.Length; i++)
            {
                float t = (i - from) / (float)rate;
                // Быстрая атака (5 мс) и экспоненциальное затухание — похоже на глухой удар.
                float env = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-t * 18f);
                data[i] += amp * env * Mathf.Sin(2f * Mathf.PI * freq * t);
            }
        }
    }
}
