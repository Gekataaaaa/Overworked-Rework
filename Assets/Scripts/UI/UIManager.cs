using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Overworked
{
    /// <summary>
    /// Общие окна интерфейса: тосты, текст на весь экран (записка), подсказка управления,
    /// видимость частей HUD. Живёт в префабе _Systems; ссылки на элементы HUD проставляют
    /// меню Overworked → P3 → UI → Build Phone UI и Build HUD.
    /// </summary>
    public class UIManager : SingletonBehaviour<UIManager>
    {
        [Header("Тост")]
        [Tooltip("Плашка тоста (CanvasGroup — для плавного появления)")]
        [SerializeField] CanvasGroup toastGroup;

        [Tooltip("Текст тоста")]
        [SerializeField] TextMeshProUGUI toastText;

        [Tooltip("Сколько секунд тост висит на экране")]
        [SerializeField] float toastSeconds = 2.5f;

        [Tooltip("Длительность появления/исчезновения тоста, секунды")]
        [SerializeField] float toastFade = 0.25f;

        [Header("Печать текста")]
        [Tooltip("Скорость печати записки и подсказки управления, символов в секунду (0 — сразу)")]
        [SerializeField] float windowCharsPerSecond = 40f;

        [Tooltip("Скорость печати тоста, символов в секунду (0 — сразу)")]
        [SerializeField] float toastCharsPerSecond = 60f;

        [Header("Окна на весь экран")]
        [Tooltip("Окно текста на весь экран (записка, системное сообщение)")]
        [SerializeField] GameObject fullscreenRoot;

        [Tooltip("Текст окна на весь экран")]
        [SerializeField] TextMeshProUGUI fullscreenText;

        [Tooltip("Подсказка управления (начало Floor_03)")]
        [SerializeField] GameObject controlsHintRoot;

        [Header("HUD")]
        [Tooltip("Интерфейс телефона — для SetHudVisible(Phone)")]
        [SerializeField] PhoneUI phoneUI;

        [Tooltip("Шкала рассудка (левый верх)")]
        [SerializeField] GameObject sanityRoot;

        [Tooltip("Пульс (левый верх, под шкалой рассудка)")]
        [SerializeField] GameObject heartbeatRoot;

        /// <summary>Открыто окно на весь экран или подсказка управления — ввод принадлежит им.</summary>
        public bool IsFullscreenOpen => openWindow != null;

        readonly Queue<string> toasts = new Queue<string>();
        Coroutine toastRoutine;

        // Части HUD, которые разрешены сценарием. Показываются ещё и только на этаже (есть игрок).
        readonly HashSet<HudPart> hidden = new HashSet<HudPart>();

        GameObject openWindow;
        float hudAlpha = 1f;

        // Пауза → Main Menu прячет этаж под меню; Continue вернёт его как был — и HUD тоже должен быть как был.
        Scene snapshotScene;
        HashSet<HudPart> snapshotHidden;
        float snapshotAlpha = 1f;
        Action onWindowClosed;
        int openedFrame;
        bool lockedByWindow;
        Coroutine windowTyping;
        bool windowTypingSkip;

        void Start()
        {
            if (toastGroup != null) toastGroup.alpha = 0f;
            if (fullscreenRoot != null) fullscreenRoot.SetActive(false);
            if (controlsHintRoot != null) controlsHintRoot.SetActive(false);
            UpdateHudParts();
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        protected override void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            base.OnDestroy();
        }

        void OnActiveSceneChanged(Scene from, Scene to)
        {
            // Ушли в меню, а этаж остался в памяти под ним (сцен загружено больше одной) — запоминаем HUD этажа.
            if (to.name == "MainMenu" && SceneManager.sceneCount > 1)
            {
                snapshotScene = from;
                snapshotHidden = new HashSet<HudPart>(hidden);
                snapshotAlpha = hudAlpha;
            }
            // Continue в тот же этаж: возвращаем ровно то, что было видно до выхода (Start сцены заново не вызовется).
            else if (snapshotHidden != null && to == snapshotScene)
            {
                foreach (HudPart part in Enum.GetValues(typeof(HudPart))) SetHudVisible(part, !snapshotHidden.Contains(part));
                SetHudAlpha(snapshotAlpha);
                snapshotHidden = null;
                return;
            }
            else snapshotHidden = null;

            // Новая сцена начинается с полного HUD: то, что спрятал онбординг или сценарий прошлой сцены,
            // не должно переехать на следующий этаж (Continue, переход по лестнице). Сцена, которой HUD
            // не нужен целиком (WhiteRoom), или онбординг прячут его заново в своём Start.
            if (openWindow != null)
            {
                // Окно старой сцены: колбэк ссылается на уничтоженные объекты — закрываем без него.
                onWindowClosed = null;
                lockedByWindow = false;
                CloseWindow();
            }
            if (hudAlpha < 1f) SetHudAlpha(1f);
            if (hidden.Count == 0) return;
            foreach (var part in new List<HudPart>(hidden)) SetHudVisible(part, true);
        }

        void Update()
        {
            UpdateHudParts();
        }

        void LateUpdate()
        {
            // Окно открыто тем же нажатием E, которым игрок взял записку, — в этом кадре не закрываем.
            if (openWindow == null || Time.frameCount == openedFrame) return;
            // Закрываем в LateUpdate: в Update этого кадра пауза (Esc) и InteractionSystem (E) уже видели
            // заблокированного игрока — то же нажатие не откроет паузу и не возьмёт соседний предмет.
            if (ClosePressed())
            {
                UiSounds.Click();
                // Первое нажатие допечатывает текст сразу, второе — закрывает окно.
                if (windowTyping != null) windowTypingSkip = true;
                else CloseWindow();
            }
        }

        static bool ClosePressed()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            return (GameInput.Interact != null && GameInput.Interact.WasPressedThisFrame())
                   || (GameInput.Pause != null && GameInput.Pause.WasPressedThisFrame())
                   || (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                   || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        }

        // ------------------------------------------------------------ тосты

        /// <summary>
        /// Короткое уведомление: «Received: Charles's note». Несколько подряд показываются по очереди.
        /// Текст проходит через TextFormatter ({name}).
        /// </summary>
        public void ShowToast(string text)
        {
            Debug.Log($"[UI] Toast: {text}");
            if (toastGroup == null || toastText == null || string.IsNullOrEmpty(text)) return;
            toasts.Enqueue(TextFormatter.Format(text));
            if (toastRoutine == null) toastRoutine = StartCoroutine(PlayToasts());
        }

        IEnumerator PlayToasts()
        {
            while (toasts.Count > 0)
            {
                toastText.text = toasts.Dequeue();
                toastText.maxVisibleCharacters = 0;
                yield return FadeToast(1f);
                // Тост печатается без звука: он часто совпадает с субтитрами или вибрацией телефона.
                yield return Typewriter.Type(toastText, toastCharsPerSecond, null, false);
                // Realtime: тост дочитывается, даже если время остановлено паузой.
                yield return new WaitForSecondsRealtime(toastSeconds);
                yield return FadeToast(0f);
            }
            toastRoutine = null;
        }

        IEnumerator FadeToast(float to)
        {
            float from = toastGroup.alpha;
            for (float t = 0; t < toastFade; t += Time.unscaledDeltaTime)
            {
                toastGroup.alpha = Mathf.Lerp(from, to, t / toastFade);
                yield return null;
            }
            toastGroup.alpha = to;
        }

        // ------------------------------------------------------------ окна на весь экран

        /// <summary>
        /// Текст на весь экран (записка, системное сообщение). Закрывается E / Space / Enter / кликом / Esc,
        /// затем closed. Дэнни стоит, пока окно открыто (если его уже держит сценарий — управление не возвращаем).
        /// Текст проходит через TextFormatter ({name}).
        /// </summary>
        public void ShowFullscreenText(string text, Action closed)
        {
            Debug.Log($"[UI] Fullscreen: {text}");
            if (fullscreenRoot == null || fullscreenText == null)
            {
                closed?.Invoke();
                return;
            }
            fullscreenText.text = TextFormatter.Format(text);
            OpenWindow(fullscreenRoot, closed);
        }

        /// <summary>Подсказка управления (онбординг Floor_03). closed — когда игрок её закрыл.</summary>
        public void ShowControlsHint(Action closed)
        {
            Debug.Log("[UI] Controls hint");
            if (controlsHintRoot == null)
            {
                closed?.Invoke();
                return;
            }
            OpenWindow(controlsHintRoot, closed);
        }

        void OpenWindow(GameObject window, Action closed)
        {
            // Новое окно поверх старого: старое закрываем честно, чтобы его closed не потерялся.
            if (openWindow != null) CloseWindow();
            openWindow = window;
            onWindowClosed = closed;
            openedFrame = Time.frameCount;
            var player = PlayerController.Instance;
            lockedByWindow = player != null && player.State == PlayerState.Normal;
            if (lockedByWindow) player.SetInputLocked(true);
            if (PhoneManager.Instance != null) PhoneManager.Instance.Close();
            window.SetActive(true);
            windowTypingSkip = false;
            windowTyping = StartCoroutine(TypeWindow(window));
        }

        /// <summary>Печатает все тексты окна по очереди (заголовок, строки, подсказка «E — close»).</summary>
        IEnumerator TypeWindow(GameObject window)
        {
            var texts = window.GetComponentsInChildren<TMP_Text>(false);
            // Сначала прячем все, чтобы нижние строки не висели готовыми, пока печатается верхняя.
            foreach (var t in texts) t.maxVisibleCharacters = 0;
            foreach (var t in texts)
                yield return Typewriter.Type(t, windowCharsPerSecond, () => windowTypingSkip);
            foreach (var t in texts) Typewriter.ShowAll(t);
            windowTyping = null;
        }

        void CloseWindow()
        {
            if (openWindow == null) return;
            if (windowTyping != null) StopCoroutine(windowTyping);
            windowTyping = null;
            UiSounds.TypingStop();
            openWindow.SetActive(false);
            openWindow = null;
            if (lockedByWindow && PlayerController.Instance != null) PlayerController.Instance.SetInputLocked(false);
            lockedByWindow = false;
            var cb = onWindowClosed;
            onWindowClosed = null;
            cb?.Invoke();
        }

        // ------------------------------------------------------------ HUD

        /// <summary>
        /// Показать или спрятать часть HUD (онбординг Floor_03, WhiteRoom). Phone — ещё и выключает
        /// сам телефон, Objective — карточку задания P2. Части HUD видны только на этаже (есть игрок).
        /// </summary>
        public void SetHudVisible(HudPart part, bool visible)
        {
            Debug.Log($"[UI] {part} visible={visible}");
            if (visible) hidden.Remove(part);
            else hidden.Add(part);

            switch (part)
            {
                case HudPart.Phone:
                    if (phoneUI != null) phoneUI.SetButtonVisible(visible);
                    if (PhoneManager.Instance != null) PhoneManager.Instance.SetAvailable(visible);
                    break;
                case HudPart.Objective:
                    if (ObjectiveUI.Instance != null) ObjectiveUI.Instance.SetVisible(visible);
                    break;
            }
            UpdateHudParts();
        }

        /// <summary>Показать/спрятать весь HUD разом (WhiteRoom, финальный скример).</summary>
        public void SetHudVisible(bool visible)
        {
            foreach (HudPart part in Enum.GetValues(typeof(HudPart))) SetHudVisible(part, visible);
        }

        /// <summary>Видна ли часть HUD по решению сценария.</summary>
        public bool IsHudVisible(HudPart part) => !hidden.Contains(part);

        /// <summary>
        /// Прозрачность рассудка, пульса и кнопки телефона 0..1 (1 — обычный вид). Для плавного исчезновения
        /// HUD (смерть в WhiteRoom). Видимость частей (SetHudVisible) не меняет; при смене сцены сбрасывается в 1.
        /// </summary>
        public void SetHudAlpha(float alpha)
        {
            hudAlpha = Mathf.Clamp01(alpha);
            ApplyAlpha(sanityRoot);
            ApplyAlpha(heartbeatRoot);
            if (phoneUI != null && phoneUI.ButtonRect != null) ApplyAlpha(phoneUI.ButtonRect.gameObject);
        }

        void ApplyAlpha(GameObject go)
        {
            if (go == null) return;
            var group = go.GetComponent<CanvasGroup>();
            if (group == null)
            {
                if (hudAlpha >= 1f) return;
                group = go.AddComponent<CanvasGroup>();
            }
            group.alpha = hudAlpha;
        }

        void UpdateHudParts()
        {
            // HUD живёт в _Systems всю игру, а рассудок и пульс нужны только на этаже, где есть Дэнни.
            bool onFloor = PlayerController.Instance != null;
            SetActive(sanityRoot, onFloor && !hidden.Contains(HudPart.Sanity));
            SetActive(heartbeatRoot, onFloor && !hidden.Contains(HudPart.Heartbeat));
        }

        static void SetActive(GameObject go, bool value)
        {
            if (go != null && go.activeSelf != value) go.SetActive(value);
        }
    }
}
