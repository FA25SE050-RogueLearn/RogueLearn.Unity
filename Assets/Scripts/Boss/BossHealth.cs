using Unity.Netcode;
using UnityEngine;
using BossFight2D.Boss;
using BossFight2D.Network;
using BossFight2D.Systems;

public class BossHealth : NetworkBehaviour
{
    [SerializeField] private int maxHealth = 300;  // MVP: Tuned for 5-8 minute matches with 2-3 players

    public delegate void HealthChangedDelegate(int newHealth, int maxHealth);
    public event HealthChangedDelegate OnHealthChanged;

    public NetworkVariable<int> currentHealth = new NetworkVariable<int>();
    private bool _defeated;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }

        currentHealth.OnValueChanged += (oldValue, newValue) =>
        {
            OnHealthChanged?.Invoke(newValue, maxHealth);

            // Update HUD via EventBus
            EventBus.RaiseBossHealthChanged(newValue, maxHealth);
        };

        // Initialize HUD with full health
        EventBus.RaiseBossHealthChanged(currentHealth.Value, maxHealth);
    }

    public void TakeDamage(int damage)
    {
        if (IsServer)
        {
            currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);
            if (currentHealth.Value <= 0 && !_defeated)
            {
                _defeated = true;
                Debug.Log("Boss has been defeated!");
                var gm = BossFight2D.Core.GameObjectFactory.FindOrCreate<BossFight2D.Core.GameManager>();
                if (gm != null) gm.WinGame();
                else NetworkGameState.ServerSetWin();
            }
        }
    }

    public int GetMaxHealth()
    {
        return maxHealth;
    }
}
