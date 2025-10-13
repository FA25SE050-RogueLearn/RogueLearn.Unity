using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;
using BossFight2D.Core;
using BossFight2D.UI;
using BossFight2D.Systems;
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

        private Dictionary<ulong, bool> playerReadyStatus = new Dictionary<ulong, bool>();
        private Dictionary<ulong, int> playerAnswers = new Dictionary<ulong, int>();

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
                    playerReadyStatus[clientId] = false;
                }
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
                RemainingTime.Value -= Time.deltaTime;
                if (RemainingTime.Value <= 0)
                {
                    RemainingTime.Value = 0;
                    ResolveAnswers();
                }
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
            playerReadyStatus[clientId] = false;
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;
            playerReadyStatus.Remove(clientId);
            playerAnswers.Remove(clientId);
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
            playerReadyStatus[clientId] = isReady;
            CheckAllPlayersReady();
        }

        private void CheckAllPlayersReady()
        {
            if (!IsServer || State.Value != QuizState.Idle) return;

            foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
            {
                if (!playerReadyStatus.ContainsKey(client.ClientId) || !playerReadyStatus[client.ClientId])
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
            if (QuestionPanelController.Instance != null)
            {
                var options = new string[payload.OptionsCount];
                if (payload.OptionsCount > 0) options[0] = payload.Option1.ToString();
                if (payload.OptionsCount > 1) options[1] = payload.Option2.ToString();
                if (payload.OptionsCount > 2) options[2] = payload.Option3.ToString();
                if (payload.OptionsCount > 3) options[3] = payload.Option4.ToString();

                QuestionData questionData = new QuestionData() { prompt = payload.Prompt.ToString(), options = options };
                QuestionPanelController.Instance.ShowQuestion(questionData);
            }
            else
            {
                Debug.LogError("QuestionPanelController not found in the scene.");
            }
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
            CheckAllAnswersSubmitted();
        }

        private void CheckAllAnswersSubmitted()
        {
            if (playerAnswers.Count >= NetworkManager.Singleton.ConnectedClients.Count)
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
                        powerPlayPlayerId = client.ClientId;
                    }
                }
            }

            if (powerPlayPlayerId.HasValue)
            {
                PowerPlayManager.Instance.StartPowerPlay(powerPlayPlayerId.Value);
            }

            ResetForNextRound();
        }

        private void ResetForNextRound()
        {
            State.Value = QuizState.Idle;
            var clientIds = playerReadyStatus.Keys.ToList();
            foreach (var id in clientIds)
            {
                playerReadyStatus[id] = false;
            }
        }

        public void EndPowerPlayAndStartNextQuestion()
        {
            if (!IsServer) return;

            ResetForNextRound();
            StartQuiz();
        }
    }
}