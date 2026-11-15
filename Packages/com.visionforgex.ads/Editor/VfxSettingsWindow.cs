using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace VfxAds.EditorTools
{
    /// <summary>
    /// Assets → VisionForgeX → Settings: the game's keys and Google App IDs, which Google SDK Android uses, test mode, and
    /// a check of the project with a fix for each problem. "Check the key" asks the platform (read only) which
    /// placements the app has, so the Editor's stand-in knows them.
    /// </summary>
    internal sealed class VfxSettingsWindow : EditorWindow
    {
        const string GuideUrl = "https://github.com/VisionForgeX/visionforgex-unity#readme";
        const string PlatformUrl = "https://apps.visionforgex.com";

        VfxSettings _s;
        SerializedObject _so;
        Vector2 _scroll;
        List<VfxIssue> _issues = new List<VfxIssue>();
        double _checkedAt;
        UnityWebRequest _request;
        bool _requestIos;
        string _keyResult = "";
        MessageType _keyResultType = MessageType.None;
        static GUIStyle _h1, _h2, _note;

        [MenuItem("Assets/VisionForgeX/Settings", false, 2000)]
        public static void Open()
        {
            var w = GetWindow<VfxSettingsWindow>(false, "VisionForgeX", true);
            w.minSize = new Vector2(440, 520);
            w.Show();
        }

        [MenuItem("Assets/VisionForgeX/Check the project", false, 2001)]
        static void CheckMenu()
        {
            var issues = VfxValidation.Check(VfxProjectState.ReadForActiveTarget());
            if (issues.Count == 0) Debug.Log("[VisionForgeX] The project check found nothing to fix.");
            foreach (var i in issues)
            {
                var m = "[VisionForgeX] " + i.Message;
                if (i.Level == VfxIssueLevel.Error) Debug.LogError(m); else if (i.Level == VfxIssueLevel.Warning) Debug.LogWarning(m); else Debug.Log(m);
            }
            Open();
        }

        [MenuItem("Assets/VisionForgeX/Integration guide", false, 2020)]
        static void Guide() => Application.OpenURL(GuideUrl);

        [MenuItem("Assets/VisionForgeX/Open the debug panel (Play mode)", false, 2021)]
        static void DebugPanel() => VisionForgeX.OpenDebugPanel();

        [MenuItem("Assets/VisionForgeX/Open the debug panel (Play mode)", true)]
        static bool DebugPanelValid() => Application.isPlaying;

        void OnEnable()
        {
            _s = VfxSettingsStore.Load();
            _so = _s != null ? new SerializedObject(_s) : null;
            Recheck();
        }

        void OnDisable()
        {
            _request?.Dispose();
            _request = null;
        }

        void Recheck()
        {
            _issues = VfxValidation.Check(VfxProjectState.ReadForActiveTarget());
            _checkedAt = EditorApplication.timeSinceStartup;
        }

        static void Styles()
        {
            if (_h1 != null) return;
            _h1 = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, richText = true };
            _h2 = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, margin = new RectOffset(0, 0, 14, 4) };
            _note = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { richText = true };
        }

        void OnGUI()
        {
            Styles();
            Header();
            if (_s == null)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox("This project has no VisionForgeX settings yet. They hold the game's keys and Google App IDs, and are kept at " + VfxSettingsStore.AssetPath + ".", MessageType.Info);
                if (GUILayout.Button("Make the settings", GUILayout.Height(30)))
                {
                    _s = VfxSettingsStore.LoadOrCreate();
                    _so = new SerializedObject(_s);
                    Recheck();
                }
                return;
            }
            if (EditorApplication.timeSinceStartup - _checkedAt > 2) Recheck();

            _so.Update();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUI.BeginChangeCheck();

            Section("The game on VisionForgeX", "Each store listing is its own app on apps.visionforgex.com, with its own key and Google App ID: both are on its page (VisionForgeX SDK panel, and the code tab).");
            Field("androidAppKey", "Android app key");
            Field("iosAppKey", "iOS app key");
            Field("androidAdManagerAppId", "Android Google App ID");
            Field("iosAdManagerAppId", "iOS Google App ID");
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Use Google's sample App IDs", "For testing only: a release needs the app's own."), EditorStyles.miniButton))
            {
                Undo.RecordObject(_s, "Google's sample App IDs");
                if (string.IsNullOrWhiteSpace(_s.androidAdManagerAppId)) _s.androidAdManagerAppId = VfxNativeSetup.GoogleSampleAndroidAppId;
                if (string.IsNullOrWhiteSpace(_s.iosAdManagerAppId)) _s.iosAdManagerAppId = VfxNativeSetup.GoogleSampleIosAppId;
                EditorUtility.SetDirty(_s);
                _so.Update();
            }
            using (new EditorGUI.DisabledScope(_request != null))
            {
                if (GUILayout.Button(new GUIContent(_request != null ? "Checking…" : "Check the key", "Asks apps.visionforgex.com which placements this app has (nothing is counted)."), EditorStyles.miniButton))
                    CheckKey(EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS);
            }
            EditorGUILayout.EndHorizontal();
            if (_keyResult != "") EditorGUILayout.HelpBox(_keyResult, _keyResultType);

            Section("Google's SDK on Android", "Both run the same game code. Next-Gen is Google's preferred Android SDK (compileSdk 35); Google Mobile Ads 25.x is retired by Google on 30 Jun 2028.");
            Field("androidEngine", "Android uses");

            Section("Testing", "Test mode shows Google's demo ads in the Editor and in development builds, with no account and nothing sent. A release build ignores it and always uses the platform's settings.");
            Field("testMode", "Test mode");
            Field("testDeviceIds", "Google test devices");
            Field("logLevel", "Device log");

            Section("In the game", "");
            Field("initializeOnStart", "Start when the game starts");
            Field("pauseAudioDuringAds", "Pause audio during ads");

            Section("iOS", "");
            Field("iosAddSkAdNetworkIds", "Google's SKAdNetwork IDs");
            Field("iosTrackingUsageDescription", "Tracking question (ATT)");

            if (EditorGUI.EndChangeCheck())
            {
                _so.ApplyModifiedProperties();
                VfxSettingsStore.Save(_s);
                Recheck();
            }

            Section("Project check", "For the " + (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS ? "iOS" : EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ? "Android" : "Android and iOS") + " build. Every build runs it too: an error stops the build.");
            if (_issues.Count == 0) EditorGUILayout.HelpBox("Nothing to fix.", MessageType.Info);
            foreach (var i in _issues.ToList())
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(i.Message, i.Level == VfxIssueLevel.Error ? MessageType.Error : i.Level == VfxIssueLevel.Warning ? MessageType.Warning : MessageType.Info);
                if (i.Fix != VfxFix.None && i.Fix != VfxFix.OpenSettings && GUILayout.Button(VfxProjectState.FixLabel(i.Fix), GUILayout.Width(96), GUILayout.Height(38)))
                {
                    VfxProjectState.Apply(i.Fix);
                    Recheck();
                }
                EditorGUILayout.EndHorizontal();
            }

            Section("Help", "");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Integration guide")) Application.OpenURL(GuideUrl);
            if (GUILayout.Button("apps.visionforgex.com")) Application.OpenURL(PlatformUrl);
            if (GUILayout.Button("Email support")) Application.OpenURL("mailto:support@visionforgex.com");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        void Header()
        {
            var r = GUILayoutUtility.GetRect(0, 64, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color32(0x1d, 0x1a, 0x2e, 0xff));
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 3, r.width * 0.62f, 3), new Color32(0x8b, 0x5c, 0xf6, 0xff));
            EditorGUI.DrawRect(new Rect(r.x + r.width * 0.62f, r.yMax - 3, r.width * 0.38f, 3), new Color32(0xfb, 0xbf, 0x24, 0xff));
            var t = new GUIStyle(_h1);
            t.normal.textColor = Color.white;
            GUI.Label(new Rect(r.x + 16, r.y + 10, r.width - 32, 26), "VisionForge<color=#fbbf24>X</color>", t);
            var sub = new GUIStyle(EditorStyles.miniLabel);
            sub.normal.textColor = new Color32(0xc4, 0xbd, 0xe0, 0xff);
            GUI.Label(new Rect(r.x + 16, r.y + 36, r.width - 32, 18), "Plugin " + VisionForgeX.Version + " · Android SDK " + VisionForgeX.AndroidSdkVersion + " · iOS SDK " + VisionForgeX.IosSdkVersion + " · every ad decision is made on the platform", sub);
        }

        static void Section(string title, string note)
        {
            GUILayout.Label(title, _h2);
            if (note != "") GUILayout.Label(note, _note);
        }

        void Field(string prop, string label)
        {
            var p = _so.FindProperty(prop);
            if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label, p.tooltip), true);
        }

        // ------------------------------------------------------------------ the key, with the platform

        void CheckKey(bool ios)
        {
            var key = ((ios ? _s.iosAppKey : _s.androidAppKey) ?? "").Trim();
            if (key == "") { _keyResult = "Paste the " + (ios ? "iOS" : "Android") + " app key first."; _keyResultType = MessageType.Warning; return; }
            var url = PlatformUrl + "/sdk/v1/config.php?k=" + UnityWebRequest.EscapeURL(key) + "&sv=unity-" + VisionForgeX.Version + "&p=" + (ios ? "ios" : "android")
                      + "&b=" + UnityWebRequest.EscapeURL(PlayerSettings.applicationIdentifier) + "&av=" + UnityWebRequest.EscapeURL(PlayerSettings.bundleVersion);
            _requestIos = ios;
            _request = UnityWebRequest.Get(url);
            _request.timeout = 15;
            _request.SendWebRequest();
            EditorApplication.update += PollKey;
        }

        void PollKey()
        {
            if (_request == null) { EditorApplication.update -= PollKey; return; }
            if (!_request.isDone) return;
            EditorApplication.update -= PollKey;
            var r = _request;
            _request = null;
            var name = _requestIos ? "iOS" : "Android";
            if (r.responseCode == 404) { _keyResult = "The platform does not know this " + name + " key: copy it again from the app's page."; _keyResultType = MessageType.Error; }
            else if (r.result != UnityWebRequest.Result.Success) { _keyResult = "apps.visionforgex.com could not be reached (" + r.error + "). Try again."; _keyResultType = MessageType.Warning; }
            else
            {
                var o = VfxJson.Object(r.downloadHandler.text);
                var app = VfxJson.O(o, "app");
                var pl = VfxJson.O(o, "placements");
                if (o == null || pl == null) { _keyResult = "The platform's answer could not be read."; _keyResultType = MessageType.Warning; }
                else
                {
                    var cache = new Dictionary<string, string>();
                    var lines = new List<string>();
                    foreach (var kv in pl)
                    {
                        var p = kv.Value as Dictionary<string, object>;
                        var f = VfxJson.S(p, "format");
                        cache[kv.Key] = f;
                        lines.Add(kv.Key + " (" + f.Replace('_', ' ') + (p != null && p.TryGetValue("on", out var on) && on is bool b && !b ? ", paused" : "") + ")");
                    }
                    WriteCache(_requestIos, cache);
                    VfxStandIn.PlatformPlacements = null;   // read again at the next Play
                    bool enabled = VfxJson.B(app, "enabled"), test = VfxJson.B(app, "test");
                    _keyResult = "The " + name + " key works. " + (enabled ? "" : "The app is paused on the platform" + (VfxJson.S(app, "reason") != "" ? " (" + VfxJson.S(app, "reason") + ")" : "") + ". ")
                                 + (test ? "Test ads are on for it. " : "") + (lines.Count == 0 ? "It has no placements yet: add them on its page." : lines.Count + " placements: " + string.Join(", ", lines) + ". The Editor's stand-in now knows them.");
                    _keyResultType = enabled && lines.Count > 0 ? MessageType.Info : MessageType.Warning;
                }
            }
            r.Dispose();
            Repaint();
        }

        static void WriteCache(bool ios, Dictionary<string, string> placements)
        {
            var path = VfxStandIn.CachePath(ios);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var parts = placements.Select(kv => VfxJsonOut.Str(kv.Key) + ":" + VfxJsonOut.Str(kv.Value));
            File.WriteAllText(path, "{" + string.Join(",", parts) + "}");
        }
    }
}
