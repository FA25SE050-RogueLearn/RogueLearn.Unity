// WebBridge.cs - Minimal WebGL bridge for messaging to/from host
#if !UNITY_WEBGL || UNITY_EDITOR
#define WEB_BRIDGE_STUB
#endif
using UnityEngine;
using System.Runtime.InteropServices;
using BossFight2D.Systems;

public class WebBridge : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void BMad_SendInit(string json);
    [DllImport("__Internal")] private static extern void BMad_SendQuestion(string json);
    [DllImport("__Internal")] private static extern void BMad_SendProgress(string json);
    [DllImport("__Internal")] private static extern void BMad_SendAnswer(string json);
    [DllImport("__Internal")] private static extern void BMad_SendComplete(string json);
    [DllImport("__Internal")] private static extern void BMad_SendError(string json);
#else
    private static void BMad_SendInit(string json) { Debug.Log("[Bridge] init: " + json); }
    private static void BMad_SendQuestion(string json) { Debug.Log("[Bridge] question: " + json); }
    private static void BMad_SendProgress(string json) { Debug.Log("[Bridge] progress: " + json); }
    private static void BMad_SendAnswer(string json) { Debug.Log("[Bridge] answer: " + json); }
    private static void BMad_SendComplete(string json) { Debug.Log("[Bridge] complete: " + json); }
    private static void BMad_SendError(string json) { Debug.LogError("[Bridge] error: " + json); }
#endif

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        // Notify host that Unity is ready
        SendInit();
        // Subscribe to gameplay events
        EventBus.GameStarted += OnGameStarted;
        EventBus.QuestionStarted += OnQuestionStarted;
        EventBus.AnswerSubmitted += OnAnswerSubmitted;
        EventBus.GameWon += OnGameWon;
        EventBus.GameLost += OnGameLost;
    }

    void OnDestroy()
    {
        EventBus.GameStarted -= OnGameStarted;
        EventBus.QuestionStarted -= OnQuestionStarted;
        EventBus.AnswerSubmitted -= OnAnswerSubmitted;
        EventBus.GameWon -= OnGameWon;
        EventBus.GameLost -= OnGameLost;
    }

    public void SendInit()
    {
        var payload = new { build = Application.version, scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name };
        BMad_SendInit(JsonUtility.ToJson(payload));
    }

    private void OnGameStarted()
    {
        var payload = new { type = "started" };
        BMad_SendProgress(JsonUtility.ToJson(payload));
    }

    private void OnQuestionStarted(QuestionData q)
    {
        var payload = new
        {
            id = q.id,
            prompt = q.prompt,
            a = q.options != null && q.options.Length > 0 ? q.options[0] : "",
            b = q.options != null && q.options.Length > 1 ? q.options[1] : "",
            c = q.options != null && q.options.Length > 2 ? q.options[2] : "",
            d = q.options != null && q.options.Length > 3 ? q.options[3] : "",
            time = q.timeLimitSec
        };
        BMad_SendQuestion(JsonUtility.ToJson(payload));
    }

    private void OnAnswerSubmitted(int choice, bool correct)
    {
        var payload = new { choice = choice, correct = correct };
        BMad_SendAnswer(JsonUtility.ToJson(payload));
    }

    private void OnGameWon()
    {
        var payload = new { result = "win" };
        BMad_SendComplete(JsonUtility.ToJson(payload));
    }

    private void OnGameLost()
    {
        var payload = new { result = "lose" };
        BMad_SendComplete(JsonUtility.ToJson(payload));
    }


}