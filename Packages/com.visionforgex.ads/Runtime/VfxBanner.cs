using System;

namespace VfxAds
{
    /// <summary>
    /// A banner placement at the top or bottom of the screen, always inside the safe area, as wide as the screen
    /// (Google's anchored adaptive size). It loads by itself once the SDK is ready and refreshes as the platform says,
    /// only while it can be seen. Collapsible when the platform says so.
    /// <code>
    /// var banner = VfxBanner.Show("home_banner", VfxBannerPosition.Bottom);
    /// banner.OnLoaded += () => LiftTheUi(banner.HeightPixels);
    /// …
    /// banner.Hide();      // during play
    /// banner.Show();      // on the menu again
    /// banner.Destroy();   // leaving the scene for good
    /// </code>
    /// Events come on Unity's main thread.
    /// </summary>
    public sealed class VfxBanner
    {
        readonly int _id;
        bool _destroyed;

        VfxBanner(int id, string placement, VfxBannerPosition position)
        {
            _id = id;
            Placement = placement;
            Position = position;
        }

        /// <summary>Puts a banner on screen for <paramref name="placement"/>, at the top or the bottom.</summary>
        public static VfxBanner Show(string placement, VfxBannerPosition position = VfxBannerPosition.Bottom)
        {
            var b = Create(placement, position);
            b.Show();
            return b;
        }

        /// <summary>Makes a banner, hidden until <see cref="Show()"/>: it starts loading only once shown.</summary>
        public static VfxBanner Create(string placement, VfxBannerPosition position = VfxBannerPosition.Bottom)
        {
            var b = new VfxBanner(VisionForgeX.NextId(), placement ?? "", position);
            if (!VisionForgeX.IsPlacementName(b.Placement))
            {
                VfxLog.Warn("Banner \"" + placement + "\" is not a placement name: 1 to 40 lowercase letters, digits or _ (like home_banner).");
                b._destroyed = true;
                return b;
            }
            if (!VisionForgeX.IsInitialized) VfxLog.Warn("Banner \"" + placement + "\" made before Initialize: it waits for the SDK.");
            VisionForgeX.Remember(b.Placement, VfxFormat.Banner);
            VisionForgeX.RegisterBanner(b._id, b);
            VisionForgeX.Bridge.BannerCreate(b._id, b.Placement, (int)position);
            return b;
        }

        /// <summary>The placement's name, as on the platform.</summary>
        public string Placement { get; }

        /// <summary>Top or bottom.</summary>
        public VfxBannerPosition Position { get; }

        /// <summary>On screen (asked to be; it is empty until an ad loads).</summary>
        public bool IsVisible { get; private set; }

        /// <summary>The banner's height in screen pixels (0 when hidden or before an ad loads): to keep the game's buttons clear of it.</summary>
        public int HeightPixels { get; private set; }

        /// <summary>Destroyed (or never valid): nothing more will happen.</summary>
        public bool IsDestroyed => _destroyed;

        /// <summary>An ad loaded (each refresh too).</summary>
        public event Action OnLoaded;

        /// <summary>No ad this time; the reason Google gave (its code). It tries again by itself.</summary>
        public event Action<string> OnFailed;

        /// <summary>An impression was counted.</summary>
        public event Action OnImpression;

        /// <summary>The user tapped it.</summary>
        public event Action OnClicked;

        /// <summary>What one impression earned (Google's paid event).</summary>
        public event Action<VfxRevenue> OnPaid;

        /// <summary>The height changed (it loaded, was hidden, or the screen turned).</summary>
        public event Action<int> OnHeightChanged;

        /// <summary>Shows it (it loads once on screen).</summary>
        public void Show()
        {
            if (_destroyed) return;
            IsVisible = true;
            VisionForgeX.Bridge.BannerSetVisible(_id, true);
        }

        /// <summary>Hides it. A hidden banner never refreshes.</summary>
        public void Hide()
        {
            if (_destroyed) return;
            IsVisible = false;
            VisionForgeX.Bridge.BannerSetVisible(_id, false);
        }

        /// <summary>Removes it for good.</summary>
        public void Destroy()
        {
            if (_destroyed) return;
            _destroyed = true;
            IsVisible = false;
            VisionForgeX.ForgetBanner(_id);
            VisionForgeX.Bridge.BannerDestroy(_id);
            SetHeight(0);
        }

        internal int Id => _id;

        internal void SetHeight(int h)
        {
            if (h < 0) h = 0;
            if (h == HeightPixels) return;
            HeightPixels = h;
            OnHeightChanged?.Invoke(h);
        }

        internal void OnPlacementEvent(VfxEvent e)
        {
            if (_destroyed) return;
            switch (e.Type)
            {
                case VfxEventType.Fill: OnLoaded?.Invoke(); break;
                case VfxEventType.NoFill: OnFailed?.Invoke(e.Detail("err")); break;
                case VfxEventType.Show: OnImpression?.Invoke(); break;
                case VfxEventType.Click: OnClicked?.Invoke(); break;
            }
        }

        internal void OnPlacementRevenue(VfxRevenue r)
        {
            if (!_destroyed) OnPaid?.Invoke(r);
        }

        public override string ToString() => "banner \"" + Placement + "\" (" + Position.ToString().ToLowerInvariant() + ")";
    }
}
