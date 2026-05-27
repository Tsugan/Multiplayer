using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Practice1
{
    public class ServerAutoStart : MonoBehaviour
    {
        [SerializeField] private ushort _port = 7777;

        private bool _started;

        private void Start()
        {
            TryStartHeadlessServer();
        }

        private void Update()
        {
            TryStartHeadlessServer();
        }

        private void TryStartHeadlessServer()
        {
            if (_started || !Application.isBatchMode)
            {
                return;
            }

            NetworkManager manager = InstanceFinder.NetworkManager;
            if (manager == null || manager.ServerManager == null)
            {
                return;
            }

            Transport transport = manager.TransportManager != null
                ? manager.TransportManager.Transport
                : manager.GetComponent<Transport>();

            if (transport != null)
            {
                transport.SetPort(_port);
            }

            Debug.Log($"[Server] Headless mode detected. Starting FishNet server on UDP {_port}.");
            manager.ServerManager.StartConnection();
            _started = true;
        }
    }
}
