using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Фон этажа темнеет вместе с лампами: лампы погасли (мерцание галлюцинации) — фон до darkLevel (50%),
    /// лампы снова горят — фон возвращает обычную яркость. Между — плавно, по средней яркости ламп (LampLight.Level).
    /// Цвета запоминаются на старте и только умножаются — арт не меняется; прозрачность объектов не трогается.
    /// wholeScene — темнеет всё на этаже (фон, двери, предметы, Дэнни), кроме самих ламп.
    /// </summary>
    public class BackgroundDimmer : MonoBehaviour
    {
        [Tooltip("Лампы этажа (LampLight)")]
        [SerializeField] LampLight[] lamps;

        [Tooltip("Что затемнять: спрайты фона (Corridor/Background)")]
        [SerializeField] SpriteRenderer[] background;

        [Tooltip("Затемнять все спрайты сцены этажа (кроме трубок и конусов ламп), а не только background")]
        [SerializeField] bool wholeScene;

        [Tooltip("Яркость фона, когда лампы полностью погасли (доля обычной)")]
        [Range(0f, 1f)] [SerializeField] float darkLevel = 0.5f;

        Color[] baseColors;
        float shown = 1f;

        // В Start: к этому времени все объекты сцены (и игрок) уже на месте.
        void Start()
        {
            if (wholeScene) background = CollectScene();
            baseColors = new Color[background.Length];
            for (int i = 0; i < background.Length; i++)
                if (background[i] != null) baseColors[i] = background[i].color;
        }

        SpriteRenderer[] CollectScene()
        {
            var skip = new System.Collections.Generic.HashSet<SpriteRenderer>();
            foreach (var l in lamps)
            {
                if (l == null) continue;
                if (l.Tube != null) skip.Add(l.Tube);
                if (l.Cone != null) skip.Add(l.Cone);
            }
            var list = new System.Collections.Generic.List<SpriteRenderer>();
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (!skip.Contains(sr)) list.Add(sr);
            return list.ToArray();
        }

        void LateUpdate()
        {
            if (lamps == null || lamps.Length == 0) return;
            float sum = 0f;
            int n = 0;
            foreach (var l in lamps)
                if (l != null) { sum += l.Level; n++; }
            float level = n > 0 ? sum / n : 1f;

            float k = Mathf.Lerp(darkLevel, 1f, level);
            // Не трогаем цвета каждый кадр, пока ничего не мигает.
            if (Mathf.Approximately(k, shown)) return;
            shown = k;
            for (int i = 0; i < background.Length; i++)
            {
                if (background[i] == null) continue;
                var c = baseColors[i];
                // Прозрачность — текущая: её могут менять другие (подбор предмета тает).
                background[i].color = new Color(c.r * k, c.g * k, c.b * k, background[i].color.a);
            }
        }
    }
}
