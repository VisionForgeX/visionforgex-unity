namespace VfxAds
{
    /// <summary>
    /// What the C# layer asks of a platform: Android (the VisionForgeX Android SDK), iOS (the iOS SDK), the Editor's
    /// stand-in, or nothing (other platforms). Answers come back as JSON messages through
    /// <see cref="VisionForgeX.Receive"/>, from any thread; they are delivered to the game on Unity's main thread.
    ///
    /// Messages (keys are a contract with VfxUnityBridge.java and VfxUnityBridge.swift; change all three together):
    ///   {"k":"init","ready":bool,"reason":str,"fromCache":bool}
    ///   {"k":"show","id":int,"e":"shown"|"clicked"|"reward"|"dismissed"|"notShown","r":reason,"t":type,"a":amount}
    ///   {"k":"event","type":str,"placement":str,"format":str,"time":long,"details":{...}}
    ///   {"k":"revenue","placement":str,"format":str,"valueMicros":long,"currency":str,"precision":str,"source":str|null}
    ///   {"k":"consent","id":int,"error":str|null}
    ///   {"k":"banner","id":int,"h":int}   (the banner's height in screen pixels, 0 when hidden or empty)
    /// </summary>
    internal interface IVfxBridge
    {
        /// <summary>"android", "ios", "editor" or "none".</summary>
        string Name { get; }

        void Initialize(string appKey, string optionsJson);
        bool IsReady();
        bool FullScreenReady(string placement, string format);
        void Load(string placement, string format);
        void Show(int id, string placement, string format);
        void PreloadAll();
        void SetKeyValue(string key, string value);
        bool IsPrivacyOptionsRequired();
        void ShowPrivacyOptions(int id);
        void SetMuted(bool muted);
        void OpenAdInspector();

        /// <summary>Opens the native SDK's own debug panel; false when the platform has none (the C# one opens instead).</summary>
        bool OpenDebugPanel();

        string SettingsSource();
        void Flush();

        /// <summary>The native SDK's version ("" when not known).</summary>
        string NativeVersion();

        void BannerCreate(int id, string placement, int position);
        void BannerSetVisible(int id, bool visible);
        void BannerDestroy(int id);
    }

    /// <summary>Platforms with no VisionForgeX SDK (desktop, WebGL builds): every call is answered, nothing is shown.</summary>
    internal sealed class VfxNoneBridge : IVfxBridge
    {
        public string Name => "none";
        public void Initialize(string appKey, string optionsJson) =>
            VisionForgeX.Receive("{\"k\":\"init\",\"ready\":false,\"reason\":\"VisionForgeX runs on Android and iOS only\",\"fromCache\":false}");
        public bool IsReady() => false;
        public bool FullScreenReady(string placement, string format) => false;
        public void Load(string placement, string format) { }
        public void Show(int id, string placement, string format) =>
            VisionForgeX.Receive("{\"k\":\"show\",\"id\":" + id + ",\"e\":\"notShown\",\"r\":\"not_ready\"}");
        public void PreloadAll() { }
        public void SetKeyValue(string key, string value) { }
        public bool IsPrivacyOptionsRequired() => false;
        public void ShowPrivacyOptions(int id) => VisionForgeX.Receive("{\"k\":\"consent\",\"id\":" + id + ",\"error\":\"no consent form on this platform\"}");
        public void SetMuted(bool muted) { }
        public void OpenAdInspector() { }
        public bool OpenDebugPanel() => false;
        public string SettingsSource() => "none";
        public void Flush() { }
        public string NativeVersion() => "";
        public void BannerCreate(int id, string placement, int position) { }
        public void BannerSetVisible(int id, bool visible) { }
        public void BannerDestroy(int id) { }
    }
}
