using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Полоска стамины под Дэнни: слева иконка бега, справа полоска. Вся группа выровнена по центру персонажа
    /// и висит под его ногами. Видна, пока Дэнни бежит или стамина не полная; через секунду после полной — тает.
    /// Выдохся — полоска красная, пока стамина не восстановится до порога бега.
    /// Создаётся сама из PlayerController (allowRun), спрайты генерируются в коде — ассеты не нужны.
    /// </summary>
    public class StaminaHud : MonoBehaviour
    {
        const float IconSize = 0.32f;
        const float Gap = 0.1f;
        const float BarWidth = 1.1f;
        const float BarHeight = 0.12f;
        const float BelowFeet = 0.28f;      // насколько ниже ног, юнитов
        const float HideAfterFull = 1f;      // секунд с полной стаминой до исчезновения
        const float FadeSpeed = 4f;
        const int SortingOrder = 400;        // поверх окружения и игрока

        static readonly Color BackColor = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color FillColor = new Color(0.92f, 0.9f, 0.82f);
        static readonly Color ExhaustedColor = new Color(0.85f, 0.25f, 0.2f);
        static readonly Color IconColor = new Color(0.92f, 0.9f, 0.82f);

        PlayerController player;
        SpriteRenderer body, back, fill, icon;
        float fullSince;
        float alpha;

        /// <summary>Создаёт полоску для игрока; body — его спрайт (по нему считаются центр и ноги).</summary>
        public static StaminaHud Attach(PlayerController player, SpriteRenderer body)
        {
            var go = new GameObject("~StaminaHud");
            // Не дочерний игроку: на него не действуют масштаб и анимации Дэнни, позицию ставим сами.
            go.transform.SetParent(player.transform.parent, false);
            var hud = go.AddComponent<StaminaHud>();
            hud.player = player;
            hud.body = body;
            hud.Build(player.StaminaHudMaterial);
            return hud;
        }

        void Build(Material mat)
        {
            var square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);
            float left = -(IconSize + Gap + BarWidth) / 2f;   // группа по центру персонажа

            icon = Part("Icon", RunIcon(), mat, SortingOrder + 1);
            icon.transform.localPosition = new Vector3(left + IconSize / 2f, 0f, 0f);
            icon.transform.localScale = Vector3.one * IconSize;

            back = Part("Back", square, mat, SortingOrder);
            back.transform.localPosition = new Vector3(left + IconSize + Gap, 0f, 0f);
            back.transform.localScale = new Vector3(BarWidth, BarHeight, 1f);

            // Полоска растёт слева направо: pivot спрайта слева, ширина = стамина.
            fill = Part("Fill", square, mat, SortingOrder + 1);
            fill.transform.localPosition = back.transform.localPosition;
            SetAlpha(0f);
        }

        SpriteRenderer Part(string name, Sprite sprite, Material mat, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            if (mat != null) sr.sharedMaterial = mat;
            return sr;
        }

        void LateUpdate()
        {
            if (player == null) { Destroy(gameObject); return; }
            gameObject.layer = player.gameObject.layer;

            // Под ногами, по центру спрайта Дэнни (если спрайта нет — по центру игрока).
            var b = body != null ? body.bounds : new Bounds(player.transform.position, Vector3.one);
            transform.position = new Vector3(b.center.x, b.min.y - BelowFeet, 0f);

            float s = player.Stamina01;
            fill.transform.localScale = new Vector3(BarWidth * s, BarHeight, 1f);

            bool full = s >= 0.999f && !player.IsRunning;
            if (!full) fullSince = Time.time;
            float target = full && Time.time - fullSince > HideAfterFull ? 0f : 1f;
            alpha = Mathf.MoveTowards(alpha, target, FadeSpeed * Time.deltaTime);
            SetAlpha(alpha);
        }

        void SetAlpha(float a)
        {
            var fc = player != null && player.Exhausted ? ExhaustedColor : FillColor;
            fill.color = new Color(fc.r, fc.g, fc.b, a);
            back.color = new Color(BackColor.r, BackColor.g, BackColor.b, BackColor.a * a);
            icon.color = new Color(IconColor.r, IconColor.g, IconColor.b, a);
        }

        /// <summary>Пиксельная иконка бегущего человечка 12×12 (# — закрашено).</summary>
        static Sprite RunIcon()
        {
            string[] rows =
            {
                "......##....",
                "......##....",
                "....####....",
                "...#.###.#..",
                "..#..##...#.",
                ".....##.....",
                ".....###....",
                "....#...#...",
                "...#.....#..",
                "..#.......#.",
                ".#..........",
                "............",
            };
            int n = rows.Length;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "RunIcon" };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    tex.SetPixel(x, n - 1 - y, rows[y][x] == '#' ? Color.white : Color.clear);
            tex.Apply();
            // pixelsPerUnit = n → иконка 1×1 юнит, дальше масштабируется до IconSize.
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
    }
}
