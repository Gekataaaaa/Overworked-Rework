using TMPro;
using UnityEngine.UI;
using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Индикатор пульса: висит на объекте Heartbeat в HUD (вёрстка P3: Icon + BPM под шкалой рассудка).
    /// Пишет «72 BPM», иконка — нарисованное сердце: вздрагивает на каждый удар HeartbeatManager.OnBeat
    /// и плавно темнеет при низком пульсе / краснеет при высоком (градиент colorKeys).
    /// </summary>
    public class HeartbeatUI : MonoBehaviour
    {
        [Tooltip("Текст с числом BPM. Пусто — берётся дочерний объект BPM")]
        [SerializeField] TMP_Text bpmText;

        [Tooltip("Иконка сердца — масштабируется на каждом ударе. Пусто — берётся дочерний объект Icon")]
        [SerializeField] RectTransform icon;

        [Tooltip("Во сколько раз иконка увеличивается в момент удара")]
        [SerializeField] float pulseScale = 1.25f;

        [Tooltip("За сколько секунд иконка возвращается к обычному размеру")]
        [SerializeField] float pulseTime = 0.15f;

        [Tooltip("Самому встать под шкалой рассудка из того же Canvas. В HUD P3 место уже задано вёрсткой — выключено")]
        [SerializeField] bool placeUnderSanityBar;

        [Tooltip("Зазор между шкалой рассудка и индикатором пульса, пиксели HUD")]
        [SerializeField] float gapUnderSanityBar = 10f;

        [Header("Сердце")]
        [Tooltip("Нарисованное сердце: контур чёрный, заливка белая и красится цветом. Пусто — Resources/Heart")]
        [SerializeField] Sprite heartSprite;

        [Tooltip("Цвет заливки по пульсу: чем ниже BPM, тем темнее. Между ключами — плавный переход; ключи по возрастанию BPM")]
        [SerializeField] BpmColor[] colorKeys =
        {
            new BpmColor(30f, Hex(0x27070A)),
            new BpmColor(70f, Hex(0x701E26)),
            new BpmColor(80f, Hex(0x7E1D27)),
            new BpmColor(90f, Hex(0x8F1B26)),
            new BpmColor(100f, Hex(0xA31E2B)),
            new BpmColor(115f, Hex(0xAE1B2A)),
            new BpmColor(140f, Hex(0xC41C2D)),
            new BpmColor(165f, Hex(0xDD0F24)),
        };

        [Tooltip("За сколько секунд цвет сердца примерно догоняет пульс (сглаживает колебания ±5 BPM)")]
        [SerializeField] float colorSmoothTime = 0.8f;

        /// <summary>Ключ градиента: при пульсе bpm сердце окрашено в color.</summary>
        [System.Serializable]
        public struct BpmColor
        {
            [Tooltip("Пульс, BPM")] public float bpm;
            [Tooltip("Цвет заливки сердца при этом пульсе")] public Color color;
            public BpmColor(float bpm, Color color) { this.bpm = bpm; this.color = color; }
        }

        bool subscribed;
        float pulse;
        int shownBpm = -1;
        Image iconImage;
        float colorBpm = -1f;
        float colorVelocity;

        // HUD P3 пересобирается меню Build HUD — ссылки ищем по именам детей (Icon, BPM), если не заданы.
        void Awake()
        {
            if (bpmText == null)
            {
                var t = transform.Find("BPM");
                if (t != null) bpmText = t.GetComponent<TMP_Text>();
            }
            if (icon == null) icon = transform.Find("Icon") as RectTransform;

            // Спрайт берём из Resources: Build HUD ставит на Icon круг-заглушку и ссылки в инспекторе не переживают пересборку.
            if (heartSprite == null) heartSprite = Resources.Load<Sprite>("Heart");
            iconImage = icon != null ? icon.GetComponent<Image>() : null;
            if (iconImage != null && heartSprite != null)
            {
                iconImage.sprite = heartSprite;
                iconImage.type = Image.Type.Simple;
                iconImage.preserveAspect = true;
            }
        }

        void OnEnable() => TrySubscribe();

        void Start()
        {
            TrySubscribe();
            if (placeUnderSanityBar) PlaceUnderSanityBar();
        }

        void PlaceUnderSanityBar()
        {
            var canvas = GetComponentInParent<Canvas>();
            var bar = canvas != null ? canvas.rootCanvas.GetComponentInChildren<SanityBarUI>(true) : null;
            if (bar == null) return;
            var barRt = (RectTransform)bar.transform;
            var rt = (RectTransform)transform;
            // Те же якоря и левый край, что у шкалы, чтобы при любом разрешении они шли друг под другом.
            rt.anchorMin = barRt.anchorMin;
            rt.anchorMax = barRt.anchorMax;
            // Pivot — левый верх: ниже считаем левый край и низ шкалы.
            rt.pivot = new Vector2(0f, 1f);
            float barBottom = barRt.anchoredPosition.y - barRt.rect.height * barRt.pivot.y;
            rt.anchoredPosition = new Vector2(barRt.anchoredPosition.x - barRt.rect.width * barRt.pivot.x, barBottom - gapUnderSanityBar);
        }

        void OnDisable()
        {
            if (subscribed && HeartbeatManager.Instance != null) HeartbeatManager.Instance.OnBeat -= OnBeat;
            subscribed = false;
        }

        void TrySubscribe()
        {
            if (subscribed || HeartbeatManager.Instance == null) return;
            HeartbeatManager.Instance.OnBeat += OnBeat;
            subscribed = true;
        }

        void OnBeat() => pulse = 1f;

        void Update()
        {
            if (HeartbeatManager.Instance == null) return;
            // Текст пересобираем только при смене числа: TMP дорого перестраивает меш.
            int bpm = Mathf.RoundToInt(HeartbeatManager.Instance.Bpm);
            if (bpm != shownBpm && bpmText != null)
            {
                shownBpm = bpm;
                bpmText.text = $"{bpm} BPM";
            }

            UpdateColor(HeartbeatManager.Instance.Bpm);

            if (icon == null) return;
            pulse = Mathf.Max(0f, pulse - Time.unscaledDeltaTime / pulseTime);
            icon.localScale = Vector3.one * Mathf.Lerp(1f, pulseScale, pulse);
        }

        // Цвет ведём не от сырого BPM, а от сглаженного: иначе сердце мерцало бы от колебаний пульса.
        void UpdateColor(float bpm)
        {
            if (iconImage == null || colorKeys == null || colorKeys.Length == 0) return;
            if (colorBpm < 0f) colorBpm = bpm;
            else colorBpm = Mathf.SmoothDamp(colorBpm, bpm, ref colorVelocity, colorSmoothTime / 3f, Mathf.Infinity, Time.unscaledDeltaTime);
            iconImage.color = Evaluate(colorBpm);
        }

        Color Evaluate(float bpm)
        {
            if (bpm <= colorKeys[0].bpm) return colorKeys[0].color;
            for (int i = 1; i < colorKeys.Length; i++)
            {
                if (bpm > colorKeys[i].bpm) continue;
                var a = colorKeys[i - 1];
                var b = colorKeys[i];
                return Color.Lerp(a.color, b.color, Mathf.InverseLerp(a.bpm, b.bpm, bpm));
            }
            return colorKeys[colorKeys.Length - 1].color;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
