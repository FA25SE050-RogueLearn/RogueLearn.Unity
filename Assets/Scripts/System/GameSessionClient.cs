using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using UnityEngine.Networking;
using UnityEngine.Scripting;
using BossFight2D.Systems;

namespace BossFight2D.Systems
{
    /// <summary>
    /// Handles resolving a Relay join code to a session and pack URL, fetching the pack,
    /// injecting it into QuizManager, and sending result summaries back to the backend.
    /// Designed to work both in WebGL (relative URLs) and desktop/editor (absolute base via env var).
    /// </summary>
    public class GameSessionClient : MonoBehaviour
    {
        public static GameSessionClient Instance { get; private set; }

        [Tooltip("Optional absolute base URL for the User API (e.g., http://localhost:5001). If empty, relative URLs will be used (WebGL).")]
        [SerializeField] private string userApiBaseUrl = "";

        [Tooltip("Last Relay join code received via WebGL or UI.")]
        [SerializeField] private string lastJoinCode = string.Empty;

        [Tooltip("Resolved session id from backend (match_id).")]
        [SerializeField] private string sessionId = string.Empty;

        [Tooltip("Resolved pack URL from backend.")]
        [SerializeField] private string packUrl = string.Empty;

        // When pack is fetched before Gameplay (no QuizManager yet), cache it and inject later
        private string pendingPackJson = null;
        private bool packInjected = false;
        public bool IsPackInjected => packInjected;
        private bool insecureTls = false;
        [SerializeField] private string appMode = "networked";
        private List<EventRecord> eventLog = new List<EventRecord>();
        private bool completionSent = false;
        [SerializeField] private string userId = string.Empty;
        private SummaryData lastCompletionSummary = new SummaryData { topics = new List<TopicSummaryData>() };
        private string lastCompletionResult = string.Empty;
        private string lastCompletionTimestamp = string.Empty;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); }
            else { Instance = this; DontDestroyOnLoad(gameObject); }

            // Default base from environment for desktop/headless; WebGL will prefer relative
            try
            {
                var envBase = Environment.GetEnvironmentVariable("USER_API_BASE");
                if (!string.IsNullOrWhiteSpace(envBase))
                {
                    userApiBaseUrl = envBase.TrimEnd('/');
                    Debug.Log($"[GameSessionClient] User API base from env: {userApiBaseUrl}");
                }
                var insecure = Environment.GetEnvironmentVariable("INSECURE_TLS");
                insecureTls = string.Equals(insecure, "1", StringComparison.OrdinalIgnoreCase);
            }
            catch { }

            // Subscribe to game end events to send completion
            EventBus.GameWon += OnGameWon;
            EventBus.GameLost += OnGameLost;
            EventBus.AnswerSubmitted += OnAnswerSubmitted;
            EventBus.QuestionStarted += OnQuestionStarted;
            // Listen for scene transitions so we can inject the pack when Gameplay loads
            SceneManager.sceneLoaded += OnSceneLoaded;
            var m = Environment.GetEnvironmentVariable("APP_MODE");
            if (!string.IsNullOrWhiteSpace(m)) appMode = m.Trim().ToLowerInvariant();
        }

        private void OnDestroy()
        {
            EventBus.GameWon -= OnGameWon;
            EventBus.GameLost -= OnGameLost;
            EventBus.AnswerSubmitted -= OnAnswerSubmitted;
            EventBus.QuestionStarted -= OnQuestionStarted;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnGameWon()
        {
            var nm = NetworkManager.Singleton;
            var isServer = nm != null && nm.IsServer;
            if (!isServer) return;
            if (completionSent) return;
            completionSent = true;
            _ = StartCoroutine(SendCompletionCoroutine("win"));
        }
        private void OnGameLost()
        {
            var nm = NetworkManager.Singleton;
            var isServer = nm != null && nm.IsServer;
            if (!isServer) return;
            if (completionSent) return;
            completionSent = true;
            _ = StartCoroutine(SendCompletionCoroutine("lose"));
        }

        /// <summary>
        /// Configure the absolute base URL for the User API from the embedding web page.
        /// Important for WebGL builds where relative URLs would otherwise target the page origin (e.g., Next.js dev server).
        /// Example: SendMessage("GameSessionClient", "SetUserApiBaseUrl", "http://localhost:5001");
        /// </summary>
        [Preserve]
        public void SetUserApiBaseUrl(string baseUrl)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(baseUrl))
                {
                    userApiBaseUrl = baseUrl.Trim().TrimEnd('/');
                    Debug.Log($"[GameSessionClient] User API base configured: {userApiBaseUrl}");
                }
                else
                {
                    Debug.LogWarning("[GameSessionClient] SetUserApiBaseUrl called with empty string; keeping existing config.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameSessionClient] Failed to set User API base: {e.Message}");
            }
        }

        /// <summary>
        /// Entry point called from RelayConnector when a join code is injected from the page.
        /// Begins resolving the code and fetching the pack.
        /// </summary>
        public void BeginAutoResolveWithJoinCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) { Debug.LogWarning("[GameSessionClient] Empty join code"); return; }
            lastJoinCode = code.Trim();
            StartCoroutine(ResolveAndLoadPackCoroutine(lastJoinCode));
        }

        [Serializable]
        private class EventRecord { public string id; public string topic; public string difficulty; public int time_ms; public bool correct; public string user_id; }
        private float lastQuestionStartTime = 0f;
        private string lastQuestionId = string.Empty;
        private string lastTopic = string.Empty;
        private string lastDifficulty = string.Empty;

        [Preserve]
        public void SetAppMode(string mode)
        {
            if (!string.IsNullOrWhiteSpace(mode)) appMode = mode.Trim().ToLowerInvariant();
        }

        [Preserve]
        public void SetUserId(string id)
        {
            if (!string.IsNullOrWhiteSpace(id)) userId = id.Trim();
        }

        /// <summary>
        /// Get the userId for this client
        /// </summary>
        public string GetUserId()
        {
            return userId;
        }

        [Preserve]
        public void InjectPackJson(string json)
        {
            pendingPackJson = json;
            if (appMode == "solo")
            {
                if (string.IsNullOrWhiteSpace(sessionId)) sessionId = System.Guid.NewGuid().ToString();
                var gameplay = "Gameplay";
                TryInjectPackIntoQuizManager();
                try { UnityEngine.SceneManagement.SceneManager.LoadScene(gameplay); } catch { }
                var gm = BossFight2D.Core.GameObjectFactory.FindOrCreate<BossFight2D.Core.GameManager>();
                gm?.StartGame();
            }
        }

        private void OnAnswerSubmitted(int choice, bool correct)
        {
            var qm = BossFight2D.Quiz.QuizManager.Instance ?? FindFirstObjectByType<BossFight2D.Quiz.QuizManager>();
            if (qm == null) return;
            var idxField = typeof(BossFight2D.Quiz.QuizManager).GetField("currentQuestionIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int idx = 0;
            if (idxField != null)
            {
                var nv = (Unity.Netcode.NetworkVariable<int>)idxField.GetValue(qm);
                idx = nv != null ? nv.Value : 0;
            }
            var qProp = typeof(BossFight2D.Quiz.QuizManager).GetField("questions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            List<QuestionData> list = qProp != null ? (List<QuestionData>)qProp.GetValue(qm) : null;
            if (list != null && idx >= 0 && idx < list.Count)
            {
                var q = list[idx];
                int ms = 0;
                try { var dt = Time.realtimeSinceStartup - lastQuestionStartTime; ms = dt > 0 ? Mathf.RoundToInt(dt * 1000f) : 0; } catch { }
                var rec = new EventRecord { id = q.id.ToString(), topic = string.IsNullOrEmpty(q.topic) ? lastTopic : q.topic, difficulty = string.IsNullOrEmpty(q.difficulty) ? lastDifficulty : q.difficulty, time_ms = ms, correct = correct, user_id = userId };
                eventLog.Add(rec);
            }
        }

        private void OnQuestionStarted(QuestionData q)
        {
            lastQuestionStartTime = Time.realtimeSinceStartup;
            try
            {
                lastQuestionId = q != null ? q.id.ToString() : string.Empty;
                lastTopic = q != null ? q.topic : string.Empty;
                var d = q != null ? q.difficulty : string.Empty;
                lastDifficulty = string.IsNullOrEmpty(d) ? lastDifficulty : d;
            }
            catch { }
        }

        private IEnumerator ResolveAndLoadPackCoroutine(string code)
        {
            var nm = NetworkManager.Singleton;
            var isServer = nm != null && nm.IsServer;
            if (!isServer)
            {
                Debug.Log("[GameSessionClient] Client skipping resolve; server will resolve and inject pack.");
                yield break;
            }

            var baseUrl = string.IsNullOrWhiteSpace(userApiBaseUrl) ? string.Empty : userApiBaseUrl;
            var resolveUrl = string.IsNullOrEmpty(baseUrl)
                ? $"/api/quests/game/sessions/resolve?code={UnityWebRequest.EscapeURL(code)}"
                : $"{baseUrl}/api/quests/game/sessions/resolve?code={UnityWebRequest.EscapeURL(code)}";

            Debug.Log($"[GameSessionClient] Resolving join code via {resolveUrl}");
            using (var req = UnityWebRequest.Get(resolveUrl))
            {
                if (insecureTls && Application.platform != RuntimePlatform.WebGLPlayer)
                {
                    req.certificateHandler = new InsecureCertHandler();
                }
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[GameSessionClient] Resolve failed: {req.error}. URL={resolveUrl}");
                    yield break;
                }
                var json = req.downloadHandler.text;
                var resolve = JsonUtility.FromJson<ResolveResponse>(json);
                if (resolve == null || string.IsNullOrWhiteSpace(resolve.match_id) || string.IsNullOrWhiteSpace(resolve.pack_url))
                {
                    Debug.LogError($"[GameSessionClient] Invalid resolve response: {json}");
                    yield break;
                }
                sessionId = resolve.match_id;
                packUrl = resolve.pack_url;
            }

            // Only the server fetches and injects the pack; clients do not need to download it.
            if (!isServer)
            {
                Debug.Log("[GameSessionClient] Resolve complete on client. Pack will be fetched and injected by the server.");
                yield break;
            }

            // Compute absolute pack URL when needed
            var fullPackUrl = packUrl;
            bool isAbsolute = packUrl.StartsWith("http://") || packUrl.StartsWith("https://");
            if (!isAbsolute)
            {
                if (string.IsNullOrWhiteSpace(userApiBaseUrl))
                {
                    Debug.LogError("[GameSessionClient] pack_url is relative but USER_API_BASE/userApiBaseUrl is empty. Set USER_API_BASE on the server or configure GameSessionClient.userApiBaseUrl.");
                    yield break;
                }
                fullPackUrl = userApiBaseUrl + (packUrl.StartsWith("/") ? packUrl : ("/" + packUrl));
            }

            Debug.Log($"[GameSessionClient] Fetching pack: {fullPackUrl}");
            using (var req2 = UnityWebRequest.Get(fullPackUrl))
            {
                if (insecureTls && Application.platform != RuntimePlatform.WebGLPlayer)
                {
                    req2.certificateHandler = new InsecureCertHandler();
                }
                yield return req2.SendWebRequest();
                if (req2.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[GameSessionClient] Pack fetch failed: {req2.error}. URL={fullPackUrl}");
                    yield break;
                }
                var packJson = req2.downloadHandler.text;
                // Cache JSON and attempt injection; if QuizManager isn't present yet (e.g., lobby scene), we'll try again on Gameplay load
                pendingPackJson = packJson;
                if (!TryInjectPackIntoQuizManager())
                {
                    Debug.Log("[GameSessionClient] Pack is ready. Waiting for Gameplay/QuizManager to load before injecting.");
                }
            }
        }

        private class InsecureCertHandler : CertificateHandler
        {
            protected override bool ValidateCertificate(byte[] certificateData) { return true; }
        }

        private IEnumerator PostJsonWithRetries(string url, byte[] bodyRaw, int maxAttempts)
        {
            int attempts = 0;
            while (attempts < maxAttempts)
            {
                attempts++;
                using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    req.useHttpContinue = false;
                    req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    req.chunkedTransfer = false;
                    try { req.uploadHandler.contentType = "application/json"; } catch { }
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("X-Rogue-Format", "v2");
                    req.SetRequestHeader("X-Rogue-Sender", "server");
                    try
                    {
                        var token = Environment.GetEnvironmentVariable("RESULTS_HTTP_TOKEN");
                        if (!string.IsNullOrWhiteSpace(token)) req.SetRequestHeader("Authorization", "Bearer " + token);
                    }
                    catch { }
                    if (insecureTls && Application.platform != RuntimePlatform.WebGLPlayer)
                    {
                        req.certificateHandler = new InsecureCertHandler();
                    }
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log("[GameSessionClient] POST ok");
                        yield break;
                    }
                    else
                    {
                        var respText = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
                        var respPreview = string.IsNullOrEmpty(respText) ? "" : (respText.Length > 200 ? respText.Substring(0, 200) + "..." : respText);
                        Debug.LogError($"[GameSessionClient] POST failed (attempt {attempts}) code={req.responseCode} err={req.error} body={respPreview}");
                        if (attempts < maxAttempts) yield return new WaitForSeconds(0.6f * attempts);
                    }
                }
            }
        }

        private IEnumerator SendCompletionCoroutine(string result)
        {
            if (completionSent == false) completionSent = true;
            if (string.IsNullOrWhiteSpace(sessionId)) yield break;
            yield return new WaitForSeconds(0.25f);
            var baseUrl = userApiBaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                try
                {
                    var envBase = Environment.GetEnvironmentVariable("USER_API_BASE");
                    if (!string.IsNullOrWhiteSpace(envBase)) baseUrl = envBase.TrimEnd('/');
                }
                catch { }
            }
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                Debug.LogError("[GameSessionClient] USER_API_BASE/userApiBaseUrl is not set; cannot POST completion.");
                yield break;
            }
            var completeUrl = baseUrl + $"/api/quests/game/sessions/{sessionId}/complete";

            yield return SendEventsBatchCoroutine();

            var summary = BuildSummary();
            var json = BuildCompletionJson(result, summary);
            Debug.Log($"[Unity] JSON being sent (len={json.Length}): {json}");
            lastCompletionSummary = summary ?? new SummaryData { topics = new List<TopicSummaryData>() };
            lastCompletionResult = result ?? string.Empty;
            lastCompletionTimestamp = DateTime.UtcNow.ToString("o");
            try
            {
                var preview = json.Length > 200 ? json.Substring(0, 200) + "..." : json;
                Debug.Log("[GameSessionClient] Completion JSON preview: " + preview);
                var qm = BossFight2D.Quiz.QuizManager.Instance ?? FindFirstObjectByType<BossFight2D.Quiz.QuizManager>();
                if (qm != null)
                {
                    var s = qm.GetTopicSummary();
                    int total = 0, correct = 0;
                    var listField = s.GetType().GetField("topics");
                    var listObj = listField != null ? listField.GetValue(s) as System.Collections.IEnumerable : null;
                    if (listObj != null)
                    {
                        foreach (var item in listObj)
                        {
                            var tTopic = item.GetType().GetField("topic");
                            var tTotal = item.GetType().GetField("total");
                            var tCorrect = item.GetType().GetField("correct");
                            string topicName = tTopic != null ? (string)tTopic.GetValue(item) : "";
                            int tt = tTotal != null ? (int)tTotal.GetValue(item) : 0;
                            int tc = tCorrect != null ? (int)tCorrect.GetValue(item) : 0;
                            total += tt; correct += tc;
                            Debug.Log($"[GameSessionClient] TopicStats: topic='{topicName}', total={tt}, correct={tc}");
                        }
                    }
                    Debug.Log($"[GameSessionClient] TopicStats aggregate: total={total}, correct={correct}");
                }
            }
            catch { }

            int attempts = 0;
            var bodyRaw = Encoding.UTF8.GetBytes(json);
            Debug.Log($"[Unity] Body bytes length: {bodyRaw.Length}");
            while (attempts < 3)
            {

                attempts++;
                using (var req = new UnityWebRequest(completeUrl, UnityWebRequest.kHttpVerbPOST))
                {
                    req.useHttpContinue = false;
                    req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    req.chunkedTransfer = false;
                    try { req.uploadHandler.contentType = "application/json"; } catch { }
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("X-Rogue-Format", "v2");
                    req.SetRequestHeader("X-Rogue-Sender", "server");

                    try
                    {
                        var token = Environment.GetEnvironmentVariable("RESULTS_HTTP_TOKEN");
                        if (!string.IsNullOrWhiteSpace(token)) req.SetRequestHeader("Authorization", "Bearer " + token);
                    }
                    catch { }
                    if (insecureTls && Application.platform != RuntimePlatform.WebGLPlayer)
                    {
                        req.certificateHandler = new InsecureCertHandler();
                    }
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log("[GameSessionClient] Completion sent.");
                        EventBus.GameWon -= OnGameWon;
                        EventBus.GameLost -= OnGameLost;
                        yield break;
                    }
                    else
                    {
                        var respText = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
                        var respPreview = string.IsNullOrEmpty(respText) ? "" : (respText.Length > 200 ? respText.Substring(0, 200) + "..." : respText);
                        Debug.LogError($"[GameSessionClient] Completion POST failed (attempt {attempts}) code={req.responseCode} err={req.error} body={respPreview}");
                        if (attempts < 3) yield return new WaitForSeconds(0.6f * attempts);
                    }
                }
            }
        }

        private IEnumerator SendEventsBatchCoroutine()
        {
            if (string.IsNullOrWhiteSpace(sessionId)) yield break;
            if (eventLog == null || eventLog.Count == 0) yield break;
            var baseUrl = userApiBaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                try
                {
                    var envBase = Environment.GetEnvironmentVariable("USER_API_BASE");
                    if (!string.IsNullOrWhiteSpace(envBase)) baseUrl = envBase.TrimEnd('/');
                }
                catch { }
            }
            if (string.IsNullOrWhiteSpace(baseUrl)) yield break;
            var url = baseUrl + $"/api/quests/game/sessions/{sessionId}/events";
            var payload = new EventsPayload { events = new List<EventRecord>(eventLog) };
            var json = JsonUtility.ToJson(payload);
            var bodyRaw = Encoding.UTF8.GetBytes(json);
            yield return PostJsonWithRetries(url, bodyRaw, 2);
            eventLog.Clear();
        }

        [Serializable]
        private class EventsPayload { public List<EventRecord> events; }

        [Serializable]
        private class ResolveResponse { public string match_id; public string pack_url; }
        [Serializable]
        public class TopicSummaryData { public string topic; public int total; public int correct; }
        [Serializable]
        public class SummaryData { public List<TopicSummaryData> topics; }
        [Serializable]
        public class PerPlayerSummary { public string user_id; public SummaryData summary; }
        public class CompletionPayload { public string format; public string result; public string timestamp; public SummaryData summary; public List<PerPlayerSummary> per_player; }

        private SummaryData BuildSummary()
        {
            var qm = BossFight2D.Quiz.QuizManager.Instance ?? FindFirstObjectByType<BossFight2D.Quiz.QuizManager>();
            if (qm != null)
            {
                try
                {
                    var s = (BossFight2D.Quiz.QuizManager.SummaryData)qm.GetTopicSummary();
                    var outS = new SummaryData { topics = new List<TopicSummaryData>() };
                    if (s != null && s.topics != null)
                    {
                        foreach (var t in s.topics)
                        {
                            outS.topics.Add(new TopicSummaryData { topic = t.topic, total = t.total, correct = t.correct });
                        }
                    }
                    return outS;
                }
                catch { }
            }
            return new SummaryData { topics = new List<TopicSummaryData>() };
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // On every scene load, attempt pack injection again (most importantly when Gameplay loads)
            TryInjectPackIntoQuizManager();
        }

        /// <summary>
        /// Attempts to inject the cached backend pack into QuizManager. Only runs on server.
        /// Returns true if injection succeeded, false otherwise.
        /// </summary>
        private bool TryInjectPackIntoQuizManager()
        {
            if (packInjected) return true;
            if (string.IsNullOrWhiteSpace(pendingPackJson)) return false;

            var nm = NetworkManager.Singleton;
            var isServer = nm != null && nm.IsServer;
            if (!isServer)
            {
                // Clients don't need to inject the pack; server is authoritative
                return false;
            }

            var qm = BossFight2D.Quiz.QuizManager.Instance ?? FindFirstObjectByType<BossFight2D.Quiz.QuizManager>();
            if (qm == null)
            {
                // QuizManager not yet present (likely still in lobby). Try again after Gameplay loads.
                return false;
            }

            try
            {
                qm.LoadQuestionsFromBackendJson(pendingPackJson);
                packInjected = true;
                Debug.Log("[GameSessionClient] Pack injected into QuizManager.");
                // Notify that game can begin (optional)
                EventBus.RaiseGameStarted();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameSessionClient] Failed to inject pack into QuizManager: {e.Message}");
                return false;
            }
        }


        private string BuildCompletionJson(string result, SummaryData summary)
        {
            var payload = new CompletionPayload
            {
                format = "v2",
                result = result,
                timestamp = DateTime.UtcNow.ToString("o"),
                summary = summary ?? new SummaryData { topics = new List<TopicSummaryData>() },
                per_player = new List<PerPlayerSummary>()
            };

            // MVP: Add all connected players, not just the host
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && nm.IsServer)
            {
                foreach (var clientId in nm.ConnectedClientsIds)
                {
                    // Build per-player summary using QuizManager data
                    var playerSummary = BuildPlayerSummary(clientId);
                    payload.per_player.Add(new PerPlayerSummary
                    {
                        user_id = clientId.ToString(), // Use clientId as string for now (backend can map to actual userId)
                        summary = playerSummary
                    });
                }
            }
            else if (!string.IsNullOrWhiteSpace(userId))
            {
                // Fallback: Single player mode, use host's userId
                payload.per_player.Add(new PerPlayerSummary { user_id = userId, summary = summary ?? new SummaryData { topics = new List<TopicSummaryData>() } });
            }

            return JsonUtility.ToJson(payload);
        }

        private SummaryData BuildPlayerSummary(ulong clientId)
        {
            var qm = BossFight2D.Quiz.QuizManager.Instance;
            if (qm == null) return new SummaryData { topics = new List<TopicSummaryData>() };

            // Get QuizManager's topic summary and convert to GameSessionClient format
            var quizSummary = qm.GetTopicSummary();

            // Convert from QuizManager.SummaryData to GameSessionClient.SummaryData
            var convertedTopics = new List<TopicSummaryData>();
            foreach (var topic in quizSummary.topics)
            {
                convertedTopics.Add(new TopicSummaryData
                {
                    topic = topic.topic,
                    total = topic.total,
                    correct = topic.correct
                });
            }

            return new SummaryData { topics = convertedTopics };
        }
        public bool TryGetLatestCompletion(out string result, out string timestamp, out SummaryData summary)
        {
            result = lastCompletionResult;
            timestamp = lastCompletionTimestamp;
            summary = lastCompletionSummary;
            return true;
        }

        public IEnumerator FetchResultFromBackend(Action<string, string, SummaryData> onDone)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) { onDone?.Invoke(string.Empty, string.Empty, new SummaryData { topics = new List<TopicSummaryData>() }); yield break; }
            var baseUrl = string.IsNullOrWhiteSpace(userApiBaseUrl) ? string.Empty : userApiBaseUrl;
            var url = string.IsNullOrEmpty(baseUrl)
                ? $"/api/quests/game/sessions/{sessionId}/result"
                : $"{baseUrl}/api/quests/game/sessions/{sessionId}/result";
            using (var req = UnityWebRequest.Get(url))
            {
                if (insecureTls && Application.platform != RuntimePlatform.WebGLPlayer)
                {
                    req.certificateHandler = new InsecureCertHandler();
                }
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onDone?.Invoke(string.Empty, string.Empty, new SummaryData { topics = new List<TopicSummaryData>() });
                }
                else
                {
                    var json = req.downloadHandler.text;
                    try
                    {
                        var payload = JsonUtility.FromJson<CompletionPayload>(json);
                        var s = payload != null ? payload.summary : new SummaryData { topics = new List<TopicSummaryData>() };
                        onDone?.Invoke(payload?.result ?? string.Empty, payload?.timestamp ?? string.Empty, s);
                    }
                    catch
                    {
                        onDone?.Invoke(string.Empty, string.Empty, new SummaryData { topics = new List<TopicSummaryData>() });
                    }
                }
            }
        }
    }
}
