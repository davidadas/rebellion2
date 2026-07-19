using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Describes the immutable presentation of one planet-system window.
/// </summary>
public sealed class PlanetSystemWindowRenderData
{
    /// <summary>
    /// Creates a planet-system window presentation.
    /// </summary>
    /// <param name="title">The displayed system title.</param>
    /// <param name="planets">The displayed planet presentations.</param>
    public PlanetSystemWindowRenderData(
        string title,
        IReadOnlyList<PlanetSystemPlanetRenderData> planets
    )
    {
        Title = title ?? string.Empty;
        Planets =
            planets == null
                ? Array.Empty<PlanetSystemPlanetRenderData>()
                : new List<PlanetSystemPlanetRenderData>(planets).AsReadOnly();
    }

    public IReadOnlyList<PlanetSystemPlanetRenderData> Planets { get; }

    public string Title { get; }
}

/// <summary>
/// Describes the immutable presentation of one planet inside a system window.
/// </summary>
public sealed class PlanetSystemPlanetRenderData
{
    /// <summary>
    /// Creates a planet presentation.
    /// </summary>
    /// <param name="planetIndex">The planet's stable position in the rendered sector.</param>
    /// <param name="galaxyOffset">The planet's projected galaxy offset from its parent system.</param>
    /// <param name="planetTexture">The planet image.</param>
    /// <param name="facilityTexture">The facility icon image.</param>
    /// <param name="facilityPressedTexture">The pressed facility icon image.</param>
    /// <param name="defenseTexture">The defense icon image.</param>
    /// <param name="defensePressedTexture">The pressed defense icon image.</param>
    /// <param name="fleetTexture">The fleet icon image.</param>
    /// <param name="fleetPressedTexture">The pressed fleet icon image.</param>
    /// <param name="missionTexture">The mission icon image.</param>
    /// <param name="missionPressedTexture">The pressed mission icon image.</param>
    /// <param name="headquartersTexture">The headquarters overlay image.</param>
    /// <param name="name">The displayed planet name.</param>
    /// <param name="nameColor">The displayed planet-name color.</param>
    /// <param name="selectedIcon">The selected planet icon.</param>
    /// <param name="hoveredIcon">The hovered planet icon.</param>
    /// <param name="energyBar">The energy status bar.</param>
    /// <param name="rawResourceBar">The raw-resource status bar.</param>
    /// <param name="supportBar">The popular-support status bar.</param>
    public PlanetSystemPlanetRenderData(
        int planetIndex,
        Vector2Int galaxyOffset,
        Texture2D planetTexture,
        Texture2D facilityTexture,
        Texture2D facilityPressedTexture,
        Texture2D defenseTexture,
        Texture2D defensePressedTexture,
        Texture2D fleetTexture,
        Texture2D fleetPressedTexture,
        Texture2D missionTexture,
        Texture2D missionPressedTexture,
        Texture2D headquartersTexture,
        string name,
        Color32 nameColor,
        PlanetIcon selectedIcon,
        PlanetIcon hoveredIcon,
        PlanetSystemBarRenderData energyBar,
        PlanetSystemBarRenderData rawResourceBar,
        PlanetSystemBarRenderData supportBar
    )
    {
        PlanetIndex = planetIndex;
        GalaxyOffset = galaxyOffset;
        PlanetTexture = planetTexture;
        FacilityTexture = facilityTexture;
        FacilityPressedTexture = facilityPressedTexture;
        DefenseTexture = defenseTexture;
        DefensePressedTexture = defensePressedTexture;
        FleetTexture = fleetTexture;
        FleetPressedTexture = fleetPressedTexture;
        MissionTexture = missionTexture;
        MissionPressedTexture = missionPressedTexture;
        HeadquartersTexture = headquartersTexture;
        Name = name ?? string.Empty;
        NameColor = nameColor;
        SelectedIcon = selectedIcon;
        HoveredIcon = hoveredIcon;
        EnergyBar = energyBar ?? throw new ArgumentNullException(nameof(energyBar));
        RawResourceBar = rawResourceBar ?? throw new ArgumentNullException(nameof(rawResourceBar));
        SupportBar = supportBar ?? throw new ArgumentNullException(nameof(supportBar));
    }

    public Texture2D DefensePressedTexture { get; }

    public Texture2D DefenseTexture { get; }

    public PlanetSystemBarRenderData EnergyBar { get; }

    public Texture2D FacilityPressedTexture { get; }

    public Texture2D FacilityTexture { get; }

    public Texture2D FleetPressedTexture { get; }

    public Texture2D FleetTexture { get; }

    public Texture2D HeadquartersTexture { get; }

    public PlanetIcon HoveredIcon { get; }

    public Texture2D MissionPressedTexture { get; }

    public Texture2D MissionTexture { get; }

    public string Name { get; }

    public Color32 NameColor { get; }

    public Texture2D PlanetTexture { get; }

    public int PlanetIndex { get; }

    public Vector2Int GalaxyOffset { get; }

    public PlanetSystemBarRenderData RawResourceBar { get; }

    public PlanetIcon SelectedIcon { get; }

    public PlanetSystemBarRenderData SupportBar { get; }
}

/// <summary>
/// Describes one immutable segmented or continuous planet status bar.
/// </summary>
public sealed class PlanetSystemBarRenderData
{
    /// <summary>
    /// Creates a planet status-bar presentation.
    /// </summary>
    /// <param name="visible">Whether the bar is displayed.</param>
    /// <param name="cellCount">The segmented cell count, or zero for a continuous bar.</param>
    /// <param name="litCells">The occupied segmented cell count.</param>
    /// <param name="fillRatio">The normalized continuous fill ratio.</param>
    /// <param name="fillColor">The occupied or continuous fill color.</param>
    /// <param name="emptyColor">The unoccupied segmented cell color.</param>
    /// <param name="backgroundColor">The bar background color.</param>
    public PlanetSystemBarRenderData(
        bool visible,
        int cellCount,
        int litCells,
        float fillRatio,
        Color32 fillColor,
        Color32 emptyColor,
        Color32 backgroundColor
    )
    {
        Visible = visible;
        CellCount = cellCount;
        LitCells = litCells;
        FillRatio = fillRatio;
        FillColor = fillColor;
        EmptyColor = emptyColor;
        BackgroundColor = backgroundColor;
    }

    public Color32 BackgroundColor { get; }

    public int CellCount { get; }

    public Color32 EmptyColor { get; }

    public Color32 FillColor { get; }

    public float FillRatio { get; }

    public int LitCells { get; }

    public bool Visible { get; }
}

/// <summary>
/// Identifies one interactive element in a rendered planet-system presentation.
/// </summary>
public sealed class PlanetSystemWindowElement
{
    /// <summary>
    /// Creates a semantic planet-system presentation element.
    /// </summary>
    /// <param name="planetIndex">The planet's position in the rendered sector.</param>
    /// <param name="icon">The represented overlay icon.</param>
    /// <param name="planetImage">Whether the element represents the planet image.</param>
    public PlanetSystemWindowElement(int planetIndex, PlanetIcon icon, bool planetImage)
    {
        PlanetIndex = planetIndex;
        Icon = icon;
        PlanetImage = planetImage;
    }

    public PlanetIcon Icon { get; }

    public int PlanetIndex { get; }

    public bool PlanetImage { get; }
}
