using System.Collections.Generic;
using System.Text;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Practice1
{
    public enum GameState
    {
        WaitingForPlayers,
        InProgress,
        ShowingResults
    }

    public enum BombPhase
    {
        Waiting,
        Carried,
        Dropped,
        Respawning
    }

    public struct GameStateBroadcast : IBroadcast
    {
        public int State;
        public int ConnectedPlayers;
        public int RequiredPlayers;
        public float MatchTimeLeft;
        public float StartCountdown;
        public float ResultsTimeLeft;
        public string ResultsText;
        public int BombPhase;
        public int BombCarrierOwnerId;
        public string BombCarrierName;
        public Vector3 BombPosition;
        public Vector3 DisposalZonePosition;
        public float BombFuseLeft;
        public float BombRespawnLeft;
        public string ObjectiveText;
        public int BombEventId;
        public int BombEventType;
    }

    public class GameManager : MonoBehaviour
    {
        private const int ObjectiveSpawnAttempts = 64;
        private const int ObjectiveFallbackDirections = 32;

        public static GameManager Instance { get; private set; }

        [Header("Session")]
        [SerializeField] private int _requiredPlayers = 2;
        [SerializeField] private float _matchDuration = 120f;
        [SerializeField] private float _lobbyStartDelay = 3f;
        [SerializeField] private float _resultsDuration = 8f;
        [SerializeField] private float _broadcastInterval = 0.25f;

        [Header("Bomb Disposal")]
        [SerializeField] private Vector3 _bombSpawnPosition = new Vector3(0f, 1f, 0f);
        [SerializeField] private Vector3 _disposalZonePosition = new Vector3(0f, 0.05f, 8f);
        [SerializeField] private Vector3 _randomObjectiveCenter = new Vector3(0f, 0f, 1f);
        [SerializeField] private float _randomObjectiveRadius = 7f;
        [SerializeField] private float _minimumObjectiveDistance = 6f;
        [SerializeField] private float _objectiveColliderPadding;
        [SerializeField] private float _bombObjectiveClearance = 1.2f;
        [SerializeField] private float _disposalObjectiveClearance = 2.2f;
        [SerializeField] private string[] _objectiveBlockedColliderNames =
        {
            "Cover_A",
            "Cover_B",
            "Cover_C",
            "Cover_D"
        };
        [SerializeField] private float _bombFuseDuration = 10f;
        [SerializeField] private float _bombRespawnDelay = 4f;
        [SerializeField] private float _pickupRadius = 2f;
        [SerializeField] private float _disposalRadius = 2.4f;
        [SerializeField] private float _explosionRadius = 4f;
        [SerializeField] private int _explosionDamage = 100;
        [SerializeField] private int _carryScorePerTick = 1;
        [SerializeField] private float _carryScoreInterval = 1f;
        [SerializeField] private int _disposalScore = 15;

        private NetworkManager _networkManager;
        private bool _clientBroadcastRegistered;
        private bool _serverConnectionRegistered;
        private float _broadcastTimer;
        private float _carryScoreTimer;
        private int _bombCarrierOwnerId = -1;
        private int _bombEventId;
        private int _bombEventType;
        private Collider[] _objectiveBlockedColliders = new Collider[0];

        public GameState CurrentState { get; private set; } = GameState.WaitingForPlayers;
        public int ConnectedPlayers { get; private set; }
        public int RequiredPlayers => Mathf.Max(1, _requiredPlayers);
        public float MatchTimeLeft { get; private set; }
        public float StartCountdown { get; private set; }
        public float ResultsTimeLeft { get; private set; }
        public string ResultsText { get; private set; } = string.Empty;
        public BombPhase CurrentBombPhase { get; private set; } = BombPhase.Waiting;
        public int BombCarrierOwnerId => _bombCarrierOwnerId;
        public string BombCarrierName { get; private set; } = string.Empty;
        public Vector3 BombPosition { get; private set; }
        public Vector3 DisposalZonePosition => _disposalZonePosition;
        public float BombFuseLeft { get; private set; }
        public float BombRespawnLeft { get; private set; }
        public string ObjectiveText { get; private set; } = "Pick up the bomb and dispose it.";
        public int BombEventId => _bombEventId;
        public int BombEventType => _bombEventType;

        public static bool IsGameplayActive =>
            Instance == null || Instance.CurrentState == GameState.InProgress;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _matchDuration = 120f;
            MatchTimeLeft = _matchDuration;
            StartCountdown = _lobbyStartDelay;
            BombPosition = _bombSpawnPosition;
            ObjectiveText = "Wait for the match to start.";
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
            {
                if (_clientBroadcastRegistered)
                {
                    _networkManager.ClientManager.UnregisterBroadcast<GameStateBroadcast>(OnGameStateBroadcast);
                }

                if (_serverConnectionRegistered)
                {
                    _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
                }
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            BindNetworkManager();

            if (!InstanceFinder.IsServerStarted)
            {
                return;
            }

            TickServerGameLoop();
            BroadcastStateIfNeeded();
        }

        public void AddScoreForClient(int clientId, int amount)
        {
            if (!InstanceFinder.IsServerStarted || CurrentState != GameState.InProgress)
            {
                return;
            }

            PlayerNetwork scorer = FindPlayerByOwnerId(clientId);
            if (scorer == null)
            {
                return;
            }

            scorer.Score.Value += Mathf.Max(0, amount);
        }

        public bool IsBombCarrier(PlayerNetwork player)
        {
            return player != null && CurrentBombPhase == BombPhase.Carried && player.OwnerId == _bombCarrierOwnerId;
        }

        public void TryPickupBomb(PlayerNetwork player)
        {
            if (!CanUseBomb(player) || CurrentBombPhase == BombPhase.Carried || CurrentBombPhase == BombPhase.Respawning)
            {
                return;
            }

            if (Vector3.Distance(player.transform.position, BombPosition) > _pickupRadius)
            {
                return;
            }

            _bombCarrierOwnerId = player.OwnerId;
            BombCarrierName = GetPlayerName(player);
            CurrentBombPhase = BombPhase.Carried;
            BombFuseLeft = BombFuseLeft > 0f ? BombFuseLeft : _bombFuseDuration;
            BombRespawnLeft = 0f;
            _carryScoreTimer = 0f;
            ObjectiveText = $"{BombCarrierName} is carrying the bomb. Dispose it in {BombFuseLeft:0.0}s.";
            RegisterBombEvent(1);
            BroadcastState();
        }

        public void TryThrowBomb(PlayerNetwork player)
        {
            if (!CanUseBomb(player) || !IsBombCarrier(player))
            {
                return;
            }

            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }

            BombPosition = player.transform.position + forward.normalized * 2.2f + Vector3.up * 0.4f;
            _bombCarrierOwnerId = -1;
            BombCarrierName = string.Empty;
            CurrentBombPhase = BombPhase.Dropped;
            ObjectiveText = $"Bomb dropped. Fuse: {BombFuseLeft:0.0}s.";
            RegisterBombEvent(2);
            BroadcastState();
        }

        public void TryDisposeBomb(PlayerNetwork player)
        {
            if (!CanUseBomb(player) || !IsBombCarrier(player))
            {
                return;
            }

            if (Vector3.Distance(player.transform.position, _disposalZonePosition) > _disposalRadius)
            {
                return;
            }

            AddScoreForClient(player.OwnerId, _disposalScore);
            StartBombRespawn($"{GetPlayerName(player)} disposed the bomb (+{_disposalScore}).", 3);
        }

        public void ExplodeBombFromHit(PlayerNetwork target, int attackerOwnerId)
        {
            if (!InstanceFinder.IsServerStarted || CurrentState != GameState.InProgress || !IsBombCarrier(target))
            {
                return;
            }

            ExplodeBomb(target.transform.position, $"{GetPlayerName(target)} was hit. Bomb exploded.");
        }

        public void OnPlayerDowned(PlayerNetwork player)
        {
            if (!InstanceFinder.IsServerStarted || CurrentState != GameState.InProgress || !IsBombCarrier(player))
            {
                return;
            }

            ExplodeBomb(player.transform.position, $"{GetPlayerName(player)} went down with the bomb.");
        }

        private void BindNetworkManager()
        {
            if (_networkManager == null)
            {
                _networkManager = InstanceFinder.NetworkManager;
            }

            if (_networkManager == null)
            {
                return;
            }

            if (!_clientBroadcastRegistered)
            {
                _networkManager.ClientManager.RegisterBroadcast<GameStateBroadcast>(OnGameStateBroadcast);
                _clientBroadcastRegistered = true;
            }

            if (!_serverConnectionRegistered)
            {
                _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
                _serverConnectionRegistered = true;
            }
        }

        private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (!InstanceFinder.IsServerStarted)
            {
                return;
            }

            RefreshConnectedPlayers();
            if (_bombCarrierOwnerId >= 0 && FindPlayerByOwnerId(_bombCarrierOwnerId) == null)
            {
                StartBombRespawn("Bomb carrier disconnected.", 4);
            }

            BroadcastState();
        }

        private void TickServerGameLoop()
        {
            RefreshConnectedPlayers();

            switch (CurrentState)
            {
                case GameState.WaitingForPlayers:
                    TickLobby();
                    break;
                case GameState.InProgress:
                    TickMatch();
                    TickBomb();
                    break;
                case GameState.ShowingResults:
                    TickResults();
                    break;
            }
        }

        private void TickLobby()
        {
            MatchTimeLeft = _matchDuration;
            ResultsTimeLeft = 0f;
            ResetBombToWaiting("Wait for the match to start.");

            if (ConnectedPlayers < RequiredPlayers)
            {
                StartCountdown = _lobbyStartDelay;
                return;
            }

            StartCountdown = Mathf.Max(0f, StartCountdown - Time.deltaTime);
            if (StartCountdown <= 0f)
            {
                StartMatch();
            }
        }

        private void TickMatch()
        {
            MatchTimeLeft = Mathf.Max(0f, MatchTimeLeft - Time.deltaTime);
            if (MatchTimeLeft <= 0f)
            {
                EndMatch();
            }
        }

        private void TickBomb()
        {
            switch (CurrentBombPhase)
            {
                case BombPhase.Carried:
                    TickCarriedBomb();
                    break;
                case BombPhase.Dropped:
                    TickDroppedBomb();
                    break;
                case BombPhase.Respawning:
                    BombRespawnLeft = Mathf.Max(0f, BombRespawnLeft - Time.deltaTime);
                    if (BombRespawnLeft <= 0f)
                    {
                        ResetBombToWaiting("Bomb respawned. Pick it up and dispose it.", randomizeObjectivePositions: true);
                        RegisterBombEvent(5);
                    }
                    break;
                default:
                    ObjectiveText = "Pick up the bomb and dispose it.";
                    break;
            }
        }

        private void TickCarriedBomb()
        {
            PlayerNetwork carrier = FindPlayerByOwnerId(_bombCarrierOwnerId);
            if (carrier == null || !carrier.IsAlive.Value)
            {
                ExplodeBomb(BombPosition, "Bomb carrier lost.");
                return;
            }

            BombPosition = carrier.transform.position + Vector3.up * 1.25f;
            BombFuseLeft = Mathf.Max(0f, BombFuseLeft - Time.deltaTime);
            _carryScoreTimer += Time.deltaTime;

            if (_carryScoreTimer >= _carryScoreInterval)
            {
                int ticks = Mathf.FloorToInt(_carryScoreTimer / _carryScoreInterval);
                _carryScoreTimer -= ticks * _carryScoreInterval;
                AddScoreForClient(carrier.OwnerId, ticks * _carryScorePerTick);
            }

            ObjectiveText = $"{BombCarrierName}: carry score +{_carryScorePerTick}/s, fuse {BombFuseLeft:0.0}s.";
            if (BombFuseLeft <= 0f)
            {
                ExplodeBomb(carrier.transform.position, "Fuse expired. Bomb exploded.");
            }
        }

        private void TickDroppedBomb()
        {
            BombFuseLeft = Mathf.Max(0f, BombFuseLeft - Time.deltaTime);
            ObjectiveText = $"Bomb is dropped. Fuse {BombFuseLeft:0.0}s.";
            if (BombFuseLeft <= 0f)
            {
                ExplodeBomb(BombPosition, "Dropped bomb exploded.");
            }
        }

        private void TickResults()
        {
            ResultsTimeLeft = Mathf.Max(0f, ResultsTimeLeft - Time.deltaTime);
            if (ResultsTimeLeft <= 0f)
            {
                ResetToLobby();
            }
        }

        private void StartMatch()
        {
            ResetPlayersForRound(resetScore: true);
            MatchTimeLeft = _matchDuration;
            ResultsText = string.Empty;
            ResetBombToWaiting("Pick up the bomb and bring it to the disposal zone.", randomizeObjectivePositions: true);
            CurrentState = GameState.InProgress;
            Debug.Log("[Server] Bomb Disposal match started.");
            BroadcastState();
        }

        private void EndMatch()
        {
            if (CurrentState != GameState.InProgress)
            {
                return;
            }

            StartBombRespawn("Match ended.", 0);
            ResultsText = BuildResultsText();
            ResultsTimeLeft = _resultsDuration;
            CurrentState = GameState.ShowingResults;
            Debug.Log("[Server] Match ended. Showing results.");
            BroadcastState();
        }

        private void ResetToLobby()
        {
            ResetPlayersForRound(resetScore: true);
            MatchTimeLeft = _matchDuration;
            StartCountdown = _lobbyStartDelay;
            ResultsTimeLeft = 0f;
            CurrentState = GameState.WaitingForPlayers;
            ResetBombToWaiting("Wait for the match to start.");
            Debug.Log("[Server] Lobby reset. Waiting for players.");
            BroadcastState();
        }

        private void ResetPlayersForRound(bool resetScore)
        {
            int spawnSlot = 0;
            foreach (PlayerNetwork player in PlayerNetwork.ActivePlayers)
            {
                if (player != null && player.IsSpawned && player.IsServerInitialized)
                {
                    player.ResetForMatchOnServer(resetScore, spawnSlot);
                    spawnSlot++;
                }
            }
        }

        private void ResetBombToWaiting(string objectiveText, bool randomizeObjectivePositions = false)
        {
            CurrentBombPhase = BombPhase.Waiting;
            _bombCarrierOwnerId = -1;
            BombCarrierName = string.Empty;
            if (randomizeObjectivePositions)
            {
                RandomizeObjectivePositions();
            }

            BombPosition = _bombSpawnPosition;
            BombFuseLeft = 0f;
            BombRespawnLeft = 0f;
            _carryScoreTimer = 0f;
            ObjectiveText = objectiveText;
        }

        private void RandomizeObjectivePositions()
        {
            float radius = Mathf.Max(0.1f, _randomObjectiveRadius);
            float minDistance = Mathf.Clamp(_minimumObjectiveDistance, 0f, radius * 2f);
            float bombClearance = Mathf.Max(1.2f, _bombObjectiveClearance);
            float disposalClearance = Mathf.Max(2.2f, _disposalObjectiveClearance);
            Vector3 bombPosition = ToBombHeight(RandomValidObjectivePoint(radius, bombClearance));
            Vector3 disposalPosition = ToDisposalHeight(RandomValidDistantObjectivePoint(
                radius,
                bombPosition,
                minDistance,
                disposalClearance
            ));

            _bombSpawnPosition = bombPosition;
            _disposalZonePosition = disposalPosition;
        }

        private Vector3 RandomValidObjectivePoint(float radius, float clearance)
        {
            for (int i = 0; i < ObjectiveSpawnAttempts; i++)
            {
                Vector3 candidate = RandomPointInObjectiveCircle(radius);
                if (!IsObjectivePointBlocked(candidate, clearance))
                {
                    return candidate;
                }
            }

            return FindBestFallbackObjectivePoint(radius, _randomObjectiveCenter, minDistance: 0f, clearance);
        }

        private Vector3 RandomValidDistantObjectivePoint(
            float radius,
            Vector3 otherPoint,
            float minDistance,
            float clearance)
        {
            Vector3 bestPoint = otherPoint;
            float bestDistance = -1f;

            for (int i = 0; i < ObjectiveSpawnAttempts; i++)
            {
                Vector3 candidate = RandomPointInObjectiveCircle(radius);
                if (IsObjectivePointBlocked(candidate, clearance))
                {
                    continue;
                }

                float distance = DistanceXZ(otherPoint, candidate);
                if (distance > bestDistance)
                {
                    bestPoint = candidate;
                    bestDistance = distance;
                }

                if (distance >= minDistance)
                {
                    return candidate;
                }
            }

            if (bestDistance >= minDistance)
            {
                return bestPoint;
            }

            return FindBestFallbackObjectivePoint(radius, otherPoint, minDistance, clearance);
        }

        private Vector3 FindBestFallbackObjectivePoint(
            float radius,
            Vector3 otherPoint,
            float minDistance,
            float clearance)
        {
            Vector3 bestPoint = _randomObjectiveCenter;
            float bestDistance = -1f;

            for (int ring = 1; ring <= 4; ring++)
            {
                float ringRadius = radius * ring / 4f;
                for (int directionIndex = 0; directionIndex < ObjectiveFallbackDirections; directionIndex++)
                {
                    float angle = Mathf.PI * 2f * directionIndex / ObjectiveFallbackDirections;
                    Vector3 candidate = new Vector3(
                        _randomObjectiveCenter.x + Mathf.Cos(angle) * ringRadius,
                        _randomObjectiveCenter.y,
                        _randomObjectiveCenter.z + Mathf.Sin(angle) * ringRadius
                    );

                    if (IsObjectivePointBlocked(candidate, clearance))
                    {
                        continue;
                    }

                    float distance = DistanceXZ(otherPoint, candidate);
                    if (distance > bestDistance)
                    {
                        bestPoint = candidate;
                        bestDistance = distance;
                    }

                    if (distance >= minDistance)
                    {
                        return candidate;
                    }
                }
            }

            return bestDistance >= 0f ? bestPoint : _randomObjectiveCenter;
        }

        private Vector3 RandomPointInObjectiveCircle(float radius)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            return new Vector3(
                _randomObjectiveCenter.x + offset.x,
                _randomObjectiveCenter.y,
                _randomObjectiveCenter.z + offset.y
            );
        }

        private void RefreshObjectiveBlockedColliders()
        {
            List<Collider> colliders = new List<Collider>();
            AddNamedObjectiveBlockerColliders(colliders);

            if (colliders.Count == 0)
            {
                Collider[] sceneColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
                for (int i = 0; i < sceneColliders.Length; i++)
                {
                    Collider sceneCollider = sceneColliders[i];
                    if (sceneCollider != null && IsObjectiveBlockerCollider(sceneCollider))
                    {
                        colliders.Add(sceneCollider);
                    }
                }
            }

            _objectiveBlockedColliders = colliders.ToArray();
        }

        private void AddNamedObjectiveBlockerColliders(List<Collider> colliders)
        {
            if (_objectiveBlockedColliderNames == null)
            {
                return;
            }

            for (int i = 0; i < _objectiveBlockedColliderNames.Length; i++)
            {
                string objectName = _objectiveBlockedColliderNames[i];
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
        }

        private bool IsObjectivePointBlocked(Vector3 point, float clearance)
        {
            if (_objectiveBlockedColliders == null || _objectiveBlockedColliders.Length == 0)
            {
                RefreshObjectiveBlockedColliders();
            }

            float padding = Mathf.Max(0f, _objectiveColliderPadding) + Mathf.Max(0f, clearance);
            bool shouldRefresh = false;
            for (int i = 0; i < _objectiveBlockedColliders.Length; i++)
            {
                Collider blockedCollider = _objectiveBlockedColliders[i];
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

            if (IsObjectivePointBlockedByPhysics(point, padding))
            {
                return true;
            }

            if (shouldRefresh)
            {
                RefreshObjectiveBlockedColliders();
            }

            return false;
        }

        private bool IsObjectivePointBlockedByPhysics(Vector3 point, float radius)
        {
            if (radius <= 0f)
            {
                return false;
            }

            Collider[] overlaps = Physics.OverlapSphere(point + Vector3.up * 0.55f, radius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider overlap = overlaps[i];
                if (overlap != null && IsObjectiveBlockerCollider(overlap))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsObjectiveBlockerCollider(Collider candidate)
        {
            Transform current = candidate.transform;
            while (current != null)
            {
                if (IsObjectiveBlockerName(current.name))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private bool IsObjectiveBlockerName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            if (_objectiveBlockedColliderNames != null)
            {
                for (int i = 0; i < _objectiveBlockedColliderNames.Length; i++)
                {
                    if (objectName == _objectiveBlockedColliderNames[i])
                    {
                        return true;
                    }
                }
            }

            return objectName.StartsWith("Cover_");
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

        private static Vector3 ToBombHeight(Vector3 position)
        {
            position.y = 1f;
            return position;
        }

        private static Vector3 ToDisposalHeight(Vector3 position)
        {
            position.y = 0.05f;
            return position;
        }

        private static float DistanceXZ(Vector3 first, Vector3 second)
        {
            float dx = first.x - second.x;
            float dz = first.z - second.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private void StartBombRespawn(string objectiveText, int eventType)
        {
            CurrentBombPhase = BombPhase.Respawning;
            _bombCarrierOwnerId = -1;
            BombCarrierName = string.Empty;
            BombFuseLeft = 0f;
            BombRespawnLeft = _bombRespawnDelay;
            _carryScoreTimer = 0f;
            ObjectiveText = objectiveText;
            RegisterBombEvent(eventType);
            BroadcastState();
        }

        private void ExplodeBomb(Vector3 explosionPosition, string objectiveText)
        {
            BombPosition = explosionPosition;
            CurrentBombPhase = BombPhase.Respawning;
            _bombCarrierOwnerId = -1;
            BombCarrierName = string.Empty;
            BombFuseLeft = 0f;
            BombRespawnLeft = _bombRespawnDelay;
            _carryScoreTimer = 0f;
            ObjectiveText = objectiveText;
            RegisterBombEvent(4);

            foreach (PlayerNetwork player in PlayerNetwork.ActivePlayers)
            {
                if (player == null || !player.IsServerInitialized || !player.IsAlive.Value)
                {
                    continue;
                }

                if (Vector3.Distance(player.transform.position, explosionPosition) <= _explosionRadius)
                {
                    player.ApplyDamageOnServer(_explosionDamage, -1);
                }
            }

            BroadcastState();
        }

        private bool CanUseBomb(PlayerNetwork player)
        {
            return InstanceFinder.IsServerStarted &&
                   CurrentState == GameState.InProgress &&
                   player != null &&
                   player.IsServerInitialized &&
                   player.IsAlive.Value;
        }

        private void RefreshConnectedPlayers()
        {
            int count = 0;
            foreach (PlayerNetwork player in PlayerNetwork.ActivePlayers)
            {
                if (player != null && player.IsSpawned && player.IsServerInitialized)
                {
                    count++;
                }
            }

            if (count == 0 && _networkManager != null && _networkManager.ServerManager != null)
            {
                count = _networkManager.ServerManager.Clients.Count;
            }

            ConnectedPlayers = count;
        }

        private PlayerNetwork FindPlayerByOwnerId(int ownerId)
        {
            foreach (PlayerNetwork player in PlayerNetwork.ActivePlayers)
            {
                if (player != null && player.IsServerInitialized && player.OwnerId == ownerId)
                {
                    return player;
                }
            }

            return null;
        }

        private static string GetPlayerName(PlayerNetwork player)
        {
            if (player == null)
            {
                return "Player";
            }

            return string.IsNullOrWhiteSpace(player.Nickname.Value)
                ? $"Player_{player.OwnerId}"
                : player.Nickname.Value;
        }

        private string BuildResultsText()
        {
            StringBuilder builder = new StringBuilder();
            PlayerNetwork winner = null;

            foreach (PlayerNetwork player in PlayerNetwork.ActivePlayers)
            {
                if (player == null || !player.IsServerInitialized)
                {
                    continue;
                }

                if (winner == null || player.Score.Value > winner.Score.Value)
                {
                    winner = player;
                }

                builder.AppendLine($"{GetPlayerName(player)}: {player.Score.Value}");
            }

            if (builder.Length == 0)
            {
                builder.AppendLine("No connected players.");
            }
            else if (winner != null)
            {
                builder.Insert(0, $"Winner: {GetPlayerName(winner)}\n");
            }

            return builder.ToString().TrimEnd();
        }

        private void RegisterBombEvent(int eventType)
        {
            if (eventType <= 0)
            {
                return;
            }

            _bombEventId++;
            _bombEventType = eventType;
        }

        private void BroadcastStateIfNeeded()
        {
            _broadcastTimer -= Time.deltaTime;
            if (_broadcastTimer > 0f)
            {
                return;
            }

            _broadcastTimer = Mathf.Max(0.05f, _broadcastInterval);
            BroadcastState();
        }

        private void BroadcastState()
        {
            if (_networkManager == null || !InstanceFinder.IsServerStarted)
            {
                return;
            }

            GameStateBroadcast message = new GameStateBroadcast
            {
                State = (int)CurrentState,
                ConnectedPlayers = ConnectedPlayers,
                RequiredPlayers = RequiredPlayers,
                MatchTimeLeft = MatchTimeLeft,
                StartCountdown = StartCountdown,
                ResultsTimeLeft = ResultsTimeLeft,
                ResultsText = ResultsText,
                BombPhase = (int)CurrentBombPhase,
                BombCarrierOwnerId = _bombCarrierOwnerId,
                BombCarrierName = BombCarrierName,
                BombPosition = BombPosition,
                DisposalZonePosition = _disposalZonePosition,
                BombFuseLeft = BombFuseLeft,
                BombRespawnLeft = BombRespawnLeft,
                ObjectiveText = ObjectiveText,
                BombEventId = _bombEventId,
                BombEventType = _bombEventType
            };

            ApplyBroadcast(message);
            _networkManager.ServerManager.Broadcast(message, requireAuthenticated: false);
        }

        private void OnGameStateBroadcast(GameStateBroadcast message, Channel channel)
        {
            ApplyBroadcast(message);
        }

        private void ApplyBroadcast(GameStateBroadcast message)
        {
            CurrentState = (GameState)message.State;
            ConnectedPlayers = message.ConnectedPlayers;
            MatchTimeLeft = message.MatchTimeLeft;
            StartCountdown = message.StartCountdown;
            ResultsTimeLeft = message.ResultsTimeLeft;
            ResultsText = message.ResultsText ?? string.Empty;
            _requiredPlayers = Mathf.Max(1, message.RequiredPlayers);
            CurrentBombPhase = (BombPhase)message.BombPhase;
            _bombCarrierOwnerId = message.BombCarrierOwnerId;
            BombCarrierName = message.BombCarrierName ?? string.Empty;
            BombPosition = message.BombPosition;
            _disposalZonePosition = message.DisposalZonePosition;
            BombFuseLeft = message.BombFuseLeft;
            BombRespawnLeft = message.BombRespawnLeft;
            ObjectiveText = message.ObjectiveText ?? string.Empty;
            _bombEventId = message.BombEventId;
            _bombEventType = message.BombEventType;
        }
    }
}
