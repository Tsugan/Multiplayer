using Unity.Collections;
using Unity.Netcode;
using TMPro;
using UnityEngine;

namespace Practice1
{
    [RequireComponent(typeof(PlayerNetwork))]
    public class PlayerView : NetworkBehaviour
    {
        private PlayerNetwork _playerNetwork;
        private string _nicknameValue = "Player";
        private int _hpValue = 100;
        private Canvas _uiCanvas;
        private RectTransform _labelRect;
        private TextMeshProUGUI _labelText;

        private void Awake()
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
        }

        public override void OnNetworkSpawn()
        {
            _playerNetwork.Nickname.OnValueChanged += OnNicknameChanged;
            _playerNetwork.HP.OnValueChanged += OnHpChanged;

            OnNicknameChanged(default, _playerNetwork.Nickname.Value);
            OnHpChanged(0, _playerNetwork.HP.Value);
            EnsureLabel();
        }

        public override void OnNetworkDespawn()
        {
            _playerNetwork.Nickname.OnValueChanged -= OnNicknameChanged;
            _playerNetwork.HP.OnValueChanged -= OnHpChanged;

            if (_labelRect != null)
            {
                Destroy(_labelRect.gameObject);
                _labelRect = null;
                _labelText = null;
            }
        }

        private void LateUpdate()
        {
            if (!IsSpawned || Camera.main == null)
            {
                return;
            }

            if (_labelRect == null)
            {
                EnsureLabel();
                return;
            }

            Vector3 worldPosition = transform.position + Vector3.up * 1.8f;
            Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);
            if (screenPosition.z <= 0f)
            {
                _labelRect.gameObject.SetActive(false);
                return;
            }

            _labelRect.gameObject.SetActive(true);

            if (_uiCanvas == null)
            {
                return;
            }

            RectTransform canvasRect = _uiCanvas.transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 localPoint))
            {
                _labelRect.anchoredPosition = localPoint;
            }
        }

        private void OnNicknameChanged(FixedString32Bytes oldValue, FixedString32Bytes newValue)
        {
            _nicknameValue = newValue.ToString();
            RefreshLabelText();
        }

        private void OnHpChanged(int oldValue, int newValue)
        {
            _hpValue = newValue;
            RefreshLabelText();
        }

        private void EnsureLabel()
        {
            if (_labelRect != null)
            {
                return;
            }

            _uiCanvas = FindFirstObjectByType<Canvas>();
            if (_uiCanvas == null)
            {
                return;
            }

            GameObject labelObject = new GameObject($"PlayerLabel_{NetworkObjectId}", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(_uiCanvas.transform, false);

            _labelRect = labelObject.GetComponent<RectTransform>();
            _labelRect.sizeDelta = new Vector2(160f, 48f);
            _labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _labelRect.pivot = new Vector2(0.5f, 0.5f);

            _labelText = labelObject.GetComponent<TextMeshProUGUI>();
            _labelText.alignment = TextAlignmentOptions.Center;
            _labelText.fontSize = 16;
            _labelText.color = Color.white;
            _labelText.raycastTarget = false;

            RefreshLabelText();
        }

        private void RefreshLabelText()
        {
            if (_labelText != null)
            {
                _labelText.text = $"{_nicknameValue}\nHP: {_hpValue}";
            }
        }
    }
}
