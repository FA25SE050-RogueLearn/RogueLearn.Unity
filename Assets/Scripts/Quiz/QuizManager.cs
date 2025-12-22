using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;
using BossFight2D.Core;
using BossFight2D.UI;
using BossFight2D.Systems;
using BossFight2D.Combat;
using BossFight2D.Boss;
using BossFight2D.Network;
using System.IO;

namespace BossFight2D.Quiz
{
    public enum QuizState
    {
        Idle,
        Question,
        Resolution,
        Decision
    }

    public class QuizManager : NetworkBehaviour
    {
        public static QuizManager Instance { get; private set; }

        [SerializeField] private string questionPackFileName;
        private List<QuestionData> questions;

        public NetworkVariable<QuizState> State = new NetworkVariable<QuizState>(QuizState.Idle);
        public NetworkVariable<float> RemainingTime = new NetworkVariable<float>(0f);
        public NetworkVariable<float> CurrentQuestionTimeLimit = new NetworkVariable<float>(10f);
        private NetworkVariable<int> currentQuestionIndex = new NetworkVariable<int>(-1);
        // Replicated count of players who have toggled ready (for client-side UI like the station label)
        public NetworkVariable<int> ReadyCount = new NetworkVariable<int>(0);
        // Replicated total number of connected players (clients + host) for accurate ready display on clients
        public NetworkVariable<int> TotalPlayers = new NetworkVariable<int>(0);

        // Dedicated/headless server runs as Host to bind Relay, but the server is not a playable client.
        // In that mode, exclude the server/host from player counts and readiness requirements.
        private bool ExcludeServerFromPlayerCounts => Application.isBatchMode && NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        private Dictionary<ulong, bool> playerReadyStatus = new Dictionary<ulong, bool>();
        private Dictionary<ulong, int> playerAnswers = new Dictionary<ulong, int>();
        private bool punishTriggered = false;
        private HashSet<ulong> wrongAnswerPlayers = new HashSet<ulong>();
        [System.Serializable]
        public class TopicSummaryData { public string topic; public int total; public int correct; }
        [System.Serializable]
        public class SummaryData { public System.Collections.Generic.List<TopicSummaryData> topics; }
        [Header("Ready Flow")]
        [SerializeField] private bool continuousReadyFlow = true; // If true, keep players ready between rounds while they remain in the station
        [SerializeField] private float interRoundDelaySeconds = 2f; // Pause between rounds before starting next question automatically
        [SerializeField] private float wrongAnswerInterRoundDelaySeconds = 1.0f;
        private float nextQuestionAllowedAt = 0f; // Time gate to avoid instant restart

        [Header("Backend Pack Gating")]
        [SerializeField] private bool requireBackendPackToStart = true; // Prevent starting with default pack until backend pack is injected

        [Header("Power Play Triggering")]
        [SerializeField] private int powerPlayStreakThreshold = 3; // Require N consecutive correct answers to trigger Power Play
        [SerializeField] private bool consumeStreakOnActivation = true; // Reset streak when Power Play activates
        private Dictionary<ulong, int> correctAnswerStreak = new Dictionary<ulong, int>();
        private Dictionary<string, (int total, int correct)> topicStats = new Dictionary<string, (int total, int correct)>();
        [Header("Decision Phase")]
        [SerializeField] private float decisionPhaseSeconds = 3f;
        [SerializeField] private DecisionRule decisionRule = DecisionRule.AnyAttack;
        private float decisionEndsAt = 0f;
        private bool decisionActive = false;  // Track if decision phase is currently active
        private Dictionary<ulong, bool> playerDecisionAttack = new Dictionary<ulong, bool>();
        public NetworkVariable<float> DecisionRemaining = new NetworkVariable<float>(0f);

        [Header("Wrong Answer Penalty")]
        [SerializeField] private bool applyWrongAnswerHealthPenalty = true;
        [SerializeField] private bool usePercentWrongAnswerPenalty = true;
        [Range(0f, 1f)][SerializeField] private float wrongAnswerPenaltyPercent = 0.1f;
        [SerializeField] private int wrongAnswerPenaltyHearts = 1;
        [SerializeField] private bool applyPenaltyOnNoAnswer = false;

        [Header("Wrong Answer Ejection")]
        [SerializeField] private bool ejectPlayersOnWrongAnswer = false;
        private HashSet<ulong> ejectedPlayers = new HashSet<ulong>();

        [Header("Reconnect")]
        [SerializeField] private bool enableReconnectGrace = true;
        [SerializeField] private float reconnectGraceSeconds = 15f;

        private class ReconnectSnapshot
        {
            public ulong OldClientId;
            public bool WasReady;
            public bool WasEjected;
            public int Streak;
            public bool HadAnswer;
            public int Answer;
            public bool QuestionParticipant;
            public float ExpiresAt;
        }

        private readonly Dictionary<string, ReconnectSnapshot> reconnectSnapshotsByUserId = new Dictionary<string, ReconnectSnapshot>();

        public enum DecisionRule { AnyAttack, MajorityAttack }


        private int GetTotalPlayableClients()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.ConnectedClientsIds == null) return 0;
            if (ExcludeServerFromPlayerCounts)
            {
                return nm.ConnectedClientsIds.Count(id => id != NetworkManager.ServerClientId);
            }
            return nm.ConnectedClientsIds.Count;
        }

        private int GetReadyPlayableClients()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return 0;
            if (ExcludeServerFromPlayerCounts)
            {
                return playerReadyStatus.Where(kv => kv.Key != NetworkManager.ServerClientId && kv.Value).Count();
            }
            return playerReadyStatus.Values.Count(v => v);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
            }
            // If we require the backend pack, skip loading from StreamingAssets
            if (requireBackendPackToStart)
            {
                questions = new List<QuestionData>();
                Debug.Log("[QuizManager] Backend pack required; skipping StreamingAssets load.");
            }
            else
            {
                LoadQuestions();
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
                PlayerIdentity.UserIdRegistered += HandleUserIdRegistered;

                foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
                {
                    // Skip the server/host client in headless mode
                    if (ExcludeServerFromPlayerCounts && clientId == NetworkManager.ServerClientId) continue;
                    playerReadyStatus[clientId] = false;
                }
                ReadyCount.Value = 0;
                TotalPlayers.Value = GetTotalPlayableClients();
            }
            State.OnValueChanged += OnStateChanged;
            // Stop quiz on game end
            EventBus.GameWon += OnGameEnded;
            EventBus.GameLost += OnGameEnded;
        }

        /// <summary>
        /// Load questions from the specified JSON file in StreamingAssets.
        /// </summary>
        private void LoadQuestions()
        {
            if (string.IsNullOrEmpty(questionPackFileName))
            {
                // Only warn if we are relying on StreamingAssets
                if (!requireBackendPackToStart)
                {
                    Debug.LogError("Question pack file name is not specified in the QuizManager.");
                }
                questions = new List<QuestionData>();
                return;
            }

            string filePath = Path.Combine(Application.streamingAssetsPath, questionPackFileName);

            if (File.Exists(filePath))
            {
                string dataAsJson = File.ReadAllText(filePath);
                Wrapper wrapper = JsonUtility.FromJson<Wrapper>(dataAsJson);
                questions = wrapper.pack.questions;
                Debug.Log(questions.Count + " questions loaded from " + questionPackFileName);
            }
            else
            {
                // Only warn if we are relying on StreamingAssets
                if (!requireBackendPackToStart)
                {
                    Debug.LogError("Cannot find file: " + filePath);
                }
                questions = new List<QuestionData>();
            }
        }

        // Backend pack models (minimal) for JSON received from API
        [System.Serializable]
        private class BackendPack
        {
            public string packId;
            public string subject;
            public string topic;
            public string difficulty;
            public List<BackendQuestion> questions;
        }

        [System.Serializable]
        private class BackendQuestion
        {
            public string id;
            public string prompt;
            public string[] options;
            public int answerIndex;
            public float timeLimitSec;
            public string topic; // optional override
            public string difficulty; // optional override
            public string explanation;
        }

        /// <summary>
        /// Allow runtime injection of questions from the backend pack JSON.
        /// Supports the backend schema and converts to QuestionData.
        /// </summary>
        public void LoadQuestionsFromBackendJson(string backendJson)
        {
            if (string.IsNullOrWhiteSpace(backendJson))
            {
                Debug.LogError("[QuizManager] Backend JSON was empty");
                return;
            }
            BackendPack pack = null;
            try
            {
                pack = JsonUtility.FromJson<BackendPack>(backendJson);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[QuizManager] Failed to parse backend JSON: {ex.Message}");
                return;
            }
            if (pack == null || pack.questions == null)
            {
                Debug.LogError("[QuizManager] Parsed backend pack is null or contains no questions");
                return;
            }
            var list = new List<QuestionData>();
            int fallbackId = 1;
            foreach (var q in pack.questions)
            {
                int idNum = fallbackId++;
                if (!string.IsNullOrEmpty(q.id))
                {
                    var digits = new string(q.id.Where(char.IsDigit).ToArray());
                    if (int.TryParse(digits, out var parsed)) idNum = parsed;
                }
                var data = new QuestionData
                {
                    id = idNum,
                    topic = string.IsNullOrEmpty(q.topic) ? pack.topic : q.topic,
                    difficulty = string.IsNullOrEmpty(q.difficulty) ? pack.difficulty : q.difficulty,
                    prompt = q.prompt ?? string.Empty,
                    options = q.options ?? new string[0],
                    correctIndex = q.answerIndex,
                    timeLimitSec = q.timeLimitSec > 0 ? Mathf.RoundToInt(q.timeLimitSec) : 20,
                    explanation = q.explanation
                };
                list.Add(data);
            }
            questions = list;
            Debug.Log($"[QuizManager] Loaded {questions.Count} questions from backend pack '{pack.packId}'.");
        }

        void Update()
        {
            if (!IsServer) return;

            PurgeExpiredReconnectSnapshots();

            if (State.Value == QuizState.Question)
            {
                // Pause the question timer during Power Play to avoid unfair timeouts
                if (PowerPlayManager.Instance != null && PowerPlayManager.Instance.IsPowerPlayActive.Value)
                {
                    return;
                }
                RemainingTime.Value -= Time.deltaTime;
                if (RemainingTime.Value <= 0)
                {
                    RemainingTime.Value = 0;
                    // Notify clients about timeout to update UI/state via EventBus
                    NotifyQuestionTimeoutClientRpc();
                    ResolveAnswers();
                }
            }

            // Auto-advance while idle after a short inter-round pause if all players are ready
            // Suppress auto-advance while Power Play is active to avoid UI conflicts
            if (State.Value == QuizState.Idle && continuousReadyFlow && Time.time >= nextQuestionAllowedAt)
            {
                if (PowerPlayManager.Instance != null && PowerPlayManager.Instance.IsPowerPlayActive.Value)
                {
                    return;
                }
                CheckAllPlayersReady();
            }

            if (State.Value == QuizState.Decision)
            {
                DecisionRemaining.Value = Mathf.Max(0f, decisionEndsAt - Time.time);
                if (Time.time >= decisionEndsAt)
                {
                    CompleteDecisionPhase();
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
                PlayerIdentity.UserIdRegistered -= HandleUserIdRegistered;
            }
            State.OnValueChanged -= OnStateChanged;
            EventBus.GameWon -= OnGameEnded;
            EventBus.GameLost -= OnGameEnded;
        }

        private void OnGameEnded()
        {
            if (!IsServer) return;
            State.Value = QuizState.Idle;
            nextQuestionAllowedAt = float.MaxValue; // prevent auto-restart
            HideQuestionPanelClientRpc();
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (!IsServer) return;
            // Skip the server/host client in headless mode
            if (ExcludeServerFromPlayerCounts && clientId == NetworkManager.ServerClientId)
            {
                TotalPlayers.Value = GetTotalPlayableClients();
                ReadyCount.Value = GetReadyPlayableClients();
                return;
            }
            playerReadyStatus[clientId] = false;
            correctAnswerStreak[clientId] = 0;
            // Update counts for client-side UI
            TotalPlayers.Value = GetTotalPlayableClients();
            ReadyCount.Value = GetReadyPlayableClients();
        }

        private void HandleUserIdRegistered(ulong clientId, string userId)
        {
            if (!IsServer) return;
            if (!enableReconnectGrace) return;
            if (string.IsNullOrEmpty(userId)) return;

            if (!reconnectSnapshotsByUserId.TryGetValue(userId, out var snap))
            {
                return;
            }

            if (Time.time > snap.ExpiresAt)
            {
                reconnectSnapshotsByUserId.Remove(userId);
                return;
            }

            reconnectSnapshotsByUserId.Remove(userId);

            playerReadyStatus[clientId] = snap.WasReady;
            correctAnswerStreak[clientId] = snap.Streak;

            if (snap.WasEjected)
            {
                ejectedPlayers.Add(clientId);
            }

            if (snap.HadAnswer)
            {
                playerAnswers[clientId] = snap.Answer;
            }

            TotalPlayers.Value = GetTotalPlayableClients();
            ReadyCount.Value = GetReadyPlayableClients();

            if (State.Value == QuizState.Question && snap.WasReady)
            {
                var payload = BuildCurrentQuestionPayload();
                var targets = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } } };
                ShowQuestionClientRpc(payload, targets);

                if (snap.HadAnswer)
                {
                    var question = questions[currentQuestionIndex.Value];
                    int correctIndex = question.correctIndex;
                    bool isCorrect = snap.Answer == correctIndex;
                    AnswerResolutionClientRpc(snap.Answer, isCorrect, correctIndex, targets);
                }
            }

            if (State.Value == QuizState.Decision && !ejectedPlayers.Contains(clientId))
            {
                var remaining = Mathf.Max(0f, DecisionRemaining.Value);
                var targets = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } } };
                ShowDecisionClientRpc(remaining, (int)decisionRule, targets);
            }

            if (State.Value == QuizState.Question && snap.QuestionParticipant)
            {
                CheckAllAnswersSubmitted();
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            if (enableReconnectGrace)
            {
                var userId = PlayerIdentity.GetUserIdForClient(clientId);
                if (!string.IsNullOrEmpty(userId))
                {
                    bool wasReady = playerReadyStatus.TryGetValue(clientId, out var r) && r;
                    bool hadAnswer = playerAnswers.TryGetValue(clientId, out var ans);
                    int streak = 0;
                    correctAnswerStreak.TryGetValue(clientId, out streak);
                    bool wasEjected = ejectedPlayers.Contains(clientId);
                    bool questionParticipant = State.Value == QuizState.Question && wasReady;

                    reconnectSnapshotsByUserId[userId] = new ReconnectSnapshot
                    {
                        OldClientId = clientId,
                        WasReady = wasReady,
                        WasEjected = wasEjected,
                        Streak = streak,
                        HadAnswer = hadAnswer,
                        Answer = ans,
                        QuestionParticipant = questionParticipant,
                        ExpiresAt = Time.time + Mathf.Max(0f, reconnectGraceSeconds)
                    };
                }
            }

            playerReadyStatus.Remove(clientId);
            playerAnswers.Remove(clientId);
            correctAnswerStreak.Remove(clientId);
            ejectedPlayers.Remove(clientId);
            // Update counts for client-side UI
            TotalPlayers.Value = GetTotalPlayableClients();
            ReadyCount.Value = GetReadyPlayableClients();
            if (State.Value == QuizState.Question)
            {
                CheckAllAnswersSubmitted();
            }
        }

        private void PurgeExpiredReconnectSnapshots()
        {
            if (!enableReconnectGrace) return;
            if (reconnectSnapshotsByUserId.Count == 0) return;

            bool removedAny = false;
            var now = Time.time;
            var keys = new List<string>(reconnectSnapshotsByUserId.Keys);
            foreach (var key in keys)
            {
                if (now > reconnectSnapshotsByUserId[key].ExpiresAt)
                {
                    reconnectSnapshotsByUserId.Remove(key);
                    removedAny = true;
                }
            }

            if (removedAny && State.Value == QuizState.Question)
            {
                CheckAllAnswersSubmitted();
            }
        }

        private void OnStateChanged(QuizState previous, QuizState current)
        {
            if (current == QuizState.Idle)
            {
                HideQuestionPanelClientRpc();
            }
        }

        public void PlayerReadyChanged(ulong clientId, bool isReady)
        {
            if (!IsServer) return;
            // Skip the server/host client in headless mode
            if (ExcludeServerFromPlayerCounts && clientId == NetworkManager.ServerClientId)
            {
                return;
            }
            playerReadyStatus[clientId] = isReady;

            // MVP: Clear ejected status when player re-readies (rejoining after being ejected)
            if (isReady && ejectedPlayers.Contains(clientId))
            {
                ejectedPlayers.Remove(clientId);
                Debug.Log($"[QuizManager] Player {clientId} re-readied and rejoined after ejection");
            }

            // Update replicated ReadyCount for client-side UI
            ReadyCount.Value = GetReadyPlayableClients();
            if (State.Value == QuizState.Idle)
            {
                CheckAllPlayersReady();
            }
            else if (State.Value == QuizState.Question && isReady)
            {
                var payload = BuildCurrentQuestionPayload();
                var targets = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } } };
                ShowQuestionClientRpc(payload, targets);
            }
        }

        // MVP: Mark a player as ejected - they must return to ready station and re-ready to rejoin
        public void MarkPlayerEjected(ulong clientId)
        {
            if (!IsServer) return;
            ejectedPlayers.Add(clientId);
            Debug.Log($"[QuizManager] Player {clientId} marked as ejected - must re-ready to rejoin");
            var readyStation = FindObjectOfType<ReadyStation>();
            if (readyStation == null)
            {
                Debug.LogError("[QuizManager] ReadyStation not found in scene.");
                return;
            }
            // Eject the player from the ready station
            readyStation.EjectPlayerServerRpc();
        }

        private void CheckAllPlayersReady()
        {
            if (!IsServer || State.Value != QuizState.Idle) return;

            // If configured, wait until GameSessionClient injected the backend pack before starting
            if (requireBackendPackToStart)
            {
                var gsc = BossFight2D.Systems.GameSessionClient.Instance ?? Object.FindFirstObjectByType<BossFight2D.Systems.GameSessionClient>();
                if (gsc != null && !gsc.IsPackInjected)
                {
                    // Still waiting for backend pack; do not start yet.
                    Debug.Log("[QuizManager] All-ready gate: backend pack required but not yet injected. Waiting for GameSessionClient to resolve and inject the pack.");
                    return;
                }
            }

            var nm = NetworkManager.Singleton;
            foreach (var client in nm.ConnectedClients.Values)
            {
                // Skip the server/host client in headless mode
                if (ExcludeServerFromPlayerCounts && client.ClientId == NetworkManager.ServerClientId)
                {
                    continue;
                }

                if (!playerReadyStatus.TryGetValue(client.ClientId, out var isReady) || !isReady)
                {
                    //Debug.Log($"[QuizManager] Waiting for all players to ready. Ready={GetReadyPlayableClients()}/{GetTotalPlayableClients()}.");
                    return;
                }
            }

            Debug.Log($"[QuizManager] All players ready ({GetReadyPlayableClients()}/{GetTotalPlayableClients()}). Starting quiz with {questions?.Count ?? 0} questions.");
            StartQuiz();
        }

        private void StartQuiz()
        {
            if (!IsServer) return;
            if (questions.Count == 0)
            {
                Debug.LogError("No questions available to start quiz.");
                return;
            }

            playerAnswers.Clear();
            punishTriggered = false;
            wrongAnswerPlayers.Clear();
            State.Value = QuizState.Question;
            currentQuestionIndex.Value = (currentQuestionIndex.Value + 1) % questions.Count;

            QuestionData question = questions[currentQuestionIndex.Value];
            CurrentQuestionTimeLimit.Value = question.timeLimitSec;
            RemainingTime.Value = question.timeLimitSec;
            var payload = BuildCurrentQuestionPayload();
            ShowQuestionClientRpc(payload);

            if (Application.isBatchMode)
            {
                EventBus.RaiseQuestionStarted(question);
            }
        }

        private void ClientShowQuestion(QuestionPayload payload)
        {
            if (PowerPlayManager.Instance != null && PowerPlayManager.Instance.IsPowerPlayActive.Value)
            {
                StartCoroutine(DeferredShowQuestion(payload));
                return;
            }
            var panel = QuestionPanelController.Instance;
            if (panel == null)
            {
                var prefab = Resources.Load<GameObject>("QuestionPanel");
                if (prefab != null)
                {
                    var go = GameObject.Instantiate(prefab);
                    panel = QuestionPanelController.Instance ?? go.GetComponent<QuestionPanelController>();
                }
                else
                {
                    Debug.LogError("QuestionPanel prefab not found in Resources. Please place Assets/Prefabs/QuestionPanel.prefab under Assets/Resources/ as 'QuestionPanel'.");
                    return;
                }
            }
            var options = new string[payload.OptionsCount];
            if (payload.OptionsCount > 0) options[0] = payload.Option1.ToString();
            if (payload.OptionsCount > 1) options[1] = payload.Option2.ToString();
            if (payload.OptionsCount > 2) options[2] = payload.Option3.ToString();
            if (payload.OptionsCount > 3) options[3] = payload.Option4.ToString();
            QuestionData questionData = new QuestionData() { prompt = payload.Prompt.ToString(), options = options };
            panel.ShowQuestion(questionData);
            if (!Application.isBatchMode)
            {
                EventBus.RaiseQuestionStarted(questionData);
            }
        }

        [ClientRpc]
        private void ShowQuestionClientRpc(QuestionPayload payload)
        {
            ClientShowQuestion(payload);
        }

        private System.Collections.IEnumerator DeferredShowQuestion(QuestionPayload payload)
        {
            float waitMax = 1.0f;
            float t = 0f;
            while (PowerPlayManager.Instance != null && PowerPlayManager.Instance.IsPowerPlayActive.Value && t < waitMax)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            // After Power Play ends (or timeout), try to show
            if (State.Value == QuizState.Question)
                ShowQuestionClientRpc(payload);
        }

        private QuestionPayload BuildCurrentQuestionPayload()
        {
            var question = questions[currentQuestionIndex.Value];
            var payload = new QuestionPayload
            {
                Prompt = question.prompt,
                OptionsCount = question.options.Length,
                Option1 = question.options.Length > 0 ? question.options[0] : "",
                Option2 = question.options.Length > 1 ? question.options[1] : "",
                Option3 = question.options.Length > 2 ? question.options[2] : "",
                Option4 = question.options.Length > 3 ? question.options[3] : ""
            };
            return payload;
        }

        [ClientRpc]
        private void ShowQuestionClientRpc(QuestionPayload payload, ClientRpcParams clientRpcParams)
        {
            ClientShowQuestion(payload);
        }

        [ClientRpc]
        private void HideQuestionPanelClientRpc()
        {
            if (QuestionPanelController.Instance != null)
            {
                QuestionPanelController.Instance.HidePanel();
            }
        }

        public void SubmitAnswer(ulong playerId, int answerIndex)
        {
            if (!IsServer || State.Value != QuizState.Question) return;

            playerAnswers[playerId] = answerIndex;

            // Resolve this player's answer immediately for client-side feedback
            var question = questions[currentQuestionIndex.Value];
            int correctIndex = question.correctIndex;
            bool isCorrect = answerIndex == correctIndex;

            var tkey = string.IsNullOrEmpty(question.topic) ? "(untagged)" : question.topic;
            if (!topicStats.ContainsKey(tkey)) topicStats[tkey] = (0, 0);
            var ts = topicStats[tkey];
            ts.total += 1;
            if (isCorrect) ts.correct += 1;
            topicStats[tkey] = ts;

            // Apply combat effects server-side
            if (isCorrect)
            {
                CombatResolver.ApplyAnswerDamage(question, this);
            }
            else
            {
                wrongAnswerPlayers.Add(playerId);
                var boss = Object.FindFirstObjectByType<BossStateMachine>();
                if (boss != null)
                {
                    if (!punishTriggered)
                    {
                        boss.BeginWrongAnswerChallenge();
                    }
                    boss.RegisterWrongAnswer(playerId);
                }
                else if (!punishTriggered)
                {
                    var fallbackBoss = Object.FindFirstObjectByType<BossStateMachine>();
                    if (fallbackBoss != null)
                    {
                        fallbackBoss.OnWrongAnswer();
                    }
                }
                punishTriggered = true;
            }

            // Notify the answering client about resolution and raise AnswerSubmitted on that client
            var targets = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { playerId } }
            };
            AnswerResolutionClientRpc(answerIndex, isCorrect, correctIndex, targets);

            CheckAllAnswersSubmitted();
        }

        private void CheckAllAnswersSubmitted()
        {
            var nm = NetworkManager.Singleton;
            int requiredCount = nm.ConnectedClients.Count;
            if (ExcludeServerFromPlayerCounts)
            {
                // Exclude the server/host client in headless mode
                requiredCount = Mathf.Max(0, requiredCount - 1);
            }

            if (enableReconnectGrace && State.Value == QuizState.Question)
            {
                int extraRequired = 0;
                int extraAnswered = 0;
                var now = Time.time;
                foreach (var snap in reconnectSnapshotsByUserId.Values)
                {
                    if (!snap.QuestionParticipant) continue;
                    if (now > snap.ExpiresAt) continue;
                    extraRequired++;
                    if (snap.HadAnswer) extraAnswered++;
                }

                if ((playerAnswers.Count + extraAnswered) >= (requiredCount + extraRequired))
                {
                    ResolveAnswers();
                }
                return;
            }

            if (playerAnswers.Count >= requiredCount)
            {
                ResolveAnswers();
            }
        }

        private void EjectWrongAnswerPlayers(QuestionData question)
        {
            if (!IsServer) return;

            // Find all Ready Stations in the scene
            var readyStations = Object.FindObjectsByType<BossFight2D.Systems.ReadyStation>(FindObjectsSortMode.None);

            foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
            {
                // Check if player answered incorrectly
                bool isCorrect = playerAnswers.ContainsKey(client.ClientId) && playerAnswers[client.ClientId] == question.correctIndex;

                if (!isCorrect)
                {
                    // Player answered wrong - check if they're inside a safe zone
                    foreach (var station in readyStations)
                    {
                        if (station.IsPlayerInside(client.ClientId))
                        {
                            // Player is inside safe zone with wrong answer - mark ejected (actual eject handled elsewhere)
                            MarkPlayerEjected(client.ClientId);
                            Debug.Log($"[QuizManager] Ejecting player {client.ClientId} after resolution (answered wrong while in safe zone)");
                            break; // Player can only be in one station
                        }
                    }
                }
            }
        }

        private void ResolveAnswers()
        {
            if (!IsServer || State.Value != QuizState.Question) return;

            State.Value = QuizState.Resolution;
            QuestionData question = questions[currentQuestionIndex.Value];

            var boss = Object.FindFirstObjectByType<BossStateMachine>();
            var useBossPunish = (boss != null && boss.combat != null);

            if (Application.isBatchMode)
            {
                EventBus.RaiseAnswerResolved();
            }

            ulong? powerPlayPlayerId = null;

            foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
            {
                if (ExcludeServerFromPlayerCounts && client.ClientId == NetworkManager.ServerClientId)
                {
                    continue;
                }
                bool isCorrect = playerAnswers.ContainsKey(client.ClientId) && playerAnswers[client.ClientId] == question.correctIndex;
                if (isCorrect)
                {
                    var playerCombat = client.PlayerObject.GetComponent<Player.PlayerCombat>();
                    if (playerCombat != null)
                    {
                        playerCombat.AwardCharges();
                        int streak = 0;
                        correctAnswerStreak.TryGetValue(client.ClientId, out streak);
                        streak++;
                        correctAnswerStreak[client.ClientId] = streak;
                        if (streak >= powerPlayStreakThreshold && !powerPlayPlayerId.HasValue)
                        {
                            powerPlayPlayerId = client.ClientId;
                        }
                    }
                }
                else
                {
                    if (applyWrongAnswerHealthPenalty)
                    {
                        var answered = playerAnswers.ContainsKey(client.ClientId);
                        var shouldApplyPenalty = !useBossPunish
                            ? (answered || applyPenaltyOnNoAnswer)
                            : (!answered && applyPenaltyOnNoAnswer);

                        if (shouldApplyPenalty)
                        {
                            var playerHealth = client.PlayerObject != null
                                ? client.PlayerObject.GetComponent<BossFight2D.Player.PlayerHealth>()
                                : null;
                            if (playerHealth != null)
                            {
                                var penaltyHearts = ComputeWrongAnswerPenaltyHearts(playerHealth);
                                if (penaltyHearts > 0)
                                {
                                    playerHealth.TakeDamage(penaltyHearts);
                                }
                            }
                        }
                    }

                    // Break the streak on incorrect or no answer
                    correctAnswerStreak[client.ClientId] = 0;
                }
            }

            if (powerPlayPlayerId.HasValue)
            {
                PowerPlayManager.Instance.StartPowerPlay(powerPlayPlayerId.Value);
                if (consumeStreakOnActivation)
                {
                    correctAnswerStreak[powerPlayPlayerId.Value] = 0;
                }
            }

            if (ejectPlayersOnWrongAnswer)
            {
                EjectWrongAnswerPlayers(question);
            }

            bool anyCorrect = false;
            bool anyIncorrect = false;
            foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
            {
                if (ExcludeServerFromPlayerCounts && client.ClientId == NetworkManager.ServerClientId)
                {
                    continue;
                }

                var answered = playerAnswers.ContainsKey(client.ClientId);
                var isCorrect = answered && playerAnswers[client.ClientId] == question.correctIndex;
                if (isCorrect)
                {
                    anyCorrect = true; break;
                }

                if (answered || applyPenaltyOnNoAnswer)
                {
                    anyIncorrect = true;
                }
            }
            if (anyCorrect)
            {
                // Power play trigger
                if (powerPlayPlayerId.HasValue)
                {
                    // Power play - no decision phase
                }
                else
                {
                    if (anyIncorrect && wrongAnswerInterRoundDelaySeconds > 0f)
                    {
                        StartCoroutine(StartDecisionPhaseAfterDelay(wrongAnswerInterRoundDelaySeconds, powerPlayPlayerId));
                    }
                    else
                    {
                        StartDecisionPhase(powerPlayPlayerId);
                    }
                }

            }
            else
            {
                ResetForNextRound();
                if (continuousReadyFlow)
                {
                    nextQuestionAllowedAt = Time.time + wrongAnswerInterRoundDelaySeconds;
                }
                StartCoroutine(StartNextQuestionAfterDelay(wrongAnswerInterRoundDelaySeconds));
            }
        }

        private int ComputeWrongAnswerPenaltyHearts(BossFight2D.Player.PlayerHealth playerHealth)
        {
            if (playerHealth == null) return 0;
            var max = Mathf.Max(1, playerHealth.maxHearts.Value);
            if (usePercentWrongAnswerPenalty)
            {
                var pct = Mathf.Clamp01(wrongAnswerPenaltyPercent);
                var raw = Mathf.CeilToInt(max * pct);
                return Mathf.Clamp(raw, 0, max);
            }
            return Mathf.Clamp(wrongAnswerPenaltyHearts, 0, max);
        }

        private void StartDecisionPhase(ulong? candidate)
        {
            State.Value = QuizState.Decision;
            playerDecisionAttack.Clear();
            decisionEndsAt = Time.time + decisionPhaseSeconds;

            // MVP: Only show decision panel to non-ejected players
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                var eligiblePlayers = nm.ConnectedClientsIds.Where(id => !ejectedPlayers.Contains(id)).ToArray();
                if (eligiblePlayers.Length > 0)
                {
                    var clientParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = eligiblePlayers }
                    };
                    ShowDecisionClientRpc(decisionPhaseSeconds, (int)decisionRule, clientParams);
                }
            }

            BroadcastDecisionTally();
            DecisionRemaining.Value = decisionPhaseSeconds;
            decisionActive = true;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitDecisionServerRpc(bool attack, ServerRpcParams rpcParams = default)
        {
            if (!IsServer || State.Value != QuizState.Decision) return;
            var senderId = rpcParams.Receive.SenderClientId;
            playerDecisionAttack[senderId] = attack;
            var nm = NetworkManager.Singleton;
            int required = nm.ConnectedClients.Count;
            if (ExcludeServerFromPlayerCounts) required = Mathf.Max(0, required - 1);

            // MVP: Exclude ejected players from required count (they can't vote)
            required -= ejectedPlayers.Count;
            required = Mathf.Max(1, required); // At least 1 player must vote

            BroadcastDecisionTally();
            if (playerDecisionAttack.Count >= required)
            {
                CompleteDecisionPhase();
            }
        }

        private void CompleteDecisionPhase()
        {
            bool anyAttack = playerDecisionAttack.Values.Any(v => v);
            if (decisionRule == DecisionRule.MajorityAttack)
            {
                int cont = playerDecisionAttack.Values.Count(v => !v);
                int atk = playerDecisionAttack.Values.Count(v => v);
                anyAttack = atk > cont;
            }
            // MVP FIX: Don't hide panel here - ShowDecisionResultClientRpc will transition it to result mode
            // The panel will auto-hide after the result timer expires (1.5s)
            int total = playerDecisionAttack.Count;
            int contCount = playerDecisionAttack.Values.Count(v => !v);
            int atkCount = playerDecisionAttack.Values.Count(v => v);
            string msg = anyAttack ? ("Attack selected (" + atkCount + "/" + total + ")") : ("Continue quiz (" + contCount + "/" + total + ")");
            ShowDecisionResultClientRpc(msg, 1.5f);
            ResetForNextRound();
            if (continuousReadyFlow)
            {
                nextQuestionAllowedAt = Time.time + 1.0f;
            }
            if (anyAttack)
            {
                ulong chosen = NetworkManager.Singleton.ConnectedClientsIds.FirstOrDefault(id => (!ExcludeServerFromPlayerCounts || id != NetworkManager.ServerClientId));
                if (chosen != 0)
                {
                    PowerPlayManager.Instance.StartPowerPlay(chosen);
                }
            }
            decisionActive = false;
            StartCoroutine(StartNextQuestionAfterDelay(1.0f));
        }

        [ClientRpc]
        private void ShowDecisionClientRpc(float seconds, int rule, ClientRpcParams clientRpcParams = default)
        {
            var panel = BossFight2D.UI.DecisionPanelController.Instance;
            if (panel == null)
            {
                var go = new GameObject("DecisionPanelController");
                panel = go.AddComponent<BossFight2D.UI.DecisionPanelController>();
            }
            panel.Show(seconds, rule);
        }

        [ClientRpc]
        private void HideDecisionClientRpc()
        {
            var panel = BossFight2D.UI.DecisionPanelController.Instance;
            if (panel != null) panel.Hide();
        }

        [ClientRpc]
        private void ShowDecisionResultClientRpc(string message, float seconds)
        {
            var panel = BossFight2D.UI.DecisionPanelController.Instance;
            if (panel == null)
            {
                var go = new GameObject("DecisionPanelController");
                panel = go.AddComponent<BossFight2D.UI.DecisionPanelController>();
            }
            panel.ShowResult(message, seconds);
        }

        [ClientRpc]
        private void UpdateDecisionTallyClientRpc(int cont, int atk)
        {
            var panel = BossFight2D.UI.DecisionPanelController.Instance;
            if (panel != null) panel.UpdateTally(cont, atk);
        }

        private void BroadcastDecisionTally()
        {
            if (!IsServer) return;
            int cont = 0, atk = 0;
            foreach (var kv in playerDecisionAttack)
            {
                if (kv.Value) atk++; else cont++;
            }
            UpdateDecisionTallyClientRpc(cont, atk);
        }

        private void ResetForNextRound()
        {
            State.Value = QuizState.Idle;
            if (continuousReadyFlow)
            {
                // Keep existing ready states (station will auto-unready on exit/ejection)
                ReadyCount.Value = GetReadyPlayableClients();
                nextQuestionAllowedAt = Time.time + interRoundDelaySeconds;
            }
            else
            {
                // Legacy behavior: require manual re-ready each round
                var clientIds = playerReadyStatus.Keys.ToList();
                foreach (var id in clientIds)
                {
                    playerReadyStatus[id] = false;
                }
                ReadyCount.Value = 0;
            }
        }

        public void EndPowerPlayAndStartNextQuestion()
        {
            if (!IsServer) return;

            if (decisionActive) return;
            ResetForNextRound();
            // Power Play flow can override delay to keep momentum
            if (continuousReadyFlow)
            {
                nextQuestionAllowedAt = Time.time; // allow immediate start
            }
            StartQuiz();
        }

        private System.Collections.IEnumerator StartNextQuestionAfterDelay(float seconds)
        {
            float t = seconds;
            while (t > 0f)
            {
                t -= Time.deltaTime;
                yield return null;
            }
            if (IsServer && State.Value == QuizState.Idle) StartQuiz();
        }

        private System.Collections.IEnumerator StartDecisionPhaseAfterDelay(float seconds, ulong? candidate)
        {
            float t = seconds;
            while (t > 0f)
            {
                t -= Time.deltaTime;
                yield return null;
            }
            if (IsServer && State.Value == QuizState.Resolution) StartDecisionPhase(candidate);
        }

        [ClientRpc]
        private void AnswerResolutionClientRpc(int selectedIndex, bool isCorrect, int correctIndex, ClientRpcParams clientRpcParams = default)
        {
            // Raise client-side event for systems like WebBridge and PlayerQuestionGuard
            EventBus.RaiseAnswerSubmitted(selectedIndex, isCorrect);

            // Drive UI resolution feedback on the answering player's client
            if (QuestionPanelController.Instance != null)
            {
                QuestionPanelController.Instance.ShowResolution(selectedIndex, isCorrect, correctIndex);
            }
        }

        [ClientRpc]
        private void NotifyQuestionTimeoutClientRpc()
        {
            EventBus.RaiseQuestionTimeout();
        }

        // Allow clients (including host) to submit answers via RPC so UI clicks are fully networked
        [ServerRpc(RequireOwnership = false)]
        public void SubmitAnswerServerRpc(int answerIndex, ServerRpcParams rpcParams = default)
        {
            // Guard: only accept during the question phase
            if (State.Value != QuizState.Question) return;
            var senderId = rpcParams.Receive.SenderClientId;
            if (playerAnswers.ContainsKey(senderId)) return;
            SubmitAnswer(senderId, answerIndex);
        }

        // Expose per-player ready for station/UI helpers
        public bool IsPlayerReady(ulong clientId)
        {
            return playerReadyStatus.TryGetValue(clientId, out var ready) && ready;
        }

        public SummaryData GetTopicSummary()
        {
            var list = new System.Collections.Generic.List<TopicSummaryData>();
            foreach (var kv in topicStats)
            {
                list.Add(new TopicSummaryData { topic = kv.Key, total = kv.Value.total, correct = kv.Value.correct });
            }
            return new SummaryData { topics = list };
        }

        // MVP: Expose player answers for match results tracking
        public Dictionary<ulong, int> GetPlayerAnswers()
        {
            return playerAnswers;
        }

    }
}

