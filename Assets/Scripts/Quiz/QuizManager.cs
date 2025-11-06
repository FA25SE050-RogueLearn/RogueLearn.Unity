using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;
using BossFight2D.Core;
using BossFight2D.UI;
using BossFight2D.Systems;
using BossFight2D.Combat;
using BossFight2D.Boss;
using System.IO;

namespace BossFight2D.Quiz
{
    public enum QuizState
    {
        Idle,
        Question,
        Resolution
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

        private Dictionary<ulong, bool> playerReadyStatus = new Dictionary<ulong, bool>();
        private Dictionary<ulong, int> playerAnswers = new Dictionary<ulong, int>();
        private bool punishTriggered = false;
        [Header("Ready Flow")]
        [SerializeField] private bool continuousReadyFlow = true; // If true, keep players ready between rounds while they remain in the station
        [SerializeField] private float interRoundDelaySeconds = 2f; // Pause between rounds before starting next question automatically
        private float nextQuestionAllowedAt = 0f; // Time gate to avoid instant restart

        [Header("Power Play Triggering")]
        [SerializeField] private int powerPlayStreakThreshold = 3; // Require N consecutive correct answers to trigger Power Play
        [SerializeField] private bool consumeStreakOnActivation = true; // Reset streak when Power Play activates
        private Dictionary<ulong, int> correctAnswerStreak = new Dictionary<ulong, int>();

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
            LoadQuestions();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

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
        }

        private void LoadQuestions()
        {
            if (string.IsNullOrEmpty(questionPackFileName))
            {
                Debug.LogError("Question pack file name is not specified in the QuizManager.");
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
                Debug.LogError("Cannot find file: " + filePath);
                questions = new List<QuestionData>();
            }
        }

        void Update()
        {
            if (!IsServer) return;

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
            if (State.Value == QuizState.Idle && continuousReadyFlow && Time.time >= nextQuestionAllowedAt)
            {
                CheckAllPlayersReady();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
            State.OnValueChanged -= OnStateChanged;
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

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;
            playerReadyStatus.Remove(clientId);
            playerAnswers.Remove(clientId);
            correctAnswerStreak.Remove(clientId);
            // Update counts for client-side UI
            TotalPlayers.Value = GetTotalPlayableClients();
            ReadyCount.Value = GetReadyPlayableClients();
            if (State.Value == QuizState.Question)
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
            // Update replicated ReadyCount for client-side UI
            ReadyCount.Value = GetReadyPlayableClients();
            CheckAllPlayersReady();
        }

        private void CheckAllPlayersReady()
        {
            if (!IsServer || State.Value != QuizState.Idle) return;

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
                    return;
                }
            }

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
            State.Value = QuizState.Question;
            currentQuestionIndex.Value = (currentQuestionIndex.Value + 1) % questions.Count;

            QuestionData question = questions[currentQuestionIndex.Value];
            CurrentQuestionTimeLimit.Value = question.timeLimitSec;
            RemainingTime.Value = question.timeLimitSec;
            var payload = new QuestionPayload
            {
                Prompt = question.prompt,
                OptionsCount = question.options.Length,
                Option1 = question.options.Length > 0 ? question.options[0] : "",
                Option2 = question.options.Length > 1 ? question.options[1] : "",
                Option3 = question.options.Length > 2 ? question.options[2] : "",
                Option4 = question.options.Length > 3 ? question.options[3] : ""
            };
            ShowQuestionClientRpc(payload);
        }

        [ClientRpc]
        private void ShowQuestionClientRpc(QuestionPayload payload)
        {
            // Ensure a QuestionPanel exists on each client; instantiate from Resources if missing
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

            // Raise event for systems that rely on EventBus (WebBridge, PlayerQuestionGuard, etc.)
            EventBus.RaiseQuestionStarted(questionData);
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

            // Apply combat effects server-side
            if (isCorrect)
            {
                CombatResolver.ApplyAnswerDamage(question, this);
            }
            else if (!punishTriggered)
            {
                // Trigger wrong-answer punish flow once per question
                var boss = Object.FindFirstObjectByType<BossStateMachine>();
                if (boss != null)
                {
                    boss.OnWrongAnswer();
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
            if (playerAnswers.Count >= requiredCount)
            {
                ResolveAnswers();
            }
        }

        private void ResolveAnswers()
        {
            if (!IsServer || State.Value != QuizState.Question) return;

            State.Value = QuizState.Resolution;
            QuestionData question = questions[currentQuestionIndex.Value];

            ulong? powerPlayPlayerId = null;

            foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
            {
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

            ResetForNextRound();
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

            ResetForNextRound();
            // Power Play flow can override delay to keep momentum
            if (continuousReadyFlow)
            {
                nextQuestionAllowedAt = Time.time; // allow immediate start
            }
            StartQuiz();
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
            SubmitAnswer(senderId, answerIndex);
        }

        // Expose per-player ready for station/UI helpers
        public bool IsPlayerReady(ulong clientId)
        {
            return playerReadyStatus.TryGetValue(clientId, out var ready) && ready;
        }
    }
}