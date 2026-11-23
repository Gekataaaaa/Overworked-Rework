using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Управление Дэнни: только влево/вправо, без прыжка. Бег на Shift со стаминой — если включён allowRun
    /// (полоска под Дэнни — StaminaHud).
    /// Лежит на объекте Player в каждой сцене этажа (префаб Prefabs/Player/Player).
    /// Двигает Rigidbody2D по X, стены коридора останавливают его коллайдерами.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : SingletonBehaviour<PlayerController>
    {
        [Tooltip("Скорость ходьбы, юнитов в секунду")]
        [SerializeField] float speed = 1.5f;

        [Tooltip("Спрайт Дэнни: отражается по X при смене направления. Пусто — ищется в детях")]
        [SerializeField] SpriteRenderer sprite;

        [Tooltip("Диалоги, где Дэнни обращается к игроку: на его репликах из них он останавливается и смотрит в экран")]
        [SerializeField] DialogueData[] lookAtScreenDialogues;

        [Tooltip("Сколько секунд взгляд в экран нельзя прервать движением — чтобы игрок успел поймать взгляд и реплику")]
        [SerializeField] float lookMinSeconds = 1.2f;

        [Header("Бег (Shift) и стамина")]
        [Tooltip("Можно ли бегать. Пока механика не утверждена — включается в рабочих сценах (Overworked → P1 → Enable Running)")]
        [SerializeField] bool allowRun;

        [Tooltip("Во сколько раз бег быстрее ходьбы")]
        [SerializeField] float runSpeedMultiplier = 2.6f;

        [Tooltip("Запас стамины: сколько секунд можно бежать с полной полоски")]
        [SerializeField] float staminaSeconds = 8f;

        [Tooltip("Восстановление стамины, секунд бега за секунду отдыха")]
        [SerializeField] float staminaRegen = 1f;

        [Tooltip("Пауза перед восстановлением после бега, секунд")]
        [SerializeField] float regenDelay = 0.8f;

        [Tooltip("После полного выдоха бег снова доступен, когда стамина восстановится до этой доли (0..1)")]
        [Range(0f, 1f)] [SerializeField] float recoverToRun = 0.3f;

        [Tooltip("Материал полоски стамины (Sprite-Unlit-Default: видна и на тёмных этажах). Пусто — материал спрайтов по умолчанию")]
        [SerializeField] Material staminaHudMaterial;

        /// <summary>Текущий режим управления.</summary>
        public PlayerState State { get; private set; } = PlayerState.Normal;

        /// <summary>Куда смотрит Дэнни: 1 — вправо, -1 — влево.</summary>
        public int Facing { get; private set; } = 1;

        /// <summary>Идёт ли Дэнни сейчас (для анимации шагов и звука).</summary>
        public bool IsMoving { get; private set; }

        /// <summary>Бежит ли Дэнни сейчас (Shift + движение + есть стамина).</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Включён ли бег на этом игроке.</summary>
        public bool AllowRun => allowRun;

        /// <summary>
        /// Отладка (F9, DebugHotkeys): бесконечная стамина и бег вдвое быстрее обычного бега.
        /// Статическое — держится между сценами до повторного F9.
        /// </summary>
        public static bool DebugFastRun;

        /// <summary>Скорость ходьбы, юнитов в секунду (от неё считают скорость враги).</summary>
        public float WalkSpeed => speed;

        // Сброс при каждом входе в Play: при выключенном Domain Reload статика пережила бы прошлый запуск.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetDebugFastRun() => DebugFastRun = false;

        /// <summary>Стамина 0..1 (для полоски под Дэнни).</summary>
        public float Stamina01 => staminaSeconds > 0f ? stamina / staminaSeconds : 1f;

        /// <summary>Стамина кончилась и ещё не восстановилась до recoverToRun — бежать нельзя.</summary>
        public bool Exhausted { get; private set; }

        /// <summary>Материал для полоски стамины (StaminaHud).</summary>
        public Material StaminaHudMaterial => staminaHudMaterial;

        float stamina;
        float lastRunTime = -100f;

        Rigidbody2D body;
        PlayerAnimator anim;
        bool onlyRight;
        float driftUntil;
        int lastDir = 1;

        Coroutine lookRoutine;

        // Этаж спрятан под главным меню (пауза → Main Menu) — игрока «нет», пока не вернёмся по Continue.
        void OnEnable()
        {
            ReleaseInstanceWhileDisabled(true);
            if (DialogueManager.Instance != null) DialogueManager.Instance.OnLineShown += OnDialogueLine;
        }

        void OnDisable()
        {
            ReleaseInstanceWhileDisabled(false);
            if (DialogueManager.Instance != null) DialogueManager.Instance.OnLineShown -= OnDialogueLine;
            if (anim != null) anim.SetLookAtScreen(false);
            lookRoutine = null;
        }

        protected override void Awake()
        {
            base.Awake();
            body = GetComponent<Rigidbody2D>();
            // Дэнни ходит по полу, физика ему нужна только для упора в стены.
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionY;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
            anim = GetComponent<PlayerAnimator>();
            stamina = staminaSeconds;
        }

        void Start()
        {
            if (allowRun) StaminaHud.Attach(this, sprite);
        }

        void FixedUpdate()
        {
            float dir = State == PlayerState.Normal ? ReadInput() : 0f;
            IsMoving = Mathf.Abs(dir) > 0.01f;
            IsRunning = UpdateStamina(Time.fixedDeltaTime);
            float run = IsRunning ? runSpeedMultiplier * (DebugFastRun ? 2f : 1f) : 1f;
            body.linearVelocity = new Vector2(dir * speed * run, 0f);
            if (IsMoving)
            {
                Facing = dir > 0 ? 1 : -1;
                lastDir = Facing;
                if (sprite != null) sprite.flipX = Facing < 0;
            }
        }

        /// <summary>
        /// Бег и стамина: Shift + движение тратят стамину; после бега, через regenDelay, она восстанавливается.
        /// Выдохся до нуля — бежать нельзя, пока не восстановится до recoverToRun (чтобы не «дёргать» Shift на нуле).
        /// Возвращает, бежит ли Дэнни в этом шаге физики.
        /// </summary>
        bool UpdateStamina(float dt)
        {
            if (!allowRun) return false;
            bool wantsRun = IsMoving && State == PlayerState.Normal && GameInput.Run != null && GameInput.Run.IsPressed();
            bool running = wantsRun && !Exhausted && stamina > 0f;
            if (running)
            {
                if (!DebugFastRun) stamina = Mathf.Max(0f, stamina - dt);   // F9 — стамина не тратится
                lastRunTime = Time.time;
                if (stamina <= 0f) Exhausted = true;
            }
            else if (Time.time - lastRunTime >= regenDelay)
            {
                stamina = Mathf.Min(staminaSeconds, stamina + staminaRegen * dt);
                if (Exhausted && Stamina01 >= recoverToRun) Exhausted = false;
            }
            return running;
        }

        float ReadInput()
        {
            float x = GameInput.Move != null ? GameInput.Move.ReadValue<float>() : 0f;
            if (onlyRight) x = Mathf.Max(0f, x);
            // Сбой ввода: пока окно открыто, отпущенная клавиша не останавливает Дэнни.
            if (Mathf.Abs(x) < 0.01f && Time.time < driftUntil) x = lastDir;
            return Mathf.Clamp(x, -1f, 1f);
        }

        /// <summary>
        /// Блокирует/возвращает управление (телефон, мини-игры, сцены).
        /// Не счётчик: последний вызов побеждает, поэтому разблокирует тот, кто блокировал.
        /// </summary>
        public void SetInputLocked(bool locked)
        {
            State = locked ? PlayerState.Locked : PlayerState.Normal;
            if (locked && body != null) body.linearVelocity = Vector2.zero;
            Debug.Log($"[Player] SetInputLocked({locked})");
        }

        /// <summary>WhiteRoom: можно идти только вправо.</summary>
        public void SetOnlyRight(bool value) => onlyRight = value;

        /// <summary>
        /// Сбой ввода на Floor_02: следующие seconds секунд Дэнни продолжает идти
        /// в последнюю сторону после отпускания клавиши.
        /// </summary>
        public void StartInputDrift(float seconds)
        {
            driftUntil = Time.time + seconds;
            Debug.Log($"[Player] StartInputDrift({seconds})");
        }

        /// <summary>Мгновенно ставит игрока в точку (вход/выход из комнаты, под затемнением).</summary>
        public void Teleport(Transform point)
        {
            Teleport(point.position);
            Debug.Log($"[Player] Teleport → {point.name}");
        }

        /// <summary>Мгновенно ставит игрока в позицию (Continue возвращает туда, где вышли в меню).</summary>
        public void Teleport(Vector3 position)
        {
            // Двигаем и Transform, и Rigidbody: иначе интерполяция на кадр вернёт старую позицию.
            transform.position = position;
            if (body != null)
            {
                body.position = position;
                body.linearVelocity = Vector2.zero;
            }
        }

        /// <summary>Загрузка сохранения: в какую сторону смотрел Дэнни.</summary>
        public void RestoreFacing(int facing)
        {
            Facing = facing < 0 ? -1 : 1;
            lastDir = Facing;
            if (sprite != null) sprite.flipX = Facing < 0;
        }

        /// <summary>Анимация по имени: "LookAtScreen", "Fall", "LieDown", "StandUp".</summary>
        public void PlayAnim(string animName)
        {
            Debug.Log($"[Player] PlayAnim({animName})");
            if (animName == "LookAtScreen") { LookAtScreen(); return; }
            if (anim != null) anim.Play(animName);
        }

        /// <summary>
        /// Дэнни останавливается и смотрит в экран (кадр Idle — лицом к зрителю), пока идут его реплики.
        /// Сбой ввода сбрасывается. Управление возвращается само после диалога (если его не забрал кто-то ещё).
        /// </summary>
        public void LookAtScreen()
        {
            if (!isActiveAndEnabled || lookRoutine != null) return;
            if (anim != null && anim.IsLyingDown) return;   // в кровати к игроку не поворачивается
            lookRoutine = StartCoroutine(LookRoutine());
        }

        System.Collections.IEnumerator LookRoutine()
        {
            if (State == PlayerState.Normal) State = PlayerState.Cutscene;
            driftUntil = 0f;
            body.linearVelocity = Vector2.zero;
            IsMoving = false;
            if (anim != null) anim.SetLookAtScreen(true);   // фронтальный кадр — лицом к игроку

            // Первые lookMinSeconds взгляд не прерывается: даже если сценарий или диалог вернули управление,
            // Дэнни стоит — игрок успевает увидеть, что на него смотрят, и прочитать реплику.
            for (float t = 0f; t < lookMinSeconds; t += Time.deltaTime)
            {
                if (State == PlayerState.Normal) State = PlayerState.Cutscene;
                yield return null;
            }

            // Дальше — пока идут реплики (могут начаться чуть позже: шаг «анимация» стоит перед шагом «реплика»).
            // Если за это время управление вернул кто-то другой (лист документа, мини-игра, сценарий) и Дэнни
            // пошёл — взгляд заканчивается сразу: иначе он ехал бы по полу во фронтальном кадре (баг в Room_101).
            var dm = DialogueManager.Instance;
            for (float t = 0f; t < 1f && dm != null && !dm.IsPlaying && !IsMoving; t += Time.deltaTime) yield return null;
            while (dm != null && dm.IsPlaying && !IsMoving) yield return null;

            // Вернуть, только если за это время никто другой не поменял режим (телефон, сценарий). Если до взгляда
            // ввод был заблокирован, а за время паузы его разблокировали — тоже отпускаем (мы держали вместо них).
            if (State == PlayerState.Cutscene) State = PlayerState.Normal;
            if (anim != null) anim.SetLookAtScreen(false);
            lookRoutine = null;
        }

        // Реплика Дэнни из диалога «к игроку» — смотрим в экран. Сравниваем с текстом после подстановки {name}.
        void OnDialogueLine(Speaker speaker, string text)
        {
            if (speaker != Speaker.Danny || lookAtScreenDialogues == null || lookRoutine != null) return;
            foreach (var d in lookAtScreenDialogues)
            {
                if (d == null || d.Lines == null) continue;
                foreach (var line in d.Lines)
                    if (line.Speaker == Speaker.Danny && TextFormatter.Format(line.Text) == text) { LookAtScreen(); return; }
            }
        }
    }
}
