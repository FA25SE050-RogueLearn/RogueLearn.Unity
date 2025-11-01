using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using BossFight2D.Systems;

namespace BossFight2D.Systems
{
    /// <summary>
    /// Minimal server-side match recorder that writes a JSON summary to RESULTS_LOG_ROOT
    /// when a match ends (win/lose). Designed for headless runs. It records start/end times,
    /// result, scene, and participating client IDs. Extend as needed for question/answer details.
    /// </summary>
    public class ServerMatchRecorder : MonoBehaviour
    {
        [Serializable]
        public class MatchSummary
        {
            public string matchId;
            public string startUtc;
            public string endUtc;
            public string result; // "win" or "lose"
            public string scene;
            public string relayRegion;
            public string joinCode; // optional; may be empty if not available
            public int totalPlayers;
            public ulong hostClientId;
            public ulong[] playerClientIds;
        }

        private DateTime _startTimeUtc;
        private bool _written;
        private string _matchId;

        private void Awake()
        {
            if (!Application.isBatchMode)
            {
                // Only run in server/headless contexts
                enabled = false;
                return;
            }
            DontDestroyOnLoad(gameObject);
            _startTimeUtc = DateTime.UtcNow;
            _matchId = Guid.NewGuid().ToString("N");

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
            WriteSummary("win");
        }

        private void OnGameLost()
        {
            WriteSummary("lose");
        }

        private void WriteSummary(string result)
        {
            if (_written) return;
            _written = true;

            try
            {
                var nm = NetworkManager.Singleton;
                var players = new List<ulong>();
                if (nm != null)
                {
                    players.AddRange(nm.ConnectedClientsIds);
                }

                var summary = new MatchSummary
                {
                    matchId = _matchId,
                    startUtc = _startTimeUtc.ToString("o"),
                    endUtc = DateTime.UtcNow.ToString("o"),
                    result = result,
                    scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    relayRegion = Environment.GetEnvironmentVariable("RELAY_REGION") ?? string.Empty,
                    joinCode = string.Empty, // Not currently available programmatically here
                    totalPlayers = players.Count,
                    hostClientId = nm != null ? nm.LocalClientId : 0,
                    playerClientIds = players.ToArray()
                };

                var root = Environment.GetEnvironmentVariable("RESULTS_LOG_ROOT");
                if (string.IsNullOrWhiteSpace(root))
                {
                    root = "/var/log/unity/matches"; // default path compatible with VM/container setup
                }

                var dateDir = Path.Combine(root, DateTime.UtcNow.ToString("yyyy-MM-dd"));
                Directory.CreateDirectory(dateDir);

                string fileName = $"match_{DateTime.UtcNow:HHmmss}_{_matchId}.json";
                var path = Path.Combine(dateDir, fileName);

                var json = JsonUtility.ToJson(summary, true);
                File.WriteAllText(path, json);
                Debug.Log($"[ServerMatchRecorder] Wrote match summary: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ServerMatchRecorder] Failed to write match summary: {ex}");
            }
        }
    }
}
