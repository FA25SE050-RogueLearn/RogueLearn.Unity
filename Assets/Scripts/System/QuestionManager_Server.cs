using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace BossFight2D.Systems
{
    public class QuestionManager_Server : NetworkBehaviour
    {
        public static QuestionManager_Server Singleton { get; private set; }


        public QuestionPackData Pack;
        
        public NetworkVariable<int> CurrentIndex = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<float> RemainingTime = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private QuestionManager_Client questionManager_Client;
        private Coroutine questionTimerCoroutine;
        private NetworkVariable<bool> isQuestionActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private void Awake()
        {
            if (Singleton != null && Singleton != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Singleton = this;
            }
            questionManager_Client = GetComponent<QuestionManager_Client>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                enabled = false;
                return;
            }
            LoadQuestions();
        }

        private void LoadQuestions()
        {
            string filePath = Path.Combine(Application.streamingAssetsPath, "questions_pack1.json");

            if (File.Exists(filePath))
            {
                string dataAsJson = File.ReadAllText(filePath);
                var w = JsonUtility.FromJson<Wrapper>(dataAsJson);
                Pack = w != null ? w.pack : null;
                if (Pack != null) { Debug.Log($"Loaded Question Pack: {Pack.name} with {Pack.questions.Count} questions."); }
                else { Debug.LogError("Failed to load or parse question pack."); }
            }
            else { Debug.LogError("questionsJson TextAsset is null. Make sure 'questions_pack1.json' exists in the StreamingAssets folder."); }
        }

        public void SelectAndSendNextQuestion()
        {
            if (Pack == null || Pack.questions == null) { Debug.LogError("Question pack not loaded."); return; }

            CurrentIndex.Value++;
            if (CurrentIndex.Value >= Pack.questions.Count) 
            { 
                Debug.Log("End of questions.");
                isQuestionActive.Value = false;
                return; 
            }

            var q = Pack.questions[CurrentIndex.Value];

            if (q.options.Length != 4)
            {
                Debug.LogError($"Question ID {q.id} does not have exactly 4 options. Skipping.");
                SelectAndSendNextQuestion();
                return;
            }

            isQuestionActive.Value = true;
            questionManager_Client.DisplayQuestionClientRpc(q.prompt, q.options[0], q.options[1], q.options[2], q.options[3]);
            StartQuestionTimer(q.timeLimitSec);
        }

        private void StartQuestionTimer(float time)
        {
            if (questionTimerCoroutine != null)
            {
                StopCoroutine(questionTimerCoroutine);
            }
            RemainingTime.Value = time;
            questionTimerCoroutine = StartCoroutine(QuestionTimer());
        }

        private IEnumerator QuestionTimer()
        {
            while (RemainingTime.Value > 0)
            {
                RemainingTime.Value -= Time.deltaTime;
                yield return null;
            }

            if (!isQuestionActive.Value) yield break; 

            Debug.Log("Server: Question timed out.");
            isQuestionActive.Value = false;
            
            var q = Pack.questions[CurrentIndex.Value];
            StartCoroutine(RevealAndProceed(q.correctIndex, 2.0f));
        }

        public void ValidateAnswer(ulong clientId, int answerIndex)
        {
            if (!IsServer || !isQuestionActive.Value) return;

            isQuestionActive.Value = false;
            if (questionTimerCoroutine != null)
            {
                StopCoroutine(questionTimerCoroutine);
                questionTimerCoroutine = null;
            }

            var q = Pack.questions[CurrentIndex.Value];
            bool isCorrect = q.correctIndex == answerIndex;

            Debug.Log($"Server received answer '{answerIndex}' from client {clientId}. Correct: {isCorrect}");

            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            };
            questionManager_Client.ShowAnswerResultClientRpc(isCorrect, q.correctIndex, answerIndex, clientRpcParams);

            StartCoroutine(RevealAndProceed(q.correctIndex, 1.5f));
        }

        private IEnumerator RevealAndProceed(int correctIndex, float delay)
        {
            questionManager_Client.RevealCorrectAnswerClientRpc(correctIndex);

            yield return new WaitForSeconds(delay);
            SelectAndSendNextQuestion();
        }
    }
}