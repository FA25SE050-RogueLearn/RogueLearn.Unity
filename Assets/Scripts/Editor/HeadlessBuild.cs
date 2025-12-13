using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class HeadlessBuild
{
    [MenuItem("Build/Headless/Linux Server (Relay)")]
    public static void BuildLinuxServerMenu()
    {
        BuildLinuxServer();
    }

    // CI-friendly method name to call from Unity CLI: -executeMethod HeadlessBuild.BuildLinuxServer
    public static void BuildLinuxServer()
    {
        // Ensure the Linux build target is supported by this Editor (module installed via Unity Hub)
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
        {
            throw new System.Exception(
                "StandaloneLinux64 build target is unsupported in this Editor. " +
                "Install 'Linux Build Support (IL2CPP)' for your Unity 2022.3 Editor via Unity Hub -> Installs -> Add modules.");
        }

        // Switch active build target to Linux
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneLinux64)
        {
            bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
            if (!switched)
            {
                throw new System.Exception("Failed to switch active build target to StandaloneLinux64.");
            }
        }

        // Enable Dedicated Server subtarget to define UNITY_SERVER and strip client-only content
#if UNITY_2021_3_OR_NEWER
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;
        //EditorUserBuildSettings.serverBuild = true;
#endif

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            throw new System.Exception("No scenes enabled in EditorBuildSettings.");
        }

        // Temporarily disable SRP (e.g., URP) for the server build to avoid shader compilation noise and
        // unnecessary graphics initialization in a headless environment. We restore the original setting afterward.
        var originalPipeline = GraphicsSettings.renderPipelineAsset;
        GraphicsSettings.renderPipelineAsset = null;

        try
        {
            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "build/StandaloneLinux64/BossFight2D.x86_64",
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.EnableHeadlessMode | BuildOptions.CompressWithLz4HC
            };

            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                var reason = report.summary.result.ToString();
                throw new System.Exception($"Linux headless build failed: {reason}. Check if Linux Build Support is installed and the active build target is StandaloneLinux64.");
            }
            else
            {
                UnityEngine.Debug.Log("Linux headless build completed: Build/LinuxServer/");
            }
        }
        finally
        {
            GraphicsSettings.renderPipelineAsset = originalPipeline;
        }
    }
}
