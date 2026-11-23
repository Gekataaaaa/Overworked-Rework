using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Описание объекта («ДО» в нарративе) и реплики Дэнни, которые показываются сами при подходе,
    /// без нажатия E: часы, кулер, кружка, стол Чарльза. Нужен Collider2D-триггер зоны подхода.
    /// Можно класть вместе с другим взаимодействием (стол Чарльза: Inspectable + NoteInteractable).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Inspectable : MonoBehaviour
    {
        [Tooltip("Описание и реплики Дэнни (ассет из Data/Inspectables)")]
        [SerializeField] DialogueData dialogue;

        [Tooltip("Показать только при первом подходе")]
        [SerializeField] bool once = true;

        bool shown;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (shown && once) return;
            var player = other.GetComponent<PlayerController>();
            // Не перебиваем катсцену и телефон: описание покажется при следующем подходе.
            if (player == null || player.State != PlayerState.Normal) return;
            shown = true;
            if (dialogue != null && DialogueManager.Instance != null) DialogueManager.Instance.Play(dialogue);
        }

        void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }
    }
}
