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
        [SerializeField] private float _spawnBlockerPadding = 0.75f;
        [SerializeField] private string[] _spawnBlockedColliderNames =
        {
            "Cover_A",
            "Cover_B",
            "Cover_C",
            "Cover_D"
        };

        private CharacterController _characterController;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private Collider[] _spawnBlockedColliders = new Collider[0];
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
            MoveToSpawnPoint(spawnSlot: -1);
        }

        private void MoveToSpawnPoint(int spawnSlot)
        {
            Transform[] sceneSpawnPoints = GetSceneSpawnPoints();
            Vector3 spawnPosition = ResolveSpawnPosition(sceneSpawnPoints, spawnSlot);

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

        private Vector3 ResolveSpawnPosition(Transform[] sceneSpawnPoints, int spawnSlot)
        {
            if (sceneSpawnPoints != null && sceneSpawnPoints.Length > 0)
            {
                int startIndex = spawnSlot >= 0
                    ? spawnSlot % sceneSpawnPoints.Length
                    : Random.Range(0, sceneSpawnPoints.Length);

                for (int offset = 0; offset < sceneSpawnPoints.Length; offset++)
                {
                    int index = (startIndex + offset) % sceneSpawnPoints.Length;
                    Transform spawnPoint = sceneSpawnPoints[index];
                    if (spawnPoint == null)
                    {
                        continue;
                    }

                    Vector3 candidate = GetSafeSpawnPosition(spawnPoint.position);
                    if (!IsSpawnPointBlocked(candidate))
                    {
                        return candidate;
                    }
                }
            }

            Vector3 fallback = GetFallbackSpawnPosition(spawnSlot);
            if (!IsSpawnPointBlocked(fallback))
            {
                return fallback;
            }

            return FindOpenFallbackSpawnPosition(fallback);
        }

        private Vector3 GetFallbackSpawnPosition(int spawnSlot)
        {
            int slot = spawnSlot >= 0 ? spawnSlot : OwnerId < 0 ? 0 : OwnerId % 8;
            return GetSafeSpawnPosition(new Vector3(-7f + slot * 2f, 1f, 0f));
        }

        private Vector3 FindOpenFallbackSpawnPosition(Vector3 fallback)
        {
            const int directions = 16;
            const int rings = 5;

            for (int ring = 1; ring <= rings; ring++)
            {
                float radius = ring * 1.5f;
                for (int i = 0; i < directions; i++)
                {
                    float angle = Mathf.PI * 2f * i / directions;
                    Vector3 candidate = new Vector3(
                        fallback.x + Mathf.Cos(angle) * radius,
                        fallback.y,
                        fallback.z + Mathf.Sin(angle) * radius
                    );

                    if (!IsSpawnPointBlocked(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return fallback;
        }

        private Vector3 GetSafeSpawnPosition(Vector3 spawnPosition)
        {
            if (_characterController == null)
            {
                return spawnPosition;
            }

            float controllerBottomOffset = _characterController.height * 0.5f - _characterController.center.y;
            spawnPosition.y = Mathf.Max(spawnPosition.y, controllerBottomOffset + 0.05f);
            return spawnPosition;
        }

        private void RefreshSpawnBlockedColliders()
        {
            if (_spawnBlockedColliderNames == null || _spawnBlockedColliderNames.Length == 0)
            {
                _spawnBlockedColliders = new Collider[0];
                return;
            }

            List<Collider> colliders = new List<Collider>();
            for (int i = 0; i < _spawnBlockedColliderNames.Length; i++)
            {
                string objectName = _spawnBlockedColliderNames[i];
                if (string.IsNullOrWhiteSpace(objectName))
                {
                    continue;
                }

                GameObject blockedObject = GameObject.Find(objectName);
                if (blockedObject == null)
                {
                    continue;
                }

                blockedObject.GetComponentsInChildren(includeInactive: true, colliders);
            }

            _spawnBlockedColliders = colliders.ToArray();
        }

        private bool IsSpawnPointBlocked(Vector3 point)
        {
            if (_spawnBlockedColliders == null || _spawnBlockedColliders.Length == 0)
            {
                RefreshSpawnBlockedColliders();
            }

            float padding = Mathf.Max(0f, _spawnBlockerPadding);
            bool shouldRefresh = false;
            for (int i = 0; i < _spawnBlockedColliders.Length; i++)
            {
                Collider blockedCollider = _spawnBlockedColliders[i];
                if (blockedCollider == null)
                {
                    shouldRefresh = true;
                    continue;
                }

                if (!blockedCollider.enabled || !blockedCollider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (IsPointInsideColliderFootprint(blockedCollider, point, padding))
                {
                    return true;
                }
            }

            if (shouldRefresh)
            {
                RefreshSpawnBlockedColliders();
            }

            return false;
        }

        private static bool IsPointInsideColliderFootprint(Collider blockedCollider, Vector3 point, float padding)
        {
            if (blockedCollider is BoxCollider boxCollider)
            {
                Vector3 localPoint = boxCollider.transform.InverseTransformPoint(point) - boxCollider.center;
                Vector3 scale = boxCollider.transform.lossyScale;
                float localPaddingX = padding / Mathf.Max(Mathf.Abs(scale.x), 0.001f);
                float localPaddingZ = padding / Mathf.Max(Mathf.Abs(scale.z), 0.001f);
                return Mathf.Abs(localPoint.x) <= boxCollider.size.x * 0.5f + localPaddingX &&
                       Mathf.Abs(localPoint.z) <= boxCollider.size.z * 0.5f + localPaddingZ;
            }

            Bounds bounds = blockedCollider.bounds;
            return point.x >= bounds.min.x - padding &&
                   point.x <= bounds.max.x + padding &&
                   point.z >= bounds.min.z - padding &&
                   point.z <= bounds.max.z + padding;
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

                    SortSpawnPoints(result);
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

            Transform[] namedResult = named.ToArray();
            SortSpawnPoints(namedResult);
            return namedResult;
        }

        private static void SortSpawnPoints(Transform[] spawnPoints)
        {
            if (spawnPoints == null)
            {
                return;
            }

            System.Array.Sort(spawnPoints, (a, b) =>
            {
                string first = a != null ? a.name : string.Empty;
                string second = b != null ? b.name : string.Empty;
                return string.CompareOrdinal(first, second);
            });
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
            ResetForMatchOnServer(resetScore, spawnSlot: -1);
        }

        public void ResetForMatchOnServer(bool resetScore, int spawnSlot)
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
            MoveToSpawnPoint(spawnSlot);
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
