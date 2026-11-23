using UnityEngine;
using UnityEngine.InputSystem;

namespace Overworked
{
    /// <summary>
    /// Единая точка доступа к действиям ввода из InputSystem_Actions (карта Player).
    /// Пользуются все системы: Move — игрок, Interact — InteractionSystem,
    /// Flashlight — фонарик, Phone — телефон, Pause — меню паузы.
    /// Пример: <c>if (GameInput.Interact.WasPressedThisFrame()) …</c>
    /// </summary>
    public static class GameInput
    {
        /// <summary>Ось -1..1: A/D и стрелки.</summary>
        public static InputAction Move => Get("Move");
        /// <summary>E. Для удержания (считыватель) — <c>IsPressed()</c>.</summary>
        public static InputAction Interact => Get("Interact");
        /// <summary>F.</summary>
        public static InputAction Flashlight => Get("Flashlight");
        /// <summary>Tab.</summary>
        public static InputAction Phone => Get("Phone");
        /// <summary>Esc.</summary>
        public static InputAction Pause => Get("Pause");
        /// <summary>Shift (левый и правый) — бег, держать. Работает, если на игроке включён allowRun.</summary>
        public static InputAction Run => Get("Run");

        static InputActionMap map;

        static InputAction Get(string action)
        {
            if (map == null)
            {
                // InputSystem.actions — ассет, назначенный в Project Settings → Input System (project-wide actions).
                map = InputSystem.actions != null ? InputSystem.actions.FindActionMap("Player", true) : null;
                if (map == null)
                {
                    Debug.LogError("[Input] нет project-wide InputSystem_Actions с картой Player");
                    return null;
                }
                map.Enable();
            }
            return map.FindAction(action, true);
        }

        // Сбрасываем кэш при входе в Play Mode, иначе при выключенном Domain Reload останется старая ссылка.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => map = null;
    }
}
