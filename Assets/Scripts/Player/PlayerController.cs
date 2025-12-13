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
        // Fallback when Cinemachine is unavailable: directly move the main camera to follow
        private bool _directCameraFollow;

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
            // Persist ONLY the local owner's player across client-side scene loads.
            // Remote players should not be marked DontDestroyOnLoad to avoid duplicates after scene transitions.
            if (IsOwner)
            {
                try { DontDestroyOnLoad(gameObject); } catch { }
                SetupCameraFollow();
                // Re-apply camera follow whenever a new scene loads on the client
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
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
            if (IsOwner)
            {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            }
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

        private void SetupCameraFollow()
        {
            // Ensure a Cinemachine Brain exists on the main camera so the vcam can drive it.
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                var brain = mainCam.GetComponent<Cinemachine.CinemachineBrain>();
                if (brain == null)
                {
                    try { mainCam.gameObject.AddComponent<Cinemachine.CinemachineBrain>(); }
                    catch { /* ignore if Cinemachine not available */ }
                    Debug.Log("[PlayerController] CinemachineBrain was missing on Main Camera and has been added at runtime.");
                }
            }

            var vcam = FindFirstObjectByType<CinemachineVirtualCamera>();
            if (vcam != null)
            {
                vcam.Follow = transform;
                _directCameraFollow = false;
                Debug.Log("[PlayerController] Cinemachine vcam follow assigned to local player.");
            }
            else
            {
                // If no Cinemachine vcam is present, enable simple direct follow as a fallback.
                _directCameraFollow = true;
                Debug.LogWarning("[PlayerController] No CinemachineVirtualCamera found. Using direct camera follow fallback for local player.");
            }
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (!IsOwner) return;
            // When transitioning to Gameplay, ensure camera follows again
            SetupCameraFollow();
        }

        void LateUpdate()
        {
            // Simple direct-follow fallback if Cinemachine is unavailable
            if (IsOwner && _directCameraFollow)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    var pos = transform.position;
                    pos.z = cam.transform.position.z; // preserve camera Z
                    cam.transform.position = pos;
                }
            }
        }

    }
}