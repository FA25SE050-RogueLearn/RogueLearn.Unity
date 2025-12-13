using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using BossFight2D.Systems;
using BossFight2D.Quiz;
using BossFight2D.Network;

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
        public class QuestionResult
        {
            public int questionId;
            public string topic;
            public string difficulty;
            public string prompt;
            public int correctAnswerIndex;
            public PlayerAnswer[] playerAnswers;
        }

        [Serializable]
        public class PlayerAnswer
        {
            public ulong playerId;  // Can be mapped to user_id on backend
            public int chosenAnswer;
            public bool correct;
            public float timeToAnswer; // seconds
        }

        [Serializable]
        public class PlayerSummary
        {
            public ulong playerId;
            public string userId;
            public int totalQuestions;
            public int correctAnswers;
            public float averageTime;
            public TopicAccuracy[] topicBreakdown;
        }

        [Serializable]
        public class TopicAccuracy
        {
            public string topic;
            public int correct;
            public int total;
        }

        [Serializable]
        public class MatchSummary
        {
            public string matchId;
            public string userId; // Supabase user ID of the match host
            public string startUtc;
            public string endUtc;
            public string result; // "win" or "lose"
            public string scene;
            public string relayRegion;
            public string joinCode; // optional; may be empty if not available
            public int totalPlayers;
            public ulong hostClientId;
            public ulong[] playerClientIds;

            // MVP: Enhanced data for exam prep
            public QuestionResult[] questions;
            public PlayerSummary[] playerSummaries;
        }

        private DateTime _startTimeUtc;
        private bool _written;
        private string _matchId;
        private string _relayJoinCode = string.Empty;

        // MVP: Track questions and answers during match
        private List<QuestionResult> _questionResults = new List<QuestionResult>();
        private Dictionary<int, DateTime> _questionStartTimes = new Dictionary<int, DateTime>();
        private QuestionData _currentQuestion;

        private void Awake()
        {
            Debug.Log($"[ServerMatchRecorder] Awake - isBatchMode: {Application.isBatchMode}");

            if (!Application.isBatchMode)
            {
                // Only run in server/headless contexts
                Debug.Log("[ServerMatchRecorder] Not in batch mode, disabling");
                enabled = false;
                return;
            }

            DontDestroyOnLoad(gameObject);
            _startTimeUtc = DateTime.UtcNow;
            _matchId = Guid.NewGuid().ToString("N");

            var envJoin = System.Environment.GetEnvironmentVariable("RELAY_JOIN_CODE") ?? System.Environment.GetEnvironmentVariable("RL_RELAY_JOIN_CODE");
            if (!string.IsNullOrWhiteSpace(envJoin))
            {
                _relayJoinCode = envJoin.Trim();
                Debug.Log($"[ServerMatchRecorder] Using relay join code from env");
            }

            Debug.Log($"[ServerMatchRecorder] Initialized - MatchID: {_matchId}");
            Debug.Log("[ServerMatchRecorder] Subscribing to EventBus events");

            EventBus.GameWon += OnGameWon;
            EventBus.GameLost += OnGameLost;
            EventBus.QuestionStarted += OnQuestionStarted;
            EventBus.AnswerResolved += OnAnswerResolved;

            Debug.Log("[ServerMatchRecorder] Ready to track match");
        }

        private void OnDestroy()
        {
            EventBus.GameWon -= OnGameWon;
            EventBus.GameLost -= OnGameLost;
            EventBus.QuestionStarted -= OnQuestionStarted;
            EventBus.AnswerResolved -= OnAnswerResolved;
        }

        private void OnQuestionStarted(QuestionData question)
        {
            Debug.Log($"[ServerMatchRecorder] OnQuestionStarted - Question ID: {question.id}, Topic: {question.topic}");
            _currentQuestion = question;
            _questionStartTimes[question.id] = DateTime.UtcNow;
        }

        private void OnAnswerResolved()
        {
            Debug.Log($"[ServerMatchRecorder] OnAnswerResolved - Current question: {(_currentQuestion != null ? _currentQuestion.id.ToString() : "NULL")}");

            if (_currentQuestion == null)
            {
                Debug.LogWarning("[ServerMatchRecorder] OnAnswerResolved called but _currentQuestion is null!");
                return;
            }

            // Collect player answers from QuizManager
            var quizManager = QuizManager.Instance;
            if (quizManager == null)
            {
                Debug.LogWarning("[ServerMatchRecorder] QuizManager.Instance is null!");
                return;
            }

            var playerAnswers = new List<PlayerAnswer>();
            var endTime = DateTime.UtcNow;
            var answers = quizManager.GetPlayerAnswers();

            Debug.Log($"[ServerMatchRecorder] GetPlayerAnswers returned {answers.Count} entries");

            foreach (var kvp in answers)
            {
                ulong playerId = kvp.Key;
                int chosenAnswer = kvp.Value;
                bool correct = chosenAnswer == _currentQuestion.correctIndex;

                float timeToAnswer = 0f;
                if (_questionStartTimes.TryGetValue(_currentQuestion.id, out DateTime startTime))
                {
                    timeToAnswer = (float)(endTime - startTime).TotalSeconds;
                }

                Debug.Log($"[ServerMatchRecorder] Player {playerId} answered {chosenAnswer} (correct: {correct})");

                playerAnswers.Add(new PlayerAnswer
                {
                    playerId = playerId,
                    chosenAnswer = chosenAnswer,
                    correct = correct,
                    timeToAnswer = timeToAnswer
                });
            }

            _questionResults.Add(new QuestionResult
            {
                questionId = _currentQuestion.id,
                topic = _currentQuestion.topic,
                difficulty = _currentQuestion.difficulty,
                prompt = _currentQuestion.prompt,
                correctAnswerIndex = _currentQuestion.correctIndex,
                playerAnswers = playerAnswers.ToArray()
            });

            Debug.Log($"[ServerMatchRecorder] Recorded question result. Total questions: {_questionResults.Count}");
            _currentQuestion = null;
        }

        private void OnGameWon()
        {
            WriteSummary("win");
        }

        private void OnGameLost()
        {
            WriteSummary("lose");
        }

        private PlayerSummary[] ComputePlayerSummaries(List<ulong> players)
        {
            var summaries = new List<PlayerSummary>();

            foreach (var playerId in players)
            {
                int totalQuestions = 0;
                int correctAnswers = 0;
                float totalTime = 0f;
                var topicStats = new Dictionary<string, (int correct, int total)>();

                foreach (var question in _questionResults)
                {
                    var playerAnswer = question.playerAnswers.FirstOrDefault(pa => pa.playerId == playerId);
                    if (playerAnswer != null)
                    {
                        totalQuestions++;
                        totalTime += playerAnswer.timeToAnswer;

                        if (playerAnswer.correct)
                            correctAnswers++;

                        // Track by topic
                        if (!topicStats.ContainsKey(question.topic))
                            topicStats[question.topic] = (0, 0);

                        var stats = topicStats[question.topic];
                        topicStats[question.topic] = (
                            stats.correct + (playerAnswer.correct ? 1 : 0),
                            stats.total + 1
                        );
                    }
                }

                var topicBreakdown = topicStats.Select(kvp => new TopicAccuracy
                {
                    topic = kvp.Key,
                    correct = kvp.Value.correct,
                    total = kvp.Value.total
                }).ToArray();

                summaries.Add(new PlayerSummary
                {
                    playerId = playerId,
                    totalQuestions = totalQuestions,
                    correctAnswers = correctAnswers,
                    averageTime = totalQuestions > 0 ? totalTime / totalQuestions : 0f,
                    topicBreakdown = topicBreakdown
                });
            }

            return summaries.ToArray();
        }

        // NEW: Get player summaries from QuizManager (same data source as GameSessionClient)
        private PlayerSummary[] ComputePlayerSummariesFromQuizManager(List<ulong> players, QuizManager quizManager)
        {
            if (quizManager == null)
            {
                // Fallback to event-based tracking if QuizManager not available
                return ComputePlayerSummaries(players);
            }

            try
            {
                var summaryData = quizManager.GetTopicSummary();
                if (summaryData == null || summaryData.topics == null)
                {
                    return ComputePlayerSummaries(players);
                }

                var summaries = new List<PlayerSummary>();

                foreach (var playerId in players)
                {
                    // Get topic stats from QuizManager
                    int totalQuestions = 0;
                    int correctAnswers = 0;
                    var topicBreakdown = new List<TopicAccuracy>();

                    foreach (var topic in summaryData.topics)
                    {
                        totalQuestions += topic.total;
                        correctAnswers += topic.correct;

                        topicBreakdown.Add(new TopicAccuracy
                        {
                            topic = topic.topic,
                            correct = topic.correct,
                            total = topic.total
                        });
                    }

                    summaries.Add(new PlayerSummary
                    {
                        playerId = playerId,
                        totalQuestions = totalQuestions,
                        correctAnswers = correctAnswers,
                        averageTime = 0f, // QuizManager doesn't track timing
                        topicBreakdown = topicBreakdown.ToArray()
                    });
                }

                return summaries.ToArray();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ServerMatchRecorder] Failed to get summary from QuizManager: {ex.Message}. Falling back to event tracking.");
                return ComputePlayerSummaries(players);
            }
        }

        private string ResolveBackendBaseUrl()
        {
            string ResolveFromEnv(params string[] keys)
            {
                foreach (var k in keys)
                {
                    var v = System.Environment.GetEnvironmentVariable(k);
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
                return string.Empty;
            }

            // Prefer explicit Unity overrides, then shared frontend/backend vars
            var backendUrl = ResolveFromEnv(
                "RL_DOCKER_USER_API_BASE",
                "USER_API_BASE",
                "NEXT_PUBLIC_API_URL",
                "NEXT_PUBLIC_USER_API_URL",
                "API_URL");

            return string.IsNullOrWhiteSpace(backendUrl)
                ? string.Empty
                : backendUrl.Trim().TrimEnd('/');
        }

        private void WriteSummary(string result)
        {
            Debug.Log($"[ServerMatchRecorder] WriteSummary called - result: {result}");
            Debug.Log($"[ServerMatchRecorder] Tracked {_questionResults.Count} questions via events");

            if (_written) return;
            _written = true;

            try
            {
                var nm = NetworkManager.Singleton;
                var players = new List<ulong>();
                if (nm != null)
                {
                    // MVP: Exclude headless server from player stats (it's not a real player)
                    bool isHeadlessHost = Application.isBatchMode && nm.IsHost;
                    foreach (var clientId in nm.ConnectedClientsIds)
                    {
                        // Skip the server/host in headless mode
                        if (isHeadlessHost && clientId == NetworkManager.ServerClientId)
                            continue;
                        players.Add(clientId);
                    }
                }

                Debug.Log($"[ServerMatchRecorder] Found {players.Count} player(s): {string.Join(", ", players)}");

                // Compute INDIVIDUAL per-player summaries from QuizManager
                // QuizManager has the authoritative stats data
                var quizManager = QuizManager.Instance;
                Debug.Log($"[ServerMatchRecorder] QuizManager.Instance: {(quizManager != null ? "Available" : "NULL")}");

                var playerSummaries = ComputePlayerSummariesFromQuizManager(players, quizManager);

                Debug.Log($"[ServerMatchRecorder] Computed {playerSummaries.Length} player summaries");

                // POST SEPARATE MATCH RESULTS FOR EACH PLAYER
                // This allows each player to see only their own stats in the stats page
                var backendUrl = ResolveBackendBaseUrl();
                if (string.IsNullOrWhiteSpace(backendUrl))
                {
                    Debug.LogError("[ServerMatchRecorder] USER_API_BASE/NEXT_PUBLIC_USER_API_URL is not set; cannot post match results.");
                    return;
                }
                var apiEndpoint = $"{backendUrl}/api/quests/game/sessions/unity-match-result";

                // Prefer session id resolved by GameSessionClient so matchId aligns with backend session
                var sessionIdFromClient = GameSessionClient.Instance != null ? GameSessionClient.Instance.SessionId : string.Empty;
                var resolvedMatchId = !string.IsNullOrWhiteSpace(sessionIdFromClient) ? sessionIdFromClient : _matchId;
                var resolvedJoinCode = !string.IsNullOrWhiteSpace(_relayJoinCode)
                    ? _relayJoinCode
                    : (GameSessionClient.Instance != null ? GameSessionClient.Instance.LastJoinCode : string.Empty);

                // Stamp userIds per player from PlayerIdentity (guests allowed => empty string)
                foreach (var ps in playerSummaries)
                {
                    var mappedUserId = PlayerIdentity.GetUserIdForClient(ps.playerId);
                    ps.userId = string.IsNullOrWhiteSpace(mappedUserId) ? string.Empty : mappedUserId;
                }

                // Build a single combined payload with all player summaries
                var matchUserId = playerSummaries.FirstOrDefault(ps => !string.IsNullOrEmpty(ps.userId))?.userId ?? string.Empty;

                var summaryCombined = new MatchSummary
                {
                    matchId = resolvedMatchId,
                    userId = matchUserId,
                    startUtc = _startTimeUtc.ToString("o"),
                    endUtc = DateTime.UtcNow.ToString("o"),
                    result = result,
                    scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    relayRegion = System.Environment.GetEnvironmentVariable("RELAY_REGION") ?? string.Empty,
                    joinCode = resolvedJoinCode ?? string.Empty,
                    totalPlayers = players.Count,
                    hostClientId = nm != null ? nm.LocalClientId : 0,
                    playerClientIds = players.ToArray(),
                    questions = _questionResults.ToArray(),
                    playerSummaries = playerSummaries
                };

                var combinedJson = JsonUtility.ToJson(summaryCombined, true);
                Debug.Log($"[ServerMatchRecorder] Posting combined match result with {playerSummaries.Length} player summaries to backend");
                StartCoroutine(PostMatchResultToBackend(apiEndpoint, combinedJson));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ServerMatchRecorder] Failed to post match summary: {ex}");
            }
        }

        private IEnumerator PostMatchResultToBackend(string apiEndpoint, string jsonData)
        {
            Debug.Log($"[ServerMatchRecorder] Posting match result to: {apiEndpoint}");

            using (UnityEngine.Networking.UnityWebRequest request = new UnityEngine.Networking.UnityWebRequest(apiEndpoint, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                // For local development: Accept self-signed certificates
                request.certificateHandler = new AcceptAllCertificatesHandler();
                request.disposeCertificateHandlerOnDispose = true;

                yield return request.SendWebRequest();

                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[ServerMatchRecorder] Successfully posted match result to backend. Response: {request.downloadHandler.text}");
                }
                else
                {
                    Debug.LogError($"[ServerMatchRecorder] Failed to post match result. Error: {request.error}. Response: {request.downloadHandler.text}");
                }
            }
        }

        // Certificate handler to accept self-signed certificates in local development
        private class AcceptAllCertificatesHandler : UnityEngine.Networking.CertificateHandler
        {
            protected override bool ValidateCertificate(byte[] certificateData)
            {
                // Accept all certificates (for development only)
                return true;
            }
        }
    }
}
