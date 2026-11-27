using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Rebellion.Game;
using Rebellion.Util.Logging;
using Rebellion.Util.Serialization;

/// <summary>
/// Resolves logical content addresses through enabled mods and the selected base pack.
/// </summary>
public sealed class ContentFileResolver
{
    private const string _applicationScopeName = "Application";
    private const string _packScopeName = "Pack";
    private const string _applicationAddressPrefix = _applicationScopeName + "/";
    private const string _packAddressPrefix = _packScopeName + "/";
    private const string _modContentDirectoryName = "Content";
    private const string _modsDirectoryName = "Mods";
    private const string _versionsDirectoryName = ".versions";
    private static readonly string[] _artworkExtensions = { ".png", ".jpg", ".jpeg" };

    private readonly IReadOnlyList<string> modContentRoots;
    private readonly IReadOnlyList<string> packRoots;
    private readonly Dictionary<string, Dictionary<string, string>> artworkAliases;
    private IReadOnlyDictionary<string, IReadOnlyList<string>> overrideWarnings;

    public string ContentRootPath { get; }

    public string PackRootPath { get; }

    /// <summary>
    /// Gets the selected pack and its inherited layers in resolution order.
    /// </summary>
    public IReadOnlyList<string> PackRootPaths => packRoots;

    public IReadOnlyList<ContentModDefinition> Mods { get; }

    /// <summary>
    /// Gets every compatible discovered mod, including disabled mods.
    /// </summary>
    public IReadOnlyList<ContentModDefinition> AvailableMods { get; }

    /// <summary>
    /// Gets discovered mods whose definitions could not be loaded safely.
    /// </summary>
    public IReadOnlyList<ContentModIssue> ModIssues { get; }

    /// <summary>
    /// Reports overlapping writes for the active mod list without scanning files during startup.
    /// </summary>
    /// <returns>Override warnings keyed by the mod whose edits are superseded.</returns>
    internal IReadOnlyDictionary<string, IReadOnlyList<string>> DescribeModOverrides() =>
        overrideWarnings ??= ContentModOverrides.Describe(Mods, modContentRoots);

    /// <summary>
    /// Reads and validates the manifest at the root of a mod project.
    /// </summary>
    /// <param name="modRoot">The directory containing the mod manifest.</param>
    /// <returns>The validated manifest.</returns>
    public static ContentModDefinition ReadValidatedMod(string modRoot)
    {
        ContentModDefinition definition = ReadModDefinition(
            ContentPackageManifest.Find(modRoot, ContentPackageType.Mod)
        );
        ValidateDefinition(definition);
        return definition;
    }

    /// <summary>
    /// Gets the user-content directory beside the installed Content directory.
    /// </summary>
    /// <param name="contentRootPath">The installed Content directory.</param>
    /// <returns>The absolute Mods directory.</returns>
    public static string ResolveModsRootPath(string contentRootPath)
    {
        return Path.Combine(
            Directory.GetParent(Path.GetFullPath(contentRootPath))?.FullName
                ?? throw new InvalidOperationException("The content root has no parent directory."),
            _modsDirectoryName
        );
    }

    /// <summary>
    /// Enumerates retained releases without making them active for new games.
    /// </summary>
    /// <param name="contentRootPath">The installed Content directory.</param>
    /// <returns>The folders containing older package versions.</returns>
    internal static IEnumerable<string> GetArchivedPackageFolders(string contentRootPath)
    {
        string root = Path.Combine(ResolveModsRootPath(contentRootPath), _versionsDirectoryName);
        return Directory.Exists(root)
            ? Directory.EnumerateDirectories(root)
            : Enumerable.Empty<string>();
    }

    /// <summary>
    /// Creates a resolver over explicit mod content roots in ascending load order.
    /// </summary>
    /// <param name="contentRootPath">The absolute application content root.</param>
    /// <param name="packRootPath">The absolute selected pack root.</param>
    /// <param name="modContentRootPaths">The mod content roots in ascending load order.</param>
    /// <param name="mods">The definitions corresponding to the mod content roots.</param>
    /// <param name="availableMods">All compatible definitions, including disabled mods.</param>
    /// <param name="modIssues">The discovered mod-definition failures.</param>
    /// <param name="fallbackPackRootPaths">Inherited pack roots in nearest-to-farthest order.</param>
    public ContentFileResolver(
        string contentRootPath,
        string packRootPath,
        IEnumerable<string> modContentRootPaths = null,
        IEnumerable<ContentModDefinition> mods = null,
        IEnumerable<ContentModDefinition> availableMods = null,
        IEnumerable<ContentModIssue> modIssues = null,
        IEnumerable<string> fallbackPackRootPaths = null
    )
    {
        ContentRootPath = Path.GetFullPath(
            contentRootPath ?? throw new ArgumentNullException(nameof(contentRootPath))
        );
        PackRootPath = Path.GetFullPath(
            packRootPath ?? throw new ArgumentNullException(nameof(packRootPath))
        );
        packRoots = new[] { PackRootPath }
            .Concat(fallbackPackRootPaths ?? Enumerable.Empty<string>())
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        modContentRoots = (modContentRootPaths ?? Enumerable.Empty<string>())
            .Select(Path.GetFullPath)
            .ToArray();
        Mods = (mods ?? Enumerable.Empty<ContentModDefinition>()).ToArray();
        AvailableMods = (availableMods ?? Mods).ToArray();
        ModIssues = (modIssues ?? Enumerable.Empty<ContentModIssue>()).ToArray();
        artworkAliases = packRoots
            .Concat(modContentRoots.Select(root => Path.Combine(root, _packScopeName)))
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(root => root, ContentArtworkAliases.Load, StringComparer.Ordinal);
    }

    /// <summary>
    /// Discovers mods compatible with a base pack from the directory beside Content.
    /// </summary>
    /// <param name="contentRootPath">The absolute application content root.</param>
    /// <param name="packRootPath">The absolute selected pack root.</param>
    /// <param name="basePackID">The selected base pack identifier.</param>
    /// <param name="basePackVersion">The selected base pack version.</param>
    /// <param name="disabledModIDs">Mod IDs to discover without loading.</param>
    /// <param name="fallbackPackRootPaths">Inherited pack roots in nearest-to-farthest order.</param>
    /// <param name="saved">The exact mod IDs, versions, and order recorded by a save.</param>
    /// <param name="workingModRoot">An explicit project to load with just its requirements.</param>
    /// <param name="preferredModOrder">The ordered mod IDs, or null to use legacy folder order.</param>
    /// <param name="appliedModIDs">The active IDs when opening an already applied selection.</param>
    /// <param name="appliedModVersions">The recorded versions in applied order, if available.</param>
    /// <param name="workingModOverride">A proposed manifest to validate in the working folder.</param>
    /// <returns>A resolver containing the compatible discovered mods.</returns>
    internal static ContentFileResolver Discover(
        string contentRootPath,
        string packRootPath,
        string basePackID,
        string basePackVersion,
        IEnumerable<string> disabledModIDs = null,
        IEnumerable<string> fallbackPackRootPaths = null,
        GameMetadata saved = null,
        string workingModRoot = null,
        IReadOnlyList<string> preferredModOrder = null,
        IReadOnlyList<string> appliedModIDs = null,
        IReadOnlyList<string> appliedModVersions = null,
        ContentModDefinition workingModOverride = null
    )
    {
        string modsRoot = ResolveModsRootPath(contentRootPath);
        var installations = new List<(ContentModDefinition Mod, string Root)>();
        List<ContentModIssue> issues = new List<ContentModIssue>();
        HashSet<string> disabledIDs = new HashSet<string>(
            disabledModIDs ?? Enumerable.Empty<string>(),
            StringComparer.Ordinal
        );
        IEnumerable<string> folders = Directory.Exists(modsRoot)
            ? Directory
                .EnumerateDirectories(modsRoot)
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            : Enumerable.Empty<string>();
        ContentModDefinition workingMod = null;
        if (workingModRoot != null)
        {
            workingModRoot = Path.GetFullPath(workingModRoot);
            workingMod = workingModOverride ?? ReadValidatedMod(workingModRoot);
            folders = new[] { workingModRoot }.Concat(
                folders.Where(path =>
                    !string.Equals(Path.GetFullPath(path), workingModRoot, StringComparison.Ordinal)
                )
            );
        }
        foreach (string modRoot in folders)
        {
            if (!ContentPackageManifest.Exists(modRoot))
            {
                if (Path.GetFileName(modRoot) == _versionsDirectoryName)
                    continue;

                string folderName = Path.GetFileName(modRoot);
                const string message = "Package manifest 'pack.xml' or 'mod.xml' was not found.";
                issues.Add(new ContentModIssue(folderName, folderName, message));
                GameLogger.Warning($"Skipping content mod folder '{modRoot}': {message}");
                continue;
            }

            ContentModDefinition definition = null;
            string definitionPath = null;
            try
            {
                definitionPath = ContentPackageManifest.Find(modRoot, ContentPackageType.Mod);
                if (ContentPackageManifest.ReadType(definitionPath) == ContentPackageType.Era)
                    continue;
                definition =
                    modRoot == workingModRoot ? workingMod : ReadModDefinition(definitionPath);
                if (string.IsNullOrWhiteSpace(definition.BasePackID))
                    ValidateDefinition(definition);
                if (!string.Equals(definition.BasePackID, basePackID, StringComparison.Ordinal))
                    continue;
                if (string.IsNullOrWhiteSpace(definition.BasePackVersion))
                    ValidateDefinition(definition);
                if (
                    !string.Equals(
                        definition.BasePackVersion,
                        basePackVersion,
                        StringComparison.Ordinal
                    )
                )
                    continue;
                ValidateDefinition(definition);
                string contentRoot = Path.Combine(modRoot, _modContentDirectoryName);
                string packOverlay = ResolveModPath(contentRoot, "Pack/pack.xml");
                if (definition.ExtendsPack && File.Exists(packOverlay))
                    ContentPackLoader.ValidateModPackOverlay(File.ReadAllText(packOverlay));
                if (definition.ID == workingMod?.ID && modRoot != workingModRoot)
                    continue;
                ContentArtworkAliases.Load(Path.Combine(contentRoot, _packScopeName));
                installations.Add((definition, modRoot));
            }
            catch (Exception exception) when (IsInvalidModDefinitionException(exception))
            {
                if (string.Equals(modRoot, workingModRoot, StringComparison.Ordinal))
                    throw;
                string folderName = Path.GetFileName(modRoot);
                string id = string.IsNullOrWhiteSpace(definition?.ID) ? folderName : definition.ID;
                string displayName = string.IsNullOrWhiteSpace(definition?.DisplayName)
                    ? id
                    : definition.DisplayName;
                issues.Add(new ContentModIssue(id, displayName, exception.Message));
                GameLogger.Warning(
                    $"Skipping invalid content mod definition '{definitionPath ?? modRoot}': {exception.Message}"
                );
                continue;
            }
        }

        var appliedIDs = appliedModIDs?.ToHashSet(StringComparer.Ordinal);
        var availableDefinitions = new List<ContentModDefinition>();
        var rootsByID = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in installations.GroupBy(entry => entry.Mod.ID, StringComparer.Ordinal))
        {
            var copies = group.ToArray();
            if (copies.Length > 1)
            {
                string foldersWithVersions = string.Join(
                    ", ",
                    copies.Select(copy => $"{Path.GetFileName(copy.Root)} ({copy.Mod.Version})")
                );
                string message =
                    $"Multiple folders use mod ID '{group.Key}': {foldersWithVersions}. Keep one active copy. Use Mods/.versions to keep older releases of the same mod.";
                issues.Add(
                    new ContentModIssue(
                        group.Key,
                        group.Key,
                        message,
                        blocksSelection: !disabledIDs.Contains(group.Key)
                            && appliedIDs?.Contains(group.Key) != false,
                        isDuplicate: true
                    )
                );
                GameLogger.Warning(message);
                continue;
            }
            var entry = copies[0];
            availableDefinitions.Add(entry.Mod);
            rootsByID.Add(entry.Mod.ID, Path.Combine(entry.Root, _modContentDirectoryName));
        }

        IReadOnlyList<ContentModDefinition> active;
        if (saved == null && appliedModVersions == null)
        {
            IReadOnlyList<ContentModDefinition> requested =
                workingMod == null
                    ? availableDefinitions
                        .Where(mod =>
                            !disabledIDs.Contains(mod.ID) && appliedIDs?.Contains(mod.ID) != false
                        )
                        .ToArray()
                    : SelectWorkingMods(workingMod, availableDefinitions);
            active = ContentModLoadOrder.Resolve(requested, issues, preferredModOrder);
            if (workingMod != null && !active.Any(mod => mod.ID == workingMod.ID))
                throw new InvalidOperationException(
                    issues.FirstOrDefault(issue => issue.ID == workingMod.ID)?.Message
                        ?? $"Could not load {workingMod.DisplayName}."
                );
        }
        else
        {
            active = SelectExactMods(
                contentRootPath,
                basePackID,
                basePackVersion,
                saved != null ? saved.ModIDs : appliedModIDs,
                saved != null ? saved.ModVersions : appliedModVersions,
                installations,
                rootsByID
            );
        }

        return new ContentFileResolver(
            contentRootPath,
            packRootPath,
            active.Select(mod => rootsByID[mod.ID]),
            active,
            availableDefinitions,
            issues,
            fallbackPackRootPaths
        );
    }

    /// <summary>
    /// Resolves an existing logical content address, preferring the last loaded mod.
    /// </summary>
    /// <param name="address">The application- or pack-scoped logical address.</param>
    /// <param name="extensions">Optional extensions to probe when the address has none.</param>
    /// <returns>The resolved absolute file path, or null when no layer contains the file.</returns>
    public string ResolveFile(string address, params string[] extensions)
    {
        string normalizedAddress = NormalizeAddress(address);
        return ResolveFile(normalizedAddress, extensions, null);
    }

    /// <summary>
    /// Checks that the active layers do not create a cycle in artwork resolution.
    /// </summary>
    internal void ValidateArtworkAliases()
    {
        foreach (string address in artworkAliases.Values.SelectMany(layer => layer.Keys).Distinct())
            ResolveFile(address, _artworkExtensions, null);
    }

    /// <summary>
    /// Follows image redirects at their owning layer while retaining normal override precedence.
    /// </summary>
    /// <param name="normalizedAddress">The scoped address being resolved.</param>
    /// <param name="extensions">The image extensions to probe.</param>
    /// <param name="visited">The addresses followed during this resolution.</param>
    /// <returns>The selected image or other content file, or null when missing.</returns>
    private string ResolveFile(
        string normalizedAddress,
        string[] extensions,
        HashSet<string> visited
    )
    {
        if (visited?.Add(normalizedAddress) == false)
            throw new InvalidDataException(
                "The artwork address maps contain a cycle between content layers."
            );
        for (int index = modContentRoots.Count - 1; index >= 0; index--)
        {
            if (
                artworkAliases[Path.Combine(modContentRoots[index], _packScopeName)]
                    .TryGetValue(normalizedAddress, out string alias)
                && alias != normalizedAddress
            )
                return ResolveFile(
                    alias,
                    extensions.Length == 0 ? _artworkExtensions : extensions,
                    visited ?? new HashSet<string>(StringComparer.Ordinal) { normalizedAddress }
                );
            string resolved = ResolveFromRoot(
                modContentRoots[index],
                normalizedAddress,
                extensions
            );
            if (resolved != null)
                return resolved;
        }

        foreach (string root in packRoots)
        {
            if (
                artworkAliases[root].TryGetValue(normalizedAddress, out string alias)
                && alias != normalizedAddress
            )
                return ResolveFile(
                    alias,
                    extensions.Length == 0 ? _artworkExtensions : extensions,
                    visited ?? new HashSet<string>(StringComparer.Ordinal) { normalizedAddress }
                );
            string basePath = ResolveScopedPath(
                Path.Combine(ContentRootPath, _applicationScopeName),
                root,
                normalizedAddress
            );
            if (File.Exists(basePath))
                return basePath;
            foreach (string extension in extensions)
            {
                if (File.Exists(basePath + extension))
                    return basePath + extension;
            }
        }
        return null;
    }

    /// <summary>
    /// Finds every existing version of one file in base-to-override order.
    /// </summary>
    /// <param name="address">The logical content address.</param>
    /// <returns>The existing files in application order.</returns>
    public IReadOnlyList<string> ResolveFileLayers(string address)
    {
        string normalized = NormalizeAddress(address);
        return ResolveBasePaths(normalized)
            .Reverse()
            .Concat(modContentRoots.Select(root => ResolveModPath(root, normalized)))
            .Where(File.Exists)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Finds manifest overlays contributed by opted-in mods in application order.
    /// </summary>
    /// <returns>The existing mod-owned pack manifest overlays.</returns>
    internal IEnumerable<string> ResolveModPackOverlays()
    {
        for (int index = 0; index < Mods.Count; index++)
        {
            if (!Mods[index].ExtendsPack)
                continue;
            string path = ResolveModPath(modContentRoots[index], "Pack/pack.xml");
            if (File.Exists(path))
                yield return path;
        }
    }

    /// <summary>
    /// Selects a working mod and recursively collects its installed requirements.
    /// </summary>
    /// <param name="workingMod">The explicit project.</param>
    /// <param name="available">Compatible installations, including the project.</param>
    /// <returns>The installed requirements and the working mod in discovery order.</returns>
    private static IReadOnlyList<ContentModDefinition> SelectWorkingMods(
        ContentModDefinition workingMod,
        IReadOnlyList<ContentModDefinition> available
    )
    {
        var byID = available.ToDictionary(mod => mod.ID, StringComparer.Ordinal);
        var selected = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(workingMod.ID);
        while (pending.Count > 0)
        {
            string id = pending.Pop();
            if (selected.Add(id) && byID.TryGetValue(id, out ContentModDefinition mod))
                foreach (ContentModRequirement requirement in mod.Requires)
                    pending.Push(requirement.ID);
        }
        return available.Where(mod => selected.Contains(mod.ID)).ToArray();
    }

    /// <summary>
    /// Selects the exact installed or retained releases in recorded order.
    /// </summary>
    /// <param name="contentRootPath">The installation whose retained versions can be used.</param>
    /// <param name="basePackID">The save's selected pack.</param>
    /// <param name="basePackVersion">The required pack version.</param>
    /// <param name="ids">The recorded mod IDs.</param>
    /// <param name="versions">The recorded mod versions.</param>
    /// <param name="installations">All compatible installations, including duplicate IDs.</param>
    /// <param name="rootsByID">Receives the roots of the exact releases selected by the save.</param>
    /// <returns>The exact recorded mod stack.</returns>
    private static IReadOnlyList<ContentModDefinition> SelectExactMods(
        string contentRootPath,
        string basePackID,
        string basePackVersion,
        IReadOnlyList<string> ids,
        IReadOnlyList<string> versions,
        IReadOnlyList<(ContentModDefinition Mod, string Root)> installations,
        IDictionary<string, string> rootsByID
    )
    {
        ids ??= Array.Empty<string>();
        versions ??= Array.Empty<string>();
        if (ids.Count != versions.Count)
            throw new InvalidDataException("The selected mod IDs and versions do not match.");
        if (ids.Count == 0)
            return Array.Empty<ContentModDefinition>();

        var releases = new List<(ContentModDefinition Mod, string Root)>(installations);
        var knownReleases = installations
            .Select(entry => (entry.Mod.ID, entry.Mod.Version))
            .ToHashSet();
        var archived = GetArchivedPackageFolders(contentRootPath)
            .Select(path => (Path: path, Managed: false))
            .Concat(
                ModPackageStore
                    .GetRetainedReleaseFolders(contentRootPath)
                    .Select(path => (Path: path, Managed: true))
            );
        foreach (var (folder, managed) in archived)
        {
            if (!ContentPackageManifest.Exists(folder))
                continue;
            try
            {
                string path = ContentPackageManifest.Find(folder, ContentPackageType.Mod);
                if (ContentPackageManifest.ReadType(path) == ContentPackageType.Era)
                    continue;
                ContentModDefinition mod = ReadModDefinition(path);
                ValidateDefinition(mod);
                if (mod.BasePackID == basePackID && mod.BasePackVersion == basePackVersion)
                {
                    bool newRelease = knownReleases.Add((mod.ID, mod.Version));
                    if (!managed || newRelease)
                        releases.Add((mod, folder));
                }
            }
            catch (Exception exception) when (IsInvalidModDefinitionException(exception))
            {
                GameLogger.Warning($"Skipping retained mod at '{folder}': {exception.Message}");
            }
        }
        var selected = new List<ContentModDefinition>(ids.Count);
        var byID = releases.ToLookup(entry => entry.Mod.ID, StringComparer.Ordinal);
        for (int index = 0; index < ids.Count; index++)
        {
            string id = ids[index];
            string version = versions[index];
            var copies = byID[id].Where(entry => entry.Mod.Version == version).ToArray();
            if (copies.Length > 1)
                throw new InvalidOperationException(
                    $"The selection requires mod '{id}' version '{version}', but more than one copy was found: "
                        + string.Join(
                            ", ",
                            copies
                                .Select(copy => copy.Root)
                                .OrderBy(path => path, StringComparer.Ordinal)
                        )
                        + ". Keep one copy of that version and try again."
                );
            if (copies.Length == 0)
            {
                string found = byID.Contains(id)
                    ? " Available versions: "
                        + string.Join(
                            ", ",
                            byID[id]
                                .Select(entry => entry.Mod.Version)
                                .Distinct(StringComparer.Ordinal)
                        )
                        + "."
                    : string.Empty;
                throw new InvalidOperationException(
                    $"The selection requires mod '{id}' version '{version}', which is not installed."
                        + found
                );
            }
            var entry = copies[0];
            selected.Add(entry.Mod);
            rootsByID[id] = Path.Combine(entry.Root, _modContentDirectoryName);
        }
        return selected;
    }

    /// <summary>
    /// Enumerates the union of files beneath an address, with later layers replacing earlier files.
    /// </summary>
    /// <param name="directoryAddress">The application- or pack-scoped directory address.</param>
    /// <returns>The layered logical file addresses beneath the directory.</returns>
    public IEnumerable<string> EnumerateFileAddresses(string directoryAddress)
    {
        string normalizedAddress = NormalizeAddress(directoryAddress).TrimEnd('/');
        Dictionary<string, string> files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string basePath in ResolveBasePaths(normalizedAddress).Reverse())
            AddFiles(files, basePath);
        foreach (string modContentRoot in modContentRoots)
            AddFiles(files, ResolveModPath(modContentRoot, normalizedAddress));
        return files
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => normalizedAddress + "/" + pair.Key);
    }

    /// <summary>
    /// Checks whether a logical directory exists in any content layer.
    /// </summary>
    /// <param name="directoryAddress">The application- or pack-scoped directory address.</param>
    /// <returns>True when at least one layer contains the directory.</returns>
    public bool DirectoryExists(string directoryAddress)
    {
        string normalizedAddress = NormalizeAddress(directoryAddress).TrimEnd('/');
        if (modContentRoots.Any(root => Directory.Exists(ResolveModPath(root, normalizedAddress))))
            return true;
        return ResolveBasePaths(normalizedAddress).Any(Directory.Exists);
    }

    /// <summary>
    /// Adds every file beneath one layer to the logical-address index.
    /// </summary>
    /// <param name="files">The indexed files, keyed by relative logical address.</param>
    /// <param name="directoryPath">The absolute layer directory to enumerate.</param>
    private static void AddFiles(IDictionary<string, string> files, string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return;

        foreach (
            string filePath in Directory.EnumerateFiles(
                directoryPath,
                "*",
                SearchOption.AllDirectories
            )
        )
        {
            string relativePath = Path.GetRelativePath(directoryPath, filePath).Replace('\\', '/');
            files[relativePath] = filePath;
        }
    }

    /// <summary>
    /// Resolves an address and its optional extensions beneath one mod content root.
    /// </summary>
    /// <param name="rootPath">The absolute mod content root.</param>
    /// <param name="normalizedAddress">The normalized scoped content address.</param>
    /// <param name="extensions">The optional file extensions to probe.</param>
    /// <returns>The first existing absolute file path, or null.</returns>
    private static string ResolveFromRoot(
        string rootPath,
        string normalizedAddress,
        IEnumerable<string> extensions
    )
    {
        string candidate = ResolveModPath(rootPath, normalizedAddress);
        if (File.Exists(candidate))
            return candidate;
        foreach (string extension in extensions)
        {
            if (File.Exists(candidate + extension))
                return candidate + extension;
        }
        return null;
    }

    /// <summary>
    /// Resolves a normalized address through the application root or selected pack inheritance chain.
    /// </summary>
    /// <param name="normalizedAddress">The normalized scoped content address.</param>
    /// <returns>The absolute candidate paths in overriding-to-fallback order.</returns>
    private IEnumerable<string> ResolveBasePaths(string normalizedAddress)
    {
        if (normalizedAddress.StartsWith(_applicationAddressPrefix, StringComparison.Ordinal))
        {
            yield return ResolveScopedPath(
                Path.Combine(ContentRootPath, _applicationScopeName),
                PackRootPath,
                normalizedAddress
            );
            yield break;
        }

        foreach (string packRoot in packRoots)
        {
            yield return ResolveScopedPath(
                Path.Combine(ContentRootPath, _applicationScopeName),
                packRoot,
                normalizedAddress
            );
        }
    }

    /// <summary>
    /// Resolves a normalized address beneath one mod content root.
    /// </summary>
    /// <param name="modContentRoot">The absolute mod content root.</param>
    /// <param name="normalizedAddress">The normalized scoped content address.</param>
    /// <returns>The corresponding absolute mod path.</returns>
    private static string ResolveModPath(string modContentRoot, string normalizedAddress)
    {
        return ResolveScopedPath(
            Path.Combine(modContentRoot, _applicationScopeName),
            Path.Combine(modContentRoot, _packScopeName),
            normalizedAddress
        );
    }

    /// <summary>
    /// Maps a logical application- or pack-scoped address to its filesystem root.
    /// </summary>
    /// <param name="applicationRootPath">The absolute application content root.</param>
    /// <param name="packRootPath">The absolute pack content root.</param>
    /// <param name="normalizedAddress">The normalized scoped content address.</param>
    /// <returns>The resolved absolute path within the selected scope.</returns>
    private static string ResolveScopedPath(
        string applicationRootPath,
        string packRootPath,
        string normalizedAddress
    )
    {
        if (string.Equals(normalizedAddress, _applicationScopeName, StringComparison.Ordinal))
            return Path.GetFullPath(applicationRootPath);
        if (string.Equals(normalizedAddress, _packScopeName, StringComparison.Ordinal))
            return Path.GetFullPath(packRootPath);
        if (normalizedAddress.StartsWith(_applicationAddressPrefix, StringComparison.Ordinal))
        {
            return ResolveWithinRoot(
                applicationRootPath,
                normalizedAddress[_applicationAddressPrefix.Length..]
            );
        }
        if (normalizedAddress.StartsWith(_packAddressPrefix, StringComparison.Ordinal))
        {
            return ResolveWithinRoot(packRootPath, normalizedAddress[_packAddressPrefix.Length..]);
        }

        throw new ArgumentException(
            $"Content addresses must begin with '{_applicationAddressPrefix}' or '{_packAddressPrefix}'.",
            nameof(normalizedAddress)
        );
    }

    /// <summary>
    /// Normalizes separators and validates a logical content address.
    /// </summary>
    /// <param name="address">The logical content address.</param>
    /// <returns>The normalized scoped address.</returns>
    private static string NormalizeAddress(string address)
    {
        string normalized = address?.Trim().Replace('\\', '/');
        if (
            string.IsNullOrEmpty(normalized)
            || Path.IsPathRooted(normalized)
            || normalized.StartsWith("/", StringComparison.Ordinal)
        )
            throw new ArgumentException("A relative content address is required.", nameof(address));
        if (
            !normalized.StartsWith(_applicationAddressPrefix, StringComparison.Ordinal)
            && !normalized.StartsWith(_packAddressPrefix, StringComparison.Ordinal)
        )
            throw new ArgumentException("A scoped content address is required.", nameof(address));
        return normalized;
    }

    /// <summary>
    /// Resolves a relative path while preventing traversal outside its content root.
    /// </summary>
    /// <param name="rootPath">The absolute allowed content root.</param>
    /// <param name="relativePath">The relative content path.</param>
    /// <returns>The resolved absolute path within the root.</returns>
    private static string ResolveWithinRoot(string rootPath, string relativePath)
    {
        string absoluteRoot = Path.GetFullPath(rootPath);
        string nativeRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathFullyQualified(nativeRelativePath))
            throw new ArgumentException("A relative content path is required.");

        string candidatePath = Path.GetFullPath(nativeRelativePath, absoluteRoot);
        string relativeToRoot = Path.GetRelativePath(absoluteRoot, candidatePath);
        if (
            relativeToRoot == ".."
            || relativeToRoot.StartsWith(
                ".." + Path.DirectorySeparatorChar,
                StringComparison.Ordinal
            )
            || Path.IsPathFullyQualified(relativeToRoot)
        )
            throw new ArgumentException("Content paths cannot leave their content root.");
        return candidatePath;
    }

    /// <summary>
    /// Deserializes one mod definition from disk.
    /// </summary>
    /// <param name="definitionPath">The absolute mod-definition path.</param>
    /// <returns>The deserialized definition.</returns>
    public static ContentModDefinition ReadModDefinition(string definitionPath)
    {
        using XmlReader reader = XmlReader.Create(definitionPath);
        var document = new XmlDocument();
        document.Load(reader);
        return ReadModDefinition(document);
    }

    /// <summary>
    /// Reads and validates an authored or proposed mod manifest.
    /// </summary>
    /// <param name="document">The manifest XML, with either supported root.</param>
    /// <returns>The parsed mod definition.</returns>
    public static ContentModDefinition ReadModDefinition(XmlDocument document)
    {
        XmlElement root = document.DocumentElement;
        if (
            ContentPackageManifest.ReadType(root.Name, root.GetAttribute("Type"))
            != ContentPackageType.Mod
        )
            throw new InvalidDataException("The package manifest must declare Type=\"Mod\".");
        GameSerializer serializer = new(
            typeof(ContentModDefinition),
            new GameSerializerSettings { RootName = root.Name }
        );
        return serializer.Deserialize(document) as ContentModDefinition
            ?? throw new InvalidDataException("Failed to deserialize content mod.");
    }

    /// <summary>
    /// Validates the required identity fields of one compatible mod definition.
    /// </summary>
    /// <param name="definition">The definition to validate.</param>
    private static void ValidateDefinition(ContentModDefinition definition)
    {
        ValidateRequiredDefinitionField(definition.ID, nameof(definition.ID));
        ValidateRequiredDefinitionField(definition.Version, nameof(definition.Version));
        ValidateRequiredDefinitionField(definition.DisplayName, nameof(definition.DisplayName));
        ValidateRequiredDefinitionField(definition.BasePackID, nameof(definition.BasePackID));
        ValidateRequiredDefinitionField(
            definition.BasePackVersion,
            nameof(definition.BasePackVersion)
        );
        foreach (ContentModRequirement requirement in definition.Requires)
            ValidateRequiredDefinitionField(requirement?.ID, "Requires/Mod/ID");
        foreach (
            string id in definition
                .LoadAfter.Concat(definition.LoadBefore)
                .Concat(definition.IncompatibleWith)
        )
            ValidateRequiredDefinitionField(id, "ModID");
    }

    /// <summary>
    /// Validates one required content-mod definition field.
    /// </summary>
    /// <param name="value">The deserialized field value.</param>
    /// <param name="fieldName">The XML element name.</param>
    private static void ValidateRequiredDefinitionField(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException(
                $"Content mod definition is missing required field '{fieldName}'."
            );
    }

    /// <summary>
    /// Determines whether a definition failure is isolated to mod-authored data or its file.
    /// </summary>
    /// <param name="exception">The failure raised while reading one definition.</param>
    /// <returns><see langword="true"/> when discovery can safely skip the definition.</returns>
    private static bool IsInvalidModDefinitionException(Exception exception)
    {
        return exception
            is InvalidDataException
                or IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or XmlException
                or FormatException;
    }
}
