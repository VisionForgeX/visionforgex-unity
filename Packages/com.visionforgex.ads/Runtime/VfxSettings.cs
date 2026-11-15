using System.Collections.Generic;
using UnityEngine;

namespace VfxAds
{
    /// <summary>Which of Google's Android SDKs the game runs on (chosen in the settings; the game's code is the same).</summary>
    public enum VfxAndroidEngine
    {
        /// <summary>Google's GMA Next-Gen SDK (Google's preferred Android SDK): vfx-ads-nextgen. Needs compileSdk 35.</summary>
        NextGen = 0,
        /// <summary>Google Mobile Ads 25.x (retired by Google on 30 Jun 2028): vfx-ads.</summary>
        GoogleMobileAds = 1,
    }

    /// <summary>
    /// The game's VisionForgeX settings: Assets → VisionForgeX → Settings. Kept in
    /// Assets/VisionForgeX/Resources/VisionForgeXSettings.asset and read at run time from Resources. Holds only what
    /// identifies the game and how it is built; every decision about ads is the platform's.
    /// </summary>
    public sealed class VfxSettings : ScriptableObject
    {
        public const string ResourceName = "VisionForgeXSettings";

        [Tooltip("The Android app's key, from its page on apps.visionforgex.com (VisionForgeX SDK panel). Starts with vfx_.")]
        public string androidAppKey = "";

        [Tooltip("The iOS app's key, from its page on apps.visionforgex.com. The iOS build is its own app there, with its own key.")]
        public string iosAppKey = "";

        [Tooltip("Google's App ID for the Android app (ca-app-pub-…~…), shown on its page on VisionForgeX. Written into the Android manifest.")]
        public string androidAdManagerAppId = "";

        [Tooltip("Google's App ID for the iOS app (ca-app-pub-…~…), shown on its page on VisionForgeX. Written into Info.plist.")]
        public string iosAdManagerAppId = "";

        [Tooltip("Which of Google's Android SDKs the game runs on. Next-Gen is Google's preferred one.")]
        public VfxAndroidEngine androidEngine = VfxAndroidEngine.NextGen;

        [Tooltip("Development builds and the Editor show Google's demo test ads instead of the platform's. Release builds never do.")]
        public bool testMode;

        [Tooltip("Start the SDK when the game starts, with no code (otherwise call VisionForgeX.Initialize()).")]
        public bool initializeOnStart;

        [Tooltip("Pause the game's audio (AudioListener.pause) while a full-screen ad is on screen.")]
        public bool pauseAudioDuringAds = true;

        [Tooltip("How much the native SDK writes to the device log. 'As the platform says' is right for release builds.")]
        public VfxSettingsLogLevel logLevel = VfxSettingsLogLevel.AsThePlatformSays;

        [Tooltip("Google test devices (their ads are test ads); the device log prints a device's ID on its first request.")]
        public List<string> testDeviceIds = new List<string>();

        [Tooltip("iOS: why the game asks to track (App Tracking Transparency). Empty: the game never asks, and Info.plist gets no text.")]
        public string iosTrackingUsageDescription = "";

        [Tooltip("iOS: Google's SKAdNetwork identifiers are written into Info.plist (recommended: buyers need them).")]
        public bool iosAddSkAdNetworkIds = true;

        static VfxSettings _cached;

        /// <summary>Tests only: these settings instead of the asset.</summary>
        internal static VfxSettings OverrideForTests;

        /// <summary>The game's settings (null when the asset was never made: open Assets → VisionForgeX → Settings).</summary>
        public static VfxSettings Current
        {
            get
            {
                if (OverrideForTests != null) return OverrideForTests;
                if (_cached == null) _cached = Resources.Load<VfxSettings>(ResourceName);
                return _cached;
            }
        }

        /// <summary>The key for the platform this build is for.</summary>
        public string AppKeyForThisPlatform() => ((VfxPlatform.IsIos ? iosAppKey : androidAppKey) ?? "").Trim();

        /// <summary>The options these settings mean for a build (test mode only in development builds).</summary>
        public VfxOptions ToOptions(bool developmentBuild)
        {
            var o = new VfxOptions
            {
                GoogleDemoAds = testMode && developmentBuild,
                TestDeviceIds = new List<string>(testDeviceIds ?? new List<string>()),
            };
            if (logLevel != VfxSettingsLogLevel.AsThePlatformSays) o.LogLevel = (VfxLogLevel)((int)logLevel - 1);
            return o;
        }
    }

    /// <summary>The settings' log level choice (the first: leave it to the platform).</summary>
    public enum VfxSettingsLogLevel { AsThePlatformSays = 0, None = 1, Error = 2, Warn = 3, Info = 4, Debug = 5 }

    /// <summary>What this build is.</summary>
    internal static class VfxPlatform
    {
        /// <summary>A development build, or the Editor: test mode may apply.</summary>
        public static bool IsDevelopmentBuild => Application.isEditor || UnityEngine.Debug.isDebugBuild;

        /// <summary>An iOS build (or the Editor set to iOS).</summary>
        public static bool IsIos
        {
            get
            {
#if UNITY_IOS
                return true;
#else
                return false;
#endif
            }
        }
    }
}
