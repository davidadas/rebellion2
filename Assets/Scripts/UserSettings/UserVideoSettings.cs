using System;

/// <summary>
/// Identifies a persisted tactical presentation option.
/// </summary>
public enum UserTacticalOption
{
    Starfield,

    Planet,

    Pyro,

    /// <summary>
    /// Controls high-detail presentation.
    /// </summary>
    HighDetail,

    /// <summary>
    /// Controls holocube presentation.
    /// </summary>
    Holocube,
}

/// <summary>
/// Stores user-configurable video settings.
/// </summary>
[Serializable]
public sealed class UserVideoSettings
{
    public int ResolutionWidth;
    public int ResolutionHeight;
    public int FullScreenMode;
    public bool ShowStarfield = true;
    public bool ShowPlanet = true;
    public bool ShowPyro = true;
    public bool HighDetail = true;
    public bool ShowHolocube = true;

    /// <summary>
    /// Gets whether a tactical presentation option is enabled.
    /// </summary>
    /// <param name="option">The tactical presentation option.</param>
    /// <returns>True when the option is enabled.</returns>
    public bool IsEnabled(UserTacticalOption option)
    {
        return option switch
        {
            UserTacticalOption.Starfield => ShowStarfield,
            UserTacticalOption.Planet => ShowPlanet,
            UserTacticalOption.Pyro => ShowPyro,
            UserTacticalOption.HighDetail => HighDetail,
            UserTacticalOption.Holocube => ShowHolocube,
            _ => throw new ArgumentOutOfRangeException(nameof(option), option, null),
        };
    }

    /// <summary>
    /// Sets whether a tactical presentation option is enabled.
    /// </summary>
    /// <param name="option">The tactical presentation option.</param>
    /// <param name="enabled">Whether the option is enabled.</param>
    public void SetEnabled(UserTacticalOption option, bool enabled)
    {
        switch (option)
        {
            case UserTacticalOption.Starfield:
                ShowStarfield = enabled;
                break;
            case UserTacticalOption.Planet:
                ShowPlanet = enabled;
                break;
            case UserTacticalOption.Pyro:
                ShowPyro = enabled;
                break;
            case UserTacticalOption.HighDetail:
                HighDetail = enabled;
                break;
            case UserTacticalOption.Holocube:
                ShowHolocube = enabled;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(option), option, null);
        }
    }

    /// <summary>
    /// Restores the tactical presentation options to their enabled defaults.
    /// </summary>
    public void RestoreTacticalDefaults()
    {
        ShowStarfield = true;
        ShowPlanet = true;
        ShowPyro = true;
        HighDetail = true;
        ShowHolocube = true;
    }
}
