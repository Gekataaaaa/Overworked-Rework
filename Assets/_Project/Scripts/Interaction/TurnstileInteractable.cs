using UnityEngine;
using UnityEngine.Events;

namespace Overworked
{
    /// <summary>
    /// Турникет Floor_03. Первый E — своя карта: «ACCESS DENIED» и onDenied (сценарий P3: сообщение Босса,
    /// после прочтения Галлюцинация 1). Когда стоит флаг пропуска Макса — мини-игра «Считыватель пропуска»;
    /// попал — «ACCESS GRANTED», Finish() (onDone открывает лестницу).
    /// </summary>
    public class TurnstileInteractable : InteractableBase
    {
        [Tooltip("Подсказка со своей картой")]
        [SerializeField] string ownCardPrompt = "Use your card";

        [Tooltip("Подсказка с пропуском Макса")]
        [SerializeField] string passPrompt = "Use Max's pass";

        [Tooltip("Флаг пропуска Макса (ставит PickupInteractable на кухне)")]
        [SerializeField] string passFlag = "MaxPass";

        [Tooltip("Мини-игра считывателя (CardReaderMiniGame)")]
        [SerializeField] MonoBehaviour cardReader;

        [Tooltip("Отказ: описание «ACCESS DENIED» и реплика Дэнни. Пусто — тост deniedText")]
        [SerializeField] DialogueData deniedDialogue;

        [Tooltip("Тост отказа, если нет deniedDialogue")]
        [SerializeField] string deniedText = "ACCESS DENIED";

        [Tooltip("Успех: описание «ACCESS GRANTED» и реплика Дэнни. Пусто — тост grantedText")]
        [SerializeField] DialogueData grantedDialogue;

        [Tooltip("Тост успеха, если нет grantedDialogue")]
        [SerializeField] string grantedText = "ACCESS GRANTED";

        [Tooltip("Первый отказ своей карты: сюда сценарий вешает сообщение Босса")]
        public UnityEvent onDenied;

        bool denied;

        bool UsePass => denied && HasFlag(passFlag);

        /// <inheritdoc/>
        public override string Prompt => UsePass ? passPrompt : ownCardPrompt;

        /// <inheritdoc/>
        public override bool CanInteract => base.CanInteract && !used;

        protected override void OnInteract()
        {
            if (!UsePass)
            {
                bool first = !denied;
                denied = true;
                if (deniedDialogue != null) PlayDialogue(deniedDialogue);
                else Toast(deniedText);
                if (first) onDenied?.Invoke();
                return;
            }

            if (cardReader is not IMiniGame game || MiniGameHost.Instance == null)
            {
                Debug.LogError($"[Interaction] {name}: не задан считыватель или нет MiniGameHost");
                return;
            }
            busy = true;
            MiniGameHost.Instance.Open(game, win =>
            {
                busy = false;
                if (!win) return;
                if (grantedDialogue != null) PlayDialogue(grantedDialogue);
                else Toast(grantedText);
                Finish();
            });
        }
    }
}
