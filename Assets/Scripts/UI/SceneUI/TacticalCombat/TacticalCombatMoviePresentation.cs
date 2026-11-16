using System;

/// <summary>
/// Transfers battle visibility and modal input ownership in the order required by full-screen battle video.
/// </summary>
public sealed class TacticalCombatMoviePresentation : IDisposable
{
    private readonly Action<bool> _setBattleActive;
    private readonly Action _enterMovieInput;
    private readonly Action _leaveMovieInput;

    public bool IsActive { get; private set; }

    /// <summary>
    /// Borrows the battle visibility and application input owners for repeated movie transitions.
    /// </summary>
    /// <param name="setBattleActive">Changes battle visibility and its associated input context.</param>
    /// <param name="enterMovieInput">Acquires modal input after the battle has hidden.</param>
    /// <param name="leaveMovieInput">Releases modal input before the battle reactivates.</param>
    public TacticalCombatMoviePresentation(
        Action<bool> setBattleActive,
        Action enterMovieInput,
        Action leaveMovieInput
    )
    {
        _setBattleActive =
            setBattleActive ?? throw new ArgumentNullException(nameof(setBattleActive));
        _enterMovieInput =
            enterMovieInput ?? throw new ArgumentNullException(nameof(enterMovieInput));
        _leaveMovieInput =
            leaveMovieInput ?? throw new ArgumentNullException(nameof(leaveMovieInput));
    }

    /// <summary>
    /// Hides the battle before acquiring movie input, or releases movie input before showing the battle.
    /// Repeating the current transition retains its existing ownership.
    /// </summary>
    /// <param name="active">Whether a movie currently owns presentation.</param>
    public void SetActive(bool active)
    {
        if (active == IsActive)
            return;
        if (active)
        {
            _setBattleActive(false);
            try
            {
                _enterMovieInput();
                IsActive = true;
            }
            catch
            {
                _setBattleActive(true);
                throw;
            }
        }
        else
        {
            IsActive = false;
            _leaveMovieInput();
            _setBattleActive(true);
        }
    }

    /// <summary>
    /// Releases an active modal input owner and restores the battle once.
    /// </summary>
    public void Dispose() => SetActive(false);
}
