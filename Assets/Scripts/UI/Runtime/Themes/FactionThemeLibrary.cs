using System;
using System.Collections.Generic;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

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
    /// <param name="themes">The configured default and faction themes.</param>
    public FactionThemeLibrary(FactionThemes themes)
    {
        if (themes == null)
            throw new ArgumentNullException(nameof(themes));

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
    /// Returns the default theme for an empty identifier or the exact configured faction theme.
    /// </summary>
    /// <param name="factionInstanceId">
    /// The faction InstanceID. May be null or empty.
    /// </param>
    /// <returns>
    /// The matching FactionTheme, or the default theme.
    /// </returns>
    public FactionTheme GetTheme(string factionInstanceId)
    {
        if (TryGetTheme(factionInstanceId, out FactionTheme theme))
            return theme;

        throw new KeyNotFoundException(
            $"No faction theme is configured for faction '{factionInstanceId}'."
        );
    }

    /// <summary>
    /// Attempts to return the default theme for an empty identifier or the exact configured
    /// faction theme without throwing when the faction has no theme.
    /// </summary>
    /// <param name="factionInstanceId">The faction InstanceID. May be null or empty.</param>
    /// <param name="theme">The resolved theme, or null when no matching theme exists.</param>
    /// <returns>True when a theme was resolved; otherwise false.</returns>
    public bool TryGetTheme(string factionInstanceId, out FactionTheme theme)
    {
        if (string.IsNullOrEmpty(factionInstanceId))
        {
            theme = defaultTheme;
            return true;
        }

        return themesByFactionId.TryGetValue(factionInstanceId, out theme);
    }

    /// <summary>
    /// Resolves the faction background for a unit's encyclopedia artwork.
    /// </summary>
    /// <param name="entity">The unit shown by the entry.</param>
    /// <param name="owner">The faction assigned to the entry.</param>
    /// <returns>The background address, when configured for the unit type.</returns>
    internal string GetEncyclopediaArtworkBackground(BaseGameEntity entity, string owner)
    {
        if (string.IsNullOrWhiteSpace(owner) || !TryGetTheme(owner, out FactionTheme theme))
            return null;
        string unitKind = entity switch
        {
            Officer => nameof(Officer),
            CapitalShip => nameof(CapitalShip),
            Starfighter => nameof(Starfighter),
            _ => null,
        };
        return theme.ArtworkDefaults?.GetBackgroundImagePath(unitKind);
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
