using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

/// <summary>
/// Persists authored pack documents and imported files without owning editing or presentation state.
/// </summary>
public static class ContentPackEditorStorage
{
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
        ContentPackEditorProject.VerifyWorkingFolder(contentRootPath, packRootPath);
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

        ContentPackEditorProject.VerifyWorkingFolder(contentRootPath, packRootPath);
        string packRoot = Path.GetFullPath(packRootPath);
        bool directoryExisted = Directory.Exists(packRoot);
        if (
            File.Exists(packRoot)
            || directoryExisted && Directory.EnumerateFileSystemEntries(packRoot).Any()
        )
            throw new IOException("Choose a new or empty content-pack folder.");
        foreach (string inheritedRoot in baseResolver.PackRootPaths)
        {
            if (
                packRoot.StartsWith(
                    inheritedRoot.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar
                    ) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase
                )
            )
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
        IEnumerable<string> addresses = resolver
            .EnumerateFileAddresses("Pack/")
            .Where(address => address.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Append(_packDefinitionAddress)
            .Distinct(StringComparer.Ordinal)
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
                return new ContentPackEditorDocument(
                    address,
                    sourcePath,
                    targetPath,
                    File.ReadAllText(sourcePath)
                );
            })
            .ToList();
        if (
            string.IsNullOrWhiteSpace(definition.GameConfigPath)
            && !result.Any(document => document.Address == "Pack/Shared/Data/game-config.xml")
        )
        {
            string defaultRules = resolver.ResolveFile("Application/Rules/game.xml");
            if (defaultRules != null)
                result.Add(
                    new ContentPackEditorDocument(
                        "Pack/Shared/Data/game-config.xml",
                        defaultRules,
                        Path.Combine(resolver.PackRootPath, "Shared", "Data", "game-config.xml"),
                        File.ReadAllText(defaultRules)
                    )
                );
        }
        return result;
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
        string address = ImportAssetToFolder(project, sourcePath, root ?? "Pack/Assets");
        return root == null ? address : address[(root.TrimEnd('/').Length + 1)..];
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
        ContentPackEditorProject.VerifyWorkingFolder(project.ContentRootPath, project.PackRootPath);
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
        File.Copy(source, target);
        return directoryAddress.TrimEnd('/') + "/" + Path.GetFileName(target);
    }

    /// <summary>
    /// Saves every changed document and refreshes the in-memory pack definition.
    /// </summary>
    /// <param name="project">The working documents to persist.</param>
    public static void Save(ContentPackEditorProject project)
    {
        ContentPackEditorProject.VerifyWorkingFolder(project.ContentRootPath, project.PackRootPath);
        ContentPackEditorDocument[] dirtyDocuments = project
            .AllDocuments.Where(document => document.IsDirty)
            .ToArray();
        foreach (ContentPackEditorDocument document in dirtyDocuments)
        {
            document.Validate();
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
            SaveDocument(document);

        project.AcceptSave(
            nextDefinition,
            baseChanged ? CreateDocuments(nextResolver, nextDefinition) : null
        );
    }

    /// <summary>
    /// Writes the current working project to a new folder with a separate pack identity.
    /// </summary>
    /// <param name="project">The source project to copy.</param>
    /// <param name="folder">The new destination folder.</param>
    /// <param name="packID">The copy's identifier.</param>
    /// <param name="displayName">The copy's display name.</param>
    /// <param name="version">The copy's version.</param>
    /// <returns>The independently opened project copy.</returns>
    public static ContentPackEditorProject SaveAs(
        ContentPackEditorProject project,
        string folder,
        string packID,
        string displayName,
        string version
    )
    {
        ValidateIdentity(packID, displayName, version);
        ContentPackEditorProject.VerifyWorkingFolder(project.ContentRootPath, folder);
        ContentPackEditorExporter.WriteFolder(project, folder, packID, displayName, version);
        return OpenFolder(project.ContentRootPath, folder);
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
    /// <param name="document">The validated working document.</param>
    private static void SaveDocument(ContentPackEditorDocument document)
    {
        document.Validate();
        VerifyUnchangedTarget(document);

        if (document.IsRemoved)
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

        string temporaryPath = document.TargetPath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, document.Text);
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
        bool exists = File.Exists(document.TargetPath);
        bool changed;
        if (document.SavedText == null)
            changed = exists;
        else if (exists)
            changed =
                document.IsInherited
                || !string.Equals(
                    File.ReadAllText(document.TargetPath),
                    document.SavedText,
                    StringComparison.Ordinal
                );
        else
            changed = !document.IsInherited;
        if (!changed && document.IsInherited)
            changed =
                !File.Exists(document.SourcePath)
                || !string.Equals(
                    File.ReadAllText(document.SourcePath),
                    document.SavedText,
                    StringComparison.Ordinal
                );
        if (changed)
            throw new IOException(
                "Content changed outside the editor. Reopen the pack before saving: "
                    + document.TargetPath
            );
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
