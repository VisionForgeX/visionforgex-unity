using System.Collections.Generic;
using UnityEngine;

namespace VfxAds
{
    /// <summary>
    /// The plugin's debug panel (VisionForgeX.OpenDebugPanel() on iOS and in the Editor; Android opens the native SDK's
    /// own): the SDK's state, each placement the game used with Load and Show, the last events, and the tools (privacy
    /// options, Google's ad inspector). For development builds; it changes nothing the platform decides.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class VfxDebugPanel : MonoBehaviour
    {
        static VfxDebugPanel _open;
        Vector2 _scroll;
        string _lastResult = "";

        public static bool IsOpen => _open != null;

        public static void Open()
        {
            if (_open != null || !Application.isPlaying) return;
            var go = new GameObject("VisionForgeX debug panel") { hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave };
            DontDestroyOnLoad(go);
            _open = go.AddComponent<VfxDebugPanel>();
        }

        public static void Close()
        {
            if (_open != null) Destroy(_open.gameObject);
            _open = null;
        }

        void OnGUI()
        {
            float k = VfxGui.Scale;
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1));
            GUI.depth = -2000;
            var safe = VfxGui.SafeArea(k);
            var r = new Rect(safe.x + 10, safe.y + 10, Mathf.Min(460, safe.width - 20), safe.height - 20);
            GUI.Box(r, "", VfxGui.Box);
            GUILayout.BeginArea(new Rect(r.x + 16, r.y + 14, r.width - 32, r.height - 28));

            GUILayout.BeginHorizontal();
            GUILayout.Label("VisionForge<color=" + VfxGui.Hex(VfxGui.Amber) + ">X</color> debug panel", VfxGui.Title);
            if (GUILayout.Button("Close", VfxGui.Button, GUILayout.Width(80), GUILayout.Height(30))) Close();
            GUILayout.EndHorizontal();

            var st = VisionForgeX.Status;
            string state = !VisionForgeX.IsInitialized ? Colour("Not started: call VisionForgeX.Initialize()", VfxGui.Bad)
                : st == null ? "Starting…"
                : st.Ready ? Colour("Ready", VfxGui.Good) + (st.FromCache ? " (cached settings)" : "")
                : Colour("Not ready: " + st.Reason, VfxGui.Bad);
            GUILayout.Label(state, VfxGui.Body);
            GUILayout.Label("Plugin " + VisionForgeX.Version + " · " + VisionForgeX.Platform
                            + (VisionForgeX.NativeVersion != "" ? " SDK " + VisionForgeX.NativeVersion : "")
                            + " · settings: " + VisionForgeX.SettingsSource()
                            + (VisionForgeX.IsPrivacyOptionsRequired ? " · privacy options required" : ""), VfxGui.Small);
            GUILayout.Space(8);

            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("<b>Placements</b>", VfxGui.Body);
            var known = new List<KeyValuePair<string, VfxFormat>>(VisionForgeX.KnownPlacements);
            if (known.Count == 0) GUILayout.Label("None used yet in this session. The game's placements appear here once it loads, shows or hears of one.", VfxGui.Small);
            foreach (var p in known)
            {
                GUILayout.BeginHorizontal();
                bool full = p.Value.IsFullScreen();
                var fs = full ? new VfxFullScreen(p.Key, p.Value) : null;
                string ready = full ? (fs.IsReady() ? Colour(" ready", VfxGui.Good) : Colour(" not loaded", VfxGui.Muted)) : "";
                GUILayout.Label("<b>" + p.Key + "</b>  <color=" + VfxGui.Hex(VfxGui.Muted) + ">" + p.Value.Wire() + "</color>" + ready, VfxGui.Body);
                if (full)
                {
                    if (GUILayout.Button("Load", VfxGui.Button, GUILayout.Width(64), GUILayout.Height(28))) fs.Load();
                    if (GUILayout.Button("Show", VfxGui.Accent, GUILayout.Width(64), GUILayout.Height(28)))
                        fs.Show(new VfxShowCallbacks { OnFinished = res => _lastResult = p.Key + ": " + res });
                }
                GUILayout.EndHorizontal();
            }
            if (_lastResult != "") GUILayout.Label("Last show: " + _lastResult, VfxGui.Small);

            GUILayout.Space(10);
            GUILayout.Label("<b>Last events</b>", VfxGui.Body);
            var ev = VisionForgeX.RecentEvents;
            if (ev.Count == 0) GUILayout.Label("No events yet.", VfxGui.Small);
            for (int i = ev.Count - 1; i >= 0 && i >= ev.Count - 25; i--)
            {
                var e = ev[i];
                var c = e.Type == VfxEventType.Fill || e.Type == VfxEventType.Show || e.Type == VfxEventType.Reward ? VfxGui.Good
                    : e.Type == VfxEventType.NoFill || e.Type == VfxEventType.Fail ? VfxGui.Bad
                    : e.Type == VfxEventType.Capped ? VfxGui.Amber : VfxGui.Text;
                GUILayout.Label(Colour(e.Type, c) + "  " + e.Placement + "  <color=" + VfxGui.Hex(VfxGui.Muted) + ">" + Details(e) + "</color>", VfxGui.Mono);
            }
            GUILayout.EndScrollView();

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Privacy options", VfxGui.Button, GUILayout.Height(34))) VisionForgeX.ShowPrivacyOptions(err => _lastResult = "privacy options: " + (err ?? "done"));
            if (GUILayout.Button("Ad inspector", VfxGui.Button, GUILayout.Height(34))) VisionForgeX.OpenAdInspector();
            if (GUILayout.Button("Send events", VfxGui.Button, GUILayout.Height(34))) VisionForgeX.Flush();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
            GUI.matrix = old;
        }

        static string Colour(string s, Color c) => "<color=" + VfxGui.Hex(c) + ">" + s + "</color>";

        static string Details(VfxEvent e)
        {
            var parts = new List<string>();
            foreach (var kv in e.Details) parts.Add(kv.Key + "=" + kv.Value);
            return string.Join(" ", parts);
        }
    }
}
