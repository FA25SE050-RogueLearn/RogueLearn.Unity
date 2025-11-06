using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using BossFight2D.Core;
using BossFight2D.Combat;
using BossFight2D.Quiz;
using BossFight2D.Systems;
using UnityEngine.EventSystems;

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
            if (attackCharges.Value >= chargeCostPerAttack)
            {
                attackCharges.Value -= chargeCostPerAttack;
                _nextAttackAllowed = Time.time + attackCooldown;



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
        }

        public void AwardCharges()
        {
            if (IsServer)
            {
                attackCharges.Value = Mathf.Clamp(attackCharges.Value + chargesPerCorrect, 0, maxAttackCharges);
            }
        }

        // Instantly fills the player's attack charges to max on the server (used by Power Play)
        public void FillChargesToMax()
        {
            if (IsServer)
            {
                attackCharges.Value = maxAttackCharges;
            }
        }

        void UpdateChargesUI()
        {
            if (chargesUIPlaceholder == null) return;
            var txt = chargesUIPlaceholder.GetComponent<Text>();
            if (txt != null) { txt.text = $"Charges: {attackCharges.Value}/{maxAttackCharges}"; return; }
            var slider = chargesUIPlaceholder.GetComponent<Slider>();
            if (slider != null) { slider.maxValue = Mathf.Max(1, maxAttackCharges); slider.value = attackCharges.Value; return; }
            var img = chargesUIPlaceholder.GetComponent<Image>();
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