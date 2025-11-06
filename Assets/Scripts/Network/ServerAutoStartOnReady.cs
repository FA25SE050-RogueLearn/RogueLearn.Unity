using System;
using System.Linq;
using System.Collections;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using BossFight2D.Quiz;

namespace BossFight2D.Network
{
    /// <summary>
    /// Server-only helper that ensures gameplay can start in headless mode without UI.
    /// - If QuizManager is present and Idle, auto-ready all connected players to start the quiz.
    /// - If QuizManager is NOT present (e.g., in HostUI/ServerHeadless), auto-load the target gameplay scene
    ///   once at least one client connects.
    /// Attach to a GameObject or create programmatically; it is safe to keep DontDestroyOnLoad.
    /// </summary>
    public class ServerAutoStartOnReady : MonoBehaviour
    {
        [Tooltip("Run only in batch/headless mode. If false, will also act in normal builds.")]
        [SerializeField] private bool requireBatchMode = true;

        [Tooltip("Delay before auto actions (seconds), to allow clients to fully spawn.")]
        [SerializeField] private float readyDelaySeconds = 1.5f;

        [Tooltip("Gameplay scene to load when QuizManager is missing.")]
        [SerializeField] private string targetSceneName = "Gameplay";

        [Tooltip("If true, auto-load gameplay when at least one client connects and QuizManager is not yet present.")]
        [SerializeField] private bool autoLoadGameplayIfMissingQuizManager = true;

        [Tooltip("If true, auto-mark all connected players as ready when QuizManager is Idle. If false, players must ready manually (e.g., via ReadyStation).")]
        [SerializeField] private bool autoReadyAllPlayers = false;

        private bool _started;
        private bool _gameplayLoadTriggered;

        private void Awake()
        {
            if (requireBatchMode && !Application.isBatchMode)
            {
                enabled = false;
                return;
            }

            // Optional environment-driven configuration for headless server behavior
            try
            {
                var autoloadEnv = Environment.GetEnvironmentVariable("HEADLESS_AUTOLOAD_ON_FIRST_CLIENT");
                if (!string.IsNullOrWhiteSpace(autoloadEnv))
                {
                    autoLoadGameplayIfMissingQuizManager =
                        autoloadEnv.Equals("1") || autoloadEnv.Equals("true", StringComparison.OrdinalIgnoreCase);
                }

                // Optional: control whether to auto mark players ready
                var autoReadyEnv = Environment.GetEnvironmentVariable("HEADLESS_AUTO_READY_ALL");
                if (!string.IsNullOrWhiteSpace(autoReadyEnv))
                {
                    autoReadyAllPlayers = autoReadyEnv.Equals("1") || autoReadyEnv.Equals("true", StringComparison.OrdinalIgnoreCase);
                }

                var gameplayEnv = Environment.GetEnvironmentVariable("GAMEPLAY_SCENE");
                if (!string.IsNullOrWhiteSpace(gameplayEnv))
                {
                    targetSceneName = gameplayEnv;
                }

                var delayEnv = Environment.GetEnvironmentVariable("AUTO_READY_DELAY_SECONDS");
                if (!string.IsNullOrWhiteSpace(delayEnv) && float.TryParse(delayEnv, out var delay))
                {
                    // Clamp to a reasonable range to avoid pathological values
                    readyDelaySeconds = Mathf.Clamp(delay, 0f, 10f);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ServerAutoStartOnReady] Env config error: {e.Message}");
            }

            Debug.Log($"[ServerAutoStartOnReady] Config: requireBatchMode={requireBatchMode}, batch={Application.isBatchMode}, autoLoadGameplayIfMissingQuizManager={autoLoadGameplayIfMissingQuizManager}, autoReadyAllPlayers={autoReadyAllPlayers}, targetScene='{targetSceneName}', readyDelaySeconds={readyDelaySeconds}");
        }

        private void OnEnable()
        {
            StartCoroutine(RunAsync());
        }

        private IEnumerator RunAsync()
        {
            // Wait for NetworkManager and server to be ready
            while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                yield return null;
            }

            // Small delay to allow scene objects to spawn and clients to be recognized
            yield return new WaitForSeconds(readyDelaySeconds);

            var nm = NetworkManager.Singleton;
            while (!_started)
            {
                var qm = BossFight2D.Quiz.QuizManager.Instance;

                if (qm == null)
                {
                    // Only auto-load Gameplay from the ServerHeadless scene.
                    // In HostUI/Lobby we want players to ready up and let lobby logic transition.
                    var active = SceneManager.GetActiveScene().name;
                    var canAutoLoadFromHeadless = string.Equals(active, "ServerHeadless", System.StringComparison.OrdinalIgnoreCase);
                    // Only trigger autoload when there's at least one external client connected (exclude the server's own host client).
                    var ids = nm.ConnectedClientsIds;
                    var anyExternalClient = ids != null && ids.Any(id => id != NetworkManager.ServerClientId);
                    if (canAutoLoadFromHeadless && autoLoadGameplayIfMissingQuizManager && !_gameplayLoadTriggered && nm.IsListening && anyExternalClient && nm.SceneManager != null)
                    {
                        if (!string.Equals(active, targetSceneName))
                        {
                            Debug.Log($"[ServerAutoStartOnReady] Loading '{targetSceneName}' for all clients (active='{active}').");
                            nm.SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
                            _gameplayLoadTriggered = true;
                            // Allow time for scene load to propagate
                            yield return new WaitForSeconds(1.0f);
                        }
                    }
                }
                else
                {
                    if (qm.IsServer && qm.State.Value == BossFight2D.Quiz.QuizState.Idle)
                    {
                        var ids = nm.ConnectedClientsIds;
                        if (ids != null && ids.Count > 0)
                        {
                            if (autoReadyAllPlayers)
                            {
                                foreach (var id in ids)
                                {
                                    // In headless mode, the server runs as Host to bind Relay but is not a playable client.
                                    // Skip auto-readying the server/host client to avoid mismatched counts.
                                    if (Application.isBatchMode && nm.IsHost && id == NetworkManager.ServerClientId)
                                    {
                                        continue;
                                    }
                                    if (!qm.IsPlayerReady(id))
                                    {
                                        qm.PlayerReadyChanged(id, true);
                                    }
                                }

                                // If all ready, QuizManager will transition to Question state
                                if (qm.State.Value == BossFight2D.Quiz.QuizState.Question)
                                {
                                    _started = true;
                                    Debug.Log("[ServerAutoStartOnReady] Quiz started automatically (all players ready).");
                                }
                            }
                            else
                            {
                                // Auto-ready disabled: wait for players to ready manually via gameplay mechanisms.
                                // If you want to start automatically, set env HEADLESS_AUTO_READY_ALL=1 (or true).
                            }
                        }
                    }
                }

                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
