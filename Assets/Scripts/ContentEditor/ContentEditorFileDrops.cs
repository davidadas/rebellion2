using System;
using System.Collections.Concurrent;
using UnityEngine;

/// <summary>
/// Receives native file drops and delivers them on the editor's Unity update thread.
/// </summary>
internal abstract class ContentEditorFileDrops : IDisposable
{
    private readonly ConcurrentQueue<(string[] Files, Vector2 Position)> drops = new();

    /// <summary>
    /// Creates the file drop receiver for this standalone editor.
    /// </summary>
    /// <param name="platform">The running Unity platform.</param>
    /// <returns>The platform receiver, when available.</returns>
    internal static ContentEditorFileDrops Create(RuntimePlatform platform) =>
        platform switch
        {
            RuntimePlatform.WindowsPlayer => new ContentEditorWindowsFileDrops(),
            RuntimePlatform.OSXPlayer => new ContentEditorMacFileDrops(),
            _ => null,
        };

    /// <summary>
    /// Updates the native registration and reads the next completed drop.
    /// </summary>
    /// <param name="drop">A file list and a normalized position measured from the bottom left.</param>
    /// <returns>Whether a drop was received.</returns>
    internal bool TryRead(out (string[] Files, Vector2 Position) drop)
    {
        Poll();
        return drops.TryDequeue(out drop);
    }

    /// <summary>
    /// Releases the native drop registration.
    /// </summary>
    public abstract void Dispose();

    /// <summary>
    /// Queues a completed native drop before returning control to the window system.
    /// </summary>
    /// <param name="files">The paths reported by the operating system.</param>
    /// <param name="position">The normalized client point measured from the bottom left.</param>
    protected void Enqueue(string[] files, Vector2 position) => drops.Enqueue((files, position));

    /// <summary>
    /// Attaches the receiver or pumps platform events when required.
    /// </summary>
    protected abstract void Poll();
}
