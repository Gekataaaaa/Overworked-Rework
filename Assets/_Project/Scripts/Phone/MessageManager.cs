using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overworked
{
    /// <summary>
    /// Доставка сообщений в телефон: история (живёт между этажами), непрочитанные,
    /// вибрация + тост и колбэк «прочитал и закрыл телефон». Живёт в префабе _Systems.
    /// Сообщение считается прочитанным, когда открыт чат его контакта (это делает PhoneUI).
    /// </summary>
    public class MessageManager : SingletonBehaviour<MessageManager>
    {
        [Tooltip("Звук вибрации при новом сообщении. Пусто — Audio/UI/Resources/UI_PhoneVibrate")]
        [SerializeField] AudioClip vibrateClip;

        [Range(0f, 1f)]
        [Tooltip("Громкость вибрации 0..1")]
        [SerializeField] float vibrateVolume = 1f;

        [Tooltip("Тост о новом сообщении от Макса")]
        [SerializeField] string toastMax = "New message from Max";

        [Tooltip("Тост о новом сообщении от Босса")]
        [SerializeField] string toastBoss = "New message from the Boss";

        /// <summary>Пришло новое сообщение (вибрация кнопки телефона).</summary>
        public event Action<MessageData> OnDelivered;

        /// <summary>Изменилась история или непрочитанные — перерисовать телефон.</summary>
        public event Action OnChanged;

        struct Waiting
        {
            public MessageData Msg;
            public Action OnReadAndClosed;
        }

        readonly List<MessageData> history = new List<MessageData>();
        readonly HashSet<MessageData> unread = new HashSet<MessageData>();
        // Сообщения, чей колбэк ещё не вызван: ждём, пока их прочитают и закроют телефон.
        readonly List<Waiting> waiting = new List<Waiting>();
        bool subscribed;

        /// <summary>Вся история сообщений в порядке прихода.</summary>
        public IReadOnlyList<MessageData> History => history;

        void Start()
        {
            if (vibrateClip == null) vibrateClip = Resources.Load<AudioClip>("UI_PhoneVibrate");
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            TrySubscribe();
        }

        protected override void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (subscribed && PhoneManager.Instance != null) PhoneManager.Instance.OnClosed -= HandlePhoneClosed;
            base.OnDestroy();
        }

        void TrySubscribe()
        {
            if (subscribed || PhoneManager.Instance == null) return;
            PhoneManager.Instance.OnClosed += HandlePhoneClosed;
            subscribed = true;
        }

        /// <summary>
        /// Доставляет сообщение: история, вибрация, тост. onReadAndClosed — когда игрок открыл
        /// чат этого контакта и закрыл телефон. Задание из LinkedObjective выдаётся в тот же момент
        /// (в нарративе задание появляется после того, как игрок закрыл телефон).
        /// Несколько сообщений подряд (Макс, следом Босс) — просто несколько вызовов Deliver.
        /// </summary>
        public void Deliver(MessageData msg, Action onReadAndClosed = null)
        {
            if (msg == null)
            {
                Debug.LogWarning("[Phone] Deliver(null)");
                return;
            }
            TrySubscribe();
            history.Add(msg);
            unread.Add(msg);
            waiting.Add(new Waiting { Msg = msg, OnReadAndClosed = onReadAndClosed });
            Debug.Log($"[Phone] Deliver {msg.name}");

            if (UIManager.Instance != null) UIManager.Instance.ShowToast(msg.Sender == Sender.Max ? toastMax : toastBoss);
            if (vibrateClip != null && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(vibrateClip, vibrateVolume);

            OnDelivered?.Invoke(msg);
            OnChanged?.Invoke();
        }

        /// <summary>Сообщения одного контакта в порядке прихода.</summary>
        public List<MessageData> MessagesFrom(Sender sender)
        {
            var list = new List<MessageData>();
            foreach (var m in history)
                if (m.Sender == sender) list.Add(m);
            return list;
        }

        /// <summary>Есть ли непрочитанные от контакта.</summary>
        public bool HasUnread(Sender sender)
        {
            foreach (var m in unread)
                if (m.Sender == sender) return true;
            return false;
        }

        /// <summary>Не прочитано ли это сообщение.</summary>
        public bool IsUnread(MessageData msg) => msg != null && unread.Contains(msg);

        /// <summary>Есть ли непрочитанные вообще (точка на кнопке телефона).</summary>
        public bool HasAnyUnread => unread.Count > 0;

        /// <summary>Контакт самого раннего непрочитанного сообщения — его чат открывается первым.</summary>
        public bool TryGetFirstUnread(out Sender sender)
        {
            foreach (var m in history)
                if (unread.Contains(m))
                {
                    sender = m.Sender;
                    return true;
                }
            sender = default;
            return false;
        }

        /// <summary>Отмечает прочитанными все сообщения контакта (игрок открыл его чат).</summary>
        public void MarkRead(Sender sender)
        {
            if (unread.RemoveWhere(m => m.Sender == sender) > 0) OnChanged?.Invoke();
        }

        /// <summary>
        /// Восстановление из сохранения (SaveSystem): история в порядке прихода и непрочитанные. Без тостов и вибрации;
        /// колбэков «прочитал» нет — этаж начнётся заново и сам доставит то, что ему нужно.
        /// </summary>
        public void Restore(IEnumerable<MessageData> messages, IEnumerable<MessageData> unreadMessages)
        {
            history.Clear();
            unread.Clear();
            waiting.Clear();
            foreach (var m in messages) if (m != null) history.Add(m);
            foreach (var m in unreadMessages) if (m != null && history.Contains(m)) unread.Add(m);
            OnChanged?.Invoke();
        }

        /// <summary>Стирает историю — новая игра.</summary>
        public void Clear()
        {
            history.Clear();
            unread.Clear();
            waiting.Clear();
            OnChanged?.Invoke();
        }

        void HandlePhoneClosed()
        {
            // Сначала собираем готовые, потом вызываем: колбэк может сразу доставить следующее сообщение.
            var ready = new List<Waiting>();
            for (int i = waiting.Count - 1; i >= 0; i--)
            {
                if (unread.Contains(waiting[i].Msg)) continue;
                ready.Insert(0, waiting[i]);
                waiting.RemoveAt(i);
            }
            foreach (var w in ready)
            {
                if (w.Msg.LinkedObjective != null && ObjectiveManager.Instance != null)
                    ObjectiveManager.Instance.StartObjective(w.Msg.LinkedObjective);
                w.OnReadAndClosed?.Invoke();
            }
        }

        void OnActiveSceneChanged(Scene from, Scene to)
        {
            // История живёт между этажами, но в главном меню начинается новая игра.
            // Кроме паузы → Main Menu: этаж спрятан под меню (сцен загружено больше одной), Continue вернёт
            // игрока туда же — история должна остаться. Новую игру всё равно очищает GameManager.ResetState.
            if (to.name == "MainMenu" && SceneManager.sceneCount == 1) Clear();
        }
    }
}
