using UnityEngine;

namespace Overworked
{
    /// <summary>Отправитель сообщения в телефоне.</summary>
    public enum Sender { Max, Boss }

    /// <summary>Одно сообщение в телефоне (несколько строк подряд от одного контакта).</summary>
    [CreateAssetMenu(menuName = "Overworked/Message", fileName = "F3_Max_01")]
    public class MessageData : ScriptableObject
    {
        public Sender Sender;

        [Tooltip("Строки сообщения (английский), поддерживают {name}")]
        [TextArea] public string[] Lines;

        [Tooltip("3, 2, 1")]
        public int Floor = 3;

        [Tooltip("Задание, которое выдаётся этим сообщением (может быть пустым)")]
        public ObjectiveData LinkedObjective;
    }
}
