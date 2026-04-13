using Unity.Netcode;
using UnityEngine;

namespace Practice1
{
    [RequireComponent(typeof(PlayerNetwork))]
    public class PlayerCombat : NetworkBehaviour
    {
        [SerializeField] private int _damage = 10;
        private PlayerNetwork _playerNetwork;

        private void Awake()
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
        }

        public void TryAttackNearest()
        {
            if (!IsOwner)
            {
                return;
            }

            PlayerNetwork target = FindNearestTarget();
            if (target == null)
            {
                return;
            }

            DealDamageServerRpc(target.NetworkObjectId, _damage);
        }

        [ServerRpc]
        private void DealDamageServerRpc(ulong targetObjectId, int damage)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetObjectId, out NetworkObject targetObject))
            {
                return;
            }

            PlayerNetwork targetPlayer = targetObject.GetComponent<PlayerNetwork>();
            if (targetPlayer == null || targetPlayer == _playerNetwork)
            {
                return;
            }

            int sanitizedDamage = Mathf.Max(0, damage);
            int nextHp = Mathf.Max(0, targetPlayer.HP.Value - sanitizedDamage);
            targetPlayer.HP.Value = nextHp;
        }

        private PlayerNetwork FindNearestTarget()
        {
            PlayerNetwork best = null;
            float bestDistance = float.MaxValue;
            Vector3 selfPosition = transform.position;

            foreach (PlayerNetwork player in PlayerNetwork.ActivePlayers)
            {
                if (player == null || player == _playerNetwork)
                {
                    continue;
                }

                float distance = (player.transform.position - selfPosition).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = player;
                }
            }

            return best;
        }
    }
}
