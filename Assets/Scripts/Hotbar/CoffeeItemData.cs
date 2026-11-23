using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Кофе: при использовании возвращает часть рассудка и ненадолго разгоняет пульс
    /// (HeartbeatManager.Spike — к концу времени пульс сам спадает к прежнему значению).
    /// </summary>
    [CreateAssetMenu(menuName = "Overworked/Hotbar Item (Coffee)", fileName = "Coffee")]
    public class CoffeeItemData : HotbarItemData
    {
        [Tooltip("Сколько рассудка вернуть, пунктов из 100")]
        public float sanityRestore = 15f;

        [Tooltip("На сколько BPM поднять пульс")]
        public float bpmBoost = 40f;

        [Tooltip("Сколько секунд длится ускорение пульса")]
        public float boostSeconds = 5f;

        [Tooltip("Звук глотка. Пусто — без звука")]
        public AudioClip drinkSound;

        public override bool Use()
        {
            var sanity = SanityManager.Instance;
            if (sanity != null) sanity.SetSanity(sanity.Target + sanityRestore);
            if (HeartbeatManager.Instance != null) HeartbeatManager.Instance.Spike(bpmBoost, boostSeconds);
            if (drinkSound != null && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(drinkSound, 0.8f);
            return true;
        }
    }
}
