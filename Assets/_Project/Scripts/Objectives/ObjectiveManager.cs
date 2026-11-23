using System;
using System.Collections.Generic;
using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Задания: выдаёт, закрывает, сообщает остальным. Активных может быть несколько сразу (Floor_01: найти ключ
    /// и поговорить с сидящим в 101) — карточка показывает их списком. Живёт в _Systems и переживает смену этажей.
    /// </summary>
    public class ObjectiveManager : SingletonBehaviour<ObjectiveManager>
    {
        /// <summary>Выдано новое задание (UI показывает карточку).</summary>
        public event Action<ObjectiveData> OnStarted;

        /// <summary>Задание выполнено (UI ставит галочку, сценарий идёт дальше).</summary>
        public event Action<ObjectiveData> OnCompleted;

        /// <summary>Последнее выданное из невыполненных или null (для кода, которому нужно одно задание).</summary>
        public ObjectiveData Current { get; private set; }

        /// <summary>Все выданные и ещё не выполненные задания в порядке выдачи.</summary>
        public IReadOnlyList<ObjectiveData> Active => active;

        readonly List<ObjectiveData> active = new List<ObjectiveData>();

        /// <summary>Выдано и ещё не выполнено (по Id).</summary>
        public bool IsActive(ObjectiveData o) => o != null && active.Exists(a => Same(a, o));

        // Выполненные — по Id, а не по ассету: два ассета с одним Id (сделаны в разных ветках) — одно задание.
        readonly HashSet<string> completed = new HashSet<string>();

        /// <summary>Одно ли это задание (по Id).</summary>
        public static bool Same(ObjectiveData a, ObjectiveData b) =>
            a != null && b != null && (a == b || a.Id == b.Id);

        /// <summary>
        /// Выдаёт новое задание. Повторная выдача текущего или уже выполненного ничего не делает —
        /// триггер, сработавший дважды, не сбросит прогресс.
        /// </summary>
        public void StartObjective(ObjectiveData o)
        {
            if (o == null || IsActive(o) || completed.Contains(o.Id)) return;
            active.Add(o);
            Current = o;
            Debug.Log($"[Objectives] Start {o.Id}");
            OnStarted?.Invoke(o);
        }

        /// <summary>
        /// Закрывает задание. Можно закрыть и не текущее (игрок сделал шаг раньше, чем получил задание) —
        /// тогда оно просто запоминается как выполненное. Повторный вызов ничего не делает.
        /// </summary>
        public void Complete(ObjectiveData o)
        {
            if (o == null || !completed.Add(o.Id)) return;
            Debug.Log($"[Objectives] Complete {o.Id}");
            active.RemoveAll(a => Same(a, o));
            if (Same(Current, o)) Current = active.Count > 0 ? active[active.Count - 1] : null;
            OnCompleted?.Invoke(o);
        }

        /// <summary>Выполнено ли задание.</summary>
        public bool IsCompleted(ObjectiveData o) => o != null && completed.Contains(o.Id);

        /// <summary>Выполнено ли задание с таким Id.</summary>
        public bool IsCompleted(string id) => !string.IsNullOrEmpty(id) && completed.Contains(id);

        /// <summary>Выполненные задания (Id) — для сохранения.</summary>
        public IEnumerable<string> CompletedIds => completed;

        /// <summary>
        /// Восстановление из сохранения (SaveSystem): выполненные по Id и активные задания. Событий не шлёт —
        /// карточка подхватит активные сама (ObjectiveUI.Start / OnStarted не нужен: этаж загрузится заново).
        /// </summary>
        public void Restore(IEnumerable<string> completedIds, IEnumerable<ObjectiveData> activeObjectives)
        {
            ResetAll();
            foreach (var id in completedIds) if (!string.IsNullOrEmpty(id)) completed.Add(id);
            foreach (var o in activeObjectives)
            {
                if (o == null || completed.Contains(o.Id) || IsActive(o)) continue;
                active.Add(o);
                Current = o;
                OnStarted?.Invoke(o);   // карточка уже живёт (DontDestroyOnLoad) — пусть покажет строку
            }
        }

        /// <summary>Сброс для нового прохождения (из главного меню).</summary>
        public void ResetAll()
        {
            Current = null;
            active.Clear();
            completed.Clear();
        }
    }
}
