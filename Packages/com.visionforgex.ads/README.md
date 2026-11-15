# VisionForgeX Ads for Unity

Rewarded, interstitial, rewarded interstitial, app open and banner ads for Unity games, by placement name. Every
decision (what to request, pacing, floors, refresh, consent, experiments) is made on
[apps.visionforgex.com](https://apps.visionforgex.com).

```csharp
using VfxAds;

VisionForgeX.Initialize(status => Debug.Log(status));   // the key from Assets → VisionForgeX → Settings
VisionForgeX.Interstitial("level_complete").Show(result => LoadNextLevel());
VisionForgeX.Rewarded("extra_life").Show(new VfxShowCallbacks { OnReward = (type, amount) => GiveLife() });
var banner = VfxBanner.Show("home_banner", VfxBannerPosition.Bottom);
```

Set-up, testing, building for Android and iOS, and troubleshooting:
**[the full guide](https://github.com/VisionForgeX/visionforgex-unity#readme)**. Support: support@visionforgex.com.
