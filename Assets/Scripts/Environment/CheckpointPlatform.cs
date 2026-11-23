using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Overworked
{
    /// <summary>
    /// Платформа-чекпоинт: Дэнни наступил — SaveSystem.Checkpoint(), платформа, свет и партиклы плавно
    /// становятся голубыми. Срабатывает один раз (used сохраняется в снимке — после перезагрузки уже голубая).
    /// Если сохранить сейчас нельзя (сценарий, Дэнни занят) — пробует снова, пока он стоит на платформе.
    /// Собирается меню Overworked/P1/Objects/Add Checkpoint To Corridor (CheckpointBuilder).
    /// </summary>
    public class CheckpointPlatform : MonoBehaviour
    {
        [SerializeField] SpriteRenderer platform;
        [SerializeField] SpriteRenderer beam;
        [SerializeField] Light2D glow;
        [SerializeField] ParticleSystem particles;
        [SerializeField] Color idleColor = new Color(1f, 0.93f, 0.8f);
        [SerializeField] Color activeColor = new Color(0.5f, 0.82f, 1f);
        [SerializeField] float colorTime = 0.5f;

        bool used;
        float blend;   // 0 — idleColor, 1 — activeColor
        float nextTry;

        void Start()
        {
            blend = used ? 1f : 0f;
            Apply(used ? activeColor : idleColor);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null)
                Debug.Log($"[Checkpoint] {name}: Дэнни на платформе (уже сохранено: {used})");
        }

        void OnTriggerStay2D(Collider2D other)
        {
            if (used || Time.time < nextTry || other.GetComponentInParent<PlayerController>() == null) return;
            nextTry = Time.time + 0.5f;   // нельзя сейчас — пробуем раз в полсекунды, а не каждый кадр
            used = true;   // до снимка — чтобы в чекпоинте платформа уже была отмечена
            if (!SaveSystem.Checkpoint(transform.position.x)) used = false;   // появление — по центру платформы
        }

        void Update()
        {
            if (!used || blend >= 1f) return;
            blend = Mathf.Min(1f, blend + Time.deltaTime / Mathf.Max(0.01f, colorTime));
            Apply(Color.Lerp(idleColor, activeColor, blend));
        }

        void Apply(Color c)
        {
            if (platform != null) platform.color = c;
            if (beam != null) beam.color = new Color(c.r, c.g, c.b, beam.color.a);
            if (glow != null) glow.color = c;
            if (particles != null)
            {
                var main = particles.main;
                main.startColor = c;
            }
        }
    }
}
