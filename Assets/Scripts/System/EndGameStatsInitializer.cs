using UnityEngine;
using BossFight2D.UI;

namespace BossFight2D.Systems
{
    /// <summary>
    /// MVP: Automatically creates EndGameStatsUI if it doesn't exist
    /// Add this script to any GameObject in your game scene to enable end-game stats
    /// </summary>
    public class EndGameStatsInitializer : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            // Only initialize if we don't already have an instance
            if (EndGameStatsUI.Instance == null)
            {
                var go = new GameObject("EndGameStatsUI");
                go.AddComponent<EndGameStatsUI>();
                Debug.Log("[EndGameStatsInitializer] EndGameStatsUI created automatically");
            }
        }
    }
}
