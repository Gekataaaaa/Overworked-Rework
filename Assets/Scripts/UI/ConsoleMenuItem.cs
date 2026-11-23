using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overworked
{
    /// <summary>
    /// Пункт меню в стиле консоли: обычный Button без фона, выбранный пункт помечается стрелкой «> »
    /// и цветом. Стрелки / W S — по пунктам (навигация UI), Enter / Space — выбрать, мышь — наведение и клик.
    /// Если выделение пропало (клик мимо пунктов), возвращается на последний выбранный пункт.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ConsoleMenuItem : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        [Tooltip("Подпись пункта")]
        [SerializeField] TMP_Text label;

        [Tooltip("Текст пункта без стрелки")]
        [SerializeField] string text = "Item";

        [Tooltip("Цвет невыбранного пункта")]
        [SerializeField] Color normalColor = new Color(0.16f, 0.14f, 0.12f);

        [Tooltip("Цвет выбранного пункта")]
        [SerializeField] Color selectedColor = new Color(0.5f, 0.05f, 0.05f);

        static ConsoleMenuItem lastSelected;
        Button button;

        void Awake()
        {
            button = GetComponent<Button>();
            Show(false);
        }

        void OnEnable() => Show(EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject);

        /// <inheritdoc/>
        public void OnSelect(BaseEventData e)
        {
            lastSelected = this;
            Show(true);
        }

        /// <inheritdoc/>
        public void OnDeselect(BaseEventData e) => Show(false);

        /// <inheritdoc/>
        public void OnPointerEnter(PointerEventData e)
        {
            if (button.interactable) button.Select();
        }

        void Update()
        {
            // Клик по пустому месту снимает выделение — без выбранного пункта клавиатура перестаёт работать.
            var es = EventSystem.current;
            if (lastSelected == this && es != null && es.currentSelectedGameObject == null && isActiveAndEnabled)
                button.Select();
        }

        void Show(bool selected)
        {
            if (label == null) return;
            // Пробелы вместо стрелки — пункты не прыгают по горизонтали (моноширинный шрифт).
            label.text = (selected ? "> " : "  ") + text;
            var c = selected ? selectedColor : normalColor;
            // Недоступный пункт (Main Menu в паузе, пока идёт сценарий) — тусклый.
            if (button != null && !button.interactable) c.a *= 0.3f;
            label.color = c;
        }

        /// <summary>Сделать пункт доступным/недоступным (недоступный тусклый и пропускается стрелками).</summary>
        public void SetInteractable(bool value)
        {
            if (button == null) button = GetComponent<Button>();
            button.interactable = value;
            Show(EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject);
        }
    }
}
