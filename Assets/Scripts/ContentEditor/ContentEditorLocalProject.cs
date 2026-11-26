using System;
using System.IO;
using System.Xml;
using UnityEngine;

/// <summary>
/// Describes one known local working folder without loading its inherited game content.
/// </summary>
internal sealed class ContentEditorLocalProject
{
    internal string Folder { get; }
    internal string Name { get; }
    internal string Version { get; }
    internal string PackageID { get; }
    internal string Error { get; }

    /// <summary>
    /// Captures display metadata for an available or missing project folder.
    /// </summary>
    /// <param name="folder">The local working folder.</param>
    /// <param name="name">The project name.</param>
    /// <param name="version">The authored version.</param>
    /// <param name="packageID">The linked publishing identity, when present.</param>
    /// <param name="error">The folder's availability problem, when present.</param>
    private ContentEditorLocalProject(
        string folder,
        string name,
        string version,
        string packageID,
        string error = null
    )
    {
        Folder = folder;
        Name = name;
        Version = version;
        PackageID = packageID;
        Error = error;
    }

    /// <summary>
    /// Reads only pack metadata and the optional publishing link for a remembered folder.
    /// </summary>
    /// <param name="folder">The remembered working folder.</param>
    /// <returns>The project metadata, retaining unavailable folders with a visible explanation.</returns>
    internal static ContentEditorLocalProject Read(string folder)
    {
        try
        {
            ContentPackDefinition definition =
                ContentDefinitionSerializer.Deserialize<ContentPackDefinition>(
                    Path.Combine(folder, "pack.xml")
                );
            return new ContentEditorLocalProject(
                folder,
                definition.DisplayName,
                definition.Version,
                ReadPackageID(folder)
            );
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or ArgumentException
                        or XmlException
                        or InvalidOperationException
            )
        {
            return new ContentEditorLocalProject(
                folder,
                Path.GetFileName(folder),
                string.Empty,
                null,
                File.Exists(Path.Combine(folder, "pack.xml"))
                    ? "Couldn’t read this project"
                    : "Project folder is unavailable"
            );
        }
    }

    /// <summary>
    /// Uses the open project's live identity so unsaved name and version edits remain visible.
    /// </summary>
    /// <param name="project">The current working project.</param>
    /// <returns>The current project's metadata.</returns>
    internal static ContentEditorLocalProject FromProject(ContentPackEditorProject project) =>
        new(
            project.PackRootPath,
            project.Definition.DisplayName,
            project.Definition.Version,
            ReadPackageID(project.PackRootPath)
        );

    /// <summary>
    /// Reads the local publishing association without treating an invalid draft as a broken project.
    /// </summary>
    /// <param name="folder">The authored project folder.</param>
    /// <returns>The saved team and package identity, or null when no usable association exists.</returns>
    private static string ReadPackageID(string folder)
    {
        string path = Path.Combine(
            folder,
            ContentEditorPublishingStorage.DirectoryName,
            "settings.json"
        );
        if (!File.Exists(path))
            return null;
        try
        {
            ContentEditorPublishingSettings settings =
                JsonUtility.FromJson<ContentEditorPublishingSettings>(File.ReadAllText(path));
            if (
                settings == null
                || string.IsNullOrWhiteSpace(settings.TeamName)
                || string.IsNullOrWhiteSpace(settings.PackageName)
            )
                return null;
            ModPackageStore.ValidateSegment(settings.TeamName, "publishing team");
            ModPackageStore.ValidateSegment(settings.PackageName, "package name");
            return settings.TeamName + "-" + settings.PackageName;
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
