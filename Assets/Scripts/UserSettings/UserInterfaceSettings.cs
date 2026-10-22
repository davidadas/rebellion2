using System;

/// <summary>
/// Identifies a persisted user-interface presentation option.
/// </summary>
public enum UserInterfaceOption
{
    ShowIdleBar,
    KeepIdleBarOpen,
    ShowUnitCardStats,
    ShowFrameRate,
}

/// <summary>
/// Identifies the strategy-desktop corner occupied by the idle bar.
/// </summary>
public enum IdleBarLocation
{
    TopRight,
    TopLeft,
    BottomRight,
    BottomLeft,
}

/// <summary>
/// Stores user-configurable interface presentation settings.
/// </summary>
[Serializable]
public sealed class UserInterfaceSettings
{
    public bool ShowIdleBar = true;
    public bool KeepIdleBarOpen;
    public bool ShowUnitCardStats;
    public bool ShowFrameRate;
    public IdleBarLocation IdleBarLocation = global::IdleBarLocation.TopRight;

    /// <summary>
    /// Replaces unsupported persisted values with interface defaults.
    /// </summary>
    public void Normalize()
    {
        if (!Enum.IsDefined(typeof(IdleBarLocation), IdleBarLocation))
            IdleBarLocation = global::IdleBarLocation.TopRight;
    }

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
            UserInterfaceOption.ShowUnitCardStats => ShowUnitCardStats,
            UserInterfaceOption.ShowFrameRate => ShowFrameRate,
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
            case UserInterfaceOption.ShowUnitCardStats:
                ShowUnitCardStats = enabled;
                break;
            case UserInterfaceOption.ShowFrameRate:
                ShowFrameRate = enabled;
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
        ShowUnitCardStats = false;
        ShowFrameRate = false;
        IdleBarLocation = global::IdleBarLocation.TopRight;
    }
}
