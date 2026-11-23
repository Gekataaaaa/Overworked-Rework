using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Предмет хотбара — категория подбираемых предметов, которые ложатся в слот сверху экрана.
    /// Создаётся через Create → Overworked → Hotbar Item. Предметы пока не придуманы.
    /// </summary>
    [CreateAssetMenu(menuName = "Overworked/Hotbar Item", fileName = "HotbarItem")]
    public class HotbarItemData : ScriptableObject
    {
        [Tooltip("Уникальный id (латиница), например \"Crowbar\"")]
        public string id;

        [Tooltip("Название на английском — для тоста «Received: …»")]
        public string displayName;

        [Tooltip("Иконка в слоте хотбара")]
        public Sprite icon;

        /// <summary>
        /// Использовать предмет (выбран в хотбаре + E). true — предмет израсходован и уходит из хотбара.
        /// Обычный предмет ничего не делает; эффекты — в наследниках (CoffeeItemData).
        /// </summary>
        public virtual bool Use() => false;
    }
}
