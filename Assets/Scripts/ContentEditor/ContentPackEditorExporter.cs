using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

/// <summary>
/// Writes complete project copies and installable archives without changing the working project.
/// </summary>
internal static class ContentPackEditorExporter
{
    /// <summary>
    /// Publishes a complete project copy to a new folder after all files are staged.
    /// </summary>
    /// <param name="project">The project being copied.</param>
    /// <param name="folder">The new output folder.</param>
    /// <param name="packID">The copy's identifier.</param>
    /// <param name="displayName">The copy's display name.</param>
    /// <param name="version">The copy's version.</param>
    internal static void WriteFolder(
        ContentPackEditorProject project,
        string folder,
        string packID,
        string displayName,
        string version
    )
    {
        string destination = Path.GetFullPath(folder);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException("Choose a new folder for the project copy.");
        RejectNestedDestination(project, destination);
        string staging = CreateStagingDirectory(Path.GetDirectoryName(destination));
        try
        {
            WriteSnapshot(project, staging);
            string manifestPath = Path.Combine(staging, "pack.xml");
            XDocument manifest = XDocument.Load(manifestPath);
            manifest.Root.SetElementValue("ID", packID);
            manifest.Root.SetElementValue("DisplayName", displayName);
            manifest.Root.SetElementValue("Version", version);
            manifest.Save(manifestPath);
            Directory.Move(staging, destination);
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
        }
    }

    /// <summary>
    /// Validates a staged snapshot and writes its archive with cancellation between checks and file copies.
    /// </summary>
    /// <param name="project">The working project to snapshot.</param>
    /// <param name="filePath">The archive destination.</param>
    /// <param name="progress">Optional status reporting for the staged validation.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The completion of the staged export.</returns>
    internal static async Task WriteArchiveAsync(
        ContentPackEditorProject project,
        string filePath,
        Action<string> progress,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        string destination = Path.GetFullPath(filePath);
        RejectNestedDestination(project, destination);
        string staging = CreateStagingDirectory(Path.GetTempPath());
        string temporaryFile = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await WriteSnapshotAsync(project, staging, cancellationToken);
            await ContentPackEditorProject
                .OpenFolder(project.ContentRootPath, staging)
                .ValidateAsync(progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            await WriteArchiveContentsAsync(staging, temporaryFile, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            CommitArchive(temporaryFile, destination);
        }
        finally
        {
            if (File.Exists(temporaryFile))
                File.Delete(temporaryFile);
            Directory.Delete(staging, true);
        }
    }

    /// <summary>
    /// Copies staged files into an archive without blocking the UI for whole media files.
    /// </summary>
    /// <param name="staging">The validated staging directory.</param>
    /// <param name="filePath">The temporary archive path.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The completion of archive creation.</returns>
    private static async Task WriteArchiveContentsAsync(
        string staging,
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
            string source in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories)
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
        string source = project.PackRootPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );
        if (
            destination.Equals(source, StringComparison.OrdinalIgnoreCase)
            || destination.StartsWith(
                source + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            )
        )
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
    /// Copies local media and writes current document states into an owned staging folder.
    /// </summary>
    /// <param name="project">The working project.</param>
    /// <param name="staging">The owned staging folder.</param>
    private static void WriteSnapshot(ContentPackEditorProject project, string staging)
    {
        var removed = project.RemovedDocumentPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (ContentPackEditorDocument document in project.Documents)
            document.Validate();
        foreach (
            string source in Directory.EnumerateFiles(
                project.PackRootPath,
                "*",
                SearchOption.AllDirectories
            )
        )
        {
            if (
                source.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                || source.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                || removed.Contains(source)
            )
                continue;
            string target = Path.Combine(
                staging,
                Path.GetRelativePath(project.PackRootPath, source)
            );
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source, target);
        }
        foreach (
            ContentPackEditorDocument document in project.Documents.Where(document =>
                !document.IsInherited || document.IsDirty
            )
        )
        {
            string target = Path.Combine(staging, document.Address[5..]);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, document.Text);
        }
    }

    /// <summary>
    /// Freezes document contents before asynchronously copying local media into an export snapshot.
    /// </summary>
    /// <param name="project">The source authoring project.</param>
    /// <param name="staging">The owned staging directory.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The completion of the snapshot copy.</returns>
    internal static async Task WriteSnapshotAsync(
        ContentPackEditorProject project,
        string staging,
        CancellationToken cancellationToken
    )
    {
        foreach (ContentPackEditorDocument document in project.Documents)
            document.Validate();
        var documents = project
            .Documents.Where(document => !document.IsInherited || document.IsDirty)
            .Select(document => (Path: document.Address[5..], document.Text))
            .ToArray();
        string root = project.PackRootPath;
        var removed = project.RemovedDocumentPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] sources = Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(source =>
                !source.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                && !source.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
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
            string target = Path.Combine(staging, document.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target, document.Text);
            await Task.Yield();
        }
    }
}
