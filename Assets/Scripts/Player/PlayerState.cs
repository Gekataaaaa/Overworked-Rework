namespace Overworked
{
    /// <summary>Режим управления Дэнни.</summary>
    public enum PlayerState
    {
        /// <summary>Обычное управление.</summary>
        Normal,
        /// <summary>Ввод заблокирован (телефон, мини-игра, катсцена).</summary>
        Locked,
        /// <summary>Персонажем управляет сценарий.</summary>
        Cutscene,
    }
}
