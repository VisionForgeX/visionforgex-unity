using System.Collections.Generic;
using System.Linq;
using System.Xml;
using NUnit.Framework;
using VfxAds.EditorTools;

namespace VfxAds.Tests
{
    /// <summary>What the plugin writes into a game's build, and the project check.</summary>
    public class EditorToolsTests
    {
        // ------------------------------------------------------------------ the External Dependency Manager's file

        [Test]
        public void TheDependencyFileNamesTheChosenAndroidSdkFromJitPack()
        {
            var next = VfxNativeSetup.DependenciesXml(VfxAndroidEngine.NextGen, "1.3.0", "1.3.0");
            var gma = VfxNativeSetup.DependenciesXml(VfxAndroidEngine.GoogleMobileAds, "1.3.0", "1.3.0");
            var doc = new XmlDocument();
            doc.LoadXml(next);
            var spec = doc.SelectSingleNode("/dependencies/androidPackages/androidPackage/@spec").Value;
            Assert.AreEqual("com.github.VisionForgeX.visionforgex-android-sdk:vfx-ads-nextgen:1.3.0", spec);
            Assert.AreEqual("https://jitpack.io", doc.SelectSingleNode("/dependencies/androidPackages/androidPackage/repositories/repository").InnerText);
            StringAssert.Contains("visionforgex-android-sdk:vfx-ads:1.3.0\"", gma);
            StringAssert.DoesNotContain("nextgen", gma);
            var pods = doc.SelectNodes("/dependencies/iosPods/iosPod").Cast<XmlElement>().Select(e => e.GetAttribute("name") + " " + e.GetAttribute("version")).ToArray();
            CollectionAssert.AreEqual(new[] { "VisionForgeX 1.3.0", "Google-Mobile-Ads-SDK ~> 13.12", "GoogleUserMessagingPlatform ~> 2.7" }, pods);
        }

        // ------------------------------------------------------------------ the Podfile

        const string EdmPodfile = "source 'https://cdn.cocoapods.org/'\nplatform :ios, '13.0'\n\ntarget 'UnityFramework' do\n  pod 'Google-Mobile-Ads-SDK', '~> 13.12'\n  pod 'VisionForgeX', '1.3.0'\nend\ntarget 'Unity-iPhone' do\nend\nuse_frameworks! :linkage => :static\n";

        [Test]
        public void ThePodfileTakesThePodFromGitHubAtItsTag()
        {
            var p = VfxNativeSetup.PatchPodfile(EdmPodfile, "1.3.0");
            StringAssert.Contains("  pod 'VisionForgeX', :git => 'https://github.com/VisionForgeX/visionforgex-ios-sdk.git', :tag => '1.3.0'\n", p);
            StringAssert.DoesNotContain("pod 'VisionForgeX', '1.3.0'", p);
            Assert.AreEqual(1, p.Split('\n').Count(l => l.Contains("'VisionForgeX'")));
            Assert.AreEqual(p, VfxNativeSetup.PatchPodfile(p, "1.3.0"), "a second run changes nothing");
            StringAssert.Contains("pod 'Google-Mobile-Ads-SDK', '~> 13.12'", p);
        }

        [Test]
        public void WithoutTheResolversLineItGoesIntoTheFrameworkTarget()
        {
            var without = EdmPodfile.Replace("  pod 'VisionForgeX', '1.3.0'\n", "");
            var p = VfxNativeSetup.PatchPodfile(without, "1.3.0");
            var lines = p.Split('\n').ToList();
            int t = lines.IndexOf("target 'UnityFramework' do");
            Assert.AreEqual("  pod 'VisionForgeX', :git => 'https://github.com/VisionForgeX/visionforgex-ios-sdk.git', :tag => '1.3.0'", lines[t + 1]);
            var bare = VfxNativeSetup.PatchPodfile("platform :ios, '13.0'\n", "1.3.0");
            StringAssert.Contains("target 'UnityFramework' do\n  pod 'VisionForgeX', :git", bare);
            StringAssert.EndsWith("end\n", bare);
        }

        // ------------------------------------------------------------------ the Android manifest

        const string Manifest = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"com.unity3d.player\">\n  <application android:extractNativeLibs=\"true\">\n    <meta-data android:name=\"unity.splash-mode\" android:value=\"0\" />\n  </application>\n</manifest>";

        static string AppIdIn(string xml)
        {
            var d = new XmlDocument();
            d.LoadXml(xml);
            var ns = new XmlNamespaceManager(d.NameTable);
            ns.AddNamespace("a", "http://schemas.android.com/apk/res/android");
            var nodes = d.SelectNodes("/manifest/application/meta-data[@a:name='com.google.android.gms.ads.APPLICATION_ID']", ns);
            Assert.AreEqual(1, nodes.Count, "exactly one App ID");
            return ((XmlElement)nodes[0]).GetAttribute("value", "http://schemas.android.com/apk/res/android");
        }

        [Test]
        public void TheAppIdGoesIntoTheManifestOnce()
        {
            var once = VfxNativeSetup.PatchManifest(Manifest, "ca-app-pub-1234567890123456~1234567890");
            Assert.AreEqual("ca-app-pub-1234567890123456~1234567890", AppIdIn(once));
            StringAssert.Contains("unity.splash-mode", once);
            StringAssert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", once);
            var again = VfxNativeSetup.PatchManifest(once, "ca-app-pub-9999999999999999~9999999999");
            Assert.AreEqual("ca-app-pub-9999999999999999~9999999999", AppIdIn(again), "replaced, not added twice");
            Assert.IsNull(VfxNativeSetup.PatchManifest("<manifest/>", "x"));
            Assert.IsNull(VfxNativeSetup.PatchManifest("not xml", "x"));
        }

        [Test]
        public void GooglesAppIdShape()
        {
            Assert.IsTrue(VfxNativeSetup.LooksLikeAppId("ca-app-pub-3940256099942544~3347511713"));
            Assert.IsTrue(VfxNativeSetup.LooksLikeAppId(" ca-app-pub-3940256099942544~1458002511 "));
            Assert.IsFalse(VfxNativeSetup.LooksLikeAppId("ca-app-pub-3940256099942544/1033173712"), "a unit, not an App ID");
            Assert.IsFalse(VfxNativeSetup.LooksLikeAppId("/21775744923/example/interstitial"));
            Assert.IsFalse(VfxNativeSetup.LooksLikeAppId(""));
            Assert.IsTrue(VfxNativeSetup.IsGoogleSample(VfxNativeSetup.GoogleSampleAndroidAppId) && VfxNativeSetup.IsGoogleSample(VfxNativeSetup.GoogleSampleIosAppId));
        }

        // ------------------------------------------------------------------ Info.plist

        [Test]
        public void GooglesSkAdNetworkListIsWholeAndAddedOnlyWhereMissing()
        {
            Assert.AreEqual(50, VfxNativeSetup.SkAdNetworkIds.Count);
            Assert.AreEqual(50, VfxNativeSetup.SkAdNetworkIds.Distinct().Count());
            Assert.AreEqual("cstr6suwn9", VfxNativeSetup.SkAdNetworkIds[0], "Google's own first");
            Assert.That(VfxNativeSetup.SkAdNetworkIds.All(i => System.Text.RegularExpressions.Regex.IsMatch(i, "^[a-z0-9]{10}$")));
            var add = VfxNativeSetup.MissingSkAdNetworkIds(new[] { "CSTR6SUWN9.skadnetwork", "4fzdc2evr5.skadnetwork", "somebuyer.skadnetwork" });
            Assert.AreEqual(48, add.Count);
            Assert.IsFalse(add.Contains("cstr6suwn9.skadnetwork"));
            Assert.AreEqual(0, VfxNativeSetup.MissingSkAdNetworkIds(VfxNativeSetup.SkAdNetworkIds.Select(i => i + ".skadnetwork")).Count);
        }

        // ------------------------------------------------------------------ the project check

        static VfxProjectFacts Good(bool android = true, bool ios = true) => new VfxProjectFacts
        {
            ForAndroid = android, ForIos = ios, AndroidKey = "vfx_a", IosKey = "vfx_i",
            AndroidAppId = "ca-app-pub-1234567890123456~1234567890", IosAppId = "ca-app-pub-1234567890123456~0987654321",
            AndroidMinSdk = 24, AndroidTargetSdk = 35, IosMinVersion = "13.0",
        };

        static string[] Codes(VfxProjectFacts f, VfxIssueLevel? level = null) =>
            VfxValidation.Check(f).Where(i => level == null || i.Level == level).Select(i => i.Code).ToArray();

        [Test]
        public void AProjectSetUpRightHasNothingToFix()
        {
            CollectionAssert.IsEmpty(VfxValidation.Check(Good()));
        }

        [Test]
        public void NoSettingsIsTheOnlyThingSaid()
        {
            var i = VfxValidation.Check(new VfxProjectFacts { SettingsExist = false, ForAndroid = true });
            Assert.AreEqual("settings", i.Single().Code);
            Assert.AreEqual(VfxFix.CreateSettings, i.Single().Fix);
        }

        [Test]
        public void AMissingOrWrongAppIdStopsTheBuild()
        {
            var f = Good(); f.AndroidAppId = "";
            Assert.Contains("android-app-id", Codes(f, VfxIssueLevel.Error));
            f = Good(); f.IosAppId = "ca-app-pub-1234567890123456/1234567890";
            var e = VfxValidation.Check(f).Single(x => x.Code == "ios-app-id");
            Assert.AreEqual(VfxIssueLevel.Error, e.Level);
            StringAssert.Contains("not one", e.Message);
            f = Good(true, false); f.IosAppId = "";
            CollectionAssert.IsEmpty(VfxValidation.Check(f), "an Android build does not need the iOS App ID");
        }

        [Test]
        public void GooglesSampleAppIdIsFineForTestingOnly()
        {
            var f = Good(); f.AndroidAppId = VfxNativeSetup.GoogleSampleAndroidAppId;
            f.DevelopmentBuild = true;
            CollectionAssert.IsEmpty(VfxValidation.Check(f));
            f.DevelopmentBuild = false;
            Assert.Contains("android-sample-app-id", Codes(f, VfxIssueLevel.Warning));
        }

        [Test]
        public void TheKeyIsWarnedAboutUnlessDemoAdsStandIn()
        {
            var f = Good(); f.AndroidKey = "";
            Assert.Contains("android-key", Codes(f, VfxIssueLevel.Warning));
            f.TestMode = true; f.DevelopmentBuild = true;
            Assert.IsFalse(Codes(f).Contains("android-key"));
            f = Good(); f.IosKey = "abc";
            StringAssert.Contains("vfx_", VfxValidation.Check(f).Single(x => x.Code == "ios-key").Message);
        }

        [Test]
        public void AndroidApiLevels()
        {
            var f = Good(); f.AndroidMinSdk = 22;
            var i = VfxValidation.Check(f).Single(x => x.Code == "android-min-sdk");
            Assert.AreEqual(VfxFix.SetAndroidMinSdk24, i.Fix);
            f = Good(); f.AndroidTargetSdk = 34;
            Assert.AreEqual(VfxFix.SetAndroidTargetSdk35, VfxValidation.Check(f).Single(x => x.Code == "android-target-sdk" && x.Level == VfxIssueLevel.Error).Fix);
            f.Engine = VfxAndroidEngine.GoogleMobileAds;
            CollectionAssert.IsEmpty(VfxValidation.Check(f), "API 35 is Next-Gen's need");
            f = Good(); f.AndroidTargetSdk = 0;
            Assert.Contains("android-target-sdk", Codes(f, VfxIssueLevel.Info));
        }

        [Test]
        public void IosVersion()
        {
            var f = Good(); f.IosMinVersion = "12.4";
            Assert.AreEqual(VfxFix.SetIosMinVersion13, VfxValidation.Check(f).Single(x => x.Code == "ios-min-version").Fix);
            Assert.IsTrue(VfxValidation.VersionBelow("12", 13, 0));
            Assert.IsFalse(VfxValidation.VersionBelow("13.0", 13, 0));
            Assert.IsFalse(VfxValidation.VersionBelow("15.2", 13, 0));
            Assert.IsFalse(VfxValidation.VersionBelow("", 13, 0));
        }

        [Test]
        public void TwoGoogleSdksOrNoDependencyManagerStopTheBuild()
        {
            var f = Good(); f.GoogleUnityPluginPresent = true;
            Assert.Contains("google-plugin", Codes(f, VfxIssueLevel.Error));
            f = Good(); f.OtherGoogleAndroidAds = new List<string> { "Assets/Plugins/Android/play-services-ads-24.0.0.aar" };
            Assert.Contains("android-two-google-sdks", Codes(f, VfxIssueLevel.Error));
            f = Good(); f.EdmPresent = false;
            Assert.AreEqual(VfxFix.InstallEdm, VfxValidation.Check(f).Single(x => x.Code == "edm").Fix);
            Assert.IsTrue(VfxValidation.HasErrors(VfxValidation.Check(f)));
            Assert.IsFalse(VfxValidation.HasErrors(VfxValidation.Check(Good())));
        }

        [Test]
        public void TestModeIsSaidForWhatItIs()
        {
            var f = Good(); f.TestMode = true; f.DevelopmentBuild = true;
            StringAssert.Contains("development builds", VfxValidation.Check(f).Single(x => x.Code == "test-mode").Message);
            f.DevelopmentBuild = false;
            StringAssert.Contains("ignored", VfxValidation.Check(f).Single(x => x.Code == "test-mode").Message);
            Assert.IsFalse(VfxValidation.HasErrors(VfxValidation.Check(f)));
        }
    }
}
