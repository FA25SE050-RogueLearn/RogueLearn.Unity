using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Logs the build settings scene list and indexes at startup to verify ordering.
// Helpful when WebGL stalls on the splash screen due to an invalid first scene.
public static class BuildScenesLogger
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void LogBuildScenes()
    {
        try
        {
            int count = SceneManager.sceneCountInBuildSettings;
            Debug.Log($"[BuildScenesLogger] sceneCountInBuildSettings={count}");
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = Path.GetFileNameWithoutExtension(path);
                Debug.Log($"[BuildScenesLogger] Index {i}: {name} ({path})");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[BuildScenesLogger] Failed to enumerate build scenes: {ex.Message}");
        }
    }
}