using Unity.Netcode;
using UnityEngine;

namespace Practice1
{
    public class HealthPickup : NetworkBehaviour
    {
        [SerializeField] private int _healAmount = 40;

        private PickupManager _manager;
        private Vector3 _spawnPosition;

        public void Init(PickupManager manager)
        {
            _manager = manager;
            _spawnPosition = transform.position;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer)
            {
                return;
            }

            PlayerNetwork player = other.GetComponentInParent<PlayerNetwork>();
            if (player == null || !player.IsAlive.Value)
            {
                return;
            }

            if (player.HP.Value >= player.MaxHp)
            {
                return;
            }

            player.HealOnServer(_healAmount);
            _manager?.OnPickedUp(_spawnPosition);

            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(destroy: true);
            }
        }
    }
}
