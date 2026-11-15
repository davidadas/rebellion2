/// <summary>
/// Keeps each save indicator visible long enough to notice while saves start and finish.
/// </summary>
internal sealed class SaveIndicatorTiming
{
    public const float MinimumVisibleSeconds = 1f;

    // The presentation time at which the indicator most recently became visible.
    private float _shownAt;
    private SaveGameKind? _activeSaveKind;

    /// <summary>
    /// Gets whether the HUD should currently show a save indicator.
    /// </summary>
    public bool IsVisible => SaveKind.HasValue;

    /// <summary>
    /// Gets the current label's save kind, including its minimum display duration after completion.
    /// </summary>
    public SaveGameKind? SaveKind { get; private set; }

    /// <summary>
    /// Applies the session's save state at the given presentation time.
    /// </summary>
    /// <param name="kind">The pending save kind, or null after saving completes.</param>
    /// <param name="now">The unscaled presentation time in seconds.</param>
    /// <returns>True when the indicator's visibility or label changed.</returns>
    public bool Refresh(SaveGameKind? kind, float now)
    {
        if (kind.HasValue && kind != _activeSaveKind)
            _shownAt = now;
        _activeSaveKind = kind;
        SaveGameKind? visibleKind =
            kind ?? (now - _shownAt < MinimumVisibleSeconds ? SaveKind : null);
        if (visibleKind == SaveKind)
            return false;

        SaveKind = visibleKind;
        return true;
    }
}
