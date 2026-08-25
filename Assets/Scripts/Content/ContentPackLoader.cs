using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Units;
using Rebellion.Generation;
using Rebellion.Util.Serialization;
using UnityEngine;

/// <summary>
/// Loads and validates the active loose-file content pack.
/// </summary>
public static class ContentPackLoader
{
    private const string _catalogFileName = "catalog.xml";
    private const string _contentDirectoryName = "Content";
    private const string _contentPathArgument = "-contentPath";
    private const string _applicationGameConfigRelativePath = "Application/Rules/game.xml";
    private const string _gameConfigSchemaRelativePath = "Application/Schemas/game-config.xsd";
    private const string _generationConfigSchemaRelativePath =
        "Application/Schemas/generation-config.xsd";
    private const string _gameEventsSchemaRelativePath = "Application/Schemas/game-events.xsd";
    private const string _messageDefinitionsSchemaRelativePath =
        "Application/Schemas/message-definitions.xsd";
    private const string _packAddressPrefix = "Pack/";
    private const string _packsDirectoryName = "Packs";
    private const string _packFileName = "pack.xml";
    private const string _applicationAddressPrefix = "Application/";
    private const string _applicationDirectoryName = "Application";
    private const string _preloadDirectoryName = "Preload";

    /// <summary>
    /// Opens the pack and scenario selected by the external content catalog.
    /// </summary>
    /// <returns>The loaded active content pack.</returns>
    public static ContentPack OpenActive()
    {
        return OpenActive(ResolveContentRootPath());
    }

    /// <summary>
    /// Opens the selected pack from an explicit external content root.
    /// </summary>
    /// <param name="contentRootPath">The external content root to inspect.</param>
    /// <returns>The loaded active content pack.</returns>
    internal static ContentPack OpenActive(string contentRootPath)
    {
        string absoluteContentRoot = Path.GetFullPath(
            contentRootPath ?? throw new ArgumentNullException(nameof(contentRootPath))
        );
        ContentCatalogDefinition catalog = DeserializeXml<ContentCatalogDefinition>(
            Path.Combine(absoluteContentRoot, _catalogFileName)
        );
        if (string.IsNullOrWhiteSpace(catalog.ActivePackID))
            throw new InvalidDataException("ContentCatalog.ActivePackID is required.");

        string packRoot = ResolvePackRoot(
            Path.Combine(absoluteContentRoot, _packsDirectoryName),
            catalog.ActivePackID
        );
        ContentPackDefinition pack = DeserializeXml<ContentPackDefinition>(
            Path.Combine(packRoot, _packFileName)
        );
        if (!string.Equals(pack.ID, catalog.ActivePackID, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Content pack '{pack.ID}' does not match catalog selection '{catalog.ActivePackID}'."
            );
        }

        List<ContentFactionDefinition> factions = LoadDefinitions<ContentFactionDefinition>(
            packRoot,
            pack.FactionPaths
        );
        List<ContentScenarioDefinition> scenarios = LoadDefinitions<ContentScenarioDefinition>(
            packRoot,
            pack.ScenarioPaths
        );
        string scenarioID = string.IsNullOrWhiteSpace(catalog.ActiveScenarioID)
            ? pack.DefaultScenarioID
            : catalog.ActiveScenarioID;
        ContentScenarioDefinition scenario =
            scenarios.SingleOrDefault(candidate =>
                string.Equals(candidate.ID, scenarioID, StringComparison.Ordinal)
            )
            ?? throw new InvalidDataException(
                $"Content pack '{pack.ID}' has no scenario '{scenarioID}'."
            );

        ValidateDefinitions(pack, factions, scenarios, scenario);
        GameDataCatalog gameData = LoadGameData(
            absoluteContentRoot,
            packRoot,
            pack,
            factions,
            scenario
        );
        return new ContentPack(
            absoluteContentRoot,
            packRoot,
            pack,
            scenario,
            factions,
            gameData,
            LoadPreloadManifests(packRoot, pack.Preloads)
        );
    }

    /// <summary>
    /// Resolves the external content directory beside a desktop player artifact.
    /// </summary>
    /// <param name="dataPath">The platform-specific Unity player data path.</param>
    /// <param name="platform">The current runtime platform.</param>
    /// <returns>The absolute external content root.</returns>
    internal static string ResolvePlayerContentRootPath(string dataPath, RuntimePlatform platform)
    {
        if (string.IsNullOrWhiteSpace(dataPath))
            throw new ArgumentException("A player data path is required.", nameof(dataPath));

        DirectoryInfo dataDirectory = new DirectoryInfo(Path.GetFullPath(dataPath));
        DirectoryInfo playerDirectory = ResolvePlayerDirectory(dataDirectory, platform);
        if (playerDirectory == null)
            throw new InvalidOperationException(
                "The player content directory could not be resolved."
            );

        return Path.Combine(playerDirectory.FullName, _contentDirectoryName);
    }

    /// <summary>
    /// Resolves the directory beside the player executable or application bundle.
    /// </summary>
    /// <param name="dataDirectory">Unity's platform-specific player data directory.</param>
    /// <param name="platform">The current runtime platform.</param>
    /// <returns>The directory that owns the external Content directory.</returns>
    private static DirectoryInfo ResolvePlayerDirectory(
        DirectoryInfo dataDirectory,
        RuntimePlatform platform
    )
    {
        if (
            string.Equals(dataDirectory.Name, "Contents", StringComparison.Ordinal)
            && string.Equals(
                dataDirectory.Parent?.Extension,
                ".app",
                StringComparison.OrdinalIgnoreCase
            )
        )
            return dataDirectory.Parent.Parent;

        DirectoryInfo resourcesDirectory = dataDirectory.Parent;
        DirectoryInfo contentsDirectory = resourcesDirectory?.Parent;
        DirectoryInfo appDirectory = contentsDirectory?.Parent;
        bool hasMacBundleLayout =
            string.Equals(dataDirectory.Name, "Data", StringComparison.Ordinal)
            && string.Equals(resourcesDirectory?.Name, "Resources", StringComparison.Ordinal)
            && string.Equals(contentsDirectory?.Name, "Contents", StringComparison.Ordinal)
            && string.Equals(appDirectory?.Extension, ".app", StringComparison.OrdinalIgnoreCase);
        if (platform == RuntimePlatform.OSXPlayer || hasMacBundleLayout)
            return appDirectory?.Parent;

        return dataDirectory.Parent;
    }

    /// <summary>
    /// Loads one application-level preload manifest.
    /// </summary>
    /// <param name="contentRootPath">The external content root.</param>
    /// <param name="preloadID">The application preload identifier.</param>
    /// <returns>The validated preload manifest.</returns>
    internal static ContentPreloadManifest LoadApplicationPreloadManifest(
        string contentRootPath,
        string preloadID
    )
    {
        if (string.IsNullOrWhiteSpace(preloadID))
            throw new ArgumentException("A preload ID is required.", nameof(preloadID));

        string absoluteContentRoot = Path.GetFullPath(
            contentRootPath ?? throw new ArgumentNullException(nameof(contentRootPath))
        );
        string preloadRoot = Path.Combine(
            absoluteContentRoot,
            _applicationDirectoryName,
            _preloadDirectoryName
        );
        ContentPreloadManifest manifest = DeserializeXml<ContentPreloadManifest>(
            ResolveSafePath(preloadRoot, preloadID + ".xml")
        );
        ValidatePreloadManifest(
            manifest,
            _applicationAddressPrefix,
            $"Application preload '{preloadID}'"
        );
        return manifest;
    }

    /// <summary>
    /// Loads and composes the typed game-data catalogs declared by a pack and scenario.
    /// </summary>
    /// <param name="contentRootPath">The absolute application content root.</param>
    /// <param name="packRoot">The absolute selected pack root.</param>
    /// <param name="pack">The selected pack definition.</param>
    /// <param name="factions">The pack's faction definitions.</param>
    /// <param name="scenario">The selected scenario definition.</param>
    /// <returns>The composed typed game-data catalog.</returns>
    private static GameDataCatalog LoadGameData(
        string contentRootPath,
        string packRoot,
        ContentPackDefinition pack,
        IReadOnlyList<ContentFactionDefinition> factions,
        ContentScenarioDefinition scenario
    )
    {
        GameConfig gameConfig = LoadGameConfig(contentRootPath, packRoot, pack.GameConfigPath);
        GameGenerationConfig generationConfig = DeserializeGameData<GameGenerationConfig>(
            packRoot,
            scenario.GenerationConfigPath,
            nameof(GameGenerationConfig),
            ResolveSafePath(contentRootPath, _generationConfigSchemaRelativePath)
        );
        PlanetSector[] planetSectors = DeserializeGameData<PlanetSector[]>(
            packRoot,
            pack.PlanetSectorsPath,
            "PlanetSectors"
        );
        Building[] buildings = DeserializeGameData<Building[]>(
            packRoot,
            pack.BuildingsPath,
            "Buildings"
        );
        GameEvent[] gameEvents = DeserializeGameData<GameEvent[]>(
            packRoot,
            pack.GameEventsPath,
            "GameEvents",
            ResolveSafePath(contentRootPath, _gameEventsSchemaRelativePath)
        );
        MessageDefinition[] messageDefinitions = DeserializeGameData<MessageDefinition[]>(
            packRoot,
            pack.MessageDefinitionsPath,
            "MessageDefinitions",
            ResolveSafePath(contentRootPath, _messageDefinitionsSchemaRelativePath)
        );
        EncyclopediaEntries encyclopediaEntries = DeserializeGameData<EncyclopediaEntries>(
            packRoot,
            pack.EncyclopediaEntriesPath,
            nameof(EncyclopediaEntries)
        );
        FactionThemes themes = DeserializeGameData<FactionThemes>(
            packRoot,
            pack.NeutralThemePath,
            nameof(FactionThemes)
        );

        List<Faction> factionData = new List<Faction>();
        List<CapitalShip> capitalShips = new List<CapitalShip>();
        List<Starfighter> starfighters = new List<Starfighter>();
        List<Regiment> regiments = new List<Regiment>();
        List<SpecialForces> specialForces = new List<SpecialForces>();
        List<Officer> officers = new List<Officer>();
        foreach (ContentFactionDefinition faction in factions)
        {
            factionData.AddRange(
                DeserializeGameData<Faction[]>(packRoot, faction.FactionDataPath, "Factions")
            );
            capitalShips.AddRange(
                DeserializeGameData<CapitalShip[]>(
                    packRoot,
                    faction.CapitalShipsPath,
                    "CapitalShips"
                )
            );
            starfighters.AddRange(
                DeserializeGameData<Starfighter[]>(
                    packRoot,
                    faction.StarfightersPath,
                    "Starfighters"
                )
            );
            regiments.AddRange(
                DeserializeGameData<Regiment[]>(packRoot, faction.RegimentsPath, "Regiments")
            );
            specialForces.AddRange(
                DeserializeGameData<SpecialForces[]>(
                    packRoot,
                    faction.SpecialForcesPath,
                    "SpecialForces"
                )
            );
            officers.AddRange(
                DeserializeGameData<Officer[]>(packRoot, faction.OfficersPath, "Officers")
            );
            foreach (
                EncyclopediaEntry entry in DeserializeGameData<EncyclopediaEntries>(
                    packRoot,
                    faction.EncyclopediaEntriesPath,
                    nameof(EncyclopediaEntries)
                )
            )
                encyclopediaEntries.Add(entry);
            foreach (
                FactionTheme theme in DeserializeGameData<FactionThemes>(
                    packRoot,
                    faction.ThemePath,
                    nameof(FactionThemes)
                )
            )
                themes.Add(theme);
        }

        return new GameDataCatalog(
            gameConfig,
            generationConfig,
            factionData.ToArray(),
            planetSectors,
            buildings,
            capitalShips.ToArray(),
            starfighters.ToArray(),
            regiments.ToArray(),
            specialForces.ToArray(),
            officers.ToArray(),
            gameEvents,
            messageDefinitions,
            encyclopediaEntries,
            themes
        );
    }

    /// <summary>
    /// Loads the application game-config defaults, applies the pack's optional
    /// sparse override, and validates the merged configuration.
    /// </summary>
    /// <param name="contentRootPath">The absolute application content root.</param>
    /// <param name="packRoot">The absolute selected pack root.</param>
    /// <param name="packGameConfigPath">The optional pack-relative override path.</param>
    /// <returns>The merged, schema-validated game configuration.</returns>
    internal static GameConfig LoadGameConfig(
        string contentRootPath,
        string packRoot,
        string packGameConfigPath
    )
    {
        XmlDocument defaults = LoadXmlDocument(
            ResolveSafePath(contentRootPath, _applicationGameConfigRelativePath)
        );

        XmlSchemaSet schemas = new XmlSchemaSet();
        using (
            XmlReader schemaReader = XmlReader.Create(
                ResolveSafePath(contentRootPath, _gameConfigSchemaRelativePath)
            )
        )
            schemas.Add(null, schemaReader);

        GameSerializer serializer = new GameSerializer(
            typeof(GameConfig),
            new GameSerializerSettings { RootName = nameof(GameConfig), Schemas = schemas }
        );
        object gameConfig = string.IsNullOrWhiteSpace(packGameConfigPath)
            ? serializer.Deserialize(defaults)
            : serializer.Deserialize(
                defaults,
                LoadXmlDocument(ResolveSafePath(packRoot, packGameConfigPath))
            );
        return gameConfig as GameConfig
            ?? throw new InvalidDataException("Failed to deserialize the merged game config.");
    }

    /// <summary>
    /// Loads one XML content file into a document.
    /// </summary>
    /// <param name="filePath">The absolute XML file path.</param>
    /// <returns>The loaded document.</returns>
    private static XmlDocument LoadXmlDocument(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Content definition not found: {filePath}");

        XmlDocument document = new XmlDocument();
        using XmlReader reader = XmlReader.Create(filePath);
        document.Load(reader);
        return document;
    }

    /// <summary>
    /// Deserializes one typed game-data file with optional schema validation.
    /// </summary>
    /// <typeparam name="T">The expected data type.</typeparam>
    /// <param name="packRoot">The absolute selected pack root.</param>
    /// <param name="relativePath">The pack-relative XML file path.</param>
    /// <param name="rootName">The serialized XML root name.</param>
    /// <param name="schemaFilePath">The optional absolute schema file path.</param>
    /// <returns>The deserialized game data.</returns>
    private static T DeserializeGameData<T>(
        string packRoot,
        string relativePath,
        string rootName,
        string schemaFilePath = null
    )
        where T : class
    {
        string filePath = ResolveSafePath(packRoot, relativePath);
        GameSerializerSettings settings = new GameSerializerSettings { RootName = rootName };
        if (!string.IsNullOrWhiteSpace(schemaFilePath))
        {
            XmlSchemaSet schemas = new XmlSchemaSet();
            using XmlReader schemaReader = XmlReader.Create(schemaFilePath);
            schemas.Add(null, schemaReader);
            settings.Schemas = schemas;
        }

        GameSerializer serializer = new GameSerializer(typeof(T), settings);
        using FileStream stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );
        return serializer.Deserialize(stream) as T
            ?? throw new InvalidDataException(
                $"Failed to deserialize content data: {relativePath}"
            );
    }

    /// <summary>
    /// Loads a sequence of XML definition files from a selected pack.
    /// </summary>
    /// <typeparam name="T">The expected definition type.</typeparam>
    /// <param name="packRoot">The absolute selected pack root.</param>
    /// <param name="paths">The pack-relative definition paths.</param>
    /// <returns>The deserialized definitions in declared order.</returns>
    private static List<T> LoadDefinitions<T>(string packRoot, IEnumerable<string> paths)
        where T : class
    {
        if (paths == null)
            return new List<T>();

        return paths.Select(path => DeserializeXml<T>(ResolveSafePath(packRoot, path))).ToList();
    }

    /// <summary>
    /// Loads every preload manifest declared by a pack.
    /// </summary>
    /// <param name="packRoot">The absolute pack root.</param>
    /// <param name="preloads">The declared preload definitions.</param>
    /// <returns>The loaded preload manifests by identifier.</returns>
    private static IReadOnlyDictionary<string, ContentPreloadManifest> LoadPreloadManifests(
        string packRoot,
        IEnumerable<ContentPreloadDefinition> preloads
    )
    {
        Dictionary<string, ContentPreloadManifest> manifests = new Dictionary<
            string,
            ContentPreloadManifest
        >(StringComparer.Ordinal);
        foreach (
            ContentPreloadDefinition preload in preloads
                ?? Enumerable.Empty<ContentPreloadDefinition>()
        )
        {
            if (
                preload == null
                || string.IsNullOrWhiteSpace(preload.ID)
                || string.IsNullOrWhiteSpace(preload.Path)
            )
                throw new InvalidDataException("Every content preload requires an ID and path.");
            if (manifests.ContainsKey(preload.ID))
                throw new InvalidDataException($"Duplicate content preload ID '{preload.ID}'.");

            ContentPreloadManifest manifest = DeserializeXml<ContentPreloadManifest>(
                ResolveSafePath(packRoot, preload.Path)
            );
            ValidatePreloadManifest(manifest, _packAddressPrefix, $"Pack preload '{preload.ID}'");
            manifests.Add(preload.ID, manifest);
        }

        return manifests;
    }

    /// <summary>
    /// Validates preload timing and address ownership.
    /// </summary>
    /// <param name="manifest">The manifest to validate.</param>
    /// <param name="requiredAddressPrefix">The required address prefix.</param>
    /// <param name="manifestName">The manifest name used in errors.</param>
    private static void ValidatePreloadManifest(
        ContentPreloadManifest manifest,
        string requiredAddressPrefix,
        string manifestName
    )
    {
        if (manifest.TexturesPerFrame <= 0)
        {
            throw new InvalidDataException(
                $"{manifestName} requires a positive TexturesPerFrame value."
            );
        }
        if (
            manifest.Textures == null
            || manifest.TextureDirectories == null
            || manifest.Audio == null
            || manifest.Models == null
        )
            throw new InvalidDataException(
                $"{manifestName} requires texture, audio, and model lists."
            );

        foreach (
            string address in manifest
                .Textures.Concat(manifest.TextureDirectories)
                .Concat(manifest.Audio)
                .Concat(manifest.Models)
        )
        {
            if (
                string.IsNullOrWhiteSpace(address)
                || !address.StartsWith(requiredAddressPrefix, StringComparison.Ordinal)
            )
            {
                throw new InvalidDataException(
                    $"{manifestName} address '{address}' must begin with '{requiredAddressPrefix}'."
                );
            }
        }
    }

    /// <summary>
    /// Validates pack identity, definition uniqueness, and scenario faction references.
    /// </summary>
    /// <param name="pack">The selected pack definition.</param>
    /// <param name="factions">The pack's faction definitions.</param>
    /// <param name="scenarios">The pack's scenario definitions.</param>
    /// <param name="activeScenario">The selected scenario definition.</param>
    private static void ValidateDefinitions(
        ContentPackDefinition pack,
        IReadOnlyList<ContentFactionDefinition> factions,
        IReadOnlyList<ContentScenarioDefinition> scenarios,
        ContentScenarioDefinition activeScenario
    )
    {
        if (string.IsNullOrWhiteSpace(pack.ID))
            throw new InvalidDataException("ContentPack.ID is required.");
        if (string.IsNullOrWhiteSpace(pack.Version))
            throw new InvalidDataException("ContentPack.Version is required.");
        EnsureUniqueIDs(factions.Select(faction => faction.ID), "faction");
        EnsureUniqueIDs(scenarios.Select(scenario => scenario.ID), "scenario");

        HashSet<string> factionIDs = factions
            .Select(faction => faction.ID)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string factionID in activeScenario.PlayableFactionIDs)
        {
            if (!factionIDs.Contains(factionID))
            {
                throw new InvalidDataException(
                    $"Scenario '{activeScenario.ID}' references missing faction '{factionID}'."
                );
            }
        }

        if (
            string.IsNullOrWhiteSpace(activeScenario.DefaultPlayerFactionID)
            || !activeScenario.PlayableFactionIDs.Contains(
                activeScenario.DefaultPlayerFactionID,
                StringComparer.Ordinal
            )
        )
        {
            throw new InvalidDataException(
                $"Scenario '{activeScenario.ID}' requires a playable DefaultPlayerFactionID."
            );
        }
    }

    /// <summary>
    /// Validates that a sequence contains unique, non-empty content identifiers.
    /// </summary>
    /// <param name="ids">The identifiers to validate.</param>
    /// <param name="kind">The content kind used in validation errors.</param>
    private static void EnsureUniqueIDs(IEnumerable<string> ids, string kind)
    {
        HashSet<string> discovered = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidDataException($"A content {kind} ID is missing.");
            if (!discovered.Add(id))
                throw new InvalidDataException($"Duplicate content {kind} ID '{id}'.");
        }
    }

    /// <summary>
    /// Deserializes a plain XML content definition.
    /// </summary>
    /// <typeparam name="T">The expected definition type.</typeparam>
    /// <param name="filePath">The absolute XML file path.</param>
    /// <returns>The deserialized definition.</returns>
    private static T DeserializeXml<T>(string filePath)
        where T : class
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Content definition not found: {filePath}");

        System.Xml.Serialization.XmlSerializer serializer =
            new System.Xml.Serialization.XmlSerializer(typeof(T));
        using FileStream stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );
        return serializer.Deserialize(stream) as T
            ?? throw new InvalidDataException(
                $"Failed to deserialize content definition: {filePath}"
            );
    }

    /// <summary>
    /// Finds the pack directory whose definition declares the requested ID.
    /// </summary>
    /// <param name="packsRootPath">The directory containing installed packs.</param>
    /// <param name="packID">The stable pack ID selected by the catalog.</param>
    /// <returns>The absolute root of the selected pack.</returns>
    private static string ResolvePackRoot(string packsRootPath, string packID)
    {
        if (!Directory.Exists(packsRootPath))
            throw new DirectoryNotFoundException(
                $"Content packs directory not found: {packsRootPath}"
            );

        string selectedRoot = null;
        foreach (string candidateRoot in Directory.EnumerateDirectories(packsRootPath))
        {
            string definitionPath = Path.Combine(candidateRoot, _packFileName);
            if (!File.Exists(definitionPath))
                continue;

            ContentPackDefinition candidate = DeserializeXml<ContentPackDefinition>(definitionPath);
            if (!string.Equals(candidate.ID, packID, StringComparison.Ordinal))
                continue;
            if (selectedRoot != null)
                throw new InvalidDataException($"Multiple content packs declare ID '{packID}'.");

            selectedRoot = candidateRoot;
        }

        return selectedRoot
            ?? throw new InvalidDataException($"No installed content pack declares ID '{packID}'.");
    }

    /// <summary>
    /// Resolves the external content root for the editor or current player platform.
    /// </summary>
    /// <returns>The absolute external content root.</returns>
    private static string ResolveContentRootPath()
    {
        string commandLinePath = GetCommandLineContentPath();
        if (commandLinePath != null)
            return Path.GetFullPath(commandLinePath);

#if UNITY_EDITOR
        return Path.Combine(Application.dataPath, _contentDirectoryName);
#else
        return ResolvePlayerContentRootPath(Application.dataPath, Application.platform);
#endif
    }

    /// <summary>
    /// Reads an optional external content root override from the command line.
    /// </summary>
    /// <returns>The configured override path, or null when none was supplied.</returns>
    private static string GetCommandLineContentPath()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length - 1; index++)
        {
            if (
                string.Equals(
                    arguments[index],
                    _contentPathArgument,
                    StringComparison.OrdinalIgnoreCase
                ) && !string.IsNullOrWhiteSpace(arguments[index + 1])
            )
                return arguments[index + 1];
        }

        return null;
    }

    /// <summary>
    /// Resolves a relative content path while preventing traversal outside its root.
    /// </summary>
    /// <param name="rootPath">The absolute path boundary.</param>
    /// <param name="relativePath">The relative content path.</param>
    /// <returns>The resolved absolute path.</returns>
    private static string ResolveSafePath(string rootPath, string relativePath)
    {
        if (Path.IsPathRooted(relativePath?.Trim() ?? string.Empty))
            throw new ArgumentException("Content paths must be relative.", nameof(relativePath));

        string normalizedPath = relativePath?.Trim().Replace('\\', '/');
        if (string.IsNullOrEmpty(normalizedPath))
            throw new ArgumentException("A content path is required.", nameof(relativePath));

        string absoluteRoot = Path.GetFullPath(rootPath);
        string candidatePath = Path.GetFullPath(Path.Combine(absoluteRoot, normalizedPath));
        string requiredPrefix =
            absoluteRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidatePath.StartsWith(requiredPrefix, StringComparison.Ordinal))
            throw new ArgumentException("Content paths cannot leave their content root.");

        return candidatePath;
    }
}
