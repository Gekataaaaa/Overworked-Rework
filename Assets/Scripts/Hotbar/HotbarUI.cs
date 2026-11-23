using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Overworked
{
    /// <summary>
    /// Хотбар сверху экрана, правее шкалы рассудка и пульса: ряд из Hotbar.SlotCount слотов с иконками предметов.
    /// По высоте — по центру между верхом шкалы рассудка и низом пульса. Клавиши 1–5 выбирают слот
    /// (повторное нажатие снимает выделение), выбранный слот плавно чуть увеличивается.
    /// Создаётся сам из SanityBarUI (соседом SanityBar в HUD) — Build HUD его не трогает.
    /// Виден вместе со шкалой рассудка; спрайт рамки слота генерируется в коде — ассеты не нужны.
    /// </summary>
    public class HotbarUI : MonoBehaviour
    {
        const float SlotSize = 56f;
        const float SlotGap = 8f;
        const float GapAfterSanity = 32f;   // зазор от правого края шкалы рассудка, px
        const float IconPadding = 8f;
        const float SelectedScale = 1.12f;  // насколько увеличен выбранный слот
        const float ScaleSpeed = 14f;       // скорость анимации увеличения

        static readonly Color FrameColor = new Color(0.88f, 0.88f, 0.88f, 0.9f);
        static readonly Color BackColor = new Color(0f, 0f, 0f, 0.55f);

        RectTransform sanityBar;
        GameObject content;
        RectTransform[] slotRects;
        Image[] icons;

        /// <summary>Создать хотбар рядом со шкалой рассудка (один раз на HUD).</summary>
        public static void Attach(RectTransform sanityBar)
        {
            var parent = sanityBar.parent;
            if (parent == null || parent.GetComponentInChildren<HotbarUI>(true) != null) return;
            var go = new GameObject("Hotbar", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(sanityBar.GetSiblingIndex() + 1);
            go.AddComponent<HotbarUI>().Build(sanityBar);
        }

        void Build(RectTransform bar)
        {
            sanityBar = bar;
            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(Hotbar.SlotCount * SlotSize + (Hotbar.SlotCount - 1) * SlotGap, SlotSize);
            Place();

            content = new GameObject("Slots", typeof(RectTransform));
            content.transform.SetParent(transform, false);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = crt.offsetMax = Vector2.zero;

            var frame = MakeFrameSprite();
            slotRects = new RectTransform[Hotbar.SlotCount];
            icons = new Image[Hotbar.SlotCount];
            for (int i = 0; i < Hotbar.SlotCount; i++)
            {
                var slot = NewImage($"Slot{i + 1}", content.transform);
                var srt = slot.rectTransform;
                // Pivot по центру — увеличение идёт от середины слота.
                srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.anchoredPosition = new Vector2(i * (SlotSize + SlotGap) + SlotSize / 2f, 0f);
                srt.sizeDelta = new Vector2(SlotSize, SlotSize);
                slot.sprite = frame;
                slot.type = Image.Type.Sliced;
                slot.color = Color.white;
                slotRects[i] = srt;

                var icon = NewImage("Icon", slot.transform);
                var irt = icon.rectTransform;
                irt.anchorMin = Vector2.zero;
                irt.anchorMax = Vector2.one;
                irt.offsetMin = new Vector2(IconPadding, IconPadding);
                irt.offsetMax = new Vector2(-IconPadding, -IconPadding);
                icon.preserveAspect = true;
                icons[i] = icon;
            }
            Redraw();
        }

        // Пульс может встать на место в своём Start — пересчитываем позицию, когда все Awake/Start прошли.
        void Start() => Place();

        // Слева — правее шкалы рассудка; по высоте — центр между верхом шкалы и низом пульса (Heartbeat).
        void Place()
        {
            if (sanityBar == null) return;
            var bar = sanityBar;
            float right = bar.anchoredPosition.x + bar.rect.width * (1f - bar.pivot.x);
            float top = bar.anchoredPosition.y + bar.rect.height * (1f - bar.pivot.y);
            float bottom = bar.anchoredPosition.y - bar.rect.height * bar.pivot.y;
            var heart = bar.parent.Find("Heartbeat") as RectTransform;
            if (heart != null) bottom = heart.anchoredPosition.y - heart.rect.height * heart.pivot.y;
            ((RectTransform)transform).anchoredPosition = new Vector2(right + GapAfterSanity, (top + bottom) / 2f);
        }

        static Image NewImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        // Квадрат 16×16: тёмная полупрозрачная середина и светлая рамка 2 px, режется 9-slice.
        static Sprite MakeFrameSprite()
        {
            const int n = 16, b = 2;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    tex.SetPixel(x, y, x < b || y < b || x >= n - b || y >= n - b ? FrameColor : BackColor);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b + 1, b + 1, b + 1, b + 1));
        }

        void OnEnable() => Hotbar.Changed += Redraw;
        void OnDisable() => Hotbar.Changed -= Redraw;

        void Redraw()
        {
            if (icons == null) return;
            for (int i = 0; i < icons.Length; i++)
            {
                var item = Hotbar.Get(i);
                icons[i].sprite = item != null ? item.icon : null;
                icons[i].enabled = item != null && item.icon != null;
            }
        }

        // Показываем хотбар только вместе со шкалой рассудка (UIManager прячет её в меню, мини-играх, прологе).
        void LateUpdate()
        {
            bool show = sanityBar != null && sanityBar.gameObject.activeInHierarchy;
            if (content.activeSelf != show) content.SetActive(show);
            if (!show) return;

            ReadKeys();

            // Выбранный слот плавно растёт до SelectedScale, остальные возвращаются к 1.
            float k = 1f - Mathf.Exp(-ScaleSpeed * Time.unscaledDeltaTime);
            for (int i = 0; i < slotRects.Length; i++)
            {
                float target = i == Hotbar.Selected ? SelectedScale : 1f;
                float s = Mathf.Lerp(slotRects[i].localScale.x, target, k);
                slotRects[i].localScale = new Vector3(s, s, 1f);
            }
            // Выбранный — поверх соседей, чтобы увеличенная рамка не уходила под них.
            if (Hotbar.Selected >= 0) slotRects[Hotbar.Selected].SetAsLastSibling();
        }

        // Клавиши 1–5 (верхний ряд и цифровой блок) — выбор слота.
        static void ReadKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            for (int i = 0; i < Hotbar.SlotCount; i++)
            {
                if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame)
                {
                    Hotbar.Select(i);
                    return;
                }
            }
        }
    }
}
