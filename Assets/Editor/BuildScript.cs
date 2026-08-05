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

        // iOS standalone PWA: AudioContext stays suspended; inject resume-on-touch shim.
        const string indexPath = "Builds/WebGL/index.html";
        string html = System.IO.File.ReadAllText(indexPath);
        string shim = System.IO.File.ReadAllText("BuildAssets/audio-unlock.html");
        if (!html.Contains("audio-unlock"))
            System.IO.File.WriteAllText(indexPath, html.Replace("</head>", shim + "\n  </head>"));

        // Version the SW cache per build and clean old caches, else installed
        // PWAs serve stale files forever (template SW is cache-first on everything).
        const string swPath = "Builds/WebGL/ServiceWorker.js";
        string sw = System.IO.File.ReadAllText(swPath);
        string stamp = System.DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        sw = System.Text.RegularExpressions.Regex.Replace(
            sw, "const cacheName = \"[^\"]*\";", $"const cacheName = \"pacman-{stamp}\";");
        if (!sw.Contains("skipWaiting"))
            sw += System.IO.File.ReadAllText("BuildAssets/sw-update.js");
        System.IO.File.WriteAllText(swPath, sw);
    }
}
