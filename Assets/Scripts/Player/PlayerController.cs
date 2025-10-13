using UnityEngine;
using Unity.Netcode;
using Cinemachine;

namespace BossFight2D.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : NetworkBehaviour
    {
        public float moveSpeed = 6f, dashSpeed = 12f, dashCooldown = 1.2f, dashDuration = 0.15f;
        Rigidbody2D rb;
        Vector2 input; // Only used by the owner client to read local input

        // Server-side state
        private Vector2 _serverInput;
        private float _serverLastDashTime = -999f;
        private bool _serverDashing;
        private float _serverDashEnd;

        public Animator animator;
        SpriteRenderer sr;
        public bool inputEnabled = true;

        private NetworkVariable<bool> networkFlipX = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<float> networkSpeed = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
            sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
            rb.gravityScale = 0;
            rb.freezeRotation = true;
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                var camera = FindFirstObjectByType<CinemachineVirtualCamera>();
                if (camera != null)
                {
                    camera.Follow = transform;
                }
            }

            networkFlipX.OnValueChanged += OnFlipXChanged;
            networkSpeed.OnValueChanged += OnSpeedChanged;
            OnFlipXChanged(false, networkFlipX.Value);
            OnSpeedChanged(0, networkSpeed.Value);
        }

        public override void OnNetworkDespawn()
        {
            networkFlipX.OnValueChanged -= OnFlipXChanged;
            networkSpeed.OnValueChanged -= OnSpeedChanged;
        }

        private void OnFlipXChanged(bool previousValue, bool newValue)
        {
            if (sr != null)
            {
                sr.flipX = newValue;
            }
        }

        private void OnSpeedChanged(float previousValue, float newValue)
        {
            if (animator != null)
            {
                animator.SetFloat("Speed", newValue);
            }
        }

        [ServerRpc]
        private void UpdateInputServerRpc(Vector2 clientInput, bool dashInput)
        {
            _serverInput = clientInput;

            if (dashInput && Time.time - _serverLastDashTime >= dashCooldown)
            {
                _serverDashing = true;
                _serverDashEnd = Time.time + dashDuration;
                _serverLastDashTime = Time.time;
            }
        }

        [ServerRpc]
        private void UpdateFacingDirectionServerRpc(bool flip)
        {
            networkFlipX.Value = flip;
        }

        void Update()
        {
            if (!IsOwner || !inputEnabled)
            {
                return;
            }

            // 1. Read local input
            input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
            bool dashInput = Input.GetKeyDown(KeyCode.LeftShift);

            // 2. Send input to server
            UpdateInputServerRpc(input, dashInput);

            // 3. Send facing direction to server
            var cam = Camera.main;
            if (cam)
            {
                var mouse = cam.ScreenToWorldPoint(Input.mousePosition);
                UpdateFacingDirectionServerRpc(mouse.x < transform.position.x);
            }
        }

        void FixedUpdate()
        {
            if (!IsServer)
            {
                return; // All logic is now server-authoritative
            }

            Vector2 velocity;
            if (_serverDashing)
            {
                velocity = _serverInput * dashSpeed;
                if (Time.time >= _serverDashEnd) _serverDashing = false;
            }
            else
            {
                velocity = _serverInput * moveSpeed;
            }

            rb.velocity = velocity;
            networkSpeed.Value = velocity.magnitude;
        }
        public bool IsDashing()
        {
            if (IsServer) return _serverDashing;
            // A client can't know for sure, but can guess based on speed
            return rb.velocity.magnitude > moveSpeed * 1.1f;
        }

    }
}