using UnityEngine;
using System.Linq;
using BossFight2D.Systems;
using BossFight2D.Core;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
/// <summary>
/// Ensures essential singleton-like runtime objects exist across scenes.
/// </summary>
public class SceneBootstrapper : MonoBehaviour
{
    private static bool _spawned;
    private static bool _lobbySetupAttempted;

    /// <summary>
    /// Creates a persistent bootstrapper instance once after the first scene load.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (_spawned) return;
        var bootstrapperObject = new GameObject("SceneBootstrapper");
        DontDestroyOnLoad(bootstrapperObject);
        bootstrapperObject.AddComponent<SceneBootstrapper>();
        _spawned = true;
    }

    /// <summary>
    /// Runs per-scene initialization for the currently active scene.
    /// </summary>
    private void Start()
    {
        var sceneName = SceneManager.GetActiveScene().name;
        Debug.Log($"[SceneBootstrapper] Start() in scene '{sceneName}'. isBatchMode={Application.isBatchMode}");
        if (ShouldSkipBootstrap(sceneName))
        {
            return;
        }

        EnsureGlobalServicesForScene(sceneName);
        var isMainMenu = FindFirstObjectByType<BossFight2D.UI.MainMenuUI>() != null;
        if (isMainMenu)
        {
            EnsureGameManagerOnly();
            return;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Handles scene transitions for persistent services and Gameplay helpers.
    /// </summary>
    /// <param name="scene">The scene that finished loading.</param>
    /// <param name="mode">The load mode used for the transition.</param>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[SceneBootstrapper] OnSceneLoaded: name='{scene.name}', mode={mode}");
        if (!ShouldSkipBootstrap(scene.name))
        {
            EnsureGlobalServicesForScene(scene.name);
        }

        if (scene.name == "Gameplay")
        {
            EnsureGameplayStartupExists();
            TryAssignGameplayCameraFollow();
        }
    }

    /// <summary>
    /// Returns true when a scene should not receive auto-bootstrap services.
    /// </summary>
    /// <param name="sceneName">The active scene name.</param>
    private static bool ShouldSkipBootstrap(string sceneName)
    {
        return sceneName == "Dashboard" || sceneName == "DashboardCharacterPreview";
    }

    /// <summary>
    /// Ensures persistent services needed by both menu and gameplay flows exist.
    /// </summary>
    /// <param name="sceneName">The active scene name.</param>
    private void EnsureGlobalServicesForScene(string sceneName)
    {
        TrySetupLobby(sceneName);
        EnsureRelayConnectorExists();
        EnsureGameSessionClientExists();
        EnsureNetworkEventLoggerExists();
    }

    /// <summary>
    /// Ensures a <see cref="BossFight2D.Network.GameplayStartup"/> exists in gameplay scenes.
    /// </summary>
    private void EnsureGameplayStartupExists()
    {
        if (FindFirstObjectByType<BossFight2D.Network.GameplayStartup>() != null) return;

        var gameplayStartupObject = new GameObject("GameplayStartup");
        var gameplayStartup = gameplayStartupObject.AddComponent<BossFight2D.Network.GameplayStartup>();
        gameplayStartup.spawnPoints = ResolveSpawnPoints();
    }

    /// <summary>
    /// Resolves spawn points via tag lookup first, falling back to a named container.
    /// </summary>
    private Transform[] ResolveSpawnPoints()
    {
        var spawnPointsByTag = TryFindSpawnPointsByTag("SpawnPoint");
        if (spawnPointsByTag != null && spawnPointsByTag.Length > 0)
        {
            Debug.Log($"[SceneBootstrapper] Found {spawnPointsByTag.Length} spawn points via tag.");
            return spawnPointsByTag;
        }

        var spawnPointsByContainer = TryFindSpawnPointsByContainer("SpawnPoints");
        if (spawnPointsByContainer != null && spawnPointsByContainer.Length > 0)
        {
            Debug.Log($"[SceneBootstrapper] Found {spawnPointsByContainer.Length} spawn points via 'SpawnPoints' container.");
            return spawnPointsByContainer;
        }

        Debug.LogWarning("[SceneBootstrapper] No spawn points found. Ensure either tag 'SpawnPoint' exists with placed objects, or create a 'SpawnPoints' container with child transforms.");
        return System.Array.Empty<Transform>();
    }

    /// <summary>
    /// Finds spawn points using a Unity tag if it is defined.
    /// </summary>
    /// <param name="tag">The Unity tag to search for.</param>
    private static Transform[] TryFindSpawnPointsByTag(string tag)
    {
        try
        {
            var tagged = GameObject.FindGameObjectsWithTag(tag);
            if (tagged == null || tagged.Length == 0) return null;
            return System.Array.ConvertAll(tagged, t => t.transform);
        }
        catch (UnityException ex)
        {
            Debug.LogWarning($"[SceneBootstrapper] Tag '{tag}' not defined in project. Fallback to container search. Details: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Finds spawn points as children of a named container GameObject.
    /// </summary>
    /// <param name="containerName">The GameObject name containing spawn point transforms.</param>
    private static Transform[] TryFindSpawnPointsByContainer(string containerName)
    {
        var container = GameObject.Find(containerName);
        if (container == null) return null;

        var list = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in container.transform)
        {
            list.Add(child);
        }
        return list.ToArray();
    }

    /// <summary>
    /// Assigns the Cinemachine follow target to the local owned player if possible.
    /// </summary>
    private void TryAssignGameplayCameraFollow()
    {
        try
        {
            var camSetup = FindFirstObjectByType<BossFight2D.CameraSystem.MultiplayerCinemachineSetup>();
            if (camSetup != null)
            {
                camSetup.RefreshLocalPlayer();
                return;
            }

            var vcam = FindFirstObjectByType<Cinemachine.CinemachineVirtualCamera>();
            if (vcam == null) return;

            var ownedPlayer = GameObject.FindObjectsOfType<BossFight2D.Player.PlayerController>(false)
                .FirstOrDefault(pc => pc.IsOwner);
            if (ownedPlayer != null)
            {
                vcam.Follow = ownedPlayer.transform;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SceneBootstrapper] Failed to assign Cinemachine camera follow: {ex.Message}");
        }
    }

    /// <summary>
    /// Ensures lobby UI/state exists in lobby-related scenes.
    /// </summary>
    /// <param name="sceneName">Active scene name.</param>
    private void TrySetupLobby(string sceneName)
    {
        if (_lobbySetupAttempted && SceneManager.GetActiveScene().name == sceneName) return;
        if (sceneName == "ServerHeadless" || sceneName == "ClientUI" || sceneName == "HostUI")
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && nm.IsServer)
            {
                var lobby = FindFirstObjectByType<BossFight2D.Network.LobbyStateManager>();
                if (lobby == null)
                {
                    Debug.LogWarning("[SceneBootstrapper] LobbyStateManager not found in scene. Readiness UI/flow will be limited.");
                }
            }

            if (!Application.isBatchMode && (nm == null || !nm.IsServer))
            {
                if (FindFirstObjectByType<LobbyUI>() == null)
                {
                    var uiGo = new GameObject("LobbyUI");
                    uiGo.AddComponent<LobbyUI>();
                }
            }

            _lobbySetupAttempted = true;
        }
    }
    /// <summary>
    /// Ensures the minimum runtime objects required for menu navigation exist.
    /// </summary>
    private void EnsureGameManagerOnly()
    {
        var gm = FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            var systemsRoot = new GameObject("Systems");
            systemsRoot.AddComponent<GameManager>();
        }
        EnsureRelayConnectorExists();
    }

    /// <summary>
    /// Ensures a <see cref="RelayConnector"/> exists for UI and WebGL join messaging.
    /// </summary>
    private void EnsureRelayConnectorExists()
    {
        RelayConnector.GetOrCreate("RelayConnector");
    }

    /// <summary>
    /// Ensures a <see cref="GameSessionClient"/> exists for backend session resolution and pack injection.
    /// </summary>
    private void EnsureGameSessionClientExists()
    {
        if (FindFirstObjectByType<GameSessionClient>() == null)
        {
            var go = new GameObject("GameSessionClient");
            go.AddComponent<GameSessionClient>();
        }
    }

    /// <summary>
    /// Ensures a <see cref="NetworkEventLogger"/> exists for runtime networking diagnostics.
    /// </summary>
    private void EnsureNetworkEventLoggerExists()
    {
        if (FindFirstObjectByType<NetworkEventLogger>() == null)
        {
            var go = new GameObject("NetworkEventLogger");
            go.AddComponent<NetworkEventLogger>();
        }
    }
}
