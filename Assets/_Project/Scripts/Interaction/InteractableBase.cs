using UnityEngine;
using UnityEngine.Events;

namespace Overworked
{
    /// <summary>
    /// Общая часть всех объектов для E: подсказка, условия (задание, флаг, замок)
    /// и что сделать после (закрыть задание, поставить флаг, событие onDone для сценария P3).
    /// Наследники реализуют только OnInteract() и в конце вызывают Finish().
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        [Tooltip("Текст подсказки после «[E] » (английский): Take note, Pick up pass…")]
        [SerializeField] protected string prompt = "Interact";

        [Tooltip("Доступен, только пока выдано это задание. Пусто — всегда")]
        [SerializeField] protected ObjectiveData requiresObjective;

        [Tooltip("Доступен, только если стоит этот флаг GameState (\"MaxPass\", \"Key102\"…). Пусто — всегда")]
        [SerializeField] protected string requiresFlag;

        [Tooltip("Заперт на старте: сценарий откроет его через SetLocked(false)")]
        [SerializeField] protected bool startLocked;

        [Tooltip("После взаимодействия закрыть это задание")]
        [SerializeField] protected ObjectiveData completesObjective;

        [Tooltip("После взаимодействия поставить этот флаг GameState")]
        [SerializeField] protected string setsFlag;

        [Tooltip("Взаимодействие завершено: сюда цепляется сценарий (сообщение, галлюцинация, следующее задание)")]
        public UnityEvent onDone;

        /// <summary>Уже срабатывал (для одноразовых объектов).</summary>
        protected bool used;

        /// <summary>Идёт взаимодействие (затемнение, мини-игра, текст) — второй E не нужен.</summary>
        protected bool busy;

        bool locked;

        /// <inheritdoc/>
        public virtual string Prompt => prompt;

        /// <inheritdoc/>
        public virtual bool CanInteract => !busy && !locked && ConditionsMet;

        /// <summary>Выполнены условия задания и флага.</summary>
        protected bool ConditionsMet =>
            (requiresObjective == null || (ObjectiveManager.Instance != null && ObjectiveManager.Instance.IsActive(requiresObjective)))
            && (string.IsNullOrEmpty(requiresFlag) || HasFlag(requiresFlag));

        protected virtual void Awake() => locked = startLocked;

        /// <summary>Запереть (true) или открыть (false) объект из сценария.</summary>
        public void SetLocked(bool value) => locked = value;

        /// <inheritdoc/>
        [ContextMenu("Interact")]
        public void Interact()
        {
            if (!CanInteract) return;
            Debug.Log($"[Interaction] {name}: {Prompt}");
            OnInteract();
        }

        /// <summary>Что делает объект по E.</summary>
        protected abstract void OnInteract();

        /// <summary>Конец взаимодействия: флаг, задание, событие для сценария.</summary>
        protected void Finish()
        {
            used = true;
            busy = false;
            if (!string.IsNullOrEmpty(setsFlag)) SetFlag(setsFlag);
            if (completesObjective != null && ObjectiveManager.Instance != null)
                ObjectiveManager.Instance.Complete(completesObjective);
            onDone?.Invoke();
        }

        /// <summary>Стоит ли флаг прогресса в GameState.</summary>
        public static bool HasFlag(string flag) =>
            GameManager.Instance != null && GameManager.Instance.State.Flags.Contains(flag);

        /// <summary>Ставит флаг прогресса в GameState.</summary>
        public static void SetFlag(string flag)
        {
            if (GameManager.Instance != null) GameManager.Instance.State.Flags.Add(flag);
        }

        /// <summary>Блокирует/возвращает управление Дэнни.</summary>
        protected static void LockPlayer(bool value)
        {
            if (PlayerController.Instance != null) PlayerController.Instance.SetInputLocked(value);
        }

        /// <summary>Тост «Received: …», если текст задан.</summary>
        protected static void Toast(string text)
        {
            if (!string.IsNullOrEmpty(text) && UIManager.Instance != null)
                UIManager.Instance.ShowToast(TextFormatter.Format(text));
        }

        /// <summary>Реплики/описания; если данных нет — done сразу.</summary>
        protected static void PlayDialogue(DialogueData data, System.Action done = null)
        {
            if (data != null && DialogueManager.Instance != null) DialogueManager.Instance.Play(data, done);
            else done?.Invoke();
        }

        // Новый объект в редакторе: коллайдер сразу триггер, чтобы Дэнни проходил сквозь.
        protected virtual void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }
    }
}
