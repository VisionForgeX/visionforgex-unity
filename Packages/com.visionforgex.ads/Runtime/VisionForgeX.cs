using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VfxAds
{
    /// <summary>
    /// The VisionForgeX SDK for Unity games. Start it once, as early as you can (the first scene):
    /// <code>
    /// using VfxAds;
    ///
    /// VisionForgeX.Initialize(status => Debug.Log("VisionForgeX: " + status));   // the key from Assets → VisionForgeX → Settings
    /// </code>
    /// Then show ads by placement name, as set on apps.visionforgex.com:
    /// <code>
    /// VisionForgeX.Interstitial("level_complete").Show();
    /// VisionForgeX.Rewarded("extra_life").Show(new VfxShowCallbacks { OnReward = (type, amount) => GiveLife() });
    /// var banner = VfxBanner.Show("home_banner", VfxBannerPosition.Bottom);
    /// </code>
    /// Everything else (what each placement requests, how often, when it may show, its floors, consent, experiments) is
    /// decided by the platform and reaches the game through the native VisionForgeX SDKs. Nothing here decides anything.
    /// Every callback is called on Unity's main thread.
    /// </summary>
    public static partial class VisionForgeX
    {
        /// <summary>This plugin's version.</summary>
        public const string Version = "1.0.0";

        /// <summary>The native SDKs this version is built on.</summary>
        public const string AndroidSdkVersion = "1.2.0";
        public const string IosSdkVersion = "1.2.0";

        static readonly Regex PlacementName = new Regex("^[a-z0-9_]{1,40}$");
        static readonly Regex KeyName = new Regex("^[a-z0-9_]{1,20}$");

        static IVfxBridge _bridge;
        static bool _started;
        static VfxStatus _status;
        static readonly List<Action<VfxStatus>> WaitingForReady = new List<Action<VfxStatus>>();
        static int _nextId;
        static readonly Dictionary<int, PendingShow> Shows = new Dictionary<int, PendingShow>();
        static readonly Dictionary<int, Action<string>> Consents = new Dictionary<int, Action<string>>();
        static readonly Dictionary<int, VfxBanner> Banners = new Dictionary<int, VfxBanner>();
        static int _fullScreenOpen;
        static readonly object Gate = new object();

        sealed class PendingShow
        {
            public VfxShowCallbacks Callbacks;
            public string Placement;
            public VfxFormat Format;
            public bool Shown;
            public bool Rewarded;
        }

        // ------------------------------------------------------------------ start

        /// <summary>
        /// Starts the SDK with the key in Assets → VisionForgeX → Settings (the Android or iOS key, as the build is),
        /// and the settings' test mode (Google's demo ads, in development builds only).
        /// </summary>
        public static void Initialize(Action<VfxStatus> onReady = null)
        {
            var s = VfxSettings.Current;
            var options = s != null ? s.ToOptions(VfxPlatform.IsDevelopmentBuild) : new VfxOptions();
            Initialize(s != null ? s.AppKeyForThisPlatform() : "", options, onReady);
        }

        /// <summary>
        /// Starts the SDK: its settings are fetched from the platform (the last ones used meanwhile), consent asked where
        /// the law needs it (Google's form), and the placements that preload begin loading. Call it once; calling it
        /// again does nothing new, and <paramref name="onReady"/> still hears the answer.
        /// </summary>
        /// <param name="appKey">The app's key, from its page on apps.visionforgex.com (VisionForgeX SDK panel).</param>
        /// <param name="options">Testing options; null for none.</param>
        /// <param name="onReady">Called once, on Unity's main thread, when the SDK is ready or knows why not.</param>
        public static void Initialize(string appKey, VfxOptions options = null, Action<VfxStatus> onReady = null)
        {
            VfxMainThread.Capture();
            EnsureRunner();
            lock (Gate)
            {
                if (_status != null) { var st = _status; if (onReady != null) VfxMainThread.Post(() => onReady(st)); return; }
                if (onReady != null) WaitingForReady.Add(onReady);
                if (_started) return;
                _started = true;
            }
            options = options ?? new VfxOptions();
            var key = (appKey ?? "").Trim();
            if (key.Length == 0 && !options.GoogleDemoAds && string.IsNullOrEmpty(options.SettingsForTesting))
                VfxLog.Warn("No app key: set it in Assets → VisionForgeX → Settings, or pass it to Initialize. The SDK will not be ready.");
            if (options.GoogleDemoAds && !VfxPlatform.IsDevelopmentBuild)
                VfxLog.Warn("Google's demo ads are on in a release build. A real game's settings come from VisionForgeX: turn them off before you publish.");
            Bridge.Initialize(key, options.ToJson());
        }

        /// <summary>True once the SDK has settings that allow ads and consent allows requests.</summary>
        public static bool IsReady => _started && Bridge.IsReady();

        /// <summary>Started (Initialize was called).</summary>
        public static bool IsInitialized => _started;

        /// <summary>The answer Initialize got (null until it has one).</summary>
        public static VfxStatus Status => _status;

        // ------------------------------------------------------------------ placements

        /// <summary>An interstitial placement.</summary>
        public static VfxFullScreen Interstitial(string placement) => new VfxFullScreen(placement, VfxFormat.Interstitial);
        /// <summary>A rewarded placement.</summary>
        public static VfxFullScreen Rewarded(string placement) => new VfxFullScreen(placement, VfxFormat.Rewarded);
        /// <summary>A rewarded interstitial placement.</summary>
        public static VfxFullScreen RewardedInterstitial(string placement) => new VfxFullScreen(placement, VfxFormat.RewardedInterstitial);
        /// <summary>An app open placement (shown by itself when the platform turns that on; or call Show()).</summary>
        public static VfxFullScreen AppOpen(string placement) => new VfxFullScreen(placement, VfxFormat.AppOpen);

        /// <summary>Loads every placement that preloads (only needed with <c>AutoPreload = false</c>).</summary>
        public static void PreloadAll() { if (_started) Bridge.PreloadAll(); }

        /// <summary>
        /// A key-value of the game's own added to every request (e.g. "level" → "12"), for targeting and reports in
        /// Ad Manager. Keys: lowercase letters, digits and _, at most 20; "vfx_" is ours. Null removes it.
        /// </summary>
        public static void SetKeyValue(string key, string value)
        {
            if (key == null || !KeyName.IsMatch(key) || key.StartsWith("vfx_", StringComparison.Ordinal))
            {
                VfxLog.Warn("Key-value \"" + key + "\" not used: lowercase letters, digits and _, at most 20, not starting with vfx_.");
                return;
            }
            Bridge.SetKeyValue(key, value);
        }

        // ------------------------------------------------------------------ consent, sound, tools

        /// <summary>Whether the game must offer a "Privacy options" button (users in the EEA, UK, Switzerland, some US states).</summary>
        public static bool IsPrivacyOptionsRequired => _started && Bridge.IsPrivacyOptionsRequired();

        /// <summary>Opens Google's privacy options form, from the game's "Privacy options" button. <paramref name="onDone"/> gets an error message, or null.</summary>
        public static void ShowPrivacyOptions(Action<string> onDone = null)
        {
            int id;
            lock (Gate) { id = ++_nextId; Consents[id] = onDone; }
            if (!_started) { Receive("{\"k\":\"consent\",\"id\":" + id + ",\"error\":\"the SDK has not started\"}"); return; }
            Bridge.ShowPrivacyOptions(id);
        }

        /// <summary>Mutes video ads (the game's own sound setting).</summary>
        public static void SetMuted(bool muted) => Bridge.SetMuted(muted);

        /// <summary>Google's ad inspector, for checking a live build on a device.</summary>
        public static void OpenAdInspector() { if (_started) Bridge.OpenAdInspector(); }

        /// <summary>
        /// The debug panel: the SDK's state, each placement with Load and Show, and the last events. On Android it is the
        /// native SDK's own screen; on iOS and in the Editor, this plugin's. For development builds.
        /// </summary>
        public static void OpenDebugPanel()
        {
            if (!(_started && Bridge.OpenDebugPanel())) OpenCSharpDebugPanel();
        }

        /// <summary>Where the SDK's settings came from: "platform", "cache", "testing", "editor" or "none".</summary>
        public static string SettingsSource() => _started ? Bridge.SettingsSource() : "none";

        /// <summary>Sends the events waiting now (they are sent by themselves too).</summary>
        public static void Flush() { if (_started) Bridge.Flush(); }

        /// <summary>The native SDK's version inside this build ("" in the Editor).</summary>
        public static string NativeVersion => Bridge.NativeVersion();

        /// <summary>"android", "ios", "editor" (the stand-in) or "none".</summary>
        public static string Platform => Bridge.Name;

        // ------------------------------------------------------------------ what happens

        /// <summary>Every ad event as it happens (for the game's own analytics or a debug screen). On Unity's main thread.</summary>
        public static event Action<VfxEvent> OnEvent;

        /// <summary>What each impression earned, as Google reported it (Firebase, AppsFlyer, Adjust…). On Unity's main thread.</summary>
        public static event Action<VfxRevenue> OnRevenue;

        /// <summary>A full-screen ad opened (true) or closed (false). On Unity's main thread.</summary>
        public static event Action<bool> OnFullScreenChanged;

        /// <summary>A full-screen ad is on screen now.</summary>
        public static bool IsFullScreenShowing => _fullScreenOpen > 0;

        /// <summary>The last events (up to 100), newest last: what the debug panel shows.</summary>
        public static IReadOnlyList<VfxEvent> RecentEvents => Recent;

        static readonly List<VfxEvent> Recent = new List<VfxEvent>();

        /// <summary>Placements the game used or heard of in this session, with their format.</summary>
        public static IReadOnlyDictionary<string, VfxFormat> KnownPlacements => Known;

        static readonly Dictionary<string, VfxFormat> Known = new Dictionary<string, VfxFormat>();

        // ------------------------------------------------------------------ inside

        internal static IVfxBridge Bridge
        {
            get
            {
                if (_bridge == null)
                {
                    IVfxBridge b = null;
                    CreatePlatformBridge(ref b);
                    _bridge = b ?? new VfxNoneBridge();
                }
                return _bridge;
            }
        }

        /// <summary>Tests only: a bridge of the test's own, and everything forgotten.</summary>
        internal static void ResetForTests(IVfxBridge bridge)
        {
            lock (Gate)
            {
                _bridge = bridge;
                _started = false;
                _status = null;
                WaitingForReady.Clear();
                Shows.Clear();
                Consents.Clear();
                Banners.Clear();
                Recent.Clear();
                Known.Clear();
                _nextId = 0;
                _fullScreenOpen = 0;
                OnEvent = null;
                OnRevenue = null;
                OnFullScreenChanged = null;
            }
            VfxMainThread.ResetForTests();
            VfxMainThread.Capture();
        }

        static partial void CreatePlatformBridge(ref IVfxBridge bridge);
        static partial void EnsureRunnerImpl();
        static partial void OpenCSharpDebugPanelImpl();

        static void EnsureRunner() => EnsureRunnerImpl();
        static void OpenCSharpDebugPanel() => OpenCSharpDebugPanelImpl();

        internal static bool IsPlacementName(string placement) => placement != null && PlacementName.IsMatch(placement);

        internal static void Remember(string placement, VfxFormat format)
        {
            if (!IsPlacementName(placement)) return;
            lock (Gate) Known[placement] = format;
        }

        /// <summary>Asks the platform to show; the answer arrives as "show" messages.</summary>
        internal static void Show(string placement, VfxFormat format, VfxShowCallbacks callbacks)
        {
            callbacks = callbacks ?? new VfxShowCallbacks();
            int id;
            lock (Gate)
            {
                id = ++_nextId;
                Shows[id] = new PendingShow { Callbacks = callbacks, Placement = placement, Format = format };
            }
            if (!IsPlacementName(placement))
            {
                VfxLog.Warn("\"" + placement + "\" is not a placement name: 1 to 40 lowercase letters, digits or _ (like level_complete).");
                Deliver(id, "notShown", VfxReason.UnknownPlacement, null, 0);
                return;
            }
            Remember(placement, format);
            if (!_started)
            {
                VfxLog.Warn("Show(\"" + placement + "\") before Initialize: nothing shown.");
                Deliver(id, "notShown", VfxReason.NotReady, null, 0);
                return;
            }
            Bridge.Show(id, placement, format.Wire());
        }

        internal static void RegisterBanner(int id, VfxBanner b) { lock (Gate) Banners[id] = b; }
        internal static void ForgetBanner(int id) { lock (Gate) Banners.Remove(id); }
        internal static int NextId() { lock (Gate) return ++_nextId; }

        /// <summary>
        /// A message from the platform's bridge (any thread). Parsed here, delivered to the game on Unity's main thread.
        /// Unknown or malformed messages are ignored: an older plugin must keep working beside a newer bridge.
        /// </summary>
        internal static void Receive(string json)
        {
            var m = VfxJson.Object(json);
            if (m == null) { VfxLog.Warn("Unreadable message from the native SDK, ignored."); return; }
            switch (VfxJson.S(m, "k"))
            {
                case "init":
                {
                    var st = new VfxStatus(VfxJson.B(m, "ready"), VfxJson.S(m, "reason"), VfxJson.B(m, "fromCache"));
                    List<Action<VfxStatus>> waiting;
                    lock (Gate)
                    {
                        if (_status != null) return;   // the first answer is the answer
                        _status = st;
                        waiting = new List<Action<VfxStatus>>(WaitingForReady);
                        WaitingForReady.Clear();
                    }
                    VfxMainThread.Post(() =>
                    {
                        if (!st.Ready) VfxLog.Warn("Not ready: " + st.Reason);
                        foreach (var w in waiting) w(st);
                    });
                    break;
                }
                case "show":
                    Deliver((int)VfxJson.L(m, "id"), VfxJson.S(m, "e"), VfxJson.SOrNull(m, "r"), VfxJson.S(m, "t"), (int)VfxJson.L(m, "a"));
                    break;
                case "event":
                {
                    var details = VfxJson.O(m, "details") ?? new Dictionary<string, object>();
                    var e = new VfxEvent(VfxJson.S(m, "type"), VfxJson.S(m, "placement"), VfxFormats.FromWire(VfxJson.S(m, "format")), details, VfxJson.L(m, "time"));
                    VfxMainThread.Post(() => Dispatch(e));
                    break;
                }
                case "revenue":
                {
                    var r = new VfxRevenue(VfxJson.S(m, "placement"), VfxFormats.FromWire(VfxJson.S(m, "format")), VfxJson.L(m, "valueMicros"),
                        VfxJson.S(m, "currency"), VfxJson.S(m, "precision"), VfxJson.SOrNull(m, "source"));
                    VfxMainThread.Post(() => Dispatch(r));
                    break;
                }
                case "consent":
                {
                    int id = (int)VfxJson.L(m, "id");
                    var err = VfxJson.SOrNull(m, "error");
                    Action<string> cb;
                    lock (Gate) { Consents.TryGetValue(id, out cb); Consents.Remove(id); }
                    if (cb != null) VfxMainThread.Post(() => cb(err));
                    break;
                }
                case "banner":
                {
                    int id = (int)VfxJson.L(m, "id");
                    int h = (int)VfxJson.L(m, "h");
                    VfxBanner b;
                    lock (Gate) Banners.TryGetValue(id, out b);
                    if (b != null) VfxMainThread.Post(() => b.SetHeight(h));
                    break;
                }
            }
        }

        static void Deliver(int id, string what, string reason, string type, int amount)
        {
            PendingShow p;
            lock (Gate) Shows.TryGetValue(id, out p);
            if (p == null) return;
            var cb = p.Callbacks;
            switch (what)
            {
                case "shown":
                    VfxMainThread.Post(() =>
                    {
                        p.Shown = true;
                        _fullScreenOpen++;
                        Safe(() => OnFullScreenChanged?.Invoke(true));
                        Safe(() => cb.OnShown?.Invoke());
                    });
                    break;
                case "clicked":
                    VfxMainThread.Post(() => Safe(() => cb.OnClicked?.Invoke()));
                    break;
                case "reward":
                    VfxMainThread.Post(() => { p.Rewarded = true; Safe(() => cb.OnReward?.Invoke(type ?? "", amount)); });
                    break;
                case "dismissed":
                case "notShown":
                    lock (Gate) Shows.Remove(id);
                    bool dismissed = what == "dismissed";
                    VfxMainThread.Post(() =>
                    {
                        if (dismissed) Safe(() => cb.OnDismissed?.Invoke());
                        else Safe(() => cb.OnNotShown?.Invoke(reason ?? VfxReason.Failed));
                        if (p.Shown)
                        {
                            _fullScreenOpen = Math.Max(0, _fullScreenOpen - 1);
                            Safe(() => OnFullScreenChanged?.Invoke(false));
                        }
                        cb.OnFinished?.Invoke(new VfxShowResult(dismissed, dismissed ? null : (reason ?? VfxReason.Failed), p.Rewarded));
                    });
                    break;
            }
        }

        /// <summary>One of the game's callbacks: if it throws, it is logged and the next callbacks still run.</summary>
        static void Safe(Action a)
        {
            try { a(); }
            catch (Exception e) { VfxLog.Error("A callback of the game's threw: " + e); }
        }

        static void Dispatch(VfxEvent e)
        {
            if (e.Format.HasValue) Remember(e.Placement, e.Format.Value);
            Recent.Add(e);
            if (Recent.Count > 100) Recent.RemoveAt(0);
            List<VfxBanner> banners = null;
            if (e.Format == VfxFormat.Banner)
                lock (Gate)
                    foreach (var b in Banners.Values)
                        if (b.Placement == e.Placement) (banners = banners ?? new List<VfxBanner>()).Add(b);
            if (banners != null) foreach (var b in banners) Safe(() => b.OnPlacementEvent(e));
            Safe(() => OnEvent?.Invoke(e));
        }

        static void Dispatch(VfxRevenue r)
        {
            List<VfxBanner> banners = null;
            if (r.Format == VfxFormat.Banner)
                lock (Gate)
                    foreach (var b in Banners.Values)
                        if (b.Placement == r.Placement) (banners = banners ?? new List<VfxBanner>()).Add(b);
            if (banners != null) foreach (var b in banners) Safe(() => b.OnPlacementRevenue(r));
            Safe(() => OnRevenue?.Invoke(r));
        }
    }
}
