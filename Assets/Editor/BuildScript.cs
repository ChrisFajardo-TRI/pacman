using UnityEditor;
using UnityEngine;

public static class BuildScript
{
    public static void BuildWebGL()
    {
        PlayerSettings.WebGL.template = "APPLICATION:PWA";
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = "Builds/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.LogError($"Build failed: {report.summary.result}");
            EditorApplication.Exit(1);
        }

        // PWA manifest points at the template's unity-logo icons; overwrite with ours.
        foreach (var name in new[] { "unity-logo-dark.png", "unity-logo-light.png" })
            System.IO.File.Copy("BuildAssets/pwa-icon-144.png",
                $"Builds/WebGL/TemplateData/icons/{name}", true);
    }
}
