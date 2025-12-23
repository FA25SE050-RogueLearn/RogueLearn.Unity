// PlayerHUD.cs - Binds PlayerHealth/PlayerFocus to UI Sliders named "Health" and "Focus"
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Netcode;

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
                healthSlider = FindSliderInLoadedScenes("Health");
            }
            if (focusSlider == null)
            {
                focusSlider = FindSliderInLoadedScenes("Focus");
            }

            // Resolve gameplay components
            TryBindLocalPlayerComponents();
            var nm = NetworkManager.Singleton;
            var netcodeActive = nm != null && (nm.IsClient || nm.IsServer);
            if (!netcodeActive)
            {
                if (playerHealth == null) playerHealth = FindFirstObjectByType<BossFight2D.Player.PlayerHealth>();
                if (playerFocus == null) playerFocus = FindFirstObjectByType<BossFight2D.Player.PlayerFocus>();
            }

            // Initialize once
            UpdateBars(force: true);
        }

        private static Slider FindSliderInLoadedScenes(string objectName)
        {
            var sliders = Resources.FindObjectsOfTypeAll<Slider>();
            foreach (var s in sliders)
            {
                if (s == null) continue;
                var go = s.gameObject;
                if (go == null) continue;
                if (go.name != objectName) continue;
                var scene = go.scene;
                if (!scene.IsValid() || !scene.isLoaded) continue;
                return s;
            }
            return null;
        }

        private void Update()
        {
            if (playerHealth == null || playerFocus == null)
            {
                TryBindLocalPlayerComponents();
            }
            // Polling approach keeps UI in sync without requiring gameplay events
            UpdateBars(force: false);
        }

        private void TryBindLocalPlayerComponents()
        {
            var nm = NetworkManager.Singleton;
            var localPlayerObject = nm != null ? nm.LocalClient?.PlayerObject : null;
            if (localPlayerObject == null) return;

            if (playerHealth == null)
            {
                playerHealth = localPlayerObject.GetComponent<BossFight2D.Player.PlayerHealth>();
            }

            if (playerFocus == null)
            {
                playerFocus = localPlayerObject.GetComponent<BossFight2D.Player.PlayerFocus>();
            }
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
