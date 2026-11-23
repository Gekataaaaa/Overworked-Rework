using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Overworked
{
    /// <summary>
    /// Фонарик в правой (ближней) руке Дэнни: спрайт держится в точке кисти текущего кадра анимации,
    /// зеркалится вместе с Дэнни, виден только когда фонарик подобран (FlashlightController.Available —
    /// на Floor_02 его выдаёт подбор). Порядок слоёв: Дэнни → фонарик → кисть (картинка «только кисть»
    /// этого кадра) — пальцы обхватывают рукоятку, а сам фонарик не прячется за телом.
    /// Свет фонарика (FlashlightController, P4) переносится в линзу, при включённом фонарике линза светится.
    /// Лежит на корне префаба Player (ставит Overworked → P1 → Player → Setup Held Flashlight).
    /// Точки кисти по кадрам — в инспекторе, в пикселях кадра Дэнни (300 × 430).
    /// </summary>
    [DefaultExecutionOrder(200)]   // после FlashlightController.LateUpdate: свет ставим уже мы
    public class HeldFlashlight : MonoBehaviour
    {
        /// <summary>Кисть на одном кадре Дэнни: точка (пиксели кадра от левого верхнего угла) и картинка «только кисть».</summary>
        [Serializable]
        public struct HandPoint
        {
            public Sprite Frame;
            public Vector2 Pixel;
            public Sprite Hand;
        }

        [Tooltip("Спрайт Дэнни (дочерний Sprite префаба Player)")]
        [SerializeField] SpriteRenderer body;

        [Tooltip("Спрайт фонарика (пивот — на рукоятке, линза в исходнике смотрит влево)")]
        [SerializeField] SpriteRenderer flashlight;

        [Tooltip("Свечение линзы — включается вместе с фонариком")]
        [SerializeField] SpriteRenderer lensGlow;

        [Tooltip("Кисть поверх фонарика (дочерний к спрайту Дэнни, картинка «только кисть» текущего кадра)")]
        [SerializeField] SpriteRenderer handOverlay;

        [Tooltip("Кисть по кадрам анимации")]
        [SerializeField] HandPoint[] handPoints;

        [Tooltip("Точка кисти для кадров, которых нет в списке")]
        [SerializeField] Vector2 defaultPixel = new Vector2(144, 276);

        [Tooltip("Линза относительно пивота фонарика, юниты (в исходнике, линзой влево)")]
        [SerializeField] Vector2 lensOffset = new Vector2(-0.24f, 0f);

        [Tooltip("Яркость свечения линзы (альфа), когда фонарик включён")]
        [Range(0f, 1f)] [SerializeField] float glowAlpha = 0.9f;

        Dictionary<Sprite, HandPoint> map;
        PlayerAnimator anim;
        Light2D spot;
        bool lightSuppressed;

        void SuppressLight(FlashlightController fc, bool suppress)
        {
            if (fc == null) return;
            if (spot == null) spot = fc.GetComponentInChildren<Light2D>(true);
            if (spot == null) return;
            if (suppress)
            {
                if (spot.enabled) spot.enabled = false;
                lightSuppressed = true;
            }
            else if (lightSuppressed)
            {
                spot.enabled = fc.IsOn;
                lightSuppressed = false;
            }
        }

        void Awake()
        {
            anim = GetComponentInParent<PlayerAnimator>();
            map = new Dictionary<Sprite, HandPoint>();
            if (handPoints != null)
                foreach (var p in handPoints) if (p.Frame != null) map[p.Frame] = p;
            SetVisible(false, false);
        }

        void LateUpdate()
        {
            var fc = FlashlightController.Instance;
            // Дэнни смотрит в экран: фонарик убран, луч погашен (вида фонарика спереди нет — он висел бы у ног).
            // Toggle() не зовём — он щёлкает и шлёт OnToggled (на нём сценарий Floor_02); гасим сам источник
            // и после взгляда возвращаем ему состояние IsOn — фонарик снова в руке и в прежнем состоянии.
            bool looking = anim != null && anim.ShowsLookAtScreen;
            SuppressLight(fc, looking);
            bool visible = fc != null && fc.Available && body != null && body.enabled && body.sprite != null
                           && !looking
                           && (anim == null || !anim.IsLyingDown);   // в кровати фонарика в руке нет
            SetVisible(visible, visible && fc.IsOn);
            if (!visible) return;

            // Пиксель кадра → локальная точка спрайта Дэнни (пивот кадра — в центре).
            var sprite = body.sprite;
            bool known = map.TryGetValue(sprite, out var hp);
            var px = known ? hp.Pixel : defaultPixel;
            var size = sprite.rect.size;
            float ppu = sprite.pixelsPerUnit;
            var local = new Vector3((px.x - size.x * 0.5f) / ppu, (size.y * 0.5f - px.y) / ppu, 0f);
            bool faceLeft = body.flipX;
            if (faceLeft) local.x = -local.x;

            var hand = body.transform.TransformPoint(local);
            flashlight.transform.position = hand;
            // Линза в исходнике смотрит влево: Дэнни смотрит вправо — зеркалим.
            flashlight.flipX = !faceLeft;

            if (handOverlay != null)
            {
                handOverlay.sprite = known ? hp.Hand : null;
                handOverlay.flipX = body.flipX;
                handOverlay.color = body.color;
            }

            float dir = faceLeft ? 1f : -1f;   // знак lensOffset.x (в исходнике линза слева)
            var lens = hand + new Vector3(lensOffset.x * dir * flashlight.transform.lossyScale.x, lensOffset.y, 0f);
            if (lensGlow != null) lensGlow.transform.position = lens;
            // Свет P4 — из линзы, а не из центра Дэнни (поворот луча он ставит сам).
            fc.transform.position = new Vector3(lens.x, lens.y, fc.transform.position.z);
        }

        void SetVisible(bool held, bool on)
        {
            if (flashlight != null && flashlight.enabled != held) flashlight.enabled = held;
            if (handOverlay != null && handOverlay.enabled != held) handOverlay.enabled = held;
            if (lensGlow == null) return;
            if (lensGlow.enabled != on) lensGlow.enabled = on;
            var c = lensGlow.color;
            c.a = glowAlpha;
            lensGlow.color = c;
        }
    }
}
