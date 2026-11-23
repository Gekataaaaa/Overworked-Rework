using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overworked
{
    /// <summary>
    /// Хранит GameState и переключает этажи. Не превращать в «бога игры» —
    /// у каждой системы свой менеджер (паспорт, раздел 25).
    /// </summary>
    public class GameManager : SingletonBehaviour<GameManager>
    {
        /// <summary>Состояние текущего прохождения.</summary>
        public GameState State { get; private set; } = new GameState();

        // Этаж, спрятанный под главным меню (пауза → Main Menu): сцена загружена, её корневые объекты выключены.
        Scene hiddenFloor;
        List<GameObject> hiddenRoots;

        /// <summary>
        /// Показывать ли Continue в главном меню: этаж спрятан под меню и всё ещё в памяти.
        /// При первом запуске, после New Game и после WhiteRoom — нет.
        /// </summary>
        public bool CanContinue => hiddenFloor.IsValid() && hiddenFloor.isLoaded && hiddenRoots != null;

        /// <summary>Есть что продолжить: этаж в памяти под меню или чекпоинт на диске (SaveSystem).</summary>
        public bool CanContinueAny => CanContinue || SaveSystem.HasSave;

        /// <summary>Загружает сцену этажа: 3, 2, 1; 0 = WhiteRoom. Спрятанный под меню этаж при этом выгружается.</summary>
        public void LoadFloor(int floor)
        {
            // Во время перехода вызов проигнорируется — не меняем этаж в состоянии зря.
            if (SceneTransitionManager.Instance.IsLoading) return;
            State.CurrentFloor = floor;
            ForgetHiddenFloor();
            string scene = floor == 0 ? "WhiteRoom" : $"Floor_0{floor}";
            Debug.Log($"[Core] LoadFloor({floor}) → {scene}");
            SceneTransitionManager.Instance.Load(scene);
        }

        /// <summary>Возврат в главное меню с выгрузкой этажа (после титров, F1). Continue после этого нет.</summary>
        public void LoadMainMenu()
        {
            Debug.Log("[Core] LoadMainMenu");
            // Из Белой комнаты в меню — игра пройдена (титры): чекпоинт больше не нужен.
            if (State.CurrentFloor == 0) SaveSystem.Delete();
            ForgetHiddenFloor();
            SceneTransitionManager.Instance.Load("MainMenu");
        }

        /// <summary>
        /// Пауза → Main Menu: этаж не выгружается, а прячется под меню как есть — двери, предметы, сценарии,
        /// фонарик, задания остаются в том же состоянии. Continue вернёт его.
        /// </summary>
        public void LeaveToMainMenu()
        {
            if (SceneTransitionManager.Instance.IsLoading) return;
            Debug.Log("[Core] LeaveToMainMenu: этаж спрятан под меню");
            SceneTransitionManager.Instance.LoadOver("MainMenu", (scene, roots) =>
            {
                hiddenFloor = scene;
                hiddenRoots = roots;
            });
        }

        /// <summary>Continue: убирает меню и включает спрятанный этаж ровно в том состоянии, в каком его оставили.</summary>
        public void ContinueGame()
        {
            if (!CanContinue || SceneTransitionManager.Instance.IsLoading) return;
            Debug.Log($"[Core] Continue → {hiddenFloor.name}");
            var menu = SceneManager.GetActiveScene();
            SceneTransitionManager.Instance.RestoreHidden(menu, hiddenFloor, hiddenRoots, ForgetHiddenFloor);
        }

        /// <summary>
        /// Continue после перезапуска игры: состояние из чекпоинта на диске и загрузка того этажа с начала.
        /// Если этаж ещё в памяти под меню (пауза → Main Menu в этой сессии) — он свежее, берётся он (ContinueGame).
        /// </summary>
        public void ContinueFromSave()
        {
            if (CanContinue) { ContinueGame(); return; }
            if (SceneTransitionManager.Instance.IsLoading) return;
            ResetState();
            int floor = SaveSystem.Load();
            if (floor < 0)
            {
                Debug.LogWarning("[Core] Continue: сохранение не читается — начните новую игру");
                return;
            }
            LoadFloor(floor);
        }

        /// <summary>Новое прохождение (из главного меню): имя спросят заново, Continue пропадает, задания и телефон сбрасываются.</summary>
        public void ResetState()
        {
            State = new GameState();
            // Сам этаж выгрузится, когда пролог загрузит Floor_03 (обычная загрузка выгружает все сцены).
            ForgetHiddenFloor();
            // Задания и история телефона живут в _Systems и пережили бы новую игру.
            if (ObjectiveManager.Instance != null) ObjectiveManager.Instance.ResetAll();
            if (MessageManager.Instance != null) MessageManager.Instance.Clear();
            Hotbar.Clear();
        }

        void ForgetHiddenFloor()
        {
            hiddenFloor = default;
            hiddenRoots = null;
        }
    }
}
