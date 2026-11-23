namespace Overworked
{
    /// <summary>Подстановки в тексты игры.</summary>
    public static class TextFormatter
    {
        /// <summary>Заменяет {name} на имя игрока из пролога.</summary>
        public static string Format(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string name = GameManager.Instance != null ? GameManager.Instance.State.PlayerName : "Tester";
            return s.Replace("{name}", name);
        }
    }
}
