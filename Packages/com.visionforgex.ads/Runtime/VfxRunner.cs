using UnityEngine;

namespace VfxAds
{
    public static partial class VisionForgeX
    {
        static partial void EnsureRunnerImpl() => VfxRunner.Ensure();

        static partial void CreatePlatformBridge(ref IVfxBridge bridge)
        {
#if UNITY_EDITOR
            bridge = new VfxStandIn();
#elif UNITY_ANDROID
            bridge = new VfxAndroidBridge();
#elif UNITY_IOS
            bridge = new VfxIosBridge();
#else
            bridge = new VfxNoneBridge();
#endif
        }

        static partial void OpenCSharpDebugPanelImpl()
        {
            VfxRunner.Ensure();
            VfxDebugPanel.Open();
        }
    }

    /// <summary>
    /// The one hidden object the plugin keeps alive across scenes: it delivers callbacks on Unity's main thread every
    /// frame, pauses the game's audio during full-screen ads (when the settings say so), and in the Editor draws the
    /// stand-in ads and moves the stand-in's clock.
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(-10000)]
    internal sealed class VfxRunner : MonoBehaviour
    {
        static VfxRunner _instance;
        bool _audioWasPaused;
        bool _pausedAudio;

        public static void Ensure()
        {
            if (_instance != null || !Application.isPlaying) return;
            var go = new GameObject("VisionForgeX") { hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<VfxRunner>();
#if UNITY_EDITOR
            go.AddComponent<VfxStandInView>();
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartFromSettings()
        {
            VfxMainThread.Capture();
            var s = VfxSettings.Current;
            if (s != null && s.initializeOnStart && !VisionForgeX.IsInitialized) VisionForgeX.Initialize();
        }

        void Awake()
        {
            VfxMainThread.Capture();
            VisionForgeX.OnFullScreenChanged += FullScreenChanged;
        }

        void OnDestroy()
        {
            VisionForgeX.OnFullScreenChanged -= FullScreenChanged;
            if (_instance == this) _instance = null;
        }

        void Update()
        {
#if UNITY_EDITOR
            (VisionForgeX.Bridge as VfxStandIn)?.Tick();
#endif
            VfxMainThread.Pump();
        }

        void FullScreenChanged(bool open)
        {
            var s = VfxSettings.Current;
            if (s != null && !s.pauseAudioDuringAds) return;
            if (open && !_pausedAudio)
            {
                _audioWasPaused = AudioListener.pause;
                AudioListener.pause = true;
                _pausedAudio = true;
            }
            else if (!open && _pausedAudio && !VisionForgeX.IsFullScreenShowing)
            {
                AudioListener.pause = _audioWasPaused;
                _pausedAudio = false;
            }
        }
    }

    /// <summary>The plugin's own on-screen look (IMGUI, so it needs no UI package): colours, rounded boxes, scale.</summary>
    internal static class VfxGui
    {
        public static readonly Color Ink = new Color32(0x14, 0x12, 0x1f, 0xff);
        public static readonly Color Panel = new Color32(0x1d, 0x1a, 0x2e, 0xf7);
        public static readonly Color Line = new Color32(0x34, 0x2f, 0x4f, 0xff);
        public static readonly Color Violet = new Color32(0x8b, 0x5c, 0xf6, 0xff);
        public static readonly Color Amber = new Color32(0xfb, 0xbf, 0x24, 0xff);
        public static readonly Color Text = new Color32(0xf5, 0xf3, 0xff, 0xff);
        public static readonly Color Muted = new Color32(0xa8, 0xa2, 0xc4, 0xff);
        public static readonly Color Good = new Color32(0x34, 0xd3, 0x99, 0xff);
        public static readonly Color Bad = new Color32(0xf8, 0x71, 0x71, 0xff);

        static GUIStyle _box, _title, _body, _small, _button, _accent, _mono;
        static Texture2D _round, _accentTex, _plain;

        /// <summary>Points to pixels: the panels are drawn at a readable size on phones and tablets.</summary>
        public static float Scale
        {
            get
            {
                var dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
                return Mathf.Clamp(dpi / 160f, 1f, 3.5f) * (Application.isMobilePlatform ? 1f : 0.9f);
            }
        }

        static Texture2D Rounded(Color c, int r = 8)
        {
            int n = r * 2 + 2;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(r - x, x - (n - 1 - r))), dy = Mathf.Max(0, Mathf.Max(r - y, y - (n - 1 - r)));
                    float a = Mathf.Clamp01(r + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    t.SetPixel(x, y, new Color(c.r, c.g, c.b, c.a * a));
                }
            t.Apply();
            return t;
        }

        static GUIStyle Make(Texture2D bg, int r, int size, FontStyle fs, Color text, TextAnchor align)
        {
            var s = new GUIStyle { fontSize = size, fontStyle = fs, alignment = align, wordWrap = true, richText = true };
            s.normal.textColor = text;
            if (bg != null) { s.normal.background = bg; s.border = new RectOffset(r, r, r, r); }
            s.padding = new RectOffset(12, 12, 8, 8);
            return s;
        }

        static void Init()
        {
            if (_box != null && _round != null) return;
            _round = Rounded(Panel, 10);
            _accentTex = Rounded(Violet, 8);
            _plain = Rounded(new Color32(0x2b, 0x26, 0x44, 0xff), 8);
            _box = Make(_round, 10, 14, FontStyle.Normal, Text, TextAnchor.UpperLeft);
            _box.padding = new RectOffset(16, 16, 14, 14);
            _title = Make(null, 0, 17, FontStyle.Bold, Text, TextAnchor.MiddleLeft);
            _title.padding = new RectOffset(0, 0, 2, 2);
            _body = Make(null, 0, 13, FontStyle.Normal, Text, TextAnchor.UpperLeft);
            _body.padding = new RectOffset(0, 0, 2, 2);
            _small = Make(null, 0, 11, FontStyle.Normal, Muted, TextAnchor.UpperLeft);
            _small.padding = new RectOffset(0, 0, 1, 1);
            _mono = Make(null, 0, 11, FontStyle.Normal, Text, TextAnchor.UpperLeft);
            _mono.padding = new RectOffset(0, 0, 1, 1);
            _button = Make(_plain, 8, 13, FontStyle.Bold, Text, TextAnchor.MiddleCenter);
            _button.hover.background = _accentTex; _button.hover.textColor = Text;
            _button.active.background = _accentTex; _button.active.textColor = Text;
            _accent = Make(_accentTex, 8, 13, FontStyle.Bold, Text, TextAnchor.MiddleCenter);
            _accent.hover.background = _accentTex; _accent.hover.textColor = Amber;
            _accent.active.background = _accentTex; _accent.active.textColor = Amber;
        }

        public static GUIStyle Box { get { Init(); return _box; } }
        public static GUIStyle Title { get { Init(); return _title; } }
        public static GUIStyle Body { get { Init(); return _body; } }
        public static GUIStyle Small { get { Init(); return _small; } }
        public static GUIStyle Mono { get { Init(); return _mono; } }
        public static GUIStyle Button { get { Init(); return _button; } }
        public static GUIStyle Accent { get { Init(); return _accent; } }

        /// <summary>A coloured hex as rich text.</summary>
        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>The screen's safe area in GUI points (y down), scaled.</summary>
        public static Rect SafeArea(float scale)
        {
            var sa = Screen.safeArea;
            return new Rect(sa.x / scale, (Screen.height - sa.yMax) / scale, sa.width / scale, sa.height / scale);
        }
    }
}
