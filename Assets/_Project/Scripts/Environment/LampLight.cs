using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Overworked
{
    /// <summary>
    /// Лампа на потолке: настоящий 2D-свет (Light2D) и её нарисованные части — трубка и конус света.
    /// Картинки повторяют яркость света: когда галлюцинация (LightFlickerVisual P4) или мерцание
    /// гасит/вспыхивает Light2D, трубка темнеет, а конус тает и проявляется вместе с ним.
    /// Так «лампы мигают» по-настоящему, а не только вспышка на экране.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class LampLight : MonoBehaviour
    {
        [Tooltip("Трубка лампы (спрайт Light_Tube_On): темнеет вместе со светом")]
        [SerializeField] SpriteRenderer tube;

        [Tooltip("Конус света под лампой (спрайт Light_Cone): прозрачнеет вместе со светом")]
        [SerializeField] SpriteRenderer cone;

        [Tooltip("Яркость трубки, когда свет полностью погас (доля обычной)")]
        [Range(0f, 1f)] [SerializeField] float tubeDarkLevel = 0.25f;

        [Tooltip("Цвет трубки, когда свет выключен совсем (intensity 0): светло-серый, на тон светлее стены коридора")]
        [SerializeField] Color tubeOffColor = new Color(0.38f, 0.39f, 0.38f);

        [Tooltip("Скачок яркости за кадр (доля обычной), который считается миганием, — лёгкое дрожание LightFlicker меньше")]
        [Range(0.05f, 1f)] [SerializeField] float flickerJump = 0.3f;

        [Tooltip("Сколько секунд после скачка лампа ещё считается мигающей (звук не рвётся между вспышками)")]
        [SerializeField] float flickerHold = 0.3f;

        /// <summary>Трубка лампы (её яркостью управляет сама лампа).</summary>
        public SpriteRenderer Tube => tube;

        /// <summary>Конус света лампы.</summary>
        public SpriteRenderer Cone => cone;

        /// <summary>Включённые лампы сцены (по ним LampHum решает, гудят ли лампы в кадре).</summary>
        public static readonly List<LampLight> Active = new List<LampLight>();

        // Enter Play Mode без перезагрузки домена: список не должен тянуть лампы прошлого запуска.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetActive() => Active.Clear();

        /// <summary>Лампа сейчас мигает (были резкие скачки яркости за последние flickerHold секунд) — LampHum трещит.</summary>
        public bool IsFlickering => Time.time < flickerUntil;

        Light2D lamp;
        float baseIntensity;
        float lastLevel = -1f, flickerUntil;
        Color tubeColor, coneColor;

        /// <summary>
        /// Насколько лампа сейчас горит: 1 — как обычно, 0 — погасла (вспышка ярче обычного — тоже 1).
        /// По ней BackgroundDimmer затемняет фон.
        /// </summary>
        public float Level
        {
            get
            {
                if (lamp == null) return 1f;
                return lamp.enabled ? Mathf.Clamp01(lamp.intensity / baseIntensity) : 0f;
            }
        }

        void Awake()
        {
            lamp = GetComponent<Light2D>();
            baseIntensity = Mathf.Max(0.0001f, lamp.intensity);
            if (tube != null) tubeColor = tube.color;
            if (cone != null) coneColor = cone.color;
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        // В LateUpdate: мерцание P4 меняет intensity в Update/корутине этого же кадра.
        void LateUpdate()
        {
            float k = lamp.enabled ? lamp.intensity / baseIntensity : 0f;
            // Мигание узнаём по самой лампе: кто бы ни дёргал свет (LightFlicker, галлюцинация, сценарий).
            float level = Mathf.Clamp01(k);
            if (lastLevel >= 0f && Mathf.Abs(level - lastLevel) >= flickerJump) flickerUntil = Time.time + flickerHold;
            lastLevel = level;
            if (tube != null)
            {
                if (k <= 0.001f)
                {
                    // Свет погашен полностью (мигание при появлении enemy01) — трубка выключена: просто серое стекло.
                    tube.color = new Color(tubeOffColor.r, tubeOffColor.g, tubeOffColor.b, tubeColor.a);
                }
                else
                {
                    float t = Mathf.Lerp(tubeDarkLevel, 1f, Mathf.Clamp01(k));
                    tube.color = new Color(tubeColor.r * t, tubeColor.g * t, tubeColor.b * t, tubeColor.a);
                }
            }
            if (cone != null)
            {
                // Вспышка ярче обычного (k > 1) — конус чуть плотнее, но не больше 1.
                float a = Mathf.Clamp01(coneColor.a * Mathf.Clamp(k, 0f, 1.3f));
                cone.color = new Color(coneColor.r, coneColor.g, coneColor.b, a);
            }
        }
    }
}
