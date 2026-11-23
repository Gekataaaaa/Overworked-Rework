using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overworked
{
    /// <summary>
    /// Смена сцен через затемнение: блок игрока → fade out → асинхронная загрузка → fade in.
    /// Живёт в _Systems. Этажи грузятся через GameManager.LoadFloor, меню — через LoadMainMenu.
    /// </summary>
    public class SceneTransitionManager : SingletonBehaviour<SceneTransitionManager>
    {
        [Tooltip("Секунд на уход в чёрное")]
        [SerializeField] float fadeOutTime = 0.6f;

        [Tooltip("Секунд на проявление новой сцены")]
        [SerializeField] float fadeInTime = 0.8f;

        [Tooltip("Пауза на чёрном экране после загрузки, секунд")]
        [SerializeField] float holdBlack = 0.2f;

        /// <summary>Идёт переход: новые вызовы Load игнорируются.</summary>
        public bool IsLoading { get; private set; }

        /// <summary>Сцена загружена (экран ещё чёрный, сразу после этого начинается fade in).</summary>
        public event Action<string> OnSceneLoaded;

        /// <summary>Экран начинает проявляться (конец затемнения любого перехода): тут гаснет звук лестницы.</summary>
        public event Action FadeInStarted;

        /// <summary>Загружает сцену по имени (fade out → load → fade in). Повторный вызов во время перехода игнорируется.</summary>
        public void Load(string sceneName)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"[Core] Load({sceneName}) проигнорирован: уже идёт переход");
                return;
            }
            StartCoroutine(LoadRoutine(sceneName));
        }

        IEnumerator LoadRoutine(string sceneName)
        {
            IsLoading = true;
            Debug.Log($"[Core] Load scene {sceneName}");
            if (PlayerController.Instance != null) PlayerController.Instance.SetInputLocked(true);

            yield return Fade(true, fadeOutTime);

            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[Core] сцены {sceneName} нет в Build Settings");
                yield return Fade(false, fadeInTime);
                IsLoading = false;
                yield break;
            }
            while (!op.isDone) yield return null;

            // Ждём кадр: Start() объектов сцены (FloorSettings, камеры) должен отработать до проявления.
            yield return null;
            OnSceneLoaded?.Invoke(sceneName);
            yield return new WaitForSecondsRealtime(holdBlack);

            yield return Fade(false, fadeInTime);
            IsLoading = false;
        }

        /// <summary>
        /// Грузит сцену поверх текущей (additive), а текущую прячет — выключает её корневые объекты, ничего не выгружая.
        /// hidden(спрятанная сцена, выключенные объекты) вызывается на чёрном экране. Так работает пауза → Main Menu:
        /// этаж остаётся в памяти как есть, Continue вернёт его через RestoreHidden.
        /// </summary>
        public void LoadOver(string sceneName, Action<Scene, List<GameObject>> hidden)
        {
            if (IsLoading) return;
            StartCoroutine(LoadOverRoutine(sceneName, hidden));
        }

        /// <summary>Выгружает сцену overlay и включает обратно спрятанную сцену (Continue).</summary>
        public void RestoreHidden(Scene overlay, Scene hiddenScene, List<GameObject> hiddenRoots, Action done = null)
        {
            if (IsLoading) return;
            StartCoroutine(RestoreRoutine(overlay, hiddenScene, hiddenRoots, done));
        }

        IEnumerator LoadOverRoutine(string sceneName, Action<Scene, List<GameObject>> hidden)
        {
            IsLoading = true;
            if (PlayerController.Instance != null) PlayerController.Instance.SetInputLocked(true);
            yield return Fade(true, fadeOutTime);

            var current = SceneManager.GetActiveScene();
            var roots = new List<GameObject>();
            foreach (var go in current.GetRootGameObjects())
            {
                if (!go.activeSelf) continue;
                roots.Add(go);
                go.SetActive(false);
            }
            hidden?.Invoke(current, roots);

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (op != null && !op.isDone) yield return null;
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
            yield return null;
            yield return new WaitForSecondsRealtime(holdBlack);
            yield return Fade(false, fadeInTime);
            IsLoading = false;
        }

        IEnumerator RestoreRoutine(Scene overlay, Scene hiddenScene, List<GameObject> hiddenRoots, Action done)
        {
            IsLoading = true;
            yield return Fade(true, fadeOutTime);

            var op = SceneManager.UnloadSceneAsync(overlay);
            while (op != null && !op.isDone) yield return null;
            SceneManager.SetActiveScene(hiddenScene);
            foreach (var go in hiddenRoots) if (go != null) go.SetActive(true);
            // Пауза перед уходом в меню вернула управление; на всякий случай — снова свободен.
            if (PlayerController.Instance != null) PlayerController.Instance.SetInputLocked(false);
            yield return null;
            done?.Invoke();
            yield return new WaitForSecondsRealtime(holdBlack);
            yield return Fade(false, fadeInTime);
            IsLoading = false;
        }

        IEnumerator Fade(bool toBlack, float sec)
        {
            if (!toBlack) FadeInStarted?.Invoke();
            var fade = FadeController.Instance;
            if (fade == null) yield break;
            bool done = false;
            if (toBlack) fade.FadeOut(sec, () => done = true);
            else fade.FadeIn(sec, () => done = true);
            while (!done) yield return null;
        }
    }
}
