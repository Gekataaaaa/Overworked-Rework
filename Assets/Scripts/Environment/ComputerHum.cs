using System.Collections.Generic;
using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Метка «работающий компьютер»: пока он в кадре камеры, слышен гул компьютера (играет ComputerHumPlayer).
    /// Кладётся на объект компьютера (Floor_03 — стол Дэнни, Floor_02 — компьютер Room_201). Выключенный объект
    /// или снятая галочка Working — компьютер молчит (сценарий может «выключить» его через SetWorking).
    /// </summary>
    public class ComputerHum : MonoBehaviour
    {
        [Tooltip("Компьютер включён и гудит")]
        [SerializeField] bool working = true;

        /// <summary>Включённые метки сцены.</summary>
        public static readonly List<ComputerHum> Active = new List<ComputerHum>();

        // Enter Play Mode без перезагрузки домена: список не должен тянуть компьютеры прошлого запуска.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetActive() => Active.Clear();

        /// <summary>Компьютер включён.</summary>
        public bool Working => working;

        /// <summary>Включить/выключить компьютер (из сценария).</summary>
        public void SetWorking(bool value) => working = value;

        /// <summary>Центр компьютера на экране: середина спрайта (у стола — весь стол), иначе позиция объекта.</summary>
        public Vector3 Center
        {
            get
            {
                var r = GetComponentInChildren<Renderer>();
                return r != null ? r.bounds.center : transform.position;
            }
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);
    }
}
