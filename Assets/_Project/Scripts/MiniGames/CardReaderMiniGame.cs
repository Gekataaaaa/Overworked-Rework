using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overworked
{
    /// <summary>
    /// Мини-игра 2 «Считыватель пропуска» (турникет Floor_03): вертикальная шкала, пока E зажата —
    /// ползунок ходит вверх-вниз; отпустил в зелёной зоне — «ACCESS GRANTED», мимо — повтор.
    /// Esc — отойти от турникета (выход без выполнения). Оверлей строится из кода.
    /// Кладётся на объект турникета, ссылку отдают в TurnstileInteractable.cardReader.
    /// </summary>
    public class CardReaderMiniGame : MonoBehaviour, IMiniGame
    {
        [Tooltip("Скорость ползунка: долей шкалы в секунду")]
        [SerializeField] float sliderSpeed = 0.9f;

        [Tooltip("Высота зелёной зоны: доля шкалы (0.2 = пятая часть)")]
        [Range(0.05f, 0.6f)]
        [SerializeField] float zoneSize = 0.22f;

        [Tooltip("Пауза после промаха перед новой попыткой, секунд")]
        [SerializeField] float missPause = 0.7f;

        [Tooltip("Сколько видно «ACCESS GRANTED» перед закрытием, секунд")]
        [SerializeField] float winPause = 0.9f;

        [Header("Тексты (английский)")]
        [SerializeField] string title = "CARD READER";
        [SerializeField] string hint = "Hold [E], release in the green zone\n[Esc] step back";
        [SerializeField] string missText = "READ ERROR";
        [SerializeField] string winText = "ACCESS GRANTED";

        const float TrackHeight = 420f;

        Action<bool> onFinish;
        Canvas canvas;
        RectTransform zone, slider;
        TMP_Text status;
        float pos;          // 0..1 — где ползунок
        float dir = 1f;
        float zoneMin;
        bool holding;
        bool armed;         // E отпущена после открытия: то же нажатие, что открыло игру, не засчитываем
        bool waiting;       // пауза после попытки

        /// <inheritdoc/>
        public void Begin(Action<bool> finish)
        {
            onFinish = finish;
            if (canvas == null) Build();
            canvas.gameObject.SetActive(true);
            armed = false;
            NewAttempt();
        }

        void Update()
        {
            if (canvas == null || !canvas.gameObject.activeSelf || waiting) return;

            if (GameInput.Pause != null && GameInput.Pause.WasPressedThisFrame())
            {
                StartCoroutine(QuitNextFrame());
                return;
            }

            bool pressed = GameInput.Interact != null && GameInput.Interact.IsPressed();
            if (!armed)
            {
                armed = !pressed;
                return;
            }

            if (pressed)
            {
                holding = true;
                pos += dir * sliderSpeed * Time.deltaTime;
                // Ползунок отражается от краёв: держать можно сколько угодно, промах — только по своей вине.
                if (pos > 1f) { pos = 2f - pos; dir = -1f; }
                if (pos < 0f) { pos = -pos; dir = 1f; }
                PlaceSlider();
            }
            else if (holding)
            {
                holding = false;
                bool hit = pos >= zoneMin && pos <= zoneMin + zoneSize;
                StartCoroutine(hit ? Win() : Miss());
            }
        }

        // Закрываемся кадром позже: в этом кадре Esc ещё «нажата», и меню паузы,
        // увидев разблокированного Дэнни, открылось бы сразу после выхода.
        IEnumerator QuitNextFrame()
        {
            waiting = true;
            yield return null;
            End(false);
        }

        IEnumerator Win()
        {
            waiting = true;
            status.text = winText;
            status.color = new Color(0.4f, 0.95f, 0.5f);
            yield return new WaitForSeconds(winPause);
            End(true);
        }

        IEnumerator Miss()
        {
            waiting = true;
            status.text = missText;
            status.color = new Color(1f, 0.4f, 0.35f);
            yield return new WaitForSeconds(missPause);
            NewAttempt();
        }

        void NewAttempt()
        {
            waiting = false;
            holding = false;
            pos = 0f;
            dir = 1f;
            // Зона всегда в верхних двух третях: ползунок стартует снизу, сразу попасть нельзя.
            zoneMin = UnityEngine.Random.Range(0.35f, 1f - zoneSize);
            zone.anchoredPosition = new Vector2(0f, zoneMin * TrackHeight);
            status.text = "";
            PlaceSlider();
        }

        void PlaceSlider() => slider.anchoredPosition = new Vector2(0f, pos * TrackHeight);

        void End(bool win)
        {
            StopAllCoroutines();
            waiting = false;
            canvas.gameObject.SetActive(false);
            var cb = onFinish;
            onFinish = null;
            cb?.Invoke(win);
        }

        void Build()
        {
            canvas = OverlayUi.CreateCanvas(transform, "CardReaderOverlay", 200);
            OverlayUi.Fill(canvas.transform, "Dim", new Color(0f, 0f, 0f, 0.6f));
            var panel = OverlayUi.Box(canvas.transform, "Panel", new Color(0.12f, 0.14f, 0.13f, 0.95f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 700f)).transform;
            OverlayUi.Label(panel, "Title", title, 40, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(480f, 50f));
            OverlayUi.Label(panel, "Hint", hint, 22, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(480f, 70f))
                .color = new Color(0.7f, 0.72f, 0.68f);
            status = OverlayUi.Label(panel, "Status", "", 30, new Vector2(0.5f, 1f), new Vector2(0f, -75f), new Vector2(480f, 40f));

            // Шкала: якорь снизу, позиции ползунка и зоны считаются от её низа.
            var track = OverlayUi.Box(panel, "Track", new Color(0.05f, 0.05f, 0.05f), new Vector2(0.5f, 0f),
                new Vector2(0f, 110f), new Vector2(70f, TrackHeight)).rectTransform;
            track.pivot = new Vector2(0.5f, 0f);
            track.anchoredPosition = new Vector2(0f, 110f);

            zone = OverlayUi.Box(track, "GreenZone", new Color(0.2f, 0.7f, 0.3f, 0.85f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(70f, zoneSize * TrackHeight)).rectTransform;
            zone.pivot = new Vector2(0.5f, 0f);

            slider = OverlayUi.Box(track, "Slider", new Color(0.95f, 0.92f, 0.8f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(100f, 8f)).rectTransform;
            slider.pivot = new Vector2(0.5f, 0.5f);

            canvas.gameObject.SetActive(false);
        }
    }
}
