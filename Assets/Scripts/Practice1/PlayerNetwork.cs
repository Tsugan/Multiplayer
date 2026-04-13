using System.Collections.Generic;
using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Practice1
{
    public class PlayerNetwork : NetworkBehaviour
    {
        private static readonly HashSet<PlayerNetwork> Players = new HashSet<PlayerNetwork>();

        public static IEnumerable<PlayerNetwork> ActivePlayers => Players;

        public NetworkVariable<FixedString32Bytes> Nickname = new NetworkVariable<FixedString32Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> HP = new NetworkVariable<int>(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<bool> IsAlive = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        [SerializeField] private int _maxHp = 100;
        [SerializeField] private float _respawnDelay = 3f;
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField] private string _spawnPointTag = "PlayerSpawn";

        private CharacterController _characterController;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private bool _isRespawning;

        public override void OnNetworkSpawn()
        {
            Players.Add(this);
            _characterController = GetComponent<CharacterController>();
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);

            HP.OnValueChanged += OnHpChanged;
            IsAlive.OnValueChanged += OnIsAliveChanged;

            if (IsServer)
            {
                IsAlive.Value = HP.Value > 0;
            }
            ApplyAliveVisualState(IsAlive.Value);

            if (IsServer)
            {
                MoveToSpawnPoint();
            }

            if (IsOwner)
            {
                SubmitNicknameServerRpc(ConnectionUI.PlayerNickname);
            }
        }

        public override void OnNetworkDespawn()
        {
            HP.OnValueChanged -= OnHpChanged;
            IsAlive.OnValueChanged -= OnIsAliveChanged;
            Players.Remove(this);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitNicknameServerRpc(string nickname)
        {
            string safeValue = string.IsNullOrWhiteSpace(nickname)
                ? $"Player_{OwnerClientId}"
                : nickname.Trim();

            Nickname.Value = safeValue;
            HP.Value = Mathf.Clamp(HP.Value, 0, _maxHp);
            IsAlive.Value = HP.Value > 0;
        }

        private void OnHpChanged(int previous, int next)
        {
            if (!IsServer)
            {
                return;
            }

            if (next <= 0 && IsAlive.Value && !_isRespawning)
            {
                IsAlive.Value = false;
                StartCoroutine(RespawnRoutine());
            }
        }

        private void OnIsAliveChanged(bool previous, bool next)
        {
            ApplyAliveVisualState(next);
        }

        private IEnumerator RespawnRoutine()
        {
            _isRespawning = true;
            yield return new WaitForSeconds(_respawnDelay);

            MoveToSpawnPoint();
            HP.Value = _maxHp;
            IsAlive.Value = true;
            _isRespawning = false;
        }

        private void MoveToSpawnPoint()
        {
            Vector3 spawnPosition;
            Transform[] sceneSpawnPoints = GetSceneSpawnPoints();
            if (sceneSpawnPoints != null && sceneSpawnPoints.Length > 0)
            {
                int idx = Random.Range(0, sceneSpawnPoints.Length);
                spawnPosition = sceneSpawnPoints[idx] != null ? sceneSpawnPoints[idx].position : transform.position;
            }
            else
            {
                int slot = (int)(OwnerClientId % 8);
                spawnPosition = new Vector3(-7f + slot * 2f, 1f, 0f);
            }

            if (_characterController != null)
            {
                _characterController.enabled = false;
            }

            transform.position = spawnPosition;

            if (_characterController != null)
            {
                _characterController.enabled = true;
            }
        }

        private Transform[] GetSceneSpawnPoints()
        {
            if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                return _spawnPoints;
            }

            if (!string.IsNullOrWhiteSpace(_spawnPointTag))
            {
                GameObject[] tagged = GameObject.FindGameObjectsWithTag(_spawnPointTag);
                if (tagged != null && tagged.Length > 0)
                {
                    Transform[] result = new Transform[tagged.Length];
                    for (int i = 0; i < tagged.Length; i++)
                    {
                        result[i] = tagged[i].transform;
                    }

                    return result;
                }
            }

            GameObject[] all = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            List<Transform> named = new List<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i];
                if (go != null && go.name.StartsWith("PlayerSpawn"))
                {
                    named.Add(go.transform);
                }
            }

            return named.ToArray();
        }

        private void ApplyAliveVisualState(bool alive)
        {
            if (_renderers != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null)
                    {
                        _renderers[i].enabled = alive;
                    }
                }
            }

            if (_colliders != null)
            {
                for (int i = 0; i < _colliders.Length; i++)
                {
                    if (_colliders[i] != null && _colliders[i].GetComponent<NetworkObject>() == null)
                    {
                        _colliders[i].enabled = alive;
                    }
                }
            }
        }

        public void HealOnServer(int amount)
        {
            if (!IsServer || !IsAlive.Value)
            {
                return;
            }

            HP.Value = Mathf.Clamp(HP.Value + Mathf.Max(0, amount), 0, _maxHp);
        }

        public int MaxHp => _maxHp;
        public bool IsDead => !IsAlive.Value;
        public float RespawnDelay => _respawnDelay;
        public int HpValue => HP.Value;
    }
}
