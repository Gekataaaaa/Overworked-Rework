using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Предмет без инвентаря: по E взлетает над головой Дэнни и втягивается в него (PickupFlyEffect), ставит флаг (setsFlag, например "MaxPass"),
    /// показывает тост «Received: …» и закрывает задание (completesObjective).
    /// </summary>
    public class PickupInteractable : InteractableBase
    {
        [Tooltip("Тост, например «Received: Max's pass». Пусто — без тоста")]
        [SerializeField] string receivedToast;

        [Tooltip("Реплика Дэнни после подбора («Got it.»)")]
        [SerializeField] DialogueData afterDialogue;

        [Tooltip("Что спрятать после подбора. Пусто — весь объект")]
        [SerializeField] GameObject hideOnPickup;

        [Tooltip("Звук подбора (Audio/SFX/SFX_Pickup — пластиковый щелчок: карта, фонарик). Пусто — без звука; " +
                 "металлическому ключу и бумагам этот звук не ставить")]
        [SerializeField] AudioClip pickupSound;

        [Tooltip("Громкость звука подбора 0..1")]
        [Range(0f, 1f)] [SerializeField] float pickupVolume = 0.8f;

        /// <inheritdoc/>
        public override bool CanInteract => base.CanInteract && !used;

        protected override void OnInteract()
        {
            if (pickupSound != null && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(pickupSound, pickupVolume);
            Toast(receivedToast);
            // Finish до выключения объекта: onDone должен дойти до сценария, пока объект ещё жив.
            Finish();
            PlayDialogue(afterDialogue);
            // Не исчезает сразу: взлетает над головой Дэнни и втягивается в него (PickupFlyEffect).
            PickupFlyEffect.Play(hideOnPickup != null ? hideOnPickup : gameObject);
        }
    }
}
