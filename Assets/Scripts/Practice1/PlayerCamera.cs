using Unity.Netcode;
using UnityEngine;

namespace Practice1
{
    public class PlayerCamera : NetworkBehaviour
    {
        [SerializeField] private Vector3 _offset = new Vector3(0f, 8f, -6f);
        [SerializeField] private bool _lookAtPlayer = true;

        private Camera _mainCamera;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            _mainCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null)
                {
                    return;
                }
            }

            _mainCamera.transform.position = transform.position + _offset;
            if (_lookAtPlayer)
            {
                _mainCamera.transform.LookAt(transform.position + Vector3.up * 1.2f);
            }
        }
    }
}
