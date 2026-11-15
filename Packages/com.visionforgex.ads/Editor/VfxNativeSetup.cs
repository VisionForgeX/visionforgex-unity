using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace VfxAds.EditorTools
{
    /// <summary>
    /// The text the plugin writes into a game's build: the External Dependency Manager's file, the Podfile line, the
    /// Android manifest's App ID, Google's SKAdNetwork list. Pure functions, so the tests check them without Unity.
    /// </summary>
    internal static class VfxNativeSetup
    {
        public const string AndroidGroup = "com.github.VisionForgeX.visionforgex-android-sdk";
        public const string JitPack = "https://jitpack.io";
        public const string IosRepo = "https://github.com/VisionForgeX/visionforgex-ios-sdk.git";
        public const string GoogleAppIdKey = "com.google.android.gms.ads.APPLICATION_ID";

        /// <summary>The artifact for an engine: vfx-ads-nextgen (Next-Gen) or vfx-ads (Google Mobile Ads 25.x).</summary>
        public static string AndroidArtifact(VfxAndroidEngine engine) => engine == VfxAndroidEngine.GoogleMobileAds ? "vfx-ads" : "vfx-ads-nextgen";

        /// <summary>
        /// The External Dependency Manager's file (Assets/VisionForgeX/Editor/VisionForgeXDependencies.xml): the Android
        /// SDK from JitPack, and on iOS the VisionForgeX pod (taken from GitHub by <see cref="PatchPodfile"/>, since it is
        /// not on CocoaPods' catalogue yet) with Google's two pods named so the resolver writes a Podfile.
        /// </summary>
        public static string DependenciesXml(VfxAndroidEngine engine, string androidVersion, string iosVersion)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            sb.Append("<!-- Written by VisionForgeX (Assets → VisionForgeX → Settings). Do not edit: change the settings instead. -->\n");
            sb.Append("<dependencies>\n");
            sb.Append("  <androidPackages>\n");
            sb.Append("    <androidPackage spec=\"").Append(AndroidGroup).Append(':').Append(AndroidArtifact(engine)).Append(':').Append(androidVersion).Append("\">\n");
            sb.Append("      <repositories>\n        <repository>").Append(JitPack).Append("</repository>\n      </repositories>\n");
            sb.Append("    </androidPackage>\n");
            sb.Append("  </androidPackages>\n");
            sb.Append("  <iosPods>\n");
            sb.Append("    <iosPod name=\"VisionForgeX\" version=\"").Append(iosVersion).Append("\" minTargetSdk=\"13.0\" addToAllTargets=\"false\" />\n");
            sb.Append("    <iosPod name=\"Google-Mobile-Ads-SDK\" version=\"~&gt; 13.12\" minTargetSdk=\"13.0\" addToAllTargets=\"false\" />\n");
            sb.Append("    <iosPod name=\"GoogleUserMessagingPlatform\" version=\"~&gt; 2.7\" minTargetSdk=\"13.0\" addToAllTargets=\"false\" />\n");
            sb.Append("  </iosPods>\n");
            sb.Append("</dependencies>\n");
            return sb.ToString();
        }

        static readonly Regex OurPod = new Regex(@"^([ \t]*)pod[ \t]+['""]VisionForgeX['""][^\n]*$", RegexOptions.Multiline);
        static readonly Regex FrameworkTarget = new Regex(@"^[ \t]*target[ \t]+['""]UnityFramework['""][ \t]+do[ \t]*\r?$", RegexOptions.Multiline);

        /// <summary>The Podfile line that takes the iOS SDK from its GitHub tag.</summary>
        public static string PodLine(string iosVersion) => "pod 'VisionForgeX', :git => '" + IosRepo + "', :tag => '" + iosVersion + "'";

        /// <summary>
        /// Makes the Podfile take the VisionForgeX pod from GitHub at its tag: the resolver's own line for it is replaced;
        /// with none, the line goes first into the UnityFramework target (or a target of its own when there is none).
        /// Running it twice changes nothing more.
        /// </summary>
        public static string PatchPodfile(string podfile, string iosVersion)
        {
            podfile = podfile ?? "";
            var line = PodLine(iosVersion);
            if (OurPod.IsMatch(podfile)) return OurPod.Replace(podfile, m => m.Groups[1].Value + line);
            var t = FrameworkTarget.Match(podfile);
            if (t.Success)
            {
                int at = t.Index + t.Length;
                if (at < podfile.Length && podfile[at] == '\n') at++;
                return podfile.Substring(0, at) + "  " + line + "\n" + podfile.Substring(at);
            }
            return podfile.TrimEnd() + "\n\ntarget 'UnityFramework' do\n  " + line + "\nend\n";
        }

        /// <summary>
        /// Puts Google's App ID into the Android manifest's &lt;application&gt; (replacing an older value; nothing else
        /// changes). Null when the text is not a manifest with an application element.
        /// </summary>
        public static string PatchManifest(string manifestXml, string appId)
        {
            var doc = new XmlDocument { PreserveWhitespace = true };
            try { doc.LoadXml(manifestXml); }
            catch (XmlException) { return null; }
            const string ns = "http://schemas.android.com/apk/res/android";
            var app = doc.SelectSingleNode("/manifest/application") as XmlElement;
            if (app == null) return null;
            XmlElement meta = null;
            foreach (XmlNode n in app.ChildNodes)
                if (n is XmlElement e && e.Name == "meta-data" && e.GetAttribute("name", ns) == GoogleAppIdKey) { meta = e; break; }
            if (meta == null)
            {
                meta = doc.CreateElement("meta-data");
                var name = doc.CreateAttribute("android", "name", ns);
                name.Value = GoogleAppIdKey;
                meta.Attributes.Append(name);
                app.AppendChild(meta);
            }
            meta.SetAttribute("value", ns, appId);
            var sb = new StringBuilder();
            using (var w = XmlWriter.Create(sb, new XmlWriterSettings { OmitXmlDeclaration = manifestXml.TrimStart().StartsWith("<?xml", StringComparison.Ordinal) == false, Indent = false }))
                doc.Save(w);
            return sb.ToString().Replace("encoding=\"utf-16\"", "encoding=\"utf-8\"");
        }

        static readonly Regex AppId = new Regex(@"^ca-app-pub-\d{16}~\d{10}$");

        /// <summary>The shape of Google's App IDs: ca-app-pub-################~##########.</summary>
        public static bool LooksLikeAppId(string s) => s != null && AppId.IsMatch(s.Trim());

        /// <summary>Google's sample App IDs (test ads only; a real game needs its own).</summary>
        public const string GoogleSampleAndroidAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string GoogleSampleIosAppId = "ca-app-pub-3940256099942544~1458002511";

        public static bool IsGoogleSample(string s) => s != null && s.Trim().StartsWith("ca-app-pub-3940256099942544~", StringComparison.Ordinal);

        /// <summary>
        /// Google's SKAdNetwork identifiers for buyers (Google's Ad Manager iOS quick-start, as of Nov 2026): written into
        /// Info.plist so these buyers can bid. Google's list changes now and then; a newer plugin carries the newer list.
        /// </summary>
        public static readonly IReadOnlyList<string> SkAdNetworkIds = new[]
        {
            "cstr6suwn9", "4fzdc2evr5", "2fnua5tdw4", "ydx93a7ass", "p78axxw29g", "v72qych5uu", "ludvb6z3bs", "cp8zw746q7",
            "3sh42y64q3", "c6k4g5qg8m", "s39g8k73mm", "wg4vff78zm", "3qy4746246", "f38h382jlk", "hs6bdukanm", "mlmmfzh3r3",
            "v4nxqhlyqp", "wzmmz9fp6w", "su67r6k2v3", "yclnxrl5pm", "t38b2kh725", "7ug5zh24hu", "gta9lk7p23", "vutu7akeur",
            "y5ghdn5j9k", "v9wttpbfk9", "n38lu8286q", "47vhws6wlr", "kbd757ywx3", "9t245vhmpl", "a2p9lx4jpn", "22mmun2rn5",
            "44jx6755aq", "k674qkevps", "4468km3ulz", "2u9pt9hc89", "8s468mfl3y", "klf5c3l5u5", "ppxm28t8ap", "kbmxgpxpgc",
            "uw77j35x4d", "578prtvx9j", "4dzt52r2t5", "tl55sbb4fm", "c3frkrj4fj", "e5fvkxwrpn", "8c4e2ghe7u", "3rd42ekr43",
            "97r2b46745", "3qcr597p9d",
        };

        /// <summary>The identifiers to add to an Info.plist that already has <paramref name="existing"/> (any case), in Google's order.</summary>
        public static List<string> MissingSkAdNetworkIds(IEnumerable<string> existing)
        {
            var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in existing ?? new string[0]) if (e != null) have.Add(e.Trim());
            var add = new List<string>();
            foreach (var id in SkAdNetworkIds)
            {
                var full = id + ".skadnetwork";
                if (!have.Contains(full)) { add.Add(full); have.Add(full); }
            }
            return add;
        }
    }
}
