// SceneBootstrapper.cs - auto-creates minimal runtime objects if missing
using UnityEngine;
using BossFight2D.Systems;
using BossFight2D.Player;
using BossFight2D.Boss;
using BossFight2D.Core;
using BossFight2D.UI;
using Cinemachine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public class SceneBootstrapper : MonoBehaviour
{
    private static bool _spawned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (_spawned) return;
        var go = new GameObject("SceneBootstrapper");
        DontDestroyOnLoad(go);
        go.AddComponent<SceneBootstrapper>();
        _spawned = true;
    }

    private void Start()
    {
        // Skip bootstrap in Dashboard or character preview scenes to avoid spawning gameplay systems
        var sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "Dashboard" || sceneName == "DashboardCharacterPreview")
        {
            return;
        }
        // If this scene contains a Main Menu, avoid spawning gameplay systems
        var isMainMenu = FindFirstObjectByType<BossFight2D.UI.MainMenuUI>() != null;
        if (isMainMenu)
        {
            EnsureGameManagerOnly();
            // Do not spawn player, boss, question systems, or ready station in the main menu.
            return;
        }

        // EnsureSystems();
        // var player = EnsurePlayer();
        //EnsureBoss();
        // Optionally: you can add camera or lighting bootstrap here later

        // Gate game start via a ReadyStation trigger near the player
        // EnsureReadyStation(player);
        // Start is gated by ReadyStation; do not auto-start here.
        // The game will transition from Init to Playing when the player marks Ready at the station.
    }

    private void EnsureSystems()
    {
        var gm = FindFirstObjectByType<GameManager>();
        GameObject systemsRoot = null;

        if (gm == null)
        {
            systemsRoot = new GameObject("Systems");
            gm = systemsRoot.AddComponent<GameManager>();
        }
        else
        {
            systemsRoot = gm.gameObject;
        }

        // Ensure WebBridge is present for WebGL messaging
        if (systemsRoot.GetComponent<WebBridge>() == null)
        {
            systemsRoot.AddComponent<WebBridge>();
        }

        // Ensure PlayerHUD exists to drive Health/Focus UI (sliders named "Health" and "Focus")
        if (FindFirstObjectByType<PlayerHUD>() == null)
        {
            systemsRoot.AddComponent<PlayerHUD>();
        }

        // Ensure BossHUD exists to drive Boss Health UI (slider named "BossHealth")
        if (FindFirstObjectByType<BossFight2D.UI.BossHUD>() == null)
        {
            systemsRoot.AddComponent<BossFight2D.UI.BossHUD>();
        }

        // Ensure GameStateUI exists so gameplay theme music and Win/Lose overlays are available
        if (FindFirstObjectByType<BossFight2D.UI.GameStateUI>() == null)
        {
            systemsRoot.AddComponent<BossFight2D.UI.GameStateUI>();
        }

        // Ensure PauseMenu exists so player can open settings/pause with Escape
        if (FindFirstObjectByType<BossFight2D.UI.PauseMenu>() == null)
        {
            systemsRoot.AddComponent<BossFight2D.UI.PauseMenu>();
        }

        // Ensure BossHealth slider exists in the UI (auto-create if missing)
        EnsureBossHealthUIExists();
    }

    private GameObject EnsurePlayer()
    {
        var playerController = FindFirstObjectByType<PlayerController>();
        GameObject go;
        if (playerController != null)
        {
            go = playerController.gameObject;
        }
        else
        {
            go = new GameObject("Player");
            go.layer = TryGetLayer("Player");
            go.tag = "Player";

            var camera = FindFirstObjectByType<CinemachineVirtualCamera>();

            if (camera)
            {
                camera.Follow = go.transform;
            }

            // Visual placeholder sprite (simple 1x1 green square)
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateColorSprite(Color.green);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            go.AddComponent<CircleCollider2D>();
            go.AddComponent<PlayerController>();
        }

        // Ensure health/focus components exist so user can wire them up in Inspector
        if (go.GetComponent<PlayerHealth>() == null) go.AddComponent<PlayerHealth>();
        if (go.GetComponent<PlayerFocus>() == null) go.AddComponent<PlayerFocus>();
        // Ensure question guard to freeze input & invincibility during questions
        if (go.GetComponent<BossFight2D.Player.PlayerQuestionGuard>() == null) go.AddComponent<BossFight2D.Player.PlayerQuestionGuard>();

        return go;
    }

    private void EnsureBoss()
    {
        var boss = FindFirstObjectByType<BossStateMachine>();
        if (boss != null) return;

        var go = new GameObject("Boss");
        go.layer = TryGetLayer("Boss");

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateColorSprite(Color.red);

        go.AddComponent<BoxCollider2D>();
        go.AddComponent<BossStateMachine>();
        go.AddComponent<BossHealth>();
        go.AddComponent<BossController>();
        go.AddComponent<Unity.Netcode.NetworkObject>();
    }

    private int TryGetLayer(string name)
    {
        int layer = LayerMask.NameToLayer(name);
        return layer < 0 ? 0 : layer;
    }

    private Sprite CreateColorSprite(Color c)
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, c);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 16f);
    }

    private void EnsureBossHealthUIExists()
    {
        // If a Slider named "BossHealth" exists, we're done
        var existing = GameObject.Find("BossHealth");
        if (existing != null && existing.GetComponent<Slider>() != null) return;

        // Find or create a Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        // Create Slider hierarchy
        var sliderGO = new GameObject("BossHealth", typeof(RectTransform), typeof(Slider));
        sliderGO.transform.SetParent(canvas.transform, false);
        var rt = sliderGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -20f);
        rt.sizeDelta = new Vector2(300f, 20f);

        var slider = sliderGO.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.interactable = false; // progress bar behavior

        // Background
        var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(sliderGO.transform, false);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one; bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGO.GetComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.5f);

        // Fill Area
        var fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        var faRT = fillAreaGO.GetComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0f, 0f);
        faRT.anchorMax = new Vector2(1f, 1f);
        faRT.offsetMin = new Vector2(3f, 3f);
        faRT.offsetMax = new Vector2(-3f, -3f);

        // Fill
        var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0f, 0f);
        fillRT.anchorMax = new Vector2(1f, 1f);
        fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;
        var fillImg = fillGO.GetComponent<Image>();
        fillImg.color = new Color(0.8f, 0.1f, 0.1f, 0.9f); // red-ish

        // Wire slider graphics
        slider.targetGraphic = bgImg;
        slider.fillRect = fillRT;
        slider.handleRect = null; // no handle for progress bar
        slider.direction = Slider.Direction.LeftToRight;
    }

    // Create or reuse a single-player ReadyStation near the player to gate game start
    private void EnsureReadyStation(GameObject player)
    {
        if (FindFirstObjectByType<ReadyStation>() != null) return;
        var go = new GameObject("ReadyStation");
        go.transform.position = player != null ? player.transform.position + new Vector3(1.5f, 0f, 0f) : Vector3.zero;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(2f, 2f);
        go.AddComponent<ReadyStation>();
    }

    // Minimal systems for Main Menu scenes: only ensure GameManager so menu can control start.
    private void EnsureGameManagerOnly()
    {
        var gm = FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            var systemsRoot = new GameObject("Systems");
            systemsRoot.AddComponent<GameManager>();
        }
        // Avoid creating QuestionManager, Player/Boss HUDs, Lifelines, Dev Hotkeys, etc.
    }
}