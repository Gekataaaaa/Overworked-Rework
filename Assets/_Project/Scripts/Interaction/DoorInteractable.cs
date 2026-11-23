using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

namespace Overworked
{
    /// <summary>
    /// Дверь: вход в комнату или выход из неё (затемнение → игрок в target → смена камеры → проявление)
    /// или запертая дверь с текстом («This doesn't look like the kitchen.»).
    /// Запертую ключом дверь (Room_102) открывает флаг unlockFlag ("Key102").
    /// Комнаты лежат в той же сцене ниже коридора; у каждой своя CinemachineCamera, выключенная на старте.
    /// Обычно двери достаточно поля room и направления: точки и камеры берутся из Room.
    /// Поля target / cameraOn / cameraOff — для особых переходов без комнаты.
    /// </summary>
    public class DoorInteractable : InteractableBase
    {
        /// <summary>Куда ведёт дверь комнаты.</summary>
        public enum Direction { IntoRoom, OutToCorridor }

        [Header("Комната")]
        [Tooltip("Комната, в которую ведёт дверь коридора или из которой ведёт ExitDoor. Пусто — переход по полям ниже")]
        [SerializeField] Room room;

        [Tooltip("IntoRoom — дверь в коридоре, OutToCorridor — ExitDoor внутри комнаты")]
        [SerializeField] Direction direction = Direction.IntoRoom;

        [Header("Переход без комнаты")]
        [Tooltip("Куда поставить Дэнни: EntryPoint комнаты или точка у двери в коридоре. Пусто — дверь только заперта")]
        [SerializeField] Transform target;

        [Tooltip("Камера, которую включить (CM Room_X при входе, CM Corridor при выходе)")]
        [SerializeField] GameObject cameraOn;

        [Tooltip("Камера, которую выключить")]
        [SerializeField] GameObject cameraOff;

        [Tooltip("Длительность затемнения и проявления, секунд")]
        [SerializeField] float fadeTime = 0.35f;

        [Header("Замок")]
        [Tooltip("Дверь заперта (декоративные двери коридора — всегда)")]
        [SerializeField] bool doorLocked;

        [Tooltip("Флаг, который отпирает дверь (\"Key102\"). Пусто — ключа нет")]
        [SerializeField] string unlockFlag;

        [Tooltip("Ключа мало: дверь отпирается, только когда выполнено и это задание (Floor_01, 102: поговорить с сидящим в 101). Пусто — хватает ключа")]
        [SerializeField] ObjectiveData unlockAlsoRequires;

        [Tooltip("Реплика, если ключ есть, а задание unlockAlsoRequires не выполнено")]
        [SerializeField] DialogueData notReadyDialogue;

        [Tooltip("Подсказка у запертой двери")]
        [SerializeField] string lockedPrompt = "Open";

        [Tooltip("Тост при попытке открыть запертую дверь (английский)")]
        [SerializeField] string lockedMessage = "It's locked.";

        [Tooltip("Описание/реплики при попытке открыть запертую (табличка Room_102). Если задано — вместо тоста")]
        [SerializeField] DialogueData lockedDialogue;

        [Tooltip("Попытка открыть запертую дверь: сценарий выдаёт задание (Floor_01: «Find the key in room 101»)")]
        public UnityEvent onLockedTried;

        /// <summary>Сейчас заперта.</summary>
        public bool IsLocked => Target == null || (doorLocked && !HasUnlockFlag);

        bool IntoRoom => room != null && direction == Direction.IntoRoom;
        Transform Target => room == null ? target : IntoRoom ? room.EntryPoint : room.ReturnPoint;
        GameObject CameraOn => room == null ? cameraOn : IntoRoom ? room.RoomCamera : room.CorridorCamera;
        GameObject CameraOff => room == null ? cameraOff : IntoRoom ? room.CorridorCamera : room.RoomCamera;

        bool HasUnlockFlag => !string.IsNullOrEmpty(unlockFlag) && HasFlag(unlockFlag) && RequirementDone;

        bool RequirementDone => unlockAlsoRequires == null
                                || (ObjectiveManager.Instance != null && ObjectiveManager.Instance.IsCompleted(unlockAlsoRequires));

        /// <inheritdoc/>
        public override string Prompt => IsLocked ? lockedPrompt : prompt;

        /// <summary>Отпереть (false) или запереть (true) дверь из сценария.</summary>
        public void SetDoorLocked(bool value) => doorLocked = value;

        protected override void OnInteract()
        {
            if (IsLocked)
            {
                // Ключ уже есть, но не сделано обязательное задание — говорим об этом, а не «заперто».
                bool keyButNotReady = !string.IsNullOrEmpty(unlockFlag) && HasFlag(unlockFlag) && !RequirementDone;
                if (keyButNotReady && notReadyDialogue != null) PlayDialogue(notReadyDialogue);
                else if (lockedDialogue != null) PlayDialogue(lockedDialogue);
                else Toast(lockedMessage);
                onLockedTried?.Invoke();
                return;
            }
            StartCoroutine(Transfer());
        }

        IEnumerator Transfer()
        {
            busy = true;
            LockPlayer(true);
            yield return Fade(true);

            PlayerController.Instance.Teleport(Target);
            yield return SwitchCameras();
            // Под затемнением: сценарий комнаты (галлюцинация, скример) стартует, пока экран чёрный.
            if (room != null)
            {
                if (IntoRoom) room.MarkEntered();
                else room.MarkExited();
            }

            yield return Fade(false);
            LockPlayer(false);
            Finish();
        }

        IEnumerator Fade(bool toBlack)
        {
            var fade = FadeController.Instance;
            if (fade == null) yield break;
            bool done = false;
            if (toBlack) fade.FadeOut(fadeTime, () => done = true);
            else fade.FadeIn(fadeTime, () => done = true);
            while (!done) yield return null;
        }

        // Под затемнением камера должна переключиться резко: без этого Cinemachine 2 секунды
        // плывёт от коридора к комнате, и проявление показывает этот полёт.
        IEnumerator SwitchCameras()
        {
            var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            var savedBlend = brain != null ? brain.DefaultBlend : default;
            if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            var on = CameraOn;
            var off = CameraOff;
            if (off != null) off.SetActive(false);
            if (on != null)
            {
                on.SetActive(true);
                var vcam = on.GetComponent<CinemachineVirtualCameraBase>();
                // Камера не помнит старую позицию — встаёт сразу на игрока, без доводки.
                if (vcam != null) vcam.PreviousStateIsValid = false;
            }
            // Brain выбирает новую камеру в LateUpdate: ждём кадр, потом возвращаем обычный блендинг.
            yield return null;
            yield return null;
            if (brain != null) brain.DefaultBlend = savedBlend;
        }
    }
}
