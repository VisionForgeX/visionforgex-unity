using System;

namespace VfxAds
{
    /// <summary>
    /// One full-screen placement: <c>VisionForgeX.Interstitial("level_complete")</c>, <c>VisionForgeX.Rewarded("extra_life")</c>,
    /// <c>VisionForgeX.RewardedInterstitial(…)</c>, <c>VisionForgeX.AppOpen(…)</c>. Cheap to make: it is only a name and a format.
    /// </summary>
    public sealed class VfxFullScreen
    {
        internal VfxFullScreen(string placement, VfxFormat format)
        {
            Placement = placement ?? "";
            Format = format;
        }

        /// <summary>The placement's name, as on the platform.</summary>
        public string Placement { get; }

        /// <summary>Its format.</summary>
        public VfxFormat Format { get; }

        /// <summary>An ad is loaded and fresh, so Show() will show it (if the pacing allows). Use it to enable a "Watch an ad" button.</summary>
        public bool IsReady()
        {
            if (!VisionForgeX.IsInitialized || !VisionForgeX.IsPlacementName(Placement)) return false;
            VisionForgeX.Remember(Placement, Format);
            return VisionForgeX.Bridge.FullScreenReady(Placement, Format.Wire());
        }

        /// <summary>Loads one if none is waiting (placements that preload do this by themselves).</summary>
        public void Load()
        {
            if (!VisionForgeX.IsInitialized || !VisionForgeX.IsPlacementName(Placement)) return;
            VisionForgeX.Remember(Placement, Format);
            VisionForgeX.Bridge.Load(Placement, Format.Wire());
        }

        /// <summary>
        /// Shows it if one is ready and the pacing allows; otherwise <see cref="VfxShowCallbacks.OnNotShown"/> says why and
        /// the game carries on. Safe to call at every opportunity (after each level, between rounds): the platform's
        /// pacing decides whether an ad shows now. Callbacks on Unity's main thread; OnFinished always last.
        /// </summary>
        public void Show(VfxShowCallbacks callbacks = null) => VisionForgeX.Show(Placement, Format, callbacks);

        /// <summary>Shows it and calls <paramref name="onFinished"/> when it is over (shown and closed, or not shown and why).</summary>
        public void Show(Action<VfxShowResult> onFinished) => Show(new VfxShowCallbacks { OnFinished = onFinished });

        /// <summary>Rewarded formats: <c>Show((type, amount) => GiveCoins(amount))</c>.</summary>
        public void Show(Action<string, int> onReward, Action<VfxShowResult> onFinished = null) =>
            Show(new VfxShowCallbacks { OnReward = onReward, OnFinished = onFinished });

        // ------------------------------------------------------------------ by name only

        /// <summary>
        /// Shows a full-screen placement by name: <c>VfxFullScreen.Show("level_complete", callbacks)</c>. Asked as a
        /// rewarded placement when the callbacks listen for a reward, as an interstitial otherwise. The platform knows each
        /// placement's real format and shows that; for a rewarded interstitial or an app open placement, use
        /// <c>VisionForgeX.RewardedInterstitial(name)</c> / <c>VisionForgeX.AppOpen(name)</c> to keep the device log quiet.
        /// </summary>
        public static void Show(string placement, VfxShowCallbacks callbacks = null) =>
            VisionForgeX.Show(placement, callbacks?.OnReward != null ? VfxFormat.Rewarded : VfxFormat.Interstitial, callbacks);

        /// <summary>An ad is loaded for this placement and Show() would show it (if the pacing allows).</summary>
        public static bool IsReady(string placement) =>
            VisionForgeX.IsInitialized && VisionForgeX.IsPlacementName(placement) && VisionForgeX.Bridge.FullScreenReady(placement, KnownFormat(placement).Wire());

        static VfxFormat KnownFormat(string placement) =>
            VisionForgeX.KnownPlacements.TryGetValue(placement, out var f) && f.IsFullScreen() ? f : VfxFormat.Interstitial;

        public override string ToString() => Format.Wire() + " \"" + Placement + "\"";
    }
}
