using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Practice1
{
    [RequireComponent(typeof(PlayerShooting))]
    public class PlayerInput : NetworkBehaviour
    {
        private PlayerShooting _shooting;

        private void Awake()
        {
            _shooting = GetComponent<PlayerShooting>();
        }

        private void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                _shooting.TryShoot();
            }
        }
    }
}
