using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace VfxAds.EditorTools
{
    /// <summary>Reads the facts the project check needs from the project, and applies the check's fixes.</summary>
    internal static class VfxProjectState
    {
        public const string EdmPackage = "com.unity.external-dependency-manager";
        public const string EdmVersion = "2.1.1";

        static readonly Regex GmaSpec = new Regex(@"com\.google\.android\.gms:play-services-ads(-lite)?:");
        static readonly Regex NextGenSpec = new Regex(@"com\.google\.android\.libraries\.ads\.mobile\.sdk:ads-mobile-sdk:");
        static readonly Regex GmaFile = new Regex(@"^play-services-ads(-lite)?-\d[\w.\-]*\.(aar|jar|srcaar)$", RegexOptions.IgnoreCase);
        static readonly Regex NextGenFile = new Regex(@"^ads-mobile-sdk-\d[\w.\-]*\.(aar|jar|srcaar)$", RegexOptions.IgnoreCase);

        public static VfxProjectFacts Read(bool android, bool ios, bool developmentBuild)
        {
            var s = VfxSettingsStore.Load();
            var f = new VfxProjectFacts { SettingsExist = s != null, ForAndroid = android, ForIos = ios, DevelopmentBuild = developmentBuild };
            if (s == null) return f;
            f.AndroidKey = s.androidAppKey ?? "";
            f.IosKey = s.iosAppKey ?? "";
            f.AndroidAppId = s.androidAdManagerAppId ?? "";
            f.IosAppId = s.iosAdManagerAppId ?? "";
            f.Engine = s.androidEngine;
            f.TestMode = s.testMode;
            f.AndroidMinSdk = (int)PlayerSettings.Android.minSdkVersion;
            f.AndroidTargetSdk = (int)PlayerSettings.Android.targetSdkVersion;
            f.IosMinVersion = PlayerSettings.iOS.targetOSVersionString;
            f.EdmPresent = EdmPresent();
            f.GoogleUnityPluginPresent = TypeExists("GoogleMobileAds.Api.MobileAds");
            if (android) f.OtherGoogleAndroidAds = OtherGoogleAndroidAds(s.androidEngine);
            return f;
        }

        /// <summary>For the active build target (both platforms when it is neither).</summary>
        public static VfxProjectFacts ReadForActiveTarget()
        {
            var t = EditorUserBuildSettings.activeBuildTarget;
            bool a = t == BuildTarget.Android, i = t == BuildTarget.iOS;
            if (!a && !i) a = i = true;
            return Read(a, i, EditorUserBuildSettings.development);
        }

        public static bool EdmPresent() =>
            TypeExists("GooglePlayServices.PlayServicesResolver") || TypeExists("Google.IOSResolver")
            || UnityEditor.PackageManager.PackageInfo.FindForPackageName(EdmPackage) != null;

        public static bool TypeExists(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { if (asm.GetType(fullName, false) != null) return true; }
                catch (Exception) { /* an assembly that cannot be read: not this one */ }
            }
            return false;
        }

        /// <summary>
        /// Files that bring the other Google SDK (or a second copy) into the Android build: another package's dependency
        /// file, an AAR in Assets, or a line in the Gradle template. Only the engine VisionForgeX does not use is looked
        /// for, so the resolver's own copy of ours is never reported.
        /// </summary>
        public static List<string> OtherGoogleAndroidAds(VfxAndroidEngine engine)
        {
            var found = new List<string>();
            var spec = engine == VfxAndroidEngine.NextGen ? GmaSpec : NextGenSpec;
            var file = engine == VfxAndroidEngine.NextGen ? GmaFile : NextGenFile;
            try
            {
                foreach (var path in Directory.GetFiles("Assets", "*Dependencies.xml", SearchOption.AllDirectories))
                {
                    var p = path.Replace('\\', '/');
                    if (p == VfxSettingsStore.DependenciesPath) continue;
                    if (spec.IsMatch(File.ReadAllText(p))) found.Add(p);
                }
                foreach (var path in Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories))
                    if (file.IsMatch(Path.GetFileName(path))) found.Add(path.Replace('\\', '/'));
                const string template = "Assets/Plugins/Android/mainTemplate.gradle";
                if (File.Exists(template) && spec.IsMatch(File.ReadAllText(template))) found.Add(template);
            }
            catch (Exception e) { Debug.LogWarning("[VisionForgeX] The project check could not read every file: " + e.Message); }
            return found.Distinct().ToList();
        }

        public static void Apply(VfxFix fix)
        {
            switch (fix)
            {
                case VfxFix.CreateSettings: VfxSettingsStore.LoadOrCreate(); break;
                case VfxFix.OpenSettings: VfxSettingsWindow.Open(); break;
                case VfxFix.SetAndroidMinSdk24: PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24; break;
                case VfxFix.SetAndroidTargetSdk35: PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)35; break;
                case VfxFix.SetIosMinVersion13: PlayerSettings.iOS.targetOSVersionString = "13.0"; break;
                case VfxFix.InstallEdm: UnityEditor.PackageManager.Client.Add(EdmPackage + "@" + EdmVersion); break;
                case VfxFix.UseNextGen:
                    var s = VfxSettingsStore.LoadOrCreate();
                    s.androidEngine = VfxAndroidEngine.NextGen;
                    VfxSettingsStore.Save(s);
                    break;
            }
        }

        public static string FixLabel(VfxFix fix)
        {
            switch (fix)
            {
                case VfxFix.CreateSettings: return "Make them";
                case VfxFix.OpenSettings: return "Open settings";
                case VfxFix.SetAndroidMinSdk24: return "Set API 24";
                case VfxFix.SetAndroidTargetSdk35: return "Set API 35";
                case VfxFix.SetIosMinVersion13: return "Set iOS 13.0";
                case VfxFix.InstallEdm: return "Install it";
                case VfxFix.UseNextGen: return "Use Next-Gen";
                default: return "";
            }
        }
    }
}
