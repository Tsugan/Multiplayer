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

    public struct GameStateBroadcast : IBroadcast
    {
        public int State;
        public int ConnectedPlayers;
        public int RequiredPlayers;
        public float MatchTimeLeft;
        public float StartCountdown;
        public float ResultsTimeLeft;
        public string ResultsText;
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private int _requiredPlayers = 2;
        [SerializeField] private float _matchDuration = 60f;
        [SerializeField] private float _lobbyStartDelay = 3f;
        [SerializeField] private float _resultsDuration = 5f;
        [SerializeField] private int _scoreToWin = 3;
        [SerializeField] private float _broadcastInterval = 0.25f;

        private NetworkManager _networkManager;
        private bool _clientBroadcastRegistered;
        private bool _serverConnectionRegistered;
        private float _broadcastTimer;

        public GameState CurrentState { get; private set; } = GameState.WaitingForPlayers;
        public int ConnectedPlayers { get; private set; }
        public int RequiredPlayers => Mathf.Max(1, _requiredPlayers);
        public float MatchTimeLeft { get; private set; }
        public float StartCountdown { get; private set; }
        public float ResultsTimeLeft { get; private set; }
        public string ResultsText { get; private set; } = string.Empty;

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
            MatchTimeLeft = _matchDuration;
            StartCountdown = _lobbyStartDelay;
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
            if (_scoreToWin > 0 && scorer.Score.Value >= _scoreToWin)
            {
                EndMatch();
            }
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
            CurrentState = GameState.InProgress;
            Debug.Log("[Server] Match started.");
            BroadcastState();
        }

        private void EndMatch()
        {
            if (CurrentState != GameState.InProgress)
            {
                return;
            }

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
            Debug.Log("[Server] Lobby reset. Waiting for players.");
            BroadcastState();
        }

        private void ResetPlayersForRound(bool resetScore)
        {
            foreach (PlayerNetwork player in PlayerNetwork.ActivePlayers)
            {
                if (player != null && player.IsSpawned && player.IsServerInitialized)
                {
                    player.ResetForMatchOnServer(resetScore);
                }
            }
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

                string nickname = string.IsNullOrWhiteSpace(player.Nickname.Value)
                    ? $"Player_{player.OwnerId}"
                    : player.Nickname.Value;
                builder.AppendLine($"{nickname}: {player.Score.Value}");
            }

            if (builder.Length == 0)
            {
                builder.AppendLine("No connected players.");
            }
            else if (winner != null)
            {
                string nickname = string.IsNullOrWhiteSpace(winner.Nickname.Value)
                    ? $"Player_{winner.OwnerId}"
                    : winner.Nickname.Value;
                builder.Insert(0, $"Winner: {nickname}\n");
            }

            return builder.ToString().TrimEnd();
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
                ResultsText = ResultsText
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
        }
    }
}
