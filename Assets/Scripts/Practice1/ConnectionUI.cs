using FishNet;
using FishNet.Managing;
using FishNet.Managing.Transporting;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Practice1
{
    public class ConnectionUI : MonoBehaviour
    {
        public static string PlayerNickname { get; private set; } = "Player";

        [Header("Connection UI")]
        [SerializeField] private GameObject _connectPanel;
        [SerializeField] private TMP_InputField _nicknameInput;
        [SerializeField] private TMP_InputField _addressInput;
        [SerializeField] private Button _hostButton;
        [SerializeField] private Button _clientButton;
        [SerializeField] private TMP_Text _statusText;

        [Header("Gameplay UI")]
        [SerializeField] private GameObject _gameplayPanel;
        [SerializeField] private Button _attackButton;
        [SerializeField] private TMP_Text _modeText;
        [SerializeField] private TMP_Text _nicknameText;
        [SerializeField] private TMP_Text _ammoText;
        [SerializeField] private TMP_Text _respawnText;

        [SerializeField] private ushort _port = 7777;

        private static readonly long[] LatencyPresets = { 0L, 200L, 500L };

        private PlayerShooting _localShooting;
        private PlayerNetwork _localPlayer;
        private float _localDeathTime = -1f;
        private int _latencyPresetIndex;

        private void Awake()
        {
            if (_nicknameInput != null)
            {
                _nicknameInput.text = PlayerNickname;
            }

            if (_addressInput != null && string.IsNullOrWhiteSpace(_addressInput.text))
            {
                _addressInput.text = "127.0.0.1";
            }

            if (_hostButton != null)
            {
                _hostButton.onClick.AddListener(StartAsHost);
            }

            if (_clientButton != null)
            {
                _clientButton.onClick.AddListener(StartAsClient);
            }

            if (_attackButton != null)
            {
                _attackButton.onClick.AddListener(OnAttackPressed);
            }

            ConfigureGameplayHudLayout();
        }

        private void Update()
        {
            NetworkManager manager = InstanceFinder.NetworkManager;
            if (manager == null)
            {
                SetPanels(true);
                SetStatus("FishNet NetworkManager not found in scene.");
                return;
            }

            if (InstanceFinder.IsOffline)
            {
                _localPlayer = null;
                _localShooting = null;
                _localDeathTime = -1f;
                SetPanels(true);
                SetStatus("Ready to connect");
                return;
            }

            SetPanels(false);
            HandlePracticeDebugInput(manager);
            EnsureLocalReferences();

            string mode = InstanceFinder.IsHostStarted
                ? "Host"
                : InstanceFinder.IsServerOnlyStarted
                    ? "Server"
                    : "Client";

            if (_modeText != null)
            {
                _modeText.text = BuildModeText(manager, mode);
            }

            if (_nicknameText != null)
            {
                _nicknameText.text = $"Nickname: {PlayerNickname}";
            }

            if (_attackButton != null && _localPlayer != null)
            {
                _attackButton.interactable =
                    _localShooting != null &&
                    _localPlayer.IsAlive.Value &&
                    _localShooting.HasAmmo &&
                    GameManager.IsGameplayActive;
            }

            if (_ammoText != null)
            {
                _ammoText.text = _localShooting == null
                    ? "Ammo: -"
                    : $"Ammo: {_localShooting.CurrentAmmo.Value}/{_localShooting.MaxAmmo} | Score: {(_localPlayer == null ? 0 : _localPlayer.Score.Value)}";
            }

            UpdateRespawnUi();

            if (_localPlayer != null)
            {
                bool dead = !_localPlayer.IsAlive.Value;
                if (dead && _localDeathTime < 0f)
                {
                    _localDeathTime = Time.time;
                }
                else if (!dead)
                {
                    _localDeathTime = -1f;
                }
            }
        }

        public void StartAsHost()
        {
            if (InstanceFinder.NetworkManager == null)
            {
                return;
            }

            SaveNickname();
            ConfigureTransport();
            InstanceFinder.ServerManager.StartConnection();
            InstanceFinder.ClientManager.StartConnection();
        }

        public void StartAsClient()
        {
            if (InstanceFinder.NetworkManager == null)
            {
                return;
            }

            SaveNickname();
            ConfigureTransport();
            InstanceFinder.ClientManager.StartConnection();
        }

        private void ConfigureTransport()
        {
            NetworkManager manager = InstanceFinder.NetworkManager;
            if (manager == null)
            {
                return;
            }

            Transport transport = manager.TransportManager != null
                ? manager.TransportManager.Transport
                : manager.GetComponent<Transport>();

            if (transport == null)
            {
                Debug.LogError("FishNet Transport component is missing on NetworkManager.");
                SetStatus("FishNet Transport is missing on NetworkManager.");
                return;
            }

            string rawAddress = _addressInput != null ? _addressInput.text : "127.0.0.1";
            string address = string.IsNullOrWhiteSpace(rawAddress) ? "127.0.0.1" : rawAddress.Trim();
            if (_addressInput != null)
            {
                _addressInput.text = address;
            }

            transport.SetClientAddress(address);
            transport.SetPort(_port);
        }

        private void SaveNickname()
        {
            string rawValue = _nicknameInput != null ? _nicknameInput.text : string.Empty;
            PlayerNickname = string.IsNullOrWhiteSpace(rawValue) ? "Player" : rawValue.Trim();
            if (_nicknameInput != null)
            {
                _nicknameInput.text = PlayerNickname;
            }
        }

        private static string BuildModeText(NetworkManager manager, string mode)
        {
            long pingMs = manager != null && manager.TimeManager != null
                ? manager.TimeManager.RoundTripTime
                : 0L;

            string predictionState = PlayerMovement.ClientSidePredictionEnabled ? "on" : "off";
            if (manager == null || manager.TransportManager == null)
            {
                return $"Mode: {mode} | Ping: {pingMs} ms | CSP: {predictionState}";
            }

            LatencySimulator latencySimulator = manager.TransportManager.LatencySimulator;
            string simulatorState = latencySimulator.GetEnabled()
                ? $"{latencySimulator.GetLatency()} ms"
                : "off";

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                return $"Mode: {mode} | Ping: {pingMs} ms | Lag sim: {simulatorState} | CSP: {predictionState}";
            }

            return
                $"Mode: {mode} | Ping: {pingMs} ms | Lag: {simulatorState} | CSP: {predictionState}\n" +
                $"State: {gameManager.CurrentState} | Players: {gameManager.ConnectedPlayers}/{gameManager.RequiredPlayers} | Time: {gameManager.MatchTimeLeft:0}s";
        }

        private void ConfigureGameplayHudLayout()
        {
            ConfigureHudText(_modeText, new Vector2(16f, -12f), new Vector2(470f, 52f), 17f);
            ConfigureHudText(_nicknameText, new Vector2(16f, -68f), new Vector2(470f, 24f), 18f);
            ConfigureHudText(_ammoText, new Vector2(16f, -96f), new Vector2(470f, 24f), 18f);
            ConfigureHudText(_respawnText, new Vector2(16f, -132f), new Vector2(470f, 84f), 21f);
        }

        private static void ConfigureHudText(TMP_Text text, Vector2 anchoredPosition, Vector2 size, float fontSize)
        {
            if (text == null)
            {
                return;
            }

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableAutoSizing = false;
            text.fontSize = fontSize;
            text.lineSpacing = -10f;
            text.overflowMode = TextOverflowModes.Overflow;
        }

        private void HandlePracticeDebugInput(NetworkManager manager)
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
                PlayerMovement.SetClientSidePredictionEnabled(!PlayerMovement.ClientSidePredictionEnabled);
            }

            if (Keyboard.current.f7Key.wasPressedThisFrame)
            {
                _latencyPresetIndex = (_latencyPresetIndex + 1) % LatencyPresets.Length;
                ApplyLatencyPreset(manager, LatencyPresets[_latencyPresetIndex]);
            }
        }

        private static void ApplyLatencyPreset(NetworkManager manager, long latencyMs)
        {
            if (manager == null || manager.TransportManager == null)
            {
                return;
            }

            LatencySimulator latencySimulator = manager.TransportManager.LatencySimulator;
            latencySimulator.SetLatency(latencyMs);
            latencySimulator.SetEnabled(latencyMs > 0L);
        }

        private void OnAttackPressed()
        {
            EnsureLocalReferences();
            if (_localShooting != null && GameManager.IsGameplayActive)
            {
                _localShooting.TryShoot();
            }
        }

        private void EnsureLocalReferences()
        {
            if (_localShooting != null && _localShooting.IsSpawned && _localPlayer != null && _localPlayer.IsSpawned)
            {
                return;
            }

            foreach (PlayerNetwork player in PlayerNetwork.ActivePlayers)
            {
                if (player != null && player.IsOwner)
                {
                    _localPlayer = player;
                    _localShooting = player.GetComponent<PlayerShooting>();
                    return;
                }
            }
        }

        private void SetPanels(bool connectVisible)
        {
            if (_connectPanel != null)
            {
                _connectPanel.SetActive(connectVisible);
            }

            if (_gameplayPanel != null)
            {
                _gameplayPanel.SetActive(!connectVisible);
            }
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }
        }

        private void UpdateRespawnUi()
        {
            if (_respawnText == null)
            {
                return;
            }

            if (_localPlayer == null)
            {
                _respawnText.text = string.Empty;
                return;
            }

            if (TryUpdateMatchStateUi())
            {
                return;
            }

            if (_localPlayer.IsAlive.Value)
            {
                _respawnText.text = _localShooting != null && !_localShooting.HasAmmo
                    ? "No ammo. Respawn to refill."
                    : string.Empty;
                return;
            }

            float deathTime = _localDeathTime < 0f ? Time.time : _localDeathTime;
            float left = Mathf.Max(0f, _localPlayer.RespawnDelay - (Time.time - deathTime));
            _respawnText.text = $"Respawn in: {left:0.0}s";
        }

        private bool TryUpdateMatchStateUi()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                return false;
            }

            switch (gameManager.CurrentState)
            {
                case GameState.WaitingForPlayers:
                    _respawnText.text = gameManager.ConnectedPlayers < gameManager.RequiredPlayers
                        ? $"Waiting for players: {gameManager.ConnectedPlayers}/{gameManager.RequiredPlayers}"
                        : $"Match starts in: {gameManager.StartCountdown:0.0}s";
                    return true;
                case GameState.ShowingResults:
                    _respawnText.text = $"Results\n{gameManager.ResultsText}\nLobby in: {gameManager.ResultsTimeLeft:0.0}s";
                    return true;
                default:
                    return false;
            }
        }
    }
}
