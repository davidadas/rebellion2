using System;

/// <summary>
/// Captures the video options used for one battle's resident presentation resources.
/// </summary>
public readonly struct TacticalCombatRenderSettings
{
    public bool ShowStarfield { get; }
    public bool ShowPlanet { get; }
    public bool ShowPyro { get; }
    public bool HighDetail { get; }
    public bool ShowHolocube { get; }

    /// <summary>
    /// Captures the selected options without retaining mutable application settings.
    /// </summary>
    /// <param name="settings">The application video options at battle entry.</param>
    public TacticalCombatRenderSettings(UserVideoSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));
        ShowStarfield = settings.ShowStarfield;
        ShowPlanet = settings.ShowPlanet;
        ShowPyro = settings.ShowPyro;
        HighDetail = settings.HighDetail;
        ShowHolocube = settings.ShowHolocube;
    }
}
