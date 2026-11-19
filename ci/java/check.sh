#!/bin/sh
# Compiles the Android bridge (Plugins/Android/*.java) against stand-ins of the Android classes it uses and of the
# VisionForgeX Android SDK 1.3.0's public API as Java sees it (from vfx-core's Api.kt, VisionForgeX.kt, VfxBannerView.kt).
# It catches Java mistakes with no Unity and no Android SDK; the real compile is the GameCI Android build.
# When the SDK's public API changes, change the stand-ins in ci/java/stubs/com/visionforgex/ads too.
set -e
cd "$(dirname "$0")"
rm -rf out && mkdir out
javac -Xlint:all -Werror -d out $(find stubs -name '*.java') ../../Packages/com.visionforgex.ads/Runtime/Plugins/Android/*.java
echo "The Android bridge compiles: $(ls out/com/visionforgex/unity | tr '\n' ' ')"
