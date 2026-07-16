using System.Collections.Generic;
using Rebellion.Util.Serialization;
using UnityEngine;

/// <summary>
/// Defines artwork for the eight sections of the galactic-information frame.
/// </summary>
[PersistableObject]
public class GalacticInformationFrameTheme
{
    /// <summary>
    /// Gets or sets the top left image path.
    /// </summary>
    public string TopLeftImagePath { get; set; }

    /// <summary>
    /// Gets or sets the top right image path.
    /// </summary>
    public string TopRightImagePath { get; set; }

    /// <summary>
    /// Gets or sets the bottom left image path.
    /// </summary>
    public string BottomLeftImagePath { get; set; }

    /// <summary>
    /// Gets or sets the bottom right image path.
    /// </summary>
    public string BottomRightImagePath { get; set; }

    /// <summary>
    /// Gets or sets the top image path.
    /// </summary>
    public string TopImagePath { get; set; }

    /// <summary>
    /// Gets or sets the left image path.
    /// </summary>
    public string LeftImagePath { get; set; }

    /// <summary>
    /// Gets or sets the right image path.
    /// </summary>
    public string RightImagePath { get; set; }

    /// <summary>
    /// Gets or sets the bottom image path.
    /// </summary>
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
    /// <summary>
    /// Gets or sets the mode.
    /// </summary>
    public GalacticInformationFilterMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the icon image path.
    /// </summary>
    public string IconImagePath { get; set; }

    /// <summary>
    /// Gets or sets the legend image path.
    /// </summary>
    public string LegendImagePath { get; set; }

    /// <summary>
    /// Gets or sets the row source layout.
    /// </summary>
    public SourceRectLayout RowSourceLayout { get; set; }

    /// <summary>
    /// Gets or sets the low threshold.
    /// </summary>
    public int LowThreshold { get; set; }

    /// <summary>
    /// Gets or sets the medium threshold.
    /// </summary>
    public int MediumThreshold { get; set; }

    /// <summary>
    /// Gets or sets the high threshold.
    /// </summary>
    public int HighThreshold { get; set; }
}

/// <summary>
/// Defines a galactic-information category and its submenu filters.
/// </summary>
[PersistableObject]
public class GalacticInformationCategoryTheme
{
    /// <summary>
    /// Gets or sets the label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the icon image path.
    /// </summary>
    public string IconImagePath { get; set; }

    /// <summary>
    /// Gets or sets the row source layout.
    /// </summary>
    public SourceRectLayout RowSourceLayout { get; set; }

    /// <summary>
    /// Gets or sets the submenu source layout.
    /// </summary>
    public SourceRectLayout SubmenuSourceLayout { get; set; }

    /// <summary>
    /// Gets or sets the filters.
    /// </summary>
    public List<GalacticInformationFilterTheme> Filters { get; set; } =
        new List<GalacticInformationFilterTheme>();
}

/// <summary>
/// Defines the galactic-information menu, submenu, legend, and frame presentation.
/// </summary>
[PersistableObject]
public class GalacticInformationDisplayTheme
{
    /// <summary>
    /// Gets or sets the background color hex.
    /// </summary>
    public string BackgroundColorHex { get; set; }

    /// <summary>
    /// Gets or sets the active filter label color hex.
    /// </summary>
    public string ActiveFilterLabelColorHex { get; set; }

    /// <summary>
    /// Gets or sets the active filter label source layout.
    /// </summary>
    public SourceRectLayout ActiveFilterLabelSourceLayout { get; set; }

    /// <summary>
    /// Gets or sets the active filter label font size.
    /// </summary>
    public int ActiveFilterLabelFontSize { get; set; }

    /// <summary>
    /// Gets or sets the selector source layout.
    /// </summary>
    public SourceRectLayout SelectorSourceLayout { get; set; }

    /// <summary>
    /// Gets or sets the legend source position.
    /// </summary>
    public SourcePointLayout LegendSourcePosition { get; set; }

    /// <summary>
    /// Gets or sets the close source inset.
    /// </summary>
    public SourcePointLayout CloseSourceInset { get; set; }

    /// <summary>
    /// Gets or sets the display off row source layout.
    /// </summary>
    public SourceRectLayout DisplayOffRowSourceLayout { get; set; }

    /// <summary>
    /// Gets or sets the menu icon source layout.
    /// </summary>
    public SourceRectLayout MenuIconSourceLayout { get; set; }

    /// <summary>
    /// Gets or sets the menu text source layout.
    /// </summary>
    public SourceRectLayout MenuTextSourceLayout { get; set; }

    /// <summary>
    /// Gets or sets the submenu arrow source layout.
    /// </summary>
    public SourceRectLayout SubmenuArrowSourceLayout { get; set; }

    /// <summary>
    /// Gets or sets the display off label.
    /// </summary>
    public string DisplayOffLabel { get; set; }

    /// <summary>
    /// Gets or sets the submenu arrow inactive image path.
    /// </summary>
    public string SubmenuArrowInactiveImagePath { get; set; }

    /// <summary>
    /// Gets or sets the submenu arrow active image path.
    /// </summary>
    public string SubmenuArrowActiveImagePath { get; set; }

    /// <summary>
    /// Gets or sets the close up image path.
    /// </summary>
    public string CloseUpImagePath { get; set; }

    /// <summary>
    /// Gets or sets the close pressed image path.
    /// </summary>
    public string ClosePressedImagePath { get; set; }

    /// <summary>
    /// Gets or sets the frame.
    /// </summary>
    public GalacticInformationFrameTheme Frame { get; set; }

    /// <summary>
    /// Gets or sets the categories.
    /// </summary>
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
