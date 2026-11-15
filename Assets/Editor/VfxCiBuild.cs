using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using VfxAds;
using VfxAds.EditorTools;

/// <summary>
/// How GitHub's CI builds the demo (this repository's own project; not part of the package). GameCI calls
/// VfxCiBuild.Android / .Ios with -customBuildPath; -vfxEngine nextgen|gma picks Google's Android SDK.
/// Test mode and Google's sample App IDs: the build proves the plugin compiles and links with the released SDKs.
/// </summary>
public static class VfxCiBuild
{
    const string Scene = "Assets/Samples/VisionForgeX/Demo/Demo.unity";

    static string Arg(string name, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    static void Prepare(BuildTarget target)
    {
        var s = VfxSettingsStore.LoadOrCreate();
        s.androidAdManagerAppId = VfxNativeSetup.GoogleSampleAndroidAppId;
        s.iosAdManagerAppId = VfxNativeSetup.GoogleSampleIosAppId;
        s.testMode = true;
        s.androidEngine = Arg("-vfxEngine", "nextgen") == "gma" ? VfxAndroidEngine.GoogleMobileAds : VfxAndroidEngine.NextGen;
        VfxSettingsStore.Save(s);
        Debug.Log("[VisionForgeX CI] Android engine: " + VfxNativeSetup.AndroidArtifact(s.androidEngine));

        PlayerSettings.companyName = "VisionForgeX";
        PlayerSettings.productName = "VisionForgeX Demo";
        PlayerSettings.SetApplicationIdentifier(target == BuildTarget.iOS ? NamedBuildTarget.iOS : NamedBuildTarget.Android, "com.visionforgex.unitydemo");
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)35;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.iOS.targetOSVersionString = "13.0";

        if (target == BuildTarget.Android) GradleTemplates();
        if (!File.Exists(Scene)) throw new Exception("The demo scene is not at " + Scene + ": the workflow copies Samples~/Demo there first.");

        var issues = VfxValidation.Check(VfxProjectState.Read(target == BuildTarget.Android, target == BuildTarget.iOS, true));
        foreach (var i in issues) Debug.Log("[VisionForgeX CI] project check: " + i);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (target == BuildTarget.Android) ResolveAndroid();
    }

    /// <summary>The Gradle templates the External Dependency Manager writes the dependencies into, from this Unity's own.</summary>
    static void GradleTemplates()
    {
        var from = Path.Combine(BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None), "Tools", "GradleTemplates");
        const string to = "Assets/Plugins/Android";
        Directory.CreateDirectory(to);
        foreach (var name in new[] { "mainTemplate.gradle", "settingsTemplate.gradle", "gradleTemplate.properties" })
        {
            var src = Path.Combine(from, name);
            var dst = Path.Combine(to, name);
            if (File.Exists(src) && !File.Exists(dst)) { File.Copy(src, dst); Debug.Log("[VisionForgeX CI] Gradle template: " + name); }
        }
        AssetDatabase.Refresh();
    }

    /// <summary>Runs the External Dependency Manager's Android resolution now (whichever of its two packages is present).</summary>
    static void ResolveAndroid()
    {
        var t = AppDomain.CurrentDomain.GetAssemblies().Select(a => { try { return a.GetType("GooglePlayServices.PlayServicesResolver", false); } catch { return null; } }).FirstOrDefault(x => x != null);
        var m = t?.GetMethod("ResolveSync", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool) }, null);
        if (m == null) { Debug.LogWarning("[VisionForgeX CI] The External Dependency Manager's resolver was not found: the build relies on its resolution at build time."); return; }
        var ok = m.Invoke(null, new object[] { true });
        Debug.Log("[VisionForgeX CI] Android resolution: " + ok);
        var template = "Assets/Plugins/Android/mainTemplate.gradle";
        if (File.Exists(template)) Debug.Log("[VisionForgeX CI] mainTemplate.gradle dependencies:\n" + string.Join("\n", File.ReadAllLines(template).Where(l => l.Contains("implementation") || l.Contains("jitpack"))));
    }

    static void Build(BuildTarget target, string defaultPath)
    {
        try
        {
            EditorUserBuildSettings.development = true;
            Prepare(target);
            var path = Arg("-customBuildPath", defaultPath);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = path,
                target = target,
                options = BuildOptions.Development,
            });
            Debug.Log("[VisionForgeX CI] " + target + " build: " + report.summary.result + ", " + report.summary.totalErrors + " errors, at " + path);
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
        catch (Exception e)
        {
            Debug.LogError("[VisionForgeX CI] " + e);
            EditorApplication.Exit(1);
        }
    }

    public static void Android() => Build(BuildTarget.Android, "build/Android/VisionForgeXDemo.apk");

    public static void Ios() => Build(BuildTarget.iOS, "build/iOS/VisionForgeXDemo");
}
