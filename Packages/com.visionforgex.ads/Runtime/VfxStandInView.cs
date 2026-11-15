#if UNITY_EDITOR
using UnityEngine;

namespace VfxAds
{
    /// <summary>
    /// Draws the Editor stand-in's ads in the Game view: a full-screen card for interstitial, rewarded and app open
    /// placements (with the reward's countdown, "Tap the ad" and "Close"), and a bar for each banner, inside the safe area.
    /// Only in the Editor; a device shows Google's real ads.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class VfxStandInView : MonoBehaviour
    {
        void OnGUI()
        {
            if (!(VisionForgeX.Bridge is VfxStandIn s)) return;
            float k = VfxGui.Scale;
            s.BannerHeightPixels = () => Mathf.RoundToInt(56 * k);
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1));
            var safe = VfxGui.SafeArea(k);
            GUI.depth = -1000;

            foreach (var b in s.Banners)
            {
                if (!b.Visible) continue;
                float h = 56;
                var r = new Rect(safe.x, b.Position == 1 ? safe.y : safe.yMax - h, safe.width, h);
                var c = GUI.color;
                GUI.color = b.Loaded ? Color.white : new Color(1, 1, 1, 0.55f);
                GUI.Box(r, "", VfxGui.Box);
                GUI.color = c;
                GUI.Label(new Rect(r.x + 14, r.y + 8, r.width - 28, 22),
                    b.Loaded ? "<b>Banner</b>  <color=" + VfxGui.Hex(VfxGui.Muted) + ">" + b.Placement + "</color>" : "<color=" + VfxGui.Hex(VfxGui.Muted) + ">Loading banner " + b.Placement + "…</color>", VfxGui.Body);
                GUI.Label(new Rect(r.x + 14, r.y + 30, r.width - 28, 20), "Editor stand-in: Google's anchored adaptive banner shows here on a device.", VfxGui.Small);
            }

            var cur = s.Current;
            if (cur != null)
            {
                GUI.color = new Color(0, 0, 0, 0.82f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width / k, Screen.height / k), Texture2D.whiteTexture);
                GUI.color = Color.white;
                float w = Mathf.Min(420, safe.width - 32), h = 300;
                var r = new Rect(safe.center.x - w / 2, safe.center.y - h / 2, w, h);
                GUI.Box(r, "", VfxGui.Box);
                GUILayout.BeginArea(new Rect(r.x + 20, r.y + 18, r.width - 40, r.height - 36));
                GUILayout.Label("<color=" + VfxGui.Hex(VfxGui.Amber) + ">VisionForgeX · Editor stand-in</color>", VfxGui.Small);
                GUILayout.Space(6);
                GUILayout.Label(Title(cur.Format), VfxGui.Title);
                GUILayout.Label("Placement <b>" + cur.Placement + "</b>", VfxGui.Body);
                GUILayout.Space(8);
                GUILayout.Label("On a device, Google's ad shows here. The game hears the same callbacks it will hear there, in the same order.", VfxGui.Small);
                GUILayout.FlexibleSpace();
                if (cur.Format == "rewarded" || cur.Format == "rewarded_interstitial")
                {
                    GUILayout.Label(cur.Rewarded ? "<color=" + VfxGui.Hex(VfxGui.Good) + ">Reward earned: coins × 10</color>" : "Reward in " + Countdown(s) + " s…", VfxGui.Body);
                    GUILayout.Space(6);
                }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Tap the ad", VfxGui.Button, GUILayout.Height(40))) s.ClickCurrent();
                GUILayout.Space(10);
                if (GUILayout.Button("Close", VfxGui.Accent, GUILayout.Height(40))) s.CloseCurrent();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
            GUI.matrix = old;
        }

        static int Countdown(VfxStandIn s) => s.Current == null ? 0 : s.Current.SecondsLeft(s.Now);

        static string Title(string format)
        {
            switch (format)
            {
                case "rewarded": return "Rewarded ad";
                case "rewarded_interstitial": return "Rewarded interstitial";
                case "app_open": return "App open ad";
                default: return "Interstitial";
            }
        }
    }
}
#endif
