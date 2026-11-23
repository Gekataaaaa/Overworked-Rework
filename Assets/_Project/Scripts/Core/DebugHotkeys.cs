using System.Collections;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Overworked
{
    /// <summary>
    /// Отладочные клавиши для всей команды (только редактор и development-билд, добавляет Bootstrap).
    /// F1 — MainMenu, F2/F3/F4 — Floor_03/02/01, F5 — WhiteRoom, F6 — fade туда-обратно,
    /// F7 — панель состояния (этаж, имя, рассудок, флаги), F8 — по кругу: коридор → комнаты этажа → коридор,
    /// F9 — быстрый бег: бесконечная стамина и бег вдвое быстрее (вкл/выкл; там, где бег включён — allowRun),
    /// F10 — рассудок −50, F11 — рассудок 100, F12 — рассудок 0 (перезагрузка с чекпоинта).
    /// </summary>
    public class DebugHotkeys : MonoBehaviour
    {
        bool showState;
        GUIStyle style;
        int debugRoom = -1;
        bool moving;

        // Живём в _Systems между сценами: на новом этаже F8 начинает снова с коридора.
        void OnEnable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
        {
            debugRoom = -1;
            moving = false;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f1Key.wasPressedThisFrame) GameManager.Instance.LoadMainMenu();
            if (kb.f2Key.wasPressedThisFrame) GameManager.Instance.LoadFloor(3);
            if (kb.f3Key.wasPressedThisFrame) GameManager.Instance.LoadFloor(2);
            if (kb.f4Key.wasPressedThisFrame) GameManager.Instance.LoadFloor(1);
            if (kb.f5Key.wasPressedThisFrame) GameManager.Instance.LoadFloor(0);
            if (kb.f6Key.wasPressedThisFrame && FadeController.Instance != null)
                FadeController.Instance.FadeOut(0.6f, () => FadeController.Instance.FadeIn(0.8f, null));
            if (kb.f7Key.wasPressedThisFrame) showState = !showState;
            if (kb.f8Key.wasPressedThisFrame) CycleRoom();
            if (kb.f9Key.wasPressedThisFrame)
            {
                PlayerController.DebugFastRun = !PlayerController.DebugFastRun;
                string state = PlayerController.DebugFastRun ? "ON" : "OFF";
                Debug.Log($"[Debug] F9 fast run {state}");
                if (UIManager.Instance != null) UIManager.Instance.ShowToast($"Debug: fast run {state}");
            }
            // F10 — рассудок −50, F11 — рассудок 100 (плавно, со шкалой).
            var sanity = SanityManager.Instance;
            if (sanity != null)
            {
                if (kb.f10Key.wasPressedThisFrame) sanity.SetSanity(sanity.Target - 50f);
                if (kb.f11Key.wasPressedThisFrame) sanity.SetSanity(100f);
                if (kb.f12Key.wasPressedThisFrame) sanity.SetSanity(0f);
            }
        }

        /// <summary>
        /// Следующая комната этажа (по имени), после последней — обратно в коридор.
        /// Как дверь, но без MarkEntered: сценарии комнат (галлюцинации, скример) не запускаются — только посмотреть.
        /// </summary>
        void CycleRoom()
        {
            if (moving || PlayerController.Instance == null) return;
            // Поиск по сцене — только здесь, по нажатию отладочной клавиши, в игровой код это не идёт.
            var rooms = FindObjectsByType<Room>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(r => r.EntryPoint != null && r.RoomCamera != null).OrderBy(r => r.name).ToArray();
            if (rooms.Length == 0)
            {
                Debug.Log("[Debug] F8: на этаже нет комнат");
                return;
            }
            debugRoom = debugRoom + 1 < rooms.Length ? debugRoom + 1 : -1;
            StartCoroutine(MoveTo(rooms, debugRoom));
        }

        IEnumerator MoveTo(Room[] rooms, int index)
        {
            moving = true;
            var player = PlayerController.Instance;
            player.SetInputLocked(true);
            yield return Fade(true);

            // Выключаем все камеры комнат и коридора, включаем нужную — так F8 работает и после входа через дверь.
            var corridorCam = rooms[0].CorridorCamera;
            foreach (var r in rooms) r.RoomCamera.SetActive(false);
            if (corridorCam != null) corridorCam.SetActive(false);

            GameObject cam;
            if (index >= 0)
            {
                player.Teleport(rooms[index].EntryPoint);
                cam = rooms[index].RoomCamera;
                Debug.Log($"[Debug] F8 → {rooms[index].name}");
            }
            else
            {
                // Обратно — к двери последней комнаты, через которую «вышли».
                var back = rooms[rooms.Length - 1].ReturnPoint;
                if (back != null) player.Teleport(back);
                cam = corridorCam;
                Debug.Log("[Debug] F8 → коридор");
            }
            yield return SwitchTo(cam);

            yield return Fade(false);
            player.SetInputLocked(false);
            moving = false;
        }

        // Резкое переключение, как у DoorInteractable: иначе Cinemachine плывёт от коридора к комнате.
        static IEnumerator SwitchTo(GameObject cam)
        {
            var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            var saved = brain != null ? brain.DefaultBlend : default;
            if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            if (cam != null)
            {
                cam.SetActive(true);
                var vcam = cam.GetComponent<CinemachineVirtualCameraBase>();
                if (vcam != null) vcam.PreviousStateIsValid = false;
            }
            yield return null;
            yield return null;
            if (brain != null) brain.DefaultBlend = saved;
        }

        static IEnumerator Fade(bool toBlack)
        {
            var fade = FadeController.Instance;
            if (fade == null) yield break;
            bool done = false;
            if (toBlack) fade.FadeOut(0.3f, () => done = true);
            else fade.FadeIn(0.3f, () => done = true);
            while (!done) yield return null;
        }

        void OnGUI()
        {
            if (!showState || GameManager.Instance == null) return;
            style ??= new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.UpperLeft };
            var s = GameManager.Instance.State;
            string sanity = SanityManager.Instance != null ? $"{SanityManager.Instance.Current:0} → {SanityManager.Instance.Target:0}" : "—";
            string player = PlayerController.Instance != null ? PlayerController.Instance.State.ToString() : "нет";
            GUI.Box(new Rect(10, 10, 420, 150),
                $"Floor: {s.CurrentFloor}\nName: {s.PlayerName}\nSanity: {sanity}\nPlayer: {player}\nFlags: {string.Join(", ", s.Flags)}",
                style);
        }
    }
}
