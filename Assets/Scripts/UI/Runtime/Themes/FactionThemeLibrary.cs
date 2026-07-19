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
        FactionThemes themes =
            ResourceManager.GetConfig<FactionThemes>()
            ?? throw new InvalidOperationException("FactionThemes configuration is missing.");

        themesByFactionId = new Dictionary<string, FactionTheme>();
        themesInLoadOrder = new List<FactionTheme>();
        FactionTheme configuredDefaultTheme = null;

        foreach (FactionTheme theme in themes)
        {
            if (theme == null)
                throw new InvalidOperationException("FactionThemes contains a null theme entry.");

            if (
                string.IsNullOrEmpty(theme.FactionInstanceID)
                || theme.FactionInstanceID.Equals(
                    _defaultThemeId,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                if (configuredDefaultTheme != null)
                {
                    throw new InvalidOperationException(
                        "FactionThemes contains more than one default theme."
                    );
                }

                configuredDefaultTheme = theme;
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

        defaultTheme =
            configuredDefaultTheme
            ?? throw new InvalidOperationException(
                "FactionThemes requires one theme with FactionInstanceID 'DEFAULT'."
            );
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
        if (string.IsNullOrEmpty(factionInstanceId))
            return defaultTheme;

        if (themesByFactionId.TryGetValue(factionInstanceId, out FactionTheme theme))
            return theme;

        return defaultTheme;
    }

    /// <summary>
    /// Returns an isolated list of every configured non-default faction theme.
    /// </summary>
    /// <returns>A new list containing the configured faction themes in load order.</returns>
    public List<FactionTheme> GetAllThemes()
    {
        return new List<FactionTheme>(themesInLoadOrder);
    }
}
