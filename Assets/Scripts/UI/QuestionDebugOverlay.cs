// using UnityEngine;
// using BossFight2D.Quiz;
// using Unity.Netcode;

// namespace BossFight2D.UI
// {
//     public class QuestionDebugOverlay : MonoBehaviour
//     {
//         private void OnGUI()
//         {
//             if (QuizManager.Instance == null || QuizManager.Instance.State.Value != QuizState.Question) return;

//             // This is a debug tool, so it's okay to find the questions this way.
//             var questions = FindObjectOfType<QuizManager>().GetComponent<QuizManager>().GetType().GetField("questions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(QuizManager.Instance) as System.Collections.Generic.List<Systems.QuestionData>;
//             var currentQuestionIndex = FindObjectOfType<QuizManager>().GetComponent<QuizManager>().GetType().GetField("currentQuestionIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(QuizManager.Instance) as NetworkVariable<int>;

//             if (questions == null || questions.Count == 0) return;

//             var q = questions[currentQuestionIndex.Value];
//             var w = Mathf.Min(600, Screen.width - 20); var x = 10; var y = 10; var line = 22; int h = 10 + line * (3 + q.Answers.Length);
//             GUI.Box(new Rect(x, y, w, h), "Question");
//             y += 24;
//             GUI.Label(new Rect(x + 8, y, w - 16, line * 2), q.QuestionText); y += line * 2;
//             GUI.Label(new Rect(x + 8, y, w - 16, line), $"Time: N/A (Press 1-4 or click options)"); y += line;
//             for (int i = 0; i < q.Answers.Length; i++)
//             {
//                 if (GUI.Button(new Rect(x + 8, y, w - 16, line + 6), $"{i + 1}. {q.Answers[i]}"))
//                 {
//                     if (NetworkManager.Singleton.IsClient)
//                     {
//                         QuizManager.Instance.SubmitAnswer(NetworkManager.Singleton.LocalClientId, i);
//                     }
//                 }
//                 y += line + 8;
//             }
//         }
//     }
// }