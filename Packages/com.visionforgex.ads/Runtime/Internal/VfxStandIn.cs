using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace VfxAds
{
    /// <summary>
    /// The Editor's stand-in for the native SDKs, so Play mode is never silent and never crashes: it starts, "loads"
    /// placements, shows placeholder ads (drawn by <see cref="VfxStandInView"/>), and sends the same messages and events
    /// a device would, in the same order. It decides nothing about ads: every show it can do, it does. It knows which
    /// placements exist only when the settings window has checked the key with the platform, or with Google's demo ads.
    /// No revenue is ever reported in the Editor.
    /// </summary>
    internal sealed class VfxStandIn : IVfxBridge
    {
        /// <summary>Google's demo placements (the native SDKs' demo settings).</summary>
        public static readonly IReadOnlyDictionary<string, string> DemoPlacements = new Dictionary<string, string>
        {
            { "banner", "banner" }, { "collapsible_banner", "banner" }, { "interstitial", "interstitial" }, { "rewarded", "rewarded" },
            { "rewarded_interstitial", "rewarded_interstitial" }, { "app_open", "app_open" },
        };

        /// <summary>The placements the platform has for the app, when the settings window checked the key (null: not known).</summary>
        public static IReadOnlyDictionary<string, string> PlatformPlacements;

        /// <summary>Tests turn this off, so a cache left in the project's Library never changes their result.</summary>
        internal static bool UseCache = true;

        /// <summary>Where the settings window keeps what the platform said (in the project's Library, never in a build).</summary>
        public static string CachePath(bool ios) => System.IO.Path.Combine("Library", "VisionForgeX", "placements-" + (ios ? "ios" : "android") + ".json");

        /// <summary>The placements the settings window last read from the platform for this platform's key (null: none read).</summary>
        public static IReadOnlyDictionary<string, string> ReadCache(bool ios)
        {
            try
            {
                var path = CachePath(ios);
                if (!System.IO.File.Exists(path)) return null;
                var o = VfxJson.Object(System.IO.File.ReadAllText(path));
                if (o == null) return null;
                var d = new Dictionary<string, string>();
                foreach (var kv in o) if (kv.Value is string f) d[kv.Key] = f;
                return d;
            }
            catch (Exception) { return null; }
        }

        public const long StartMs = 400, LoadMs = 700, RewardMs = 5000;

        /// <summary>A full-screen ad on screen in the Editor.</summary>
        public sealed class Showing
        {
            public int Id;
            public string Placement;
            public string Format;
            public long Since;
            public bool Rewarded;

            /// <summary>Seconds left before a rewarded ad's reward (0 when earned or not rewarded).</summary>
            public int SecondsLeft(long now) => Format == "rewarded" || Format == "rewarded_interstitial"
                ? Rewarded ? 0 : (int)Math.Ceiling(Math.Max(0, RewardMs - (now - Since)) / 1000.0) : 0;
        }

        /// <summary>A banner in the Editor.</summary>
        public sealed class Banner
        {
            public int Id;
            public string Placement;
            public int Position;
            public bool Visible;
            public bool Loaded;
            public long Due = -1;
        }

        readonly Func<long> _now;
        readonly Action<string> _send;
        readonly Dictionary<string, long> _loadedAt = new Dictionary<string, long>();
        readonly Dictionary<string, long> _loadingUntil = new Dictionary<string, long>();
        readonly Dictionary<string, string> _formats = new Dictionary<string, string>();
        readonly Dictionary<string, string> _kv = new Dictionary<string, string>();
        readonly Dictionary<int, Banner> _banners = new Dictionary<int, Banner>();
        long _readyAt = -1;
        bool _ready, _answered, _demo, _autoPreload = true, _muted;
        string _reason = "";

        /// <summary>The stand-in, with the Editor's clock and VisionForgeX.Receive; tests give their own.</summary>
        public VfxStandIn(Func<long> now = null, Action<string> send = null)
        {
            var clock = Stopwatch.StartNew();
            _now = now ?? (() => clock.ElapsedMilliseconds);
            _send = send ?? VisionForgeX.Receive;
        }

        /// <summary>The banner's height in pixels on this screen (the view sets it from the screen's density).</summary>
        public Func<int> BannerHeightPixels = () => 150;

        public string Name => "editor";

        /// <summary>The stand-in's clock, in milliseconds.</summary>
        public long Now => _now();

        /// <summary>What the view draws: the full-screen ad on screen, if any.</summary>
        public Showing Current { get; private set; }

        /// <summary>What the view draws: the banners.</summary>
        public IEnumerable<Banner> Banners => _banners.Values;

        public bool Muted => _muted;

        public void Initialize(string appKey, string optionsJson)
        {
            if (_readyAt >= 0) return;
            if (PlatformPlacements == null && UseCache) PlatformPlacements = ReadCache(VfxPlatform.IsIos);
            var o = VfxJson.Object(optionsJson) ?? new Dictionary<string, object>();
            _demo = VfxJson.B(o, "googleDemoAds");
            _autoPreload = !(o.TryGetValue("autoPreload", out var ap) && ap is bool b && !b);
            var testing = VfxJson.SOrNull(o, "settingsForTesting");
            if (!_demo && string.IsNullOrEmpty(testing) && string.IsNullOrEmpty(appKey))
                _reason = "no app key (set it in Assets → VisionForgeX → Settings)";
            _readyAt = _now() + StartMs;
            VfxLog.Info(_demo ? "Editor stand-in: Google's demo placements (banner, collapsible_banner, interstitial, rewarded, rewarded_interstitial, app_open)."
                : PlatformPlacements != null ? "Editor stand-in: the placements the platform has for this app (" + string.Join(", ", PlatformPlacements.Keys) + ")."
                : "Editor stand-in: placeholder ads for any placement name. Assets → VisionForgeX → Settings → Check the key, to know the app's real placements here.");
        }

        IReadOnlyDictionary<string, string> Placements => _demo ? DemoPlacements : PlatformPlacements;

        /// <summary>The platform's format for a placement ("" when not known), or the one the game asked for.</summary>
        string FormatOf(string placement, string asked)
        {
            var list = Placements;
            if (list != null && list.TryGetValue(placement, out var f)) return f;
            return asked;
        }

        bool Exists(string placement) => Placements == null || Placements.ContainsKey(placement);

        /// <summary>Moves time on: answers the start, finishes loads and banners. Called every frame by the view, by tests at will.</summary>
        public void Tick()
        {
            long now = _now();
            if (!_answered && _readyAt >= 0 && now >= _readyAt)
            {
                _answered = true;
                _ready = _reason.Length == 0;
                _send("{\"k\":\"init\",\"ready\":" + (_ready ? "true" : "false") + ",\"reason\":" + VfxJsonOut.Str(_reason) + ",\"fromCache\":false}");
                if (_ready && _autoPreload && Placements != null)
                    foreach (var p in Placements)
                        if (p.Value != "banner" && p.Value != "native") StartLoad(p.Key, p.Value);
            }
            if (!_ready) return;
            var done = new List<string>();
            foreach (var l in _loadingUntil) if (now >= l.Value) done.Add(l.Key);
            foreach (var p in done)
            {
                _loadingUntil.Remove(p);
                _loadedAt[p] = now;
                Event("fill", p, _formats[p], "\"tier\":0,\"ms\":" + LoadMs + ",\"src\":\"editor\"");
            }
            foreach (var b in _banners.Values)
            {
                if (b.Visible && !b.Loaded && b.Due < 0) { b.Due = now + LoadMs; Event("req", b.Placement, "banner", "\"tier\":0"); }
                if (b.Visible && !b.Loaded && b.Due >= 0 && now >= b.Due)
                {
                    b.Loaded = true;
                    Event("fill", b.Placement, "banner", "\"tier\":0,\"ms\":" + LoadMs + ",\"src\":\"editor\"");
                    Event("show", b.Placement, "banner", "");
                    _send("{\"k\":\"banner\",\"id\":" + b.Id + ",\"h\":" + BannerHeightPixels() + "}");
                }
            }
            if (Current != null && !Current.Rewarded && Current.SecondsLeft(now) == 0 && Current.Format.Contains("rewarded"))
            {
                Current.Rewarded = true;
                Event("reward", Current.Placement, Current.Format, "\"type\":\"coins\",\"amount\":10");
                _send("{\"k\":\"show\",\"id\":" + Current.Id + ",\"e\":\"reward\",\"t\":\"coins\",\"a\":10}");
            }
        }

        void StartLoad(string placement, string format)
        {
            if (_loadingUntil.ContainsKey(placement) || _loadedAt.ContainsKey(placement)) return;
            _formats[placement] = format;
            _loadingUntil[placement] = _now() + LoadMs;
            Event("req", placement, format, "\"tier\":0");
        }

        void Event(string type, string placement, string format, string details)
        {
            _send("{\"k\":\"event\",\"type\":\"" + type + "\",\"placement\":" + VfxJsonOut.Str(placement) + ",\"format\":\"" + format
                  + "\",\"time\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture) + ",\"details\":{" + details + "}}");
        }

        void NotShown(int id, string reason) => _send("{\"k\":\"show\",\"id\":" + id + ",\"e\":\"notShown\",\"r\":\"" + reason + "\"}");

        public bool IsReady() => _ready;

        public bool FullScreenReady(string placement, string format) => _ready && _loadedAt.ContainsKey(placement);

        public void Load(string placement, string format)
        {
            if (!_ready || !Exists(placement)) return;
            StartLoad(placement, FormatOf(placement, format));
        }

        public void Show(int id, string placement, string format)
        {
            Tick();
            if (!_ready) { NotShown(id, "not_ready"); return; }
            if (!Exists(placement)) { NotShown(id, "unknown_placement"); return; }
            var real = FormatOf(placement, format);
            if (real != format) VfxLog.Warn("\"" + placement + "\" is a " + real + " placement on the platform, not " + format + ".");
            if (real == "banner" || real == "native") { NotShown(id, "unknown_placement"); return; }
            if (Current != null) { Event("capped", placement, real, "\"why\":\"showing\""); NotShown(id, "showing"); return; }
            if (!_loadedAt.ContainsKey(placement)) { StartLoad(placement, real); NotShown(id, "not_ready"); return; }
            _loadedAt.Remove(placement);
            Current = new Showing { Id = id, Placement = placement, Format = real, Since = _now() };
            Event("show", placement, real, "");
            _send("{\"k\":\"show\",\"id\":" + id + ",\"e\":\"shown\"}");
        }

        /// <summary>The view's "tap the ad".</summary>
        public void ClickCurrent()
        {
            if (Current == null) return;
            Event("click", Current.Placement, Current.Format, "");
            _send("{\"k\":\"show\",\"id\":" + Current.Id + ",\"e\":\"clicked\"}");
        }

        /// <summary>The view's close button: the ad closes, and the placement loads again.</summary>
        public void CloseCurrent()
        {
            var c = Current;
            if (c == null) return;
            Current = null;
            Event("close", c.Placement, c.Format, "");
            _send("{\"k\":\"show\",\"id\":" + c.Id + ",\"e\":\"dismissed\"}");
            StartLoad(c.Placement, c.Format);
        }

        /// <summary>Tests: the reward's wait is over now.</summary>
        internal void SkipRewardWait() { if (Current != null) Current.Since -= RewardMs; }

        public void PreloadAll()
        {
            if (!_ready || Placements == null) return;
            foreach (var p in Placements) if (p.Value != "banner" && p.Value != "native") StartLoad(p.Key, p.Value);
        }

        public void SetKeyValue(string key, string value)
        {
            if (value == null) _kv.Remove(key); else _kv[key] = value;
        }

        public IReadOnlyDictionary<string, string> KeyValues => _kv;

        public bool IsPrivacyOptionsRequired() => false;

        public void ShowPrivacyOptions(int id)
        {
            VfxLog.Info("Editor stand-in: on a device in the EEA, the UK or Switzerland, Google's privacy options form opens here.");
            _send("{\"k\":\"consent\",\"id\":" + id + ",\"error\":null}");
        }

        public void SetMuted(bool muted) => _muted = muted;
        public void OpenAdInspector() => VfxLog.Info("Editor stand-in: Google's ad inspector opens on a device (development builds and test devices).");
        public bool OpenDebugPanel() => false;
        public string SettingsSource() => _answered ? (_demo ? "testing" : "editor") : "none";
        public void Flush() { }
        public string NativeVersion() => "";

        public void BannerCreate(int id, string placement, int position)
        {
            if (!Exists(placement)) { VfxLog.Warn("Banner \"" + placement + "\": no such placement for this app on the platform."); return; }
            var f = FormatOf(placement, "banner");
            if (f != "banner") { VfxLog.Warn("Banner \"" + placement + "\": it is a " + f + " placement on the platform, not a banner."); return; }
            _banners[id] = new Banner { Id = id, Placement = placement, Position = position };
        }

        public void BannerSetVisible(int id, bool visible)
        {
            if (!_banners.TryGetValue(id, out var b)) return;
            b.Visible = visible;
            _send("{\"k\":\"banner\",\"id\":" + id + ",\"h\":" + (visible && b.Loaded ? BannerHeightPixels() : 0) + "}");
        }

        public void BannerDestroy(int id) => _banners.Remove(id);
    }
}
