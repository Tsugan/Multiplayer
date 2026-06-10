using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Practice1
{
    public class PlayerNetwork : NetworkBehaviour
    {
        private static readonly HashSet<PlayerNetwork> Players = new HashSet<PlayerNetwork>();

        public static IEnumerable<PlayerNetwork> ActivePlayers => Players;

        public readonly SyncVar<string> Nickname = new SyncVar<string>("Player");
        public readonly SyncVar<int> HP = new SyncVar<int>(100);
        public readonly SyncVar<bool> IsAlive = new SyncVar<bool>(true);
        public readonly SyncVar<int> Score = new SyncVar<int>(0);

        [SerializeField] private int _maxHp = 100;
        [SerializeField] private float _respawnDelay = 3f;
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField] private string _spawnPointTag = "PlayerSpawn";

        private CharacterController _characterController;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private Coroutine _respawnRoutine;
        private bool _isRespawning;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);

            HP.OnChange += OnHpChanged;
            IsAlive.OnChange += OnIsAliveChanged;
        }

        private void OnDestroy()
        {
            HP.OnChange -= OnHpChanged;
            IsAlive.OnChange -= OnIsAliveChanged;
            Players.Remove(this);
        }

        public override void OnStartNetwork()
        {
            Players.Add(this);
            ApplyAliveVisualState(IsAlive.Value);
        }

        public override void OnStopNetwork()
        {
            Players.Remove(this);
        }

        public override void OnStartServer()
        {
            HP.Value = Mathf.Clamp(HP.Value, 0, _maxHp);
            IsAlive.Value = HP.Value > 0;
            MoveToSpawnPoint();
        }

        public override void OnStartClient()
        {
            ApplyAliveVisualState(IsAlive.Value);

            if (base.IsOwner)
            {
                SubmitNicknameServerRpc(ConnectionUI.PlayerNickname);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitNicknameServerRpc(string nickname)
        {
            string safeValue = string.IsNullOrWhiteSpace(nickname)
                ? $"Player_{OwnerId}"
                : nickname.Trim();

            Nickname.Value = safeValue;
            HP.Value = Mathf.Clamp(HP.Value, 0, _maxHp);
            IsAlive.Value = HP.Value > 0;
        }

        private void OnHpChanged(int previous, int next, bool asServer)
        {
            if (!asServer || !base.IsServerInitialized)
            {
                return;
            }

            if (next <= 0 && IsAlive.Value && !_isRespawning)
            {
                IsAlive.Value = false;
                GameManager.Instance?.OnPlayerDowned(this);
                _respawnRoutine = StartCoroutine(RespawnRoutine());
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
            _respawnRoutine = null;
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
                int slot = OwnerId < 0 ? 0 : OwnerId % 8;
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
            if (!base.IsServerInitialized || !IsAlive.Value)
            {
                return;
            }

            HP.Value = Mathf.Clamp(HP.Value + Mathf.Max(0, amount), 0, _maxHp);
        }

        public void ApplyDamageOnServer(int amount, int attackerOwnerId)
        {
            if (!base.IsServerInitialized || !IsAlive.Value || !GameManager.IsGameplayActive)
            {
                return;
            }

            int sanitizedDamage = Mathf.Max(0, amount);
            if (sanitizedDamage <= 0)
            {
                return;
            }

            HP.Value = Mathf.Max(0, HP.Value - sanitizedDamage);
        }

        public void ResetForMatchOnServer(bool resetScore)
        {
            if (!base.IsServerInitialized)
            {
                return;
            }

            if (_respawnRoutine != null)
            {
                StopCoroutine(_respawnRoutine);
                _respawnRoutine = null;
            }

            _isRespawning = false;
            MoveToSpawnPoint();
            HP.Value = _maxHp;
            IsAlive.Value = true;

            if (resetScore)
            {
                Score.Value = 0;
            }

            PlayerShooting shooting = GetComponent<PlayerShooting>();
            if (shooting != null)
            {
                shooting.ResetAmmoOnServer();
            }
        }

        public int MaxHp => _maxHp;
        public bool IsDead => !IsAlive.Value;
        public float RespawnDelay => _respawnDelay;
        public int HpValue => HP.Value;
    }
}
