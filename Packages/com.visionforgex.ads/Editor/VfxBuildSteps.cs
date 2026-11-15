using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif
#if UNITY_IOS
using System.Collections.Generic;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif

namespace VfxAds.EditorTools
{
    /// <summary>
    /// Before an Android or iOS build: the project check (an error stops the build, with what to do), and the dependency
    /// file brought up to date with the settings.
    /// </summary>
    internal sealed class VfxPreBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;   // before the External Dependency Manager resolves

        public void OnPreprocessBuild(BuildReport report)
        {
            var t = report.summary.platform;
            if (t != BuildTarget.Android && t != BuildTarget.iOS) return;
            var s = VfxSettingsStore.Load();
            if (s != null) VfxSettingsStore.WriteDependencies(s);
            bool dev = (report.summary.options & BuildOptions.Development) != 0;
            var issues = VfxValidation.Check(VfxProjectState.Read(t == BuildTarget.Android, t == BuildTarget.iOS, dev));
            var errors = new StringBuilder();
            foreach (var i in issues)
            {
                if (i.Level == VfxIssueLevel.Error) errors.Append("\n  • ").Append(i.Message);
                else if (i.Level == VfxIssueLevel.Warning) Debug.LogWarning("[VisionForgeX] " + i.Message);
            }
            if (errors.Length > 0)
                throw new BuildFailedException("[VisionForgeX] The build was stopped, because the game would crash or earn nothing:" + errors
                                               + "\nAssets → VisionForgeX → Settings shows each with its fix.");
        }
    }

#if UNITY_ANDROID
    /// <summary>Android: Google's App ID into the manifest of the generated Gradle project (unityLibrary).</summary>
    internal sealed class VfxAndroidBuild : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var s = VfxSettingsStore.Load();
            if (s == null || string.IsNullOrWhiteSpace(s.androidAdManagerAppId)) return;
            var manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifest)) { Debug.LogError("[VisionForgeX] No AndroidManifest.xml at " + manifest + ": Google's App ID was not written."); return; }
            var patched = VfxNativeSetup.PatchManifest(File.ReadAllText(manifest), s.androidAdManagerAppId.Trim());
            if (patched == null) { Debug.LogError("[VisionForgeX] " + manifest + " has no <application>: Google's App ID was not written."); return; }
            File.WriteAllText(manifest, patched);
        }
    }
#endif

#if UNITY_IOS
    /// <summary>
    /// iOS, after Unity writes the Xcode project: the VisionForgeX pod taken from GitHub (between the External Dependency
    /// Manager writing the Podfile and running pod install), Swift turned on for the plugin's bridge, and Info.plist given
    /// Google's App ID, Google's SKAdNetwork identifiers and, when set, the tracking question's text.
    /// </summary>
    internal static class VfxIosBuild
    {
        // the External Dependency Manager writes the Podfile at 40 and installs the pods at 50
        [PostProcessBuild(45)]
        public static void Podfile(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var podfile = Path.Combine(path, "Podfile");
            if (!File.Exists(podfile))
            {
                Debug.LogWarning("[VisionForgeX] No Podfile in the Xcode project: the External Dependency Manager did not write one. Add to a Podfile, in target 'UnityFramework': " + VfxNativeSetup.PodLine(VisionForgeX.IosSdkVersion));
                return;
            }
            File.WriteAllText(podfile, VfxNativeSetup.PatchPodfile(File.ReadAllText(podfile), VisionForgeX.IosSdkVersion));
        }

        [PostProcessBuild(46)]
        public static void Project(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            var framework = proj.GetUnityFrameworkTargetGuid();
            var main = proj.GetUnityMainTargetGuid();
            // the bridge is Swift; the iOS SDK and Google's are Swift and Objective-C frameworks with modules
            if (string.IsNullOrEmpty(proj.GetBuildPropertyForAnyConfig(framework, "SWIFT_VERSION"))) proj.SetBuildProperty(framework, "SWIFT_VERSION", "5.0");
            proj.SetBuildProperty(framework, "CLANG_ENABLE_MODULES", "YES");
            proj.SetBuildProperty(main, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
            proj.SetBuildProperty(framework, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");
            proj.SetBuildProperty(main, "ENABLE_BITCODE", "NO");
            proj.SetBuildProperty(framework, "ENABLE_BITCODE", "NO");
            proj.WriteToFile(projPath);

            var s = VfxSettingsStore.Load();
            var plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            if (s != null && !string.IsNullOrWhiteSpace(s.iosAdManagerAppId)) root.SetString("GADApplicationIdentifier", s.iosAdManagerAppId.Trim());
            if (s == null || s.iosAddSkAdNetworkIds)
            {
                var items = root["SKAdNetworkItems"] as PlistElementArray ?? root.CreateArray("SKAdNetworkItems");
                var have = new List<string>();
                foreach (var e in items.values)
                    if (e is PlistElementDict d && d["SKAdNetworkIdentifier"] is PlistElementString str) have.Add(str.value);
                foreach (var id in VfxNativeSetup.MissingSkAdNetworkIds(have))
                    items.AddDict().SetString("SKAdNetworkIdentifier", id);
            }
            if (s != null && !string.IsNullOrWhiteSpace(s.iosTrackingUsageDescription))
                root.SetString("NSUserTrackingUsageDescription", s.iosTrackingUsageDescription.Trim());
            plist.WriteToFile(plistPath);
        }
    }
#endif
}
