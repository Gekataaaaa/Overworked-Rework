using System;
using UnityEngine;
using UnityEngine.Events;

namespace Overworked
{
    /// <summary>
    /// Комната этажа (Environment/Rooms/Room_X): где Дэнни появляется, какой камерой её снимать,
    /// куда возвращаться в коридор. Двери берут всё это отсюда (DoorInteractable.room).
    /// Знает, в какой комнате сейчас Дэнни (Room.Current, null — коридор), и даёт события входа/выхода:
    /// Room_101 — Галлюцинация 4 при входе, Room_102 — финальный скример при выходе.
    /// Ставится инструментом Overworked → P2 → Wire Rooms.
    /// </summary>
    public class Room : MonoBehaviour
    {
        [Tooltip("Где появляется Дэнни при входе. Пусто — дочерний объект EntryPoint")]
        [SerializeField] Transform entryPoint;

        [Tooltip("Точка в коридоре перед дверью комнаты — сюда Дэнни выходит")]
        [SerializeField] Transform returnPoint;

        [Tooltip("CinemachineCamera комнаты (Cameras/CM Room_X), выключена на старте")]
        [SerializeField] GameObject roomCamera;

        [Tooltip("CinemachineCamera коридора (Cameras/CM Corridor)")]
        [SerializeField] GameObject corridorCamera;

        [Tooltip("Дэнни вошёл (экран ещё чёрный, Дэнни уже в комнате)")]
        public UnityEvent onEnter;

        [Tooltip("Дэнни вышел в коридор (экран ещё чёрный)")]
        public UnityEvent onExit;

        /// <summary>Комната, где сейчас Дэнни; null — коридор.</summary>
        public static Room Current { get; private set; }

        /// <summary>Дэнни сменил комнату (null — вышел в коридор).</summary>
        public static event Action<Room> OnRoomChanged;

        /// <summary>Точка появления при входе.</summary>
        public Transform EntryPoint => entryPoint;

        /// <summary>Точка в коридоре при выходе.</summary>
        public Transform ReturnPoint => returnPoint;

        /// <summary>Камера комнаты.</summary>
        public GameObject RoomCamera => roomCamera;

        /// <summary>Камера коридора.</summary>
        public GameObject CorridorCamera => corridorCamera;

        void Awake()
        {
            if (entryPoint == null) entryPoint = transform.Find("EntryPoint");
        }

        void OnDestroy()
        {
            // Этаж выгрузился вместе с комнатой — новая сцена начинается в коридоре.
            if (Current == this) Current = null;
        }

        /// <summary>
        /// Загрузка сохранения: Дэнни уже в этой комнате — текущая комната и камеры как внутри, без onEnter/OnRoomChanged
        /// (сценарии входа уже отыграны).
        /// </summary>
        public void RestoreInside()
        {
            Current = this;
            if (corridorCamera != null) corridorCamera.SetActive(false);
            if (roomCamera != null) roomCamera.SetActive(true);
        }

        /// <summary>Отмечает, что Дэнни вошёл (вызывает дверь под затемнением).</summary>
        public void MarkEntered()
        {
            Current = this;
            Debug.Log($"[Rooms] → {name}");
            onEnter?.Invoke();
            OnRoomChanged?.Invoke(this);
        }

        /// <summary>Отмечает, что Дэнни вышел в коридор.</summary>
        public void MarkExited()
        {
            if (Current == this) Current = null;
            Debug.Log($"[Rooms] {name} → corridor");
            onExit?.Invoke();
            OnRoomChanged?.Invoke(null);
        }

        // Сцена запущена заново (без перезагрузки домена в редакторе) — статическое состояние сбрасываем.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            OnRoomChanged = null;
        }
    }
}
