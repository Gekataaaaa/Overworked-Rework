using System;
using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Точка входа для галлюцинаций (живёт в _Systems): сценарии и триггеры вызывают Play,
    /// само проигрывание — в HallucinationEvent (визуал, звук, пульс, рассудок).
    /// </summary>
    public class HallucinationManager : SingletonBehaviour<HallucinationManager>
    {
        /// <summary>Галлюцинация, которая идёт сейчас (null — никакой).</summary>
        public HallucinationEvent Current { get; private set; }

        /// <summary>Проигрывает галлюцинацию h, по окончании вызывает done.</summary>
        public void Play(HallucinationEvent h, Action done = null)
        {
            Debug.Log($"[Hallucinations] Play {(h != null ? h.name : "null")}");
            if (h == null)
            {
                done?.Invoke();
                return;
            }
            Current = h;
            h.Play(() =>
            {
                if (Current == h) Current = null;
                done?.Invoke();
            });
        }
    }
}
