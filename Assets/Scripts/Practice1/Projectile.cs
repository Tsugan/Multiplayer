using FishNet.Object;
using UnityEngine;

namespace Practice1
{
    public class Projectile : NetworkBehaviour
    {
        [SerializeField] private float _speed = 18f;
        [SerializeField] private int _damage = 20;
        [SerializeField] private float _lifetime = 4f;
        [SerializeField] private float _collisionSweepRadius = 0.12f;

        private float _spawnTime;
        private int _shooterClientId = -1;
        private Rigidbody _rigidbody;
        private Vector3 _lastServerPosition;
        private bool _despawned;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody != null)
            {
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        public override void OnStartNetwork()
        {
            _spawnTime = Time.time;
            _lastServerPosition = transform.position;
            if (!base.IsServerInitialized)
            {
                return;
            }

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = transform.forward * _speed;
            }
        }

        private void FixedUpdate()
        {
            if (!base.IsServerInitialized)
            {
                return;
            }

            if (SweepServerCollision())
            {
                return;
            }

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = transform.forward * _speed;
            }
            else
            {
                transform.Translate(Vector3.forward * _speed * Time.fixedDeltaTime, Space.World);
            }

            if (Time.time >= _spawnTime + _lifetime && NetworkObject != null && NetworkObject.IsSpawned)
            {
                DespawnOnServer();
            }

            _lastServerPosition = transform.position;
        }

        public void Configure(float speed, int damage)
        {
            _speed = speed;
            _damage = damage;
        }

        public void SetShooterClientId(int shooterClientId)
        {
            _shooterClientId = shooterClientId;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!base.IsServerInitialized || _despawned)
            {
                return;
            }

            HandleCollision(other);
        }

        private bool SweepServerCollision()
        {
            if (_despawned)
            {
                return true;
            }

            Vector3 currentPosition = transform.position;
            Vector3 movement = currentPosition - _lastServerPosition;
            float distance = movement.magnitude;
            if (distance <= 0.001f)
            {
                return false;
            }

            RaycastHit[] hits = Physics.SphereCastAll(
                _lastServerPosition,
                Mathf.Max(0.01f, _collisionSweepRadius),
                movement / distance,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider.attachedRigidbody == _rigidbody)
                {
                    continue;
                }

                if (HandleCollision(hitCollider))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HandleCollision(Collider other)
        {
            if (other == null || _despawned)
            {
                return false;
            }

            PlayerNetwork target = other.GetComponentInParent<PlayerNetwork>();
            if (target != null)
            {
                if (!target.IsAlive.Value || target.OwnerId == _shooterClientId)
                {
                    return false;
                }

                GameManager.Instance?.ExplodeBombFromHit(target, _shooterClientId);
                target.ApplyDamageOnServer(_damage, _shooterClientId);
                DespawnOnServer();
                return true;
            }

            if (!other.isTrigger)
            {
                DespawnOnServer();
                return true;
            }

            return false;
        }

        private void DespawnOnServer()
        {
            if (_despawned)
            {
                return;
            }

            _despawned = true;
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                base.ServerManager.Despawn(NetworkObject, DespawnType.Destroy);
            }
        }
    }
}
