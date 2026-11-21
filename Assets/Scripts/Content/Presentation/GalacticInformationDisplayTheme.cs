using System.Collections.Generic;
using Rebellion.Util.Serialization;
using UnityEngine;

/// <summary>
/// Defines artwork for the eight sections of the galactic-information frame.
/// </summary>
[PersistableObject]
public class GalacticInformationFrameTheme
{
    public string TopLeftImagePath { get; set; }

    public string TopRightImagePath { get; set; }

    public string BottomLeftImagePath { get; set; }

    public string BottomRightImagePath { get; set; }

    public string TopImagePath { get; set; }

    public string LeftImagePath { get; set; }

    public string RightImagePath { get; set; }

    public string BottomImagePath { get; set; }

    /// <summary>
    /// Gets the frame image path at the requested section index.
    /// </summary>
    /// <param name="index">The frame section index.</param>
    /// <returns>The image path, or <see langword="null"/> for an unsupported index.</returns>
    public string GetImagePath(int index)
    {
        return index switch
        {
            0 => TopLeftImagePath,
            1 => TopRightImagePath,
            2 => BottomLeftImagePath,
            3 => BottomRightImagePath,
            4 => TopImagePath,
            5 => LeftImagePath,
            6 => RightImagePath,
            7 => BottomImagePath,
            _ => null,
        };
    }
}

/// <summary>
/// Defines one galactic-information filter and its map thresholds.
/// </summary>
[PersistableObject]
public class GalacticInformationFilterTheme
{
    public GalacticInformationFilterMode Mode { get; set; }

    public string Label { get; set; }

    public string IconImagePath { get; set; }

    public string LegendImagePath { get; set; }

    public SourceRectLayout RowSourceLayout { get; set; }

    public int LowThreshold { get; set; }

    public int MediumThreshold { get; set; }

    public int HighThreshold { get; set; }
}

/// <summary>
/// Defines a galactic-information category and its submenu filters.
/// </summary>
[PersistableObject]
public class GalacticInformationCategoryTheme
{
    public string Label { get; set; }

    public string IconImagePath { get; set; }

    public SourceRectLayout RowSourceLayout { get; set; }

    public SourceRectLayout SubmenuSourceLayout { get; set; }

    public List<GalacticInformationFilterTheme> Filters { get; set; } =
        new List<GalacticInformationFilterTheme>();
}

/// <summary>
/// Defines the galactic-information menu, submenu, legend, and frame presentation.
/// </summary>
[PersistableObject]
public class GalacticInformationDisplayTheme
{
    public string BackgroundColorHex { get; set; }

    public string ActiveFilterLabelColorHex { get; set; }

    public SourceRectLayout ActiveFilterLabelSourceLayout { get; set; }

    public int ActiveFilterLabelFontSize { get; set; }

    public SourceRectLayout SelectorSourceLayout { get; set; }

    public SourcePointLayout LegendSourcePosition { get; set; }

    public SourcePointLayout CloseSourceInset { get; set; }

    public SourceRectLayout DisplayOffRowSourceLayout { get; set; }

    public SourceRectLayout MenuIconSourceLayout { get; set; }

    public SourceRectLayout MenuTextSourceLayout { get; set; }

    public SourceRectLayout SubmenuArrowSourceLayout { get; set; }

    public string DisplayOffLabel { get; set; }

    public string SubmenuArrowInactiveImagePath { get; set; }

    public string SubmenuArrowActiveImagePath { get; set; }

    public string CloseUpImagePath { get; set; }

    public string ClosePressedImagePath { get; set; }

    public GalacticInformationFrameTheme Frame { get; set; }

    public List<GalacticInformationCategoryTheme> Categories { get; set; } =
        new List<GalacticInformationCategoryTheme>();

    private Color backgroundColor;
    private bool backgroundColorParsed;
    private Color activeFilterLabelColor;
    private bool activeFilterLabelColorParsed;

    /// <summary>
    /// Gets the parsed display background color.
    /// </summary>
    /// <returns>The cached background color.</returns>
    public Color GetBackgroundColor()
    {
        if (!backgroundColorParsed)
        {
            backgroundColor = ThemeColorParser.Parse(BackgroundColorHex, Color.black);
            backgroundColorParsed = true;
        }

        return backgroundColor;
    }

    /// <summary>
    /// Gets the parsed active filter label color.
    /// </summary>
    /// <returns>The cached active filter label color.</returns>
    public Color GetActiveFilterLabelColor()
    {
        if (!activeFilterLabelColorParsed)
        {
            activeFilterLabelColor = ThemeColorParser.Parse(ActiveFilterLabelColorHex, Color.white);
            activeFilterLabelColorParsed = true;
        }

        return activeFilterLabelColor;
    }

    /// <summary>
    /// Finds the configured filter for a galactic-information mode.
    /// </summary>
    /// <param name="mode">The filter mode.</param>
    /// <returns>The matching filter, or <see langword="null"/>.</returns>
    public GalacticInformationFilterTheme GetFilter(GalacticInformationFilterMode mode)
    {
        foreach (GalacticInformationCategoryTheme category in Categories)
        {
            foreach (GalacticInformationFilterTheme filter in category.Filters)
            {
                if (filter?.Mode == mode)
                    return filter;
            }
        }

        return null;
    }
}
