using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;
using UnityEngine;
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

        private PlayerShooting _localShooting;
        private PlayerNetwork _localPlayer;
        private float _localDeathTime = -1f;

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
        }

        private void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                SetPanels(true);
                SetStatus("NetworkManager not found in scene.");
                return;
            }

            if (!manager.IsListening)
            {
                SetPanels(true);
                SetStatus("Ready to connect");
            }
            else
            {
                SetPanels(false);
                EnsureLocalReferences();

                string mode = manager.IsHost ? "Host" : manager.IsServer ? "Server" : "Client";
                if (_modeText != null)
                {
                    _modeText.text = $"Mode: {mode}";
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
            if (NetworkManager.Singleton == null)
            {
                return;
            }

            SaveNickname();
            ConfigureTransport();
            NetworkManager.Singleton.StartHost();
        }

        public void StartAsClient()
        {
            if (NetworkManager.Singleton == null)
            {
                return;
            }

            SaveNickname();
            ConfigureTransport();
            NetworkManager.Singleton.StartClient();
        }

        private void ConfigureTransport()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                return;
            }

            UnityTransport transport = manager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("UnityTransport component is missing on NetworkManager.");
                SetStatus("UnityTransport is missing on NetworkManager.");
                return;
            }

            if (manager.NetworkConfig.NetworkTransport == null)
            {
                manager.NetworkConfig.NetworkTransport = transport;
            }

            string rawAddress = _addressInput != null ? _addressInput.text : "127.0.0.1";
            string address = string.IsNullOrWhiteSpace(rawAddress) ? "127.0.0.1" : rawAddress.Trim();
            if (_addressInput != null)
            {
                _addressInput.text = address;
            }

            transport.SetConnectionData(address, _port);
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
    }
}
