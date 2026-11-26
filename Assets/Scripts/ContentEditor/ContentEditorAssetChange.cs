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
        before = File.Exists(path) ? Snapshot(File.ReadAllBytes(path)) : null;
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
    /// Refuses to undo over changes made outside this history entry.
    /// </summary>
    /// <param name="forward">Whether redo is requested.</param>
    internal void Verify(bool forward)
    {
        ContentEditorPaths.VerifyWritePath(project, Path);
        string expected = forward ? before : after;
        if (
            expected == null
                ? File.Exists(Path)
                : !File.Exists(Path)
                    || !File.ReadAllBytes(Path).AsSpan().SequenceEqual(File.ReadAllBytes(expected))
        )
            throw new IOException(
                "An artwork file changed outside the editor. Reopen the pack before undoing."
            );
    }

    /// <summary>
    /// Restores one captured revision or removes a file created by the edit.
    /// </summary>
    /// <param name="forward">Whether to restore the resulting revision.</param>
    internal void Restore(bool forward)
    {
        string snapshot = forward ? after : before;
        if (snapshot == null)
            File.Delete(Path);
        else
            ContentPackEditorStorage.WriteBytesAtomically(Path, File.ReadAllBytes(snapshot));
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
