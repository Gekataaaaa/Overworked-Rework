using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Триггер (Collider2D, isTrigger): Дэнни вошёл — запускает enemy01 и эффект появления
    /// (мигание ламп, как в H1 на турникете: гаснут до 70 %, включаются — 100 %). Один раз.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Enemy01Trigger : MonoBehaviour
    {
        [Tooltip("Какого врага запустить")]
        [SerializeField] Enemy01 enemy;

        [Tooltip("Эффект при появлении врага (мигание ламп). Пусто — без эффекта")]
        [SerializeField] HallucinationEvent effect;

        bool fired;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (fired || enemy == null || other.GetComponentInParent<PlayerController>() == null) return;
            fired = true;
            enemy.Activate();
            if (effect == null) return;
            if (HallucinationManager.Instance != null) HallucinationManager.Instance.Play(effect);
            else effect.Play();
        }

        void Reset() => GetComponent<Collider2D>().isTrigger = true;
    }
}
