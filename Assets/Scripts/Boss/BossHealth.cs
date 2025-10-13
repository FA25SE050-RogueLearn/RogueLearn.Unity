using Unity.Netcode;
using UnityEngine;

public class BossHealth : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 1000;

    public delegate void HealthChangedDelegate(int newHealth, int maxHealth);
    public event HealthChangedDelegate OnHealthChanged;

    public NetworkVariable<int> currentHealth = new NetworkVariable<int>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }

        currentHealth.OnValueChanged += (oldValue, newValue) =>
        {
            OnHealthChanged?.Invoke(newValue, maxHealth);
        };
    }

    public void TakeDamage(int damage)
    {
        if (IsServer)
        {
            currentHealth.Value -= damage;
            if (currentHealth.Value <= 0)
            {
                Debug.Log("Boss has been defeated!");
                // Handle boss death logic here
            }
        }
    }

    public int GetMaxHealth()
    {
        return maxHealth;
    }
}