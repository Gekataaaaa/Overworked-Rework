using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Настройки этажа, лежит в каждой сцене этажа (объект FloorSettings).
    /// При старте сцены выставляет номер этажа и стартовый рассудок.
    /// </summary>
    public class FloorSettings : MonoBehaviour
    {
        [Tooltip("3, 2, 1; 0 — WhiteRoom")]
        [SerializeField] int floor = 3;

        [Tooltip("Рассудок в начале этажа: 100 / 90 / 50 (паспорт, раздел 11)")]
        [SerializeField] float startSanity = 100f;

        void Start()
        {
            if (GameManager.Instance != null) GameManager.Instance.State.CurrentFloor = floor;
            if (SanityManager.Instance != null) SanityManager.Instance.SetSanityInstant(startSanity);
        }
    }
}
