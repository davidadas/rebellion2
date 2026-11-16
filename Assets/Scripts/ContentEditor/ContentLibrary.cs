using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;
using Rebellion.Content.Modio;
using Rebellion.Util.Serialization;

/// <summary>
/// Discovers installed content independently of an editing project and owns guarded removals.
/// </summary>
public sealed class ContentLibrary
{
    private readonly string contentRoot;
    private readonly string modsRoot;

    /// <summary>
    /// Selects the installation whose content will be managed.
    /// </summary>
    /// <param name="contentRoot">The installed Content directory.</param>
    public ContentLibrary(string contentRoot)
    {
        this.contentRoot = Path.GetFullPath(
            contentRoot ?? throw new ArgumentNullException(nameof(contentRoot))
        );
        modsRoot = ContentFileResolver.ResolveModsRootPath(this.contentRoot);
    }

    /// <summary>
    /// Reads metadata for packs and overlay mods, retaining malformed entries for inspection.
    /// </summary>
    /// <returns>The installed entries sorted by display name.</returns>
    public IReadOnlyList<ContentLibraryItem> Discover()
    {
        return Directories(Path.Combine(contentRoot, "Packs"))
            .Select(path => Read(path, true))
            .Concat(Directories(modsRoot).Select(path => Read(path, false)))
            .Where(item => item != null)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Directory, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Removes a freshly discovered local installation while protecting built-in content and dependencies.
    /// </summary>
    /// <param name="directory">The selected immediate child of Mods.</param>
    public void Remove(string directory)
    {
        string path = Path.GetFullPath(
            directory ?? throw new ArgumentNullException(nameof(directory))
        );
        IReadOnlyList<ContentLibraryItem> entries = Discover();
        ContentLibraryItem selected = entries.FirstOrDefault(item =>
            string.Equals(item.Directory, path, StringComparison.Ordinal)
        );
        if (
            selected?.IsReadOnly != false
            || !string.Equals(Path.GetDirectoryName(path), modsRoot, StringComparison.Ordinal)
        )
            throw new IOException(
                "Only content in this installation's Mods folder can be removed."
            );
        ContentEditorPaths.VerifyWritableLocation(contentRoot, path);
        if (!ContentEditorPaths.Contains(modsRoot, path))
            throw new IOException("The selected folder resolves outside Mods.");
        if (
            entries.Any(item =>
                item != selected
                && !string.IsNullOrEmpty(selected.ID)
                && item.BasePackID == selected.ID
            )
        )
            throw new IOException(
                "Other installed content depends on this pack. Remove that content first."
            );
        RequireOrdinaryDirectory(path);
        Directory.Delete(path, true);
    }

    /// <summary>
    /// Enumerates an optional content directory without creating it during discovery.
    /// </summary>
    /// <param name="root">The parent directory.</param>
    /// <returns>Its immediate child directories.</returns>
    private static IEnumerable<string> Directories(string root) =>
        Directory.Exists(root) ? Directory.GetDirectories(root) : Array.Empty<string>();

    /// <summary>
    /// Rejects directory links before a recursive removal.
    /// </summary>
    /// <param name="path">The directory being inspected.</param>
    private static void RequireOrdinaryDirectory(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException(
                "Linked content folders cannot be removed here. Remove the link in your file manager."
            );
        foreach (string child in Directory.GetDirectories(path))
            RequireOrdinaryDirectory(child);
    }

    /// <summary>
    /// Reads one package's manifest and optional managed-installation record.
    /// </summary>
    /// <param name="directory">The package root.</param>
    /// <param name="readOnly">Whether the application owns this installed directory.</param>
    /// <returns>The entry, or null for folders without a content manifest.</returns>
    private static ContentLibraryItem Read(string directory, bool readOnly)
    {
        bool pack = File.Exists(Path.Combine(directory, "pack.xml"));
        if (!pack && !File.Exists(Path.Combine(directory, "mod.xml")))
            return null;
        ContentLibraryItem item = new ContentLibraryItem
        {
            Directory = Path.GetFullPath(directory),
            Name = Path.GetFileName(directory),
            IsReadOnly = readOnly,
            IsPack = pack,
        };
        try
        {
            if (pack)
            {
                ContentPackDefinition definition =
                    ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(
                        Path.Combine(directory, "pack.xml")
                    );
                item.ID = definition.ID;
                item.Name = definition.DisplayName ?? definition.ID;
                item.Version = definition.Version;
                item.BasePackID = definition.BasePackID;
            }
            else
            {
                GameSerializer serializer = new GameSerializer(
                    typeof(ContentModDefinition),
                    new GameSerializerSettings { RootName = nameof(ContentModDefinition) }
                );
                using FileStream stream = File.OpenRead(Path.Combine(directory, "mod.xml"));
                ContentModDefinition definition = (ContentModDefinition)
                    serializer.Deserialize(stream);
                item.ID = definition.ID;
                item.Name = definition.DisplayName ?? definition.ID;
                item.Version = definition.Version;
                item.BasePackID = definition.BasePackID;
            }
            if (string.IsNullOrWhiteSpace(item.ID) || string.IsNullOrWhiteSpace(item.Version))
                throw new InvalidDataException(
                    "The content definition must declare an ID and version."
                );
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or InvalidOperationException
                        or XmlException
                        or ArgumentException
            )
        {
            item.Error = exception.GetBaseException().Message;
        }
        try
        {
            ModioInstallation record = ModioInstallation.Read(directory);
            if (
                record?.SchemaVersion == 1
                && record.GameID > 0
                && record.ModID > 0
                && record.FileID > 0
                && record.ContentID == item.ID
            )
                item.Installation = record;
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or SerializationException)
        {
            item.Error = "The mod.io installation record could not be read. " + exception.Message;
        }
        if (string.IsNullOrWhiteSpace(item.Name))
            item.Name = string.IsNullOrWhiteSpace(item.ID) ? Path.GetFileName(directory) : item.ID;
        return item;
    }
}

/// <summary>
/// Contains lightweight library metadata without loading game data or media.
/// </summary>
public sealed class ContentLibraryItem
{
    public string Directory { get; internal set; }
    public string ID { get; internal set; }
    public string Name { get; internal set; }
    public string Version { get; internal set; }
    public string BasePackID { get; internal set; }
    public bool IsReadOnly { get; internal set; }
    public bool IsPack { get; internal set; }
    public string Error { get; internal set; }
    public ModioInstallation Installation { get; internal set; }
}
