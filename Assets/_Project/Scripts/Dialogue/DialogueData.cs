using UnityEngine;

namespace Overworked
{
    /// <summary>Кто говорит: реплика Дэнни, описание объекта или системный текст.</summary>
    public enum Speaker { Danny, Description, System }

    /// <summary>Одна строка диалога.</summary>
    [System.Serializable]
    public struct DialogueLine
    {
        public Speaker Speaker;
        [Tooltip("Текст (английский), поддерживает {name}")]
        [TextArea] public string Text;
    }

    /// <summary>Набор строк, которые показываются подряд субтитрами внизу.</summary>
    [CreateAssetMenu(menuName = "Overworked/Dialogue", fileName = "F3_Start")]
    public class DialogueData : ScriptableObject
    {
        public DialogueLine[] Lines;
    }
}
