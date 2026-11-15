using System;
using System.Collections.Generic;

namespace VfxAds.EditorTools
{
    /// <summary>How serious a finding is. An error stops the build: the game would crash or earn nothing.</summary>
    internal enum VfxIssueLevel { Info, Warning, Error }

    /// <summary>What the settings window's "Fix" button does for a finding.</summary>
    internal enum VfxFix { None, CreateSettings, OpenSettings, SetAndroidMinSdk24, SetAndroidTargetSdk35, SetIosMinVersion13, InstallEdm, UseNextGen }

    /// <summary>One finding of the project check.</summary>
    internal sealed class VfxIssue
    {
        public VfxIssue(VfxIssueLevel level, string code, string message, VfxFix fix = VfxFix.None)
        {
            Level = level; Code = code; Message = message; Fix = fix;
        }

        public VfxIssueLevel Level { get; }
        /// <summary>A short name for tests and logs (e.g. "android-app-id").</summary>
        public string Code { get; }
        public string Message { get; }
        public VfxFix Fix { get; }
        public override string ToString() => Level + " " + Code + ": " + Message;
    }

    /// <summary>What the check looks at: read from the project by <see cref="VfxProjectState"/>, made by hand in tests.</summary>
    internal sealed class VfxProjectFacts
    {
        public bool SettingsExist = true;
        public bool ForAndroid;
        public bool ForIos;
        public bool DevelopmentBuild;
        public string AndroidKey = "", IosKey = "", AndroidAppId = "", IosAppId = "";
        public VfxAndroidEngine Engine = VfxAndroidEngine.NextGen;
        public bool TestMode;
        /// <summary>PlayerSettings.Android.minSdkVersion as a number.</summary>
        public int AndroidMinSdk = 24;
        /// <summary>PlayerSettings.Android.targetSdkVersion as a number; 0 = "Automatic (highest installed)".</summary>
        public int AndroidTargetSdk;
        /// <summary>PlayerSettings.iOS.targetOSVersionString.</summary>
        public string IosMinVersion = "13.0";
        /// <summary>The External Dependency Manager is in the project (Unity's or Google's).</summary>
        public bool EdmPresent = true;
        /// <summary>Google's own Unity plugin (GoogleMobileAds) is in the project: a second copy of Google's SDK.</summary>
        public bool GoogleUnityPluginPresent;
        /// <summary>Other files in the project that bring a Google ads SDK on Android (file names or dependency specs).</summary>
        public List<string> OtherGoogleAndroidAds = new List<string>();
    }

    /// <summary>
    /// The project check: the settings window shows it, and every Android or iOS build runs it first (errors stop the
    /// build with the reason and what to do). It checks how the game is built, never anything the platform decides.
    /// </summary>
    internal static class VfxValidation
    {
        public static List<VfxIssue> Check(VfxProjectFacts f)
        {
            var list = new List<VfxIssue>();
            if (!f.SettingsExist)
            {
                list.Add(new VfxIssue(VfxIssueLevel.Error, "settings", "VisionForgeX has no settings in this project yet. Open Assets → VisionForgeX → Settings to make them.", VfxFix.CreateSettings));
                return list;
            }
            if (!f.EdmPresent)
                list.Add(new VfxIssue(VfxIssueLevel.Error, "edm", "The External Dependency Manager is missing: without it the VisionForgeX and Google SDKs are not added to the build. Install it (Unity Registry: External Dependency Manager).", VfxFix.InstallEdm));
            if (f.GoogleUnityPluginPresent)
                list.Add(new VfxIssue(VfxIssueLevel.Error, "google-plugin", "Google's Mobile Ads Unity plugin (GoogleMobileAds) is in the project. VisionForgeX brings Google's SDK itself: two copies cannot be in one app. Remove the GoogleMobileAds folder (and its Plugins) first."));

            if (f.ForAndroid)
            {
                if (string.IsNullOrWhiteSpace(f.AndroidAppId))
                    list.Add(new VfxIssue(VfxIssueLevel.Error, "android-app-id", "No Google App ID for Android. Google's SDK stops the app at start without it. Paste it from the app's page on VisionForgeX (ca-app-pub-…~…).", VfxFix.OpenSettings));
                else if (!VfxNativeSetup.LooksLikeAppId(f.AndroidAppId))
                    list.Add(new VfxIssue(VfxIssueLevel.Error, "android-app-id", "The Android Google App ID \"" + f.AndroidAppId.Trim() + "\" is not one: it looks like ca-app-pub-################~########## (with ~, not /).", VfxFix.OpenSettings));
                else if (VfxNativeSetup.IsGoogleSample(f.AndroidAppId) && !f.DevelopmentBuild)
                    list.Add(new VfxIssue(VfxIssueLevel.Warning, "android-sample-app-id", "The Android App ID is Google's sample one: fine for testing, but a release needs the app's own (from its page on VisionForgeX).", VfxFix.OpenSettings));
                Key(list, "android", f.AndroidKey, f.TestMode && f.DevelopmentBuild);
                if (f.AndroidMinSdk < 24)
                    list.Add(new VfxIssue(VfxIssueLevel.Error, "android-min-sdk", "Minimum API level is " + f.AndroidMinSdk + "; the VisionForgeX and Google SDKs need 24 (Android 7.0) or higher.", VfxFix.SetAndroidMinSdk24));
                if (f.Engine == VfxAndroidEngine.NextGen)
                {
                    if (f.AndroidTargetSdk > 0 && f.AndroidTargetSdk < 35)
                        list.Add(new VfxIssue(VfxIssueLevel.Error, "android-target-sdk", "Target API level is " + f.AndroidTargetSdk + "; Google's Next-Gen SDK needs the game compiled against 35 or higher.", VfxFix.SetAndroidTargetSdk35));
                    else if (f.AndroidTargetSdk == 0)
                        list.Add(new VfxIssue(VfxIssueLevel.Info, "android-target-sdk", "Target API level is \"Automatic\" (the highest installed): Google's Next-Gen SDK needs 35 or higher installed.", VfxFix.SetAndroidTargetSdk35));
                }
                foreach (var other in f.OtherGoogleAndroidAds)
                    list.Add(new VfxIssue(VfxIssueLevel.Error, "android-two-google-sdks", "\"" + other + "\" brings another Google ads SDK into the Android build. Google's Next-Gen SDK and Google Mobile Ads cannot sit in one app, nor two copies of one: remove it (VisionForgeX brings Google's SDK)."));
            }

            if (f.ForIos)
            {
                if (string.IsNullOrWhiteSpace(f.IosAppId))
                    list.Add(new VfxIssue(VfxIssueLevel.Error, "ios-app-id", "No Google App ID for iOS. Google's SDK stops the app at start without it. Paste it from the iOS app's page on VisionForgeX (ca-app-pub-…~…).", VfxFix.OpenSettings));
                else if (!VfxNativeSetup.LooksLikeAppId(f.IosAppId))
                    list.Add(new VfxIssue(VfxIssueLevel.Error, "ios-app-id", "The iOS Google App ID \"" + f.IosAppId.Trim() + "\" is not one: it looks like ca-app-pub-################~##########.", VfxFix.OpenSettings));
                else if (VfxNativeSetup.IsGoogleSample(f.IosAppId) && !f.DevelopmentBuild)
                    list.Add(new VfxIssue(VfxIssueLevel.Warning, "ios-sample-app-id", "The iOS App ID is Google's sample one: fine for testing, but a release needs the app's own.", VfxFix.OpenSettings));
                Key(list, "ios", f.IosKey, f.TestMode && f.DevelopmentBuild);
                if (VersionBelow(f.IosMinVersion, 13, 0))
                    list.Add(new VfxIssue(VfxIssueLevel.Error, "ios-min-version", "Target minimum iOS version is " + f.IosMinVersion + "; the VisionForgeX and Google SDKs need iOS 13.0 or higher.", VfxFix.SetIosMinVersion13));
            }

            if (f.TestMode)
                list.Add(f.DevelopmentBuild
                    ? new VfxIssue(VfxIssueLevel.Info, "test-mode", "Test mode is on: development builds and the Editor show Google's demo ads. Release builds never do.")
                    : new VfxIssue(VfxIssueLevel.Info, "test-mode", "Test mode is on, and this is a release build: it is ignored here, the platform's settings are used."));
            return list;
        }

        static void Key(List<VfxIssue> list, string platform, string key, bool demoAds)
        {
            var name = platform == "ios" ? "iOS" : "Android";
            if (string.IsNullOrWhiteSpace(key))
            {
                if (!demoAds)
                    list.Add(new VfxIssue(VfxIssueLevel.Warning, platform + "-key", "No " + name + " app key: the SDK will not be ready and no ad will show. Paste the key from the app's page on VisionForgeX (VisionForgeX SDK panel).", VfxFix.OpenSettings));
            }
            else if (!key.Trim().StartsWith("vfx_", StringComparison.Ordinal))
                list.Add(new VfxIssue(VfxIssueLevel.Warning, platform + "-key", "The " + name + " app key does not start with vfx_: check it was copied whole from the app's page on VisionForgeX.", VfxFix.OpenSettings));
        }

        /// <summary>"12.4" is below 13.0; an empty or unreadable version is not.</summary>
        public static bool VersionBelow(string v, int major, int minor)
        {
            if (string.IsNullOrWhiteSpace(v)) return false;
            var parts = v.Trim().Split('.');
            if (!int.TryParse(parts[0], out var ma)) return false;
            int mi = parts.Length > 1 && int.TryParse(parts[1], out var m) ? m : 0;
            return ma < major || (ma == major && mi < minor);
        }

        public static bool HasErrors(IEnumerable<VfxIssue> issues)
        {
            foreach (var i in issues) if (i.Level == VfxIssueLevel.Error) return true;
            return false;
        }
    }
}
