using System;

/// <summary>
/// Identifies a persisted user-interface presentation option.
/// </summary>
public enum UserInterfaceOption
{
    ShowIdleBar,
    KeepIdleBarOpen,
}

/// <summary>
/// Stores user-configurable interface presentation settings.
/// </summary>
[Serializable]
public sealed class UserInterfaceSettings
{
    public bool ShowIdleBar = true;
    public bool KeepIdleBarOpen;

    /// <summary>
    /// Gets whether a user-interface presentation option is enabled.
    /// </summary>
    /// <param name="option">The user-interface option.</param>
    /// <returns>True when the option is enabled.</returns>
    public bool IsEnabled(UserInterfaceOption option)
    {
        return option switch
        {
            UserInterfaceOption.ShowIdleBar => ShowIdleBar,
            UserInterfaceOption.KeepIdleBarOpen => KeepIdleBarOpen,
            _ => throw new ArgumentOutOfRangeException(nameof(option), option, null),
        };
    }

    /// <summary>
    /// Sets whether a user-interface presentation option is enabled.
    /// </summary>
    /// <param name="option">The user-interface option.</param>
    /// <param name="enabled">Whether the option is enabled.</param>
    public void SetEnabled(UserInterfaceOption option, bool enabled)
    {
        switch (option)
        {
            case UserInterfaceOption.ShowIdleBar:
                ShowIdleBar = enabled;
                break;
            case UserInterfaceOption.KeepIdleBarOpen:
                KeepIdleBarOpen = enabled;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(option), option, null);
        }
    }

    /// <summary>
    /// Restores interface options to their defaults.
    /// </summary>
    public void RestoreDefaults()
    {
        ShowIdleBar = true;
        KeepIdleBarOpen = false;
    }
}
