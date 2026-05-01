using System.Collections;
using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace Practice1
{
    public class PickupManager : MonoBehaviour
    {
        [SerializeField] private GameObject _healthPickupPrefab;
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField] private float _respawnDelay = 10f;
        private bool _spawnedInitial;

        private void OnEnable()
        {
            TrySpawnInitial();

            if (InstanceFinder.NetworkManager != null)
            {
                InstanceFinder.NetworkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
            }
        }

        private void OnDisable()
        {
            if (InstanceFinder.NetworkManager != null)
            {
                InstanceFinder.NetworkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
            }
        }

        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                TrySpawnInitial();
            }
        }

        private void TrySpawnInitial()
        {
            if (_spawnedInitial)
            {
                return;
            }

            if (!InstanceFinder.IsServerStarted)
            {
                return;
            }

            _spawnedInitial = true;
            SpawnAll();
        }

        private void SpawnAll()
        {
            if (_spawnPoints == null)
            {
                return;
            }

            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                if (_spawnPoints[i] != null)
                {
                    SpawnPickup(_spawnPoints[i].position);
                }
            }
        }

        public void OnPickedUp(Vector3 position)
        {
            if (!InstanceFinder.IsServerStarted)
            {
                return;
            }

            StartCoroutine(RespawnAfterDelay(position));
        }

        private IEnumerator RespawnAfterDelay(Vector3 position)
        {
            yield return new WaitForSeconds(_respawnDelay);
            SpawnPickup(position);
        }

        private void SpawnPickup(Vector3 position)
        {
            if (_healthPickupPrefab == null)
            {
                return;
            }

            GameObject pickup = Instantiate(_healthPickupPrefab, position, Quaternion.identity);
            HealthPickup healthPickup = pickup.GetComponent<HealthPickup>();
            if (healthPickup != null)
            {
                healthPickup.Init(this);
            }

            NetworkObject networkObject = pickup.GetComponent<NetworkObject>();
            if (networkObject != null && InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.Spawn(networkObject);
            }
        }
    }
}
