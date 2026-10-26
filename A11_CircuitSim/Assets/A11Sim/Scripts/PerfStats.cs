using System.Diagnostics;

namespace A11Sim
{
    /// Per-frame cost counters for the -a11perf benchmark (main thread).
    public static class PerfStats
    {
        public static double hudMs, viewMs, panelMs;
        public static int hudEvents;
        static readonly Stopwatch clock = Stopwatch.StartNew();

        public static double Now => clock.Elapsed.TotalMilliseconds;

        // Allocation probe: heap growth per named section (only when enabled by -a11perf).
        public static bool probe;
        public static readonly System.Collections.Generic.Dictionary<string, long> alloc = new();
        public static readonly System.Collections.Generic.Dictionary<string, double> time = new();
        static long last;

        public static void Begin()
        {
            if (probe) last = System.GC.GetTotalMemory(false);
        }

        public static void Mark(string section)
        {
            if (!probe) return;
            long h = System.GC.GetTotalMemory(false);
            if (h > last) alloc[section] = (alloc.TryGetValue(section, out var v) ? v : 0) + (h - last);
            last = System.GC.GetTotalMemory(false);
        }

        public static void Reset()
        {
            hudMs = viewMs = panelMs = 0;
            hudEvents = 0;
        }
    }
}
