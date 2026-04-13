using System.Collections;
using Unity.Netcode;
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

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            }
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
            }
        }

        private void OnServerStarted()
        {
            TrySpawnInitial();
        }

        private void TrySpawnInitial()
        {
            if (_spawnedInitial)
            {
                return;
            }

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
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
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
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
            if (networkObject != null)
            {
                networkObject.Spawn();
            }
        }
    }
}
