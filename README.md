# VisionForgeX for Unity

Ads for Unity games, earning from Google Ad Exchange through [VisionForgeX](https://apps.visionforgex.com).
You show ads **by placement name** (`level_complete`, `extra_life`, `home_banner`). Everything else is decided
on apps.visionforgex.com and can change without a game update: what each placement requests, how often it may show,
price floors, banner refresh, consent and experiments.

- **Formats:** rewarded, interstitial, rewarded interstitial, app open, and banners (top or bottom, inside the safe area).
- **Android:** runs on the VisionForgeX Android SDK 1.2.0 with Google's **GMA Next-Gen SDK** (Google's preferred
  Android SDK). You can choose Google Mobile Ads 25.x instead (`vfx-ads`); the game's code is the same.
- **iOS:** runs on the VisionForgeX iOS SDK 1.2.0 (Google Mobile Ads 13).
- **In the Editor**, Play mode shows placeholder ads and sends the game the same callbacks as a phone, in the same order.
- **Callbacks always arrive on Unity's main thread.**

This repository holds only what runs inside a game. It makes no decisions: those are made on the platform.

---

## Before you start

You need:

1. **Unity 6** (6000.0 LTS) or newer, with Android Build Support and/or iOS Build Support.
2. **Your game added on [apps.visionforgex.com](https://apps.visionforgex.com)**, approved, with the VisionForgeX SDK
   switched on. Its page shows two things you will paste into Unity:
   - the **app key** (starts with `vfx_`), in the VisionForgeX SDK panel;
   - the **Google App ID** (`ca-app-pub-…~…`), in the code tab.

   The Android and the iOS version of a game are two apps there, each with its own key and App ID.
3. For iOS: a Mac with **Xcode 16** or newer, and [CocoaPods](https://cocoapods.org) (`sudo gem install cocoapods`).

## 1. Add the plugin to your game

In Unity: **Window → Package Manager → the + button → Add package from git URL…**, paste this and press **Add**:

```
https://github.com/VisionForgeX/visionforgex-unity.git?path=/Packages/com.visionforgex.ads#1.0.0
```

The `#1.0.0` at the end is the version. The Package Manager also installs Unity's **External Dependency Manager**,
which adds the VisionForgeX and Google SDKs to your Android and iOS builds.

*No Package Manager?* Download `VisionForgeX-1.0.0.unitypackage` from this repository's
[Releases](https://github.com/VisionForgeX/visionforgex-unity/releases), then **Assets → Import Package → Custom
Package**. Install **External Dependency Manager** from the Package Manager's *Unity Registry* too.

> Using Google's own Mobile Ads Unity plugin (the `GoogleMobileAds` folder)? Remove it first. VisionForgeX brings
> Google's SDK itself, and a game cannot have two copies of it. The plugin's project check says so if it finds one.

## 2. Settings

Open **Assets → VisionForgeX → Settings** and press **Make the settings**. Then fill in:

| Setting | What to put |
|---|---|
| Android app key / iOS app key | The app's key from its page on VisionForgeX |
| Android / iOS Google App ID | The App ID from the same page (`ca-app-pub-…~…`) |
| Android uses | **Next-Gen** (recommended), or Google Mobile Ads 25.x |
| Test mode | On while you build the game: the Editor and **development** builds show Google's demo ads. A release build never does. |

Press **Check the key**: the plugin asks VisionForgeX which placements the app has (nothing is counted), and the
Editor then knows them.

The **Project check** at the bottom lists anything that would stop the game from earning, each with a **Fix**
button: the minimum Android version (API 24), the Android target version (API 35 for Next-Gen), the minimum iOS
version (13.0), a missing App ID. Every Android and iOS build runs the same check, and stops with the reason if
something would make the game crash.

**Android only, once:** *Edit → Project Settings → Player → Android → Publishing Settings*, tick **Custom Main Gradle
Template** and **Custom Gradle Settings Template**. Then *Assets → External Dependency Manager → Android Resolver →
Force Resolve*. (The External Dependency Manager may offer to do this for you: say yes.)

## 3. Start it

Once, when the game starts (your first scene):

```csharp
using UnityEngine;
using VfxAds;

public class Boot : MonoBehaviour
{
    void Start()
    {
        VisionForgeX.Initialize(status => Debug.Log("VisionForgeX: " + status));
    }
}
```

It uses the key from the settings (the Android or the iOS one, as the build is). Where the law needs consent (the
EEA, the UK, Switzerland, some US states), Google's consent form shows by itself the first time.

You can also pass the key yourself: `VisionForgeX.Initialize("vfx_…", new VfxOptions(), status => { … })`. Or tick
**Start when the game starts** in the settings and write no code at all.

## 4. Show ads

### Interstitial: at a natural pause

```csharp
// after every level: ask every time; VisionForgeX decides whether an ad shows now
VisionForgeX.Interstitial("level_complete").Show(result => LoadNextLevel());
```

`Show` never blocks the game. If no ad shows, the callback says why at once and the game carries on.

### Rewarded: the player chooses to watch

```csharp
// make the button live only when an ad is ready
watchButton.interactable = VisionForgeX.Rewarded("extra_life").IsReady();

void OnWatchPressed()
{
    VisionForgeX.Rewarded("extra_life").Show(new VfxShowCallbacks
    {
        OnReward = (type, amount) => GiveExtraLife(),     // earned: before the ad closes
        OnFinished = result => ResumeGame(),              // always last, shown or not
    });
}
```

`RewardedInterstitial("…")` works the same way. Google asks for a screen first that tells the player a reward is
offered and lets them say no.

### App open

Nothing to write: app open ads show by themselves when the player comes back to the game, whenever VisionForgeX
switches that on for a placement. To show one yourself: `VisionForgeX.AppOpen("app_open").Show();`

### Banner

```csharp
VfxBanner banner = VfxBanner.Show("home_banner", VfxBannerPosition.Bottom);

banner.OnHeightChanged += pixels => MoveMyButtonsUp(pixels);   // keep your UI clear of it
banner.Hide();      // during play
banner.Show();      // back on the menu
banner.Destroy();   // gone for good
```

It is as wide as the screen, stays inside the safe area (notches, the home bar), loads by itself, and refreshes as
VisionForgeX says, but only while it can be seen. `banner.HeightPixels` is how much of the screen's edge it covers,
in screen pixels.

### Every callback

`VfxShowCallbacks`: `OnShown`, `OnClicked`, `OnReward(type, amount)`, `OnDismissed`, `OnNotShown(reason)`, and
`OnFinished(result)`, which always comes last, exactly once. All of them arrive on Unity's main thread.

When no ad shows, the reason is one of these (`VfxReason`), the same words on Android and iOS:

| Reason | Means |
|---|---|
| `not_ready` | No ad loaded yet (or the SDK has not started) |
| `interval`, `session`, `hour`, `day`, `grace`, `nth`, `gap` | The platform's pacing said "not now". This is normal. |
| `showing` | Another full-screen ad is on screen |
| `paused` | The app or the placement is paused on VisionForgeX |
| `consent` | Consent does not allow ad requests |
| `failed` | Google could not show the loaded ad |
| `no_activity` | (Android) there was no screen to show it on |
| `unknown_placement` | No placement of that name on VisionForgeX (names are lowercase letters, digits and `_`) |

### Placement names by themselves

`VfxFullScreen.Show("level_complete", callbacks)` and `VfxFullScreen.IsReady("level_complete")` work too. The ad is
requested as rewarded when the callbacks have `OnReward`, and as an interstitial otherwise.

## 5. Privacy, revenue and the rest

```csharp
// a "Privacy options" button in your settings screen: required in the EEA, the UK and some US states
privacyButton.SetActive(VisionForgeX.IsPrivacyOptionsRequired);
VisionForgeX.ShowPrivacyOptions(error => { });

// what each impression earned, for your analytics (Firebase, AppsFlyer, Adjust…)
VisionForgeX.OnRevenue += r => Analytics.LogAdRevenue(r.Value, r.Currency);

// every ad event (req, fill, nofill, show, click, close, fail, reward, paid, capped)
VisionForgeX.OnEvent += e => Debug.Log(e);

// your own targeting key-values, for Ad Manager (lowercase letters, digits and _, at most 20; not vfx_…)
VisionForgeX.SetKeyValue("level", "12");

VisionForgeX.SetMuted(true);          // your game's sound setting, for video ads
VisionForgeX.OpenDebugPanel();        // development builds: state, each placement with Load and Show, last events
VisionForgeX.OpenAdInspector();       // Google's ad inspector, on a phone
```

While a full-screen ad is on screen the plugin pauses the game's audio (`AudioListener.pause`), and
`VisionForgeX.OnFullScreenChanged` tells you when one opens and closes. Turn the audio pause off in the settings if
your game handles it.

## 6. Test it

1. **In the Editor:** press Play. Placeholder ads show with the same callbacks a phone gives. A rewarded one rewards
   after 5 seconds, and you can tap it or close it. The Editor never reports revenue.
2. **On a phone, with test mode on:** a development build (*Build Settings → Development Build*) shows Google's demo
   ads. Its placements are `banner`, `collapsible_banner`, `interstitial`, `rewarded`, `rewarded_interstitial` and
   `app_open`.
3. **On a phone, with your own placements:** turn test mode off. Then either turn on **Test ads** for the app on
   VisionForgeX, or add your phone as a Google test device (the device log prints its ID).

The sample (**Package Manager → VisionForgeX Ads → Samples → Demo scene → Import**) is a tiny game that uses all of
this: a rewarded button, an interstitial between levels, a bottom banner, privacy options and the debug panel.

## 7. Build

**Android:** build as usual. The plugin writes Google's App ID into the manifest, and the External Dependency
Manager adds `com.github.VisionForgeX.visionforgex-android-sdk:vfx-ads-nextgen:1.2.0` (or `vfx-ads:1.2.0`) from
JitPack. Next-Gen needs **Target API Level 35**; the project check offers the fix.

**iOS:** build the Xcode project from Unity. The plugin then:

- takes the VisionForgeX iOS SDK from GitHub: `pod 'VisionForgeX', :git => 'https://github.com/VisionForgeX/visionforgex-ios-sdk.git', :tag => '1.2.0'`
  goes into the Podfile, because it is not on CocoaPods' public list yet;
- turns Swift on for its bridge;
- adds `GADApplicationIdentifier`, Google's SKAdNetwork identifiers and, if you wrote one, the tracking question to
  Info.plist.

The External Dependency Manager runs `pod install`. **Open `Unity-iPhone.xcworkspace`** (not `.xcodeproj`) in Xcode,
then build.

## If something is wrong

| You see | Why, usually | Do |
|---|---|---|
| The build stops with "[VisionForgeX] The build was stopped…" | The project check found something that would crash the game or stop it earning | Assets → VisionForgeX → Settings: each problem has its fix |
| The game closes at once on a phone | No Google App ID in the build | Paste it in the settings |
| `Not ready: …` in the console | No key, a wrong key, or the app paused on VisionForgeX | **Check the key** in the settings |
| Every show says `not_ready` | Nothing loaded yet: new apps, no fill, or no internet | Wait a minute; open the debug panel; try test mode |
| Android: `ClassNotFoundException: com.visionforgex…` | The External Dependency Manager did not resolve | Tick the two Gradle templates, then *Force Resolve* |
| Android: "Dependency requires compileSdk 35" | Target API below 35 with Next-Gen | Player Settings → Target API Level 35 |
| iOS: `No such module 'VisionForgeX'` | The pods are not installed, or you opened the `.xcodeproj` | `pod install` in the Xcode project's folder, then open the `.xcworkspace` |

Still stuck: write to **support@visionforgex.com** with the debug panel's screenshot and your Unity version.

## How it works

```
your game ──C#──▶ VisionForgeX (this plugin) ──┬─ Android: VfxUnityBridge.java ─▶ VisionForgeX Android SDK 1.2.0 ─▶ Google
                                               ├─ iOS: VfxUnityBridge.mm/.swift ─▶ VisionForgeX iOS SDK 1.2.0 ─────▶ Google
                                               └─ Editor: the stand-in (placeholder ads)
                         ◀── callbacks, on Unity's main thread ──
```

The native SDKs fetch the game's settings from apps.visionforgex.com and carry them out. This plugin passes the
game's calls to them and their answers back.

| Folder | What |
|---|---|
| `Packages/com.visionforgex.ads/Runtime` | The C# API (`VisionForgeX`, `VfxFullScreen`, `VfxBanner`, `VfxOptions`…), the main-thread delivery, the Editor's stand-in, the debug panel |
| `…/Runtime/Plugins/Android` | The Java bridge to the Android SDK |
| `…/Runtime/Plugins/iOS` | The Objective-C++ and Swift bridge to the iOS SDK |
| `…/Editor` | Settings, the project check, the dependency file, the Android manifest, Podfile and Info.plist steps |
| `…/Tests/Editor` | The tests of the C# layer |
| `…/Samples~/Demo` | The demo scene |
| `Assets/Editor`, `ci/` | How GitHub's CI checks and builds it (not part of the package) |

## For the people who maintain it

**CI** (`.github/workflows/ci.yml`, the *Actions* tab):

- **Without Unity**, on every push: the C# tests on .NET; the Java bridge compiled against stand-ins of the Android
  SDK's API; the `.meta` files; the package's consistency (versions, bridge functions on every side); the
  `.unitypackage`.
- **With Unity** (GameCI, Unity 6000.0.86f1), once the repository has the Unity account's secrets (below): the tests in Unity, the
  demo built for Android on Next-Gen and on Google Mobile Ads 25.x, and for iOS. The iOS project is then compiled
  with Xcode on GitHub's Mac, with the pods.

**The Unity licence** (Unity Personal is free). CI signs in to Unity with an account's email and password, takes a free
Personal seat for each Unity job and gives it back when the job ends. No licence file is needed: on Unity 6 a `.ulf`
file does not work in CI.

1. Have a Unity account (unity.com) with a Personal licence: sign in once in Unity Hub and accept the free Personal
   licence. **Two-step verification must be off for this account**, because CI cannot type a code. A separate
   Unity account just for CI is the safest choice.
2. On GitHub, open this repository's **Settings → Secrets and variables → Actions → New repository secret**, and add
   two secrets:
   - `UNITY_EMAIL`: the Unity account's email;
   - `UNITY_PASSWORD`: its password.

Until those exist, the Unity jobs are skipped and the rest still runs.

**A release:** change the version in `package.json`, in `VisionForgeX.Version`, in both `CHANGELOG.md` files and in
this README's install address. CI checks they agree. Then on GitHub: **Releases → Draft a new release → Choose a tag**,
type the version (`1.0.1`) → **Create new tag on publish** → **Publish release**. CI then attaches the
`.unitypackage` to it.

---

Questions: **support@visionforgex.com** · [apps.visionforgex.com](https://apps.visionforgex.com)
