using UnityEditor;
using UnityEngine;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

/// <summary>
/// One-click export for Daber UaaL builds.
/// Menu: Daber → Export Android / Export iOS / Export Both
///
/// Exports directly into the Expo project's unity/builds/ folder
/// so you never need to manually copy files again.
/// </summary>
public class DaberExporter
{
    // ── CONFIGURE THESE ────────────────────────────────────────────
    // Path to your Expo project root (adjust if yours is different)
    private const string ExpoProjectRoot =
        "/Users/tommyr/Desktop/Personal/expo/Daber";
    // NOTE: Unity project lives at /Users/tommyr/Desktop/Personal/Unity/DaberAvatar

    private static string AndroidExportPath =>
        Path.Combine(ExpoProjectRoot, "unity", "builds", "android");

    private static string IOSExportPath =>
        Path.Combine(ExpoProjectRoot, "unity", "builds", "ios");
    // ───────────────────────────────────────────────────────────────

    private static string[] GetEnabledScenes()
    {
        var scenes = new System.Collections.Generic.List<string>();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
                scenes.Add(scene.path);
        }
        return scenes.ToArray();
    }

    // ── ANDROID ────────────────────────────────────────────────────

    [MenuItem("Daber/Export Android %&a")] // Ctrl+Alt+A shortcut
    public static void ExportAndroid()
    {
        Debug.Log("[Daber] Starting Android export...");
        var stopwatch = Stopwatch.StartNew();

        // Ensure output directory exists
        if (Directory.Exists(AndroidExportPath))
        {
            // Clean previous export to avoid stale files
            Debug.Log("[Daber] Cleaning previous Android export...");
            Directory.Delete(AndroidExportPath, true);
        }
        Directory.CreateDirectory(AndroidExportPath);

        var options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = AndroidExportPath,
            target = BuildTarget.Android,
            options = BuildOptions.AcceptExternalModificationsToPlayer // = "Export Project"
        };

        var result = BuildPipeline.BuildPlayer(options);

        if (result.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            stopwatch.Stop();
            Debug.Log($"[Daber] Android export succeeded in {stopwatch.Elapsed.TotalSeconds:F1}s");

            // Auto-fix: remove the LAUNCHER intent-filter from Unity's manifest
            StripUnityLauncherIntent();
        }
        else
        {
            Debug.LogError($"[Daber] Android export FAILED: {result.summary.totalErrors} errors");
        }
    }

    /// <summary>
    /// Removes the MAIN/LAUNCHER intent-filter from Unity's AndroidManifest.xml
    /// so it doesn't create a second app icon.
    /// </summary>
    private static void StripUnityLauncherIntent()
    {
        var manifestPath = Path.Combine(
            AndroidExportPath, "unityLibrary", "src", "main", "AndroidManifest.xml"
        );

        if (!File.Exists(manifestPath))
        {
            Debug.LogWarning("[Daber] AndroidManifest.xml not found — skipping intent-filter strip");
            return;
        }

        var content = File.ReadAllText(manifestPath);
        var original = content;

        // Remove the <intent-filter> block containing LAUNCHER
        var pattern = @"<intent-filter>\s*<action\s+android:name=""android\.intent\.action\.MAIN""\s*/>\s*<category\s+android:name=""android\.intent\.category\.LAUNCHER""\s*/>\s*</intent-filter>";
        content = System.Text.RegularExpressions.Regex.Replace(content, pattern, "");

        if (content != original)
        {
            File.WriteAllText(manifestPath, content);
            Debug.Log("[Daber] Stripped LAUNCHER intent-filter from AndroidManifest.xml");
        }
    }

    // ── iOS ────────────────────────────────────────────────────────

    [MenuItem("Daber/Export iOS %&i")] // Ctrl+Alt+I shortcut
    public static void ExportIOS()
    {
        Debug.Log("[Daber] Starting iOS export...");
        var stopwatch = Stopwatch.StartNew();

        // For iOS, export to a temp location first, then we'll build the framework
        if (Directory.Exists(IOSExportPath))
        {
            Debug.Log("[Daber] Cleaning previous iOS export...");
            Directory.Delete(IOSExportPath, true);
        }
        Directory.CreateDirectory(IOSExportPath);

        var options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = IOSExportPath,
            target = BuildTarget.iOS,
            options = BuildOptions.None
        };

        var result = BuildPipeline.BuildPlayer(options);

        if (result.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            stopwatch.Stop();
            Debug.Log($"[Daber] iOS Xcode project export succeeded in {stopwatch.Elapsed.TotalSeconds:F1}s");
            Debug.Log("[Daber] Now run: ./scripts/build-unity-ios.sh to build the framework");
            Debug.Log("[Daber] Or use: Daber → Build iOS Framework (runs xcodebuild)");
        }
        else
        {
            Debug.LogError($"[Daber] iOS export FAILED: {result.summary.totalErrors} errors");
        }
    }

    [MenuItem("Daber/Build iOS Framework %&f")] // Ctrl+Alt+F shortcut
    public static void BuildIOSFramework()
    {
        Debug.Log("[Daber] Building UnityFramework.framework via xcodebuild...");

        var xcodeProjectPath = Path.Combine(IOSExportPath, "Unity-iPhone.xcodeproj");
        if (!Directory.Exists(xcodeProjectPath))
        {
            Debug.LogError("[Daber] No Xcode project found. Run 'Export iOS' first.");
            return;
        }

        // Run xcodebuild from a shell script for reliability
        var scriptPath = Path.Combine(ExpoProjectRoot, "scripts", "build-unity-ios.sh");
        if (!File.Exists(scriptPath))
        {
            Debug.LogError($"[Daber] Build script not found at {scriptPath}");
            Debug.Log("[Daber] Create it first — see the script template in your Expo project.");
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = scriptPath,
            WorkingDirectory = ExpoProjectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var proc = Process.Start(psi);
        proc.WaitForExit(600000); // 10 min timeout

        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();

        if (proc.ExitCode == 0)
        {
            Debug.Log($"[Daber] Framework build succeeded!\n{stdout}");
        }
        else
        {
            Debug.LogError($"[Daber] Framework build failed (exit {proc.ExitCode}):\n{stderr}");
        }
    }

    // ── BOTH ───────────────────────────────────────────────────────

    [MenuItem("Daber/Export Both Platforms %&b")] // Ctrl+Alt+B shortcut
    public static void ExportBoth()
    {
        ExportAndroid();
        ExportIOS();
    }
}
