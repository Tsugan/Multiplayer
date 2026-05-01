using FishNet.Component.Transforming;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Practice1
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerNetwork))]
    public class PlayerMovement : TickNetworkBehaviour
    {
        public struct MoveData : IReplicateData
        {
            public float Horizontal;
            public float Vertical;

            private uint _tick;

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        public struct ReconcileData : IReconcileData
        {
            public Vector3 Position;
            public float VerticalVelocity;

            private uint _tick;

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        [SerializeField] private float _speed = 5f;
        [SerializeField] private float _gravity = -18f;

        private CharacterController _characterController;
        private PlayerNetwork _playerNetwork;
        private NetworkTransform _networkTransform;
        private float _verticalVelocity;
        private Vector2 _classicInput;
        private bool _lastPredictionEnabled;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _playerNetwork = GetComponent<PlayerNetwork>();
            _networkTransform = GetComponent<NetworkTransform>();
            SetTickCallbacks(TickCallback.Tick);
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _lastPredictionEnabled = ConnectionUI.PredictionEnabled;
            ApplyMovementMode();
        }

        protected override void TimeManager_OnTick()
        {
            if (_playerNetwork == null)
            {
                return;
            }

            if (_lastPredictionEnabled != ConnectionUI.PredictionEnabled)
            {
                ApplyMovementMode();
            }

            if (_playerNetwork.IsDead)
            {
                _classicInput = Vector2.zero;
                return;
            }

            if (ConnectionUI.PredictionEnabled)
            {
                if (base.IsOwner)
                {
                    PerformReplicate(BuildMoveData());
                }
                else
                {
                    PerformReplicate(default);
                }

                CreateReconcile();
            }
            else
            {
                RunClassicTick();
            }
        }

        private void RunClassicTick()
        {
            if (base.IsOwner)
            {
                Vector2 input = ReadMoveInput();
                if (base.IsServerInitialized)
                {
                    _classicInput = input;
                }
                else
                {
                    SubmitClassicInputServerRpc(input);
                }
            }

            if (base.IsServerInitialized)
            {
                SimulateMovement(_classicInput);
            }
        }

        [ServerRpc]
        private void SubmitClassicInputServerRpc(Vector2 input)
        {
            _classicInput = input;
        }

        [Replicate]
        private void PerformReplicate(
            MoveData data,
            ReplicateState state = ReplicateState.Invalid,
            Channel channel = Channel.Unreliable)
        {
            SimulateMovement(new Vector2(data.Horizontal, data.Vertical));
        }

        [Reconcile]
        private void PerformReconcile(ReconcileData data, Channel channel = Channel.Unreliable)
        {
            _verticalVelocity = data.VerticalVelocity;

            _characterController.enabled = false;
            transform.position = data.Position;
            _characterController.enabled = true;
        }

        private MoveData BuildMoveData()
        {
            Vector2 input = ReadMoveInput();
            return new MoveData
            {
                Horizontal = input.x,
                Vertical = input.y
            };
        }

        private void SimulateMovement(Vector2 input)
        {
            Vector3 move = new Vector3(input.x, 0f, input.y);
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            move *= _speed;

            _verticalVelocity += _gravity * (float)base.TimeManager.TickDelta;
            if (_characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -1f;
            }

            move.y = _verticalVelocity;
            _characterController.Move(move * (float)base.TimeManager.TickDelta);
        }

        public override void CreateReconcile()
        {
            ReconcileData data = new()
            {
                Position = transform.position,
                VerticalVelocity = _verticalVelocity
            };

            PerformReconcile(data);
        }

        private void ApplyMovementMode()
        {
            _lastPredictionEnabled = ConnectionUI.PredictionEnabled;
            _classicInput = Vector2.zero;

            if (_networkTransform != null)
            {
                _networkTransform.enabled = !_lastPredictionEnabled;
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
