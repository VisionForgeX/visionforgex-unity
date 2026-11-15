# Working on the VisionForgeX plugin for Unity

A thin Unity plugin over the native VisionForgeX SDKs (`VisionForgeX/visionforgex-android-sdk`,
`VisionForgeX/visionforgex-ios-sdk`), the same way the Flutter and React Native ones are. This repository is
**public** and holds only what runs inside a game. It makes **no decision**: pacing, floors, refresh, consent tags and
experiments all live on the platform (private repository `VisionForgeX/visionforgex-apps`) and arrive through the
native SDKs. If a feature needs a decision, it is built there, not here.

## Layout
- `Packages/com.visionforgex.ads/`: the UPM package (installed by Git URL with `?path=/Packages/com.visionforgex.ads#<tag>`).
- The repository root is a Unity 6 project (`ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`) used by CI
  to test the package and build the demo. `Assets/Editor/VfxCiBuild.cs` is CI's build script, not part of the package.
- `ci/`: checks that need no Unity (`metas.py`, `check_package.py`, `java/check.sh`, `dotnet/`, `unitypackage.py`).

## Rules
- **Push after every change**, with a CHANGELOG line (both `CHANGELOG.md` files, identical). Tag releases (`1.0.0`).
  Release on GitHub is the owner's step: CI then attaches the `.unitypackage`.
- Keep the C# API (`VisionForgeX`, `VfxFullScreen`, `VfxBanner`, `VfxShowCallbacks`, `VfxOptions`, `VfxReason`…)
  compatible within a major version. Mirror the native SDKs' names and the reason strings of `docs/PROTOCOL.md` (in the
  native repositories) exactly.
- **The bridge is a contract** between `Runtime/Internal/IVfxBridge.cs` (the JSON messages), `VfxAndroidBridge.cs` +
  `Plugins/Android/VfxUnityBridge.java`, and `VfxIosBridge.cs` + `Plugins/iOS/VfxUnityBridge.mm` +
  `VfxUnityBridge.swift`: change every side together. `ci/check_package.py` checks the functions and message keys match.
- Every file and folder in the package needs a `.meta` (a Git package is read-only): run `python3 ci/metas.py` after
  adding one. GUIDs come from the path and must never change.
- When the native SDKs' public API changes, update the Java stand-ins in `ci/java/stubs/com/visionforgex/ads`.
- **Test what you change.** `ci/dotnet` runs the EditMode tests on plain .NET (CI; locally when NuGet is reachable).
  Unity, Android and Xcode builds run only in GameCI and need the Unity licence secrets. Claude's workspace has no Unity,
  Android SDK or Xcode: code written there is "not compiled" until those CI jobs are green. Never report a pass you did
  not watch.
- Publisher-facing wording says VisionForgeX and support@visionforgex.com.
