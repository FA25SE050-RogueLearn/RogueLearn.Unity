using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using BossFight2D.UI;

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
            questionPanelController = FindObjectOfType<QuestionPanelController>();
            for (int i = 0; i < answerButtons.Length; i++)
            {
                int index = i;
                answerButtons[i].onClick.AddListener(() => SubmitAnswer(index));
            }
        }

        public override void OnNetworkSpawn()
        {
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
            }
        }

        [ClientRpc]
        public void ShowAnswerResultClientRpc(bool isCorrect, int correctIndex, int chosenIndex, ClientRpcParams clientRpcParams = default)
        {
            SetButtonsInteractable(false);
            if (!isCorrect)
            {
                answerButtons[chosenIndex].GetComponent<Image>().color = Color.red;
            }
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
            SubmitAnswerServerRpc(choice);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitAnswerServerRpc(int choice, ServerRpcParams rpcParams = default)
        {
            QuestionManager_Server.Singleton.ValidateAnswer(rpcParams.Receive.SenderClientId, choice);
        }
    }
}