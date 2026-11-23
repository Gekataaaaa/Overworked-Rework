using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Overworked
{
    /// <summary>
    /// Интерфейс телефона в HUD: кнопка в правом нижнем углу (точка непрочитанных, тряска при вибрации),
    /// список контактов Max / Boss и чат с историей. Ответить нельзя.
    /// Открывает/закрывает <see cref="PhoneManager"/>; данные берутся из <see cref="MessageManager"/>.
    /// Собирается меню Overworked → P3 → UI → Build Phone UI.
    /// </summary>
    public class PhoneUI : MonoBehaviour
    {
        [Header("Кнопка телефона")]
        [Tooltip("Кнопка на экране, открывает телефон")]
        [SerializeField] Button phoneButton;

        [Tooltip("Точка «есть непрочитанные» на кнопке")]
        [SerializeField] GameObject phoneButtonDot;

        [Header("Окно телефона")]
        [Tooltip("Окно телефона целиком")]
        [SerializeField] GameObject panel;

        [Tooltip("Заголовок окна: «Messages» или имя контакта")]
        [SerializeField] TextMeshProUGUI title;

        [Tooltip("Кнопка «назад» к списку контактов (видна в чате)")]
        [SerializeField] Button backButton;

        [Tooltip("Список контактов")]
        [SerializeField] GameObject contactsView;

        [Tooltip("Контакт Max")]
        [SerializeField] Button maxContact;

        [Tooltip("Точка непрочитанных у Max")]
        [SerializeField] GameObject maxDot;

        [Tooltip("Контакт Boss")]
        [SerializeField] Button bossContact;

        [Tooltip("Точка непрочитанных у Boss")]
        [SerializeField] GameObject bossDot;

        [Header("Чат")]
        [Tooltip("Экран чата")]
        [SerializeField] GameObject chatView;

        [Tooltip("Прокрутка чата")]
        [SerializeField] ScrollRect chatScroll;

        [Tooltip("Контейнер пузырей сообщений (VerticalLayoutGroup)")]
        [SerializeField] RectTransform chatContent;

        [Tooltip("Шаблон пузыря (выключен), внутри TextMeshProUGUI")]
        [SerializeField] GameObject bubbleTemplate;

        [Header("Тексты")]
        [Tooltip("Заголовок списка контактов")]
        [SerializeField] string contactsTitle = "Messages";

        [Tooltip("Имя контакта Max в заголовке чата")]
        [SerializeField] string maxName = "Max";

        [Tooltip("Имя контакта Boss в заголовке чата")]
        [SerializeField] string bossName = "Boss";

        [Header("Печать")]
        [Tooltip("Скорость печати новых сообщений, символов в секунду (0 — сразу)")]
        [SerializeField] float charsPerSecond = 16f;

        [Header("Вибрация")]
        [Tooltip("Сколько секунд трясётся кнопка при новом сообщении")]
        [SerializeField] float shakeSeconds = 0.6f;

        [Tooltip("Угол тряски кнопки, градусы")]
        [SerializeField] float shakeAngle = 12f;

        // null — открыт список контактов.
        Sender? openChat;
        readonly List<GameObject> bubbles = new List<GameObject>();
        Coroutine shake;
        Coroutine scrollRoutine;
        Coroutine typingRoutine;
        // Непрочитанные сообщения открытого чата: их пузыри печатаются по буквам, история — сразу.
        readonly HashSet<MessageData> toType = new HashSet<MessageData>();
        bool subscribed;
        // Кнопку разрешил HUD (онбординг Floor_03 может её спрятать). Видна она ещё и только при игроке.
        bool shownByHud = true;

        void Start()
        {
            phoneButton.onClick.AddListener(() => PhoneManager.Instance?.Toggle());
            backButton.onClick.AddListener(ShowContacts);
            maxContact.onClick.AddListener(() => OpenChat(Sender.Max));
            bossContact.onClick.AddListener(() => OpenChat(Sender.Boss));
            bubbleTemplate.SetActive(false);
            panel.SetActive(false);
            EnsureScrollCatchesMouse();
            TrySubscribe();
            RefreshDots();
            UpdateButtonVisibility();
        }

        void OnDestroy()
        {
            if (subscribed && MessageManager.Instance != null)
            {
                MessageManager.Instance.OnChanged -= HandleChanged;
                MessageManager.Instance.OnDelivered -= HandleDelivered;
            }
        }

        void EnsureScrollCatchesMouse()
        {
            // Колесо мыши доходит до ScrollRect, только если под курсором есть Graphic с raycastTarget.
            // У пузырей он выключен, поэтому нужна прозрачная подложка на viewport. Сборщик её ставит,
            // но префаб, собранный старой версией сборщика, без неё — добавляем здесь.
            var viewport = chatScroll.viewport != null ? chatScroll.viewport : (RectTransform)chatScroll.transform;
            if (viewport.GetComponent<Graphic>() == null) viewport.gameObject.AddComponent<Image>().color = Color.clear;
        }

        void TrySubscribe()
        {
            if (subscribed || MessageManager.Instance == null) return;
            MessageManager.Instance.OnChanged += HandleChanged;
            MessageManager.Instance.OnDelivered += HandleDelivered;
            subscribed = true;
        }

        void Update()
        {
            UpdateButtonVisibility();
            if (!panel.activeSelf || GameInput.Move == null) return;
            // Клавиатура: A/D (стрелки) переключают чат Max ↔ Boss. Игрок в это время стоит.
            if (!GameInput.Move.WasPressedThisFrame()) return;
            float x = GameInput.Move.ReadValue<float>();
            if (x < -0.5f) OpenChat(Sender.Max);
            else if (x > 0.5f) OpenChat(Sender.Boss);
        }

        /// <summary>Показать окно. Если есть непрочитанные — сразу открыть чат самого раннего из них.</summary>
        public void Show()
        {
            TrySubscribe();
            panel.SetActive(true);
            var mm = MessageManager.Instance;
            if (mm != null && mm.TryGetFirstUnread(out var sender)) OpenChat(sender);
            else if (openChat.HasValue) OpenChat(openChat.Value);
            else ShowContacts();
        }

        /// <summary>Спрятать окно.</summary>
        public void Hide()
        {
            // Телефон закрыли посреди печати — печать и её звук останавливаем (откроют — чат перестроится).
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            typingRoutine = null;
            UiSounds.TypingStop();
            panel.SetActive(false);
        }

        /// <summary>Кнопка телефона на экране (для прозрачности HUD).</summary>
        public RectTransform ButtonRect => phoneButton != null ? (RectTransform)phoneButton.transform : null;

        /// <summary>Показать или спрятать кнопку телефона (онбординг HUD).</summary>
        public void SetButtonVisible(bool visible)
        {
            shownByHud = visible;
            UpdateButtonVisibility();
            if (!visible) Hide();
        }

        void UpdateButtonVisibility()
        {
            // HUD живёт в _Systems всю игру, но телефон нужен только на этаже, где есть Дэнни:
            // в главном меню, прологе и до появления игрока кнопку не показываем.
            bool visible = shownByHud && PlayerController.Instance != null;
            if (phoneButton.gameObject.activeSelf != visible) phoneButton.gameObject.SetActive(visible);
        }

        void ShowContacts()
        {
            openChat = null;
            title.text = contactsTitle;
            backButton.gameObject.SetActive(false);
            contactsView.SetActive(true);
            chatView.SetActive(false);
            RefreshDots();
        }

        void OpenChat(Sender sender)
        {
            openChat = sender;
            title.text = sender == Sender.Max ? maxName : bossName;
            backButton.gameObject.SetActive(true);
            contactsView.SetActive(false);
            chatView.SetActive(true);
            var mm = MessageManager.Instance;
            // Есть непрочитанные — MarkRead вызовет OnChanged → HandleChanged → RebuildChat.
            // Иначе перерисовываем сами. Так чат строится один раз, а не дважды.
            if (mm != null && mm.HasUnread(sender))
            {
                CollectUnread(sender);
                mm.MarkRead(sender);
            }
            else RebuildChat();
        }

        void RebuildChat()
        {
            foreach (var b in bubbles)
            {
                // Destroy срабатывает только в конце кадра, а выключенный объект layout не учитывает сразу.
                // Иначе старые пузыри попадают в высоту чата и прокрутка «вниз» останавливается посреди сообщения.
                b.SetActive(false);
                Destroy(b);
            }
            bubbles.Clear();
            var mm = MessageManager.Instance;
            if (mm == null || !openChat.HasValue) return;

            if (typingRoutine != null) StopCoroutine(typingRoutine);
            typingRoutine = null;
            UiSounds.TypingStop();

            var typed = new List<GameObject>();
            foreach (var msg in mm.MessagesFrom(openChat.Value))
            {
                if (msg.Lines == null) continue;
                bool type = toType.Contains(msg);
                foreach (var line in msg.Lines)
                {
                    var bubble = Instantiate(bubbleTemplate, chatContent);
                    bubble.GetComponentInChildren<TextMeshProUGUI>().text = TextFormatter.Format(line);
                    // Новые пузыри включаются по одному, когда до них доходит печать.
                    bubble.SetActive(!type);
                    if (type) typed.Add(bubble);
                    bubbles.Add(bubble);
                }
            }
            toType.Clear();
            if (scrollRoutine != null) StopCoroutine(scrollRoutine);
            scrollRoutine = StartCoroutine(ScrollToBottom());
            if (typed.Count > 0) typingRoutine = StartCoroutine(TypeBubbles(typed));
        }

        void CollectUnread(Sender sender)
        {
            var mm = MessageManager.Instance;
            foreach (var m in mm.MessagesFrom(sender))
                if (mm.IsUnread(m)) toType.Add(m);
        }

        IEnumerator TypeBubbles(List<GameObject> typed)
        {
            // Ждём, пока прокрутка встанет вниз под уже показанную историю.
            yield return null;
            foreach (var bubble in typed)
            {
                if (bubble == null) continue;
                bubble.SetActive(true);
                var text = bubble.GetComponentInChildren<TextMeshProUGUI>();
                text.maxVisibleCharacters = 0;
                LayoutRebuilder.ForceRebuildLayoutImmediate(chatContent);
                chatScroll.verticalNormalizedPosition = 0f;
                // Сообщения печатаются своей скоростью до конца — пропустить печать нельзя.
                yield return Typewriter.Type(text, charsPerSecond);
            }
            foreach (var bubble in typed)
                if (bubble != null)
                {
                    bubble.SetActive(true);
                    Typewriter.ShowAll(bubble.GetComponentInChildren<TextMeshProUGUI>());
                }
            LayoutRebuilder.ForceRebuildLayoutImmediate(chatContent);
            chatScroll.verticalNormalizedPosition = 0f;
            typingRoutine = null;
        }

        IEnumerator ScrollToBottom()
        {
            // Ждём кадр, чтобы TMP посчитал перенос строк, затем принудительно пересчитываем layout:
            // высота пузыря зависит от его ширины, и с первого прохода она бывает неверной.
            yield return null;
            LayoutRebuilder.ForceRebuildLayoutImmediate(chatContent);
            chatScroll.verticalNormalizedPosition = 0f;
            scrollRoutine = null;
        }

        void RefreshDots()
        {
            var mm = MessageManager.Instance;
            bool max = mm != null && mm.HasUnread(Sender.Max);
            bool boss = mm != null && mm.HasUnread(Sender.Boss);
            maxDot.SetActive(max);
            bossDot.SetActive(boss);
            phoneButtonDot.SetActive(max || boss);
        }

        void HandleChanged()
        {
            RefreshDots();
            if (!panel.activeSelf || !openChat.HasValue) return;
            // Сообщение пришло в открытый чат — игрок его видит, значит оно прочитано.
            if (MessageManager.Instance.HasUnread(openChat.Value))
            {
                CollectUnread(openChat.Value);
                MessageManager.Instance.MarkRead(openChat.Value);
                return; // MarkRead снова вызовет HandleChanged
            }
            RebuildChat();
        }

        void HandleDelivered(MessageData msg)
        {
            if (shake != null) StopCoroutine(shake);
            shake = StartCoroutine(Shake());
        }

        IEnumerator Shake()
        {
            var rt = (RectTransform)phoneButton.transform;
            // Unscaled: вибрация не должна замирать, если время замедлено эффектами.
            for (float t = 0; t < shakeSeconds; t += Time.unscaledDeltaTime)
            {
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 60f) * shakeAngle);
                yield return null;
            }
            rt.localRotation = Quaternion.identity;
            shake = null;
        }
    }
}
