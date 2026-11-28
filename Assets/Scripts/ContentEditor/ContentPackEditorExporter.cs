using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

/// <summary>
/// Writes frozen project snapshots and installable archives without changing the working project.
/// </summary>
internal static class ContentPackEditorExporter
{
    /// <summary>
    /// Stages a derived game pack as a separate stackable mod and publishes the validated copy.
    /// </summary>
    /// <param name="project">The source project, including its current draft.</param>
    /// <param name="destination">The new mod's folder.</param>
    /// <param name="progress">Optional status reporting.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>Non-blocking validation warnings for the converted project.</returns>
    internal static async Task<IReadOnlyList<string>> ConvertToModAsync(
        ContentPackEditorProject project,
        string destination,
        Action<string> progress = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (
            project.Mod != null
            || project.IsInstalled
            || string.IsNullOrWhiteSpace(project.Definition.BasePackID)
        )
            throw new InvalidOperationException(
                "Open an editable game pack with a base pack to convert it."
            );
        destination = Path.GetFullPath(destination);
        RejectNestedDestination(project, destination);
        if (File.Exists(destination) || Directory.Exists(destination))
            throw new IOException(
                "Choose a location where the converted mod folder does not exist."
            );

        string staging = CreateStagingDirectory(Path.GetDirectoryName(destination));
        try
        {
            string modRoot = Path.Combine(staging, "project");
            string packSnapshot = Path.Combine(staging, "pack");
            Directory.CreateDirectory(packSnapshot);
            progress?.Invoke("Copying project files");
            await WriteSnapshotAsync(
                project,
                packSnapshot,
                cancellationToken,
                EnumerateConversionFiles(project.ProjectRootPath)
            );
            string campaignPath = Path.Combine(packSnapshot, ContentPackageManifest.FileName);
            ContentPackDefinition campaign =
                ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(campaignPath);
            ContentFileResolver original = project.CreateResolver(campaign);
            ContentFileResolver baseline = ContentPackLoader.CreatePackResolver(
                project.ContentRootPath,
                campaign.BasePackID
            );
            if (!original.PackRootPaths.Skip(1).SequenceEqual(baseline.PackRootPaths))
                throw new InvalidDataException(
                    "Install the game pack releases this project inherits before converting it."
                );

            using (
                ContentPackEditorStorage.CreateInFolder(
                    project.ContentRootPath,
                    campaign.BasePackID,
                    modRoot,
                    campaign.ID + "-mod",
                    campaign.DisplayName,
                    campaign.Version,
                    asMod: true
                )
            ) { }

            WriteConvertedCampaign(campaignPath, ContentPackLoader.ReadModPackManifest(baseline));
            string content = Path.Combine(modRoot, "Content");
            Directory.CreateDirectory(content);
            Directory.Move(packSnapshot, Path.Combine(content, "Pack"));
            using ContentPackEditorProject converted = ContentPackEditorStorage.OpenFolder(
                project.ContentRootPath,
                modRoot
            );
            IReadOnlyList<string> warnings = await ContentEditorPackValidator.ValidateAsync(
                converted,
                progress,
                cancellationToken
            );
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(modRoot, destination);
            return warnings;
        }
        finally
        {
            Directory.Delete(staging, true);
        }
    }

    /// <summary>
    /// Selects local files without importing source control metadata or following directory links.
    /// </summary>
    /// <param name="directory">The current project directory.</param>
    /// <returns>The files in the authored project tree.</returns>
    private static IEnumerable<string> EnumerateConversionFiles(string directory)
    {
        foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
        {
            if (string.Equals(Path.GetFileName(entry), ".git", StringComparison.OrdinalIgnoreCase))
                continue;
            FileAttributes attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException(
                    "Copy linked project files into the project before converting it."
                );
            if ((attributes & FileAttributes.Directory) != 0)
            {
                foreach (string path in EnumerateConversionFiles(entry))
                    yield return path;
            }
            else
                yield return entry;
        }
    }

    /// <summary>
    /// Writes campaign settings as an overlay while keeping the selected base pack's identity.
    /// </summary>
    /// <param name="path">The staged copy of the original campaign manifest.</param>
    /// <param name="baseManifest">The resolved base campaign manifest.</param>
    private static void WriteConvertedCampaign(string path, string baseManifest)
    {
        XElement baseline = XElement.Parse(baseManifest);
        XElement authored = XElement.Load(path);
        authored.Name = baseline.Name;
        authored.Attribute("Type")?.Remove();
        foreach (
            XName name in new XName[]
            {
                "ID",
                "Version",
                "BasePackID",
                "DisplayName",
                "InheritedPacks",
            }
        )
        {
            authored.Elements(name).Remove();
            if (baseline.Element(name) is XElement value)
                authored.Add(new XElement(value));
        }

        string overlay = ContentXmlOverrides.Update(baseManifest, authored.ToString(), null);
        ContentPackLoader.ValidateModPackOverlay(overlay);
        if (ContentXmlOverrides.IsEmpty(overlay))
            File.Delete(path);
        else
            File.WriteAllText(path, overlay);
    }

    /// <summary>
    /// Validates a staged snapshot and writes its archive with cancellation between checks and file copies.
    /// </summary>
    /// <param name="project">The working project to snapshot.</param>
    /// <param name="filePath">The archive destination.</param>
    /// <param name="progress">Optional status reporting for the staged validation.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The staged validation's non-blocking authoring warnings.</returns>
    internal static async Task<IReadOnlyList<string>> WriteArchiveAsync(
        ContentPackEditorProject project,
        string filePath,
        Action<string> progress = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        string destination = Path.GetFullPath(filePath);
        RejectNestedDestination(project, destination);
        string staging = CreateStagingDirectory(Path.GetTempPath());
        string temporaryFile = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            IReadOnlyList<string> warnings = await WriteValidatedSnapshotAsync(
                project,
                staging,
                progress,
                cancellationToken
            );
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            await WriteArchiveContentsAsync(
                staging,
                GetSnapshotPackRoot(project, staging),
                temporaryFile,
                cancellationToken
            );
            cancellationToken.ThrowIfCancellationRequested();
            CommitArchive(temporaryFile, destination);
            return warnings;
        }
        finally
        {
            if (File.Exists(temporaryFile))
                File.Delete(temporaryFile);
            Directory.Delete(staging, true);
        }
    }

    /// <summary>
    /// Freezes and validates a project for archive formats that share the same content checks.
    /// </summary>
    /// <param name="project">The editable source project.</param>
    /// <param name="staging">The owned, empty staging directory.</param>
    /// <param name="progress">The validation progress callback.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The non-blocking authoring warnings.</returns>
    internal static async Task<IReadOnlyList<string>> WriteValidatedSnapshotAsync(
        ContentPackEditorProject project,
        string staging,
        Action<string> progress,
        CancellationToken cancellationToken
    )
    {
        await WriteSnapshotAsync(project, staging, cancellationToken);
        using ContentPackEditorProject snapshot = ContentPackEditorStorage.OpenFolder(
            project.ContentRootPath,
            staging
        );
        ContentPackEditorStorage.Save(snapshot);
        if (snapshot.Mod == null && !string.IsNullOrWhiteSpace(snapshot.Definition.BasePackID))
            PinInheritedPacks(snapshot);
        IReadOnlyList<string> warnings = await ContentEditorPackValidator.ValidateAsync(
            snapshot,
            progress,
            cancellationToken
        );
        return string.IsNullOrEmpty(snapshot.ArtworkConversionReport)
            ? warnings
            : warnings.Append(snapshot.ArtworkConversionReport).ToArray();
    }

    /// <summary>
    /// Records the exact parent releases used by the archive before validating its contents.
    /// </summary>
    /// <param name="snapshot">The staged derived pack whose manifest will be published.</param>
    private static void PinInheritedPacks(ContentPackEditorProject snapshot)
    {
        snapshot.Definition.InheritedPacks = snapshot
            .CreateResolver()
            .PackRootPaths.Skip(1)
            .Select(root =>
            {
                var parent = ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(
                    Path.Combine(root, "pack.xml")
                );
                return new ContentPackDependency { ID = parent.ID, Version = parent.Version };
            })
            .ToList();
        ContentDefinitionSerializer.Serialize(
            Path.Combine(snapshot.ProjectRootPath, "pack.xml"),
            snapshot.Definition
        );
    }

    /// <summary>
    /// Copies staged files into an archive without blocking the UI for whole media files.
    /// </summary>
    /// <param name="staging">The validated staging directory.</param>
    /// <param name="packRoot">The staged pack layer that may contain editor history.</param>
    /// <param name="filePath">The temporary archive path.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The completion of archive creation.</returns>
    private static async Task WriteArchiveContentsAsync(
        string staging,
        string packRoot,
        string filePath,
        CancellationToken cancellationToken
    )
    {
        using FileStream output = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            true
        );
        using ZipArchive archive = new ZipArchive(output, ZipArchiveMode.Create);
        foreach (
            string source in Directory
                .EnumerateFiles(staging, "*", SearchOption.AllDirectories)
                .Where(source => !IsEditorFile(packRoot, source))
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            ZipArchiveEntry entry = archive.CreateEntry(
                Path.GetRelativePath(staging, source).Replace('\\', '/'),
                CompressionLevel.Fastest
            );
            using FileStream input = new FileStream(
                source,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                true
            );
            using Stream compressed = entry.Open();
            await input.CopyToAsync(compressed, 81920, cancellationToken);
            await Task.Yield();
        }
    }

    /// <summary>
    /// Identifies internal artwork history relative to the writable pack layer.
    /// </summary>
    /// <param name="packRoot">The writable pack layer inside the project or staged archive.</param>
    /// <param name="path">The path being considered for publication.</param>
    /// <returns>True for an editor history or backup file.</returns>
    internal static bool IsEditorFile(string packRoot, string path) =>
        Path.GetRelativePath(packRoot, path)
            .Replace('\\', '/')
            .StartsWith(ContentEditorAssetChange.EditorDirectory + "/", StringComparison.Ordinal);

    /// <summary>
    /// Locates the writable content layer inside the staged project.
    /// </summary>
    /// <param name="project">The source project that determines the package format.</param>
    /// <param name="staging">The staging directory containing its manifest.</param>
    /// <returns>The staging directory's pack-layer path.</returns>
    internal static string GetSnapshotPackRoot(ContentPackEditorProject project, string staging) =>
        project.Mod == null ? staging : Path.Combine(staging, "Content", "Pack");

    /// <summary>
    /// Replaces an archive only after all validation and compression have completed.
    /// </summary>
    /// <param name="temporaryFile">The completed temporary archive.</param>
    /// <param name="destination">The requested archive path.</param>
    private static void CommitArchive(string temporaryFile, string destination)
    {
        if (File.Exists(destination))
            File.Replace(temporaryFile, destination, null);
        else
            File.Move(temporaryFile, destination);
    }

    /// <summary>
    /// Rejects outputs inside the project so copies cannot recursively include themselves.
    /// </summary>
    /// <param name="project">The source project.</param>
    /// <param name="destination">The normalized output path.</param>
    private static void RejectNestedDestination(
        ContentPackEditorProject project,
        string destination
    )
    {
        ContentEditorPaths.VerifyWritableLocation(project.ContentRootPath, destination);
        if (ContentEditorPaths.Contains(project.ProjectRootPath, destination))
            throw new IOException("Choose an output outside the working content-pack folder.");
    }

    /// <summary>
    /// Creates a uniquely owned temporary folder for an output operation.
    /// </summary>
    /// <param name="parent">The staging parent directory.</param>
    /// <returns>The created staging directory.</returns>
    private static string CreateStagingDirectory(string parent)
    {
        string path = Path.Combine(parent, ".rebellion2-editor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Freezes document contents before asynchronously copying local media into an export snapshot.
    /// </summary>
    /// <param name="project">The source authoring project.</param>
    /// <param name="staging">The owned staging directory.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <param name="localFiles">An optional prefiltered set of authored files.</param>
    /// <returns>The completion of the snapshot copy.</returns>
    internal static async Task WriteSnapshotAsync(
        ContentPackEditorProject project,
        string staging,
        CancellationToken cancellationToken,
        IEnumerable<string> localFiles = null
    )
    {
        foreach (ContentPackEditorDocument document in project.Documents)
        {
            document.Validate();
            if (project.Mod != null && document.Address == "Pack/pack.xml" && !document.InheritsAll)
            {
                if (!project.Mod.ExtendsPack)
                    throw new InvalidDataException(
                        "Set ExtendsPack in the package manifest to edit campaign settings."
                    );
                ContentPackLoader.ValidateModPackOverlay(document.AuthoredText);
            }
        }
        var documents = project
            .Documents.Where(document =>
                (!document.IsInherited || document.IsDirty) && !document.InheritsAll
            )
            .Select(document => (Path: document.Address[5..], Text: document.AuthoredText))
            .ToArray();
        string root = project.ProjectRootPath;
        string packStaging = GetSnapshotPackRoot(project, staging);
        var removed = project
            .RemovedDocumentPaths.Concat(
                project
                    .Documents.Where(document => document.InheritsAll)
                    .Select(document => document.TargetPath)
            )
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] sources = (
            localFiles ?? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        )
            .Where(source =>
                !source.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                && !source.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                && !Path.GetRelativePath(root, source)
                    .Split(Path.DirectorySeparatorChar)[0]
                    .Equals(
                        ContentEditorPublishingStorage.DirectoryName,
                        StringComparison.OrdinalIgnoreCase
                    )
                && !IsEditorFile(project.PackRootPath, source)
                && !removed.Contains(source)
            )
            .ToArray();
        foreach (string source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string target = Path.Combine(staging, Path.GetRelativePath(root, source));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            using FileStream input = new FileStream(
                source,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                true
            );
            using FileStream output = new FileStream(
                target,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                true
            );
            await input.CopyToAsync(output, 81920, cancellationToken);
            await Task.Yield();
        }
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string target = Path.Combine(packStaging, document.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, document.Text);
            await Task.Yield();
        }
    }
}
