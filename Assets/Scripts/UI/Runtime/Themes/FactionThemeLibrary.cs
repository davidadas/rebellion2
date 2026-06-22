using System;
using System.Collections.Generic;

/// <summary>
/// Loads and provides access to all faction themes defined in FactionThemes.xml.
/// Responsible only for presentation-layer theme lookup.
/// </summary>
public sealed class FactionThemeLibrary
{
    private const string _defaultThemeId = "DEFAULT";

    private readonly Dictionary<string, FactionTheme> themesByFactionId;
    private readonly List<FactionTheme> themesInLoadOrder;
    private readonly FactionTheme defaultTheme;

    /// <summary>
    /// Constructs the theme library and loads all faction themes.
    /// </summary>
    public FactionThemeLibrary()
    {
        FactionThemes themes = ResourceManager.GetConfig<FactionThemes>();

        themesByFactionId = new Dictionary<string, FactionTheme>();
        themesInLoadOrder = new List<FactionTheme>();

        foreach (FactionTheme theme in themes)
        {
            if (
                string.IsNullOrEmpty(theme.FactionInstanceID)
                || theme.FactionInstanceID.Equals(
                    _defaultThemeId,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                defaultTheme = theme;
                continue;
            }

            if (themesByFactionId.ContainsKey(theme.FactionInstanceID))
            {
                throw new InvalidOperationException(
                    $"Duplicate FactionTheme detected for FactionInstanceID '{theme.FactionInstanceID}'."
                );
            }

            themesByFactionId.Add(theme.FactionInstanceID, theme);
            themesInLoadOrder.Add(theme);
        }

        if (defaultTheme == null)
        {
            throw new InvalidOperationException(
                "No default FactionTheme defined. A theme with empty FactionInstanceID is required."
            );
        }
    }

    /// <summary>
    /// Returns the theme for the given faction.
    /// Falls back to the default theme if not found.
    /// </summary>
    /// <param name="factionInstanceId">
    /// The faction InstanceID. May be null or empty.
    /// </param>
    /// <returns>
    /// The matching FactionTheme, or the default theme.
    /// </returns>
    public FactionTheme GetTheme(string factionInstanceId)
    {
        if (themesByFactionId.TryGetValue(factionInstanceId, out FactionTheme theme))
        {
            return theme;
        }

        return defaultTheme;
    }

    public List<FactionTheme> GetAllThemes()
    {
        return new List<FactionTheme>(themesInLoadOrder);
    }
}
