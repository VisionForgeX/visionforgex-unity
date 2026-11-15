# Changelog

## 1.0.0 (15 Nov 2026)

First version: VisionForgeX for Unity games, a thin layer over the released native SDKs. Every decision is made on the
platform.

- **Native SDKs:** Android 1.2.0 (`vfx-ads-nextgen` on Google's GMA Next-Gen SDK, the default, or `vfx-ads` on Google
  Mobile Ads 25.x, chosen in the settings) and iOS 1.2.0 (Google Mobile Ads 13).
- **C# API** (`using VfxAds;`): `VisionForgeX.Initialize`, `Interstitial`, `Rewarded`, `RewardedInterstitial` and
  `AppOpen` with `Show`, `IsReady` and `Load`; `VfxFullScreen.Show(name, callbacks)`; `VfxBanner` at the top or bottom
  inside the safe area (show, hide, destroy, its height, its events); privacy options; key-values; mute; the debug
  panel; Google's ad inspector; `OnEvent`, `OnRevenue`, `OnFullScreenChanged`. Every callback arrives on Unity's main
  thread, and one that throws never silences the next. The reasons are the protocol's (`VfxReason`).
- **Editor:** Assets → VisionForgeX → Settings (the keys, the Google App IDs, Google's SDK on Android, test mode,
  "Check the key" against the platform) with a project check and a fix for each problem. The same check runs before
  every Android and iOS build, and stops a build that would crash or earn nothing. In Play mode a stand-in shows
  placeholder ads and gives the game the same callbacks a phone would. It never reports revenue.
- **Builds:** the External Dependency Manager's file is written from the settings. On Android, Google's App ID goes
  into the manifest. On iOS, the VisionForgeX pod is taken from GitHub at its tag, Swift is turned on for the bridge,
  and Info.plist gets the App ID, Google's 50 SKAdNetwork identifiers and the tracking question's text.
- **Package:** `com.visionforgex.ads`, installable by Git URL; a `.unitypackage` attached to releases. Depends on
  Unity's External Dependency Manager 2.1.1. Needs Unity 6 (6000.0) or newer. The demo scene is a sample.
- **Tests:** 55 EditMode tests of the C# layer against a mock bridge and the stand-in: callbacks and their order,
  main-thread delivery, reason strings, placement names, banners, JSON, the project check, the dependency file,
  Podfile, manifest and SKAdNetwork steps.
- **Not in 1.0.0:** native ads; Swift Package Manager for iOS (CocoaPods only).
- Written without Unity, Xcode or an Android SDK: the C# layer was compiled and tested on .NET, and the Java bridge was
  compiled against stand-ins of the SDK's API. The first GameCI run is the first Unity compile and the first Android
  and iOS build.
- CI activates Unity with a Personal seat from the account's email and password (GameCI CLI 0.1.73 through
  `unity-test-runner` 4.4.0 and `unity-builder` 6.0.0), with no `.ulf`: on Unity 6 a `.ulf` failed in CI. The Unity
  jobs run one after another (a Personal account has few seats). CI's project is on Unity 6.3 LTS (6000.3.28f1):
  late 6000.0 builds are Extended LTS, which a Personal licence cannot open. CI's build script prints GameCI's
  "Build succeeded!" marker; Android's minimum API is never set below Unity's own (25 in Unity 6.3).
