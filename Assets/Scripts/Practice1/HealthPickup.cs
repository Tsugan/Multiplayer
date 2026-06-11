using FishNet.Object;
using UnityEngine;

namespace Practice1
{
    public class HealthPickup : NetworkBehaviour
    {
        [SerializeField] private int _healAmount = 40;
        [SerializeField] private float _pickupRadius = 0.75f;

        private PickupManager _manager;
        private Vector3 _spawnPosition;
        private bool _collected;

        public void Init(PickupManager manager)
        {
            _manager = manager;
            _spawnPosition = transform.position;
            _collected = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryCollect(other);
        }

        private void FixedUpdate()
        {
            if (!base.IsServerInitialized)
            {
                return;
            }

            if (_collected)
            {
                return;
            }

            Collider[] overlaps = Physics.OverlapSphere(
                transform.position,
                Mathf.Max(0.1f, _pickupRadius),
                ~0,
                QueryTriggerInteraction.Collide
            );

            for (int i = 0; i < overlaps.Length; i++)
            {
                if (TryCollect(overlaps[i]))
                {
                    return;
                }
            }
        }

        private bool TryCollect(Collider other)
        {
            if (!base.IsServerInitialized || _collected || other == null)
            {
                return false;
            }

            PlayerNetwork player = other.GetComponentInParent<PlayerNetwork>();
            if (player == null || !player.IsAlive.Value)
            {
                return false;
            }

            if (player.HP.Value >= player.MaxHp)
            {
                return false;
            }

            _collected = true;
            player.HealOnServer(_healAmount);
            _manager?.OnPickedUp(_spawnPosition);

            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                base.ServerManager.Despawn(NetworkObject, DespawnType.Destroy);
            }

            return true;
        }
    }
}
