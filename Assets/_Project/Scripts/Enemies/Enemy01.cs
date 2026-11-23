using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// enemy01 — чёрный квадрат в коридоре. Запускается триггером (Enemy01Trigger): появляется у правого конца
    /// коридора и идёт влево со скоростью ходьбы Дэнни × speedVsPlayer; пропадает, только когда целиком
    /// ушёл за левый край камеры (исчезновения не видно).
    /// Увидел Дэнни (впереди в пределах viewDistance или вплотную сзади) — идёт за ним.
    /// Догнал — пропадает и снимает sanityDamage рассудка. Дэнни спрятался в шкафчик (HideLocker) вне поля
    /// обзора — враг теряет его и идёт дальше влево; залез на глазах у врага — тот всё равно подходит и пугает; Дэнни вылез, пока враг ещё на экране, — враг снова идёт за ним.
    /// Срабатывает один раз за загрузку сцены. Собирается меню Overworked → P1 → Objects → Add Enemy01 To Open Corridor.
    /// </summary>
    public class Enemy01 : MonoBehaviour
    {
        [Tooltip("Во сколько раз быстрее ходьбы Дэнни (бегом от него можно уйти)")]
        [SerializeField] float speedVsPlayer = 2f;

        [Tooltip("Левый конец пути (локальный X относительно родителя-коридора). Пропадает враг, только когда целиком уйдёт за левый край камеры; без камеры — тут")]
        [SerializeField] float leftEndX = -25f;

        [Tooltip("Правый конец (локальный X): тут враг появляется")]
        [SerializeField] float rightEndX = 25f;

        [Tooltip("Как далеко враг видит перед собой, юнитов")]
        [SerializeField] float viewDistance = 10f;

        [Tooltip("Вплотную враг замечает Дэнни с любой стороны (догнали его сзади), юнитов")]
        [SerializeField] float noticeDistance = 1.5f;

        [Tooltip("Доля дальности обзора: залез в шкафчик ближе viewDistance × это (перед врагом) — враг видел и всё равно идёт пугать")]
        [Range(0f, 1f)] [SerializeField] float hideSpotFraction = 0.5f;

        [Tooltip("На таком расстоянии по X враг догнал Дэнни")]
        [SerializeField] float catchDistance = 0.7f;

        [Tooltip("Насколько разница по высоте ещё считается «тот же коридор», юнитов")]
        [SerializeField] float sameFloorHeight = 2.5f;

        [Tooltip("Сколько рассудка снять, если враг догнал")]
        [SerializeField] float sanityDamage = 20f;

        [Tooltip("Видимая часть врага (чёрный квадрат): прячется, пока враг не активен")]
        [SerializeField] GameObject body;

        [Header("Конус обзора")]
        [Tooltip("Отладка: полупрозрачный конус обзора (жёлтый — идёт, красный — преследует). Только редактор и dev-билд")]
        [SerializeField] bool showVision = true;

        [Tooltip("Высота конуса на дальнем конце, юнитов")]
        [SerializeField] float visionHeight = 3f;

        [SerializeField] Color walkVisionColor = new Color(1f, 0.9f, 0.3f, 0.35f);
        [SerializeField] Color chaseVisionColor = new Color(1f, 0.15f, 0.1f, 0.45f);

        enum State { Idle, Walk, Chase }

        State state = State.Idle;
        bool used;
        bool wasHidden;
        bool sawHide;   // видел, как Дэнни залез в шкафчик, — шкафчик его не спасает
        int facing = -1;
        SpriteRenderer vision;
        Vector2 eye;   // точка «глаза» относительно корня врага — вершина конуса

        /// <summary>Враг сейчас в коридоре (идёт или преследует).</summary>
        public bool Active => state != State.Idle;

        void Awake()
        {
            if (body != null) body.SetActive(false);
        }

        /// <summary>Появиться у правого конца и пойти влево (один раз).</summary>
        public void Activate()
        {
            if (used) return;
            used = true;
            var p = transform.localPosition;
            transform.localPosition = new Vector3(rightEndX, p.y, p.z);
            MatchPlayerHitbox();
            facing = -1;
            wasHidden = HideLocker.Occupied != null;
            state = State.Walk;
            if (body != null) body.SetActive(true);
            // Только для разработчика: в редакторе и dev-билде, в релизе конуса нет.
            if (showVision && Debug.isDebugBuild) BuildVision();
            Debug.Log("[Enemy01] появился");
        }

        // Квадрат — того же размера и на той же высоте, что хитбокс Дэнни (BoxCollider2D на корне Player).
        void MatchPlayerHitbox()
        {
            var player = PlayerController.Instance;
            if (player == null || body == null || !player.TryGetComponent(out BoxCollider2D box)) return;
            var ps = player.transform.lossyScale;
            var size = new Vector2(box.size.x * ps.x, box.size.y * ps.y);
            var offset = new Vector2(box.offset.x * ps.x, box.offset.y * ps.y);
            // Корень врага — на высоте корня Дэнни, квадрат смещён как его коллайдер.
            var pos = transform.position;
            transform.position = new Vector3(pos.x, player.transform.position.y, pos.z);
            body.transform.localPosition = new Vector3(0f, offset.y, 0f);
            body.transform.localScale = new Vector3(size.x, size.y, 1f);
            eye = new Vector2(0f, offset.y + size.y * 0.3f);   // на уровне «головы»
        }

        // Конус: треугольник с вершиной в глазу, длиной viewDistance; к дальнему концу тает.
        void BuildVision()
        {
            if (vision == null)
            {
                var go = new GameObject("~Vision");
                go.transform.SetParent(transform, false);
                vision = go.AddComponent<SpriteRenderer>();
                vision.sprite = ConeSprite();
                var bodySr = body != null ? body.GetComponent<SpriteRenderer>() : null;
                if (bodySr != null)
                {
                    vision.sharedMaterial = bodySr.sharedMaterial;
                    vision.sortingLayerID = bodySr.sortingLayerID;
                    vision.sortingOrder = bodySr.sortingOrder - 1;
                }
            }
            vision.gameObject.SetActive(true);
            DrawVision();
        }

        void DrawVision()
        {
            if (vision == null) return;
            // Спрайт 1×1 юнит, вершина слева по центру: тянем на viewDistance и разворачиваем по facing.
            vision.transform.localPosition = new Vector3(eye.x, eye.y, 0f);
            vision.transform.localScale = new Vector3(facing * viewDistance, visionHeight, 1f);
            vision.color = state == State.Chase ? chaseVisionColor : walkVisionColor;
        }

        static Sprite coneSprite;

        static Sprite ConeSprite()
        {
            if (coneSprite != null) return coneSprite;
            const int w = 64, h = 64;   // квадрат: при PPU = w спрайт ровно 1×1 юнит
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;                     // 0 — вершина, 1 — дальний край
                    float v = Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f; // 0 — центр, 1 — край по высоте
                    float a = v <= u ? 1f - u * 0.8f : 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            coneSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), w);
            return coneSprite;
        }

        void Update()
        {
            if (state == State.Idle || Time.timeScale <= 0f) return;
            var player = PlayerController.Instance;

            bool hidden = HideLocker.Occupied != null;
            if (player != null)
            {
                // Залез в шкафчик на глазах у врага (в поле обзора) — враг знает, где он, и идёт пугать.
                if (hidden && !wasHidden)
                {
                    sawHide = Sees(player, viewDistance * hideSpotFraction);
                    if (sawHide) state = State.Chase;
                }
                if (!hidden) sawHide = false;
                // Вылез из шкафчика, пока враг ещё виден, — враг разворачивается к нему.
                if (wasHidden && !hidden && OnScreen()) state = State.Chase;
                if (state == State.Walk && !hidden && Sees(player)) state = State.Chase;
                // Спрятался незаметно — враг теряет его и идёт дальше влево.
                if (state == State.Chase && hidden && !sawHide) state = State.Walk;
            }
            wasHidden = hidden;

            float speed = (player != null ? player.WalkSpeed : 1.5f) * speedVsPlayer;
            float x = transform.position.x;
            if (state == State.Chase && player != null)
            {
                float dx = player.transform.position.x - x;
                facing = dx >= 0f ? 1 : -1;
                if (Mathf.Abs(dx) <= catchDistance)
                {
                    Caught();
                    return;
                }
            }
            else facing = -1;

            transform.position += new Vector3(facing * speed * Time.deltaTime, 0f, 0f);
            DrawVision();

            // Уходит влево (не преследует) и целиком скрылся за левым краем камеры — пропадает.
            // Дошёл до левого конца, а камера его ещё видит, — идёт дальше, пока не скроется: исчезновения не видно.
            if (state == State.Walk && LeftOfCamera()) Vanish();
        }

        // Весь квадрат левее левого края кадра.
        bool LeftOfCamera()
        {
            var cam = Camera.main;
            if (cam == null) return transform.localPosition.x <= leftEndX;
            var sr = body != null ? body.GetComponent<SpriteRenderer>() : null;
            float right = sr != null ? sr.bounds.max.x : transform.position.x;
            return cam.WorldToViewportPoint(new Vector3(right, transform.position.y, transform.position.z)).x < 0f;
        }

        // Дэнни в том же коридоре и либо впереди в пределах viewDistance, либо вплотную с любой стороны.
        // range — дальность взгляда вперёд (по умолчанию viewDistance; для «видел, как прячется» — её доля).
        bool Sees(PlayerController player, float range = -1f)
        {
            if (range < 0f) range = viewDistance;
            Vector2 d = player.transform.position - transform.position;
            if (Mathf.Abs(d.y) > sameFloorHeight) return false;
            if (Mathf.Abs(d.x) <= noticeDistance) return true;
            return d.x * facing > 0f && Mathf.Abs(d.x) <= range;
        }

        // В редакторе у выделенного врага: линия обзора и путь между концами.
        void OnDrawGizmosSelected()
        {
            var p = transform.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(p, p + new Vector3(facing * viewDistance, 0f, 0f));
            var parent = transform.parent;
            if (parent == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(parent.TransformPoint(new Vector3(leftEndX, transform.localPosition.y, 0f)), 0.3f);
            Gizmos.DrawWireSphere(parent.TransformPoint(new Vector3(rightEndX, transform.localPosition.y, 0f)), 0.3f);
        }

        bool OnScreen()
        {
            var cam = Camera.main;
            if (cam == null) return true;
            var v = cam.WorldToViewportPoint(transform.position);
            return v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
        }

        void Caught()
        {
            var sanity = SanityManager.Instance;
            if (sanity != null) sanity.SetSanity(sanity.Target - sanityDamage);
            Debug.Log($"[Enemy01] догнал: рассудок −{sanityDamage}");
            // Напугал в шкафчике — Дэнни от испуга вываливается наружу.
            if (HideLocker.Occupied != null) HideLocker.Occupied.Exit();
            Vanish();
        }

        void Vanish()
        {
            state = State.Idle;
            if (body != null) body.SetActive(false);
            if (vision != null) vision.gameObject.SetActive(false);
        }
    }
}
