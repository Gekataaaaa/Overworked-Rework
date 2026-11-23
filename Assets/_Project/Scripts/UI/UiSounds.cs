using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Overworked
{
    /// <summary>
    /// Звуки интерфейса (взяты из Tiny UFO): «бегущий текст» при печати субтитров, клик на
    /// «нажми, чтобы продолжить» (субтитры, записка, подсказка управления) и на нажатие кнопок,
    /// короткий бип при выделении кнопки (стрелки/мышь — главное меню, пауза, телефон).
    /// Создаёт себя сам при старте игры (DontDestroyOnLoad); клипы — Audio/UI/Resources.
    /// </summary>
    public class UiSounds : MonoBehaviour
    {
        /// <summary>Единственный экземпляр (создаётся сам).</summary>
        public static UiSounds Instance { get; private set; }

        [Tooltip("Длинный звук «бегущего текста»: играет по кругу, пока печатаются буквы")]
        [SerializeField] AudioClip typing;

        [Tooltip("Клик: продолжить / нажать кнопку")]
        [SerializeField] AudioClip click;

        [Range(0f, 1f)]
        [Tooltip("Громкость печати")]
        [SerializeField] float typingVolume = 0.42f;

        [Tooltip("Скорость печати-звука: 0.8 — на 20% медленнее, как в Tiny UFO")]
        [SerializeField] float typingPitch = 0.8f;

        [Tooltip("Если новых букв нет дольше этого (секунд) — звук печати смолкает")]
        [SerializeField] float typingTimeout = 0.12f;

        [Range(0f, 1f)]
        [Tooltip("Громкость клика")]
        [SerializeField] float clickVolume = 0.7f;

        [Range(0f, 1f)]
        [Tooltip("Громкость бипа выделения кнопки")]
        [SerializeField] float selectVolume = 1f;

        [Tooltip("С какой секунды клипа печати берётся бип: в UI_Typing первые 0.08 с — тишина, дальше удары каждые ~0.07 с")]
        [SerializeField] float selectBlipStart = 0.08f;

        [Tooltip("Длина бипа выделения, секунды реального времени — ровно один удар печати")]
        [SerializeField] float selectBlipLength = 0.075f;

        AudioSource typingSource, uiSource, blipSource;
        float lastTypeTick = -10f, blipStopAt = -1f;
        GameObject lastSelected, lastHovered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            if (Instance != null) return;
            var go = new GameObject("UiSounds");
            DontDestroyOnLoad(go);
            go.AddComponent<UiSounds>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (typing == null) typing = Resources.Load<AudioClip>("UI_Typing");
            if (click == null) click = Resources.Load<AudioClip>("UI_Click");

            typingSource = NewSource(loop: true, ignorePause: false);
            typingSource.clip = typing;
            typingSource.pitch = typingPitch;
            typingSource.volume = typingVolume;
            // Кнопки паузы должны звучать, хотя пауза глушит игру (AudioListener.pause).
            uiSource = NewSource(loop: false, ignorePause: true);
            blipSource = NewSource(loop: false, ignorePause: true);
            blipSource.clip = typing;
            blipSource.pitch = typingPitch;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        AudioSource NewSource(bool loop, bool ignorePause)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.ignoreListenerPause = ignorePause;
            return s;
        }

        // ------------------------------------------------------------ API

        /// <summary>
        /// Заглушить звук печати субтитров и клик пропуска реплики (Белая комната: полная тишина, кроме шагов
        /// и писка монитора). Клики меню не трогает. Ставит и снимает WhiteRoomSequence.
        /// </summary>
        public static bool MuteTyping { get; set; }

        // Enter Play Mode без перезагрузки домена: тишина Белой комнаты не должна перейти в следующий запуск.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMute() => MuteTyping = false;

        /// <summary>Напечатана новая буква — звук печати играет, пока буквы идут (зовут каждый кадр печати).</summary>
        public static void TypingTick()
        {
            var s = Instance;
            if (s == null || s.typing == null || MuteTyping) return;
            s.lastTypeTick = Time.unscaledTime;
            if (!s.typingSource.isPlaying) s.typingSource.Play();
        }

        /// <summary>Печать закончилась или прервана — звук сразу смолкает.</summary>
        public static void TypingStop()
        {
            if (Instance != null && Instance.typingSource.isPlaying) Instance.typingSource.Stop();
        }

        /// <summary>Клик: «продолжить» или нажатие кнопки.</summary>
        public static void Click()
        {
            var s = Instance;
            if (s != null && s.click != null) s.uiSource.PlayOneShot(s.click, s.clickVolume);
        }

        /// <summary>Одиночный бип выделения кнопки — один удар звука печати.</summary>
        public static void SelectBlip()
        {
            var s = Instance;
            if (s == null || s.typing == null) return;
            s.blipSource.volume = s.selectVolume;
            s.blipSource.Stop();
            // Один удар печати: начинаем с первого удара в клипе, а не с тишины в его начале.
            s.blipSource.time = Mathf.Min(s.selectBlipStart, s.typing.length - 0.01f);
            s.blipSource.Play();
            s.blipStopAt = Time.unscaledTime + s.selectBlipLength;
        }

        // ------------------------------------------------------------ кнопки

        void Update()
        {
            if (typingSource.isPlaying && Time.unscaledTime - lastTypeTick > typingTimeout) typingSource.Stop();
            if (blipSource.isPlaying && blipStopAt > 0f && Time.unscaledTime >= blipStopAt)
            {
                blipSource.Stop();
                blipStopAt = -1f;
            }
            WatchButtons();
        }

        void WatchButtons()
        {
            var es = EventSystem.current;
            if (es == null) return;

            // Выделение стрелками / Select() в коде.
            var selected = es.currentSelectedGameObject;
            if (selected != lastSelected)
            {
                // Первое выделение при открытии меню — без бипа: меню само ставит фокус на кнопку.
                if (lastSelected != null && IsActiveSelectable(selected)) SelectBlip();
                lastSelected = selected;
            }

            // Наведение мышью: кнопка под курсором сменилась.
            var mouse = Mouse.current;
            GameObject hovered = null;
            if (mouse != null)
            {
                var data = new PointerEventData(es) { position = mouse.position.ReadValue() };
                var hits = new System.Collections.Generic.List<RaycastResult>();
                es.RaycastAll(data, hits);
                foreach (var h in hits)
                {
                    var sel = h.gameObject.GetComponentInParent<Selectable>();
                    if (sel != null && sel.IsInteractable()) { hovered = sel.gameObject; break; }
                }
            }
            if (hovered != lastHovered)
            {
                // Наведение на ту же кнопку, что уже выделена стрелками, — не бипаем дважды.
                if (hovered != null && hovered != selected) SelectBlip();
                lastHovered = hovered;
            }

            // Нажатие кнопки: клик по ней мышью или Enter/Space на выделенной.
            var kb = Keyboard.current;
            bool mouseClick = mouse != null && mouse.leftButton.wasPressedThisFrame && hovered != null
                              && hovered.GetComponent<Button>() != null;
            // Space — только в меню и на паузе: на этаже Space пропускает субтитры (у них свой клик),
            // а кнопка телефона после клика мышью остаётся выделенной.
            bool inMenu = PlayerController.Instance == null || (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused);
            bool submitKey = kb != null && (kb.enterKey.wasPressedThisFrame || (inMenu && kb.spaceKey.wasPressedThisFrame));
            bool submit = submitKey && selected != null && selected.GetComponent<Button>() != null && IsActiveSelectable(selected);
            if (mouseClick || submit) Click();
        }

        static bool IsActiveSelectable(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return false;
            var sel = go.GetComponent<Selectable>();
            return sel != null && sel.IsInteractable();
        }
    }
}
