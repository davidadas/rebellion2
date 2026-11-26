using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Rebellion.Util.Logging;

/// <summary>
/// Retains complete document revisions until a save commits or every failed write is restored.
/// </summary>
internal sealed class ContentPackEditorSaveRecovery
{
    private readonly ContentPackEditorProject project;
    private readonly Dictionary<ContentPackEditorDocument, ContentEditorAssetChange> files;
    private readonly List<ContentEditorAssetChange> pending = new();

    public string DirectoryPath { get; }
    public bool HasPendingRestores => pending.Count > 0;

    /// <summary>
    /// Persists every preceding revision and its destination before any document is published.
    /// </summary>
    /// <param name="project">The owner of the writable documents.</param>
    /// <param name="documents">The complete set of documents being saved.</param>
    public ContentPackEditorSaveRecovery(
        ContentPackEditorProject project,
        IReadOnlyList<ContentPackEditorDocument> documents
    )
    {
        this.project = project;
        DirectoryPath = Path.Combine(
            project.PackRootPath,
            ContentEditorAssetChange.EditorDirectory,
            "History",
            Guid.NewGuid().ToString("N")
        );
        ContentEditorPaths.VerifyWritePath(project, DirectoryPath);
        Directory.CreateDirectory(DirectoryPath);
        try
        {
            files = documents.ToDictionary(
                document => document,
                document => new ContentEditorAssetChange(
                    project,
                    document.TargetPath,
                    DirectoryPath
                )
            );
            new XElement(
                "SaveRecovery",
                files.Values.Select(file => new XElement(
                    "File",
                    new XAttribute("Path", Path.GetRelativePath(project.PackRootPath, file.Path)),
                    new XAttribute("WasPresent", file.Before != null),
                    new XAttribute(
                        "Previous",
                        file.Before == null ? "" : Path.GetFileName(file.Before)
                    )
                ))
            ).Save(Path.Combine(DirectoryPath, "recovery.xml"));
        }
        catch
        {
            RemoveRecoveryFiles();
            throw;
        }
    }

    /// <summary>
    /// Publishes one validated document and records the exact revision this save wrote.
    /// </summary>
    /// <param name="document">The prepared document to publish.</param>
    public void Write(ContentPackEditorDocument document)
    {
        document.Validate();
        document.VerifyUnchangedSources();
        ContentEditorAssetChange file = files[document];
        file.Verify(true);
        if (document.IsRemoved || document.InheritsAll)
            file.Delete();
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file.Path));
            file.Write(Encoding.UTF8.GetBytes(document.AuthoredText));
        }
        pending.Add(file);
    }

    /// <summary>
    /// Attempts every outstanding restore without replacing external edits or losing blocked files.
    /// </summary>
    public void Restore()
    {
        List<Exception> failures = new();
        foreach (ContentEditorAssetChange file in pending.AsEnumerable().Reverse().ToArray())
        {
            try
            {
                if (!file.MatchesRevision(file.Before))
                {
                    file.Verify(false);
                    file.Restore(false);
                }
                pending.Remove(file);
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException)
            {
                failures.Add(new IOException("Could not restore " + file.Path, exception));
            }
        }
        if (failures.Count > 0)
            throw new IOException(
                "The save could not be fully restored. Recovery files are retained at "
                    + DirectoryPath
                    + ". Resolve the file conflicts or permissions and retry Save or Discard.",
                new AggregateException(failures)
            );
        RemoveRecoveryFiles();
    }

    /// <summary>
    /// Releases preceding revisions only after every document has been accepted as saved.
    /// </summary>
    public void Commit()
    {
        pending.Clear();
        RemoveRecoveryFiles();
    }

    /// <summary>
    /// Removes this completed transaction's private files without changing the save outcome.
    /// </summary>
    private void RemoveRecoveryFiles()
    {
        try
        {
            ContentEditorPaths.VerifyWritePath(project, DirectoryPath);
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            GameLogger.Warning(
                "Could not remove completed save recovery files at "
                    + DirectoryPath
                    + ": "
                    + exception.Message
            );
        }
    }
}
