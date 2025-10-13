using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using BossFight2D.Player;

public class HealthUI : MonoBehaviour
{
    public Slider healthSlider;
    private PlayerHealth localPlayerHealth;

    void Start()
    {
        // Defer finding the player until the network is ready
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientStarted += FindAndBindPlayer;
        }
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
            }
        }
    }

    private void OnMaxHealthChanged(int previousValue, int newValue)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = newValue;
        }
    }

    private void OnHealthChanged(int previousValue, int newValue)
    {
        if (healthSlider != null)
        {
            healthSlider.value = newValue;
        }
    }
}