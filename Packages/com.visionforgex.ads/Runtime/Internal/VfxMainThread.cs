using System;
using System.Collections.Concurrent;
using System.Threading;

namespace VfxAds
{
    /// <summary>
    /// Brings every callback to Unity's main thread. The native SDKs answer on Android's UI thread or iOS's main
    /// thread; a game's code must only ever run on Unity's. Anything posted here runs at the next frame, in order,
    /// from <see cref="VfxRunner"/>'s Update (or <see cref="Pump"/> in tests).
    /// </summary>
    internal static class VfxMainThread
    {
        static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();
        static int _mainId = -1;

        /// <summary>Remembers the calling thread as Unity's main thread (called from Initialize and the runner).</summary>
        public static void Capture()
        {
            if (_mainId < 0) _mainId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>True on Unity's main thread (true too before it is known, which is only during start-up).</summary>
        public static bool IsMain => _mainId < 0 || Thread.CurrentThread.ManagedThreadId == _mainId;

        /// <summary>Runs <paramref name="a"/> on Unity's main thread at the next frame. Safe from any thread.</summary>
        public static void Post(Action a)
        {
            if (a != null) Queue.Enqueue(a);
        }

        /// <summary>How many callbacks wait.</summary>
        public static int Pending => Queue.Count;

        /// <summary>
        /// Runs what is waiting (only on the main thread; elsewhere it does nothing). A callback that throws is logged and
        /// the others still run: one broken handler in a game never silences the next.
        /// </summary>
        public static int Pump()
        {
            if (!IsMain) return 0;
            int n = 0;
            // only what was there when the pump began: a callback posting another gets it next frame
            int max = Queue.Count;
            while (n < max && Queue.TryDequeue(out var a))
            {
                n++;
                try { a(); }
                catch (Exception e) { VfxLog.Error("A callback of the game's threw: " + e); }
            }
            return n;
        }

        /// <summary>Tests only: forget the main thread and anything waiting.</summary>
        internal static void ResetForTests()
        {
            while (Queue.TryDequeue(out _)) { }
            _mainId = -1;
        }
    }

    /// <summary>The package's own console lines, all starting "[VisionForgeX]".</summary>
    internal static class VfxLog
    {
        /// <summary>Where lines go; Unity's console unless a test replaces it.</summary>
        public static Action<int, string> Sink = (level, msg) =>
        {
            if (level >= 2) UnityEngine.Debug.LogError(msg);
            else if (level == 1) UnityEngine.Debug.LogWarning(msg);
            else UnityEngine.Debug.Log(msg);
        };

        public static void Info(string m) => Sink(0, "[VisionForgeX] " + m);
        public static void Warn(string m) => Sink(1, "[VisionForgeX] " + m);
        public static void Error(string m) => Sink(2, "[VisionForgeX] " + m);
    }
}
