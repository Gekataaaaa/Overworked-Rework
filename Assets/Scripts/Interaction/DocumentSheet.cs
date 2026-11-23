using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Overworked
{
    /// <summary>
    /// Лист бумаги формата A4 на весь экран (документ из ящика: графики смен в Room_201).
    /// Белый лист с пропорциями A4 (1 : 1.414), тёмный текст. Пока лист открыт, Дэнни заблокирован;
    /// закрывается по E, Space, Enter, Esc или клику. Строится из кода, живёт рядом с InteractionSystem.
    /// </summary>
    public class DocumentSheet : MonoBehaviour
    {
        const float SheetHeight = 960f;
        const float SheetWidth = SheetHeight / 1.414f;

        static DocumentSheet instance;

        Canvas canvas;
        TMP_Text body;
        TMP_Text hint;
        Action onClosed;
        bool open;
        float openedAt;

        /// <summary>Лист сейчас открыт (субтитры ждут, пока его закроют).</summary>
        public static bool IsOpen => instance != null && instance.open;

        /// <summary>Показать лист с текстом; closed — после того как игрок его закрыл.</summary>
        public static void Show(string text, Action closed)
        {
            if (instance == null)
            {
                var go = new GameObject("DocumentSheet");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<DocumentSheet>();
            }
            instance.Open(text, closed);
        }

        void Open(string text, Action closed)
        {
            if (canvas == null) Build();
            body.text = text;
            onClosed = closed;
            open = true;
            openedAt = Time.unscaledTime;
            canvas.gameObject.SetActive(true);
            if (PlayerController.Instance != null) PlayerController.Instance.SetInputLocked(true);
        }

        void Update()
        {
            if (!open) return;
            // Короткая задержка: то же нажатие E, что открыло ящик, не должно сразу закрыть лист.
            if (Time.unscaledTime - openedAt < 0.3f) return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool close = (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame
                                         || kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                         || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (close) StartCoroutine(CloseNextFrame());
        }

        // Закрываемся кадром позже: в этом кадре E/Esc ещё «нажаты» — InteractionSystem или пауза
        // увидели бы разблокированного Дэнни и сработали сразу после листа.
        IEnumerator CloseNextFrame()
        {
            open = false;
            yield return null;
            canvas.gameObject.SetActive(false);
            if (PlayerController.Instance != null) PlayerController.Instance.SetInputLocked(false);
            var cb = onClosed;
            onClosed = null;
            cb?.Invoke();
        }

        void Build()
        {
            canvas = OverlayUi.CreateCanvas(transform, "DocumentSheetCanvas", 220);
            OverlayUi.Fill(canvas.transform, "Dim", new Color(0f, 0f, 0f, 0.75f));
            // Лёгкая тень под листом — бумага «лежит» поверх экрана.
            OverlayUi.Box(canvas.transform, "Shadow", new Color(0f, 0f, 0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(10f, 6f), new Vector2(SheetWidth, SheetHeight));
            var sheet = OverlayUi.Box(canvas.transform, "Sheet", new Color(0.95f, 0.94f, 0.9f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 10f), new Vector2(SheetWidth, SheetHeight)).transform;
            body = OverlayUi.Label(sheet, "Body", "", 30, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(SheetWidth - 110f, SheetHeight - 120f), TextAlignmentOptions.TopLeft);
            body.color = new Color(0.12f, 0.12f, 0.12f);
            hint = OverlayUi.Label(canvas.transform, "Hint", "[E] close", 24, new Vector2(0.5f, 0f),
                new Vector2(0f, 20f), new Vector2(400f, 40f));
            hint.color = new Color(0.75f, 0.75f, 0.7f);
            canvas.gameObject.SetActive(false);
        }
    }
}
