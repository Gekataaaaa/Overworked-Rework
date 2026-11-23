using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Записка: по E текст на весь экран, после закрытия — тост «Received: …» и реплики Дэнни.
    /// Пример — записка Чарльза на Floor_03.
    /// </summary>
    public class NoteInteractable : InteractableBase
    {
        [Tooltip("Текст записки на весь экран (английский, поддерживает {name})")]
        [TextArea(2, 6)]
        [SerializeField] string noteText;

        [Tooltip("Тост после закрытия, например «Received: Charles's note». Пусто — без тоста")]
        [SerializeField] string receivedToast;

        [Tooltip("Реплики Дэнни после записки")]
        [SerializeField] DialogueData afterDialogue;

        [Tooltip("Шорох бумаги, когда Дэнни берёт записку (Audio/SFX/SFX_Paper). Пусто — без звука")]
        [SerializeField] AudioClip paperSound;

        [Tooltip("Громкость шороха 0..1")]
        [Range(0f, 1f)] [SerializeField] float paperVolume = 0.8f;

        /// <inheritdoc/>
        public override bool CanInteract => base.CanInteract && !used;

        protected override void OnInteract()
        {
            busy = true;
            if (paperSound != null && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(paperSound, paperVolume);
            LockPlayer(true);
            if (UIManager.Instance != null)
                UIManager.Instance.ShowFullscreenText(TextFormatter.Format(noteText), Closed);
            else
                Closed();
        }

        void Closed()
        {
            LockPlayer(false);
            Toast(receivedToast);
            PlayDialogue(afterDialogue, Finish);
        }
    }
}
