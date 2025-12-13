using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using BossFight2D.UI;
using BossFight2D.Systems;
using BossFight2D.Player;
using BossFight2D.Quiz;

namespace BossFight2D.Systems
{
    public class QuestionManager_Client : NetworkBehaviour
    {
        private QuestionPanelController questionPanelController;
        [Header("UI References")]
        public TextMeshProUGUI questionText;
        public Slider timerSlider;
        public Button[] answerButtons;

        private void Awake()
        {
            // Auto-disable legacy manager if QuizManager exists in the scene
            var qm = QuizManager.Instance ?? UnityEngine.Object.FindFirstObjectByType<QuizManager>();
            if (qm != null)
            {
                Debug.Log("[QuestionManager_Client] QuizManager detected; disabling legacy QuestionManager_Client to avoid conflicts. Please remove this component from the scene.");
                enabled = false;
                return;
            }
            questionPanelController = FindObjectOfType<QuestionPanelController>();
            // Do NOT wire button clicks here. QuestionPanelController handles button clicks and sends answers via QuizManager.
        }

        public override void OnNetworkSpawn()
        {
            if (!enabled) return; // disabled due to QuizManager presence
            if (IsServer)
            {
                enabled = false;
                return;
            }
            if (QuestionManager_Server.Singleton != null)
            {
                QuestionManager_Server.Singleton.RemainingTime.OnValueChanged += UpdateTimerUI;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (QuestionManager_Server.Singleton != null)
            {
                QuestionManager_Server.Singleton.RemainingTime.OnValueChanged -= UpdateTimerUI;
            }
        }

        private void UpdateTimerUI(float previousValue, float newValue)
        {
            if (timerSlider != null)
            {
                timerSlider.value = newValue;
            }
        }

        [ClientRpc]
        public void DisplayQuestionClientRpc(string question, string answer1, string answer2, string answer3, string answer4)
        {
            if (questionPanelController != null)
            {
                var questionData = new QuestionData
                {
                    prompt = question,
                    options = new string[] { answer1, answer2, answer3, answer4 }
                };
                questionPanelController.ShowQuestion(questionData);
                // Raise EventBus for systems that gate combat and other behaviors
                EventBus.RaiseQuestionStarted(questionData);
            }
        }

        [ClientRpc]
        public void ShowAnswerResultClientRpc(bool isCorrect, int correctIndex, int chosenIndex, ClientRpcParams clientRpcParams = default)
        {
            // Drive resolution via centralized QuestionPanelController for consistent visuals and hide timing.
            if (questionPanelController != null)
            {
                questionPanelController.ShowResolution(chosenIndex, isCorrect, correctIndex);
            }
            else
            {
                // Fallback to local coloring if controller is missing
                SetButtonsInteractable(false);
                if (!isCorrect)
                {
                    var img = answerButtons[chosenIndex].GetComponent<Image>();
                    if (img != null) img.color = Color.red;
                }
            }
            // Raise client-side event for systems that rely on EventBus (e.g., guards)
            EventBus.RaiseAnswerSubmitted(chosenIndex, isCorrect);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            foreach (var button in answerButtons)
            {
                button.interactable = interactable;
            }
        }

        private void ResetButtonColors()
        {
            foreach (var button in answerButtons)
            {
                button.GetComponent<Image>().color = Color.white; // Or your default button color
            }
        }

        [ClientRpc]
        public void RevealCorrectAnswerClientRpc(int correctIndex)
        {
            // All clients will see the correct answer highlighted in green.
            answerButtons[correctIndex].GetComponent<Image>().color = Color.green;
            SetButtonsInteractable(false); // Prevent further answers
        }

        public void SubmitAnswer(int choice)
        {
            // Immediately suppress local player attacks when answering to prevent click-attack
            var localCombat = FindLocalPlayerCombat();
            if (localCombat != null)
            {
                localCombat.BeginAnswerInteractionLocal();
            }
            SubmitAnswerServerRpc(choice);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitAnswerServerRpc(int choice, ServerRpcParams rpcParams = default)
        {
            QuestionManager_Server.Singleton.ValidateAnswer(rpcParams.Receive.SenderClientId, choice);
        }

        private PlayerCombat FindLocalPlayerCombat()
        {
            var combats = Object.FindObjectsOfType<PlayerCombat>();
            foreach (var pc in combats)
            {
                if (pc.IsOwner) return pc;
            }
            return null;
        }
    }
}