using System;
using System.IO;
using System.Xml;

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
                ContentEditorPublishingStorage.ReadPackageID(folder)
            );
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or InvalidDataException
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
            ContentEditorPublishingStorage.ReadPackageID(project.PackRootPath)
        );
}
