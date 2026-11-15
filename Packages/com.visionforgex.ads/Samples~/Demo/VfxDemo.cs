using UnityEngine;
using VfxAds;

/// <summary>
/// The VisionForgeX demo: a tiny "game" with levels and coins, showing how a real game uses the plugin.
///   • the SDK starts once (key from Assets → VisionForgeX → Settings);
///   • an interstitial is asked for after every level: the platform's pacing decides whether one shows;
///   • a rewarded ad gives 50 coins, and its button is live only when an ad is ready;
///   • a bottom banner on the menu, hidden during play, with the game's buttons kept clear of it;
///   • privacy options when the law needs them, and the debug panel.
/// The placement names below are Google's demo ones, so it runs at once with test mode on; in a real game they are
/// the names set on apps.visionforgex.com.
/// </summary>
public sealed class VfxDemo : MonoBehaviour
{
    [Header("Placements (as named on apps.visionforgex.com)")]
    public string interstitialPlacement = "interstitial";
    public string rewardedPlacement = "rewarded";
    public string bannerPlacement = "banner";

    int _level = 1;
    int _coins;
    bool _playing;
    float _levelStarted;
    string _line = "Starting VisionForgeX…";
    VfxBanner _banner;

    void Start()
    {
        VisionForgeX.Initialize(status => _line = status.Ready ? "VisionForgeX is ready." : "Not ready: " + status.Reason);
        VisionForgeX.OnRevenue += r => Debug.Log("VisionForgeX revenue: " + r);   // your analytics: Firebase, AppsFlyer, Adjust…
        _banner = VfxBanner.Show(bannerPlacement, VfxBannerPosition.Bottom);
    }

    void StartLevel()
    {
        _playing = true;
        _levelStarted = Time.time;
        _banner.Hide();   // no banner while playing
        VisionForgeX.SetKeyValue("level", _level.ToString());
    }

    void FinishLevel()
    {
        _playing = false;
        _level++;
        // a natural pause: ask every time; the platform decides whether an ad shows now
        VisionForgeX.Interstitial(interstitialPlacement).Show(result =>
        {
            _line = result.Shown ? "Thanks for watching." : "No ad this time (" + result.Reason + "): on with the game.";
            _banner.Show();
        });
    }

    void WatchForCoins()
    {
        VisionForgeX.Rewarded(rewardedPlacement).Show(new VfxShowCallbacks
        {
            OnReward = (type, amount) => { _coins += 50; _line = "+50 coins."; },
            OnFinished = result => { if (!result.Shown) _line = "No ad right now (" + result.Reason + "). Try again in a moment."; },
        });
    }

    void OnGUI()
    {
        float k = Mathf.Clamp((Screen.dpi > 0 ? Screen.dpi : 160f) / 160f, 1f, 3.5f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1));
        float w = Screen.width / k, h = Screen.height / k;
        var safe = Screen.safeArea;
        float top = (Screen.height - safe.yMax) / k + 16;
        float bottomClear = Mathf.Max((safe.y) / k, (_banner != null ? _banner.HeightPixels : 0) / k) + 16;   // keep clear of the banner

        var title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        var body = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        var button = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };

        float col = Mathf.Min(360, w - 32), x = (w - col) / 2, y = top;
        GUI.Label(new Rect(x, y, col, 34), "Level " + _level + "   ·   " + _coins + " coins", title); y += 44;
        GUI.Label(new Rect(x, y, col, 44), _line, body); y += 56;

        if (_playing)
        {
            float t = Time.time - _levelStarted;
            GUI.Label(new Rect(x, y, col, 30), "Playing… " + t.ToString("0.0") + " s", body); y += 40;
            if (GUI.Button(new Rect(x, y, col, 54), "Finish the level", button)) FinishLevel();
            return;
        }

        if (GUI.Button(new Rect(x, y, col, 54), "Play level " + _level, button)) StartLevel();
        y += 66;
        bool ready = VisionForgeX.Rewarded(rewardedPlacement).IsReady();
        GUI.enabled = ready;
        if (GUI.Button(new Rect(x, y, col, 54), ready ? "Watch an ad for 50 coins" : "Loading an ad…", button)) WatchForCoins();
        GUI.enabled = true;
        y += 66;

        float row = Mathf.Min(h - bottomClear - 48, Mathf.Max(y, h - bottomClear - 48));
        float half = (col - 10) / 2;
        if (VisionForgeX.IsPrivacyOptionsRequired && GUI.Button(new Rect(x, row - 54, col, 44), "Privacy options"))
            VisionForgeX.ShowPrivacyOptions(err => _line = err == null ? "Privacy choices saved." : "Privacy options: " + err);
        if (GUI.Button(new Rect(x, row, half, 44), "Debug panel")) VisionForgeX.OpenDebugPanel();
        if (GUI.Button(new Rect(x + half + 10, row, half, 44), _banner.IsVisible ? "Hide banner" : "Show banner"))
        {
            if (_banner.IsVisible) _banner.Hide(); else _banner.Show();
        }
    }
}
