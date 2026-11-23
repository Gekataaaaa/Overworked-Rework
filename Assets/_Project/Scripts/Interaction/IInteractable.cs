namespace Overworked
{
    /// <summary>Объект, с которым можно взаимодействовать по E.</summary>
    public interface IInteractable
    {
        /// <summary>Текст подсказки после "[E] ", например "Take note".</summary>
        string Prompt { get; }

        /// <summary>Можно ли сейчас взаимодействовать (дверь заперта, задание не выдано…).</summary>
        bool CanInteract { get; }

        /// <summary>Вызывается по нажатию E.</summary>
        void Interact();
    }
}
