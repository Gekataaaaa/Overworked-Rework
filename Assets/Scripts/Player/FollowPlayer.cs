using Unity.Cinemachine;
using UnityEngine;

namespace Overworked
{
    /// <summary>
    /// Вешается на CinemachineCamera (коридор, комнаты): при старте сцены сам назначает
    /// Follow = игрок, чтобы не тянуть ссылку руками в каждой сцене этажа.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class FollowPlayer : MonoBehaviour
    {
        void Start()
        {
            var player = PlayerController.Instance;
            if (player == null)
            {
                Debug.LogWarning($"[Player] FollowPlayer на {name}: в сцене нет игрока");
                return;
            }
            GetComponent<CinemachineCamera>().Follow = player.transform;
        }
    }
}
