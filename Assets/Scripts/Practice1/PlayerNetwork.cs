using System.Collections;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Practice1
{
    public class PlayerNetwork : NetworkBehaviour
    {
        private static readonly HashSet<PlayerNetwork> Players = new();

        public static IEnumerable<PlayerNetwork> ActivePlayers => Players;

        public readonly SyncVar<string> Nickname = new("Player");
        public readonly SyncVar<int> HP = new(100);
        public readonly SyncVar<bool> IsAlive = new(true);

        [SerializeField] private int _maxHp = 100;
        [SerializeField] private float _respawnDelay = 3f;
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField] private string _spawnPointTag = "PlayerSpawn";

        private CharacterController _characterController;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private bool _isRespawning;
        private Coroutine _nicknameSyncRoutine;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            Players.Add(this);
            _characterController = GetComponent<CharacterController>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);

            HP.OnChange += OnHpChanged;
            IsAlive.OnChange += OnIsAliveChanged;

            if (base.IsServerInitialized)
            {
                IsAlive.Value = HP.Value > 0;
                MoveToSpawnPoint();
            }

            ApplyAliveVisualState(IsAlive.Value);

            StartNicknameSyncRoutine();
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            StartNicknameSyncRoutine();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            HP.OnChange -= OnHpChanged;
            IsAlive.OnChange -= OnIsAliveChanged;
            Players.Remove(this);

            if (_nicknameSyncRoutine != null)
            {
                StopCoroutine(_nicknameSyncRoutine);
                _nicknameSyncRoutine = null;
            }
        }

        [ServerRpc]
        private void SubmitNicknameServerRpc(string nickname)
        {
            string safeValue = string.IsNullOrWhiteSpace(nickname)
                ? $"Player_{OwnerId}"
                : nickname.Trim();

            Nickname.Value = safeValue;
            HP.Value = Mathf.Clamp(HP.Value, 0, _maxHp);
            IsAlive.Value = HP.Value > 0;
        }

        private void StartNicknameSyncRoutine()
        {
            if (_nicknameSyncRoutine != null)
            {
                return;
            }

            if (base.Owner == null || !base.Owner.IsLocalClient)
            {
                return;
            }

            _nicknameSyncRoutine = StartCoroutine(NicknameSyncRoutine());
        }

        private IEnumerator NicknameSyncRoutine()
        {
            while (IsSpawned && base.Owner != null && base.Owner.IsLocalClient)
            {
                string desiredNickname = ConnectionUI.GetEffectiveNickname();
                if (!string.Equals(Nickname.Value, desiredNickname))
                {
                    SubmitNicknameServerRpc(desiredNickname);
                }

                yield return new WaitForSeconds(0.5f);
            }

            _nicknameSyncRoutine = null;
        }

        private void OnHpChanged(int previous, int next, bool asServer)
        {
            if (!asServer)
            {
                return;
            }

            if (next <= 0 && IsAlive.Value && !_isRespawning)
            {
                IsAlive.Value = false;
                StartCoroutine(RespawnRoutine());
            }
        }

        private void OnIsAliveChanged(bool previous, bool next, bool asServer)
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
                int index = Random.Range(0, sceneSpawnPoints.Length);
                spawnPosition = sceneSpawnPoints[index] != null ? sceneSpawnPoints[index].position : transform.position;
            }
            else
            {
                int slot = Mathf.Abs(OwnerId % 8);
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
            List<Transform> named = new();
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
            if (!base.IsServerInitialized || !IsAlive.Value)
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
