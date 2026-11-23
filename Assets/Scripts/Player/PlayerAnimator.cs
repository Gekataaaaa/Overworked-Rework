using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Связывает PlayerController с Animator: параметр Speed для ходьбы и сценарные анимации по имени.
    /// Взгляд в экран — отдельный фронтальный кадр (lookAtScreenSprite): Animator его не знает, поэтому
    /// кадр ставится поверх анимации в LateUpdate, пока PlayerController держит взгляд.
    /// Так же — «ложится в кровать» (Белая комната): кадры lieDownFrames проигрываются один раз и последний держится.
    /// Пока у Дэнни нет арта, Animator может отсутствовать — тогда компонент ничего не делает.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAnimator : MonoBehaviour
    {
        [Tooltip("Animator Дэнни. Пусто — ищется в детях")]
        [SerializeField] Animator animator;

        [Tooltip("Кадр «Дэнни смотрит в экран» (лицом к камере). Пусто — во время взгляда обычный Idle")]
        [SerializeField] Sprite lookAtScreenSprite;

        [Tooltip("Кадры «ложится в кровать» (кровать нарисована в кадре). Пусто — Play(\"LieDown\") уходит в Animator")]
        [SerializeField] Sprite[] lieDownFrames;

        [Tooltip("Скорость укладывания, кадров в секунду")]
        [SerializeField] float lieDownFps = 5f;

        /// <summary>Сейчас показан фронтальный кадр «смотрит в экран» (на ходу — никогда).</summary>
        public bool ShowsLookAtScreen => lookingAtScreen && lookAtScreenSprite != null && !IsLyingDown
                                         && controller != null && !controller.IsMoving;

        /// <summary>Дэнни лёг (или ложится) — дальше только последний кадр, взгляд в экран не включается.</summary>
        public bool IsLyingDown => lieDownStart >= 0f;

        static readonly int SpeedParam = Animator.StringToHash("Speed");

        PlayerController controller;
        SpriteRenderer body;
        bool lookingAtScreen;
        float lieDownStart = -1f;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) body = animator.GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.SetFloat(SpeedParam, controller.IsMoving ? 1f : 0f);
            // Отдельной анимации бега нет — на бегу шаги проигрываются быстрее.
            animator.speed = controller.IsRunning ? RunAnimSpeed : 1f;
        }

        const float RunAnimSpeed = 1.7f;

        // Animator выставил кадр в этом кадре — подменяем его фронтальным (до HeldFlashlight, он читает кадр позже).
        void LateUpdate()
        {
            if (body == null) return;
            if (IsLyingDown)
            {
                int i = Mathf.Min((int)((Time.time - lieDownStart) * lieDownFps), lieDownFrames.Length - 1);
                body.sprite = lieDownFrames[i];
                body.flipX = false;   // кровать в кадре справа от Дэнни
                return;
            }
            // На ходу фронтальный кадр не показываем никогда — даже если взгляд ещё не снят.
            if (ShowsLookAtScreen) body.sprite = lookAtScreenSprite;
        }

        /// <summary>Включить/выключить кадр «смотрит в экран» (вызывает PlayerController.LookAtScreen).</summary>
        public void SetLookAtScreen(bool value)
        {
            lookingAtScreen = value && !IsLyingDown;
            if (value && animator != null && animator.runtimeAnimatorController != null) animator.Play("Idle");
        }

        /// <summary>Проигрывает состояние Animator с таким же именем ("Idle", "Fall"…). LookAtScreen делает PlayerController.</summary>
        public void Play(string stateName)
        {
            if (stateName == "LieDown" && lieDownFrames != null && lieDownFrames.Length > 0)
            {
                lookingAtScreen = false;
                lieDownStart = Time.time;
                if (animator != null && animator.runtimeAnimatorController != null) animator.Play("Idle");
                return;
            }
            lieDownStart = -1f;   // любое другое состояние (StandUp…) — встаёт
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.Play(stateName);
        }
    }
}
