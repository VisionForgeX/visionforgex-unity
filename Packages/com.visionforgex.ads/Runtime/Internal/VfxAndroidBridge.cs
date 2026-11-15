#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;

namespace VfxAds
{
    /// <summary>
    /// Android: calls into com.visionforgex.unity.VfxUnityBridge (Plugins/Android, compiled by the game's Gradle build
    /// against the VisionForgeX Android SDK that the External Dependency Manager adds). Answers come back through an
    /// AndroidJavaProxy on Android's main thread and are handed to <see cref="VisionForgeX.Receive"/>.
    /// </summary>
    internal sealed class VfxAndroidBridge : IVfxBridge
    {
        const string BridgeClass = "com.visionforgex.unity.VfxUnityBridge";

        [UnityEngine.Scripting.Preserve]
        sealed class Listener : AndroidJavaProxy
        {
            public Listener() : base("com.visionforgex.unity.VfxUnityListener") { }

            // called by Java through reflection, on Android's main thread: kept from IL2CPP's code stripping
            [UnityEngine.Scripting.Preserve]
            public void onMessage(string json) => VisionForgeX.Receive(json);
        }

        static Listener _listener;   // kept: Java holds it for the life of the game
        readonly AndroidJavaClass _c;

        public VfxAndroidBridge()
        {
            try { _c = new AndroidJavaClass(BridgeClass); }
            catch (System.Exception e)
            {
                VfxLog.Error("The Android bridge is missing from this build (" + e.Message + "). Was the VisionForgeX Android SDK resolved? Assets → External Dependency Manager → Android Resolver → Force Resolve.");
            }
        }

        static AndroidJavaObject Activity
        {
            get
            {
                using (var p = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    return p.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }

        public string Name => "android";

        void Call(string method, params object[] args)
        {
            if (_c == null) return;
            try { _c.CallStatic(method, args); }
            catch (System.Exception e) { VfxLog.Error("Android bridge " + method + ": " + e.Message); }
        }

        T Get<T>(string method, T fallback, params object[] args)
        {
            if (_c == null) return fallback;
            try { return _c.CallStatic<T>(method, args); }
            catch (System.Exception e) { VfxLog.Error("Android bridge " + method + ": " + e.Message); return fallback; }
        }

        public void Initialize(string appKey, string optionsJson)
        {
            if (_c == null)
            {
                VisionForgeX.Receive("{\"k\":\"init\",\"ready\":false,\"reason\":\"the VisionForgeX Android SDK is not in this build\",\"fromCache\":false}");
                return;
            }
            _listener = new Listener();
            Call("init", Activity, appKey ?? "", optionsJson ?? "{}", _listener);
        }

        public bool IsReady() => Get("isReady", false);
        public bool FullScreenReady(string placement, string format) => Get("fullScreenReady", false, placement, format);
        public void Load(string placement, string format) => Call("load", placement, format);

        public void Show(int id, string placement, string format)
        {
            if (_c == null) { VisionForgeX.Receive("{\"k\":\"show\",\"id\":" + id + ",\"e\":\"notShown\",\"r\":\"not_ready\"}"); return; }
            Call("show", Activity, id, placement, format);
        }

        public void PreloadAll() => Call("preloadAll");
        public void SetKeyValue(string key, string value) => Call("setKeyValue", key, value ?? "", value == null);   // JNI takes no null string
        public bool IsPrivacyOptionsRequired() => Get("isPrivacyOptionsRequired", false);
        public void ShowPrivacyOptions(int id) => Call("showPrivacyOptions", Activity, id);
        public void SetMuted(bool muted) => Call("setMuted", muted);
        public void OpenAdInspector() => Call("openAdInspector", Activity);
        public bool OpenDebugPanel() => Get("openDebugPanel", false, Activity);
        public string SettingsSource() => Get("settingsSource", "none");
        public void Flush() => Call("flush");
        public string NativeVersion() => Get("nativeVersion", "");
        public void BannerCreate(int id, string placement, int position) => Call("bannerCreate", Activity, id, placement, position);
        public void BannerSetVisible(int id, bool visible) => Call("bannerSetVisible", id, visible);
        public void BannerDestroy(int id) => Call("bannerDestroy", id);
    }
}
#endif
