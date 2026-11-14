/// <summary>
/// Keeps the HUD's autosave label visible long enough to read while autosaves start and finish.
/// </summary>
internal sealed class AutosaveIndicatorTiming
{
    public const float MinimumVisibleSeconds = 1f;

    // The presentation time at which the label most recently became visible.
    private float _shownAt;

    /// <summary>
    /// Gets whether the HUD should currently show the autosave label.
    /// </summary>
    public bool IsVisible { get; private set; }

    /// <summary>
    /// Applies the session's autosave state at the given presentation time.
    /// </summary>
    /// <param name="autosaving">Whether the session is waiting to capture or writing an autosave.</param>
    /// <param name="now">The unscaled presentation time in seconds.</param>
    /// <returns>True when the label's visibility changed.</returns>
    public bool Refresh(bool autosaving, float now)
    {
        if (autosaving && !IsVisible)
            _shownAt = now;

        bool visible = autosaving || IsVisible && now - _shownAt < MinimumVisibleSeconds;
        if (visible == IsVisible)
            return false;

        IsVisible = visible;
        return true;
    }
}
