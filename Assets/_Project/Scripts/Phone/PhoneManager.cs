using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overworked
{
    /// <summary>
    /// Открытие/закрытие телефона (Tab, кнопка на экране; Esc закрывает).
    /// Пока телефон открыт, Дэнни стоит, но мир не на паузе. Живёт в префабе _Systems.
    /// Внешний вид — <see cref="PhoneUI"/> в HUD, сообщения — <see cref="MessageManager"/>.
    /// </summary>
    public class PhoneManager : SingletonBehaviour<PhoneManager>
    {
        [Tooltip("Интерфейс телефона в HUD (собирается меню Overworked → P3 → UI → Build Phone UI)")]
        [SerializeField] PhoneUI ui;

        /// <summary>Игрок открыл телефон.</summary>
        public event Action OnOpened;

        /// <summary>Игрок закрыл телефон.</summary>
        public event Action OnClosed;

        /// <summary>Открыт ли сейчас телефон.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// Можно ли вообще пользоваться телефоном. На Floor_03 телефон появляется только
        /// в онбординге HUD — до этого UIManager.SetHudVisible(Phone, false) выключает его.
        /// </summary>
        public bool IsAvailable { get; private set; } = true;

        // Блокировали ли игрока именно мы: если его уже держит сценарий, после закрытия
        // телефона управление не возвращаем — его вернёт сценарий.
        bool lockedByPhone;

        void Start()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        protected override void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            base.OnDestroy();
        }

        void Update()
        {
            if (GameInput.Phone != null && GameInput.Phone.WasPressedThisFrame()) Toggle();
        }

        void LateUpdate()
        {
            // Esc закрываем в LateUpdate: меню паузы читает Esc в Update и видит, что игрок
            // заблокирован телефоном, — поэтому пауза не откроется тем же нажатием.
            if (IsOpen && GameInput.Pause != null && GameInput.Pause.WasPressedThisFrame()) Close();
        }

        /// <summary>Открыть, если закрыт, и наоборот.</summary>
        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        /// <summary>Открыть телефон. Ничего не делает, если сейчас нельзя (см. <see cref="CanOpen"/>).</summary>
        public void Open()
        {
            if (IsOpen || !CanOpen()) return;
            IsOpen = true;
            var player = PlayerController.Instance;
            lockedByPhone = player != null && player.State == PlayerState.Normal;
            if (lockedByPhone) player.SetInputLocked(true);
            if (ui != null) ui.Show();
            Debug.Log("[Phone] Open");
            OnOpened?.Invoke();
        }

        /// <summary>Закрыть телефон. После закрытия срабатывают колбэки прочитанных сообщений.</summary>
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (ui != null) ui.Hide();
            if (lockedByPhone && PlayerController.Instance != null) PlayerController.Instance.SetInputLocked(false);
            lockedByPhone = false;
            Debug.Log("[Phone] Close");
            OnClosed?.Invoke();
        }

        /// <summary>Включает/выключает телефон целиком (кнопку прячет UIManager.SetHudVisible).</summary>
        public void SetAvailable(bool available)
        {
            IsAvailable = available;
            if (!available) Close();
        }

        /// <summary>
        /// Открыть можно на этаже (есть игрок), не на паузе, не во время перехода,
        /// не в катсцене и не поверх Tiny UFO. Если игрока держит сценарий (Locked) — можно:
        /// сцена «пришло сообщение → ждём, пока игрок прочитает» блокирует Дэнни.
        /// </summary>
        public bool CanOpen()
        {
            if (!IsAvailable) return false;
            var player = PlayerController.Instance;
            if (player == null || player.State == PlayerState.Cutscene) return false;
            if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused) return false;
            if (UIManager.Instance != null && UIManager.Instance.IsFullscreenOpen) return false;
            if (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsLoading) return false;
            // Мини-игры (считыватель, Tiny UFO) — у них свой ввод; Tiny UFO ещё и additive-сцена поверх этажа.
            if (MiniGameHost.Instance != null && MiniGameHost.Instance.IsOpen) return false;
            if (SceneManager.sceneCount > 1) return false;
            return true;
        }

        void OnActiveSceneChanged(Scene from, Scene to)
        {
            // Игрок старой сцены уничтожен — блокировку снимать не с кого.
            lockedByPhone = false;
            if (IsOpen) Close();
        }
    }
}
