using System;

namespace Overworked
{
    /// <summary>Мини-игра: печать отчёта, считыватель, сигнал монитора, Tiny UFO.</summary>
    public interface IMiniGame
    {
        /// <summary>Запуск. onFinish(true) — победа, onFinish(false) — выход без выполнения.</summary>
        void Begin(Action<bool> onFinish);
    }
}
