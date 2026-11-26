using System;
using System.IO;
using System.Security.Cryptography;

/// <summary>
/// Stores immutable file revisions so artwork replacement participates in document undo.
/// </summary>
internal sealed class ContentEditorAssetChange
{
    internal const string EditorDirectory = ".rebellion2-editor";
    private readonly ContentPackEditorProject project;
    private readonly string snapshotDirectory;
    private readonly string before;
    private string after;
    internal string Path { get; }
    internal bool HasChanges => before != after;
    internal string Before => before;
    internal string After => after;

    /// <summary>
    /// Captures the file before a pack-owned replacement begins.
    /// </summary>
    /// <param name="project">The writable project owning the file.</param>
    /// <param name="path">The verified destination.</param>
    /// <param name="snapshotDirectory">The temporary revision directory owned by this project's history.</param>
    internal ContentEditorAssetChange(
        ContentPackEditorProject project,
        string path,
        string snapshotDirectory
    )
    {
        this.project = project;
        this.snapshotDirectory = snapshotDirectory;
        Path = path;
        before = File.Exists(path) ? SnapshotFile(path) : null;
        after = before;
    }

    /// <summary>
    /// Writes a complete revision while retaining the first preceding version.
    /// </summary>
    /// <param name="bytes">The replacement bytes.</param>
    internal void Write(byte[] bytes)
    {
        string revision = Snapshot(bytes);
        ContentPackEditorStorage.WriteBytesAtomically(Path, bytes);
        after = revision;
    }

    /// <summary>
    /// Copies a file through an immutable revision without buffering the full media payload.
    /// </summary>
    /// <param name="source">The selected source file.</param>
    internal void CopyFrom(string source)
    {
        string revision = SnapshotFile(source);
        ContentPackEditorStorage.CopyFileAtomically(Path, revision);
        after = revision;
    }

    /// <summary>
    /// Refuses to undo over changes made outside this history entry.
    /// </summary>
    /// <param name="forward">Whether redo is requested.</param>
    internal void Verify(bool forward) => VerifyRevision(forward ? before : after);

    /// <summary>
    /// Refuses to overwrite a file that no longer matches the project's observed revision.
    /// </summary>
    /// <param name="expected">The expected snapshot, or null for an absent file.</param>
    internal void VerifyRevision(string expected)
    {
        ContentEditorPaths.VerifyWritePath(project, Path);
        if (
            expected == null ? File.Exists(Path) : !File.Exists(Path) || !FilesMatch(Path, expected)
        )
            throw new IOException(
                "Artwork changed outside the editor: "
                    + System.IO.Path.GetRelativePath(project.PackRootPath, Path)
                    + ". Resolve the file conflict before continuing."
            );
    }

    /// <summary>
    /// Restores one captured revision or removes a file created by the edit.
    /// </summary>
    /// <param name="forward">Whether to restore the resulting revision.</param>
    internal void Restore(bool forward) => RestoreRevision(forward ? after : before);

    /// <summary>
    /// Restores an owned saved revision or removes a file created since the last save.
    /// </summary>
    /// <param name="snapshot">The captured revision, or null for an absent file.</param>
    internal void RestoreRevision(string snapshot)
    {
        ContentEditorPaths.VerifyWritePath(project, Path);
        if (snapshot == null)
            File.Delete(Path);
        else
            ContentPackEditorStorage.CopyFileAtomically(Path, snapshot);
    }

    /// <summary>
    /// Compares complete file contents with the same bounded-memory digest used by revision names.
    /// </summary>
    /// <param name="left">The current authored file.</param>
    /// <param name="right">The expected immutable revision.</param>
    /// <returns>Whether both files contain the same bytes.</returns>
    private static bool FilesMatch(string left, string right)
    {
        using SHA256 hash = SHA256.Create();
        using FileStream current = File.OpenRead(left);
        using FileStream expected = File.OpenRead(right);
        return current.Length == expected.Length
            && hash.ComputeHash(current).AsSpan().SequenceEqual(hash.ComputeHash(expected));
    }

    /// <summary>
    /// Captures large media in a private file before deriving its immutable revision name.
    /// </summary>
    /// <param name="source">The complete source file.</param>
    /// <returns>The immutable revision path.</returns>
    private string SnapshotFile(string source)
    {
        ContentEditorPaths.VerifyWritePath(project, snapshotDirectory);
        Directory.CreateDirectory(snapshotDirectory);
        string temporary = System.IO.Path.Combine(
            snapshotDirectory,
            Guid.NewGuid().ToString("N") + ".tmp"
        );
        try
        {
            File.Copy(source, temporary);
            File.SetAttributes(temporary, FileAttributes.Normal);
            using SHA256 hash = SHA256.Create();
            string key;
            using (FileStream input = File.OpenRead(temporary))
                key = BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "");
            string snapshot = System.IO.Path.Combine(snapshotDirectory, key);
            ContentEditorPaths.VerifyWritePath(project, snapshot);
            if (!File.Exists(snapshot))
                File.Move(temporary, snapshot);
            return snapshot;
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    /// <summary>
    /// Keeps one content-addressed revision in the owning history's private directory.
    /// </summary>
    /// <param name="bytes">The complete file contents.</param>
    /// <returns>The immutable revision path.</returns>
    private string Snapshot(byte[] bytes)
    {
        using SHA256 hash = SHA256.Create();
        string key = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
        string snapshot = System.IO.Path.Combine(snapshotDirectory, key);
        ContentEditorPaths.VerifyWritePath(project, snapshot);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(snapshot));
        if (!File.Exists(snapshot))
            ContentPackEditorStorage.WriteBytesAtomically(snapshot, bytes);
        return snapshot;
    }
}
