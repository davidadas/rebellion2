using Rebellion.Util.Serialization;
using UnityEngine;

/// <summary>
/// Defines a rectangle in source-resolution coordinates.
/// </summary>
[PersistableObject]
public class SourceRectLayout
{
    /// <summary>
    /// Gets or sets the horizontal coordinate.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Gets or sets the vertical coordinate.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Gets or sets the width.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the height.
    /// </summary>
    public int Height { get; set; }
}

/// <summary>
/// Defines a point in source-resolution coordinates.
/// </summary>
[PersistableObject]
public class SourcePointLayout
{
    /// <summary>
    /// Gets or sets the horizontal coordinate.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Gets or sets the vertical coordinate.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Converts the configured point to a Unity integer vector.
    /// </summary>
    /// <returns>The source position as a vector.</returns>
    public Vector2Int ToVector2Int()
    {
        return new Vector2Int(X, Y);
    }
}

/// <summary>
/// Defines active and inactive title artwork for a strategy window.
/// </summary>
[PersistableObject]
public class WindowTitleTheme
{
    /// <summary>
    /// Gets or sets the active image path.
    /// </summary>
    public string ActiveImagePath { get; set; }

    /// <summary>
    /// Gets or sets the inactive image path.
    /// </summary>
    public string InactiveImagePath { get; set; }
}

/// <summary>
/// Defines artwork for the supported states of a window tab.
/// </summary>
[PersistableObject]
public class WindowTabImageTheme
{
    /// <summary>
    /// Gets or sets the active image path.
    /// </summary>
    public string ActiveImagePath { get; set; }

    /// <summary>
    /// Gets or sets the inactive image path.
    /// </summary>
    public string InactiveImagePath { get; set; }

    /// <summary>
    /// Gets or sets the disabled image path.
    /// </summary>
    public string DisabledImagePath { get; set; }

    /// <summary>
    /// Gets or sets the empty image path.
    /// </summary>
    public string EmptyImagePath { get; set; }

    /// <summary>
    /// Gets the image path for a numeric tab state.
    /// </summary>
    /// <param name="state">Zero for active, one for inactive, or another value for disabled.</param>
    /// <returns>The image path for the requested state.</returns>
    public string GetImagePath(int state)
    {
        return state switch
        {
            0 => ActiveImagePath,
            1 => InactiveImagePath,
            _ => DisabledImagePath,
        };
    }

    /// <summary>
    /// Gets the image path for the tab's enabled and active state.
    /// </summary>
    /// <param name="enabled">Whether the tab is enabled.</param>
    /// <param name="active">Whether the tab is active.</param>
    /// <returns>The image path for the requested state.</returns>
    public string GetImagePath(bool enabled, bool active)
    {
        if (active)
            return ActiveImagePath;

        return enabled ? InactiveImagePath : DisabledImagePath;
    }

    /// <summary>
    /// Gets the image path for a tab whose content may be empty.
    /// </summary>
    /// <param name="hasItems">Whether the tab has content.</param>
    /// <param name="active">Whether the tab is active.</param>
    /// <returns>The image path for the requested state.</returns>
    public string GetImagePathForContent(bool hasItems, bool active)
    {
        if (active)
            return ActiveImagePath;

        return hasItems ? InactiveImagePath : EmptyImagePath;
    }
}

/// <summary>
/// Defines artwork and layout for a window button.
/// </summary>
[PersistableObject]
public class WindowButtonImageTheme
{
    /// <summary>
    /// Gets or sets the up image path.
    /// </summary>
    public string UpImagePath { get; set; }

    /// <summary>
    /// Gets or sets the down image path.
    /// </summary>
    public string DownImagePath { get; set; }

    /// <summary>
    /// Gets or sets the disabled image path.
    /// </summary>
    public string DisabledImagePath { get; set; }

    /// <summary>
    /// Gets or sets the source layout.
    /// </summary>
    public SourceRectLayout SourceLayout { get; set; }

    /// <summary>
    /// Gets the image path for the button's pressed state.
    /// </summary>
    /// <param name="pressed">Whether the button is pressed.</param>
    /// <returns>The image path for the requested state.</returns>
    public string GetImagePath(bool pressed)
    {
        return pressed ? DownImagePath : UpImagePath;
    }
}

/// <summary>
/// Parses optional serialized HTML colors without mutating their configured values.
/// </summary>
internal static class ThemeColorParser
{
    /// <summary>
    /// Parses a configured HTML color or returns a caller-selected fallback.
    /// </summary>
    /// <param name="configuredValue">The serialized HTML color value.</param>
    /// <param name="fallback">The value returned when configuration is absent or invalid.</param>
    /// <returns>The parsed color or the supplied fallback.</returns>
    public static Color Parse(string configuredValue, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
            return fallback;

        string htmlValue = configuredValue.StartsWith("#")
            ? configuredValue
            : "#" + configuredValue;
        return ColorUtility.TryParseHtmlString(htmlValue, out Color color) ? color : fallback;
    }
}
