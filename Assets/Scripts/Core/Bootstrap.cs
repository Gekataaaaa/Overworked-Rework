using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Создаёт префаб _Systems (все менеджеры + HUD) один раз до загрузки первой сцены.
    /// Поэтому любую сцену можно запускать прямо из редактора.
    /// </summary>
    public static class Bootstrap
    {
        /// <summary>Путь префаба внутри папки Resources.</summary>
        public const string SystemsPath = "_Systems";

        /// <summary>Путь префаба меню паузы внутри папки Resources.</summary>
        public const string PauseMenuPath = "PauseMenu";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateSystems()
        {
            if (GameManager.Instance != null) return;
            var prefab = Resources.Load<GameObject>(SystemsPath);
            if (prefab == null)
            {
                Debug.LogError($"[Core] нет Resources/{SystemsPath}.prefab");
                return;
            }
            var systems = Object.Instantiate(prefab);
            systems.name = SystemsPath;
            Object.DontDestroyOnLoad(systems);

            // Пауза — отдельный префаб, чтобы не править _Systems одновременно с P3 (HUD).
            var pause = Resources.Load<GameObject>(PauseMenuPath);
            if (pause != null) Object.Instantiate(pause, systems.transform).name = PauseMenuPath;
            else Debug.LogWarning($"[Core] нет Resources/{PauseMenuPath}.prefab — пауза недоступна");

            // В редакторе и development-билде — отладочные F1–F6 (переходы по сценам).
            if (Debug.isDebugBuild) systems.AddComponent<DebugHotkeys>();
        }
    }
}
