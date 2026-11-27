using System.Collections.Generic;
using System.Xml.Serialization;

/// <summary>
/// Selects the active content pack and scenario from an external content root.
/// </summary>
[XmlRoot("ContentCatalog")]
public sealed class ContentCatalogDefinition
{
    [XmlElement]
    public string ActivePackID { get; set; }

    [XmlElement]
    public string ActiveScenarioID { get; set; }
}

/// <summary>
/// Declares one content pack's identity, data catalogs, factions, scenarios, and preload groups.
/// </summary>
[XmlRoot("ContentPack")]
public sealed class ContentPackDefinition
{
    // The built-in video used when the pack's startup intro slot is empty.
    public const string DefaultStartupIntroVideoPath = "Application/Boot/Videos/intro";

    // The built-in video used when the pack's opening crawl slot is empty.
    public const string DefaultOpeningCrawlVideoPath = "Application/Boot/Videos/opening-crawl";

    [XmlElement]
    public string ID { get; set; }

    [XmlElement]
    public string BasePackID { get; set; }

    [XmlElement]
    public string Version { get; set; }

    [XmlElement]
    public string DisplayName { get; set; }

    [XmlElement]
    public string DefaultScenarioID { get; set; }

    [XmlElement]
    public string MainMenuMusicPath { get; set; }

    /// <summary>
    /// Gets or sets the optional video shown first at startup.
    /// </summary>
    [XmlElement]
    public string StartupIntroVideoPath { get; set; }

    /// <summary>
    /// Gets or sets the optional opening crawl shown before the main menu.
    /// </summary>
    [XmlElement]
    public string OpeningCrawlVideoPath { get; set; }

    [XmlElement]
    public MainMenuLayout MainMenuLayout { get; set; }

    [XmlElement]
    public PlanetSectorWindowLayout PlanetSectorWindowLayout { get; set; }

    [XmlElement]
    public PlanetWindowHeaderLayout PlanetWindowHeaderLayout { get; set; }

    /// <summary>
    /// Gets or sets the prompt area in the shared confirmation dialog's source coordinates.
    /// </summary>
    [XmlElement]
    public SourceRectLayout ConfirmationMessageArea { get; set; }

    [XmlElement]
    public ConfirmationWindowArtwork ConfirmationWindowArtwork { get; set; }

    [XmlElement]
    public string GameConfigPath { get; set; }

    [XmlElement]
    public string PlanetSectorsPath { get; set; }

    [XmlElement]
    public string BuildingsPath { get; set; }

    [XmlElement]
    public string OfficerTraitsPath { get; set; }

    [XmlElement("TacticalCombatDefinitionsPath")]
    public string TacticalCombatPath { get; set; }

    [XmlElement]
    public string GameEventsPath { get; set; }

    [XmlElement]
    public string MessageDefinitionsPath { get; set; }

    [XmlElement]
    public string EncyclopediaEntriesPath { get; set; }

    [XmlElement]
    public string NeutralThemePath { get; set; }

    [XmlArray]
    [XmlArrayItem("Path")]
    public List<string> FactionPaths { get; set; } = new List<string>();

    [XmlArray]
    [XmlArrayItem("Path")]
    public List<string> ScenarioPaths { get; set; } = new List<string>();

    [XmlArray]
    [XmlArrayItem("Preload")]
    public List<ContentPreloadDefinition> Preloads { get; set; } =
        new List<ContentPreloadDefinition>();
}

/// <summary>
/// Stores pack-wide presentation overrides for the planet-sector window.
/// </summary>
public sealed class PlanetSectorWindowLayout
{
    public string BackgroundColorHex { get; set; }
    public SourcePointLayout SwapButton { get; set; }
    public SourcePointLayout CloseButton { get; set; }
}

/// <summary>
/// Overrides the shared header of the Defense, Fleet, Facilities, and Missions windows.
/// Positions are displacements from each window's authored header in source pixels.
/// </summary>
public sealed class PlanetWindowHeaderLayout
{
    public SourcePointLayout TitleOffset { get; set; }
    public SourcePointLayout CaptionOffset { get; set; }
    public SourcePointLayout OpenSectorOffset { get; set; }
    public SourcePointLayout MinimizeOffset { get; set; }
    public SourcePointLayout CloseOffset { get; set; }
    public string OpenSectorImagePath { get; set; }
    public string OpenSectorPressedImagePath { get; set; }
    public string MinimizeImagePath { get; set; }
    public string MinimizePressedImagePath { get; set; }
    public string CloseImagePath { get; set; }
    public string ClosePressedImagePath { get; set; }
}

/// <summary>
/// Overrides the artwork of the shared two-choice confirmation window.
/// </summary>
public sealed class ConfirmationWindowArtwork
{
    public string BackgroundImagePath { get; set; }
    public string YesImagePath { get; set; }
    public string YesPressedImagePath { get; set; }
    public string NoImagePath { get; set; }
    public string NoPressedImagePath { get; set; }
    public SourcePointLayout YesPosition { get; set; }
    public SourcePointLayout NoPosition { get; set; }
}

/// <summary>
/// Stores launch-slot offsets in the main menu's reference pixels, with positive Y down.
/// </summary>
public sealed class MainMenuLayout
{
    public const int ReferenceWidth = 2560;
    public const int ReferenceHeight = 1440;

    public int LeftLaunchX { get; set; }
    public int LeftLaunchY { get; set; }
    public int RightLaunchX { get; set; }
    public int RightLaunchY { get; set; }

    [XmlElement("Control")]
    public List<MainMenuControlLayout> Controls { get; set; } = new();

    /// <summary>
    /// Finds the optional artwork and coordinates for a menu control.
    /// </summary>
    /// <param name="id">The control identifier.</param>
    /// <returns>The configured control, or null to use its authored appearance.</returns>
    public MainMenuControlLayout FindControl(string id) =>
        Controls.Find(control => control.ID == id);

    /// <summary>
    /// Returns the two editable members for the specified launch slot.
    /// </summary>
    /// <param name="slot">The slot index in scenario faction order.</param>
    /// <returns>The horizontal and vertical members of the layout.</returns>
    public static (string X, string Y) GetLaunchMembers(int slot) =>
        slot == 0
            ? (nameof(LeftLaunchX), nameof(LeftLaunchY))
            : (nameof(RightLaunchX), nameof(RightLaunchY));

    /// <summary>
    /// Returns the stored offset of a launch button.
    /// </summary>
    /// <param name="slot">The slot index in scenario faction order.</param>
    /// <returns>The horizontal and vertical reference-pixel offsets.</returns>
    public (int X, int Y) GetLaunchOffset(int slot) =>
        slot == 0 ? (LeftLaunchX, LeftLaunchY) : (RightLaunchX, RightLaunchY);
}

/// <summary>
/// Overrides one main-menu control's artwork and position in reference pixels.
/// </summary>
public sealed class MainMenuControlLayout
{
    [XmlAttribute]
    public string ID { get; set; }

    public int X { get; set; }
    public int Y { get; set; }
    public string ImagePath { get; set; }
    public string ModelPath { get; set; }
    public string SelectionImagePath { get; set; }
    public int SelectionX { get; set; }
    public int SelectionY { get; set; }
}

/// <summary>
/// Describes one installed content-pack directory, including definitions that could not be read.
/// </summary>
internal sealed class InstalledContentPack
{
    internal string ID { get; }

    internal string DisplayName { get; }

    internal ContentPackDefinition Definition { get; }

    internal string ErrorMessage { get; }

    /// <summary>
    /// Creates an installed content-pack description.
    /// </summary>
    /// <param name="id">The definition ID, or the directory name when unreadable.</param>
    /// <param name="displayName">The player-facing pack name.</param>
    /// <param name="definition">The parsed definition when available.</param>
    /// <param name="errorMessage">The load error when the definition is unavailable.</param>
    internal InstalledContentPack(
        string id,
        string displayName,
        ContentPackDefinition definition,
        string errorMessage
    )
    {
        ID = id;
        DisplayName = displayName;
        Definition = definition;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// Associates a preload group identifier with its manifest.
/// </summary>
public sealed class ContentPreloadDefinition
{
    [XmlElement]
    public string ID { get; set; }

    [XmlElement]
    public string Path { get; set; }
}

/// <summary>
/// Declares one faction's data catalogs and presentation theme.
/// </summary>
[XmlRoot("Faction")]
public sealed class ContentFactionDefinition
{
    [XmlElement]
    public string ID { get; set; }

    [XmlElement]
    public string DisplayName { get; set; }

    [XmlElement]
    public string FactionDataPath { get; set; }

    [XmlElement]
    public string CapitalShipsPath { get; set; }

    [XmlElement]
    public string StarfightersPath { get; set; }

    [XmlElement]
    public string RegimentsPath { get; set; }

    [XmlElement]
    public string SpecialForcesPath { get; set; }

    [XmlElement]
    public string OfficersPath { get; set; }

    [XmlElement]
    public string EncyclopediaEntriesPath { get; set; }

    [XmlElement]
    public string ThemePath { get; set; }
}

/// <summary>
/// Declares one playable scenario and its generation configuration.
/// </summary>
[XmlRoot("Scenario")]
public sealed class ContentScenarioDefinition
{
    [XmlElement]
    public string ID { get; set; }

    [XmlElement]
    public string DisplayName { get; set; }

    [XmlElement]
    public string GenerationConfigPath { get; set; }

    [XmlElement]
    public string DefaultPlayerFactionID { get; set; }

    [XmlArray]
    [XmlArrayItem("FactionID")]
    public List<string> PlayableFactionIDs { get; set; } = new List<string>();
}

/// <summary>
/// Lists media that must be resident before a content phase becomes visible.
/// </summary>
[XmlRoot("ContentPreload")]
public sealed class ContentPreloadManifest
{
    [XmlElement]
    public int TexturesPerFrame { get; set; }

    [XmlArray]
    [XmlArrayItem("Path")]
    public List<string> Textures { get; set; } = new List<string>();

    [XmlArray]
    [XmlArrayItem("Path")]
    public List<string> TextureDirectories { get; set; } = new List<string>();

    [XmlArray]
    [XmlArrayItem("Path")]
    public List<string> Audio { get; set; } = new List<string>();

    [XmlArray]
    [XmlArrayItem("Path")]
    public List<string> Models { get; set; } = new List<string>();
}
