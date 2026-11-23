using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Overworked
{
    /// <summary>
    /// Мерцание настоящего света сцены во время галлюцинации: «лампы резко мигают» (H1),
    /// «свет бешено мигает» (H3), «лампы мигают в такт сердцебиению» (H4).
    /// Список ламп пустой — мигает весь свет сцены (Global Light 2D и лампы), кроме фонарика:
    /// к конкретным лампам пока не привязано, подключим позже (поле lights).
    /// </summary>
    public class LightFlickerVisual : HallucinationVisual
    {
        public enum Mode { Wild, Heartbeat }

        [Tooltip("Какие лампы мигают. Пусто — весь свет сцены, кроме фонарика")]
        [SerializeField] Light2D[] lights;

        [Tooltip("Wild — бешено/резко мигает; Heartbeat — провал света на каждый удар сердца")]
        [SerializeField] Mode mode = Mode.Wild;

        [Tooltip("Сколько секунд мигает")]
        [SerializeField] float duration = 1.5f;

        [Tooltip("Смен состояния в секунду (Wild)")]
        [SerializeField] float rate = 12f;

        [Tooltip("Яркость в «тёмной» фазе (доля от обычной)")]
        [Range(0f, 1f)] [SerializeField] float darkLevel = 0.1f;

        [Tooltip("Яркость общего света сцены (Global Light 2D) в «тёмной» фазе. Меньше 0 — как darkLevel")]
        [SerializeField] float globalDarkLevel = -1f;

        [Tooltip("Яркость во «вспышке» (Wild): больше 1 — лампа резко вспыхивает ярче обычного")]
        [SerializeField] float brightLevel = 1f;

        [Tooltip("Длина провала на удар сердца, секунд (Heartbeat)")]
        [SerializeField] float beatDipTime = 0.12f;

        [Tooltip("Удар (звук + спайк) на первом мигании")]
        [SerializeField] bool hitOnFirst = true;

        public override IEnumerator Play(HallucinationEvent hallucination)
        {
            var targets = Collect();
            var baseIntensity = new float[targets.Count];
            for (int i = 0; i < targets.Count; i++) baseIntensity[i] = targets[i].intensity;
            void SetLevel(float k)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    if (targets[i] == null) continue;
                    // Общий свет в тёмной фазе может гаснуть слабее/сильнее ламп (globalDarkLevel).
                    float level = k == darkLevel && globalDarkLevel >= 0f && targets[i].lightType == Light2D.LightType.Global
                        ? globalDarkLevel : k;
                    targets[i].intensity = baseIntensity[i] * level;
                }
            }

            SeenIfNeeded(hallucination);
            if (hitOnFirst) HitIfNeeded(hallucination);

            var heart = HeartbeatManager.Instance;
            if (mode == Mode.Heartbeat && heart != null)
            {
                float dimUntil = 0f;
                void OnBeat() => dimUntil = Time.time + beatDipTime;
                heart.OnBeat += OnBeat;
                for (float t = 0f; t < duration; t += Time.deltaTime)
                {
                    SetLevel(Time.time < dimUntil ? darkLevel : 1f);
                    yield return null;
                }
                heart.OnBeat -= OnBeat;
            }
            else
            {
                bool dark = true;
                float next = 0f;
                for (float t = 0f; t < duration; t += Time.deltaTime)
                {
                    if (t >= next)
                    {
                        dark = !dark;
                        next = t + Random.Range(0.5f, 1.5f) / Mathf.Max(0.1f, rate);
                    }
                    // Каждый кадр: LightFlicker на лампе в своём Update возвращает обычную яркость —
                    // если ставить уровень только при переключении, лампа гаснет лишь на один кадр.
                    SetLevel(dark ? darkLevel : brightLevel);
                    yield return null;
                }
            }
            SetLevel(1f);
            HitIfNeeded(hallucination);
        }

        List<Light2D> Collect()
        {
            var result = new List<Light2D>();
            if (lights != null)
                foreach (var l in lights)
                    if (l != null) result.Add(l);
            if (result.Count > 0) return result;
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsInactive.Exclude))
                if (l.GetComponentInParent<FlashlightController>() == null) result.Add(l);
            return result;
        }
    }
}
