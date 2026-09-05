using System;

/// <summary>
/// Identifies a persisted gameplay option.
/// </summary>
public enum UserGameplayOption
{
    PauseAfterEnemyBombardment,
    PauseWhenSpaceBattleBegins,
    AutosaveEnabled,
}

/// <summary>
/// Stores user-configurable gameplay behavior.
/// </summary>
[Serializable]
public sealed class UserGameplaySettings
{
    public const int DefaultAutosaveIntervalTicks = 100;
    public const int DefaultAutosavesToKeep = 5;
    public const int MinimumAutosaveIntervalTicks = 1;
    public const int MinimumAutosavesToKeep = 1;
    public const int MaximumAutosavesToKeep = 10;

    public bool AutosaveEnabled = true;
    public int AutosaveIntervalTicks = DefaultAutosaveIntervalTicks;
    public int AutosavesToKeep = DefaultAutosavesToKeep;
    public bool PauseAfterEnemyBombardment = true;
    public bool PauseWhenSpaceBattleBegins = true;
    public bool ShowMissionOdds = true;

    /// <summary>
    /// Gets whether a gameplay option is enabled.
    /// </summary>
    /// <param name="option">The gameplay option.</param>
    /// <returns>True when the option is enabled.</returns>
    public bool IsEnabled(UserGameplayOption option)
    {
        return option switch
        {
            UserGameplayOption.PauseAfterEnemyBombardment => PauseAfterEnemyBombardment,
            UserGameplayOption.PauseWhenSpaceBattleBegins => PauseWhenSpaceBattleBegins,
            UserGameplayOption.AutosaveEnabled => AutosaveEnabled,
            _ => throw new ArgumentOutOfRangeException(nameof(option), option, null),
        };
    }

    /// <summary>
    /// Sets whether a gameplay option is enabled.
    /// </summary>
    /// <param name="option">The gameplay option.</param>
    /// <param name="enabled">Whether the option is enabled.</param>
    public void SetEnabled(UserGameplayOption option, bool enabled)
    {
        switch (option)
        {
            case UserGameplayOption.PauseAfterEnemyBombardment:
                PauseAfterEnemyBombardment = enabled;
                break;
            case UserGameplayOption.PauseWhenSpaceBattleBegins:
                PauseWhenSpaceBattleBegins = enabled;
                break;
            case UserGameplayOption.AutosaveEnabled:
                AutosaveEnabled = enabled;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(option), option, null);
        }
    }

    /// <summary>
    /// Constrains persisted autosave values to valid runtime ranges.
    /// </summary>
    public void Normalize()
    {
        AutosaveIntervalTicks = Math.Max(MinimumAutosaveIntervalTicks, AutosaveIntervalTicks);
        AutosavesToKeep = Math.Max(
            MinimumAutosavesToKeep,
            Math.Min(MaximumAutosavesToKeep, AutosavesToKeep)
        );
    }

    /// <summary>
    /// Sets the autosave interval after constraining it to the supported range.
    /// </summary>
    /// <param name="ticks">The requested number of ticks between autosaves.</param>
    public void SetAutosaveIntervalTicks(int ticks)
    {
        AutosaveIntervalTicks = ticks;
        Normalize();
    }

    /// <summary>
    /// Sets the retained autosave count after constraining it to the supported range.
    /// </summary>
    /// <param name="count">The requested number of autosaves to retain.</param>
    public void SetAutosavesToKeep(int count)
    {
        AutosavesToKeep = count;
        Normalize();
    }

    /// <summary>
    /// Restores gameplay options to their enabled defaults.
    /// </summary>
    public void RestoreDefaults()
    {
        AutosaveEnabled = true;
        AutosaveIntervalTicks = DefaultAutosaveIntervalTicks;
        AutosavesToKeep = DefaultAutosavesToKeep;
        PauseAfterEnemyBombardment = true;
        PauseWhenSpaceBattleBegins = true;
        ShowMissionOdds = true;
    }
}
