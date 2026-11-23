using UnityEngine;
using UnityEngine.UI;

namespace Overworked
{
    /// <summary>
    /// Шкала рассудка в левом верхнем углу HUD — нарисованная от руки полоса без процентов.
    /// Висит на объекте SanityBar в HUD (вёрстка P3). Сама достраивает себе вид поверх того,
    /// что собрал Build HUD: Frame (рамка с тёмной подложкой), Trail (след потери) и Fill (заполнение).
    /// При падении рассудка: заполнение плавно стекает, за ним тянется красный след потерянной части,
    /// полоса вздрагивает; на низком рассудке слегка дрожит и краснеет.
    /// Слева от полосы — иконка рассудка (голова со спиралью, Resources/SanityIcon): её белая заливка всегда
    /// того же цвета, что заполнение полосы. Иконка — в одной колонке с сердцем пульса (тот же размер и левый край).
    /// </summary>
    public class SanityBarUI : MonoBehaviour
    {
        // Спрайты лежат в Art/UI/Sanity/Resources: Build HUD пересоздаёт SanityBar, и ссылки в инспекторе слетают.
        const string FrameResource = "SanityBar_Frame";
        const string FillResource = "SanityBar_Fill";
        const string IconResource = "SanityIcon";

        [Header("Вид")]
        [Tooltip("Рамка с пустой подложкой. Пусто — Resources/SanityBar_Frame")]
        [SerializeField] Sprite frameSprite;

        [Tooltip("Маска заполнения (белая, красится цветом). Пусто — Resources/SanityBar_Fill")]
        [SerializeField] Sprite fillSprite;

        [Tooltip("Заполнение шкалы. Пусто — берётся Image дочернего объекта Fill")]
        [SerializeField] Image fill;

        [Tooltip("Выставлять размер и позицию шкалы под пропорции картинки (2000×244)")]
        [SerializeField] bool applyLayout = true;

        [Tooltip("Позиция от левого верхнего угла HUD (если applyLayout)")]
        [SerializeField] Vector2 position = new Vector2(24f, -28f);

        [Tooltip("Размер шкалы, px (если applyLayout); держите пропорцию ≈ 8.2 : 1")]
        [SerializeField] Vector2 size = new Vector2(300f, 37f);

        [Tooltip("Иконка рассудка: контур чёрный, заливка белая и красится цветом заполнения. Пусто — Resources/SanityIcon")]
        [SerializeField] Sprite iconSprite;

        [Tooltip("Размер иконки, px — как у сердца пульса (34), чтобы они стояли одной колонкой")]
        [SerializeField] Vector2 iconSize = new Vector2(34f, 34f);

        [Tooltip("Зазор между иконкой и полосой, px. 14 — левый край полосы совпадает с текстом BPM под ней")]
        [SerializeField] float iconGap = 14f;

        [Tooltip("Где в картинке начинается и кончается светлая часть (доля ширины): 0 % и 100 % рассудка")]
        [SerializeField] Vector2 fillRange = new Vector2(0.0145f, 0.9855f);

        [Header("Цвета")]
        [Tooltip("Цвет заполнения (как светлая часть в макете)")]
        [SerializeField] Color fillColor = new Color(0.88f, 0.88f, 0.88f);

        [Tooltip("Цвет заполнения при нулевом рассудке; к нему шкала уходит ниже lowThreshold")]
        [SerializeField] Color lowColor = new Color(0.72f, 0.36f, 0.33f);

        [Tooltip("Ниже этой доли рассудка шкала краснеет и начинает дрожать")]
        [Range(0f, 1f)]
        [SerializeField] float lowThreshold = 0.3f;

        [Tooltip("Цвет следа — потерянной части шкалы")]
        [SerializeField] Color trailColor = new Color(0.6f, 0.1f, 0.08f);

        [Header("Падение рассудка")]
        [Tooltip("Сколько секунд след стоит на старом значении, прежде чем начать догонять")]
        [SerializeField] float trailHold = 0.5f;

        [Tooltip("За сколько секунд след примерно догоняет заполнение")]
        [SerializeField] float trailCatchTime = 0.8f;

        [Tooltip("Сила вздрагивания при падении на 100 пунктов, px (меньшее падение — пропорционально)")]
        [SerializeField] float shakeStrength = 14f;

        [Tooltip("Минимальная сила вздрагивания, px — чтобы даже маленькая потеря была заметна")]
        [SerializeField] float shakeMin = 3f;

        [Tooltip("Длительность вздрагивания, сек")]
        [SerializeField] float shakeTime = 0.45f;

        [Tooltip("Насколько шкала «подпрыгивает» в размере при падении (0.05 = на 5 %)")]
        [SerializeField] float punchScale = 0.05f;

        [Tooltip("Постоянная дрожь на нулевом рассудке, px (ниже lowThreshold нарастает от 0)")]
        [SerializeField] float lowTremor = 1.5f;

        RectTransform rect;
        Image trail;
        Image icon;
        Vector2 basePosition;

        bool subscribed;
        float lastTarget01 = 1f;
        float trailValue = 1f;
        float trailVelocity;
        float holdTimer;
        float shakeTimer;
        float shakeAmplitude;

        // HUD P3 пересобирается меню Build HUD — ссылка может слететь, поэтому ищем Fill по имени у себя в детях.
        void Awake()
        {
            rect = (RectTransform)transform;
            if (frameSprite == null) frameSprite = Resources.Load<Sprite>(FrameResource);
            if (fillSprite == null) fillSprite = Resources.Load<Sprite>(FillResource);
            if (iconSprite == null) iconSprite = LoadSprite(IconResource);
            if (fill == null)
            {
                var child = transform.Find("Fill");
                if (child != null) fill = child.GetComponent<Image>();
            }

            if (applyLayout)
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                // position — левый край всей группы: сначала иконка, правее — полоса.
                float shift = iconSprite != null ? iconSize.x + iconGap : 0f;
                rect.anchoredPosition = position + new Vector2(shift, 0f);
                rect.sizeDelta = size;
            }
            basePosition = rect.anchoredPosition;

            BuildLayers();
            HotbarUI.Attach(rect);   // хотбар — правее шкалы, сверху экрана
        }

        // Порядок снизу вверх: Frame → Trail → Fill. Светлая маска не заходит на контур рамки, поэтому рамку сверху класть не нужно.
        void BuildLayers()
        {
            var frame = Layer("Frame", frameSprite);
            frame.type = Image.Type.Simple;
            frame.transform.SetAsFirstSibling();

            if (fill == null) fill = Layer("Fill", fillSprite);
            ConfigureFilled(fill);

            trail = Layer("Trail", fillSprite);
            ConfigureFilled(trail);
            trail.transform.SetSiblingIndex(fill.transform.GetSiblingIndex());

            BuildIcon();
        }

        // Иконка — дочерний объект полосы (трясётся и вздрагивает вместе с ней), левее её края на iconGap.
        void BuildIcon()
        {
            if (iconSprite == null) return;
            var t = transform.Find("Icon");
            icon = t != null ? t.GetComponent<Image>() : null;
            if (icon == null)
            {
                var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                icon = go.GetComponent<Image>();
            }
            var rt = icon.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-iconGap, 0f);
            rt.sizeDelta = iconSize;
            rt.localScale = Vector3.one;
            icon.sprite = iconSprite;
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        // Спрайт из Resources; если картинка импортирована не как спрайт — собираем спрайт из текстуры.
        static Sprite LoadSprite(string name)
        {
            var sprite = Resources.Load<Sprite>(name);
            if (sprite != null) return sprite;
            var tex = Resources.Load<Texture2D>(name);
            return tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)) : null;
        }

        Image Layer(string name, Sprite sprite)
        {
            var t = transform.Find(name);
            var img = t != null ? t.GetComponent<Image>() : null;
            if (img == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                img = go.GetComponent<Image>();
            }
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        void ConfigureFilled(Image img)
        {
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            img.sprite = fillSprite;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.raycastTarget = false;
        }

        void OnEnable() => TrySubscribe();

        // Start — запасной вариант: HUD может включиться раньше, чем _Systems создаст SanityManager.
        void Start() => TrySubscribe();

        void OnDisable()
        {
            if (subscribed && SanityManager.Instance != null) SanityManager.Instance.OnSanitySnapped -= Snap;
            subscribed = false;
            shakeTimer = 0f;
            rect.anchoredPosition = basePosition;
            rect.localScale = Vector3.one;
        }

        void TrySubscribe()
        {
            if (subscribed || SanityManager.Instance == null) return;
            SanityManager.Instance.OnSanitySnapped += Snap;
            subscribed = true;
            // Шкала была скрыта (не на этаже, онбординг) — прошлые потери не проигрываем, просто встаём на значение.
            Snap(SanityManager.Instance.Current01);
        }

        // Мгновенная установка: без следа и вздрагивания.
        void Snap(float value01)
        {
            trailValue = value01;
            trailVelocity = 0f;
            holdTimer = 0f;
            lastTarget01 = SanityManager.Instance != null ? SanityManager.Instance.Target / 100f : value01;
            Draw(value01, 0f);
        }

        void Update()
        {
            var sanity = SanityManager.Instance;
            if (!subscribed || sanity == null || fill == null) return;

            float value = sanity.Current01;
            float target = sanity.Target / 100f;

            // Новая цель ниже прежней — ключевой момент (галлюцинация): вздрогнуть и придержать след.
            if (target < lastTarget01 - 0.001f) OnDrop(lastTarget01 - target);
            lastTarget01 = target;

            if (value >= trailValue)
            {
                // Рассудок растёт (Белая комната) — след не нужен.
                trailValue = value;
                trailVelocity = 0f;
            }
            else if (holdTimer > 0f)
            {
                holdTimer -= Time.deltaTime;
            }
            else
            {
                trailValue = Mathf.SmoothDamp(trailValue, value, ref trailVelocity, trailCatchTime / 3f);
                if (trailValue - value < 0.001f) trailValue = value;
            }

            Draw(value, Time.deltaTime);
        }

        void OnDrop(float amount01)
        {
            holdTimer = trailHold;
            shakeAmplitude = Mathf.Max(shakeMin, amount01 * shakeStrength);
            shakeTimer = shakeTime;
        }

        void Draw(float value, float dt)
        {
            fill.fillAmount = Mathf.Lerp(fillRange.x, fillRange.y, value);
            trail.fillAmount = Mathf.Lerp(fillRange.x, fillRange.y, trailValue);

            float low = lowThreshold > 0f ? Mathf.Clamp01(1f - value / lowThreshold) : 0f;
            fill.color = Color.Lerp(fillColor, lowColor, low);
            if (icon != null) icon.color = fill.color;   // заливка иконки — всегда цвета заполнения

            // След чуть пульсирует, пока виден, — глаз цепляется за потерянный кусок.
            var tc = trailColor;
            tc.a *= 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 18f);
            trail.color = tc;

            Vector2 offset = Vector2.zero;
            float scale = 1f;
            if (shakeTimer > 0f)
            {
                shakeTimer -= dt;
                float k = Mathf.Clamp01(shakeTimer / shakeTime);
                offset += Random.insideUnitCircle * shakeAmplitude * k * k;
                scale += punchScale * k * k * Mathf.Clamp01(shakeAmplitude / shakeStrength);
            }
            if (low > 0f)
            {
                // Перлин, а не Random: на низком рассудке шкала «плывёт», а не трясётся.
                float s = Time.unscaledTime * 9f;
                offset += new Vector2(Mathf.PerlinNoise(s, 0f) - 0.5f, Mathf.PerlinNoise(0f, s) - 0.5f) * (2f * lowTremor * low);
            }
            rect.anchoredPosition = basePosition + offset;
            rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
