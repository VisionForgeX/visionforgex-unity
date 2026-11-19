using System.Collections.Generic;

namespace VfxAds.Tests
{
    /// <summary>A bridge that records every call and lets a test answer as a native SDK would.</summary>
    internal sealed class MockBridge : IVfxBridge
    {
        public readonly List<string> Calls = new List<string>();
        public bool Ready;
        public bool PrivacyRequired;
        public readonly HashSet<string> Loaded = new HashSet<string>();
        public readonly Dictionary<string, string> KeyValues = new Dictionary<string, string>();
        public string LastOptions = "";
        public string LastKey = "";

        public string Name => "mock";

        public void Initialize(string appKey, string optionsJson)
        {
            Calls.Add("init:" + appKey);
            LastKey = appKey;
            LastOptions = optionsJson;
        }

        public bool IsReady() => Ready;
        public bool FullScreenReady(string placement, string format) { Calls.Add("ready?:" + placement + ":" + format); return Loaded.Contains(placement); }
        public void Load(string placement, string format) => Calls.Add("load:" + placement + ":" + format);
        public void Show(int id, string placement, string format) => Calls.Add("show:" + id + ":" + placement + ":" + format);
        public void PreloadAll() => Calls.Add("preloadAll");
        public void SetKeyValue(string key, string value) { Calls.Add("kv:" + key + "=" + (value ?? "<null>")); if (value == null) KeyValues.Remove(key); else KeyValues[key] = value; }
        public bool IsPrivacyOptionsRequired() => PrivacyRequired;
        public void ShowPrivacyOptions(int id) => Calls.Add("privacy:" + id);
        public void SetMuted(bool muted) => Calls.Add("muted:" + muted);
        public void OpenAdInspector() => Calls.Add("inspector");
        public bool OpenDebugPanel() { Calls.Add("debugPanel"); return true; }
        public string SettingsSource() => "platform";
        public void Flush() => Calls.Add("flush");
        public string NativeVersion() => "1.3.0";
        public void BannerCreate(int id, string placement, int position) => Calls.Add("banner+:" + id + ":" + placement + ":" + position);
        public void BannerSetVisible(int id, bool visible) => Calls.Add("banner" + (visible ? "Show" : "Hide") + ":" + id);
        public void BannerDestroy(int id) => Calls.Add("banner-:" + id);

        /// <summary>The id of the last show call (0 when none).</summary>
        public int LastShowId
        {
            get
            {
                for (int i = Calls.Count - 1; i >= 0; i--)
                    if (Calls[i].StartsWith("show:")) return int.Parse(Calls[i].Split(':')[1]);
                return 0;
            }
        }

        public int LastBannerId
        {
            get
            {
                for (int i = Calls.Count - 1; i >= 0; i--)
                    if (Calls[i].StartsWith("banner+:")) return int.Parse(Calls[i].Split(':')[1]);
                return 0;
            }
        }

        // answers, as the native bridges send them
        public static void Init(bool ready, string reason = "", bool cache = false) =>
            VisionForgeX.Receive("{\"k\":\"init\",\"ready\":" + (ready ? "true" : "false") + ",\"reason\":\"" + reason + "\",\"fromCache\":" + (cache ? "true" : "false") + "}");
        public static void ShowEvent(int id, string e, string extra = "") => VisionForgeX.Receive("{\"k\":\"show\",\"id\":" + id + ",\"e\":\"" + e + "\"" + extra + "}");
    }
}
