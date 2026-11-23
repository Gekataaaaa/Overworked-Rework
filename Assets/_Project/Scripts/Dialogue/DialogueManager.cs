using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Overworked
{
    /// <summary>
    /// Показывает реплики Дэнни, описания объектов и системный текст субтитрами внизу экрана.
    /// Строки идут по очереди: печатаются, висят по времени (зависит от длины) и сменяются сами;
    /// Space / Enter / клик мышью — допечатать строку сразу, повторно — перейти к следующей.
    /// Если пришёл новый Play, пока идёт старый, он встаёт в очередь — колбэки done не теряются.
    /// Управление Дэнни не блокирует (это делает сценарий). Живёт в префабе _Systems,
    /// UI — объект Subtitles в HUD (Overworked → P3 → UI → Build Dialogue UI).
    /// </summary>
    public class DialogueManager : SingletonBehaviour<DialogueManager>
    {
        /// <summary>Как выглядит строка конкретного говорящего.</summary>
        [Serializable]
        public struct SpeakerStyle
        {
            public Speaker Speaker;
            [Tooltip("Шрифт строки (пусто — шрифт текста по умолчанию)")]
            public TMP_FontAsset Font;
            public Color Color;
            public FontStyles Style;
        }

        [Header("UI")]
        [Tooltip("Плашка субтитров (CanvasGroup — для плавного появления)")]
        [SerializeField] CanvasGroup group;

        [Tooltip("Текст субтитров")]
        [SerializeField] TextMeshProUGUI text;

        [Tooltip("Стили: Danny — реплики, Description — описания «ДО», System — системный текст")]
        [SerializeField] SpeakerStyle[] styles;

        [Header("Тайминги")]
        [Tooltip("Скорость печати, символов в секунду (0 — строка появляется сразу)")]
        [SerializeField] float charsPerSecond = 45f;

        [Tooltip("Сколько секунд строка висит после печати: база")]
        [SerializeField] float holdBase = 1.2f;

        [Tooltip("…плюс столько секунд за каждый символ — длинные строки висят дольше")]
        [SerializeField] float holdPerChar = 0.04f;

        [Tooltip("Длительность появления/исчезновения плашки, секунды")]
        [SerializeField] float fade = 0.2f;

        [Tooltip("Реплики, начатые в течение стольких секунд после взаимодействия (E, закрытие документа, мини-игры, телефона), не листаются сами — ждут нажатия")]
        [SerializeField] float interactionWindow = 1.5f;

        [Tooltip("Подсказка в конце реплики, которая ждёт нажатия")]
        // Без </alpha>: у TMP нет закрывающего тега alpha (он печатался текстом) — прозрачность действует до конца строки.
        [SerializeField] string pressHint = "<size=60%><alpha=#88>  [E]";

        /// <summary>Сейчас показываются субтитры (или есть очередь).</summary>
        public bool IsPlaying => routine != null;

        /// <summary>
        /// Идёт реплика после взаимодействия (листается по нажатию): E в это время листает её,
        /// InteractionSystem не взаимодействует повторно.
        /// </summary>
        public bool HoldsInteract => routine != null && currentManual;

        /// <summary>
        /// Открыт документ, мини-игра или телефон: печать и таймер реплики стоят, пока игрок не закончит.
        /// </summary>
        public static bool InteractionBusy =>
            DocumentSheet.IsOpen
            || (MiniGameHost.Instance != null && MiniGameHost.Instance.IsOpen)
            || (PhoneManager.Instance != null && PhoneManager.Instance.IsOpen);

        /// <summary>Игрок только что взаимодействовал (зовёт InteractionSystem): следующие реплики — по нажатию.</summary>
        public static void NoteInteraction() => lastInteraction = Time.unscaledTime;

        static float lastInteraction = -100f;

        /// <summary>Показана новая строка (уже с подставленным {name}).</summary>
        public event Action<Speaker, string> OnLineShown;

        struct Request
        {
            public DialogueData Data;
            public Action Done;
            public bool Manual;
        }

        readonly Queue<Request> queue = new Queue<Request>();
        Coroutine routine;
        TMP_FontAsset defaultFont;
        bool skipPressed;
        bool currentManual;
        // Строка допечатана: только теперь нажатие что-то делает (убирает её и запускает следующую).
        bool lineTyped;
        float lastBusy = -100f;
        // Последний раз был открыт документ или мини-игра (не телефон): от этого считается окно «реплики по нажатию».
        float lastInteractionBusy = -100f;

        protected override void Awake()
        {
            base.Awake();
            if (text != null) defaultFont = text.font;
            if (group != null) group.alpha = 0f;
        }

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
            if (InteractionBusy) lastBusy = Time.unscaledTime;
            if (DocumentSheet.IsOpen || (MiniGameHost.Instance != null && MiniGameHost.Instance.IsOpen))
                lastInteractionBusy = Time.unscaledTime;
            if (routine == null || !lineTyped) return;   // пока строка печатается, нажатия не действуют
            // Документ/мини-игра открыты, или их только что закрыли тем же нажатием — оно не листает реплику.
            if (Time.unscaledTime - lastBusy < 0.25f) return;
            // На паузе и при открытом телефоне нажатия относятся к ним, а не к субтитрам.
            if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused) return;
            if (PhoneManager.Instance != null && PhoneManager.Instance.IsOpen) return;
            if (UIManager.Instance != null && UIManager.Instance.IsFullscreenOpen) return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool key = kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                       || (currentManual && GameInput.Interact != null && GameInput.Interact.WasPressedThisFrame());
            // Клик по кнопке UI (например, по кнопке телефона) — не пропуск строки.
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame
                         && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
            if (key || click)
            {
                skipPressed = true;
                if (!UiSounds.MuteTyping) UiSounds.Click();
            }
        }

        /// <summary>
        /// Проигрывает все строки по очереди, затем вызывает done. canWaitForPress = false — строки всегда
        /// сменяются сами (реплики сценариев: игрок заблокирован и не должен «зависнуть» в ожидании нажатия).
        /// </summary>
        public void Play(DialogueData d, Action done = null, bool canWaitForPress = true)
        {
            if (d == null || d.Lines == null || d.Lines.Length == 0)
            {
                done?.Invoke();
                return;
            }
            Debug.Log($"[Dialogue] Play {d.name}");
            // После взаимодействия (E по предмету, документ, мини-игра — не телефон) или по цепочке от такой
            // реплики — листается по нажатию. Реплики сценариев — всегда сами.
            float since = Time.unscaledTime - Mathf.Max(lastInteraction, lastInteractionBusy);
            bool interactionBusy = DocumentSheet.IsOpen || (MiniGameHost.Instance != null && MiniGameHost.Instance.IsOpen);
            bool manual = canWaitForPress
                          && (since < interactionWindow || interactionBusy || (routine != null && currentManual));
            queue.Enqueue(new Request { Data = d, Done = done, Manual = manual });
            if (routine == null) routine = StartCoroutine(Run());
        }

        /// <summary>Обрывает субтитры и очередь без вызова done (смена сцены, скример).</summary>
        public void Stop()
        {
            queue.Clear();
            currentManual = false;
            UiSounds.TypingStop();
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (group != null) group.alpha = 0f;
        }

        IEnumerator Run()
        {
            if (group == null || text == null)
            {
                // UI ещё не собран — хотя бы не ломаем сценарии: печатаем в консоль и идём дальше.
                while (queue.Count > 0)
                {
                    var r = queue.Dequeue();
                    foreach (var line in r.Data.Lines)
                        Debug.Log($"[Dialogue] {line.Speaker}: {TextFormatter.Format(line.Text)}");
                    r.Done?.Invoke();
                }
                routine = null;
                yield break;
            }

            do
            {
                yield return Fade(1f);
                while (queue.Count > 0)
                {
                    var r = queue.Dequeue();
                    currentManual = r.Manual;
                    foreach (var line in r.Data.Lines)
                        yield return ShowLine(line, r.Manual);
                    // done может сразу добавить новый Play — он попадёт в эту же очередь.
                    r.Done?.Invoke();
                }
                currentManual = false;
                yield return Fade(0f);
                text.text = "";
                // Play мог прийти во время затухания (сценарий ждёт колбэк через WaitCallback и зовёт следующий
                // DialogueStep только кадром позже): routine ещё не null, новый Run не стартовал — подхватываем сами,
                // иначе реплика навсегда остаётся в очереди, сценарий не доходит до конца и Дэнни остаётся заблокирован.
            } while (queue.Count > 0);
            routine = null;
        }

        IEnumerator ShowLine(DialogueLine line, bool manual)
        {
            string s = TextFormatter.Format(line.Text);
            ApplyStyle(line.Speaker);
            text.text = s;
            text.maxVisibleCharacters = 0;
            text.ForceMeshUpdate();
            int total = text.textInfo.characterCount;
            OnLineShown?.Invoke(line.Speaker, s);
            skipPressed = false;
            lineTyped = false;

            // Печать. Время игровое (не unscaled): на паузе субтитры замирают вместе с миром.
            if (charsPerSecond > 0f)
            {
                float shown = 0f;
                // Печать идёт своей скоростью до конца — допечатать нажатием нельзя.
                while (shown < total)
                {
                    if (!InteractionBusy) shown += charsPerSecond * Time.deltaTime;   // документ/мини-игра — ждём
                    int visible = Mathf.Min(total, Mathf.FloorToInt(shown));
                    // Звук «бегущего текста» (из Tiny UFO) играет, пока появляются новые буквы.
                    if (visible > text.maxVisibleCharacters) UiSounds.TypingTick();
                    text.maxVisibleCharacters = visible;
                    yield return null;
                }
            }
            text.maxVisibleCharacters = int.MaxValue;
            UiSounds.TypingStop();
            skipPressed = false;
            lineTyped = true;

            if (manual)
            {
                // Реплика после взаимодействия висит, пока игрок не нажмёт (E/Space/Enter/клик).
                if (!string.IsNullOrEmpty(pressHint)) text.text = s + pressHint;
                while (!skipPressed) yield return null;
                skipPressed = false;
                yield break;
            }

            float hold = holdBase + holdPerChar * total;
            for (float t = 0f; t < hold && !skipPressed; )
            {
                // Пока открыт документ/мини-игра/телефон — время строки стоит.
                if (!InteractionBusy) t += Time.deltaTime;
                yield return null;
            }
            skipPressed = false;
        }

        void ApplyStyle(Speaker speaker)
        {
            if (styles != null)
                foreach (var st in styles)
                    if (st.Speaker == speaker)
                    {
                        text.font = st.Font != null ? st.Font : defaultFont;
                        text.color = st.Color;
                        text.fontStyle = st.Style;
                        return;
                    }
            text.font = defaultFont;
            text.color = Color.white;
            text.fontStyle = FontStyles.Normal;
        }

        IEnumerator Fade(float to)
        {
            float from = group.alpha;
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / fade);
                yield return null;
            }
            group.alpha = to;
        }

        void OnActiveSceneChanged(Scene from, Scene to)
        {
            // Реплики старой сцены в новой не нужны, а их колбэки ссылаются на уничтоженные объекты.
            Stop();
        }
    }
}
