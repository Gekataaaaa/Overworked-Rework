using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Подбираемый предмет хотбара: по E кладёт item в хотбар, показывает тост «Received: …»
    /// и втягивается в Дэнни (PickupFlyEffect). Хотбар полон — предмет остаётся на месте.
    /// </summary>
    public class HotbarPickupInteractable : InteractableBase
    {
        [Tooltip("Какой предмет ложится в хотбар")]
        [SerializeField] HotbarItemData item;

        [Tooltip("Что спрятать после подбора. Пусто — весь объект")]
        [SerializeField] GameObject hideOnPickup;

        [Tooltip("Звук подбора. Пусто — без звука")]
        [SerializeField] AudioClip pickupSound;

        [Range(0f, 1f)] [SerializeField] float pickupVolume = 0.8f;

        [Tooltip("На сколько юнитов вниз дотянуть триггер при старте: предмет на столе или полке выше Дэнни " +
                 "должен попадать в радиус поиска от его ног. Верх триггера (над ним подсказка) не меняется")]
        [SerializeField] float reachDown = 2f;

        protected override void Awake()
        {
            base.Awake();
            if (reachDown > 0f && TryGetComponent(out BoxCollider2D box))
            {
                box.size += new Vector2(0f, reachDown);
                box.offset -= new Vector2(0f, reachDown / 2f);
            }
        }

        /// <inheritdoc/>
        public override bool CanInteract => base.CanInteract && !used && item != null && !Hotbar.IsFull;

        protected override void Reset()
        {
            base.Reset();
            prompt = "Pick up";
        }

        protected override void OnInteract()
        {
            if (!Hotbar.Add(item)) return;
            if (pickupSound != null && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(pickupSound, pickupVolume);
            if (!string.IsNullOrEmpty(item.displayName)) Toast("Received: " + item.displayName);
            Finish();
            PickupFlyEffect.Play(hideOnPickup != null ? hideOnPickup : gameObject);
        }
    }
}
