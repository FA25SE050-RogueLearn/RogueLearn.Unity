// PlayerHUD.cs - Binds PlayerHealth/PlayerFocus to UI Sliders named "Health" and "Focus"
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BossFight2D.UI
{
    public class PlayerHUD : MonoBehaviour
    {
        [Header("Optional explicit bindings (auto-resolved by name if empty)")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider focusSlider;
        [SerializeField] private BossFight2D.Player.PlayerHealth playerHealth;
        [SerializeField] private BossFight2D.Player.PlayerFocus playerFocus;

        private void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            FindAndBind();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Re-find and bind references when a new scene loads (e.g., after a replay)
            FindAndBind();
        }

        private void FindAndBind()
        {
            // Resolve UI by common names if not explicitly assigned
            if (healthSlider == null)
            {
                var go = GameObject.Find("Health");
                if (go != null) healthSlider = go.GetComponent<Slider>();
            }
            if (focusSlider == null)
            {
                var go = GameObject.Find("Focus");
                if (go != null) focusSlider = go.GetComponent<Slider>();
            }

            // Resolve gameplay components
            if (playerHealth == null) playerHealth = FindFirstObjectByType<BossFight2D.Player.PlayerHealth>();
            if (playerFocus == null) playerFocus = FindFirstObjectByType<BossFight2D.Player.PlayerFocus>();

            // Initialize once
            UpdateBars(force: true);
        }

        private void Update()
        {
            // Polling approach keeps UI in sync without requiring gameplay events
            UpdateBars(force: false);
        }

        private void UpdateBars(bool force)
        {
            if (playerHealth != null && healthSlider != null)
            {
                int maxHearts = playerHealth.maxHearts.Value;
                int curHearts = playerHealth.hearts.Value;
                float h = maxHearts > 0 ? (float)curHearts / maxHearts : 0f;
                if (force || !Mathf.Approximately(healthSlider.value, h))
                {
                    healthSlider.SetValueWithoutNotify(h);
                }
            }

            if (playerFocus != null && focusSlider != null)
            {
                float f = playerFocus.max > 0 ? (float)playerFocus.current / playerFocus.max : 0f;
                if (force || !Mathf.Approximately(focusSlider.value, f))
                {
                    focusSlider.SetValueWithoutNotify(f);
                }
            }
        }
    }
}