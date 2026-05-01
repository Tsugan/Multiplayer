using FishNet;
using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using FishNet.Transporting;
using LiteNetLib;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Practice1
{
    public class ConnectionUI : MonoBehaviour
    {
        public static string PlayerNickname { get; private set; } = "Player";
        public static bool PredictionEnabled { get; private set; } = true;
        public static int SimulatedLatencyMs { get; private set; }
        private static ConnectionUI _instance;

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

        private PlayerShooting _localShooting;
        private PlayerNetwork _localPlayer;
        private float _localDeathTime = -1f;

        private void Awake()
        {
            _instance = this;

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
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            HandleDebugHotkeys();

            NetworkManager manager = InstanceFinder.NetworkManager;
            if (manager == null)
            {
                SetPanels(true);
                SetStatus("FishNet NetworkManager not found in scene.");
                return;
            }

            if (manager.IsOffline && !manager.ServerManager.Started && !manager.ClientManager.Started)
            {
                SetPanels(true);
                SetStatus("Ready to connect. F6: CSP, F7: Lag");
            }
            else
            {
                SetPanels(false);
                EnsureLocalReferences();
                ApplyLatencySimulation(manager);

                string mode = manager.IsHostStarted ? "Host" : manager.IsServerStarted ? "Server" : manager.IsClientStarted ? "Client" : "Connecting";
                if (_modeText != null)
                {
                    _modeText.text = $"Mode: {mode} | CSP: {(PredictionEnabled ? "ON" : "OFF")} | Lag: {SimulatedLatencyMs}ms";
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
                        _localShooting.HasAmmo;
                }

                if (_ammoText != null)
                {
                    if (_localShooting == null)
                    {
                        _ammoText.text = "Ammo: -";
                    }
                    else
                    {
                        _ammoText.text = $"Ammo: {_localShooting.CurrentAmmo.Value}/{_localShooting.MaxAmmo}";
                    }
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
        }

        public void StartAsHost()
        {
            NetworkManager manager = InstanceFinder.NetworkManager;
            if (manager == null)
            {
                return;
            }

            SaveNickname();
            if (ConfigureTransport(manager) == null)
            {
                return;
            }

            bool serverStarted = manager.ServerManager.StartConnection();
            if (serverStarted)
            {
                manager.ClientManager.StartConnection();
                ApplyLatencySimulation(manager);
            }
        }

        public void StartAsClient()
        {
            NetworkManager manager = InstanceFinder.NetworkManager;
            if (manager == null)
            {
                return;
            }

            SaveNickname();
            if (ConfigureTransport(manager) == null)
            {
                return;
            }

            manager.ClientManager.StartConnection();
            ApplyLatencySimulation(manager);
        }

        private Tugboat ConfigureTransport(NetworkManager manager)
        {
            Tugboat transport = manager.GetComponent<Tugboat>();
            if (transport == null)
            {
                Debug.LogError("Tugboat component is missing on NetworkManager.");
                SetStatus("Tugboat is missing on NetworkManager.");
                return null;
            }

            string rawAddress = _addressInput != null ? _addressInput.text : "127.0.0.1";
            string address = string.IsNullOrWhiteSpace(rawAddress) ? "127.0.0.1" : rawAddress.Trim();
            if (_addressInput != null)
            {
                _addressInput.text = address;
            }

            transport.SetClientAddress(address);
            transport.SetServerBindAddress("0.0.0.0", IPAddressType.IPv4);
            transport.SetPort(_port);

            return transport;
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

        public static string GetEffectiveNickname()
        {
            string uiValue = _instance != null && _instance._nicknameInput != null
                ? _instance._nicknameInput.text
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(uiValue))
            {
                return uiValue.Trim();
            }

            if (!string.IsNullOrWhiteSpace(PlayerNickname))
            {
                return PlayerNickname.Trim();
            }

            return "Player";
        }

        private void OnAttackPressed()
        {
            EnsureLocalReferences();
            if (_localShooting != null)
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

            if (_localPlayer.IsAlive.Value)
            {
                if (_localShooting != null && !_localShooting.HasAmmo)
                {
                    _respawnText.text = "No ammo. Respawn to refill.";
                }
                else
                {
                    _respawnText.text = string.Empty;
                }
                return;
            }

            float deathTime = _localDeathTime < 0f ? Time.time : _localDeathTime;
            float left = Mathf.Max(0f, _localPlayer.RespawnDelay - (Time.time - deathTime));
            _respawnText.text = $"Respawn in: {left:0.0}s";
        }

        private void HandleDebugHotkeys()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
                PredictionEnabled = !PredictionEnabled;
                SetStatus($"CSP {(PredictionEnabled ? "enabled" : "disabled")}.");
            }

            if (Keyboard.current.f7Key.wasPressedThisFrame)
            {
                SimulatedLatencyMs = SimulatedLatencyMs switch
                {
                    0 => 200,
                    200 => 500,
                    _ => 0
                };

                SetStatus($"Latency simulation: {SimulatedLatencyMs}ms.");
            }
        }

        private static void ApplyLatencySimulation(NetworkManager manager)
        {
            Tugboat transport = manager.GetComponent<Tugboat>();
            if (transport == null)
            {
                return;
            }

            FieldInfo netManagerField = typeof(CommonSocket).GetField("NetManager", BindingFlags.Instance | BindingFlags.NonPublic);
            ConfigureSocket(netManagerField, transport.ClientSocket);
            ConfigureSocket(netManagerField, transport.ServerSocket);

            static void ConfigureSocket(FieldInfo netManagerField, CommonSocket socketWrapper)
            {
                NetManager socket = netManagerField?.GetValue(socketWrapper) as NetManager;
                if (socket == null)
                {
                    return;
                }

                bool enabled = SimulatedLatencyMs > 0;
                socket.SimulateLatency = enabled;
                socket.SimulatePacketLoss = false;
                socket.SimulationMinLatency = SimulatedLatencyMs;
                socket.SimulationMaxLatency = SimulatedLatencyMs;
            }
        }
    }
}
