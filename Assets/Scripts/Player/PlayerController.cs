using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using BossFight2D.Network;
using Cinemachine;

namespace BossFight2D.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : NetworkBehaviour
    {
        public float moveSpeed = 6f, dashSpeed = 12f, dashCooldown = 1.2f, dashDuration = 0.15f;
        Rigidbody2D rb;
        Vector2 input; // Only used by the owner client to read local input
        // Owner-authoritative dash state
        private float _lastDashTime = -999f;
        private bool _dashing;
        private float _dashEnd;

        public Animator animator;
        SpriteRenderer sr;
        public bool inputEnabled = true;

        private NetworkVariable<bool> networkFlipX = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> networkSpeed = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
            sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
            rb.gravityScale = 0;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            // Ensure there is an owner-authoritative transform sync component present on all instances
            var nt = GetComponent<NetworkTransform>();
            var cnt = GetComponent<BossFight2D.Network.ClientNetworkTransform>();
            if (nt == null && cnt == null)
            {
                gameObject.AddComponent<BossFight2D.Network.ClientNetworkTransform>();
            }
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

        // Server RPCs removed for owner-authoritative movement

        void Update()
        {
            if (!IsOwner || !inputEnabled)
            {
                return;
            }

            // 1. Read local input
            input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
            bool dashInput = Input.GetKeyDown(KeyCode.LeftShift);

            // 2. Handle dash locally
            if (dashInput && Time.time - _lastDashTime >= dashCooldown)
            {
                _dashing = true;
                _dashEnd = Time.time + dashDuration;
                _lastDashTime = Time.time;
            }

            // 3. Update facing direction (replicated via NetworkVariable owned by the client)
            var cam = Camera.main;
            if (cam)
            {
                var mouse = cam.ScreenToWorldPoint(Input.mousePosition);
                networkFlipX.Value = mouse.x < transform.position.x;
            }
        }

        void FixedUpdate()
        {
            if (!IsOwner) return; // Owner moves locally and replicates transform

            Vector2 velocity;
            if (_dashing)
            {
                velocity = input * dashSpeed;
                if (Time.time >= _dashEnd) _dashing = false;
            }
            else
            {
                velocity = input * moveSpeed;
            }

            rb.velocity = velocity;
            networkSpeed.Value = velocity.magnitude;
        }
        public bool IsDashing()
        {
            if (IsOwner) return _dashing;
            // Non-owners can guess based on observed speed
            return rb.velocity.magnitude > moveSpeed * 1.1f;
        }

    }
}