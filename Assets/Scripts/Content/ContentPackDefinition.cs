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
    [XmlElement]
    public string ID { get; set; }

    [XmlElement]
    public string Version { get; set; }

    [XmlElement]
    public string DisplayName { get; set; }

    [XmlElement]
    public string DefaultScenarioID { get; set; }

    [XmlElement]
    public string GameConfigPath { get; set; }

    [XmlElement]
    public string PlanetSystemsPath { get; set; }

    [XmlElement]
    public string BuildingsPath { get; set; }

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
