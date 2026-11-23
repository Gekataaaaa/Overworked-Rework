using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Overworked
{
    /// <summary>
    /// Мерцание лампы (Light2D): на низком рассудке свет иногда проваливается и дрожит, по желанию —
    /// мигает в такт пульсу (Room_101). Burst(сек) — бешеное мигание по команде (галлюцинация, сценарий).
    /// Вешается на объект с Light2D (лампы этажа). idleBlinkEvery — неисправная лампа: сама мигает очередью
    /// раз в несколько секунд при любом рассудке.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class LightFlicker : MonoBehaviour
    {
        [Header("От рассудка")]
        [Tooltip("Ниже какого рассудка (0..1) лампа начинает мерцать сама (1 — всегда, 0 — никогда)")]
        [Range(0f, 1f)] [SerializeField] float flickerBelowSanity = 0.7f;
        [Tooltip("Сколько провалов света в секунду в среднем при рассудке 0")]
        [SerializeField] float dropoutRate = 2f;
        [Tooltip("Яркость во время провала (доля от обычной)")]
        [Range(0f, 1f)] [SerializeField] float dropoutLevel = 0.1f;

        [Header("В такт пульсу")]
        [Tooltip("Проваливаться на каждый удар сердца (Room_101: лампы мигают в такт пульсу)")]
        [SerializeField] bool syncToHeartbeat;
        [Tooltip("Сколько секунд длится провал на удар")]
        [SerializeField] float beatDipTime = 0.1f;

        [Header("Неисправная лампа (с любого рассудка)")]
        [Tooltip("Раз в сколько секунд (от-до) лампа сама коротко мигает очередью. 0 — выключено")]
        [SerializeField] Vector2 idleBlinkEvery = Vector2.zero;
        [Tooltip("Сколько секунд (от-до) длится одна очередь миганий")]
        [SerializeField] Vector2 idleBlinkLength = new Vector2(0.15f, 0.6f);

        [Header("Бешеное мигание (Burst)")]
        [Tooltip("Смен состояния в секунду при Burst")]
        [SerializeField] float burstRate = 14f;

        Light2D lamp;
        float baseIntensity;
        float dimUntil, burstUntil, nextBurstToggle, nextIdleBlink;
        bool burstDark;
        // Свой сдвиг шума у каждой лампы — соседние не дрожат синхронно.
        float noiseSeed;

        /// <summary>Мигать в такт пульсу (вкл/выкл из сценария).</summary>
        public bool SyncToHeartbeat
        {
            get => syncToHeartbeat;
            set => syncToHeartbeat = value;
        }

        void Awake()
        {
            lamp = GetComponent<Light2D>();
            baseIntensity = lamp.intensity;
            noiseSeed = Random.value * 100f;
            ScheduleIdleBlink();
        }

        void ScheduleIdleBlink()
        {
            if (idleBlinkEvery.y > 0f) nextIdleBlink = Time.time + Random.Range(idleBlinkEvery.x, idleBlinkEvery.y);
        }

        void OnEnable()
        {
            if (HeartbeatManager.Instance != null) HeartbeatManager.Instance.OnBeat += OnBeat;
        }

        void OnDisable()
        {
            if (HeartbeatManager.Instance != null) HeartbeatManager.Instance.OnBeat -= OnBeat;
            if (lamp != null) lamp.intensity = baseIntensity;
        }

        /// <summary>Бешеное мигание seconds секунд (Галлюцинация 3: «свет начинает бешено мигать»).</summary>
        public void Burst(float seconds) => burstUntil = Mathf.Max(burstUntil, Time.time + seconds);

        void OnBeat()
        {
            if (syncToHeartbeat) dimUntil = Time.time + beatDipTime;
        }

        void Update()
        {
            float k = 1f;
            if (idleBlinkEvery.y > 0f && Time.time >= nextIdleBlink)
            {
                Burst(Random.Range(idleBlinkLength.x, idleBlinkLength.y));
                ScheduleIdleBlink();
            }
            if (Time.time < burstUntil)
            {
                if (Time.time >= nextBurstToggle)
                {
                    burstDark = !burstDark;
                    nextBurstToggle = Time.time + Random.Range(0.5f, 1.5f) / burstRate;
                }
                k = burstDark ? dropoutLevel : 1f;
            }
            else
            {
                float sanity = SanityManager.Instance != null ? SanityManager.Instance.Current01 : 1f;
                if (sanity < flickerBelowSanity)
                {
                    // 0 на пороге, 1 при нулевом рассудке.
                    float strength = 1f - sanity / Mathf.Max(0.0001f, flickerBelowSanity);
                    if (Random.value < dropoutRate * strength * Time.deltaTime)
                        dimUntil = Mathf.Max(dimUntil, Time.time + Random.Range(0.04f, 0.2f));
                    // Лёгкое дрожание — как у старой люминесцентной лампы.
                    k = 1f - strength * 0.2f * Mathf.PerlinNoise(Time.time * 15f, noiseSeed);
                }
                if (Time.time < dimUntil) k = dropoutLevel;
            }
            lamp.intensity = baseIntensity * k;
        }
    }
}
