using System;
using System.Collections;
using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Анимация подбора: предмет взлетает над головой Дэнни, чуть зависает и втягивается в него
    /// (уменьшается и тает), затем выключается. Добавляется на предмет в момент подбора
    /// (PickupInteractable), коллайдеры предмета сразу выключаются, чтобы его нельзя было взять второй раз.
    /// Следит за игроком на ходу — Дэнни не блокируется.
    /// </summary>
    public class PickupFlyEffect : MonoBehaviour
    {
        const float RiseTime = 0.35f;
        const float HoverTime = 0.35f;
        const float AbsorbTime = 0.3f;
        const float HeadHeight = 1.7f;   // над центром Дэнни, юнитов
        const int TopSortingOrder = 500; // поверх игрока и окружения

        /// <summary>Запускает анимацию на объекте target; done — когда он выключен.</summary>
        public static void Play(GameObject target, Action done = null)
        {
            var player = PlayerController.Instance;
            if (target == null || player == null)
            {
                if (target != null) target.SetActive(false);
                done?.Invoke();
                return;
            }
            foreach (var col in target.GetComponentsInChildren<Collider2D>()) col.enabled = false;
            target.AddComponent<PickupFlyEffect>().StartCoroutine(Run(target, player.transform, done));
        }

        static IEnumerator Run(GameObject target, Transform player, Action done)
        {
            var t = target.transform;
            // Без жёлтой обводки InteractionHighlight: её копии-спрайты вложены в предмет и удаляются,
            // когда предмет теряет фокус — обращение к ним роняло анимацию и предмет зависал в воздухе.
            var renderers = System.Array.FindAll(target.GetComponentsInChildren<SpriteRenderer>(),
                sr => !sr.name.StartsWith("~"));
            var baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                baseColors[i] = renderers[i].color;
                renderers[i].sortingOrder = TopSortingOrder + i;
            }
            Vector3 startPos = t.position, startScale = t.localScale;

            // 1. Взлёт над головой (ease-out) с лёгким увеличением.
            for (float k = 0f; k < 1f; k += Time.deltaTime / RiseTime)
            {
                float e = 1f - (1f - k) * (1f - k);
                t.position = Vector3.Lerp(startPos, Head(player), e);
                t.localScale = startScale * Mathf.Lerp(1f, 1.15f, e);
                yield return null;
            }

            // 2. Висит над головой и чуть покачивается.
            for (float time = 0f; time < HoverTime; time += Time.deltaTime)
            {
                t.position = Head(player) + Vector3.up * (Mathf.Sin(time * 14f) * 0.05f);
                yield return null;
            }

            // 3. Втягивается в Дэнни: вниз к центру, уменьшается, тает (ease-in).
            Vector3 from = t.position;
            for (float k = 0f; k < 1f; k += Time.deltaTime / AbsorbTime)
            {
                float e = k * k;
                t.position = Vector3.Lerp(from, player.position + Vector3.up * 0.2f, e);
                t.localScale = startScale * Mathf.Lerp(1.15f, 0.15f, e);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;   // кто-то удалил спрайт — не роняем анимацию
                    var c = baseColors[i];
                    c.a *= 1f - e;
                    renderers[i].color = c;
                }
                yield return null;
            }

            target.SetActive(false);
            done?.Invoke();
        }

        static Vector3 Head(Transform player) => player.position + Vector3.up * HeadHeight;
    }
}
