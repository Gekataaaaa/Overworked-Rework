using System.Collections.Generic;
using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Шкафчик в рост Дэнни: по E Дэнни залезает внутрь, повторное E — вылезает.
    /// Внутри Дэнни рисуется между задней стенкой и дверцей (его спрайты опускаются на hiddenOrderShift),
    /// поэтому через щель в дверце видно его голову. Внутри он обрезан маской по контуру шкафчика — ступни
    /// ниже дна не видны. Пока сидит — управление заблокировано, фонарик выключен.
    /// Собирается меню Overworked → P1 → Objects → Add Locker In Open Scene.
    /// </summary>
    public class HideLocker : InteractableBase
    {
        [Tooltip("На сколько опустить sortingOrder спрайтов Дэнни внутри: ниже дверцы, выше задней стенки")]
        [SerializeField] int hiddenOrderShift = 15;

        [Tooltip("Подсказка, пока Дэнни снаружи")]
        [SerializeField] string hidePrompt = "Hide";

        [Tooltip("Подсказка, пока Дэнни внутри")]
        [SerializeField] string leavePrompt = "Leave";

        /// <summary>Шкафчик, в котором сейчас сидит Дэнни (InteractionSystem держит его в фокусе). Нет — null.</summary>
        public static HideLocker Occupied { get; private set; }

        // Сброс при входе в Play: при выключенном Domain Reload статика пережила бы прошлый запуск.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOccupied() => Occupied = null;

        /// <summary>Дэнни сейчас в шкафчике.</summary>
        public bool IsHidden { get; private set; }

        readonly List<(SpriteRenderer sr, int order, SpriteMaskInteraction mask)> shifted = new List<(SpriteRenderer, int, SpriteMaskInteraction)>();
        bool flashlightWasOn;
        SpriteMask clip;
        static Sprite clipSprite;

        public override string Prompt => IsHidden ? leavePrompt : hidePrompt;

        protected override void OnInteract()
        {
            if (IsHidden) { Exit(); return; }
            var player = PlayerController.Instance;
            if (player == null) return;
            IsHidden = true;
            Occupied = this;

            var fl = FlashlightController.Instance;
            flashlightWasOn = fl != null && fl.IsOn;
            if (flashlightWasOn) fl.Toggle();

            // Встаём по центру шкафчика и уходим за дверцу.
            var p = player.transform.position;
            player.Teleport(new Vector3(transform.position.x, p.y, p.z));
            LockPlayer(true);
            shifted.Clear();
            EnableClip(true);
            foreach (var sr in player.GetComponentsInChildren<SpriteRenderer>(true))
            {
                shifted.Add((sr, sr.sortingOrder, sr.maskInteraction));
                sr.sortingOrder -= hiddenOrderShift;
                // Дэнни виден только в пределах шкафчика: ступни ниже его дна не торчат.
                sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
        }

        // Маска по коллайдеру шкафчика (от пола до верха) — обрезает спрятанного Дэнни по контуру шкафчика.
        void EnableClip(bool on)
        {
            if (!on)
            {
                if (clip != null) clip.enabled = false;
                return;
            }
            if (clip == null)
            {
                var go = new GameObject("~HideClip");
                go.transform.SetParent(transform, false);
                clip = go.AddComponent<SpriteMask>();
                clip.sprite = ClipSprite();
            }
            var b = GetComponent<Collider2D>().bounds;
            var t = clip.transform;
            t.position = new Vector3(b.center.x, b.center.y, transform.position.z);
            var ls = transform.lossyScale;
            t.localScale = new Vector3(b.size.x / ls.x, b.size.y / ls.y, 1f);
            clip.enabled = true;
        }

        // Белый квадрат 1×1 юнит для маски.
        static Sprite ClipSprite()
        {
            if (clipSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                clipSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            }
            return clipSprite;
        }

        /// <summary>Вылезти из шкафчика (E или из сценария).</summary>
        public void Exit()
        {
            if (!IsHidden) return;
            IsHidden = false;
            if (Occupied == this) Occupied = null;
            foreach (var (sr, order, mask) in shifted)
            {
                if (sr == null) continue;
                sr.sortingOrder = order;
                sr.maskInteraction = mask;
            }
            shifted.Clear();
            EnableClip(false);
            LockPlayer(false);
            if (flashlightWasOn && FlashlightController.Instance != null) FlashlightController.Instance.Toggle();
            Finish();
        }

        void OnDisable() => Exit();
    }
}
