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
    private const string _applicationAddressPrefix = "Application/";
    private const string _packAddressPrefix = "Pack/";
    private const string _modDefinitionFileName = "mod.xml";
    private const string _modContentDirectoryName = "Content";

    private readonly IReadOnlyList<string> modContentRoots;

    public string ContentRootPath { get; }

    public string PackRootPath { get; }

    public IReadOnlyList<ContentModDefinition> Mods { get; }

    /// <summary>
    /// Creates a resolver over explicit mod content roots in ascending load order.
    /// </summary>
    public ContentFileResolver(
        string contentRootPath,
        string packRootPath,
        IEnumerable<string> modContentRootPaths = null,
        IEnumerable<ContentModDefinition> mods = null
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
    }

    /// <summary>
    /// Discovers mods compatible with a base pack from the directory beside Content.
    /// </summary>
    internal static ContentFileResolver Discover(
        string contentRootPath,
        string packRootPath,
        string basePackID
    )
    {
        string modsRoot = Path.Combine(
            Directory.GetParent(Path.GetFullPath(contentRootPath))?.FullName
                ?? throw new InvalidOperationException("The content root has no parent directory."),
            "Mods"
        );
        if (!Directory.Exists(modsRoot))
            return new ContentFileResolver(contentRootPath, packRootPath);

        List<string> contentRoots = new List<string>();
        List<ContentModDefinition> definitions = new List<ContentModDefinition>();
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
            ValidateDefinition(definition, definitionPath);
            if (!string.Equals(definition.BasePackID, basePackID, StringComparison.Ordinal))
                continue;
            if (!ids.Add(definition.ID))
                throw new InvalidDataException(
                    $"Multiple content mods declare ID '{definition.ID}'."
                );

            definitions.Add(definition);
            contentRoots.Add(Path.Combine(modRoot, _modContentDirectoryName));
        }

        return new ContentFileResolver(contentRootPath, packRootPath, contentRoots, definitions);
    }

    /// <summary>
    /// Resolves an existing logical content address, preferring the last loaded mod.
    /// </summary>
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
    public IEnumerable<string> EnumerateFileAddresses(string directoryAddress)
    {
        string normalizedAddress = NormalizeAddress(directoryAddress).TrimEnd('/');
        Dictionary<string, string> files = new Dictionary<string, string>(StringComparer.Ordinal);
        AddFiles(files, ResolveBasePath(normalizedAddress));
        foreach (string modContentRoot in modContentRoots)
            AddFiles(files, ResolveSafePath(modContentRoot, normalizedAddress));
        return files
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => normalizedAddress + "/" + pair.Key);
    }

    /// <summary>
    /// Checks whether a logical directory exists in any content layer.
    /// </summary>
    public bool DirectoryExists(string directoryAddress)
    {
        string normalizedAddress = NormalizeAddress(directoryAddress).TrimEnd('/');
        if (modContentRoots.Any(root => Directory.Exists(ResolveSafePath(root, normalizedAddress))))
            return true;
        return Directory.Exists(ResolveBasePath(normalizedAddress));
    }

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

    private static string ResolveFromRoot(
        string rootPath,
        string normalizedAddress,
        IEnumerable<string> extensions
    )
    {
        string candidate = ResolveSafePath(rootPath, normalizedAddress);
        if (File.Exists(candidate))
            return candidate;
        foreach (string extension in extensions)
        {
            if (File.Exists(candidate + extension))
                return candidate + extension;
        }
        return null;
    }

    private string ResolveBasePath(string normalizedAddress)
    {
        if (normalizedAddress.StartsWith(_applicationAddressPrefix, StringComparison.Ordinal))
            return ResolveSafePath(ContentRootPath, normalizedAddress);
        if (normalizedAddress.StartsWith(_packAddressPrefix, StringComparison.Ordinal))
            return ResolveSafePath(PackRootPath, normalizedAddress[_packAddressPrefix.Length..]);

        throw new ArgumentException(
            $"Content addresses must begin with '{_applicationAddressPrefix}' or '{_packAddressPrefix}'.",
            nameof(normalizedAddress)
        );
    }

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

    private static string ResolveSafePath(string rootPath, string relativePath)
    {
        string absoluteRoot = Path.GetFullPath(rootPath);
        string candidatePath = Path.GetFullPath(Path.Combine(absoluteRoot, relativePath));
        string requiredPrefix =
            absoluteRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidatePath.StartsWith(requiredPrefix, StringComparison.Ordinal))
            throw new ArgumentException("Content paths cannot leave their content root.");
        return candidatePath;
    }

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

    private static void ValidateDefinition(ContentModDefinition definition, string definitionPath)
    {
        if (
            string.IsNullOrWhiteSpace(definition.ID)
            || string.IsNullOrWhiteSpace(definition.Version)
            || string.IsNullOrWhiteSpace(definition.DisplayName)
            || string.IsNullOrWhiteSpace(definition.BasePackID)
        )
            throw new InvalidDataException(
                $"Content mod definition is incomplete: {definitionPath}"
            );
    }
}
