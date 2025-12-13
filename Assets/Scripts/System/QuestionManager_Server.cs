using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;
using BossFight2D.Quiz;

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
            // Auto-disable legacy manager if QuizManager exists in the scene
            var qm = QuizManager.Instance ?? UnityEngine.Object.FindFirstObjectByType<QuizManager>();
            if (qm != null)
            {
                Debug.Log("[QuestionManager_Server] QuizManager detected; disabling legacy QuestionManager_Server to avoid conflicts. Please remove this component from the scene.");
                enabled = false;
                return;
            }
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
            if (!enabled) return; // disabled due to QuizManager presence
            if (!IsServer)
            {
                enabled = false;
                return;
            }
            // Load questions on the server using a WebGL-friendly approach
            StartCoroutine(LoadQuestionsCoroutine());
        }

        private IEnumerator LoadQuestionsCoroutine()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "questions_pack1.json");

#if UNITY_WEBGL && !UNITY_EDITOR
            // In WebGL, StreamingAssets must be loaded via UnityWebRequest
            using (var req = UnityWebRequest.Get(path))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Failed to load questions from '{path}' on WebGL: {req.error}. Fallback to Resources.");
                    FallbackLoadFromResources();
                    yield break;
                }
                string dataAsJson = req.downloadHandler.text;
                ApplyPackFromJson(dataAsJson);
                yield break;
            }
#else
            // Non-WebGL: prefer direct file I/O, fallback to UnityWebRequest if needed
            if (File.Exists(path))
            {
                string dataAsJson = File.ReadAllText(path);
                ApplyPackFromJson(dataAsJson);
                yield break;
            }
            else
            {
                using (var req = UnityWebRequest.Get(path))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        string dataAsJson = req.downloadHandler.text;
                        ApplyPackFromJson(dataAsJson);
                    }
                    else
                    {
                        Debug.LogError($"Failed to load questions from '{path}': {req.error}. Fallback to Resources.");
                        FallbackLoadFromResources();
                    }
                }
            }
#endif
        }

        private void ApplyPackFromJson(string dataAsJson)
        {
            var w = JsonUtility.FromJson<Wrapper>(dataAsJson);
            Pack = w != null ? w.pack : null;
            if (Pack != null) { Debug.Log($"Loaded Question Pack: {Pack.name} with {Pack.questions.Count} questions."); }
            else { Debug.LogError("Failed to load or parse question pack."); }
        }

        private void FallbackLoadFromResources()
        {
            // Optional fallback: load from Resources/QuestionPacks/questions_pack1
            var ta = Resources.Load<TextAsset>("QuestionPacks/questions_pack1");
            if (ta != null)
            {
                ApplyPackFromJson(ta.text);
            }
            else
            {
                Debug.LogError("Could not load 'questions_pack1' from Resources as a fallback. Ensure the JSON exists in StreamingAssets or Resources.");
            }
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

            // If correct, trigger Power Play and let PowerPlayManager/QuizManager drive the next question.
            // Otherwise, proceed to reveal and continue.
            if (isCorrect)
            {
                var ppm = BossFight2D.Core.PowerPlayManager.Instance;
                if (ppm != null)
                {
                    ppm.StartPowerPlay(clientId);
                    // Do not immediately proceed; QuizManager will start the next question after Power Play ends.
                }
                else
                {
                    // Fallback: continue question flow if PowerPlayManager is not present
                    StartCoroutine(RevealAndProceed(q.correctIndex, 1.5f));
                }
            }
            else
            {
                StartCoroutine(RevealAndProceed(q.correctIndex, 1.5f));
            }
        }

        private IEnumerator RevealAndProceed(int correctIndex, float delay)
        {
            questionManager_Client.RevealCorrectAnswerClientRpc(correctIndex);

            yield return new WaitForSeconds(delay);
            SelectAndSendNextQuestion();
        }
    }
}