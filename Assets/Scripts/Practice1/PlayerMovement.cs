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
        private uint _tick;

        public MoveData(Vector2 input)
        {
            Horizontal = input.x;
            Vertical = input.y;
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
        public float VerticalVelocity;
        private uint _tick;

        public ReconcileData(Vector3 position, float verticalVelocity)
        {
            Position = position;
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
            bool canMove = _playerNetwork != null && !_playerNetwork.IsDead;
            bool replicated = false;
            bool waitsForServerMovement = false;

            if (base.IsOwner && canMove)
            {
                Vector2 input = ReadMoveInput();
                if (ClientSidePredictionEnabled || base.IsServerInitialized)
                {
                    Replicate(new MoveData(input));
                    replicated = true;
                }
                else
                {
                    SubmitMoveInputServerRpc(input.x, input.y, Channel.Unreliable);
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
        private void SubmitMoveInputServerRpc(float horizontal, float vertical, Channel channel = Channel.Reliable)
        {
            _serverAuthoritativeMoveData = new MoveData(new Vector2(horizontal, vertical));
            _hasServerAuthoritativeMoveData = true;
        }

        public override void CreateReconcile()
        {
            Reconcile(new ReconcileData(transform.position, _verticalVelocity));
        }

        [Replicate]
        private void Replicate(MoveData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            if (_playerNetwork != null && _playerNetwork.IsDead)
            {
                return;
            }

            float tickDelta = (float)base.TimeManager.TickDelta;
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
    }
}
