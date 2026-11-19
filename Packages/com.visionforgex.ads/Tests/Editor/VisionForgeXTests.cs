using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace VfxAds.Tests
{
    /// <summary>The C# layer against a mock bridge: what reaches the native SDK, and what reaches the game, on which thread.</summary>
    public class VisionForgeXTests
    {
        MockBridge _b;
        List<string> _log;

        [SetUp]
        public void SetUp()
        {
            _b = new MockBridge();
            VisionForgeX.ResetForTests(_b);
            _log = new List<string>();
            VfxLog.Sink = (level, m) => _log.Add(level + ":" + m);
            VfxSettings.OverrideForTests = null;
        }

        [TearDown]
        public void TearDown()
        {
            VfxSettings.OverrideForTests = null;
            VfxLog.Sink = (level, m) => { };
        }

        static void Pump() => VfxMainThread.Pump();

        static void OffMainThread(Action a)
        {
            var t = new Thread(() => a());
            t.Start();
            t.Join();
        }

        // ------------------------------------------------------------------ the protocol's words

        [Test]
        public void TheReasonsAreTheProtocolsExactly()
        {
            CollectionAssert.AreEqual(new[] { "not_ready", "paused", "consent", "interval", "session", "hour", "day", "grace", "nth", "gap", "showing", "failed", "no_activity", "unknown_placement" },
                VfxReason.All.ToArray());
            Assert.IsTrue(VfxReason.IsPacing("interval") && VfxReason.IsPacing("gap") && !VfxReason.IsPacing("not_ready") && !VfxReason.IsPacing("consent"));
        }

        [Test]
        public void TheEventTypesAreTheProtocols()
        {
            CollectionAssert.AreEqual(new[] { "req", "fill", "nofill", "show", "click", "close", "fail", "reward", "paid", "capped" }, VfxEventType.All.ToArray());
        }

        [Test]
        public void FormatsHaveThePlatformsNamesBothWays()
        {
            foreach (VfxFormat f in Enum.GetValues(typeof(VfxFormat))) Assert.AreEqual(f, VfxFormats.FromWire(f.Wire()));
            Assert.AreEqual("rewarded_interstitial", VfxFormat.RewardedInterstitial.Wire());
            Assert.AreEqual("app_open", VfxFormat.AppOpen.Wire());
            Assert.IsNull(VfxFormats.FromWire("video"));
            Assert.IsTrue(VfxFormat.AppOpen.IsFullScreen() && !VfxFormat.Banner.IsFullScreen() && !VfxFormat.Native.IsFullScreen());
            Assert.IsTrue(VfxFormat.RewardedInterstitial.IsRewarded() && !VfxFormat.Interstitial.IsRewarded());
        }

        // ------------------------------------------------------------------ JSON

        [Test]
        public void JsonReadsWhatTheBridgesSend()
        {
            var o = VfxJson.Object("{\"k\":\"event\",\"time\":1763200000123,\"details\":{\"v\":1234567890123,\"ms\":12.5,\"src\":\"Google \\\"AdX\\\" \\u00e9\",\"ok\":true,\"n\":null},\"list\":[1,\"a\",[]]}");
            Assert.NotNull(o);
            Assert.AreEqual(1763200000123L, VfxJson.L(o, "time"));
            var d = VfxJson.O(o, "details");
            Assert.AreEqual(1234567890123L, d["v"]);
            Assert.AreEqual(12.5, d["ms"]);
            Assert.AreEqual("Google \"AdX\" é", d["src"]);
            Assert.AreEqual(true, d["ok"]);
            Assert.IsNull(d["n"]);
            Assert.AreEqual(3, ((List<object>)o["list"]).Count);
        }

        [Test]
        public void MalformedJsonIsNullNeverAnException()
        {
            foreach (var bad in new[] { "", "{", "{\"a\":}", "[1,2", "nul", "{\"a\":1}x", "\"\\q\"", null })
                Assert.IsNull(VfxJson.Parse(bad), bad ?? "null");
            VisionForgeX.Receive("{not json");
            Assert.That(_log.Any(l => l.Contains("Unreadable message")));
        }

        [Test]
        public void OptionsAreWrittenForTheNativeBridges()
        {
            var json = new VfxOptions { GoogleDemoAds = true, LogLevel = VfxLogLevel.Debug, TestDeviceIds = new List<string> { " ABC ", "", "DEF" }, AutoPreload = false, BaseUrl = "https://x.test" }.ToJson();
            var o = VfxJson.Object(json);
            Assert.IsTrue(VfxJson.B(o, "googleDemoAds"));
            Assert.AreEqual("debug", VfxJson.S(o, "logLevel"));
            CollectionAssert.AreEqual(new object[] { "ABC", "DEF" }, (List<object>)o["testDeviceIds"]);
            Assert.AreEqual(false, o["autoPreload"]);
            Assert.AreEqual("https://x.test", VfxJson.S(o, "baseUrl"));
            Assert.IsNull(o["settingsForTesting"]);
            Assert.IsNull(VfxJson.Object(new VfxOptions().ToJson())["logLevel"], "no level: as the platform says");
        }

        // ------------------------------------------------------------------ start

        [Test]
        public void InitializeAnswersOnceOnUnitysMainThreadOnly()
        {
            VfxStatus got = null;
            int calls = 0, thread = -1;
            VisionForgeX.Initialize("vfx_key", new VfxOptions(), st => { got = st; calls++; thread = Thread.CurrentThread.ManagedThreadId; });
            Assert.AreEqual("init:vfx_key", _b.Calls.Single());
            OffMainThread(() => MockBridge.Init(true, "", true));
            Assert.IsNull(got, "nothing reaches the game until Unity's next frame");
            Pump();
            Assert.AreEqual(1, calls);
            Assert.AreEqual(Thread.CurrentThread.ManagedThreadId, thread);
            Assert.IsTrue(got.Ready && got.FromCache);
            MockBridge.Init(false, "late");   // a second answer is not the answer
            Pump();
            Assert.AreEqual(1, calls);
            Assert.AreSame(got, VisionForgeX.Status);
        }

        [Test]
        public void IsReadyAsksTheNativeSdkOnlyOnceStarted()
        {
            _b.Ready = true;
            Assert.IsFalse(VisionForgeX.IsReady, "not started: not ready, whatever the native side says");
            VisionForgeX.Initialize("vfx_key", null, null);
            Assert.IsTrue(VisionForgeX.IsReady);
            _b.Ready = false;
            Assert.IsFalse(VisionForgeX.IsReady);
        }

        [Test]
        public void InitializingAgainStartsNothingNewButStillAnswers()
        {
            VfxStatus a = null, b = null, c = null;
            VisionForgeX.Initialize("vfx_key", null, st => a = st);
            VisionForgeX.Initialize("vfx_key", null, st => b = st);
            MockBridge.Init(true);
            Pump();
            VisionForgeX.Initialize("vfx_other", null, st => c = st);
            Pump();
            Assert.AreEqual(1, _b.Calls.Count(x => x.StartsWith("init:")));
            Assert.IsTrue(a.Ready && b.Ready && c.Ready);
        }

        [Test]
        public void TheKeyAndTestModeComeFromTheSettings()
        {
            var s = UnityEngine.ScriptableObject.CreateInstance<VfxSettings>();
            s.androidAppKey = "  vfx_from_settings ";
            s.testMode = true;
            s.logLevel = VfxSettingsLogLevel.Warn;
            VfxSettings.OverrideForTests = s;
            VisionForgeX.Initialize();
            Assert.AreEqual("vfx_from_settings", _b.LastKey);
            var o = VfxJson.Object(_b.LastOptions);
            Assert.IsTrue(VfxJson.B(o, "googleDemoAds"), "the Editor counts as a development build");
            Assert.AreEqual("warn", VfxJson.S(o, "logLevel"));
            Assert.IsFalse(s.ToOptions(false).GoogleDemoAds, "a release build never shows Google's demo ads");
            Assert.AreEqual(VfxLogLevel.Warn, s.ToOptions(true).LogLevel);
            s.logLevel = VfxSettingsLogLevel.AsThePlatformSays;
            Assert.IsNull(s.ToOptions(true).LogLevel);
        }

        [Test]
        public void NoKeyIsSaidPlainly()
        {
            VisionForgeX.Initialize("", new VfxOptions());
            Assert.That(_log.Any(l => l.Contains("No app key")));
        }

        // ------------------------------------------------------------------ full-screen ads

        [Test]
        public void ShowBeforeInitializeIsNotReadyAndAsksNothing()
        {
            VfxShowResult r = null;
            string reason = null;
            VisionForgeX.Interstitial("level_complete").Show(new VfxShowCallbacks { OnNotShown = x => reason = x, OnFinished = x => r = x });
            Pump();
            Assert.AreEqual("not_ready", reason);
            Assert.IsFalse(r.Shown);
            Assert.AreEqual("not_ready", r.Reason);
            Assert.IsFalse(_b.Calls.Any(c => c.StartsWith("show:")));
        }

        [Test]
        public void ABadPlacementNameIsUnknownAndNeverAsked()
        {
            VisionForgeX.Initialize("vfx_k");
            foreach (var name in new[] { "", "Level Complete", "level-complete", new string('a', 41), null })
            {
                VfxShowResult r = null;
                VisionForgeX.Interstitial(name).Show(x => r = x);
                Pump();
                Assert.AreEqual("unknown_placement", r.Reason, name ?? "null");
            }
            Assert.IsFalse(_b.Calls.Any(c => c.StartsWith("show:")));
            VisionForgeX.Interstitial(new string('a', 40)).Show();
            Assert.IsTrue(_b.Calls.Any(c => c.StartsWith("show:")), "40 characters is still a name");
        }

        [Test]
        public void ARewardedAdTellsTheGameEachStepInOrder()
        {
            VisionForgeX.Initialize("vfx_k");
            var steps = new List<string>();
            var changes = new List<bool>();
            VisionForgeX.OnFullScreenChanged += open => changes.Add(open);
            VfxShowResult result = null;
            VisionForgeX.Rewarded("extra_life").Show(new VfxShowCallbacks
            {
                OnShown = () => steps.Add("shown:" + VisionForgeX.IsFullScreenShowing),
                OnClicked = () => steps.Add("clicked"),
                OnReward = (t, a) => steps.Add("reward:" + t + "x" + a),
                OnDismissed = () => steps.Add("dismissed"),
                OnNotShown = r => steps.Add("notShown"),
                OnFinished = r => { steps.Add("finished"); result = r; },
            });
            Assert.AreEqual("show:1:extra_life:rewarded", _b.Calls.Last());
            int id = _b.LastShowId;
            OffMainThread(() =>
            {
                MockBridge.ShowEvent(id, "shown");
                MockBridge.ShowEvent(id, "clicked");
                MockBridge.ShowEvent(id, "reward", ",\"t\":\"coins\",\"a\":50");
                MockBridge.ShowEvent(id, "dismissed");
            });
            Assert.AreEqual(0, steps.Count);
            Pump();
            CollectionAssert.AreEqual(new[] { "shown:True", "clicked", "reward:coinsx50", "dismissed", "finished" }, steps);
            Assert.IsTrue(result.Shown && result.Rewarded && result.Reason == null);
            CollectionAssert.AreEqual(new[] { true, false }, changes);
            Assert.IsFalse(VisionForgeX.IsFullScreenShowing);
            MockBridge.ShowEvent(id, "dismissed");   // nothing after the end
            Pump();
            Assert.AreEqual(5, steps.Count);
        }

        [Test]
        public void EveryReasonReachesTheGameAsSent()
        {
            VisionForgeX.Initialize("vfx_k");
            foreach (var reason in VfxReason.All)
            {
                string heard = null;
                VfxShowResult r = null;
                VisionForgeX.Interstitial("level_complete").Show(new VfxShowCallbacks { OnNotShown = x => heard = x, OnFinished = x => r = x });
                MockBridge.ShowEvent(_b.LastShowId, "notShown", ",\"r\":\"" + reason + "\"");
                Pump();
                Assert.AreEqual(reason, heard);
                Assert.IsFalse(r.Shown);
                Assert.AreEqual(reason, r.Reason);
            }
        }

        [Test]
        public void TwoShowsAreKeptApartById()
        {
            VisionForgeX.Initialize("vfx_k");
            string a = null, b = null;
            VisionForgeX.Interstitial("one").Show(r => a = r.ToString());
            int ida = _b.LastShowId;
            VisionForgeX.AppOpen("two").Show(r => b = r.ToString());
            int idb = _b.LastShowId;
            Assert.AreNotEqual(ida, idb);
            MockBridge.ShowEvent(idb, "notShown", ",\"r\":\"showing\"");
            MockBridge.ShowEvent(ida, "shown");
            MockBridge.ShowEvent(ida, "dismissed");
            Pump();
            Assert.AreEqual("shown", a);
            Assert.AreEqual("not shown: showing", b);
            Assert.AreEqual("show:" + idb + ":two:app_open", _b.Calls.Last());
        }

        [Test]
        public void AGameCallbackThatThrowsDoesNotSilenceTheNext()
        {
            VisionForgeX.Initialize("vfx_k");
            bool finished = false;
            VisionForgeX.Interstitial("x").Show(new VfxShowCallbacks { OnNotShown = r => throw new InvalidOperationException("game bug"), OnFinished = r => finished = true });
            MockBridge.ShowEvent(_b.LastShowId, "notShown", ",\"r\":\"interval\"");
            Pump();
            Assert.IsTrue(finished);
            Assert.That(_log.Any(l => l.StartsWith("2:") && l.Contains("game bug")));
        }

        [Test]
        public void ShowByNameAsksRewardedOnlyWhenTheGameWantsAReward()
        {
            VisionForgeX.Initialize("vfx_k");
            VfxFullScreen.Show("level_complete");
            Assert.AreEqual("show:1:level_complete:interstitial", _b.Calls.Last());
            VfxFullScreen.Show("extra_life", new VfxShowCallbacks { OnReward = (t, a) => { } });
            Assert.AreEqual("show:2:extra_life:rewarded", _b.Calls.Last());
        }

        [Test]
        public void ReadyAndLoadAskTheNativeSdk()
        {
            Assert.IsFalse(VisionForgeX.Rewarded("extra_life").IsReady(), "not started");
            VisionForgeX.Initialize("vfx_k");
            _b.Loaded.Add("extra_life");
            Assert.IsTrue(VisionForgeX.Rewarded("extra_life").IsReady());
            Assert.IsFalse(VisionForgeX.Interstitial("other").IsReady());
            VisionForgeX.Interstitial("other").Load();
            Assert.AreEqual("load:other:interstitial", _b.Calls.Last());
            Assert.IsTrue(VfxFullScreen.IsReady("extra_life"));
            Assert.AreEqual("ready?:extra_life:rewarded", _b.Calls.Last(), "the format the game used for it");
        }

        // ------------------------------------------------------------------ events and revenue

        [Test]
        public void EventsAndRevenueArriveWhole()
        {
            VisionForgeX.Initialize("vfx_k");
            VfxEvent e = null;
            VfxRevenue r = null;
            VisionForgeX.OnEvent += x => e = x;
            VisionForgeX.OnRevenue += x => r = x;
            OffMainThread(() =>
            {
                VisionForgeX.Receive("{\"k\":\"event\",\"type\":\"nofill\",\"placement\":\"home_banner\",\"format\":\"banner\",\"time\":1763200000000,\"details\":{\"tier\":0,\"err\":\"3\",\"ms\":420}}");
                VisionForgeX.Receive("{\"k\":\"revenue\",\"placement\":\"extra_life\",\"format\":\"rewarded\",\"valueMicros\":9876543210,\"currency\":\"USD\",\"precision\":\"precise\",\"source\":null}");
            });
            Pump();
            Assert.AreEqual("nofill", e.Type);
            Assert.AreEqual(VfxFormat.Banner, e.Format);
            Assert.AreEqual("3", e.Detail("err"));
            Assert.AreEqual("420", e.Detail("ms"));
            Assert.AreEqual("", e.Detail("nothing"));
            Assert.AreEqual(9876543210L, r.ValueMicros);
            Assert.AreEqual(9876.54321, r.Value, 1e-9);
            Assert.IsNull(r.Source);
            Assert.AreEqual(VfxFormat.Banner, VisionForgeX.KnownPlacements["home_banner"]);
            Assert.AreEqual(1, VisionForgeX.RecentEvents.Count);
        }

        [Test]
        public void TheRecentEventsKeepTheLastHundred()
        {
            for (int i = 0; i < 130; i++)
                VisionForgeX.Receive("{\"k\":\"event\",\"type\":\"req\",\"placement\":\"p" + i + "\",\"format\":\"interstitial\",\"time\":" + i + ",\"details\":{}}");
            Pump();
            Assert.AreEqual(100, VisionForgeX.RecentEvents.Count);
            Assert.AreEqual("p129", VisionForgeX.RecentEvents.Last().Placement);
        }

        [Test]
        public void AnUnknownMessageKindIsIgnored()
        {
            VisionForgeX.Receive("{\"k\":\"something_newer\",\"x\":1}");
            Assert.AreEqual(0, VfxMainThread.Pending);
        }

        // ------------------------------------------------------------------ banners

        [Test]
        public void ABannerHearsItsOwnPlacementsEventsOnly()
        {
            VisionForgeX.Initialize("vfx_k");
            var banner = VfxBanner.Show("home_banner", VfxBannerPosition.Top);
            int id = _b.LastBannerId;
            CollectionAssert.AreEqual(new[] { "banner+:" + id + ":home_banner:1", "bannerShow:" + id }, _b.Calls.Skip(1).ToArray());
            var heard = new List<string>();
            banner.OnLoaded += () => heard.Add("loaded");
            banner.OnFailed += err => heard.Add("failed:" + err);
            banner.OnImpression += () => heard.Add("impression");
            banner.OnClicked += () => heard.Add("clicked");
            banner.OnPaid += r => heard.Add("paid:" + r.ValueMicros);
            banner.OnHeightChanged += h => heard.Add("h:" + h);
            VisionForgeX.Receive("{\"k\":\"event\",\"type\":\"fill\",\"placement\":\"home_banner\",\"format\":\"banner\",\"time\":1,\"details\":{}}");
            VisionForgeX.Receive("{\"k\":\"event\",\"type\":\"fill\",\"placement\":\"other_banner\",\"format\":\"banner\",\"time\":1,\"details\":{}}");
            VisionForgeX.Receive("{\"k\":\"event\",\"type\":\"show\",\"placement\":\"home_banner\",\"format\":\"banner\",\"time\":1,\"details\":{}}");
            VisionForgeX.Receive("{\"k\":\"banner\",\"id\":" + id + ",\"h\":168}");
            VisionForgeX.Receive("{\"k\":\"event\",\"type\":\"click\",\"placement\":\"home_banner\",\"format\":\"banner\",\"time\":1,\"details\":{}}");
            VisionForgeX.Receive("{\"k\":\"revenue\",\"placement\":\"home_banner\",\"format\":\"banner\",\"valueMicros\":1200,\"currency\":\"USD\",\"precision\":\"estimated\"}");
            VisionForgeX.Receive("{\"k\":\"event\",\"type\":\"nofill\",\"placement\":\"home_banner\",\"format\":\"banner\",\"time\":1,\"details\":{\"err\":\"3\"}}");
            Pump();
            CollectionAssert.AreEqual(new[] { "loaded", "impression", "h:168", "clicked", "paid:1200", "failed:3" }, heard);
            Assert.AreEqual(168, banner.HeightPixels);
            banner.Hide();
            Assert.AreEqual("bannerHide:" + id, _b.Calls.Last());
            Assert.IsFalse(banner.IsVisible);
            banner.Destroy();
            Assert.AreEqual("banner-:" + id, _b.Calls.Last());
            Assert.AreEqual(0, banner.HeightPixels);
            VisionForgeX.Receive("{\"k\":\"event\",\"type\":\"fill\",\"placement\":\"home_banner\",\"format\":\"banner\",\"time\":1,\"details\":{}}");
            Pump();
            Assert.AreEqual("h:0", heard.Last(), "nothing after Destroy");
            banner.Show();
            Assert.AreEqual("banner-:" + id, _b.Calls.Last(), "a destroyed banner stays destroyed");
        }

        [Test]
        public void ABannerWithABadNameAsksNothing()
        {
            VisionForgeX.Initialize("vfx_k");
            var b = VfxBanner.Show("Home Banner");
            Assert.IsTrue(b.IsDestroyed);
            Assert.IsFalse(_b.Calls.Any(c => c.StartsWith("banner")));
        }

        // ------------------------------------------------------------------ the rest of the API

        [Test]
        public void KeyValuesAreCheckedAsTheNativeSdksCheckThem()
        {
            VisionForgeX.Initialize("vfx_k");
            VisionForgeX.SetKeyValue("level", "12");
            VisionForgeX.SetKeyValue("level", null);
            VisionForgeX.SetKeyValue("vfx_pl", "x");
            VisionForgeX.SetKeyValue("Level", "x");
            VisionForgeX.SetKeyValue("a_key_that_is_far_too_long", "x");
            CollectionAssert.AreEqual(new[] { "kv:level=12", "kv:level=<null>" }, _b.Calls.Where(c => c.StartsWith("kv:")).ToArray());
            Assert.AreEqual(3, _log.Count(l => l.Contains("not used")));
        }

        [Test]
        public void PrivacyOptionsAnswerOnTheMainThread()
        {
            string err = "unset";
            VisionForgeX.ShowPrivacyOptions(e => err = e);
            Pump();
            Assert.AreEqual("the SDK has not started", err);
            VisionForgeX.Initialize("vfx_k");
            VisionForgeX.ShowPrivacyOptions(e => err = e);
            var id = int.Parse(_b.Calls.Last().Split(':')[1]);
            OffMainThread(() => VisionForgeX.Receive("{\"k\":\"consent\",\"id\":" + id + ",\"error\":null}"));
            Assert.AreEqual("the SDK has not started", err);
            Pump();
            Assert.IsNull(err);
            _b.PrivacyRequired = true;
            Assert.IsTrue(VisionForgeX.IsPrivacyOptionsRequired);
        }

        [Test]
        public void ToolsReachTheNativeSdkOnceStarted()
        {
            VisionForgeX.OpenAdInspector();
            VisionForgeX.Flush();
            Assert.AreEqual(0, _b.Calls.Count, "nothing before Initialize");
            VisionForgeX.Initialize("vfx_k");
            VisionForgeX.OpenAdInspector();
            VisionForgeX.OpenDebugPanel();
            VisionForgeX.Flush();
            VisionForgeX.SetMuted(true);
            VisionForgeX.PreloadAll();
            CollectionAssert.AreEqual(new[] { "inspector", "debugPanel", "flush", "muted:True", "preloadAll" }, _b.Calls.Skip(1).ToArray());
            Assert.AreEqual("platform", VisionForgeX.SettingsSource());
            Assert.AreEqual("1.3.0", VisionForgeX.NativeVersion);
        }

        [Test]
        public void ThePluginIsBuiltOnTheReleasedNativeSdks()
        {
            Assert.AreEqual("1.3.0", VisionForgeX.AndroidSdkVersion);
            Assert.AreEqual("1.3.0", VisionForgeX.IosSdkVersion);
            Assert.That(VisionForgeX.Version, Does.Match(@"^\d+\.\d+\.\d+$"));
        }

        [Test]
        public void OtherPlatformsAnswerAndShowNothing()
        {
            VisionForgeX.ResetForTests(new VfxNoneBridge());
            VfxStatus st = null;
            VfxShowResult r = null;
            VisionForgeX.Initialize("vfx_k", null, x => st = x);
            VisionForgeX.Interstitial("level_complete").Show(x => r = x);
            Pump();
            Assert.IsFalse(st.Ready);
            Assert.AreEqual("not_ready", r.Reason);
            Assert.AreEqual("none", VisionForgeX.SettingsSource());
        }
    }
}
