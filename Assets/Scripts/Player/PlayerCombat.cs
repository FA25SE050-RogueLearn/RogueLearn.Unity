using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using BossFight2D.Core;
using BossFight2D.Combat;

namespace BossFight2D.Player
{
    public class PlayerCombat : NetworkBehaviour
    {

        public Hitbox2D hitbox;
        public Animator animator;
        public int defaultDamage = 10;
        public float attackCooldown = 0.5f;

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

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (hitbox == null) hitbox = GetComponentInChildren<Hitbox2D>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                attackCharges.OnValueChanged += OnAttackChargesChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                attackCharges.OnValueChanged -= OnAttackChargesChanged;
            }
        }

        void Update()
        {
            if (!IsOwner) return;

            // In a real game, you'd check for a "Power Play" state here
            // For now, we'll allow attacks if the player has charges.
            if (Input.GetMouseButtonDown(0) && Time.time >= _nextAttackAllowed)
            {
                if (attackCharges.Value > 0)
                {
                    RequestAttackServerRpc();
                }
            }
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
    }
}