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
            StartupIntroVideoPath = source.StartupIntroVideoPath,
            OpeningCrawlVideoPath = source.OpeningCrawlVideoPath,
            MainMenuLayout = source.MainMenuLayout,
            PlanetSectorWindowLayout = source.PlanetSectorWindowLayout,
            PlanetWindowHeaderLayout = source.PlanetWindowHeaderLayout,
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
        string unitAddress = ContentEditorArtworkPaths.GetAddress(field, preserveCurrent: false);
        if (unitAddress != null)
        {
            byte[] bytes = File.ReadAllBytes(sourcePath);
            UnityEngine.Texture2D texture = new UnityEngine.Texture2D(2, 2);
            try
            {
                if (!UnityEngine.ImageConversion.LoadImage(texture, bytes))
                    throw new InvalidDataException("The selected image could not be read.");
                bytes = UnityEngine.ImageConversion.EncodeToPNG(texture);
            }
            finally
            {
                if (UnityEngine.Application.isPlaying)
                    UnityEngine.Object.Destroy(texture);
                else
                    UnityEngine.Object.DestroyImmediate(texture);
            }
            string candidate = unitAddress;
            int suffix = 2;
            ContentFileResolver resolver = ContentPackLoader.CreateFolderResolver(
                project.ContentRootPath,
                project.PackRootPath,
                project.Definition
            );
            Dictionary<string, string> aliases = ContentArtworkAliases.Load(project.PackRootPath);
            while (
                aliases.ContainsKey(candidate)
                || field.MediaExtensions.Any(extension =>
                    File.Exists(Path.GetFullPath(candidate[5..] + extension, project.PackRootPath))
                )
            )
            {
                string existing = field
                    .MediaExtensions.Select(extension =>
                        Path.GetFullPath(candidate[5..] + extension, project.PackRootPath)
                    )
                    .FirstOrDefault(File.Exists);
                if (existing != null)
                {
                    ContentEditorPaths.VerifyWritePath(project, existing);
                    if (
                        string.Equals(
                            resolver.ResolveFile(candidate, field.MediaExtensions),
                            existing,
                            StringComparison.OrdinalIgnoreCase
                        ) && File.ReadAllBytes(existing).AsSpan().SequenceEqual(bytes)
                    )
                        return candidate;
                }
                candidate = unitAddress + "-" + suffix++;
            }
            return WriteUnitArtwork(project, field, candidate, bytes, null);
        }
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
    ) => GetReplaceableAssetPath(project, field) != null;

    /// <summary>
    /// Resolves a media field to its writable file, including an extensionless reference.
    /// </summary>
    /// <param name="project">The working pack.</param>
    /// <param name="field">The media field to resolve.</param>
    /// <returns>The local file, or null for a missing or inherited asset.</returns>
    internal static string GetReplaceableAssetPath(
        ContentPackEditorProject project,
        ContentPackEditorField field
    )
    {
        if (
            project == null
            || field?.IsMedia != true
            || field.IsMediaDirectory
            || field.IsList
            || string.IsNullOrWhiteSpace(field.Value)
        )
            return null;
        string address = field.GetMediaAddress(field.Value);
        if (!address.StartsWith("Pack/", StringComparison.Ordinal))
            return null;
        if (
            ContentEditorContentSections.IsFactionMissionIcon(field)
            && !address.StartsWith(
                "Pack/Factions/" + field.Record.Catalog.Faction + "/",
                StringComparison.Ordinal
            )
        )
            return null;
        try
        {
            string target = Path.GetFullPath(address[5..], project.PackRootPath);
            string resolved = File.Exists(target)
                ? target
                : field
                    .MediaExtensions.Select(extension => target + extension)
                    .FirstOrDefault(File.Exists);
            if (resolved != null)
                ContentEditorPaths.VerifyWritePath(project, resolved);
            return resolved;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Checks whether replacement can keep the original media format and address.
    /// </summary>
    /// <param name="sourcePath">The selected media file.</param>
    /// <param name="targetPath">The current media file.</param>
    /// <returns>True for the same format, including the two JPEG extensions.</returns>
    internal static bool HasMatchingMediaFormat(string sourcePath, string targetPath)
    {
        string source = Path.GetExtension(sourcePath).ToLowerInvariant();
        string target = Path.GetExtension(targetPath).ToLowerInvariant();
        return source == target || (source is ".jpg" or ".jpeg" && target is ".jpg" or ".jpeg");
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
        project.RequireOwnedRecord(field.Record);
        string target =
            GetReplaceableAssetPath(project, field)
            ?? throw new IOException(
                "The current media file is not writable in this content pack."
            );
        string source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source))
            throw new FileNotFoundException("The selected media file does not exist.", source);
        if (!HasMatchingMediaFormat(source, target))
            throw new ArgumentException(
                "Replacing a media file requires the same file type as the current asset."
            );
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            return;

        project.CopyAsset(target, source);
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
        string unitAddress = ContentEditorArtworkPaths.GetAddress(field);
        if (unitAddress != null)
            return WriteUnitArtwork(project, field, unitAddress, pngBytes, layerRecipe);
        string fileName = GetGeneratedPathSegment(GetGeneratedMemberName(field.MemberName));
        if (preservePrevious)
            fileName += "-" + GetArtworkRevision(pngBytes, layerRecipe);
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
        using (project.BeginEdit("Replace artwork"))
        {
            project.WriteAsset(target, pngBytes);
            if (layerRecipe != null)
                project.WriteAsset(target + ".layers.json", Encoding.UTF8.GetBytes(layerRecipe));
        }
        return field.MediaRoot == null ? address : address[(root.Length + 1)..];
    }

    /// <summary>
    /// Publishes one unit image and its editable layers as one reversible file operation.
    /// </summary>
    /// <param name="project">The pack receiving the artwork.</param>
    /// <param name="field">The unit image slot being edited.</param>
    /// <param name="address">The extensionless unit-folder address.</param>
    /// <param name="pngBytes">The complete PNG contents.</param>
    /// <param name="layerRecipe">The optional editable layer data.</param>
    /// <returns>The address stored in the unit definition.</returns>
    private static string WriteUnitArtwork(
        ContentPackEditorProject project,
        ContentPackEditorField field,
        string address,
        byte[] pngBytes,
        string layerRecipe
    )
    {
        string target = Path.GetFullPath(address[5..] + ".png", project.PackRootPath);
        ContentEditorPaths.VerifyWritePath(project, target);
        using (project.BeginEdit("Replace artwork"))
        {
            ContentEditorArtworkConversion.PreserveAddress(project, field, address);
            project.WriteAsset(target, pngBytes);
            if (layerRecipe != null)
            {
                project.WriteAsset(target + ".layers.json", Encoding.UTF8.GetBytes(layerRecipe));
                project.WriteAsset(
                    target + ".layers.hash",
                    Encoding.UTF8.GetBytes(GetArtworkRevision(pngBytes, layerRecipe))
                );
            }
        }
        return address;
    }

    /// <summary>
    /// Captures a draft's source bytes without publishing files or changing project state.
    /// </summary>
    /// <param name="sourcePath">The template or source image, or an absent optional template.</param>
    /// <param name="pending">The source bytes owned by the active artwork draft.</param>
    /// <returns>The portable source address, or null for an absent optional template.</returns>
    internal static string StageArtworkLayerSource(
        string sourcePath,
        IDictionary<string, byte[]> pending
    )
    {
        if (string.IsNullOrEmpty(sourcePath))
            return null;
        byte[] image = File.ReadAllBytes(sourcePath);
        string address = GetArtworkLayerSourceAddress(image, Path.GetExtension(sourcePath));
        pending[address] = image;
        return address;
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
        string address = GetArtworkLayerSourceAddress(image, Path.GetExtension(sourcePath));
        StoreArtworkLayerSource(project, address, image);
        return address;
    }

    /// <summary>
    /// Derives the pack address for an immutable source image before it is saved.
    /// </summary>
    /// <param name="image">The source image bytes.</param>
    /// <param name="extension">The image format extension.</param>
    /// <returns>The portable pack address for the image.</returns>
    internal static string GetArtworkLayerSourceAddress(byte[] image, string extension)
    {
        using SHA256 hash = SHA256.Create();
        string key = BitConverter
            .ToString(hash.ComputeHash(image))
            .Replace("-", "")
            .ToLowerInvariant();
        return "Pack/Assets/ArtworkSources/" + key + extension.ToLowerInvariant();
    }

    /// <summary>
    /// Writes an image at its derived pack address for use by saved artwork recipes.
    /// </summary>
    /// <param name="project">The pack receiving the layer source.</param>
    /// <param name="address">The derived pack address for the image.</param>
    /// <param name="image">The source image bytes.</param>
    internal static void StoreArtworkLayerSource(
        ContentPackEditorProject project,
        string address,
        byte[] image
    )
    {
        string target = Path.GetFullPath(address[5..], project.PackRootPath);
        ContentEditorPaths.VerifyWritePath(project, target);
        if (!File.Exists(target))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            project.WriteAsset(target, image);
        }
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
            || (
                !address.Contains("/Generated/", StringComparison.Ordinal)
                && ContentEditorArtworkPaths.GetAddress(field) == null
            )
        )
            return null;
        string imagePath = Path.GetFullPath(address[5..], project.PackRootPath);
        if (!File.Exists(imagePath) && File.Exists(imagePath + ".png"))
            imagePath += ".png";
        string sidecar = imagePath + ".layers.json";
        ContentEditorPaths.VerifyWritePath(project, sidecar);
        if (!File.Exists(sidecar) || !File.Exists(imagePath))
            return null;
        ContentEditorPaths.VerifyWritePath(project, imagePath);
        string recipe = File.ReadAllText(sidecar);
        string revision = GetArtworkRevision(File.ReadAllBytes(imagePath), recipe);
        string revisionFile = imagePath + ".layers.hash";
        if (File.Exists(revisionFile))
        {
            ContentEditorPaths.VerifyWritePath(project, revisionFile);
            return File.ReadAllText(revisionFile) == revision ? recipe : null;
        }
        return Path.GetFileNameWithoutExtension(imagePath)
            .EndsWith("-" + revision, StringComparison.Ordinal)
            ? recipe
            : null;
    }

    /// <summary>
    /// Identifies the exact generated image and its optional editor recipe.
    /// </summary>
    /// <param name="png">The saved PNG data.</param>
    /// <param name="recipe">The optional layers associated with the PNG.</param>
    /// <returns>The revision suffix used in the generated file name.</returns>
    internal static string GetArtworkRevision(byte[] png, string recipe)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(png);
        if (recipe != null)
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(recipe));
        return BitConverter
            .ToString(hash.GetHashAndReset(), 0, 12)
            .Replace("-", "")
            .ToLowerInvariant();
    }

    /// <summary>
    /// Removes the conventional image suffix from a generated output member name.
    /// </summary>
    /// <param name="member">The serialized image member.</param>
    /// <returns>The semantic portion used by the generated filename.</returns>
    internal static string GetGeneratedMemberName(string member)
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
    internal static string GetGeneratedPathSegment(string value)
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
    internal static void WriteBytesAtomically(string target, byte[] bytes) =>
        WriteAtomically(target, temporary => File.WriteAllBytes(temporary, bytes));

    /// <summary>
    /// Publishes a complete file copy without allocating a buffer as large as the source file.
    /// </summary>
    /// <param name="target">The verified destination file.</param>
    /// <param name="source">The complete source file.</param>
    internal static void CopyFileAtomically(string target, string source) =>
        WriteAtomically(target, temporary => File.Copy(source, temporary));

    /// <summary>
    /// Replaces a destination only after its temporary file has been written successfully.
    /// </summary>
    /// <param name="target">The verified destination file.</param>
    /// <param name="write">The operation that writes a complete temporary file.</param>
    private static void WriteAtomically(string target, Action<string> write)
    {
        string temporary = Path.Combine(
            Path.GetDirectoryName(target),
            "." + Guid.NewGuid().ToString("N") + ".tmp"
        );
        try
        {
            write(temporary);
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
        project.CopyAsset(target, source);
        return directoryAddress.TrimEnd('/') + "/" + Path.GetFileName(target);
    }

    /// <summary>
    /// Saves every changed document and refreshes the in-memory pack definition.
    /// </summary>
    /// <param name="project">The working documents to persist.</param>
    public static void Save(ContentPackEditorProject project)
    {
        ContentEditorPaths.VerifyWritableLocation(project.ContentRootPath, project.PackRootPath);
        project.RestoreDocumentSave();
        project.VerifyAssets();
        foreach (ContentPackEditorDocument document in project.AllDocuments)
        {
            document.Validate();
            VerifyUnchangedTarget(document);
        }
        project.ArtworkConversionReport = ContentEditorArtworkConversion.Convert(project);
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
        ContentPackEditorSaveRecovery recovery = project.BeginDocumentSave(dirtyDocuments);
        IReadOnlyList<ContentPackEditorDocument> refreshed = null;
        try
        {
            foreach (ContentPackEditorDocument document in dirtyDocuments)
                recovery.Write(document);
            if (baseChanged)
                refreshed = CreateDocuments(nextResolver, nextDefinition);
        }
        catch (Exception saveFailure)
        {
            try
            {
                project.RestoreDocumentSave();
            }
            catch (IOException restoreFailure)
            {
                throw new IOException(
                    restoreFailure.Message,
                    new AggregateException(saveFailure, restoreFailure)
                );
            }
            throw;
        }
        foreach (ContentPackEditorDocument document in dirtyDocuments)
            document.AcceptSave();
        project.AcceptSave(nextDefinition, refreshed);
        project.CompleteDocumentSave();
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
    /// Refuses to overwrite a file that another process changed after this document was opened.
    /// </summary>
    /// <param name="document">The document and its last observed disk state.</param>
    private static void VerifyUnchangedTarget(ContentPackEditorDocument document)
    {
        document.VerifyUnchangedSources();
    }
}
