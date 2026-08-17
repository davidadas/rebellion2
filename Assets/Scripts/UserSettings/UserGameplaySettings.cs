using System;

/// <summary>
/// Identifies a persisted gameplay option.
/// </summary>
public enum UserGameplayOption
{
    PauseAfterEnemyBombardment,
    PauseWhenSpaceBattleBegins,
}

/// <summary>
/// Stores user-configurable gameplay behavior.
/// </summary>
[Serializable]
public sealed class UserGameplaySettings
{
    public bool PauseAfterEnemyBombardment = true;
    public bool PauseWhenSpaceBattleBegins = true;

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
            default:
                throw new ArgumentOutOfRangeException(nameof(option), option, null);
        }
    }

    /// <summary>
    /// Restores gameplay options to their enabled defaults.
    /// </summary>
    public void RestoreDefaults()
    {
        PauseAfterEnemyBombardment = true;
        PauseWhenSpaceBattleBegins = true;
    }
}
