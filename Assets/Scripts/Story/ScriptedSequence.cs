using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Overworked
{
    /// <summary>
    /// Сценарная сцена — цепочка шагов, которые выполняются по очереди (паспорт, раздел 17):
    /// реплика, сообщение с ожиданием прочтения, блок управления, анимация, HUD, галлюцинация,
    /// мини-игра, задание, вкл/выкл объекта, fade, пауза. Шаги настраиваются в инспекторе
    /// (кнопка «Add step»). Запуск — <see cref="Play()"/>, <see cref="SequenceTrigger"/> или playOnStart.
    /// </summary>
    public class ScriptedSequence : MonoBehaviour
    {
        [Tooltip("Запустить сам при старте сцены (начало этажа)")]
        [SerializeField] bool playOnStart;

        [Tooltip("Проиграть только один раз — повторные Play игнорируются")]
        [SerializeField] bool once = true;

        [Tooltip("Заблокировать Дэнни на всё время сценария и вернуть управление в конце")]
        [SerializeField] bool lockPlayer;

        [SerializeReference] List<SequenceStep> steps = new List<SequenceStep>();

        [Tooltip("Вызывается, когда все шаги выполнены")]
        [SerializeField] UnityEvent onFinished;

        /// <summary>Сценарий сейчас идёт.</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>Сценарий уже запускался.</summary>
        public bool HasPlayed { get; private set; }

        /// <summary>Все шаги выполнены.</summary>
        public event Action Finished;

        /// <summary>Шаги сценария (для редактора и отладки).</summary>
        public IReadOnlyList<SequenceStep> Steps => steps;

        Coroutine routine;

        void Start()
        {
            if (playOnStart) Play();
        }

        /// <summary>Запускает цепочку с первого шага.</summary>
        public void Play() => Play(null);

        /// <summary>Запускает цепочку с первого шага; done — когда все шаги выполнены.</summary>
        public void Play(Action done)
        {
            if (IsPlaying)
            {
                Debug.LogWarning($"[Story] {name} уже идёт");
                return;
            }
            if (once && HasPlayed) return;
            routine = StartCoroutine(Run(done));
        }

        /// <summary>Обрывает сценарий на текущем шаге (done и onFinished не вызываются).</summary>
        public void Stop()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (IsPlaying && lockPlayer && PlayerController.Instance != null) PlayerController.Instance.SetInputLocked(false);
            IsPlaying = false;
        }

        IEnumerator Run(Action done)
        {
            IsPlaying = true;
            HasPlayed = true;
            Debug.Log($"[Story] Play {name}");
            if (lockPlayer && PlayerController.Instance != null) PlayerController.Instance.SetInputLocked(true);

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null) continue;
                Debug.Log($"[Story] {name} · {i + 1}. {step.Describe()}");
                yield return step.Run(this);
            }

            if (lockPlayer && PlayerController.Instance != null) PlayerController.Instance.SetInputLocked(false);
            IsPlaying = false;
            routine = null;
            Debug.Log($"[Story] {name} done");
            onFinished?.Invoke();
            Finished?.Invoke();
            done?.Invoke();
        }

        /// <summary>
        /// Ждёт колбэк в стиле «Action done»: start вызывает API и передаёт ему готовый колбэк.
        /// Пример: <c>yield return ScriptedSequence.WaitCallback(cb => DialogueManager.Instance.Play(d, cb));</c>
        /// </summary>
        public static IEnumerator WaitCallback(Action<Action> start)
        {
            bool finished = false;
            start(() => finished = true);
            while (!finished) yield return null;
        }
    }
}
