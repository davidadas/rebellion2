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
    /// <returns>The fallback stream, or null when Unity should retain output ownership.</returns>
    public static WindowsWaveOutput Open(
        bool forceStereo,
        Func<bool?> hasOutputSession,
        Func<WindowsWaveOutput> openStereo
    )
    {
        if (!forceStereo && hasOutputSession() != false)
            return null;
        return openStereo();
    }
}
