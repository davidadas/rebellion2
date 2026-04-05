using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Loads and provides access to all faction themes defined in FactionThemes.xml.
/// Responsible only for presentation-layer theme lookup.
/// </summary>
public sealed class FactionThemeLibrary
{
    private readonly Dictionary<string, FactionTheme> themesByFactionId;
    private readonly FactionTheme defaultTheme;

    /// <summary>
    /// Constructs the theme library and loads all faction themes.
    /// </summary>
    /// <param name="resourceManager">
    /// Resource manager used to deserialize FactionThemes.xml.
    /// </param>
    public FactionThemeLibrary(IResourceManager resourceManager)
    {
        if (resourceManager == null)
        {
            throw new ArgumentNullException(nameof(resourceManager));
        }

        FactionThemes themes = resourceManager.GetConfig<FactionThemes>();

        themesByFactionId = new Dictionary<string, FactionTheme>();

        foreach (FactionTheme theme in themes)
        {
            if (
                string.IsNullOrEmpty(theme.FactionInstanceID)
                || theme.FactionInstanceID.Equals("default", System.StringComparison.OrdinalIgnoreCase)
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

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public List<FactionTheme> GetAllThemes()
    {
        return themesByFactionId.Values.ToList();
    }
}
