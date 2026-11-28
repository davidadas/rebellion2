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
    private const string _macApplicationDataDirectoryName = "Rebellion 2";
    private const string _macApplicationSupportDirectoryName = "Application Support";
    private const string _macLibraryDirectoryName = "Library";
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

        return installedPackRoots
            .Concat(authoredPackRoots)
            .Select(DiscoverInstalledPack)
            .Where(pack => pack != null)
            .ToArray();
    }

    /// <summary>
    /// Tries to discover compatible mods for a selected pack without propagating content errors.
    /// </summary>
    /// <param name="contentRootPath">The external content root to inspect.</param>
    /// <param name="packID">The selected content-pack identifier.</param>
    /// <param name="disabledModIDs">The mod identifiers disabled by the player.</param>
    /// <param name="resolver">The discovered resolver when successful.</param>
    /// <param name="errorMessage">The content error when discovery fails.</param>
    /// <param name="preferredModOrder">The pending order for the pack.</param>
    /// <returns>True when the selected pack and its mods were discovered.</returns>
    internal static bool TryDiscoverModResolver(
        string contentRootPath,
        string packID,
        IEnumerable<string> disabledModIDs,
        out ContentFileResolver resolver,
        out string errorMessage,
        IReadOnlyList<string> preferredModOrder = null
    )
    {
        try
        {
            resolver = DiscoverModResolver(
                contentRootPath,
                packID,
                disabledModIDs,
                preferredModOrder
            );
            errorMessage = string.Empty;
            return true;
        }
        catch (Exception exception) when (IsContentDiscoveryException(exception))
        {
            resolver = null;
            errorMessage = GetContentErrorMessage(exception);
            GameLogger.Warning($"Game pack '{packID}' could not be inspected: {errorMessage}");
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
    /// <param name="preferredModOrder">The order for the pack, or null to preserve legacy ordering.</param>
    /// <param name="appliedModIDs">The exact enabled set, when reading a saved selection.</param>
    /// <param name="appliedModVersions">The versions recorded for that set.</param>
    /// <returns>A resolver containing the selected pack's compatible mods.</returns>
    internal static ContentFileResolver DiscoverModResolver(
        string contentRootPath,
        string packID,
        IEnumerable<string> disabledModIDs,
        IReadOnlyList<string> preferredModOrder = null,
        IReadOnlyList<string> appliedModIDs = null,
        IReadOnlyList<string> appliedModVersions = null
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
            packRoots.Skip(1),
            preferredModOrder: preferredModOrder,
            appliedModIDs: appliedModIDs,
            appliedModVersions: appliedModVersions
        );
    }

    /// <summary>
    /// Lists the installed mod releases compatible with a game pack for authors selecting load rules.
    /// </summary>
    /// <param name="contentRootPath">The installed content directory.</param>
    /// <param name="packID">The game pack the working mod targets.</param>
    /// <returns>The compatible, unambiguous releases.</returns>
    public static IReadOnlyList<ContentModDefinition> DiscoverCompatibleMods(
        string contentRootPath,
        string packID
    ) => DiscoverModResolver(contentRootPath, packID, Array.Empty<string>()).AvailableMods;

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
        IReadOnlyList<string> inheritedRoots =
            string.IsNullOrWhiteSpace(definition.BasePackID)
            && definition.InheritedPacks is not { Count: > 0 }
                ? Array.Empty<string>()
                : ResolvePackRoots(
                    contentRootPath,
                    definition.BasePackID,
                    inheritedPacks: definition.InheritedPacks
                );
        foreach (string inheritedRoot in inheritedRoots)
        {
            ContentPackDefinition inherited =
                ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(
                    Path.Combine(inheritedRoot, "pack.xml")
                );
            if (inherited.ID == definition.ID)
                throw new InvalidDataException("The game pack cannot inherit from itself.");
        }
        return new ContentFileResolver(
            contentRootPath,
            packRootPath,
            fallbackPackRootPaths: inheritedRoots
        );
    }

    /// <summary>
    /// Resolves an authored mod on its declared base with its installed dependencies.
    /// </summary>
    /// <param name="contentRootPath">The installed application content.</param>
    /// <param name="modRootPath">The directory containing mod.xml.</param>
    /// <param name="mod">The manifest from that directory.</param>
    /// <returns>The isolated mod and base pack resolver.</returns>
    public static ContentFileResolver CreateModFolderResolver(
        string contentRootPath,
        string modRootPath,
        ContentModDefinition mod
    )
    {
        ContentFileResolver baseline = CreatePackResolver(contentRootPath, mod.BasePackID);
        ContentPackDefinition pack = ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(
            Path.Combine(baseline.PackRootPath, _packFileName)
        );
        if (pack.Version != mod.BasePackVersion)
            throw new InvalidOperationException(
                $"{mod.DisplayName} requires {mod.BasePackID} version {mod.BasePackVersion}, but {pack.Version} is installed."
            );
        if (mod.Requires.Count > 0)
            return ContentFileResolver.Discover(
                contentRootPath,
                baseline.PackRootPath,
                pack.ID,
                pack.Version,
                fallbackPackRootPaths: baseline.PackRootPaths.Skip(1),
                workingModRoot: modRootPath,
                workingModOverride: mod
            );
        return new ContentFileResolver(
            contentRootPath,
            baseline.PackRootPath,
            new[] { Path.Combine(modRootPath, "Content") },
            new[] { mod },
            fallbackPackRootPaths: baseline.PackRootPaths.Skip(1)
        );
    }

    /// <summary>
    /// Reads the selected pack manifest with its active mods' campaign overrides.
    /// </summary>
    /// <param name="resolver">The selected base and its ordered active mods.</param>
    /// <returns>The composed manifest with the selected base identity.</returns>
    public static ContentPackDefinition ReadModPackDefinition(ContentFileResolver resolver) =>
        ContentDefinitionSerializer.DeserializeText<ContentPackDefinition>(
            ReadModPackManifest(resolver)
        );

    /// <summary>
    /// Composes campaign overrides and optionally leaves out the file currently being edited.
    /// </summary>
    /// <param name="resolver">The base and all resolved mod layers.</param>
    /// <param name="excludedOverlay">The editor's local override file to omit from its baseline.</param>
    /// <returns>The effective pack manifest XML.</returns>
    public static string ReadModPackManifest(
        ContentFileResolver resolver,
        string excludedOverlay = null
    )
    {
        string text = File.ReadAllText(Path.Combine(resolver.PackRootPath, _packFileName));
        var root = System.Xml.Linq.XElement.Parse(text);
        if (root.Name == ContentPackageManifest.RootName)
            text = ContentPackageManifest.AsCampaignRoot(root).ToString();
        foreach (string path in resolver.ResolveModPackOverlays())
        {
            if (string.Equals(path, excludedOverlay, StringComparison.Ordinal))
                continue;
            string overlay = File.ReadAllText(path);
            ValidateModPackOverlay(overlay);
            text = ContentXmlOverrides.Merge(text, overlay);
        }
        return text;
    }

    /// <summary>
    /// Checks that a mod's campaign overrides preserve the base manifest's identity.
    /// </summary>
    /// <param name="overlay">The authored sparse pack manifest.</param>
    public static void ValidateModPackOverlay(string overlay)
    {
        var root = System.Xml.Linq.XElement.Parse(overlay);
        if (root.Name != "ContentPack" || !ContentXmlOverrides.IsOverlay(overlay))
            throw new InvalidDataException("A mod's pack.xml must inherit from its base pack.");
        if (
            root.Attribute("RemoveAttributes") != null
            || root.Elements()
                .Any(element =>
                    element.Name.LocalName
                        is "ID"
                            or "Version"
                            or "BasePackID"
                            or "DisplayName"
                            or "InheritedPacks"
                )
        )
            throw new InvalidDataException("Change the mod identity in its package manifest.");
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
        string manifest = ContentPackageManifest.Find(packRootPath, ContentPackageType.GamePack);
        if (ContentPackageManifest.ReadType(manifest) == ContentPackageType.Mod)
        {
            ContentModDefinition mod = ContentFileResolver.ReadValidatedMod(packRootPath);
            ContentFileResolver resolver = CreateModFolderResolver(
                contentRootPath,
                packRootPath,
                mod
            );
            return LoadPack(resolver, ReadModPackDefinition(resolver), scenarioID);
        }
        ContentPackDefinition definition =
            ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(manifest);
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
    /// <param name="getModLoadOrder">Provides the preferred order for the pack actually loaded.</param>
    /// <param name="reportRecovery">Reports why startup had to open a pack without the requested mods.</param>
    /// <param name="getModVersions">Provides the exact applied versions in order, if recorded.</param>
    /// <returns>The loaded active content pack.</returns>
    internal static ContentPack OpenActive(
        string contentRootPath,
        string packIdOverride,
        string scenarioIdOverride,
        IEnumerable<string> disabledModIDs = null,
        Func<string, IReadOnlyList<string>> getModLoadOrder = null,
        Action<string> reportRecovery = null,
        Func<string, IReadOnlyList<string>> getModVersions = null
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
                "No active game pack is selected (set ContentCatalog.ActivePackID or a user selection)."
            );

        bool usingOverride =
            !string.IsNullOrWhiteSpace(packIdOverride)
            && !string.Equals(packID, catalog.ActivePackID, StringComparison.Ordinal);
        try
        {
            return OpenStartupSelection(
                absoluteContentRoot,
                packID,
                scenarioID,
                disabledModIDs,
                string.IsNullOrWhiteSpace(scenarioIdOverride),
                getModLoadOrder?.Invoke(packID),
                getModVersions?.Invoke(packID),
                reportRecovery
            );
        }
        catch (Exception) when (usingOverride && !string.IsNullOrWhiteSpace(catalog.ActivePackID))
        {
            // Selected pack unavailable; fall back to the shipped default.
            return OpenStartupSelection(
                absoluteContentRoot,
                catalog.ActivePackID,
                catalog.ActiveScenarioID,
                disabledModIDs,
                true,
                getModLoadOrder?.Invoke(catalog.ActivePackID),
                getModVersions?.Invoke(catalog.ActivePackID),
                reportRecovery
            );
        }
    }

    /// <summary>
    /// Opens the remembered selection and recovers to the same pack's clean default if its mods fail.
    /// </summary>
    /// <param name="contentRootPath">The installed content root.</param>
    /// <param name="packID">The requested pack.</param>
    /// <param name="scenarioID">The requested scenario.</param>
    /// <param name="disabledModIDs">The disabled mod IDs.</param>
    /// <param name="allowModDefault">Whether a mod may set the initial scenario.</param>
    /// <param name="appliedModIDs">The last applied mod IDs, or null for legacy preferences.</param>
    /// <param name="appliedModVersions">The last applied mod releases, when recorded.</param>
    /// <param name="reportRecovery">The startup recovery callback, when recovery is enabled.</param>
    /// <returns>The requested selection or the clean pack after recovery.</returns>
    private static ContentPack OpenStartupSelection(
        string contentRootPath,
        string packID,
        string scenarioID,
        IEnumerable<string> disabledModIDs,
        bool allowModDefault,
        IReadOnlyList<string> appliedModIDs,
        IReadOnlyList<string> appliedModVersions,
        Action<string> reportRecovery
    )
    {
        try
        {
            ContentPack selected = OpenPack(
                contentRootPath,
                packID,
                scenarioID,
                disabledModIDs,
                allowModDefault: allowModDefault,
                preferredModOrder: appliedModIDs,
                appliedModVersions: appliedModVersions
            );
            string missing = appliedModIDs
                ?.Except(selected.FileResolver.Mods.Select(mod => mod.ID), StringComparer.Ordinal)
                .FirstOrDefault();
            if (reportRecovery != null && missing != null)
                throw new InvalidDataException($"Applied mod '{missing}' could not be loaded.");
            return selected;
        }
        catch (Exception exception)
            when (reportRecovery != null && IsContentDiscoveryException(exception))
        {
            ContentPack clean = OpenPack(
                contentRootPath,
                packID,
                null,
                disabledModIDs,
                preferredModOrder: Array.Empty<string>()
            );
            reportRecovery(
                $"Opened {clean.Definition.DisplayName} with mods off. {GetContentErrorMessage(exception)}"
            );
            return clean;
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
            throw new InvalidDataException("The save does not identify a game pack.");
        if (string.IsNullOrWhiteSpace(scenarioID))
            throw new InvalidDataException("The save does not identify a content scenario.");

        string absoluteContentRoot = Path.GetFullPath(
            contentRootPath ?? throw new ArgumentNullException(nameof(contentRootPath))
        );
        return OpenPack(absoluteContentRoot, packID, scenarioID, disabledModIDs);
    }

    /// <summary>
    /// Opens an installed or retained pack and mods at the exact saved versions.
    /// </summary>
    /// <param name="contentRootPath">The installed content directory.</param>
    /// <param name="saved">The already validated save identity.</param>
    /// <param name="clientMods">The local presentation mods to retain for this player.</param>
    /// <returns>The content required to load the save.</returns>
    internal static ContentPack OpenSaved(
        string contentRootPath,
        GameMetadata saved,
        IReadOnlyList<ContentModDefinition> clientMods = null
    )
    {
        if (
            string.IsNullOrWhiteSpace(saved.PackID)
            || string.IsNullOrWhiteSpace(saved.PackVersion)
            || string.IsNullOrWhiteSpace(saved.ScenarioID)
        )
            throw new InvalidOperationException(
                "The save does not identify its pack, version, and scenario."
            );
        return OpenPack(
            Path.GetFullPath(contentRootPath),
            saved.PackID,
            saved.ScenarioID,
            null,
            saved,
            clientMods: clientMods
        );
    }

    /// <summary>
    /// Opens a selected pack at the scenario declared by its active content.
    /// </summary>
    /// <param name="contentRootPath">The installed content directory.</param>
    /// <param name="packID">The selected pack identity.</param>
    /// <param name="disabledModIDs">Mods excluded from this selection.</param>
    /// <param name="preferredModOrder">The player-selected priority of mod IDs.</param>
    /// <param name="requireValidSelection">Whether to reject blocked mods instead of using the applied set.</param>
    /// <param name="appliedModVersions">The recorded releases when opening applied content.</param>
    /// <returns>The selected content and its effective default scenario.</returns>
    internal static ContentPack OpenDefaultScenario(
        string contentRootPath,
        string packID,
        IEnumerable<string> disabledModIDs,
        IReadOnlyList<string> preferredModOrder = null,
        bool requireValidSelection = false,
        IReadOnlyList<string> appliedModVersions = null
    ) =>
        OpenPack(
            Path.GetFullPath(contentRootPath),
            packID,
            null,
            disabledModIDs,
            preferredModOrder: preferredModOrder,
            requireValidSelection: requireValidSelection,
            appliedModVersions: appliedModVersions
        );

    /// <summary>
    /// Loads and validates a specific pack + scenario from a resolved content root.
    /// </summary>
    /// <param name="absoluteContentRoot">The resolved external content root.</param>
    /// <param name="packID">The pack ID to open.</param>
    /// <param name="scenarioID">The scenario ID to open, or empty for the pack default.</param>
    /// <param name="disabledModIDs">Mod IDs that must not enter the active content layers.</param>
    /// <param name="saved">The exact content identity when restoring saved content.</param>
    /// <param name="allowModDefault">Whether an authored default supersedes a catalog default.</param>
    /// <param name="preferredModOrder">The saved or staged order for a new game.</param>
    /// <param name="requireValidSelection">Whether every requested mod must be loadable.</param>
    /// <param name="appliedModVersions">The exact releases when opening an applied stack.</param>
    /// <param name="clientMods">The player's local presentation selection when restoring gameplay.</param>
    /// <returns>The loaded content pack.</returns>
    private static ContentPack OpenPack(
        string absoluteContentRoot,
        string packID,
        string scenarioID,
        IEnumerable<string> disabledModIDs,
        GameMetadata saved = null,
        bool allowModDefault = false,
        IReadOnlyList<string> preferredModOrder = null,
        bool requireValidSelection = false,
        IReadOnlyList<string> appliedModVersions = null,
        IReadOnlyList<ContentModDefinition> clientMods = null
    )
    {
        IReadOnlyList<string> packRoots = ResolvePackRoots(
            absoluteContentRoot,
            packID,
            saved?.PackVersion
        );
        string packRoot = packRoots[0];
        ContentPackDefinition pack = ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(
            Path.Combine(packRoot, _packFileName)
        );
        if (!string.Equals(pack.ID, packID, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Game pack '{pack.ID}' does not match selection '{packID}'."
            );
        }

        ContentFileResolver fileResolver = ContentFileResolver.Discover(
            absoluteContentRoot,
            packRoot,
            pack.ID,
            pack.Version,
            disabledModIDs,
            packRoots.Skip(1),
            saved,
            preferredModOrder: preferredModOrder,
            appliedModIDs: requireValidSelection ? null : preferredModOrder,
            appliedModVersions: appliedModVersions,
            clientMods: clientMods
        );
        if (requireValidSelection)
        {
            ContentModIssue issue = fileResolver.ModIssues.FirstOrDefault(issue =>
                issue.BlocksSelection
            );
            if (issue != null)
                throw new InvalidOperationException($"{issue.DisplayName}: {issue.Message}");
        }

        fileResolver.ValidateArtworkAliases();
        ContentPackDefinition effective = fileResolver.Mods.Any(mod => mod.ExtendsPack)
            ? ReadModPackDefinition(fileResolver)
            : pack;
        return LoadPack(
            fileResolver,
            effective,
            allowModDefault && effective.DefaultScenarioID != pack.DefaultScenarioID
                ? null
                : scenarioID
        );
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
        ContentFileResolver gameplayResolver = fileResolver.ForGameplay();
        List<ContentFactionDefinition> factions = LoadDefinitions<ContentFactionDefinition>(
            gameplayResolver,
            pack.FactionPaths
        );
        List<ContentScenarioDefinition> scenarios = LoadDefinitions<ContentScenarioDefinition>(
            gameplayResolver,
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
                $"Game pack '{pack.ID}' has no scenario '{resolvedScenarioID}'."
            );

        ValidateDefinitions(pack, factions, scenarios, scenario);
        if (fileResolver.GameplayMods.Count != fileResolver.Mods.Count)
            fileResolver.ValidateClientOnlyContent(ReadThemePaths(gameplayResolver, pack));
        GameDataCatalog gameData = LoadGameData(
            gameplayResolver,
            pack,
            factions,
            scenario,
            fileResolver
        );
        return new ContentPack(
            fileResolver.ContentRootPath,
            fileResolver.PackRootPath,
            fileResolver,
            pack,
            scenario,
            factions,
            gameData,
            LoadPreloadManifests(fileResolver, pack.Preloads),
            scenarios
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
        return ResolvePlayerContentRootPath(dataPath, platform, null);
    }

    /// <summary>
    /// Resolves the external content directory for a desktop player, including the shared macOS
    /// application-data location used by a player nested inside its launcher bundle.
    /// </summary>
    /// <param name="dataPath">The platform-specific Unity player data path.</param>
    /// <param name="platform">The current runtime platform.</param>
    /// <param name="macUserProfilePath">The macOS user profile root, when available.</param>
    /// <returns>The absolute external content root.</returns>
    internal static string ResolvePlayerContentRootPath(
        string dataPath,
        RuntimePlatform platform,
        string macUserProfilePath
    )
    {
        if (string.IsNullOrWhiteSpace(dataPath))
            throw new ArgumentException("A player data path is required.", nameof(dataPath));

        DirectoryInfo dataDirectory = new DirectoryInfo(Path.GetFullPath(dataPath));
        DirectoryInfo playerDirectory = ResolvePlayerDirectory(dataDirectory, platform);
        if (playerDirectory == null)
            throw new InvalidOperationException(
                "The player content directory could not be resolved."
            );

        if (
            IsNestedMacBundleResourcesDirectory(playerDirectory)
            && !string.IsNullOrWhiteSpace(macUserProfilePath)
        )
        {
            return Path.Combine(
                Path.GetFullPath(macUserProfilePath),
                _macLibraryDirectoryName,
                _macApplicationSupportDirectoryName,
                _macApplicationDataDirectoryName,
                _contentDirectoryName
            );
        }

        return Path.Combine(playerDirectory.FullName, _contentDirectoryName);
    }

    /// <summary>
    /// Returns whether a resolved player directory belongs to the Resources directory of a host
    /// application bundle.
    /// </summary>
    /// <param name="directory">The resolved player directory.</param>
    /// <returns>True when a nested macOS player shares its host application's resources.</returns>
    private static bool IsNestedMacBundleResourcesDirectory(DirectoryInfo directory)
    {
        return string.Equals(directory?.Name, "Resources", StringComparison.Ordinal)
            && string.Equals(directory.Parent?.Name, "Contents", StringComparison.Ordinal)
            && string.Equals(
                directory.Parent.Parent?.Extension,
                ".app",
                StringComparison.OrdinalIgnoreCase
            );
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
            ContentDefinitionSerializer.DeserializeText<ContentPreloadManifest>(
                ContentXmlOverrides.Load(
                    fileResolver,
                    _applicationAddressPrefix + _preloadDirectoryName + "/" + preloadID + ".xml"
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
    /// <param name="presentationResolver">The shared and local layers used only for UI themes.</param>
    /// <returns>The composed typed game-data catalog.</returns>
    private static GameDataCatalog LoadGameData(
        ContentFileResolver fileResolver,
        ContentPackDefinition pack,
        IReadOnlyList<ContentFactionDefinition> factions,
        ContentScenarioDefinition scenario,
        ContentFileResolver presentationResolver
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
        FactionThemes themes = new();
        foreach (
            string path in ResolveThemePaths(
                pack,
                factions,
                path => ContentXmlOverrides.Load(fileResolver, _packAddressPrefix + path)
            )
        )
            themes.AddRange(
                DeserializeGameData<FactionThemes>(
                    presentationResolver,
                    path,
                    nameof(FactionThemes)
                )
            );
        if (themes.Count(theme => FactionThemes.IsNeutral(theme.FactionInstanceID)) != 1)
            throw new InvalidDataException("A game pack must define exactly one Neutral theme.");

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
        XmlDocument defaults = LoadXmlText(
            ContentXmlOverrides.Load(fileResolver, _applicationGameConfigRelativePath)
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
                LoadXmlText(
                    ContentXmlOverrides.Load(
                        fileResolver,
                        _packAddressPrefix + packGameConfigPath,
                        defaults.OuterXml
                    )
                )
            );
        GameConfig config =
            gameConfig as GameConfig
            ?? throw new InvalidDataException("Failed to deserialize the merged game config.");
        GameConfig.BombardmentConfig bombardment = config.Combat?.Bombardment;
        if (bombardment != null && bombardment.StrikeRollMinimum > bombardment.StrikeRollMaximum)
            throw new InvalidDataException(
                "Combat / Bombardment: strike roll minimum must not exceed strike roll maximum."
            );
        if (bombardment?.StrikeRollMaximum == int.MaxValue)
            throw new InvalidDataException(
                $"Combat / Bombardment: strike roll maximum must be less than {int.MaxValue}."
            );
        return config;
    }

    /// <summary>
    /// Parses resolved XML for the strict typed configuration loader.
    /// </summary>
    /// <param name="text">The resolved XML text.</param>
    /// <returns>The parsed document.</returns>
    private static XmlDocument LoadXmlText(string text)
    {
        XmlDocument document = new XmlDocument();
        document.LoadXml(text);
        return document;
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
        string xml = ContentXmlOverrides.Load(fileResolver, _packAddressPrefix + relativePath);
        GameSerializerSettings settings = new GameSerializerSettings { RootName = rootName };
        if (!string.IsNullOrWhiteSpace(schemaFilePath))
        {
            XmlSchemaSet schemas = new XmlSchemaSet();
            using XmlReader schemaReader = XmlReader.Create(schemaFilePath);
            schemas.Add(null, schemaReader);
            settings.Schemas = schemas;
        }

        GameSerializer serializer = new GameSerializer(typeof(T), settings);
        using StringReader reader = new StringReader(xml);
        return serializer.Deserialize(reader) as T
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
                ContentDefinitionSerializer.DeserializeText<T>(
                    ContentXmlOverrides.Load(fileResolver, _packAddressPrefix + path)
                )
            )
            .ToList();
    }

    /// <summary>
    /// Reads theme paths registered by the pack and by legacy faction manifests.
    /// </summary>
    /// <param name="resolver">The installed content and inheritance layers.</param>
    /// <param name="pack">The effective pack manifest.</param>
    /// <returns>The pack-relative paths to the theme catalogs.</returns>
    public static IEnumerable<string> ReadThemePaths(
        ContentFileResolver resolver,
        ContentPackDefinition pack
    ) =>
        ResolveThemePaths(
            pack,
            LoadDefinitions<ContentFactionDefinition>(resolver, pack.FactionPaths),
            path => ContentXmlOverrides.Load(resolver, _packAddressPrefix + path)
        );

    /// <summary>
    /// Resolves the shared list and fills in themes still registered by legacy manifests.
    /// </summary>
    /// <param name="pack">The effective pack manifest.</param>
    /// <param name="factions">The registered faction manifests.</param>
    /// <param name="readTheme">Reads a theme at its pack-relative path.</param>
    /// <returns>The unique theme paths in declaration order.</returns>
    public static IEnumerable<string> ResolveThemePaths(
        ContentPackDefinition pack,
        IEnumerable<ContentFactionDefinition> factions,
        Func<string, string> readTheme
    )
    {
        string[] paths = pack
            .ThemePaths.Select(path => path.Replace('\\', '/'))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var remaining = factions
            .Select(faction => (faction.ID, Path: faction.ThemePath))
            .Prepend((null, pack.LegacyNeutralThemePath))
            .Where(theme => !string.IsNullOrWhiteSpace(theme.Path))
            .Select(theme => (theme.ID, Path: theme.Path.Replace('\\', '/')))
            .Where(theme => !paths.Contains(theme.Path, StringComparer.Ordinal))
            .ToArray();
        if (remaining.Length == 0)
            return paths;
        HashSet<string> registeredIDs = paths
            .SelectMany(path => FactionThemes.ReadFactionIDs(readTheme(path)))
            .ToHashSet(StringComparer.Ordinal);
        bool hasNeutral = registeredIDs.Any(FactionThemes.IsNeutral);
        return paths
            .Concat(
                remaining
                    .Where(theme =>
                        theme.ID == null ? !hasNeutral : !registeredIDs.Contains(theme.ID)
                    )
                    .Select(theme => theme.Path)
            )
            .Distinct(StringComparer.Ordinal);
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
                ContentDefinitionSerializer.DeserializeText<ContentPreloadManifest>(
                    ContentXmlOverrides.Load(fileResolver, _packAddressPrefix + preload.Path)
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
            definitionPath = ContentPackageManifest.Find(packRoot, ContentPackageType.GamePack);
            if (ContentPackageManifest.ReadType(definitionPath) == ContentPackageType.Mod)
                return null;
            ContentPackDefinition definition =
                ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(definitionPath);
            if (string.IsNullOrWhiteSpace(definition.ID))
            {
                const string message = "Game pack definition is missing required field 'ID'.";
                GameLogger.Warning(
                    $"Skipping invalid game pack definition '{definitionPath}': {message}"
                );
                return new InstalledContentPack(directoryName, directoryName, null, message);
            }

            if (string.IsNullOrWhiteSpace(definition.Version))
            {
                const string message = "Game pack definition is missing required field 'Version'.";
                GameLogger.Warning(
                    $"Skipping invalid game pack definition '{definitionPath}': {message}"
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
                $"Skipping invalid game pack definition '{definitionPath}': {message}"
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
    /// <param name="requiredVersion">The exact saved version, when restoring a save.</param>
    /// <param name="inheritedPacks">The published parent chain of an explicit working folder.</param>
    /// <returns>The absolute pack roots from the selected pack through its furthest ancestor.</returns>
    private static IReadOnlyList<string> ResolvePackRoots(
        string contentRootPath,
        string packID,
        string requiredVersion = null,
        IReadOnlyList<ContentPackDependency> inheritedPacks = null
    )
    {
        string packsRootPath = Path.Combine(Path.GetFullPath(contentRootPath), _packsDirectoryName);
        if (
            !Directory.Exists(packsRootPath)
            && !Directory.Exists(ContentFileResolver.ResolveModsRootPath(contentRootPath))
        )
            throw new DirectoryNotFoundException(
                $"Game packs directory not found: {packsRootPath}"
            );

        Dictionary<string, List<(string Root, ContentPackDefinition Definition)>> packs =
            new Dictionary<string, List<(string Root, ContentPackDefinition Definition)>>(
                StringComparer.Ordinal
            );
        List<string> definitionIssues = new List<string>();
        foreach (
            var (definitionPath, candidate) in ReadPackDefinitions(
                contentRootPath,
                definitionIssues
            )
        )
        {
            string candidateRoot = Path.GetDirectoryName(definitionPath);
            if (string.IsNullOrWhiteSpace(candidate.ID))
            {
                definitionIssues.Add($"'{definitionPath}' is missing its content-pack ID.");
                continue;
            }
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
        int inheritedIndex = 0;
        while (!string.IsNullOrWhiteSpace(currentPackID))
        {
            if (!visited.Add(currentPackID))
            {
                throw new InvalidDataException(
                    $"Game pack inheritance contains a cycle at '{currentPackID}'."
                );
            }
            if (inheritedPacks is { Count: > 0 })
            {
                ContentPackDependency inherited = inheritedPacks.ElementAtOrDefault(
                    inheritedIndex++
                );
                if (inherited?.ID != currentPackID || string.IsNullOrWhiteSpace(inherited.Version))
                    throw new InvalidOperationException(
                        $"The published inheritance chain does not specify a version for '{currentPackID}'."
                    );
                requiredVersion = inherited.Version;
            }
            packs.TryGetValue(currentPackID, out var matches);
            if (requiredVersion != null)
            {
                matches = FindPackRelease(contentRootPath, currentPackID, requiredVersion, matches);
                requiredVersion = null;
            }
            if (matches == null)
            {
                string discovered = string.Join(
                    "; ",
                    packs
                        .Values.SelectMany(definitions => definitions)
                        .Select(pack =>
                            $"'{Path.Combine(pack.Root, _packFileName)}' declares '{pack.Definition.ID}'"
                        )
                );
                throw new InvalidDataException(
                    $"No installed game pack declares ID '{currentPackID}'. "
                        + $"Content root: '{Path.GetFullPath(contentRootPath)}'. "
                        + $"Readable pack definitions: {(discovered.Length == 0 ? "none" : discovered)}. "
                        + $"Pack definition issues: {(definitionIssues.Count == 0 ? "none" : string.Join("; ", definitionIssues))}."
                );
            }
            if (matches.Count > 1)
            {
                throw new InvalidDataException(
                    $"Multiple game packs declare ID '{currentPackID}'."
                );
            }

            var currentPack = matches[0];
            if (string.IsNullOrWhiteSpace(currentPack.Definition.Version))
            {
                throw new InvalidDataException(
                    $"Game pack '{currentPackID}' must declare Version."
                );
            }
            roots.Add(currentPack.Root);
            if (currentPack.Definition.InheritedPacks is { Count: > 0 } parentPins)
            {
                if (inheritedPacks is not { Count: > 0 })
                {
                    inheritedPacks = parentPins;
                    inheritedIndex = 0;
                }
                else if (
                    !inheritedPacks
                        .Skip(inheritedIndex)
                        .Select(pin => (pin?.ID, pin?.Version))
                        .SequenceEqual(parentPins.Select(pin => (pin?.ID, pin?.Version)))
                )
                    throw new InvalidDataException(
                        $"The published inheritance chain disagrees with '{currentPackID}'."
                    );
            }
            currentPackID = currentPack.Definition.BasePackID;
        }

        if (inheritedPacks != null && inheritedIndex != inheritedPacks.Count)
            throw new InvalidDataException("The published inheritance chain has unused entries.");
        return roots;
    }

    /// <summary>
    /// Finds a required pack release in the installed and retained package locations.
    /// </summary>
    /// <param name="contentRootPath">The installed content root.</param>
    /// <param name="packID">The required pack ID.</param>
    /// <param name="version">The required version.</param>
    /// <param name="installed">The active installations with that ID.</param>
    /// <returns>The matching installations, with duplicates retained for validation.</returns>
    private static List<(string Root, ContentPackDefinition Definition)> FindPackRelease(
        string contentRootPath,
        string packID,
        string version,
        List<(string Root, ContentPackDefinition Definition)> installed
    )
    {
        var matches = installed?.Where(entry => entry.Definition.Version == version).ToList();
        if (matches == null || matches.Count == 0)
            matches = ReadRetainedPack(contentRootPath, packID, version);
        if (matches.Count == 0)
            throw new InvalidOperationException(
                $"Required game pack '{packID}' version '{version}' is not installed."
            );
        return matches;
    }

    /// <summary>
    /// Finds a retained release matching the save's selected pack.
    /// </summary>
    /// <param name="contentRootPath">The installation whose archive is inspected.</param>
    /// <param name="id">The saved pack ID.</param>
    /// <param name="version">The saved pack version.</param>
    /// <returns>Matching retained pack roots and manifests.</returns>
    private static List<(string Root, ContentPackDefinition Definition)> ReadRetainedPack(
        string contentRootPath,
        string id,
        string version
    )
    {
        var results = new List<(string, ContentPackDefinition)>();
        foreach (
            IEnumerable<string> archives in new[]
            {
                ContentFileResolver.GetArchivedPackageFolders(contentRootPath),
                ModPackageStore.GetRetainedReleaseFolders(contentRootPath),
            }
        )
        {
            foreach (string folder in archives)
            {
                if (!File.Exists(Path.Combine(folder, _packFileName)))
                    continue;
                InstalledContentPack pack = DiscoverInstalledPack(folder);
                if (pack?.Definition?.ID == id && pack.Definition.Version == version)
                    results.Add((folder, pack.Definition));
            }
            if (results.Count > 0)
                break;
        }
        return results;
    }

    /// <summary>
    /// Reads installed definitions while isolating malformed authored drafts from unrelated packs.
    /// </summary>
    /// <param name="contentRootPath">The installed Content directory.</param>
    /// <param name="definitionIssues">Receives definition failures for an unresolved pack's diagnostic.</param>
    /// <returns>The readable definitions and their manifest paths.</returns>
    private static IEnumerable<(string Path, ContentPackDefinition Definition)> ReadPackDefinitions(
        string contentRootPath,
        ICollection<string> definitionIssues
    )
    {
        foreach (string path in EnumeratePackDefinitions(contentRootPath))
        {
            ContentPackDefinition definition;
            try
            {
                string manifest = ContentPackageManifest.Find(
                    Path.GetDirectoryName(path),
                    ContentPackageType.GamePack
                );
                if (ContentPackageManifest.ReadType(manifest) == ContentPackageType.Mod)
                    continue;
                definition = ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(path);
            }
            catch (Exception exception) when (IsContentDiscoveryException(exception))
            {
                definitionIssues.Add($"'{path}': {GetContentErrorMessage(exception)}");
                GameLogger.Warning(
                    $"Game pack could not be loaded: {path}. {GetContentErrorMessage(exception)}"
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
#if UNITY_STANDALONE_OSX
        return ResolvePlayerContentRootPath(
            Application.dataPath,
            Application.platform,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        );
#else
        return ResolvePlayerContentRootPath(Application.dataPath, Application.platform);
#endif
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
