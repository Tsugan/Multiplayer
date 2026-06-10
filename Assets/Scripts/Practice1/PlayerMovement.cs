using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Practice1
{
    public struct MoveData : IReplicateData
    {
        public float Horizontal;
        public float Vertical;
        public float AimYaw;
        private uint _tick;

        public MoveData(Vector2 input, float aimYaw)
        {
            Horizontal = input.x;
            Vertical = input.y;
            AimYaw = aimYaw;
            _tick = 0;
        }

        public void Dispose()
        {
        }

        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    public struct ReconcileData : IReconcileData
    {
        public Vector3 Position;
        public float AimYaw;
        public float VerticalVelocity;
        private uint _tick;

        public ReconcileData(Vector3 position, float aimYaw, float verticalVelocity)
        {
            Position = position;
            AimYaw = aimYaw;
            VerticalVelocity = verticalVelocity;
            _tick = 0;
        }

        public void Dispose()
        {
        }

        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerNetwork))]
    public class PlayerMovement : NetworkBehaviour
    {
        public static bool ClientSidePredictionEnabled { get; private set; } = true;

        [SerializeField] private float _speed = 5f;
        [SerializeField] private float _gravity = -18f;

        private CharacterController _characterController;
        private PlayerNetwork _playerNetwork;
        private float _verticalVelocity;
        private MoveData _serverAuthoritativeMoveData;
        private bool _hasServerAuthoritativeMoveData;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _playerNetwork = GetComponent<PlayerNetwork>();
        }

        public override void OnStartNetwork()
        {
            base.TimeManager.OnTick += OnTick;
        }

        public override void OnStopNetwork()
        {
            if (base.TimeManager != null)
            {
                base.TimeManager.OnTick -= OnTick;
            }

            _hasServerAuthoritativeMoveData = false;
        }

        private void OnTick()
        {
            bool canMove = _playerNetwork != null && !_playerNetwork.IsDead && GameManager.IsGameplayActive;
            bool replicated = false;
            bool waitsForServerMovement = false;

            if (base.IsOwner && canMove)
            {
                Vector2 input = ReadMoveInput();
                float aimYaw = ReadAimYaw(transform.position, transform.eulerAngles.y);
                if (ClientSidePredictionEnabled || base.IsServerInitialized)
                {
                    Replicate(new MoveData(input, aimYaw));
                    replicated = true;
                }
                else
                {
                    SubmitMoveInputServerRpc(input.x, input.y, aimYaw, Channel.Unreliable);
                    waitsForServerMovement = true;
                }
            }

            if (!replicated && base.IsServerInitialized && _hasServerAuthoritativeMoveData && canMove)
            {
                Replicate(_serverAuthoritativeMoveData);
                _hasServerAuthoritativeMoveData = false;
                replicated = true;
            }

            if (!replicated && !waitsForServerMovement)
            {
                Replicate(default);
            }

            if (base.IsServerInitialized)
            {
                CreateReconcile();
            }
        }

        public static void SetClientSidePredictionEnabled(bool enabled)
        {
            ClientSidePredictionEnabled = enabled;
        }

        [ServerRpc]
        private void SubmitMoveInputServerRpc(float horizontal, float vertical, float aimYaw, Channel channel = Channel.Reliable)
        {
            _serverAuthoritativeMoveData = new MoveData(new Vector2(horizontal, vertical), aimYaw);
            _hasServerAuthoritativeMoveData = true;
        }

        public override void CreateReconcile()
        {
            Reconcile(new ReconcileData(transform.position, transform.eulerAngles.y, _verticalVelocity));
        }

        [Replicate]
        private void Replicate(MoveData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            if (_playerNetwork != null && _playerNetwork.IsDead)
            {
                return;
            }

            float tickDelta = (float)base.TimeManager.TickDelta;
            transform.rotation = Quaternion.Euler(0f, data.AimYaw, 0f);
            Vector3 move = new Vector3(data.Horizontal, 0f, data.Vertical);
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            move *= _speed;

            _verticalVelocity += _gravity * tickDelta;
            if (_characterController != null && _characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -1f;
            }

            move.y = _verticalVelocity;

            if (_characterController != null && _characterController.enabled)
            {
                _characterController.Move(move * tickDelta);
            }
            else
            {
                transform.position += move * tickDelta;
            }
        }

        [Reconcile]
        private void Reconcile(ReconcileData data, Channel channel = Channel.Unreliable)
        {
            bool restoreCharacterController = _characterController != null && _characterController.enabled;
            if (restoreCharacterController)
            {
                _characterController.enabled = false;
            }

            transform.position = data.Position;
            transform.rotation = Quaternion.Euler(0f, data.AimYaw, 0f);
            _verticalVelocity = data.VerticalVelocity;

            if (restoreCharacterController)
            {
                _characterController.enabled = true;
            }
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

        private static float ReadAimYaw(Vector3 playerPosition, float fallbackYaw)
        {
            if (Mouse.current == null || Camera.main == null)
            {
                Vector2 input = ReadMoveInput();
                if (input.sqrMagnitude > 0.01f)
                {
                    return Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;
                }

                return fallbackYaw;
            }

            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane ground = new Plane(Vector3.up, new Vector3(0f, playerPosition.y, 0f));
            if (!ground.Raycast(ray, out float distance))
            {
                return fallbackYaw;
            }

            Vector3 point = ray.GetPoint(distance);
            Vector3 direction = point - playerPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                return fallbackYaw;
            }

            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }
    }
}
