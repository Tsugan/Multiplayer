using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Practice1
{
    [RequireComponent(typeof(PlayerNetwork))]
    public class PlayerShooting : NetworkBehaviour
    {
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private Transform _firePoint;
        [SerializeField] private float _cooldown = 0.4f;
        [SerializeField] private int _maxAmmo = 10;
        [SerializeField] private float _projectileSpeed = 18f;
        [SerializeField] private int _projectileDamage = 20;

        private PlayerNetwork _playerNetwork;
        private float _lastShotServerTime = -999f;

        public NetworkVariable<int> CurrentAmmo = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private void Awake()
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                CurrentAmmo.Value = _maxAmmo;
            }

            _playerNetwork.IsAlive.OnValueChanged += OnIsAliveChanged;
        }

        public override void OnNetworkDespawn()
        {
            _playerNetwork.IsAlive.OnValueChanged -= OnIsAliveChanged;
        }

        private void Update()
        {
            if (!IsOwner || _playerNetwork.IsDead)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TryShoot();
            }
        }

        public void TryShoot()
        {
            if (!IsOwner || _playerNetwork.IsDead)
            {
                return;
            }

            Vector3 shotPosition = _firePoint != null ? _firePoint.position : transform.position + Vector3.up;
            Vector3 shotDirection = _firePoint != null ? _firePoint.forward : transform.forward;
            ShootServerRpc(shotPosition, shotDirection);
        }

        [ServerRpc]
        private void ShootServerRpc(Vector3 shotPosition, Vector3 shotDirection, ServerRpcParams rpc = default)
        {
            if (_playerNetwork.HP.Value <= 0 || !_playerNetwork.IsAlive.Value)
            {
                return;
            }

            if (CurrentAmmo.Value <= 0)
            {
                return;
            }

            if (Time.time < _lastShotServerTime + _cooldown)
            {
                return;
            }

            if (_projectilePrefab == null)
            {
                return;
            }

            Vector3 dir = shotDirection.sqrMagnitude < 0.0001f ? transform.forward : shotDirection.normalized;

            _lastShotServerTime = Time.time;
            CurrentAmmo.Value--;

            GameObject projectile = Instantiate(
                _projectilePrefab,
                shotPosition + dir * 1.2f,
                Quaternion.LookRotation(dir)
            );

            Projectile projectileLogic = projectile.GetComponent<Projectile>();
            if (projectileLogic != null)
            {
                projectileLogic.Configure(_projectileSpeed, _projectileDamage);
            }

            NetworkObject projectileNetworkObject = projectile.GetComponent<NetworkObject>();
            if (projectileNetworkObject != null)
            {
                if (projectileLogic != null)
                {
                    projectileLogic.SetShooterClientId(rpc.Receive.SenderClientId);
                }

                // Keep projectile server-owned so server movement is authoritative.
                projectileNetworkObject.Spawn();
            }
        }

        private void OnIsAliveChanged(bool previous, bool next)
        {
            if (!IsServer)
            {
                return;
            }

            if (!previous && next)
            {
                CurrentAmmo.Value = _maxAmmo;
                _lastShotServerTime = -999f;
            }
        }

        public int MaxAmmo => _maxAmmo;
        public bool HasAmmo => CurrentAmmo.Value > 0;
    }
}
