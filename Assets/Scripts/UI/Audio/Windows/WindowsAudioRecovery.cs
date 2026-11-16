using System;

/// <summary>
/// Opens the stereo fallback only when requested or when Windows reports no existing game output.
/// </summary>
internal static class WindowsAudioRecovery
{
    /// <summary>
    /// Resolves output ownership before opening a private stream.
    /// </summary>
    /// <param name="forceStereo">Whether the explicit stereo override was selected.</param>
    /// <param name="hasOutputSession">The existing Windows audio session query.</param>
    /// <param name="openStereo">The private stereo stream factory.</param>
    /// <param name="report">Receives the output ownership decision before opening a stream.</param>
    /// <returns>The fallback stream, or null when Unity should retain output ownership.</returns>
    public static WindowsWaveOutput Open(
        bool forceStereo,
        Func<bool?> hasOutputSession,
        Func<WindowsWaveOutput> openStereo,
        Action<string> report = null
    )
    {
        if (forceStereo)
            report?.Invoke("Opening stereo output: explicit override.");
        else
        {
            bool? existingSession = hasOutputSession();
            if (existingSession != false)
            {
                report?.Invoke(
                    existingSession == true
                        ? "Keeping Unity output: an existing game render session was found."
                        : "Keeping Unity output: session inspection was inconclusive."
                );
                return null;
            }
            report?.Invoke("Opening stereo output: no existing game render session was found.");
        }
        return openStereo();
    }
}
