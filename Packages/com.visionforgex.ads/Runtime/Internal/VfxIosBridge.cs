#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using AOT;

namespace VfxAds
{
    /// <summary>
    /// iOS: calls the C functions of Plugins/iOS/VfxUnityBridge.mm, which pass them to VfxUnityBridge.swift and the
    /// VisionForgeX iOS SDK (added to the Xcode project by the External Dependency Manager and this plugin's build step).
    /// Answers come back through one C callback, on iOS's main thread, and are handed to <see cref="VisionForgeX.Receive"/>.
    /// </summary>
    internal sealed class VfxIosBridge : IVfxBridge
    {
        delegate void Callback(string json);

        [DllImport("__Internal")] static extern void VfxUnity_SetCallback(Callback cb);
        [DllImport("__Internal")] static extern void VfxUnity_Initialize(string appKey, string optionsJson);
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.I1)] static extern bool VfxUnity_IsReady();
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.I1)] static extern bool VfxUnity_FullScreenReady(string placement, string format);
        [DllImport("__Internal")] static extern void VfxUnity_Load(string placement, string format);
        [DllImport("__Internal")] static extern void VfxUnity_Show(int id, string placement, string format);
        [DllImport("__Internal")] static extern void VfxUnity_PreloadAll();
        [DllImport("__Internal")] static extern void VfxUnity_SetKeyValue(string key, string value, [MarshalAs(UnmanagedType.I1)] bool remove);
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.I1)] static extern bool VfxUnity_IsPrivacyOptionsRequired();
        [DllImport("__Internal")] static extern void VfxUnity_ShowPrivacyOptions(int id);
        [DllImport("__Internal")] static extern void VfxUnity_SetMuted([MarshalAs(UnmanagedType.I1)] bool muted);
        [DllImport("__Internal")] static extern void VfxUnity_OpenAdInspector();
        [DllImport("__Internal")] static extern string VfxUnity_SettingsSource();
        [DllImport("__Internal")] static extern void VfxUnity_Flush();
        [DllImport("__Internal")] static extern string VfxUnity_NativeVersion();
        [DllImport("__Internal")] static extern void VfxUnity_BannerCreate(int id, string placement, int position);
        [DllImport("__Internal")] static extern void VfxUnity_BannerSetVisible(int id, [MarshalAs(UnmanagedType.I1)] bool visible);
        [DllImport("__Internal")] static extern void VfxUnity_BannerDestroy(int id);

        [MonoPInvokeCallback(typeof(Callback))]
        static void OnMessage(string json) => VisionForgeX.Receive(json);

        static readonly Callback Keep = OnMessage;   // never collected while the game runs

        public string Name => "ios";

        public void Initialize(string appKey, string optionsJson)
        {
            VfxUnity_SetCallback(Keep);
            VfxUnity_Initialize(appKey ?? "", optionsJson ?? "{}");
        }

        public bool IsReady() => VfxUnity_IsReady();
        public bool FullScreenReady(string placement, string format) => VfxUnity_FullScreenReady(placement, format);
        public void Load(string placement, string format) => VfxUnity_Load(placement, format);
        public void Show(int id, string placement, string format) => VfxUnity_Show(id, placement, format);
        public void PreloadAll() => VfxUnity_PreloadAll();
        public void SetKeyValue(string key, string value) => VfxUnity_SetKeyValue(key, value ?? "", value == null);
        public bool IsPrivacyOptionsRequired() => VfxUnity_IsPrivacyOptionsRequired();
        public void ShowPrivacyOptions(int id) => VfxUnity_ShowPrivacyOptions(id);
        public void SetMuted(bool muted) => VfxUnity_SetMuted(muted);
        public void OpenAdInspector() => VfxUnity_OpenAdInspector();
        public bool OpenDebugPanel() => false;   // the iOS SDK has no panel of its own: the plugin's opens
        public string SettingsSource() => VfxUnity_SettingsSource() ?? "none";
        public void Flush() => VfxUnity_Flush();
        public string NativeVersion() => VfxUnity_NativeVersion() ?? "";
        public void BannerCreate(int id, string placement, int position) => VfxUnity_BannerCreate(id, placement, position);
        public void BannerSetVisible(int id, bool visible) => VfxUnity_BannerSetVisible(id, visible);
        public void BannerDestroy(int id) => VfxUnity_BannerDestroy(id);
    }
}
#endif
