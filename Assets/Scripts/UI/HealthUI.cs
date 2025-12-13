using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using BossFight2D.Player;

public class HealthUI : MonoBehaviour
{
    public Slider healthSlider;
    private PlayerHealth localPlayerHealth;
    private bool _bound;

    void Start()
    {
        // Defer finding the player until the network is ready
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientStarted += FindAndBindPlayer;
        }
        // Also attempt immediate bind in case we're already started
        TryBindImmediate();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientStarted -= FindAndBindPlayer;
        }
        if (localPlayerHealth != null)
        {
            localPlayerHealth.hearts.OnValueChanged -= OnHealthChanged;
            localPlayerHealth.maxHearts.OnValueChanged -= OnMaxHealthChanged;
        }
    }

    void FindAndBindPlayer()
    {
        if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            localPlayerHealth = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerHealth>();
            if (localPlayerHealth != null)
            {
                // Initial setup
                OnMaxHealthChanged(0, localPlayerHealth.maxHearts.Value);
                OnHealthChanged(0, localPlayerHealth.hearts.Value);

                // Subscribe to future changes
                localPlayerHealth.maxHearts.OnValueChanged += OnMaxHealthChanged;
                localPlayerHealth.hearts.OnValueChanged += OnHealthChanged;
                _bound = true;
            }
        }
    }

    void TryBindImmediate()
    {
        if (!_bound)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                FindAndBindPlayer();
            }
        }
    }

    private void OnMaxHealthChanged(int previousValue, int newValue)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = 1f;
        }
    }

    private void OnHealthChanged(int previousValue, int newValue)
    {
        if (healthSlider != null && localPlayerHealth != null)
        {
            int max = Mathf.Max(1, localPlayerHealth.maxHearts.Value);
            float ratio = Mathf.Clamp01((float)newValue / max);
            healthSlider.value = ratio;
        }
    }

    void Update()
    {
        if (!_bound) TryBindImmediate();
        // Polling fallback if events missed
        if (healthSlider != null && localPlayerHealth != null)
        {
            int max = Mathf.Max(1, localPlayerHealth.maxHearts.Value);
            float ratio = Mathf.Clamp01((float)localPlayerHealth.hearts.Value / max);
            if (!Mathf.Approximately(healthSlider.value, ratio))
            {
                healthSlider.value = ratio;
            }
        }
    }
}