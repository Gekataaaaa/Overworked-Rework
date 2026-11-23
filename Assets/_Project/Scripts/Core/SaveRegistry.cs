using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Список всех заданий, сообщений и предметов хотбара — чтобы SaveSystem находил их по Id / имени при загрузке сохранения.
    /// Лежит в Resources/SaveRegistry; заполняется сам в редакторе (SaveRegistryRefresher) при каждой перекомпиляции.
    /// </summary>
    public class SaveRegistry : ScriptableObject
    {
        public const string ResourcePath = "SaveRegistry";

        [Tooltip("Все ObjectiveData проекта (Data/Objectives)")]
        public ObjectiveData[] Objectives = new ObjectiveData[0];

        [Tooltip("Все MessageData проекта (Data/Messages)")]
        public MessageData[] Messages = new MessageData[0];

        [Tooltip("Все HotbarItemData проекта (Data/Items) — содержимое хотбара в чекпоинте")]
        public HotbarItemData[] Items = new HotbarItemData[0];

        /// <summary>Предмет хотбара по id или null.</summary>
        public HotbarItemData Item(string id)
        {
            foreach (var i in Items) if (i != null && i.id == id) return i;
            return null;
        }

        /// <summary>Задание по Id или null.</summary>
        public ObjectiveData Objective(string id)
        {
            foreach (var o in Objectives) if (o != null && o.Id == id) return o;
            return null;
        }

        /// <summary>Сообщение по имени ассета или null.</summary>
        public MessageData Message(string name)
        {
            foreach (var m in Messages) if (m != null && m.name == name) return m;
            return null;
        }
    }
}
