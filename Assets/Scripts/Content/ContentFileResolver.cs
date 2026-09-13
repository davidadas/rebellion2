using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private const string _modDefinitionFileName = "mod.xml";
    private const string _modContentDirectoryName = "Content";
    private const string _modsDirectoryName = "Mods";

    private readonly IReadOnlyList<string> modContentRoots;

    public string ContentRootPath { get; }

    public string PackRootPath { get; }

    public IReadOnlyList<ContentModDefinition> Mods { get; }

    /// <summary>
    /// Gets every compatible discovered mod, including disabled mods.
    /// </summary>
    public IReadOnlyList<ContentModDefinition> AvailableMods { get; }

    /// <summary>
    /// Creates a resolver over explicit mod content roots in ascending load order.
    /// </summary>
    /// <param name="contentRootPath">The absolute application content root.</param>
    /// <param name="packRootPath">The absolute selected pack root.</param>
    /// <param name="modContentRootPaths">The mod content roots in ascending load order.</param>
    /// <param name="mods">The definitions corresponding to the mod content roots.</param>
    /// <param name="availableMods">All compatible definitions, including disabled mods.</param>
    public ContentFileResolver(
        string contentRootPath,
        string packRootPath,
        IEnumerable<string> modContentRootPaths = null,
        IEnumerable<ContentModDefinition> mods = null,
        IEnumerable<ContentModDefinition> availableMods = null
    )
    {
        ContentRootPath = Path.GetFullPath(
            contentRootPath ?? throw new ArgumentNullException(nameof(contentRootPath))
        );
        PackRootPath = Path.GetFullPath(
            packRootPath ?? throw new ArgumentNullException(nameof(packRootPath))
        );
        modContentRoots = (modContentRootPaths ?? Enumerable.Empty<string>())
            .Select(Path.GetFullPath)
            .ToArray();
        Mods = (mods ?? Enumerable.Empty<ContentModDefinition>()).ToArray();
        AvailableMods = (availableMods ?? Mods).ToArray();
    }

    /// <summary>
    /// Discovers mods compatible with a base pack from the directory beside Content.
    /// </summary>
    /// <param name="contentRootPath">The absolute application content root.</param>
    /// <param name="packRootPath">The absolute selected pack root.</param>
    /// <param name="basePackID">The selected base pack identifier.</param>
    /// <param name="disabledModIDs">Mod IDs to discover without loading.</param>
    /// <returns>A resolver containing the compatible discovered mods.</returns>
    internal static ContentFileResolver Discover(
        string contentRootPath,
        string packRootPath,
        string basePackID,
        IEnumerable<string> disabledModIDs = null
    )
    {
        string modsRoot = Path.Combine(
            Directory.GetParent(Path.GetFullPath(contentRootPath))?.FullName
                ?? throw new InvalidOperationException("The content root has no parent directory."),
            _modsDirectoryName
        );
        if (!Directory.Exists(modsRoot))
            return new ContentFileResolver(contentRootPath, packRootPath);

        List<string> contentRoots = new List<string>();
        List<ContentModDefinition> definitions = new List<ContentModDefinition>();
        List<ContentModDefinition> availableDefinitions = new List<ContentModDefinition>();
        HashSet<string> disabledIDs = new HashSet<string>(
            disabledModIDs ?? Enumerable.Empty<string>(),
            StringComparer.Ordinal
        );
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (
            string modRoot in Directory
                .EnumerateDirectories(modsRoot)
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
        )
        {
            string definitionPath = Path.Combine(modRoot, _modDefinitionFileName);
            if (!File.Exists(definitionPath))
                continue;

            ContentModDefinition definition = DeserializeDefinition(definitionPath);
            if (
                !string.IsNullOrWhiteSpace(definition.BasePackID)
                && !string.Equals(definition.BasePackID, basePackID, StringComparison.Ordinal)
            )
                continue;
            ValidateDefinition(definition, definitionPath);
            if (!ids.Add(definition.ID))
                throw new InvalidDataException(
                    $"Multiple content mods declare ID '{definition.ID}'."
                );

            availableDefinitions.Add(definition);
            if (!disabledIDs.Contains(definition.ID))
            {
                definitions.Add(definition);
                contentRoots.Add(Path.Combine(modRoot, _modContentDirectoryName));
            }
        }

        return new ContentFileResolver(
            contentRootPath,
            packRootPath,
            contentRoots,
            definitions,
            availableDefinitions
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
        for (int index = modContentRoots.Count - 1; index >= 0; index--)
        {
            string resolved = ResolveFromRoot(
                modContentRoots[index],
                normalizedAddress,
                extensions
            );
            if (resolved != null)
                return resolved;
        }

        string basePath = ResolveBasePath(normalizedAddress);
        if (File.Exists(basePath))
            return basePath;
        foreach (string extension in extensions)
        {
            if (File.Exists(basePath + extension))
                return basePath + extension;
        }
        return null;
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
        AddFiles(files, ResolveBasePath(normalizedAddress));
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
        return Directory.Exists(ResolveBasePath(normalizedAddress));
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
    /// Resolves a normalized address beneath the selected base content roots.
    /// </summary>
    /// <param name="normalizedAddress">The normalized scoped content address.</param>
    /// <returns>The corresponding absolute base path.</returns>
    private string ResolveBasePath(string normalizedAddress)
    {
        return ResolveScopedPath(
            Path.Combine(ContentRootPath, _applicationScopeName),
            PackRootPath,
            normalizedAddress
        );
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
    private static ContentModDefinition DeserializeDefinition(string definitionPath)
    {
        GameSerializer serializer = new GameSerializer(
            typeof(ContentModDefinition),
            new GameSerializerSettings { RootName = nameof(ContentModDefinition) }
        );
        using FileStream stream = File.OpenRead(definitionPath);
        return serializer.Deserialize(stream) as ContentModDefinition
            ?? throw new InvalidDataException(
                $"Failed to deserialize content mod: {definitionPath}"
            );
    }

    /// <summary>
    /// Validates the required identity fields of one compatible mod definition.
    /// </summary>
    /// <param name="definition">The definition to validate.</param>
    /// <param name="definitionPath">The source path used in validation errors.</param>
    private static void ValidateDefinition(ContentModDefinition definition, string definitionPath)
    {
        if (
            string.IsNullOrWhiteSpace(definition.ID)
            || string.IsNullOrWhiteSpace(definition.Version)
            || string.IsNullOrWhiteSpace(definition.DisplayName)
        )
            throw new InvalidDataException(
                $"Content mod definition is incomplete: {definitionPath}"
            );
    }
}
