using System.Collections.Generic;

namespace Overworked
{
    /// <summary>
    /// Состояние одного прохождения: имя игрока, этаж, сюжетные флаги.
    /// Живёт в GameManager и переживает смену сцен. Не [Serializable]: Unity его не сохраняет,
    /// а HashSet сериализатор Unity всё равно не поддерживает.
    /// </summary>
    public class GameState
    {
        /// <summary>Имя из пролога. "Tester" — когда сцена запущена прямо из редактора.</summary>
        public string PlayerName = "Tester";

        /// <summary>3, 2, 1; 0 — WhiteRoom.</summary>
        public int CurrentFloor = 3;

        /// <summary>Флаги прогресса: подобранные предметы, пройденные события ("MaxPass", "Key102"…).</summary>
        public HashSet<string> Flags = new HashSet<string>();
    }
}
