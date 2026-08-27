using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Rebellion.Util.Common
{
    /// <summary>
    /// Temporary instrumentation for ranking per-tick system cost in headless runs.
    /// </summary>
    public static class TickProfiler
    {
        private static readonly Dictionary<string, double> _totalMs =
            new Dictionary<string, double>();
        private static readonly Dictionary<string, double> _windowMs =
            new Dictionary<string, double>();
        private static readonly Stopwatch _watch = new Stopwatch();
        private static string _current;

        /// <summary>
        /// Starts timing a phase.
        /// </summary>
        /// <param name="phase">The phase label to attribute elapsed time to.</param>
        public static void Begin(string phase)
        {
            _current = phase;
            _watch.Restart();
        }

        /// <summary>
        /// Stops timing the current phase and accumulates its elapsed time.
        /// </summary>
        public static void End()
        {
            if (_current == null)
                return;

            _watch.Stop();
            double ms = _watch.Elapsed.TotalMilliseconds;
            _totalMs[_current] = _totalMs.TryGetValue(_current, out double t) ? t + ms : ms;
            _windowMs[_current] = _windowMs.TryGetValue(_current, out double w) ? w + ms : ms;
            _current = null;
        }

        /// <summary>
        /// Logs and resets the rolling window averages every windowTicks ticks.
        /// </summary>
        /// <param name="tick">The current game tick.</param>
        /// <param name="windowTicks">The reporting cadence in ticks.</param>
        public static void Report(int tick, int windowTicks)
        {
            if (windowTicks <= 0 || tick % windowTicks != 0 || _windowMs.Count == 0)
                return;

            string summary = string.Join(
                ", ",
                _windowMs
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => $"{kv.Key}={kv.Value / windowTicks:F1}")
            );
            GameLogger.Warning($"[perf] t{tick} avg ms/tick: {summary}");
            _windowMs.Clear();
        }

        /// <summary>
        /// Logs cumulative totals for the whole run.
        /// </summary>
        /// <param name="ticks">The number of ticks the run executed.</param>
        public static void ReportTotals(int ticks)
        {
            if (_totalMs.Count == 0)
                return;

            string summary = string.Join(
                ", ",
                _totalMs
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => $"{kv.Key}={kv.Value:F0}ms ({kv.Value / ticks:F1}/tick)")
            );
            GameLogger.Warning($"[perf] TOTALS over {ticks} ticks: {summary}");
        }
    }
}
