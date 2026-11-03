using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.GameEvents;
using Rebellion.Game.Messages;
using Rebellion.Game.Units;
using Rebellion.Util.Logging;
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
    private const string _preloadDirectoryName = "Preload";

    /// <summary>
    /// Opens the pack and scenario selected by the external content catalog.
    /// </summary>
    /// <returns>The loaded active content pack.</returns>
    public static ContentPack OpenActive()
    {
        return OpenActive(null, null);
    }

    /// <summary>
    /// Discovers valid content-pack definitions beneath an external content root.
    /// </summary>
    /// <param name="contentRootPath">The external content root to inspect.</param>
    /// <returns>The discovered pack definitions in stable directory order.</returns>
    public static IReadOnlyList<ContentPackDefinition> DiscoverPacks(string contentRootPath)
    {
        return DiscoverInstalledPacks(contentRootPath)
            .Where(pack => pack.Definition != null)
            .Select(pack => pack.Definition)
            .ToArray();
    }

    /// <summary>
    /// Discovers every installed content-pack directory and preserves per-pack read failures.
    /// </summary>
    /// <param name="contentRootPath">The external content root to inspect.</param>
    /// <returns>The installed packs in stable directory order.</returns>
    internal static IReadOnlyList<InstalledContentPack> DiscoverInstalledPacks(
        string contentRootPath
    )
    {
        string packsRoot = Path.Combine(Path.GetFullPath(contentRootPath), _packsDirectoryName);
        string modsRoot = ContentFileResolver.ResolveModsRootPath(contentRootPath);
        IEnumerable<string> installedPackRoots = Directory.Exists(packsRoot)
            ? Directory
                .EnumerateDirectories(packsRoot)
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            : Enumerable.Empty<string>();
        IEnumerable<string> authoredPackRoots = Directory.Exists(modsRoot)
            ? Directory
                .EnumerateDirectories(modsRoot)
                .Where(path => File.Exists(Path.Combine(path, _packFileName)))
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            : Enumerable.Empty<string>();

        return installedPackRoots.Concat(authoredPackRoots).Select(DiscoverInstalledPack).ToArray();
    }

    /// <summary>
    /// Tries to discover compatible mods for a selected pack without propagating content errors.
    /// </summary>
    /// <param name="contentRootPath">The external content root to inspect.</param>
    /// <param name="packID">The selected content-pack identifier.</param>
    /// <param name="disabledModIDs">The mod identifiers disabled by the player.</param>
    /// <param name="resolver">The discovered resolver when successful.</param>
    /// <param name="errorMessage">The content error when discovery fails.</param>
    /// <returns>True when the selected pack and its mods were discovered.</returns>
    internal static bool TryDiscoverModResolver(
        string contentRootPath,
        string packID,
        IEnumerable<string> disabledModIDs,
        out ContentFileResolver resolver,
        out string errorMessage
    )
    {
        try
        {
            resolver = DiscoverModResolver(contentRootPath, packID, disabledModIDs);
            errorMessage = string.Empty;
            return true;
        }
        catch (Exception exception) when (IsContentDiscoveryException(exception))
        {
            resolver = null;
            errorMessage = GetContentErrorMessage(exception);
            GameLogger.Warning($"Content pack '{packID}' could not be inspected: {errorMessage}");
            return false;
        }
    }

    /// <summary>
    /// Gets the pack identifier selected by the content catalog.
    /// </summary>
    /// <param name="contentRootPath">The external content root to inspect.</param>
    /// <returns>The catalog's default pack identifier.</returns>
    internal static string GetDefaultPackID(string contentRootPath)
    {
        string absoluteContentRoot = Path.GetFullPath(
            contentRootPath ?? throw new ArgumentNullException(nameof(contentRootPath))
        );
        ContentCatalogDefinition catalog =
            ContentDefinitionSerializer.Deserialize<ContentCatalogDefinition>(
                Path.Combine(absoluteContentRoot, _catalogFileName)
            );
        return catalog.ActivePackID;
    }

    /// <summary>
    /// Discovers compatible mods for a selected content pack without loading the pack data.
    /// </summary>
    /// <param name="contentRootPath">The external content root to inspect.</param>
    /// <param name="packID">The selected content-pack identifier.</param>
    /// <param name="disabledModIDs">The mod identifiers disabled by the player.</param>
    /// <returns>A resolver containing the selected pack's compatible mods.</returns>
    internal static ContentFileResolver DiscoverModResolver(
        string contentRootPath,
        string packID,
        IEnumerable<string> disabledModIDs
    )
    {
        IReadOnlyList<string> packRoots = ResolvePackRoots(contentRootPath, packID);
        ContentPackDefinition pack = ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(
            Path.Combine(packRoots[0], _packFileName)
        );
        return ContentFileResolver.Discover(
            contentRootPath,
            packRoots[0],
            pack.ID,
            pack.Version,
            disabledModIDs,
            packRoots.Skip(1)
        );
    }

    /// <summary>
    /// Creates an unmodified resolver for one installed pack and its inherited pack chain.
    /// </summary>
    /// <param name="contentRootPath">The external content root to inspect.</param>
    /// <param name="packID">The selected content-pack identifier.</param>
    /// <returns>A resolver over the selected pack and its inherited packs.</returns>
    public static ContentFileResolver CreatePackResolver(string contentRootPath, string packID)
    {
        string absoluteContentRoot = Path.GetFullPath(
            contentRootPath ?? throw new ArgumentNullException(nameof(contentRootPath))
        );
        IReadOnlyList<string> packRoots = ResolvePackRoots(absoluteContentRoot, packID);
        return new ContentFileResolver(
            absoluteContentRoot,
            packRoots[0],
            fallbackPackRootPaths: packRoots.Skip(1)
        );
    }

    /// <summary>
    /// Resolves an explicit authored folder over its installed base-pack chain.
    /// </summary>
    /// <param name="contentRootPath">The installed application content root.</param>
    /// <param name="packRootPath">The authored content-pack folder.</param>
    /// <param name="definition">The manifest from that folder.</param>
    /// <returns>The resolver without unrelated installed mod overrides.</returns>
    public static ContentFileResolver CreateFolderResolver(
        string contentRootPath,
        string packRootPath,
        ContentPackDefinition definition
    )
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        IReadOnlyList<string> inheritedRoots = string.IsNullOrWhiteSpace(definition.BasePackID)
            ? Array.Empty<string>()
            : CreatePackResolver(contentRootPath, definition.BasePackID).PackRootPaths;
        foreach (string inheritedRoot in inheritedRoots)
        {
            ContentPackDefinition inherited =
                ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(
                    Path.Combine(inheritedRoot, "pack.xml")
                );
            if (inherited.ID == definition.ID)
                throw new InvalidDataException("The content pack cannot inherit from itself.");
        }
        return new ContentFileResolver(
            contentRootPath,
            packRootPath,
            fallbackPackRootPaths: inheritedRoots
        );
    }

    /// <summary>
    /// Loads an exact content-pack folder without registering it or changing saved preferences.
    /// </summary>
    /// <param name="contentRootPath">The installed application and base-pack content.</param>
    /// <param name="packRootPath">The folder containing the requested pack.xml.</param>
    /// <param name="scenarioID">The scenario, or null to use the authored default.</param>
    /// <returns>The loaded content and scenario.</returns>
    public static ContentPack OpenFolder(
        string contentRootPath,
        string packRootPath,
        string scenarioID = null
    )
    {
        ContentPackDefinition definition =
            ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(
                Path.Combine(Path.GetFullPath(packRootPath), "pack.xml")
            );
        return LoadPack(
            CreateFolderResolver(contentRootPath, packRootPath, definition),
            definition,
            scenarioID
        );
    }

    /// <summary>
    /// Opens the selected pack/scenario, or the catalog default when either is empty.
    /// </summary>
    /// <param name="packIdOverride">Selected pack ID, or null/empty for the catalog default.</param>
    /// <param name="scenarioIdOverride">Selected scenario ID, or null/empty for the catalog default.</param>
    /// <returns>The loaded active content pack.</returns>
    public static ContentPack OpenActive(string packIdOverride, string scenarioIdOverride)
    {
        return OpenActive(packIdOverride, scenarioIdOverride, null);
    }

    /// <summary>
    /// Opens the selected pack/scenario while omitting disabled compatible mods.
    /// </summary>
    /// <param name="packIdOverride">Selected pack ID, or null/empty for the catalog default.</param>
    /// <param name="scenarioIdOverride">Selected scenario ID, or null/empty for the catalog default.</param>
    /// <param name="disabledModIDs">Mod IDs that must not enter the active content layers.</param>
    /// <returns>The loaded active content pack.</returns>
    public static ContentPack OpenActive(
        string packIdOverride,
        string scenarioIdOverride,
        IEnumerable<string> disabledModIDs
    )
    {
        return OpenActive(
            ResolveContentRootPath(),
            packIdOverride,
            scenarioIdOverride,
            disabledModIDs
        );
    }

    /// <summary>
    /// Opens the selected pack from an explicit external content root.
    /// </summary>
    /// <param name="contentRootPath">The external content root to inspect.</param>
    /// <returns>The loaded active content pack.</returns>
    internal static ContentPack OpenActive(string contentRootPath)
    {
        return OpenActive(contentRootPath, null, null, null);
    }

    /// <summary>
    /// Opens the selected pack from an explicit content root, falling back to the
    /// catalog default when the selection can't be opened.
    /// </summary>
    /// <param name="contentRootPath">The external content root to inspect.</param>
    /// <param name="packIdOverride">Selected pack ID, or null/empty for the catalog default.</param>
    /// <param name="scenarioIdOverride">Selected scenario ID, or null/empty for the catalog default.</param>
    /// <param name="disabledModIDs">Mod IDs that must not enter the active content layers.</param>
    /// <returns>The loaded active content pack.</returns>
    internal static ContentPack OpenActive(
        string contentRootPath,
        string packIdOverride,
        string scenarioIdOverride,
        IEnumerable<string> disabledModIDs = null
    )
    {
        string absoluteContentRoot = Path.GetFullPath(
            contentRootPath ?? throw new ArgumentNullException(nameof(contentRootPath))
        );
        ContentCatalogDefinition catalog =
            ContentDefinitionSerializer.Deserialize<ContentCatalogDefinition>(
                Path.Combine(absoluteContentRoot, _catalogFileName)
            );

        string packID = string.IsNullOrWhiteSpace(packIdOverride)
            ? catalog.ActivePackID
            : packIdOverride;
        string scenarioID = string.IsNullOrWhiteSpace(scenarioIdOverride)
            ? catalog.ActiveScenarioID
            : scenarioIdOverride;
        if (string.IsNullOrWhiteSpace(packID))
            throw new InvalidDataException(
                "No active content pack is selected (set ContentCatalog.ActivePackID or a user selection)."
            );

        bool usingOverride =
            !string.IsNullOrWhiteSpace(packIdOverride)
            && !string.Equals(packID, catalog.ActivePackID, StringComparison.Ordinal);
        try
        {
            return OpenPack(absoluteContentRoot, packID, scenarioID, disabledModIDs);
        }
        catch (Exception) when (usingOverride && !string.IsNullOrWhiteSpace(catalog.ActivePackID))
        {
            // Selected pack unavailable; fall back to the shipped default.
            return OpenPack(
                absoluteContentRoot,
                catalog.ActivePackID,
                catalog.ActiveScenarioID,
                disabledModIDs
            );
        }
    }

    /// <summary>
    /// Opens one exact pack and scenario without falling back to the catalog selection.
    /// </summary>
    /// <param name="contentRootPath">The external content root to inspect.</param>
    /// <param name="packID">The required pack identifier.</param>
    /// <param name="scenarioID">The required scenario identifier.</param>
    /// <param name="disabledModIDs">Mod identifiers that must remain disabled.</param>
    /// <returns>The exact loaded content pack.</returns>
    internal static ContentPack OpenExact(
        string contentRootPath,
        string packID,
        string scenarioID,
        IEnumerable<string> disabledModIDs
    )
    {
        if (string.IsNullOrWhiteSpace(packID))
            throw new InvalidDataException("The save does not identify a content pack.");
        if (string.IsNullOrWhiteSpace(scenarioID))
            throw new InvalidDataException("The save does not identify a content scenario.");

        string absoluteContentRoot = Path.GetFullPath(
            contentRootPath ?? throw new ArgumentNullException(nameof(contentRootPath))
        );
        return OpenPack(absoluteContentRoot, packID, scenarioID, disabledModIDs);
    }

    /// <summary>
    /// Loads and validates a specific pack + scenario from a resolved content root.
    /// </summary>
    /// <param name="absoluteContentRoot">The resolved external content root.</param>
    /// <param name="packID">The pack ID to open.</param>
    /// <param name="scenarioID">The scenario ID to open, or empty for the pack default.</param>
    /// <param name="disabledModIDs">Mod IDs that must not enter the active content layers.</param>
    /// <returns>The loaded content pack.</returns>
    private static ContentPack OpenPack(
        string absoluteContentRoot,
        string packID,
        string scenarioID,
        IEnumerable<string> disabledModIDs
    )
    {
        IReadOnlyList<string> packRoots = ResolvePackRoots(absoluteContentRoot, packID);
        string packRoot = packRoots[0];
        ContentPackDefinition pack = ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(
            Path.Combine(packRoot, _packFileName)
        );
        if (!string.Equals(pack.ID, packID, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Content pack '{pack.ID}' does not match selection '{packID}'."
            );
        }

        ContentFileResolver fileResolver = ContentFileResolver.Discover(
            absoluteContentRoot,
            packRoot,
            pack.ID,
            pack.Version,
            disabledModIDs,
            packRoots.Skip(1)
        );

        return LoadPack(fileResolver, pack, scenarioID);
    }

    /// <summary>
    /// Loads a complete pack from an explicit resolver and manifest.
    /// </summary>
    /// <param name="fileResolver">The pack and its inherited content layers.</param>
    /// <param name="pack">The selected manifest.</param>
    /// <param name="scenarioID">The scenario to load, or empty for the manifest default.</param>
    /// <returns>The validated pack and its game-data catalogs.</returns>
    internal static ContentPack LoadPack(
        ContentFileResolver fileResolver,
        ContentPackDefinition pack,
        string scenarioID
    )
    {
        if (fileResolver == null)
            throw new ArgumentNullException(nameof(fileResolver));
        if (pack == null)
            throw new ArgumentNullException(nameof(pack));
        List<ContentFactionDefinition> factions = LoadDefinitions<ContentFactionDefinition>(
            fileResolver,
            pack.FactionPaths
        );
        List<ContentScenarioDefinition> scenarios = LoadDefinitions<ContentScenarioDefinition>(
            fileResolver,
            pack.ScenarioPaths
        );
        string resolvedScenarioID = string.IsNullOrWhiteSpace(scenarioID)
            ? pack.DefaultScenarioID
            : scenarioID;
        ContentScenarioDefinition scenario =
            scenarios.SingleOrDefault(candidate =>
                string.Equals(candidate.ID, resolvedScenarioID, StringComparison.Ordinal)
            )
            ?? throw new InvalidDataException(
                $"Content pack '{pack.ID}' has no scenario '{resolvedScenarioID}'."
            );

        ValidateDefinitions(pack, factions, scenarios, scenario);
        GameDataCatalog gameData = LoadGameData(fileResolver, pack, factions, scenario);
        return new ContentPack(
            fileResolver.ContentRootPath,
            fileResolver.PackRootPath,
            fileResolver,
            pack,
            scenario,
            factions,
            gameData,
            LoadPreloadManifests(fileResolver, pack.Preloads)
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
            return ResolveAppInstallationDirectory(dataDirectory.Parent);

        DirectoryInfo resourcesDirectory = dataDirectory.Parent;
        DirectoryInfo contentsDirectory = resourcesDirectory?.Parent;
        DirectoryInfo appDirectory = contentsDirectory?.Parent;
        bool hasMacBundleLayout =
            string.Equals(dataDirectory.Name, "Data", StringComparison.Ordinal)
            && string.Equals(resourcesDirectory?.Name, "Resources", StringComparison.Ordinal)
            && string.Equals(contentsDirectory?.Name, "Contents", StringComparison.Ordinal)
            && string.Equals(appDirectory?.Extension, ".app", StringComparison.OrdinalIgnoreCase);
        if (platform == RuntimePlatform.OSXPlayer || hasMacBundleLayout)
            return ResolveAppInstallationDirectory(appDirectory);

        return dataDirectory.Parent;
    }

    /// <summary>
    /// Resolves a macOS app's installation root, sharing the host's content with nested helper apps.
    /// </summary>
    /// <param name="appDirectory">The app bundle that owns the player data.</param>
    /// <returns>The host app's installation directory, or null for an incomplete path.</returns>
    private static DirectoryInfo ResolveAppInstallationDirectory(DirectoryInfo appDirectory)
    {
        DirectoryInfo directory = appDirectory?.Parent;
        while (
            string.Equals(directory?.Name, "Helpers", StringComparison.Ordinal)
            && string.Equals(directory.Parent?.Name, "Contents", StringComparison.Ordinal)
            && string.Equals(
                directory.Parent.Parent?.Extension,
                ".app",
                StringComparison.OrdinalIgnoreCase
            )
        )
            directory = directory.Parent.Parent.Parent;
        return directory;
    }

    /// <summary>
    /// Loads one application-level preload manifest.
    /// </summary>
    /// <param name="fileResolver">The layered content file resolver.</param>
    /// <param name="preloadID">The application preload identifier.</param>
    /// <returns>The validated preload manifest.</returns>
    internal static ContentPreloadManifest LoadApplicationPreloadManifest(
        ContentFileResolver fileResolver,
        string preloadID
    )
    {
        if (string.IsNullOrWhiteSpace(preloadID))
            throw new ArgumentException("A preload ID is required.", nameof(preloadID));

        ContentPreloadManifest manifest =
            ContentDefinitionSerializer.Deserialize<ContentPreloadManifest>(
                fileResolver.ResolveFile(
                    _applicationAddressPrefix + _preloadDirectoryName + "/" + preloadID + ".xml"
                )
                    ?? throw new FileNotFoundException(
                        $"Application preload '{preloadID}' was not found."
                    )
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
    /// <param name="fileResolver">The layered content file resolver.</param>
    /// <param name="pack">The selected pack definition.</param>
    /// <param name="factions">The pack's faction definitions.</param>
    /// <param name="scenario">The selected scenario definition.</param>
    /// <returns>The composed typed game-data catalog.</returns>
    private static GameDataCatalog LoadGameData(
        ContentFileResolver fileResolver,
        ContentPackDefinition pack,
        IReadOnlyList<ContentFactionDefinition> factions,
        ContentScenarioDefinition scenario
    )
    {
        GameConfig gameConfig = LoadGameConfig(fileResolver, pack.GameConfigPath);
        GameGenerationConfig generationConfig = DeserializeGameData<GameGenerationConfig>(
            fileResolver,
            scenario.GenerationConfigPath,
            nameof(GameGenerationConfig),
            RequireFile(fileResolver, _generationConfigSchemaRelativePath)
        );
        PlanetSector[] planetSectors = DeserializeGameData<PlanetSector[]>(
            fileResolver,
            pack.PlanetSectorsPath,
            "PlanetSectors"
        );
        Building[] buildings = DeserializeGameData<Building[]>(
            fileResolver,
            pack.BuildingsPath,
            "Buildings"
        );
        OfficerTraitDefinition[] officerTraits = string.IsNullOrWhiteSpace(pack.OfficerTraitsPath)
            ? Array.Empty<OfficerTraitDefinition>()
            : DeserializeGameData<OfficerTraitDefinition[]>(
                fileResolver,
                pack.OfficerTraitsPath,
                "OfficerTraits"
            );
        GameEvent[] gameEvents = DeserializeGameData<GameEvent[]>(
            fileResolver,
            pack.GameEventsPath,
            "GameEvents",
            RequireFile(fileResolver, _gameEventsSchemaRelativePath)
        );
        MessageDefinition[] messageDefinitions = DeserializeGameData<MessageDefinition[]>(
            fileResolver,
            pack.MessageDefinitionsPath,
            "MessageDefinitions",
            RequireFile(fileResolver, _messageDefinitionsSchemaRelativePath)
        );
        EncyclopediaEntries encyclopediaEntries = DeserializeGameData<EncyclopediaEntries>(
            fileResolver,
            pack.EncyclopediaEntriesPath,
            nameof(EncyclopediaEntries)
        );
        FactionThemes themes = DeserializeGameData<FactionThemes>(
            fileResolver,
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
                DeserializeGameData<Faction[]>(fileResolver, faction.FactionDataPath, "Factions")
            );
            capitalShips.AddRange(
                DeserializeGameData<CapitalShip[]>(
                    fileResolver,
                    faction.CapitalShipsPath,
                    "CapitalShips"
                )
            );
            starfighters.AddRange(
                DeserializeGameData<Starfighter[]>(
                    fileResolver,
                    faction.StarfightersPath,
                    "Starfighters"
                )
            );
            regiments.AddRange(
                DeserializeGameData<Regiment[]>(fileResolver, faction.RegimentsPath, "Regiments")
            );
            specialForces.AddRange(
                DeserializeGameData<SpecialForces[]>(
                    fileResolver,
                    faction.SpecialForcesPath,
                    "SpecialForces"
                )
            );
            officers.AddRange(
                DeserializeGameData<Officer[]>(fileResolver, faction.OfficersPath, "Officers")
            );
            foreach (
                EncyclopediaEntry entry in DeserializeGameData<EncyclopediaEntries>(
                    fileResolver,
                    faction.EncyclopediaEntriesPath,
                    nameof(EncyclopediaEntries)
                )
            )
                encyclopediaEntries.Add(entry);
            foreach (
                FactionTheme theme in DeserializeGameData<FactionThemes>(
                    fileResolver,
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
            themes,
            officerTraits
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
        return LoadGameConfig(
            new ContentFileResolver(contentRootPath, packRoot),
            packGameConfigPath
        );
    }

    /// <summary>
    /// Loads the layered game configuration through the active content resolver.
    /// </summary>
    /// <param name="fileResolver">The resolver supplying application, pack, and mod files.</param>
    /// <param name="packGameConfigPath">The optional pack-relative override path.</param>
    /// <returns>The merged, schema-validated game configuration.</returns>
    private static GameConfig LoadGameConfig(
        ContentFileResolver fileResolver,
        string packGameConfigPath
    )
    {
        XmlDocument defaults = LoadXmlDocument(
            RequireFile(fileResolver, _applicationGameConfigRelativePath)
        );

        XmlSchemaSet schemas = new XmlSchemaSet();
        using (
            XmlReader schemaReader = XmlReader.Create(
                RequireFile(fileResolver, _gameConfigSchemaRelativePath)
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
                LoadXmlDocument(RequirePackFile(fileResolver, packGameConfigPath))
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
    /// Resolves a required pack-scoped content file.
    /// </summary>
    /// <param name="fileResolver">The layered content resolver.</param>
    /// <param name="relativePath">The path relative to the pack scope.</param>
    /// <returns>The resolved absolute file path.</returns>
    private static string RequirePackFile(ContentFileResolver fileResolver, string relativePath)
    {
        return RequireFile(fileResolver, _packAddressPrefix + relativePath);
    }

    /// <summary>
    /// Resolves a required logical content address.
    /// </summary>
    /// <param name="fileResolver">The layered content resolver.</param>
    /// <param name="address">The scoped logical content address.</param>
    /// <returns>The resolved absolute file path.</returns>
    private static string RequireFile(ContentFileResolver fileResolver, string address)
    {
        return fileResolver.ResolveFile(address)
            ?? throw new FileNotFoundException($"Content definition not found: {address}");
    }

    /// <summary>
    /// Deserializes one typed game-data file with optional schema validation.
    /// </summary>
    /// <typeparam name="T">The expected data type.</typeparam>
    /// <param name="fileResolver">The layered content file resolver.</param>
    /// <param name="relativePath">The pack-relative XML file path.</param>
    /// <param name="rootName">The serialized XML root name.</param>
    /// <param name="schemaFilePath">The optional absolute schema file path.</param>
    /// <returns>The deserialized game data.</returns>
    private static T DeserializeGameData<T>(
        ContentFileResolver fileResolver,
        string relativePath,
        string rootName,
        string schemaFilePath = null
    )
        where T : class
    {
        string filePath = RequirePackFile(fileResolver, relativePath);
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
    /// <param name="fileResolver">The layered content file resolver.</param>
    /// <param name="paths">The pack-relative definition paths.</param>
    /// <returns>The deserialized definitions in declared order.</returns>
    private static List<T> LoadDefinitions<T>(
        ContentFileResolver fileResolver,
        IEnumerable<string> paths
    )
        where T : class
    {
        if (paths == null)
            return new List<T>();

        return paths
            .Select(path =>
                ContentDefinitionSerializer.Deserialize<T>(RequirePackFile(fileResolver, path))
            )
            .ToList();
    }

    /// <summary>
    /// Loads every preload manifest declared by a pack.
    /// </summary>
    /// <param name="fileResolver">The layered content file resolver.</param>
    /// <param name="preloads">The declared preload definitions.</param>
    /// <returns>The loaded preload manifests by identifier.</returns>
    private static IReadOnlyDictionary<string, ContentPreloadManifest> LoadPreloadManifests(
        ContentFileResolver fileResolver,
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

            ContentPreloadManifest manifest =
                ContentDefinitionSerializer.Deserialize<ContentPreloadManifest>(
                    RequirePackFile(fileResolver, preload.Path)
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
        if (string.IsNullOrWhiteSpace(pack.MainMenuMusicPath))
            throw new InvalidDataException("ContentPack.MainMenuMusicPath is required.");
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
    /// Reads one installed pack while retaining failures for the content-management UI.
    /// </summary>
    /// <param name="packRoot">The installed pack directory.</param>
    /// <returns>The installed pack description.</returns>
    private static InstalledContentPack DiscoverInstalledPack(string packRoot)
    {
        string directoryName = Path.GetFileName(packRoot);
        string definitionPath = Path.Combine(packRoot, _packFileName);
        try
        {
            ContentPackDefinition definition =
                ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(definitionPath);
            if (string.IsNullOrWhiteSpace(definition.ID))
            {
                const string message = "Content pack definition is missing required field 'ID'.";
                GameLogger.Warning(
                    $"Skipping invalid content pack definition '{definitionPath}': {message}"
                );
                return new InstalledContentPack(directoryName, directoryName, null, message);
            }

            if (string.IsNullOrWhiteSpace(definition.Version))
            {
                const string message =
                    "Content pack definition is missing required field 'Version'.";
                GameLogger.Warning(
                    $"Skipping invalid content pack definition '{definitionPath}': {message}"
                );
                return new InstalledContentPack(
                    definition.ID,
                    string.IsNullOrWhiteSpace(definition.DisplayName)
                        ? definition.ID
                        : definition.DisplayName,
                    null,
                    message
                );
            }

            return new InstalledContentPack(
                definition.ID,
                string.IsNullOrWhiteSpace(definition.DisplayName)
                    ? definition.ID
                    : definition.DisplayName,
                definition,
                string.Empty
            );
        }
        catch (Exception exception) when (IsContentDiscoveryException(exception))
        {
            string message = GetContentErrorMessage(exception);
            GameLogger.Warning(
                $"Skipping invalid content pack definition '{definitionPath}': {message}"
            );
            return new InstalledContentPack(directoryName, directoryName, null, message);
        }
    }

    /// <summary>
    /// Returns whether an exception represents an installed-content discovery failure.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns>True when the failure belongs to content discovery.</returns>
    private static bool IsContentDiscoveryException(Exception exception)
    {
        return exception
            is InvalidDataException
                or IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or XmlException;
    }

    /// <summary>
    /// Extracts the useful content failure from serializer exception wrappers.
    /// </summary>
    /// <param name="exception">The content discovery exception.</param>
    /// <returns>The player-facing failure detail.</returns>
    private static string GetContentErrorMessage(Exception exception)
    {
        Exception detail = exception;
        while (detail.InnerException != null)
            detail = detail.InnerException;
        return detail.Message;
    }

    /// <summary>
    /// Resolves the selected pack and its inherited packs in fallback order.
    /// </summary>
    /// <param name="contentRootPath">The installed Content directory.</param>
    /// <param name="packID">The stable pack ID selected by the catalog.</param>
    /// <returns>The absolute pack roots from the selected pack through its furthest ancestor.</returns>
    private static IReadOnlyList<string> ResolvePackRoots(string contentRootPath, string packID)
    {
        string packsRootPath = Path.Combine(Path.GetFullPath(contentRootPath), _packsDirectoryName);
        if (
            !Directory.Exists(packsRootPath)
            && !Directory.Exists(ContentFileResolver.ResolveModsRootPath(contentRootPath))
        )
            throw new DirectoryNotFoundException(
                $"Content packs directory not found: {packsRootPath}"
            );

        Dictionary<string, List<(string Root, ContentPackDefinition Definition)>> packs =
            new Dictionary<string, List<(string Root, ContentPackDefinition Definition)>>(
                StringComparer.Ordinal
            );
        foreach (var (definitionPath, candidate) in ReadPackDefinitions(contentRootPath))
        {
            string candidateRoot = Path.GetDirectoryName(definitionPath);
            if (string.IsNullOrWhiteSpace(candidate.ID))
                continue;
            if (!packs.TryGetValue(candidate.ID, out var matches))
            {
                matches = new List<(string Root, ContentPackDefinition Definition)>();
                packs.Add(candidate.ID, matches);
            }
            matches.Add((candidateRoot, candidate));
        }

        List<string> roots = new List<string>();
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
        string currentPackID = packID;
        while (!string.IsNullOrWhiteSpace(currentPackID))
        {
            if (!visited.Add(currentPackID))
            {
                throw new InvalidDataException(
                    $"Content pack inheritance contains a cycle at '{currentPackID}'."
                );
            }
            if (!packs.TryGetValue(currentPackID, out var matches))
            {
                throw new InvalidDataException(
                    $"No installed content pack declares ID '{currentPackID}'."
                );
            }
            if (matches.Count > 1)
            {
                throw new InvalidDataException(
                    $"Multiple content packs declare ID '{currentPackID}'."
                );
            }

            var currentPack = matches[0];
            if (string.IsNullOrWhiteSpace(currentPack.Definition.Version))
            {
                throw new InvalidDataException(
                    $"Content pack '{currentPackID}' must declare Version."
                );
            }
            roots.Add(currentPack.Root);
            currentPackID = currentPack.Definition.BasePackID;
        }

        return roots;
    }

    /// <summary>
    /// Reads installed definitions while isolating malformed authored drafts from unrelated packs.
    /// </summary>
    /// <param name="contentRootPath">The installed Content directory.</param>
    /// <returns>The readable definitions and their manifest paths.</returns>
    private static IEnumerable<(string Path, ContentPackDefinition Definition)> ReadPackDefinitions(
        string contentRootPath
    )
    {
        foreach (string path in EnumeratePackDefinitions(contentRootPath))
        {
            ContentPackDefinition definition;
            try
            {
                definition = ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(path);
            }
            catch (Exception exception) when (IsContentDiscoveryException(exception))
            {
                GameLogger.Warning(
                    $"Content pack could not be loaded: {path}. {GetContentErrorMessage(exception)}"
                );
                continue;
            }
            yield return (path, definition);
        }
    }

    /// <summary>
    /// Enumerates installed packs and authored packs in Mods using the same discovery order.
    /// </summary>
    /// <param name="contentRootPath">The installed Content directory.</param>
    /// <returns>The existing pack manifests in stable root and directory order.</returns>
    private static IEnumerable<string> EnumeratePackDefinitions(string contentRootPath)
    {
        string[] roots =
        {
            Path.Combine(Path.GetFullPath(contentRootPath), _packsDirectoryName),
            ContentFileResolver.ResolveModsRootPath(contentRootPath),
        };
        return roots
            .Where(Directory.Exists)
            .SelectMany(root =>
                Directory
                    .EnumerateDirectories(root)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            )
            .Select(path => Path.Combine(path, _packFileName))
            .Where(File.Exists);
    }

    /// <summary>
    /// Resolves the external content root for the editor or current player platform.
    /// </summary>
    /// <returns>The absolute external content root.</returns>
    public static string ResolveContentRootPath()
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
}
