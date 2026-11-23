using System;

namespace Overworked
{
    /// <summary>
    /// Содержимое хотбара: фиксированное число слотов, предмет кладётся в первый пустой.
    /// Статический — живёт всю игру; новая игра очищает его (GameManager.ResetState).
    /// </summary>
    public static class Hotbar
    {
        public const int SlotCount = 5;

        static readonly HotbarItemData[] slots = new HotbarItemData[SlotCount];

        /// <summary>Содержимое поменялось — UI перерисовывает слоты.</summary>
        public static event Action Changed;

        /// <summary>Выбранный слот (клавиши 1–5); -1 — ничего не выбрано.</summary>
        public static int Selected { get; private set; } = -1;

        /// <summary>Предмет в выбранном слоте (null — пусто или ничего не выбрано).</summary>
        public static HotbarItemData SelectedItem => Selected >= 0 ? slots[Selected] : null;

        /// <summary>Выбрать слот; повторный выбор того же слота снимает выделение.</summary>
        public static void Select(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return;
            Selected = Selected == slot ? -1 : slot;
        }

        /// <summary>Предмет в слоте (null — пусто).</summary>
        public static HotbarItemData Get(int slot) => slots[slot];

        /// <summary>Положить в первый пустой слот. false — хотбар полон.</summary>
        public static bool Add(HotbarItemData item)
        {
            if (item == null) return false;
            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] != null) continue;
                slots[i] = item;
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>Убрать предмет (например, когда он использован).</summary>
        public static bool Remove(HotbarItemData item)
        {
            int i = Array.IndexOf(slots, item);
            if (item == null || i < 0) return false;
            slots[i] = null;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Использовать предмет в выбранном слоте; израсходованный убирается, выделение снимается.</summary>
        public static bool UseSelected()
        {
            var item = SelectedItem;
            if (item == null || !item.Use()) return false;
            slots[Selected] = null;
            Selected = -1;
            Changed?.Invoke();
            return true;
        }

        public static bool Has(HotbarItemData item) => item != null && Array.IndexOf(slots, item) >= 0;

        public static bool IsFull => Array.IndexOf(slots, null) < 0;

        /// <summary>Содержимое из чекпоинта (SaveSystem): предметы по слотам, выделение снимается.</summary>
        public static void Restore(HotbarItemData[] items)
        {
            Array.Clear(slots, 0, SlotCount);
            for (int i = 0; i < SlotCount && i < items.Length; i++) slots[i] = items[i];
            Selected = -1;
            Changed?.Invoke();
        }

        public static void Clear()
        {
            Array.Clear(slots, 0, SlotCount);
            Selected = -1;
            Changed?.Invoke();
        }
    }
}
