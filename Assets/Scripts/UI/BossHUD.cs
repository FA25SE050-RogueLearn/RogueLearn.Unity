// BossHUD.cs - Binds BossStateMachine HP to a UI Slider named "BossHealth"
using UnityEngine;
using UnityEngine.UI;

namespace BossFight2D.UI
{
    public class BossHUD : MonoBehaviour
    {
        [Header("Optional explicit bindings (auto-resolved by name if empty)")]
        [SerializeField] private Slider bossHealthSlider;
        [SerializeField] private BossFight2D.Boss.BossStateMachine boss;

        private void Awake()
        {
            // Resolve UI by common name if not explicitly assigned
            if (bossHealthSlider == null)
            {
                var go = GameObject.Find("BossHealth");
                if (go != null) bossHealthSlider = go.GetComponent<Slider>();
            }


        }

        private void Start()
        {
            // Resolve gameplay component
            if (boss == null) boss = FindFirstObjectByType<BossFight2D.Boss.BossStateMachine>();
            // Ensure slider normalized range
            if (bossHealthSlider != null)
            {
                bossHealthSlider.minValue = 0f;
                bossHealthSlider.maxValue = 1f;
            }

            if (boss != null)
            {
                BossHealth health = boss.GetComponent<BossHealth>();
                if (health != null)
                {
                    health.OnHealthChanged += UpdateHealthBar;
                    // Initial update
                    UpdateHealthBar(health.currentHealth.Value, health.GetMaxHealth());
                }
            }
        }

        private void OnDestroy()
        {
            if (boss != null)
            {
                BossHealth health = boss.GetComponent<BossHealth>();
                if (health != null)
                {
                    health.OnHealthChanged -= UpdateHealthBar;
                }
            }
        }

        private void UpdateHealthBar(int currentHealth, int maxHealth)
        {
            if (bossHealthSlider != null)
            {
                float healthPercentage = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
                Debug.Log($"Updating health bar: {healthPercentage}");
                bossHealthSlider.SetValueWithoutNotify(healthPercentage);
            }
        }
    }
}