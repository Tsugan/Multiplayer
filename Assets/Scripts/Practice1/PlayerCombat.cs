using FishNet.Object;
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
            if (!base.IsOwner || !GameManager.IsGameplayActive)
            {
                return;
            }

            PlayerNetwork target = FindNearestTarget();
            if (target == null)
            {
                return;
            }

            DealDamageServerRpc(target.ObjectId, _damage);
        }

        [ServerRpc]
        private void DealDamageServerRpc(int targetObjectId, int damage)
        {
            if (!GameManager.IsGameplayActive)
            {
                return;
            }

            if (!base.ServerManager.Objects.Spawned.TryGetValue(targetObjectId, out NetworkObject targetObject))
            {
                return;
            }

            PlayerNetwork targetPlayer = targetObject.GetComponent<PlayerNetwork>();
            if (targetPlayer == null || targetPlayer == _playerNetwork)
            {
                return;
            }

            int sanitizedDamage = Mathf.Max(0, damage);
            targetPlayer.ApplyDamageOnServer(sanitizedDamage, OwnerId);
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
