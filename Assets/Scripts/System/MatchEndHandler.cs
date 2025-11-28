using System.Collections;
using UnityEngine;
using Unity.Netcode;

namespace BossFight2D.Systems
{
    /// <summary>
    /// Handles match end events and notifies the frontend to navigate to stats page
    /// </summary>
    public class MatchEndHandler : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float delayBeforeNavigate = 3f; // Show victory/defeat screen for 3 seconds

        private bool _matchEnded = false;

        private void Awake()
        {
            EventBus.GameWon += OnGameWon;
            EventBus.GameLost += OnGameLost;
        }

        private void OnDestroy()
        {
            EventBus.GameWon -= OnGameWon;
            EventBus.GameLost -= OnGameLost;
        }

        private void OnGameWon()
        {
            HandleMatchEnd("win");
        }

        private void OnGameLost()
        {
            HandleMatchEnd("lose");
        }

        private void HandleMatchEnd(string result)
        {
            if (_matchEnded) return;
            _matchEnded = true;

            Debug.Log($"[MatchEndHandler] Match ended with result: {result}");

            // Only navigate on WebGL clients (not headless server)
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                StartCoroutine(NavigateToStatsAfterDelay(result));
            }
        }

        private IEnumerator NavigateToStatsAfterDelay(string result)
        {
            Debug.Log($"[MatchEndHandler] Will navigate to stats in {delayBeforeNavigate} seconds");

            // Wait to show victory/defeat screen
            yield return new WaitForSeconds(delayBeforeNavigate);

            // Send message to frontend via Unity WebGL external call
            NavigateToStats(result);
        }

        /// <summary>
        /// Call JavaScript function in the browser to navigate to stats page
        /// </summary>
        private void NavigateToStats(string result)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                Debug.Log($"[MatchEndHandler] Calling JavaScript to navigate to /stats");

                // Call JavaScript function defined in the Unity WebGL template
                // Format: window.navigateToStats(result)
                Application.ExternalEval($"if (window.navigateToStats) {{ window.navigateToStats('{result}'); }}");

                Debug.Log("[MatchEndHandler] Navigation request sent to browser");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MatchEndHandler] Failed to navigate to stats: {ex.Message}");
            }
#else
            Debug.Log($"[MatchEndHandler] Not in WebGL build, skipping navigation to stats (result: {result})");
#endif
        }
    }
}
