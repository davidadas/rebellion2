using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Emits one correlated timing trace for a requested game startup.
/// </summary>
internal static class GameStartupTrace
{
    private static readonly Stopwatch _stopwatch = new Stopwatch();
    private static int _nextSequence;
    private static int _sequence;

    /// <summary>
    /// Gets whether a startup trace is currently collecting timings.
    /// </summary>
    internal static bool IsActive => _stopwatch.IsRunning;

    /// <summary>
    /// Starts a new startup trace and replaces any incomplete prior trace.
    /// </summary>
    /// <param name="description">The launch request being measured.</param>
    internal static void Begin(string description)
    {
        _sequence = ++_nextSequence;
        _stopwatch.Restart();
        Write(description);
    }

    /// <summary>
    /// Records a startup milestone against the active trace.
    /// </summary>
    /// <param name="description">The completed or pending startup phase.</param>
    internal static void Log(string description)
    {
        if (_stopwatch.IsRunning)
            Write(description);
    }

    /// <summary>
    /// Records the final startup milestone and stops the active trace.
    /// </summary>
    /// <param name="description">The state reached at startup completion.</param>
    internal static void Complete(string description)
    {
        if (!_stopwatch.IsRunning)
            return;

        Write(description);
        _stopwatch.Stop();
    }

    /// <summary>
    /// Writes one consistently formatted startup timing entry.
    /// </summary>
    /// <param name="description">The milestone description.</param>
    private static void Write(string description)
    {
        UnityEngine.Debug.Log(
            $"[GameStartup {_sequence} +{_stopwatch.Elapsed.TotalSeconds:F3}s] {description}"
        );
    }
}
