using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using BossFight2D.Core;
using BossFight2D.Combat;
using BossFight2D.Quiz;
using BossFight2D.Systems;
using UnityEngine.EventSystems;
using TMPro;

namespace BossFight2D.Player
{
    public class PlayerCombat : NetworkBehaviour
    {

        public Hitbox2D hitbox;
        public Animator animator;
        public int defaultDamage = 10;
        public float attackCooldown = 0.5f;

        [Header("Hitbox Alignment")]
        [Tooltip("If true, the player's hitbox will align to the current facing (flipX) so it stays in front of the player.")]
        [SerializeField] private bool alignHitboxToFacing = true;
        [Tooltip("Horizontal offset for the hitbox relative to the player. Positive value for facing right; automatically mirrored when facing left.")]
        [SerializeField] private float hitboxForwardOffset = 0.6f;
        [Tooltip("Vertical offset for the hitbox relative to the player.")]
        [SerializeField] private float hitboxUpOffset = 0f;

        [Header("Attack Charges (Stamina)")]
        public NetworkVariable<int> attackCharges = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Tooltip("Maximum attack charges the player can hold.")]
        public int maxAttackCharges = 3;

        [Tooltip("Charges gained when answering a question correctly.")]
        public int chargesPerCorrect = 1;

        [Tooltip("Charges consumed per attack (applies even during Power Play).")]
        public int chargeCostPerAttack = 1;

        [Tooltip("Optional placeholder for a UI element (e.g., Text/Slider/Image) to reflect charges in the Inspector.")]
        public GameObject chargesUIPlaceholder;

        private float _nextAttackAllowed;
        private SpriteRenderer _spriteRenderer;
        private Vector3 _hitboxDefaultLocalPosition;
        private Quaternion _hitboxDefaultLocalRotation;

        [Header("Phase & UI Gating")]
        [Tooltip("Disables player attacks while a question is being answered via UI.")]
        [SerializeField] private bool blockAttacksDuringQuestion = true;
        [Tooltip("Cooldown after submitting an answer before attacks are re-enabled.")]
        [SerializeField] private float postAnswerAttackLockSeconds = 1.25f;
        private bool _questionActive;
        private bool _answerInteractionActive; // local flag set the moment the answer button is pressed
        private bool _powerPlayActive;
        private float _attacksReenabledAtTime;

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (hitbox == null) hitbox = GetComponentInChildren<Hitbox2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (hitbox != null)
            {
                _hitboxDefaultLocalPosition = hitbox.transform.localPosition;
                _hitboxDefaultLocalRotation = hitbox.transform.localRotation;
                // Use current hitbox local position as the default offsets so alignment respects prefab setup
                hitboxForwardOffset = Mathf.Abs(_hitboxDefaultLocalPosition.x);
                hitboxUpOffset = _hitboxDefaultLocalPosition.y;
            }
            if (chargesUIPlaceholder == null)
            {
                //find  object in the hierarchy with the name "AttackCharge"
                chargesUIPlaceholder = GameObject.Find("AttackCharge");
                if (chargesUIPlaceholder == null)
                {
                    Debug.LogError("PlayerCombat: Could not find ChargesUI placeholder. Please add a child object with the name 'ChargesUI' to the player prefab.");
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                attackCharges.OnValueChanged += OnAttackChargesChanged;
                // Subscribe to global phase events to gate combat appropriately
                EventBus.QuestionStarted += OnQuestionStarted;
                EventBus.AnswerSubmitted += OnAnswerSubmitted;
                EventBus.QuestionTimeout += OnQuestionTimeout;
                EventBus.AnswerModeExited += OnAnswerModeExited;
                EventBus.PowerPlayStarted += OnPowerPlayStarted;
                EventBus.PowerPlayEnded += OnPowerPlayEnded;

                // Initialize HUD with starting charges
                EventBus.RaiseChargesChanged(attackCharges.Value, maxAttackCharges);
            }
            // Server-side: enforce consistent combat config to avoid prefab mismatch across host vs client
            if (IsServer)
            {
                // Temporary safeguard: unify cost and max across all spawned players
                // to prevent unexpected 3-per-attack consumption on remote clients.
                if (chargeCostPerAttack != 1 || maxAttackCharges != 3)
                {
                    Debug.LogWarning($"[PlayerCombat][Server] Overriding combat config for client {OwnerClientId}: cost {chargeCostPerAttack} -> 1, max {maxAttackCharges} -> 3");
                }
                chargeCostPerAttack = 1;
                maxAttackCharges = 3;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                attackCharges.OnValueChanged -= OnAttackChargesChanged;
                EventBus.QuestionStarted -= OnQuestionStarted;
                EventBus.AnswerSubmitted -= OnAnswerSubmitted;
                EventBus.QuestionTimeout -= OnQuestionTimeout;
                EventBus.AnswerModeExited -= OnAnswerModeExited;
                EventBus.PowerPlayStarted -= OnPowerPlayStarted;
                EventBus.PowerPlayEnded -= OnPowerPlayEnded;
            }
        }

        void Update()
        {
            if (!IsOwner) return;

            // Determine phase-based gating
            bool attacksAllowedByPhase = _powerPlayActive || (!blockAttacksDuringQuestion || (!_questionActive && !_answerInteractionActive));
            bool pastPostAnswerCooldown = Time.time >= _attacksReenabledAtTime;

            // Prevent attack from UI clicks; only allow direct combat input
            bool clickingOnUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if (attacksAllowedByPhase && pastPostAnswerCooldown && !clickingOnUI && Input.GetMouseButtonDown(0) && Time.time >= _nextAttackAllowed)
            {
                if (attackCharges.Value > 0)
                {
                    RequestAttackServerRpc();
                }
            }
        }

        // Ensure the hitbox stays in front of the player based on facing direction
        private void UpdateHitboxAlignment()
        {
            if (!alignHitboxToFacing || hitbox == null) return;
            bool flipX = _spriteRenderer != null && _spriteRenderer.flipX;
            float x = Mathf.Abs(hitboxForwardOffset);
            Vector3 desiredLocalPos = new Vector3(flipX ? -x : x, hitboxUpOffset, _hitboxDefaultLocalPosition.z);
            if (hitbox.transform.localPosition != desiredLocalPos)
            {
                hitbox.transform.localPosition = desiredLocalPos;
            }
            // Keep default rotation (circle colliders don't need rotation); if needed, extend to aim toward mouse.
        }

        [ServerRpc]
        private void RequestAttackServerRpc()
        {
            // Clamp cost to a sane range in case of out-of-range inspector values
            int cost = Mathf.Clamp(chargeCostPerAttack, 1, maxAttackCharges);
            int before = attackCharges.Value;
            Debug.Log($"[PlayerCombat][Server] Attack requested by client {OwnerClientId}: before={before}, cost={cost}, max={maxAttackCharges}");
            if (attackCharges.Value >= cost)
            {
                attackCharges.Value -= cost;
                _nextAttackAllowed = Time.time + attackCooldown;
                int after = attackCharges.Value;
                Debug.Log($"[PlayerCombat][Server] Attack processed for client {OwnerClientId}: after={after}");
                // Trigger attack animation on all clients
                AttackClientRpc();
            }
        }

        [ClientRpc]
        private void AttackClientRpc()
        {
            if (animator != null)
            {
                animator.SetTrigger("Attack");
            }
        }

        public void ActivateHitBox()
        {
            // Align hitbox to current facing before activation for precise collisions
            UpdateHitboxAlignment();
            // Resolve the hit and damage via the Hitbox.
            int damageToDeal = defaultDamage;
            if (PowerPlayManager.Instance != null && PowerPlayManager.Instance.IsPowerPlayActive.Value)
            {
                damageToDeal = PowerPlayManager.Instance.ModifyDamageOnBossHit(damageToDeal);
            }

            if (hitbox != null)
            {
                // Activate the hitbox, passing the calculated damage.
                // The hitbox is responsible for detecting collisions and applying damage.
                // This assumes Hitbox2D.Activate is updated to take damage.
                hitbox.Activate(0.5f, damageToDeal);
            }

        }

        public void DeactivateHitBox()
        {
            if (hitbox != null)
            {
                hitbox.Deactivate();
            }
        }


        public bool TryConsumeChargeForQuestionEscape()
        {
            if (attackCharges.Value > 0)
            {
                RequestQuestionEscapeServerRpc();
                return true;
            }
            return false;
        }

        [ServerRpc]
        private void RequestQuestionEscapeServerRpc()
        {
            if (attackCharges.Value > 0)
            {
                attackCharges.Value--;
            }
        }

        public void SetCombatEnabled(bool enabled)
        {
            SetCombatEnabledServerRpc(enabled);
        }

        [ServerRpc]
        private void SetCombatEnabledServerRpc(bool enabled)
        {
            // On the server, you might want to add logic to prevent players from attacking when combat is disabled.
            // For now, we'll just rely on the client-side check.
        }

        public void TriggerAttack()
        {
            if (IsServer)
            {
                AttackClientRpc();
            }
        }

        // Phase event hooks
        private void OnQuestionStarted(QuestionData q)
        {
            if (!IsOwner) return;
            if (blockAttacksDuringQuestion)
            {
                _questionActive = true;
            }
        }

        private void OnAnswerSubmitted(int selected, bool correct)
        {
            if (!IsOwner) return;
            // Start cooldown window after answering before attacks are re-enabled
            _attacksReenabledAtTime = Time.time + postAnswerAttackLockSeconds;
            // End local answer interaction gating; resolution UI will hide shortly
            _answerInteractionActive = false;
        }

        private void OnQuestionTimeout()
        {
            if (!IsOwner) return;
            _questionActive = false;
            _answerInteractionActive = false;
        }

        private void OnPowerPlayStarted(float duration)
        {
            if (!IsOwner) return;
            _powerPlayActive = true;
            // Power Play temporarily overrides question gating
        }

        private void OnPowerPlayEnded()
        {
            if (!IsOwner) return;
            _powerPlayActive = false;
        }

        private void OnAnswerModeExited()
        {
            if (!IsOwner) return;
            // Question interaction is fully finished; allow attacks again if cooldown elapsed
            _questionActive = false;
            _answerInteractionActive = false;
            // Do not force-enable attacks here; we respect postAnswerAttackLockSeconds timer
        }

        private void OnAttackChargesChanged(int previousValue, int newValue)
        {
            UpdateChargesUI();

            // Update HUD via EventBus
            EventBus.RaiseChargesChanged(newValue, maxAttackCharges);
        }

        public void AwardCharges()
        {
            if (IsServer)
            {
                int before = attackCharges.Value;
                attackCharges.Value = Mathf.Clamp(attackCharges.Value + chargesPerCorrect, 0, maxAttackCharges);
                Debug.Log($"[PlayerCombat][Server] AwardCharges to client {OwnerClientId}: before={before}, +{chargesPerCorrect} -> {attackCharges.Value} (max={maxAttackCharges})");
            }
        }

        // Instantly fills the player's attack charges to max on the server (used by Power Play)
        public void FillChargesToMax()
        {
            if (IsServer)
            {
                int before = attackCharges.Value;
                attackCharges.Value = maxAttackCharges;
                Debug.Log($"[PlayerCombat][Server] FillChargesToMax for client {OwnerClientId}: before={before} -> {attackCharges.Value}");
            }
        }

        void UpdateChargesUI()
        {
            if (chargesUIPlaceholder == null) return;
            var txt = chargesUIPlaceholder.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) { txt.text = $"Charges: {attackCharges.Value}/{maxAttackCharges}"; return; }
            var slider = chargesUIPlaceholder.GetComponentInChildren<Slider>();
            if (slider != null) { slider.maxValue = Mathf.Max(1, maxAttackCharges); slider.value = attackCharges.Value; return; }
            var img = chargesUIPlaceholder.GetComponentInChildren<Image>();
            if (img != null)
            {
                float pct = Mathf.Clamp01(maxAttackCharges > 0 ? (float)attackCharges.Value / maxAttackCharges : 0f);
                img.fillAmount = pct;
            }
        }

        // Keep alignment updated each frame for consistent visuals across clients
        void LateUpdate()
        {
            UpdateHitboxAlignment();
        }

        // Called from UI (QuestionPanelController) to immediately suppress attacks when the Answer button is pressed
        public void BeginAnswerInteractionLocal()
        {
            if (!IsOwner) return;
            _answerInteractionActive = true;
            // Ensure attacks are suppressed for at least the configured lock window
            _attacksReenabledAtTime = Mathf.Max(_attacksReenabledAtTime, Time.time + postAnswerAttackLockSeconds);
        }

        // Utility: allow external systems to extend suppression for a custom duration
        public void SuppressAttacksForSeconds(float duration)
        {
            if (!IsOwner) return;
            _attacksReenabledAtTime = Mathf.Max(_attacksReenabledAtTime, Time.time + Mathf.Max(0f, duration));
        }
    }
}