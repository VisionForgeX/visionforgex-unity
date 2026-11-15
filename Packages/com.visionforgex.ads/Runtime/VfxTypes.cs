using System;
using System.Collections.Generic;
using System.Text;

namespace VfxAds
{
    /// <summary>The ad formats, as the platform names them.</summary>
    public enum VfxFormat
    {
        Banner,
        Interstitial,
        Rewarded,
        RewardedInterstitial,
        AppOpen,
        Native,
    }

    /// <summary>The platform's names for the formats ("rewarded_interstitial"…), both ways.</summary>
    public static class VfxFormats
    {
        public static string Wire(this VfxFormat f)
        {
            switch (f)
            {
                case VfxFormat.Banner: return "banner";
                case VfxFormat.Interstitial: return "interstitial";
                case VfxFormat.Rewarded: return "rewarded";
                case VfxFormat.RewardedInterstitial: return "rewarded_interstitial";
                case VfxFormat.AppOpen: return "app_open";
                default: return "native";
            }
        }

        /// <summary>The format for a platform name; null when it is not one.</summary>
        public static VfxFormat? FromWire(string s)
        {
            switch (s)
            {
                case "banner": return VfxFormat.Banner;
                case "interstitial": return VfxFormat.Interstitial;
                case "rewarded": return VfxFormat.Rewarded;
                case "rewarded_interstitial": return VfxFormat.RewardedInterstitial;
                case "app_open": return VfxFormat.AppOpen;
                case "native": return VfxFormat.Native;
                default: return null;
            }
        }

        public static bool IsFullScreen(this VfxFormat f) => f != VfxFormat.Banner && f != VfxFormat.Native;
        public static bool IsRewarded(this VfxFormat f) => f == VfxFormat.Rewarded || f == VfxFormat.RewardedInterstitial;
    }

    /// <summary>
    /// Why no ad was shown. These are the protocol's words (docs/PROTOCOL.md in the native SDKs), exactly as the native
    /// SDKs say them, so a game can log or count them the same way on every platform.
    /// </summary>
    public static class VfxReason
    {
        /// <summary>No ad is loaded yet (or the SDK has not started).</summary>
        public const string NotReady = "not_ready";
        /// <summary>The app or this placement is paused on the platform.</summary>
        public const string Paused = "paused";
        /// <summary>Consent does not allow ad requests.</summary>
        public const string Consent = "consent";
        /// <summary>Too soon after this placement's last ad.</summary>
        public const string Interval = "interval";
        /// <summary>The placement's limit for this session is reached.</summary>
        public const string Session = "session";
        /// <summary>The placement's limit for this hour is reached.</summary>
        public const string Hour = "hour";
        /// <summary>The placement's limit for today is reached.</summary>
        public const string Day = "day";
        /// <summary>Too early in the session (or in the first sessions).</summary>
        public const string Grace = "grace";
        /// <summary>Only every Nth opportunity shows an ad.</summary>
        public const string Nth = "nth";
        /// <summary>Too soon after any full-screen ad.</summary>
        public const string Gap = "gap";
        /// <summary>Another full-screen ad is on screen.</summary>
        public const string Showing = "showing";
        /// <summary>Google could not show the loaded ad.</summary>
        public const string Failed = "failed";
        /// <summary>There is no screen to show it on (Android: no current activity).</summary>
        public const string NoActivity = "no_activity";
        /// <summary>The platform has no placement of that name (or the name is not a placement name).</summary>
        public const string UnknownPlacement = "unknown_placement";

        /// <summary>Every reason, in the protocol's order.</summary>
        public static readonly IReadOnlyList<string> All = new[]
        {
            NotReady, Paused, Consent, Interval, Session, Hour, Day, Grace, Nth, Gap, Showing, Failed, NoActivity, UnknownPlacement,
        };

        /// <summary>The pacing reasons: the platform said "not now", which is normal.</summary>
        public static bool IsPacing(string reason) =>
            reason == Interval || reason == Session || reason == Hour || reason == Day || reason == Grace || reason == Nth || reason == Gap;
    }

    /// <summary>The protocol's event types (VfxEvent.Type).</summary>
    public static class VfxEventType
    {
        public const string Request = "req";
        public const string Fill = "fill";
        public const string NoFill = "nofill";
        public const string Show = "show";
        public const string Click = "click";
        public const string Close = "close";
        public const string Fail = "fail";
        public const string Reward = "reward";
        public const string Paid = "paid";
        public const string Capped = "capped";

        public static readonly IReadOnlyList<string> All = new[] { Request, Fill, NoFill, Show, Click, Close, Fail, Reward, Paid, Capped };
    }

    /// <summary>How much the SDK writes to the device's log.</summary>
    public enum VfxLogLevel { None, Error, Warn, Info, Debug }

    /// <summary>Where a banner sits. It always stays inside the safe area (notches, rounded corners, the home bar).</summary>
    public enum VfxBannerPosition { Bottom = 0, Top = 1 }

    /// <summary>
    /// Options for <see cref="VisionForgeX.Initialize(string, VfxOptions, Action{VfxStatus})"/>. Everything that decides
    /// revenue comes from the platform; these are only for building and testing the game.
    /// </summary>
    [Serializable]
    public sealed class VfxOptions
    {
        /// <summary>Google test devices: their ads are test ads (the device log prints the ID on the first request).</summary>
        public List<string> TestDeviceIds = new List<string>();

        /// <summary>The device log's level; null: as the platform says.</summary>
        public VfxLogLevel? LogLevel;

        /// <summary>Show the consent form as if the device were in the EEA (testing only).</summary>
        public bool ConsentDebugEea;

        /// <summary>Hashed device IDs for the consent debug settings (the device log prints yours).</summary>
        public List<string> ConsentTestDeviceHashedIds = new List<string>();

        /// <summary>The platform's address; only for testing against another server.</summary>
        public string BaseUrl;

        /// <summary>Start loading as soon as the settings arrive (true), or only when asked.</summary>
        public bool AutoPreload = true;

        /// <summary>
        /// Testing only: every format on Google's own demo ad units, which always answer with a test ad. Nothing is sent
        /// anywhere. Never in a release: a real game's settings come from VisionForgeX. Placements: banner,
        /// collapsible_banner, interstitial, rewarded, rewarded_interstitial, app_open.
        /// </summary>
        public bool GoogleDemoAds;

        /// <summary>Testing only: these settings (the protocol's JSON) instead of the platform's.</summary>
        public string SettingsForTesting;

        /// <summary>The options as the native bridges read them.</summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"testDeviceIds\":").Append(VfxJsonOut.Array(TestDeviceIds));
            sb.Append(",\"logLevel\":").Append(LogLevel.HasValue ? VfxJsonOut.Str(LogLevel.Value.ToString().ToLowerInvariant()) : "null");
            sb.Append(",\"consentDebugEea\":").Append(ConsentDebugEea ? "true" : "false");
            sb.Append(",\"consentTestDeviceHashedIds\":").Append(VfxJsonOut.Array(ConsentTestDeviceHashedIds));
            sb.Append(",\"baseUrl\":").Append(string.IsNullOrEmpty(BaseUrl) ? "null" : VfxJsonOut.Str(BaseUrl));
            sb.Append(",\"autoPreload\":").Append(AutoPreload ? "true" : "false");
            sb.Append(",\"googleDemoAds\":").Append(GoogleDemoAds ? "true" : "false");
            sb.Append(",\"settingsForTesting\":").Append(string.IsNullOrEmpty(SettingsForTesting) ? "null" : VfxJsonOut.Str(SettingsForTesting));
            sb.Append('}');
            return sb.ToString();
        }
    }

    /// <summary>What <see cref="VisionForgeX.Initialize(string, VfxOptions, Action{VfxStatus})"/> found out.</summary>
    public sealed class VfxStatus
    {
        public VfxStatus(bool ready, string reason, bool fromCache)
        {
            Ready = ready;
            Reason = reason ?? "";
            FromCache = fromCache;
        }

        /// <summary>Ads may be shown: settings received, the app enabled, consent allows requests.</summary>
        public bool Ready { get; }

        /// <summary>Why not, when <see cref="Ready"/> is false.</summary>
        public string Reason { get; }

        /// <summary>The settings came from the cache (the platform was not reached).</summary>
        public bool FromCache { get; }

        public override string ToString() => Ready ? (FromCache ? "ready (cached settings)" : "ready") : "not ready: " + Reason;
    }

    /// <summary>What happened to one Show() call, given to <see cref="VfxShowCallbacks.OnFinished"/>.</summary>
    public sealed class VfxShowResult
    {
        public VfxShowResult(bool shown, string reason, bool rewarded)
        {
            Shown = shown;
            Reason = reason;
            Rewarded = rewarded;
        }

        /// <summary>An ad was shown, and has been closed by now.</summary>
        public bool Shown { get; }

        /// <summary>When nothing was shown, why: one of <see cref="VfxReason"/>.</summary>
        public string Reason { get; }

        /// <summary>A rewarded ad's reward was earned.</summary>
        public bool Rewarded { get; }

        public override string ToString() => Shown ? (Rewarded ? "shown, rewarded" : "shown") : "not shown: " + Reason;
    }

    /// <summary>
    /// What a game wants to hear from one Show() call. Every callback is optional and is called on Unity's main thread.
    /// <see cref="OnFinished"/> is always called last, exactly once: carry on with the game there.
    /// </summary>
    public sealed class VfxShowCallbacks
    {
        /// <summary>The ad is on screen (pause the game's music or timers here if you wish).</summary>
        public Action OnShown;

        /// <summary>The user tapped the ad.</summary>
        public Action OnClicked;

        /// <summary>Rewarded formats: the user earned the reward (type and amount as set in Ad Manager). Before the ad closes.</summary>
        public Action<string, int> OnReward;

        /// <summary>The ad was closed.</summary>
        public Action OnDismissed;

        /// <summary>No ad was shown, and why (one of <see cref="VfxReason"/>). Nothing to do but carry on.</summary>
        public Action<string> OnNotShown;

        /// <summary>Always last, once: shown and closed, or not shown and why.</summary>
        public Action<VfxShowResult> OnFinished;
    }

    /// <summary>Every ad event as it happens (for the game's own analytics or a debug screen).</summary>
    public sealed class VfxEvent
    {
        public VfxEvent(string type, string placement, VfxFormat? format, IReadOnlyDictionary<string, object> details, long time)
        {
            Type = type ?? "";
            Placement = placement ?? "";
            Format = format;
            Details = details ?? new Dictionary<string, object>();
            Time = time;
        }

        /// <summary>One of <see cref="VfxEventType"/>: req, fill, nofill, show, click, close, fail, reward, paid, capped.</summary>
        public string Type { get; }
        public string Placement { get; }
        public VfxFormat? Format { get; }

        /// <summary>The rest: tier, ms, err, why, src, and for "paid" v (micros), cur, prec.</summary>
        public IReadOnlyDictionary<string, object> Details { get; }

        /// <summary>Milliseconds since 1970 (UTC).</summary>
        public long Time { get; }

        /// <summary>A detail as text ("" when absent).</summary>
        public string Detail(string key) => Details.TryGetValue(key, out var v) && v != null ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : "";

        public override string ToString()
        {
            var sb = new StringBuilder(Type).Append(" · ").Append(Placement);
            foreach (var kv in Details) sb.Append(' ').Append(kv.Key).Append('=').Append(Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }

    /// <summary>What one impression earned, as Google reported it (its paid event): for Firebase, AppsFlyer, Adjust…</summary>
    public sealed class VfxRevenue
    {
        public VfxRevenue(string placement, VfxFormat? format, long valueMicros, string currency, string precision, string source)
        {
            Placement = placement ?? "";
            Format = format;
            ValueMicros = valueMicros;
            Currency = currency ?? "";
            Precision = precision ?? "";
            Source = source;
        }

        public string Placement { get; }
        public VfxFormat? Format { get; }

        /// <summary>Micros: 1,000,000 = one unit of <see cref="Currency"/>.</summary>
        public long ValueMicros { get; }

        public string Currency { get; }

        /// <summary>Google's precision: "estimated", "publisher_provided", "precise" or "unknown".</summary>
        public string Precision { get; }

        /// <summary>The ad source that filled it, when Google says.</summary>
        public string Source { get; }

        /// <summary>The value in units of <see cref="Currency"/>.</summary>
        public double Value => ValueMicros / 1000000.0;

        public override string ToString() => $"{Placement}: {Value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)} {Currency} ({Precision})";
    }
}
