using FishNet.Object;
using UnityEngine;

namespace Practice1
{
    public class Projectile : NetworkBehaviour
    {
        [SerializeField] private float _speed = 18f;
        [SerializeField] private int _damage = 20;
        [SerializeField] private float _lifetime = 4f;

        private float _spawnTime;
        private int _shooterClientId;
        private Rigidbody _rigidbody;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _spawnTime = Time.time;
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

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = transform.forward * _speed;
            }
            else
            {
                transform.Translate(Vector3.forward * _speed * Time.fixedDeltaTime, Space.World);
            }

            if (Time.time >= _spawnTime + _lifetime)
            {
                Despawn();
            }
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
            if (!base.IsServerInitialized)
            {
                return;
            }

            PlayerNetwork target = other.GetComponent<PlayerNetwork>();
            if (target == null || !target.IsAlive.Value)
            {
                return;
            }

            if (target.OwnerId == _shooterClientId)
            {
                return;
            }

            int nextHp = Mathf.Max(0, target.HP.Value - _damage);
            target.HP.Value = nextHp;

            Despawn();
        }
    }
}
