using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonUtility = UnityEngine.JsonUtility;

/// <summary>
/// Wraps a validated content-pack snapshot in a distributable Thunderstore package.
/// </summary>
internal static class ThunderstorePackageExporter
{
    /// <summary>
    /// Exports a release while preserving both the working project and any existing output on failure.
    /// </summary>
    /// <param name="project">The project whose current edits should be released.</param>
    /// <param name="settings">The listing and release notes.</param>
    /// <param name="destination">The archive selected by the author.</param>
    /// <param name="progress">The validation progress callback.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>Non-blocking authoring warnings from the validated snapshot.</returns>
    internal static async Task<IReadOnlyList<string>> WriteArchiveAsync(
        ContentPackEditorProject project,
        ContentEditorPublishingSettings settings,
        string destination,
        Action<string> progress = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        ContentEditorPaths.VerifyWritableLocation(project.ContentRootPath, project.PackRootPath);
        settings.Validate(project.Definition);
        byte[] icon = ContentEditorPublishingStorage.ReadIcon(settings.IconPath);
        string manifest = JsonUtility.ToJson(
            new Manifest(settings, project.Definition.Version),
            true
        );
        string readme = settings.Readme;
        string changelog = settings.Changelog;
        string packID = project.Definition.ID;
        destination = Path.GetFullPath(destination);
        ContentEditorPaths.VerifyWritableLocation(project.ContentRootPath, destination);
        if (ContentEditorPaths.Contains(project.PackRootPath, destination))
            throw new IOException("Choose an output outside the working content-pack folder.");
        string staging = Path.Combine(
            Path.GetTempPath(),
            ".rebellion2-release-" + Guid.NewGuid().ToString("N")
        );
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        Directory.CreateDirectory(staging);
        try
        {
            string snapshot = Path.Combine(staging, "Mods", packID);
            Directory.CreateDirectory(snapshot);
            IReadOnlyList<string> warnings =
                await ContentPackEditorExporter.WriteValidatedSnapshotAsync(
                    project,
                    snapshot,
                    progress,
                    cancellationToken
                );
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            using (
                FileStream output = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    true
                )
            )
            using (ZipArchive archive = new ZipArchive(output, ZipArchiveMode.Create))
            {
                await WriteEntryAsync(
                    archive,
                    "manifest.json",
                    Encoding.UTF8.GetBytes(manifest),
                    cancellationToken
                );
                await WriteEntryAsync(archive, "icon.png", icon, cancellationToken);
                await WriteEntryAsync(
                    archive,
                    "README.md",
                    Encoding.UTF8.GetBytes(readme),
                    cancellationToken
                );
                if (!string.IsNullOrWhiteSpace(changelog))
                    await WriteEntryAsync(
                        archive,
                        "CHANGELOG.md",
                        Encoding.UTF8.GetBytes(changelog),
                        cancellationToken
                    );
                foreach (
                    string source in Directory.EnumerateFiles(
                        snapshot,
                        "*",
                        SearchOption.AllDirectories
                    )
                )
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string address = Path.GetRelativePath(staging, source).Replace('\\', '/');
                    ZipArchiveEntry target = archive.CreateEntry(address, CompressionLevel.Fastest);
                    using FileStream input = new FileStream(
                        source,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        81920,
                        true
                    );
                    using Stream compressed = target.Open();
                    await input.CopyToAsync(compressed, 81920, cancellationToken);
                    await Task.Yield();
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(destination))
                File.Replace(temporary, destination, null);
            else
                File.Move(temporary, destination);
            return warnings;
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
            Directory.Delete(staging, true);
        }
    }

    /// <summary>
    /// Writes one frozen publishing document into the archive.
    /// </summary>
    /// <param name="archive">The open release archive.</param>
    /// <param name="name">The root-relative file name.</param>
    /// <param name="bytes">The frozen file contents.</param>
    /// <param name="cancellationToken">The requested cancellation.</param>
    /// <returns>The completion of the entry write.</returns>
    private static async Task WriteEntryAsync(
        ZipArchive archive,
        string name,
        byte[] bytes,
        CancellationToken cancellationToken
    )
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using Stream stream = entry.Open();
        await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
    }

    /// <summary>
    /// Defines the field names required by the Thunderstore package manifest.
    /// </summary>
    [Serializable]
    private sealed class Manifest
    {
        public string name;
        public string version_number;
        public string website_url;
        public string description;
        public string[] dependencies;

        /// <summary>
        /// Freezes listing values using the content pack's canonical release version.
        /// </summary>
        /// <param name="settings">The authored listing.</param>
        /// <param name="version">The pack release version.</param>
        internal Manifest(ContentEditorPublishingSettings settings, string version)
        {
            name = settings.PackageName;
            version_number = version;
            website_url = settings.WebsiteUrl ?? string.Empty;
            description = settings.Description;
            dependencies = settings.GetDependencies();
        }
    }
}
