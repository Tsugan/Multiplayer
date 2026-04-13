using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Practice1
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerNetwork))]
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private float _speed = 5f;
        [SerializeField] private float _gravity = -18f;

        private CharacterController _characterController;
        private PlayerNetwork _playerNetwork;
        private float _verticalVelocity;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _playerNetwork = GetComponent<PlayerNetwork>();
        }

        private void Update()
        {
            if (!IsOwner || _playerNetwork == null || _playerNetwork.IsDead)
            {
                return;
            }

            Vector2 input = ReadMoveInput();
            Vector3 move = new Vector3(input.x, 0f, input.y);
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            move *= _speed;

            _verticalVelocity += _gravity * Time.deltaTime;
            if (_characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -1f;
            }

            move.y = _verticalVelocity;
            _characterController.Move(move * Time.deltaTime);
        }

        private static Vector2 ReadMoveInput()
        {
            if (Keyboard.current == null)
            {
                return Vector2.zero;
            }

            float x = 0f;
            float y = 0f;

            if (Keyboard.current.aKey.isPressed)
            {
                x -= 1f;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                x += 1f;
            }

            if (Keyboard.current.sKey.isPressed)
            {
                y -= 1f;
            }

            if (Keyboard.current.wKey.isPressed)
            {
                y += 1f;
            }

            return new Vector2(x, y);
        }
    }
}
