using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace VfxAds.Tests
{
    /// <summary>The Editor's stand-in: Play mode gets the same callbacks, in the same order, as a device.</summary>
    public class StandInTests
    {
        long _now;
        VfxStandIn _s;
        List<string> _sent;

        [SetUp]
        public void SetUp()
        {
            _now = 1000;
            _sent = new List<string>();
            VfxStandIn.UseCache = false;
            VfxStandIn.PlatformPlacements = null;
            _s = new VfxStandIn(() => _now, json => { _sent.Add(json); VisionForgeX.Receive(json); });
            VisionForgeX.ResetForTests(_s);
            VfxLog.Sink = (l, m) => { };
        }

        [TearDown]
        public void TearDown()
        {
            VfxStandIn.PlatformPlacements = null;
            VfxStandIn.UseCache = true;
        }

        void Advance(long ms) { _now += ms; _s.Tick(); VfxMainThread.Pump(); }

        VfxStatus Start(VfxOptions o, string key = "vfx_editor")
        {
            VfxStatus st = null;
            VisionForgeX.Initialize(key, o, x => st = x);
            Advance(VfxStandIn.StartMs);
            return st;
        }

        [Test]
        public void ItStartsAfterAMomentAndLoadsGooglesDemoPlacements()
        {
            VfxStatus st = null;
            VisionForgeX.Initialize("demo", new VfxOptions { GoogleDemoAds = true }, x => st = x);
            _s.Tick();
            VfxMainThread.Pump();
            Assert.IsNull(st, "not at once, as on a device");
            Advance(VfxStandIn.StartMs);
            Assert.IsTrue(st.Ready);
            Assert.AreEqual("testing", VisionForgeX.SettingsSource());
            Assert.IsFalse(VisionForgeX.Interstitial("interstitial").IsReady());
            Advance(VfxStandIn.LoadMs);
            Assert.IsTrue(VisionForgeX.Interstitial("interstitial").IsReady());
            Assert.IsTrue(VisionForgeX.Rewarded("rewarded").IsReady());
            Assert.IsFalse(VisionForgeX.Interstitial("banner").IsReady(), "banners are not preloaded");
            var types = VisionForgeX.RecentEvents.Where(e => e.Placement == "rewarded").Select(e => e.Type).ToArray();
            CollectionAssert.AreEqual(new[] { "req", "fill" }, types);
        }

        [Test]
        public void WithoutAKeyItSaysWhy()
        {
            var st = Start(new VfxOptions(), "");
            Assert.IsFalse(st.Ready);
            StringAssert.Contains("no app key", st.Reason);
            VfxShowResult r = null;
            VisionForgeX.Interstitial("level_complete").Show(x => r = x);
            VfxMainThread.Pump();
            Assert.AreEqual("not_ready", r.Reason);
        }

        [Test]
        public void AnInterstitialShowsClosesAndLoadsAgain()
        {
            Start(new VfxOptions { GoogleDemoAds = true });
            Advance(VfxStandIn.LoadMs);
            var steps = new List<string>();
            VisionForgeX.Interstitial("interstitial").Show(new VfxShowCallbacks
            {
                OnShown = () => steps.Add("shown"), OnClicked = () => steps.Add("clicked"), OnDismissed = () => steps.Add("dismissed"), OnFinished = r => steps.Add(r.ToString()),
            });
            VfxMainThread.Pump();
            Assert.AreEqual("interstitial", _s.Current.Placement);
            Assert.IsTrue(VisionForgeX.IsFullScreenShowing);
            VfxShowResult second = null;
            VisionForgeX.Rewarded("rewarded").Show(x => second = x);
            VfxMainThread.Pump();
            Assert.AreEqual("showing", second.Reason, "never two full-screen ads at once");
            _s.ClickCurrent();
            _s.CloseCurrent();
            VfxMainThread.Pump();
            CollectionAssert.AreEqual(new[] { "shown", "clicked", "dismissed", "shown" }, steps);
            Assert.IsNull(_s.Current);
            Assert.IsFalse(VisionForgeX.Interstitial("interstitial").IsReady(), "used: it loads again");
            Advance(VfxStandIn.LoadMs);
            Assert.IsTrue(VisionForgeX.Interstitial("interstitial").IsReady());
        }

        [Test]
        public void ARewardedAdRewardsAfterItsWaitThenCloses()
        {
            Start(new VfxOptions { GoogleDemoAds = true });
            Advance(VfxStandIn.LoadMs);
            var steps = new List<string>();
            VfxShowResult result = null;
            VisionForgeX.Rewarded("rewarded").Show(new VfxShowCallbacks { OnReward = (t, a) => steps.Add(t + ":" + a), OnDismissed = () => steps.Add("dismissed"), OnFinished = r => result = r });
            Advance(1000);
            Assert.AreEqual(4, _s.Current.SecondsLeft(_now));
            Assert.AreEqual(0, steps.Count);
            Advance(VfxStandIn.RewardMs);
            Assert.AreEqual("coins:10", steps.Single());
            _s.CloseCurrent();
            VfxMainThread.Pump();
            Assert.IsTrue(result.Shown && result.Rewarded);
        }

        [Test]
        public void ClosingARewardedAdEarlyGivesNoReward()
        {
            Start(new VfxOptions { GoogleDemoAds = true });
            Advance(VfxStandIn.LoadMs);
            VfxShowResult result = null;
            VisionForgeX.Rewarded("rewarded").Show(new VfxShowCallbacks { OnFinished = r => result = r });
            Advance(1000);
            _s.CloseCurrent();
            Advance(VfxStandIn.RewardMs);
            Assert.IsTrue(result.Shown);
            Assert.IsFalse(result.Rewarded);
        }

        [Test]
        public void UnknownAndUnloadedPlacementsAnswerAsTheSdkWould()
        {
            Start(new VfxOptions { GoogleDemoAds = true });
            VfxShowResult a = null, b = null;
            VisionForgeX.Interstitial("level_complete").Show(x => a = x);
            VisionForgeX.Interstitial("interstitial").Show(x => b = x);
            VfxMainThread.Pump();
            Assert.AreEqual("unknown_placement", a.Reason, "not one of Google's demo placements");
            Assert.AreEqual("not_ready", b.Reason, "still loading");
        }

        [Test]
        public void ThePlatformsPlacementsWhenTheKeyWasChecked()
        {
            VfxStandIn.PlatformPlacements = new Dictionary<string, string> { { "level_complete", "interstitial" }, { "extra_life", "rewarded" }, { "home_banner", "banner" } };
            Start(new VfxOptions());
            Advance(VfxStandIn.LoadMs);
            Assert.IsTrue(VisionForgeX.Interstitial("level_complete").IsReady());
            VfxShowResult r = null;
            VisionForgeX.Interstitial("menu").Show(x => r = x);
            VfxMainThread.Pump();
            Assert.AreEqual("unknown_placement", r.Reason);
            VisionForgeX.Interstitial("extra_life").Show();
            VfxMainThread.Pump();
            Assert.AreEqual("rewarded", _s.Current.Format, "the platform's format, as a device would show it");
        }

        [Test]
        public void AnyPlacementNameWhenThePlacementsAreNotKnown()
        {
            Start(new VfxOptions());
            VfxShowResult r = null;
            VisionForgeX.Interstitial("level_complete").Show(x => r = x);
            VfxMainThread.Pump();
            Assert.AreEqual("not_ready", r.Reason, "first asked: it starts loading");
            Advance(VfxStandIn.LoadMs);
            VisionForgeX.Interstitial("level_complete").Show();
            VfxMainThread.Pump();
            Assert.IsNotNull(_s.Current);
        }

        [Test]
        public void ABannerLoadsOnceShownAndReportsItsHeight()
        {
            Start(new VfxOptions { GoogleDemoAds = true });
            _s.BannerHeightPixels = () => 147;
            var b = VfxBanner.Create("banner");
            bool loaded = false;
            b.OnLoaded += () => loaded = true;
            Advance(VfxStandIn.LoadMs * 2);
            Assert.IsFalse(loaded, "hidden: it never loads");
            b.Show();
            Advance(10);
            Advance(VfxStandIn.LoadMs);
            Assert.IsTrue(loaded);
            Assert.AreEqual(147, b.HeightPixels);
            b.Hide();
            VfxMainThread.Pump();
            Assert.AreEqual(0, b.HeightPixels);
            b.Destroy();
            Assert.AreEqual(0, _s.Banners.Count());
        }

        [Test]
        public void ConsentAndToolsAnswerInTheEditor()
        {
            Start(new VfxOptions { GoogleDemoAds = true });
            string err = "unset";
            VisionForgeX.ShowPrivacyOptions(e => err = e);
            VfxMainThread.Pump();
            Assert.IsNull(err);
            Assert.IsFalse(VisionForgeX.IsPrivacyOptionsRequired);
            VisionForgeX.SetKeyValue("level", "3");
            Assert.AreEqual("3", _s.KeyValues["level"]);
            Assert.AreEqual("editor", VisionForgeX.Platform);
        }

        [Test]
        public void TheEditorNeverReportsRevenue()
        {
            Start(new VfxOptions { GoogleDemoAds = true });
            Advance(VfxStandIn.LoadMs);
            VisionForgeX.Interstitial("interstitial").Show();
            _s.CloseCurrent();
            Advance(VfxStandIn.LoadMs);
            Assert.IsFalse(_sent.Any(m => m.Contains("\"k\":\"revenue\"") || m.Contains("\"type\":\"paid\"")));
        }
    }
}
