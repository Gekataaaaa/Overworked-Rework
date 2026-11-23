using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Overworked
{
    /// <summary>
    /// Фонарик Дэнни (F) — 2D Spot Light, который держится у игрока и светит туда, куда он смотрит.
    /// Лежит объектом в сцене этажа (Floor_02, Floor_01); на Floor_03 его нет.
    /// На низком рассудке свет иногда мерцает (паспорт, раздел 19).
    /// </summary>
    public class FlashlightController : SingletonBehaviour<FlashlightController>
    {
        [Header("Старт")]
        [Tooltip("Доступен ли фонарик сразу при загрузке сцены (на Floor_02 — да, он уже в руках)")]
        [SerializeField] bool availableOnStart = true;
        [Tooltip("Включён ли фонарик при загрузке сцены (на Floor_02 — нет: игрок включает его сам)")]
        [SerializeField] bool onAtStart;

        [Header("Свет")]
        [Tooltip("Свет фонарика. Пусто — Spot Light создаётся автоматически дочерним объектом")]
        [SerializeField] Light2D spot;
        [Tooltip("Смещение от центра игрока: X — вперёд по взгляду, Y — высота руки")]
        [SerializeField] Vector2 handOffset = new Vector2(0.3f, 0.2f);
        [Tooltip("Яркость света")]
        [SerializeField] float intensity = 1.2f;
        [Tooltip("Дальность луча, юниты")]
        [SerializeField] float range = 9f;
        [Tooltip("Ширина луча, градусы")]
        [SerializeField] float coneAngle = 55f;
        [Tooltip("Цвет света (тёплый, чуть жёлтый)")]
        [SerializeField] Color color = new Color(1f, 0.95f, 0.8f);

        [Header("Мерцание на низком рассудке")]
        [Tooltip("Ниже какого рассудка (0..1) фонарик начинает мерцать")]
        [Range(0f, 1f)] [SerializeField] float flickerBelowSanity = 0.5f;
        [Tooltip("Сколько раз в секунду в среднем случается провал света при рассудке 0")]
        [SerializeField] float flickerRate = 1.5f;

        [Header("Звук")]
        [Tooltip("Щелчок кнопки фонарика (необязательно)")]
        [SerializeField] AudioClip toggleClip;

        /// <summary>Фонарик включили/выключили.</summary>
        public event Action<bool> OnToggled;

        /// <summary>Включён ли фонарик.</summary>
        public bool IsOn { get; private set; }

        /// <summary>Доступен ли фонарик (на Floor_03 — нет).</summary>
        public bool Available { get; private set; }

        /// <summary>
        /// Светит ли фонарик на точку: включён, точка впереди по взгляду Дэнни, не дальше maxDistance
        /// и не дальше луча (силуэт в Галлюцинации 2 реагирует на свет).
        /// </summary>
        public bool Illuminates(Vector3 point, float maxDistance)
        {
            var player = PlayerController.Instance;
            if (!IsOn || player == null) return false;
            float dx = (point.x - transform.position.x) * player.Facing;
            return dx > 0f && dx <= Mathf.Min(maxDistance, range);
        }

        float dropoutUntil;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;
            if (spot == null) spot = CreateSpot();
            Available = availableOnStart;
            IsOn = onAtStart && Available;
            spot.enabled = IsOn;
        }

        Light2D CreateSpot()
        {
            var go = new GameObject("FlashlightSpot");
            go.transform.SetParent(transform, false);
            var light = go.AddComponent<Light2D>();
            // Spot в 2D URP — это Point Light с ограниченным углом.
            light.lightType = Light2D.LightType.Point;
            light.pointLightInnerRadius = 0.5f;
            light.pointLightOuterRadius = range;
            light.pointLightInnerAngle = coneAngle * 0.6f;
            light.pointLightOuterAngle = coneAngle;
            light.intensity = intensity;
            light.color = color;
            return light;
        }

        /// <summary>Сделать фонарик доступным/недоступным; недоступный выключается.</summary>
        public void SetAvailable(bool available)
        {
            Available = available;
            if (!available && IsOn) Toggle();
        }

        /// <summary>Переключить фонарик (то же, что F). Недоступный не включается.</summary>
        public void Toggle()
        {
            if (!Available && !IsOn) return;
            IsOn = !IsOn;
            spot.enabled = IsOn;
            if (toggleClip != null && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(toggleClip);
            Debug.Log($"[Flashlight] {(IsOn ? "on" : "off")}");
            OnToggled?.Invoke(IsOn);
        }

        void Update()
        {
            // F работает, только когда игрок управляет Дэнни (не в телефоне, мини-игре, катсцене).
            var player = PlayerController.Instance;
            bool canUse = player != null && player.State == PlayerState.Normal;
            if (canUse && GameInput.Flashlight != null && GameInput.Flashlight.WasPressedThisFrame()) Toggle();

            if (IsOn) spot.intensity = intensity * FlickerFactor();
        }

        void LateUpdate()
        {
            // LateUpdate: игрок уже сдвинут в этом кадре, свет не отстаёт на кадр.
            var player = PlayerController.Instance;
            if (player == null) return;
            int facing = player.Facing;
            transform.position = player.transform.position + new Vector3(handOffset.x * facing, handOffset.y, 0f);
            // Свет 2D смотрит вдоль своей оси +Y: -90° — вправо, 90° — влево.
            // Без анимации поворота: Дэнни развернулся — луч сразу смотрит в ту же сторону.
            transform.rotation = Quaternion.Euler(0f, 0f, facing > 0 ? -90f : 90f);
        }

        float FlickerFactor()
        {
            float sanity = SanityManager.Instance != null ? SanityManager.Instance.Current01 : 1f;
            if (sanity >= flickerBelowSanity) return 1f;
            // 0 на пороге, 1 при нулевом рассудке.
            float strength = 1f - sanity / flickerBelowSanity;
            if (Time.time < dropoutUntil) return 0.15f;
            if (UnityEngine.Random.value < flickerRate * strength * Time.deltaTime)
                dropoutUntil = Time.time + UnityEngine.Random.Range(0.04f, 0.18f);
            // Лёгкая дрожь яркости — как от плохого контакта батарейки.
            return 1f - strength * 0.25f * Mathf.PerlinNoise(Time.time * 12f, 0f);
        }
    }
}
