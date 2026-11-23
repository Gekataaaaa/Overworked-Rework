using UnityEngine;

namespace Overworked
{
    /// <summary>Задание: заголовок для карточки в правом верхнем углу.</summary>
    [CreateAssetMenu(menuName = "Overworked/Objective", fileName = "F3_NewObjective")]
    public class ObjectiveData : ScriptableObject
    {
        [Tooltip("Уникальный id, например F3_TakeMaxPass")]
        public string Id;

        [Tooltip("Текст карточки задания (английский)")]
        public string Title;
    }
}
