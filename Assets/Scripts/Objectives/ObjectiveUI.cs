using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overworked
{
    /// <summary>
    /// Карточка заданий в правом верхнем углу (под пульсом): список активных заданий, у каждого свой
    /// квадрат-чекбокс. Выполненное получает галочку и через пару секунд уходит из списка; пустой список —
    /// карточка прячется. Создаёт себя сам (DontDestroyOnLoad) и рисует карточку из кода.
    /// </summary>
    public class ObjectiveUI : SingletonBehaviour<ObjectiveUI>
    {
        [Tooltip("Сколько секунд видна выполненная строка с галочкой")]
        [SerializeField] float hideAfter = 2.5f;

        [Tooltip("Высота строки задания, px")]
        [SerializeField] float rowHeight = 52f;

        const float Width = 460f;
        const float HeaderHeight = 34f;

        class Row
        {
            public ObjectiveData Objective;
            public RectTransform Root;
            public Graphic Check;
        }

        readonly List<Row> rows = new List<Row>();
        RectTransform card;
        bool visible = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            if (Instance != null) return;
            var go = new GameObject("ObjectiveUI");
            DontDestroyOnLoad(go);
            go.AddComponent<ObjectiveUI>();
        }

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;
            BuildCard();
            Refresh();
        }

        void Start()
        {
            // ObjectiveManager создаётся в _Systems до первой сцены, мы — после неё: к Start он уже есть.
            if (ObjectiveManager.Instance == null) return;
            ObjectiveManager.Instance.OnStarted += Show;
            ObjectiveManager.Instance.OnCompleted += Check;
            foreach (var o in ObjectiveManager.Instance.Active) Show(o);
        }

        protected override void OnDestroy()
        {
            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.OnStarted -= Show;
                ObjectiveManager.Instance.OnCompleted -= Check;
            }
            base.OnDestroy();
        }

        /// <summary>Спрятать/показать карточку (онбординг HUD, мини-игры, WhiteRoom).</summary>
        public void SetVisible(bool value)
        {
            visible = value;
            Refresh();
        }

        void Show(ObjectiveData o)
        {
            if (o == null || rows.Exists(r => ObjectiveManager.Same(r.Objective, o))) return;
            rows.Add(BuildRow(o));
            Refresh();
        }

        void Check(ObjectiveData o)
        {
            // Закрыли задание, которого нет в списке (шаг сделан заранее), — список не трогаем.
            var row = rows.Find(r => ObjectiveManager.Same(r.Objective, o));
            if (row == null || row.Check.enabled) return;
            row.Check.enabled = true;
            StartCoroutine(RemoveLater(row));
        }

        IEnumerator RemoveLater(Row row)
        {
            yield return new WaitForSeconds(hideAfter);
            rows.Remove(row);
            if (row.Root != null) Destroy(row.Root.gameObject);
            Refresh();
        }

        // Строки — одна под другой под заголовком; высота карточки — по числу строк.
        void Refresh()
        {
            for (int i = 0; i < rows.Count; i++)
                rows[i].Root.anchoredPosition = new Vector2(0f, -HeaderHeight - i * rowHeight);
            card.sizeDelta = new Vector2(Width, HeaderHeight + rows.Count * rowHeight + 6f);
            card.gameObject.SetActive(ShouldShow);
        }

        // Карточка — только на этаже (есть Дэнни): не висит над главным меню после паузы и при Continue из сохранения.
        bool ShouldShow => visible && rows.Count > 0 && PlayerController.Instance != null;

        void LateUpdate()
        {
            if (card != null && card.gameObject.activeSelf != ShouldShow) card.gameObject.SetActive(ShouldShow);
        }

        Row BuildRow(ObjectiveData o)
        {
            var go = new GameObject("Row_" + o.Id, typeof(RectTransform));
            go.transform.SetParent(card, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(Width, rowHeight);
            // Чекбокс: светлая рамка, тёмная середина, зелёная галочка-квадрат внутри.
            OverlayUi.Box(rt, "Box", new Color(0.85f, 0.85f, 0.8f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(30f, 30f));
            OverlayUi.Box(rt, "BoxInner", new Color(0.08f, 0.08f, 0.08f), new Vector2(0f, 0.5f), new Vector2(25f, 0f), new Vector2(24f, 24f));
            var check = OverlayUi.Box(rt, "Check", new Color(0.35f, 0.85f, 0.45f), new Vector2(0f, 0.5f), new Vector2(29f, 0f), new Vector2(16f, 16f));
            check.enabled = false;
            var title = OverlayUi.Label(rt, "Title", TextFormatter.Format(o.Title), 26, new Vector2(0f, 0.5f),
                new Vector2(70f, 0f), new Vector2(370f, rowHeight), TextAlignmentOptions.Left);
            // Длинное задание («Check the documents in room 201.») вылезало за правый край карточки и экрана:
            // одна строка, шрифт уменьшается, пока текст не влезет в ширину строки.
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;
            title.fontSizeMin = 16f;
            title.fontSizeMax = 26f;
            return new Row { Objective = o, Root = rt, Check = check };
        }

        void BuildCard()
        {
            var canvas = OverlayUi.CreateCanvas(transform, "ObjectiveCanvas", 40);
            var bg = OverlayUi.Box(canvas.transform, "ObjectivePanel", new Color(0f, 0f, 0f, 0.55f),
                new Vector2(1f, 1f), new Vector2(-30f, -130f), new Vector2(Width, 90f));
            card = bg.rectTransform;
            OverlayUi.Label(card, "Header", "OBJECTIVE", 18, new Vector2(0f, 1f),
                new Vector2(70f, -8f), new Vector2(370f, 24f), TextAlignmentOptions.Left).color = new Color(0.6f, 0.65f, 0.6f);
        }
    }
}
