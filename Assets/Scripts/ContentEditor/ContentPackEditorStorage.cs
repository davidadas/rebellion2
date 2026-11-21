using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Persists authored pack documents and imported files without owning editing or presentation state.
/// </summary>
public static class ContentPackEditorStorage
{
    private const string _defaultGameConfigAddress = "Pack/Shared/Data/game-config.xml";
    private const string _packDefinitionAddress = "Pack/pack.xml";

    /// <summary>
    /// Opens one installed pack through its inheritance chain.
    /// </summary>
    /// <param name="contentRootPath">The external Content directory.</param>
    /// <param name="packID">The pack identifier to open.</param>
    /// <returns>The editable project.</returns>
    public static ContentPackEditorProject Open(string contentRootPath, string packID)
    {
        ContentFileResolver resolver = ContentPackLoader.CreatePackResolver(
            contentRootPath,
            packID
        );
        ContentPackDefinition definition =
            ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(
                Path.Combine(resolver.PackRootPath, "pack.xml")
            );
        return new ContentPackEditorProject(
            resolver,
            definition,
            CreateDocuments(resolver, definition)
        );
    }

    /// <summary>
    /// Opens an authored pack folder while resolving inherited assets from installed content.
    /// </summary>
    /// <param name="contentRootPath">The installed application Content directory.</param>
    /// <param name="packRootPath">The authored directory containing pack.xml.</param>
    /// <returns>The editable project.</returns>
    public static ContentPackEditorProject OpenFolder(string contentRootPath, string packRootPath)
    {
        ContentEditorPaths.VerifyWritableLocation(contentRootPath, packRootPath);
        ContentPackDefinition definition =
            ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(
                Path.Combine(packRootPath, "pack.xml")
            );
        ContentFileResolver resolver = ContentPackLoader.CreateFolderResolver(
            contentRootPath,
            packRootPath,
            definition
        );
        return new ContentPackEditorProject(
            resolver,
            definition,
            CreateDocuments(resolver, definition)
        );
    }

    /// <summary>
    /// Creates a derived pack that initially inherits every authored document from a base pack.
    /// </summary>
    /// <param name="contentRootPath">The external Content directory.</param>
    /// <param name="basePackID">The installed base-pack identifier.</param>
    /// <param name="packID">The new stable pack identifier.</param>
    /// <param name="displayName">The new user-facing name.</param>
    /// <param name="version">The initial pack version.</param>
    /// <returns>The newly created editable project.</returns>
    public static ContentPackEditorProject Create(
        string contentRootPath,
        string basePackID,
        string packID,
        string displayName,
        string version
    )
    {
        ValidateIdentity(packID, displayName, version);
        return CreateInFolder(
            contentRootPath,
            basePackID,
            Path.Combine(ContentFileResolver.ResolveModsRootPath(contentRootPath), packID),
            packID,
            displayName,
            version
        );
    }

    /// <summary>
    /// Creates a derived pack in a chosen working folder without modifying installed base content.
    /// </summary>
    /// <param name="contentRootPath">The installed Content directory.</param>
    /// <param name="basePackID">The installed pack to inherit.</param>
    /// <param name="packRootPath">The new working folder.</param>
    /// <param name="packID">The new stable identifier.</param>
    /// <param name="displayName">The new display name.</param>
    /// <param name="version">The initial version.</param>
    /// <returns>The newly opened project.</returns>
    public static ContentPackEditorProject CreateInFolder(
        string contentRootPath,
        string basePackID,
        string packRootPath,
        string packID,
        string displayName,
        string version
    )
    {
        ValidateIdentity(packID, displayName, version);
        ContentFileResolver baseResolver = ContentPackLoader.CreatePackResolver(
            contentRootPath,
            basePackID
        );
        ContentPackDefinition baseDefinition =
            ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(
                Path.Combine(baseResolver.PackRootPath, "pack.xml")
            );

        ContentEditorPaths.VerifyWritableLocation(contentRootPath, packRootPath);
        string packRoot = Path.GetFullPath(packRootPath);
        bool directoryExisted = Directory.Exists(packRoot);
        if (
            File.Exists(packRoot)
            || directoryExisted && Directory.EnumerateFileSystemEntries(packRoot).Any()
        )
            throw new IOException("Choose a new or empty content-pack folder.");
        foreach (string inheritedRoot in baseResolver.PackRootPaths)
        {
            if (ContentEditorPaths.Contains(inheritedRoot, packRoot))
                throw new IOException(
                    "Choose a working folder outside the inherited content pack."
                );
        }
        if (
            ContentPackLoader
                .DiscoverPacks(contentRootPath)
                .Any(pack => string.Equals(pack.ID, packID, StringComparison.Ordinal))
        )
            throw new InvalidDataException($"A content pack already declares ID '{packID}'.");

        ContentPackDefinition definition = CloneDefinition(
            baseDefinition,
            packID.Trim(),
            displayName.Trim(),
            version.Trim()
        );
        try
        {
            Directory.CreateDirectory(packRoot);
            ContentDefinitionSerializer.Serialize(Path.Combine(packRoot, "pack.xml"), definition);
            return OpenFolder(contentRootPath, packRoot);
        }
        catch
        {
            string manifestPath = Path.Combine(packRoot, "pack.xml");
            if (File.Exists(manifestPath))
                File.Delete(manifestPath);
            if (
                !directoryExisted
                && Directory.Exists(packRoot)
                && !Directory.EnumerateFileSystemEntries(packRoot).Any()
            )
                Directory.Delete(packRoot);
            throw;
        }
    }

    /// <summary>
    /// Creates the editable XML document collection in stable path order.
    /// </summary>
    /// <param name="resolver">The selected pack's layered resolver.</param>
    /// <param name="definition">The selected pack manifest.</param>
    /// <returns>The editable documents.</returns>
    internal static List<ContentPackEditorDocument> CreateDocuments(
        ContentFileResolver resolver,
        ContentPackDefinition definition
    )
    {
        IEnumerable<string> addresses = GetReferencedDocumentAddresses(resolver, definition)
            .OrderBy(address => address, StringComparer.Ordinal);

        List<ContentPackEditorDocument> result = addresses
            .Select(address =>
            {
                string relativePath = address[5..];
                string sourcePath =
                    address == _packDefinitionAddress
                        ? Path.Combine(resolver.PackRootPath, "pack.xml")
                        : resolver.ResolveFile(address)
                            ?? throw new FileNotFoundException(
                                $"Content document not found: {address}"
                            );
                string targetPath = Path.GetFullPath(relativePath, resolver.PackRootPath);
                return OpenDocument(resolver, address, sourcePath, targetPath);
            })
            .ToList();
        if (
            string.IsNullOrWhiteSpace(definition.GameConfigPath)
            && !result.Any(document => document.Address == _defaultGameConfigAddress)
        )
        {
            string defaultRules = resolver.ResolveFile("Application/Rules/game.xml");
            if (defaultRules != null)
            {
                string defaults = ContentXmlOverrides.Load(resolver, "Application/Rules/game.xml");
                result.Add(
                    new ContentPackEditorDocument(
                        _defaultGameConfigAddress,
                        defaultRules,
                        Path.Combine(resolver.PackRootPath, "Shared", "Data", "game-config.xml"),
                        File.ReadAllText(defaultRules),
                        defaults,
                        resolver
                            .ResolveFileLayers("Application/Rules/game.xml")
                            .ToDictionary(path => path, File.ReadAllText),
                        true,
                        defaults
                    )
                );
            }
        }
        return result;
    }

    /// <summary>
    /// Follows the selected pack manifest to every XML document used by the pack.
    /// </summary>
    /// <param name="resolver">The selected pack's layered resolver.</param>
    /// <param name="definition">The selected pack manifest.</param>
    /// <returns>The referenced document addresses, including the root manifest.</returns>
    private static IReadOnlyCollection<string> GetReferencedDocumentAddresses(
        ContentFileResolver resolver,
        ContentPackDefinition definition
    )
    {
        HashSet<string> addresses = new HashSet<string>(StringComparer.Ordinal)
        {
            _packDefinitionAddress,
        };
        AddDocumentAddress(addresses, definition.GameConfigPath);
        AddDocumentAddress(addresses, definition.PlanetSectorsPath);
        AddDocumentAddress(addresses, definition.BuildingsPath);
        AddDocumentAddress(addresses, definition.OfficerTraitsPath);
        AddDocumentAddress(addresses, definition.GameEventsPath);
        AddDocumentAddress(addresses, definition.MessageDefinitionsPath);
        AddDocumentAddress(addresses, definition.EncyclopediaEntriesPath);
        AddDocumentAddress(addresses, definition.NeutralThemePath);
        if (
            string.IsNullOrWhiteSpace(definition.GameConfigPath)
            && resolver.ResolveFile(_defaultGameConfigAddress) != null
        )
            addresses.Add(_defaultGameConfigAddress);

        foreach (string path in definition.FactionPaths)
        {
            string address = AddDocumentAddress(addresses, path);
            ContentFactionDefinition faction =
                DeserializeReferencedDefinition<ContentFactionDefinition>(resolver, address);
            AddDocumentAddress(addresses, faction.FactionDataPath);
            AddDocumentAddress(addresses, faction.CapitalShipsPath);
            AddDocumentAddress(addresses, faction.StarfightersPath);
            AddDocumentAddress(addresses, faction.RegimentsPath);
            AddDocumentAddress(addresses, faction.SpecialForcesPath);
            AddDocumentAddress(addresses, faction.OfficersPath);
            AddDocumentAddress(addresses, faction.EncyclopediaEntriesPath);
            AddDocumentAddress(addresses, faction.ThemePath);
        }

        foreach (string path in definition.ScenarioPaths)
        {
            string address = AddDocumentAddress(addresses, path);
            ContentScenarioDefinition scenario =
                DeserializeReferencedDefinition<ContentScenarioDefinition>(resolver, address);
            AddDocumentAddress(addresses, scenario.GenerationConfigPath);
        }

        foreach (ContentPreloadDefinition preload in definition.Preloads)
            AddDocumentAddress(addresses, preload?.Path);

        return addresses;
    }

    /// <summary>
    /// Adds a pack-relative document path to the discovered address set.
    /// </summary>
    /// <param name="addresses">The discovered logical addresses.</param>
    /// <param name="relativePath">The path declared by a pack manifest.</param>
    /// <returns>The logical address, or null when the path is empty.</returns>
    private static string AddDocumentAddress(ISet<string> addresses, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;
        string address = "Pack/" + relativePath.Replace('\\', '/');
        addresses.Add(address);
        return address;
    }

    /// <summary>
    /// Reads a referenced definition after composing its inherited content layers.
    /// </summary>
    /// <typeparam name="T">The referenced definition type.</typeparam>
    /// <param name="resolver">The selected pack's layered resolver.</param>
    /// <param name="address">The definition's logical address.</param>
    /// <returns>The resolved definition.</returns>
    private static T DeserializeReferencedDefinition<T>(
        ContentFileResolver resolver,
        string address
    )
        where T : class
    {
        return ContentDefinitionSerializer.DeserializeText<T>(
            ContentXmlOverrides.Load(resolver, address)
        );
    }

    /// <summary>
    /// Retains authored XML separately from every resolved inherited dependency.
    /// </summary>
    /// <param name="resolver">The pack's content layers.</param>
    /// <param name="address">The logical document address.</param>
    /// <param name="sourcePath">The highest existing source.</param>
    /// <param name="targetPath">The local override destination.</param>
    /// <returns>The editor document with its inherited baseline.</returns>
    private static ContentPackEditorDocument OpenDocument(
        ContentFileResolver resolver,
        string address,
        string sourcePath,
        string targetPath
    )
    {
        Dictionary<string, string> sources = resolver
            .ResolveFileLayers(address)
            .ToDictionary(path => path, File.ReadAllText, StringComparer.Ordinal);
        string baseline = null;
        string applicationDefaults = null;
        bool hasInheritedPackDocument = sources.Keys.Any(path =>
            !string.Equals(path, targetPath, StringComparison.OrdinalIgnoreCase)
        );
        if (System.Xml.Linq.XElement.Parse(sources[sourcePath]).Name.LocalName == "GameConfig")
        {
            applicationDefaults = ContentXmlOverrides.Load(resolver, "Application/Rules/game.xml");
            baseline = applicationDefaults;
            foreach (string path in resolver.ResolveFileLayers("Application/Rules/game.xml"))
                sources[path] = File.ReadAllText(path);
        }
        if (address != _packDefinitionAddress)
            foreach (string path in resolver.ResolveFileLayers(address))
                if (!string.Equals(path, targetPath, StringComparison.OrdinalIgnoreCase))
                    baseline =
                        ContentXmlOverrides.IsOverlay(sources[path])
                            ? ContentXmlOverrides.Merge(baseline, sources[path])
                        : applicationDefaults == null ? sources[path]
                        : ContentXmlOverrides.MergeRules(applicationDefaults, sources[path]);
        return new ContentPackEditorDocument(
            address,
            sourcePath,
            targetPath,
            File.ReadAllText(sourcePath),
            baseline,
            sources,
            applicationDefaults != null && !hasInheritedPackDocument,
            applicationDefaults
        );
    }

    /// <summary>
    /// Copies the base manifest while assigning the new pack identity.
    /// </summary>
    /// <param name="source">The base manifest.</param>
    /// <param name="packID">The new pack identifier.</param>
    /// <param name="displayName">The new display name.</param>
    /// <param name="version">The new version.</param>
    /// <returns>The derived manifest.</returns>
    private static ContentPackDefinition CloneDefinition(
        ContentPackDefinition source,
        string packID,
        string displayName,
        string version
    )
    {
        return new ContentPackDefinition
        {
            ID = packID,
            BasePackID = source.ID,
            Version = version,
            DisplayName = displayName,
            DefaultScenarioID = source.DefaultScenarioID,
            MainMenuMusicPath = source.MainMenuMusicPath,
            GameConfigPath = source.GameConfigPath,
            PlanetSectorsPath = source.PlanetSectorsPath,
            BuildingsPath = source.BuildingsPath,
            OfficerTraitsPath = source.OfficerTraitsPath,
            GameEventsPath = source.GameEventsPath,
            MessageDefinitionsPath = source.MessageDefinitionsPath,
            EncyclopediaEntriesPath = source.EncyclopediaEntriesPath,
            NeutralThemePath = source.NeutralThemePath,
            FactionPaths = new List<string>(source.FactionPaths),
            ScenarioPaths = new List<string>(source.ScenarioPaths),
            Preloads = source.Preloads.ConvertAll(preload => new ContentPreloadDefinition
            {
                ID = preload.ID,
                Path = preload.Path,
            }),
        };
    }

    /// <summary>
    /// Rejects incomplete content-pack identity fields before touching disk.
    /// </summary>
    /// <param name="packID">The proposed stable identifier.</param>
    /// <param name="displayName">The proposed display name.</param>
    /// <param name="version">The proposed version.</param>
    private static void ValidateIdentity(string packID, string displayName, string version)
    {
        if (string.IsNullOrWhiteSpace(packID))
            throw new ArgumentException("A content-pack ID is required.", nameof(packID));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("A display name is required.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("A version is required.", nameof(version));
        if (
            packID.Any(character =>
                !(char.IsLower(character) || char.IsDigit(character) || character == '-')
            )
            || packID[0] == '-'
            || packID[^1] == '-'
            || packID.Contains("--", StringComparison.Ordinal)
        )
            throw new ArgumentException(
                "Content-pack IDs must use lowercase letters or digits separated by single hyphens.",
                nameof(packID)
            );
    }

    /// <summary>
    /// Imports a media file into this pack without replacing another authored asset.
    /// </summary>
    /// <param name="project">The project receiving the imported file.</param>
    /// <param name="sourcePath">The local media file.</param>
    /// <returns>The imported pack-scoped content address.</returns>
    public static string ImportAsset(ContentPackEditorProject project, string sourcePath)
    {
        return ImportAssetToFolder(project, sourcePath, "Pack/Assets");
    }

    /// <summary>
    /// Imports media using the destination and storage convention declared by the field.
    /// </summary>
    /// <param name="project">The project receiving the imported file.</param>
    /// <param name="sourcePath">The selected source file.</param>
    /// <param name="field">The target field.</param>
    /// <returns>The value to store in the field.</returns>
    public static string ImportAsset(
        ContentPackEditorProject project,
        string sourcePath,
        ContentPackEditorField field
    )
    {
        project.RequireOwnedRecord(field.Record);
        if (field.IsMediaDirectory || !field.IsMedia)
            throw new ArgumentException("Choose a media file field.");
        if (
            !field.MediaExtensions.Contains(
                Path.GetExtension(sourcePath),
                StringComparer.OrdinalIgnoreCase
            )
        )
            throw new ArgumentException("Unsupported file type.");
        string root = field.MediaRoot;
        string folder =
            ContentEditorContentSections.IsFactionMissionIcon(field)
            && field.Record.Catalog.Faction != "Shared"
                ? "Pack/Factions/" + field.Record.Catalog.Faction + "/Assets"
                : "Pack/Assets";
        string address = ImportAssetToFolder(project, sourcePath, root ?? folder);
        return root == null ? address : address[(root.TrimEnd('/').Length + 1)..];
    }

    /// <summary>
    /// Reports whether a scalar media field currently points to a writable file in this pack.
    /// </summary>
    /// <param name="project">The project that owns the working pack.</param>
    /// <param name="field">The media field whose current file may be replaced.</param>
    /// <returns>True when the current value resolves to an existing pack-owned file.</returns>
    public static bool CanReplaceAsset(
        ContentPackEditorProject project,
        ContentPackEditorField field
    )
    {
        if (
            project == null
            || field == null
            || !field.IsMedia
            || field.IsMediaDirectory
            || field.IsList
            || string.IsNullOrWhiteSpace(field.Value)
        )
            return false;
        string address = field.GetMediaAddress(field.Value);
        if (!address.StartsWith("Pack/", StringComparison.Ordinal))
            return false;
        if (
            ContentEditorContentSections.IsFactionMissionIcon(field)
            && !address.StartsWith(
                "Pack/Factions/" + field.Record.Catalog.Faction + "/",
                StringComparison.Ordinal
            )
        )
            return false;
        try
        {
            string target = Path.GetFullPath(address[5..], project.PackRootPath);
            ContentEditorPaths.VerifyWritePath(project, target);
            return File.Exists(target);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Replaces the current pack-owned media file while preserving its existing content address.
    /// </summary>
    /// <param name="project">The project that owns the working pack.</param>
    /// <param name="sourcePath">The replacement local media file.</param>
    /// <param name="field">The scalar media field whose current file is replaced.</param>
    public static void ReplaceAsset(
        ContentPackEditorProject project,
        string sourcePath,
        ContentPackEditorField field
    )
    {
        if (!CanReplaceAsset(project, field))
            throw new IOException("The current media file is not writable in this content pack.");
        string source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source))
            throw new FileNotFoundException("The selected media file does not exist.", source);
        string address = field.GetMediaAddress(field.Value);
        string target = Path.GetFullPath(address[5..], project.PackRootPath);
        if (
            !string.Equals(
                Path.GetExtension(source),
                Path.GetExtension(target),
                StringComparison.OrdinalIgnoreCase
            )
        )
            throw new ArgumentException(
                "Replacing a media file requires the same file type as the current asset."
            );
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            return;

        ContentEditorPaths.VerifyWritePath(project, target);
        string temporary = Path.Combine(
            Path.GetDirectoryName(target),
            "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".tmp"
        );
        ContentEditorPaths.VerifyWritePath(project, temporary);
        try
        {
            File.Copy(source, temporary);
            File.Replace(temporary, target, null);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    /// <summary>
    /// Writes a generated PNG to a pack-owned output and returns its stored value.
    /// </summary>
    /// <param name="project">The project receiving the generated image.</param>
    /// <param name="field">The unit image field represented by the output.</param>
    /// <param name="pngBytes">The complete encoded PNG file.</param>
    /// <param name="preservePrevious">Whether to give each distinct image a reusable address for undo.</param>
    /// <returns>The pack-scoped or media-root-relative value to store in the field.</returns>
    /// <param name="layerRecipe">Optional editor layer data that belongs to this exact output.</param>
    public static string WriteGeneratedPng(
        ContentPackEditorProject project,
        ContentPackEditorField field,
        byte[] pngBytes,
        bool preservePrevious = false,
        string layerRecipe = null
    )
    {
        if (project == null)
            throw new ArgumentNullException(nameof(project));
        if (field == null)
            throw new ArgumentNullException(nameof(field));
        if (pngBytes == null || pngBytes.Length == 0)
            throw new ArgumentException("Generated image data is required.", nameof(pngBytes));
        project.RequireOwnedRecord(field.Record);
        if (
            field.MediaType != ContentPackEditorMediaType.Image
            || field.IsMediaDirectory
            || field.IsList
        )
            throw new ArgumentException("Choose one image field.", nameof(field));

        string root = field.MediaRoot?.TrimEnd('/') ?? "Pack/Assets";
        if (!root.StartsWith("Pack/", StringComparison.Ordinal))
            throw new IOException("Generated images require a writable pack media folder.");
        string fileName = GetGeneratedPathSegment(GetGeneratedMemberName(field.MemberName));
        if (preservePrevious)
        {
            using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(
                System.Security.Cryptography.HashAlgorithmName.SHA256
            );
            hash.AppendData(pngBytes);
            if (layerRecipe != null)
                hash.AppendData(System.Text.Encoding.UTF8.GetBytes(layerRecipe));
            fileName +=
                "-"
                + BitConverter
                    .ToString(hash.GetHashAndReset(), 0, 12)
                    .Replace("-", "")
                    .ToLowerInvariant();
        }
        string address = string.Join(
            "/",
            root,
            "Generated",
            GetGeneratedPathSegment(field.Record.Kind),
            GetGeneratedPathSegment(field.Record.ID),
            fileName + ".png"
        );
        string target = Path.GetFullPath(address[5..], project.PackRootPath);
        ContentEditorPaths.VerifyWritePath(project, target);
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        WriteBytesAtomically(target, pngBytes);
        if (layerRecipe != null)
            WriteBytesAtomically(
                target + ".layers.json",
                System.Text.Encoding.UTF8.GetBytes(layerRecipe)
            );
        return field.MediaRoot == null ? address : address[(root.Length + 1)..];
    }

    /// <summary>
    /// Stores the exact source image needed to reconstruct imported layers after reopening a pack.
    /// </summary>
    /// <param name="project">The pack receiving the layer source.</param>
    /// <param name="sourcePath">The resolved original state image.</param>
    /// <returns>The immutable pack address of the captured source image.</returns>
    internal static string StoreArtworkLayerSource(
        ContentPackEditorProject project,
        string sourcePath
    )
    {
        byte[] image = File.ReadAllBytes(sourcePath);
        using SHA256 hash = SHA256.Create();
        string key = BitConverter
            .ToString(hash.ComputeHash(image))
            .Replace("-", "")
            .ToLowerInvariant();
        string address =
            "Pack/Assets/ArtworkSources/" + key + Path.GetExtension(sourcePath).ToLowerInvariant();
        string target = Path.GetFullPath(address[5..], project.PackRootPath);
        ContentEditorPaths.VerifyWritePath(project, target);
        if (!File.Exists(target))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            WriteBytesAtomically(target, image);
        }
        return address;
    }

    /// <summary>
    /// Reads editable layers associated with a current generated artwork reference.
    /// </summary>
    /// <param name="project">The writable project being edited.</param>
    /// <param name="field">The field containing the current runtime artwork path.</param>
    /// <returns>The sidecar data, or null for artwork without editable layers.</returns>
    internal static string ReadGeneratedLayers(
        ContentPackEditorProject project,
        ContentPackEditorField field
    )
    {
        if (string.IsNullOrWhiteSpace(field.Value))
            return null;
        string address = field.GetMediaAddress(field.Value);
        if (
            !address.StartsWith("Pack/", StringComparison.Ordinal)
            || !address.Contains("/Generated/", StringComparison.Ordinal)
            || !address.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        )
            return null;
        string path = Path.GetFullPath(address[5..], project.PackRootPath) + ".layers.json";
        ContentEditorPaths.VerifyWritePath(project, path);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>
    /// Removes the conventional image suffix from a generated output member name.
    /// </summary>
    /// <param name="member">The serialized image member.</param>
    /// <returns>The semantic portion used by the generated filename.</returns>
    private static string GetGeneratedMemberName(string member)
    {
        const string suffix = "ImagePath";
        return member.EndsWith(suffix, StringComparison.Ordinal)
            ? member[..^suffix.Length]
            : member;
    }

    /// <summary>
    /// Converts an authored identifier into a stable readable directory or filename segment.
    /// </summary>
    /// <param name="value">The authored identifier.</param>
    /// <returns>A lowercase path-safe segment.</returns>
    private static string GetGeneratedPathSegment(string value)
    {
        StringBuilder result = new StringBuilder();
        bool separator = false;
        char previous = '\0';
        foreach (char character in value ?? string.Empty)
        {
            if (!char.IsLetterOrDigit(character))
            {
                separator = result.Length > 0;
                continue;
            }
            if (
                result.Length > 0
                && (separator || char.IsUpper(character) && char.IsLower(previous))
                && result[^1] != '-'
            )
                result.Append('-');
            result.Append(char.ToLowerInvariant(character));
            separator = false;
            previous = character;
        }
        return result.Length == 0 ? "item" : result.ToString();
    }

    /// <summary>
    /// Replaces or creates one generated file without exposing a partially written image.
    /// </summary>
    /// <param name="target">The verified absolute pack path.</param>
    /// <param name="bytes">The complete encoded file.</param>
    private static void WriteBytesAtomically(string target, byte[] bytes)
    {
        string temporary = Path.Combine(
            Path.GetDirectoryName(target),
            "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".tmp"
        );
        try
        {
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(target))
                File.Replace(temporary, target, null);
            else
                File.Move(temporary, target);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    /// <summary>
    /// Copies a file to a pack directory without overwriting an existing asset.
    /// </summary>
    /// <param name="project">The project receiving the imported file.</param>
    /// <param name="sourcePath">The selected source file.</param>
    /// <param name="directoryAddress">The pack-scoped destination folder.</param>
    /// <returns>The imported content address.</returns>
    private static string ImportAssetToFolder(
        ContentPackEditorProject project,
        string sourcePath,
        string directoryAddress
    )
    {
        ContentEditorPaths.VerifyWritableLocation(project.ContentRootPath, project.PackRootPath);
        string source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source))
            throw new FileNotFoundException("The selected media file does not exist.", source);
        if (!directoryAddress.StartsWith("Pack/", StringComparison.Ordinal))
            throw new ArgumentException("Import requires a folder inside your pack.");
        ContentFileResolver resolver = ContentPackLoader.CreateFolderResolver(
            project.ContentRootPath,
            project.PackRootPath,
            project.Definition
        );
        resolver.ResolveFile(directoryAddress);
        string directory = Path.GetFullPath(directoryAddress[5..], project.PackRootPath);
        ContentEditorPaths.VerifyWritePath(project, directory);
        Directory.CreateDirectory(directory);
        string fileName = Path.GetFileName(source);
        string target = Path.Combine(directory, fileName);
        int suffix = 2;
        while (File.Exists(target))
        {
            using SHA256 hash = SHA256.Create();
            using FileStream original = File.OpenRead(source);
            using FileStream existing = File.OpenRead(target);
            if (hash.ComputeHash(original).SequenceEqual(hash.ComputeHash(existing)))
                return directoryAddress.TrimEnd('/') + "/" + Path.GetFileName(target);
            target = Path.Combine(
                directory,
                Path.GetFileNameWithoutExtension(fileName)
                    + "-"
                    + suffix++
                    + Path.GetExtension(fileName)
            );
        }
        ContentEditorPaths.VerifyWritePath(project, target);
        File.Copy(source, target);
        return directoryAddress.TrimEnd('/') + "/" + Path.GetFileName(target);
    }

    /// <summary>
    /// Saves every changed document and refreshes the in-memory pack definition.
    /// </summary>
    /// <param name="project">The working documents to persist.</param>
    public static void Save(ContentPackEditorProject project)
    {
        ContentEditorPaths.VerifyWritableLocation(project.ContentRootPath, project.PackRootPath);
        ContentPackEditorDocument[] dirtyDocuments = project
            .AllDocuments.Where(document => document.IsDirty)
            .ToArray();
        foreach (ContentPackEditorDocument document in dirtyDocuments)
        {
            document.Validate();
            ContentEditorPaths.VerifyWritePath(project, document.TargetPath);
            VerifyUnchangedTarget(document);
        }
        ContentPackDefinition nextDefinition =
            ContentDefinitionSerializer.DeserializeText<ContentPackDefinition>(
                project
                    .AllDocuments.Single(document => document.Address == _packDefinitionAddress)
                    .Text
            );
        ValidateIdentity(nextDefinition.ID, nextDefinition.DisplayName, nextDefinition.Version);
        ContentFileResolver nextResolver = ContentPackLoader.CreateFolderResolver(
            project.ContentRootPath,
            project.PackRootPath,
            nextDefinition
        );
        bool baseChanged = !string.Equals(
            project.Definition.BasePackID,
            nextDefinition.BasePackID,
            StringComparison.Ordinal
        );
        foreach (ContentPackEditorDocument document in dirtyDocuments)
            SaveDocument(project, document);

        project.AcceptSave(
            nextDefinition,
            baseChanged ? CreateDocuments(nextResolver, nextDefinition) : null
        );
    }

    /// <summary>
    /// Creates an asset store for this pack's current inheritance chain.
    /// </summary>
    /// <param name="project">The workspace whose content layers are requested.</param>
    /// <returns>The owned asset store.</returns>
    public static ContentAssets CreateAssets(ContentPackEditorProject project)
    {
        return new ContentAssets(
            ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            )
        );
    }

    /// <summary>
    /// Validates and writes the working text to the selected pack's local layer.
    /// </summary>
    /// <param name="project">The owner of the write.</param>
    /// <param name="document">The validated working document.</param>
    private static void SaveDocument(
        ContentPackEditorProject project,
        ContentPackEditorDocument document
    )
    {
        document.Validate();
        ContentEditorPaths.VerifyWritePath(project, document.TargetPath);
        VerifyUnchangedTarget(document);

        if (document.IsRemoved || document.InheritsAll)
        {
            if (File.Exists(document.TargetPath))
                File.Delete(document.TargetPath);
            document.AcceptSave();
            return;
        }

        string directory = Path.GetDirectoryName(document.TargetPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new IOException($"Content document path has no directory: {document.TargetPath}");
        Directory.CreateDirectory(directory);

        string temporaryPath = document.TargetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, document.AuthoredText);
            ReplaceFile(temporaryPath, document.TargetPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        document.AcceptSave();
    }

    /// <summary>
    /// Refuses to overwrite a file that another process changed after this document was opened.
    /// </summary>
    /// <param name="document">The document and its last observed disk state.</param>
    private static void VerifyUnchangedTarget(ContentPackEditorDocument document)
    {
        document.VerifyUnchangedSources();
    }

    /// <summary>
    /// Moves a completed temporary file into place without exposing partial XML.
    /// </summary>
    /// <param name="temporaryPath">The complete temporary file.</param>
    /// <param name="destinationPath">The final document path.</param>
    private static void ReplaceFile(string temporaryPath, string destinationPath)
    {
        if (File.Exists(destinationPath))
            File.Replace(temporaryPath, destinationPath, null);
        else
            File.Move(temporaryPath, destinationPath);
    }
}
