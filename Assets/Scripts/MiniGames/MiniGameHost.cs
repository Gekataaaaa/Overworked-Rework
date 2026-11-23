using System;
using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Открывает мини-игру поверх сцены и блокирует Дэнни на это время. Одна мини-игра за раз.
    /// Живёт в _Systems. Мини-игры сами рисуют свой оверлей; Tiny UFO грузит свою сцену.
    /// </summary>
    public class MiniGameHost : SingletonBehaviour<MiniGameHost>
    {
        /// <summary>Сейчас открыта мини-игра.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Мини-игра открылась (true) или закрылась (false): P3 может прятать HUD, P4 — приглушать звук.</summary>
        public event Action<bool> OnOpenChanged;

        /// <summary>
        /// Запускает мини-игру, по окончании возвращает управление и вызывает onFinish
        /// (true — победа, false — выход без выполнения). Пока открыта другая — вызов игнорируется.
        /// </summary>
        public void Open(IMiniGame game, Action<bool> onFinish)
        {
            if (game == null) return;
            if (IsOpen)
            {
                Debug.LogWarning($"[MiniGames] {game} не открыта: уже идёт другая мини-игра");
                return;
            }
            Debug.Log($"[MiniGames] Open {game}");
            IsOpen = true;
            SetPlayerLocked(true);
            if (ObjectiveUI.Instance != null) ObjectiveUI.Instance.SetVisible(false);
            OnOpenChanged?.Invoke(true);

            bool finished = false;
            game.Begin(win =>
            {
                // Мини-игра могла позвать колбэк дважды (победа и закрытие) — считаем только первый.
                if (finished) return;
                finished = true;
                Debug.Log($"[MiniGames] Close {game}: {(win ? "win" : "quit")}");
                IsOpen = false;
                SetPlayerLocked(false);
                if (ObjectiveUI.Instance != null) ObjectiveUI.Instance.SetVisible(true);
                OnOpenChanged?.Invoke(false);
                onFinish?.Invoke(win);
            });
        }

        static void SetPlayerLocked(bool value)
        {
            if (PlayerController.Instance != null) PlayerController.Instance.SetInputLocked(value);
        }
    }
}
