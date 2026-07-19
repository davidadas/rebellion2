using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Identifies the game action represented by a strategy confirmation dialog.
/// </summary>
public enum ConfirmDialogKind
{
    Move,
    Scrap,
    Retire,
    StopConstruction,
}

/// <summary>
/// Contains an immutable strategy confirmation-dialog presentation snapshot.
/// </summary>
public sealed class ConfirmDialogWindowRenderData
{
    /// <summary>
    /// Creates a complete confirmation-dialog presentation snapshot.
    /// </summary>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="backgroundTexture">The window background texture.</param>
    /// <param name="titleTexture">The action title texture.</param>
    /// <param name="lines">The ordered dialog text lines.</param>
    public ConfirmDialogWindowRenderData(
        int x,
        int y,
        Texture2D backgroundTexture,
        Texture2D titleTexture,
        IReadOnlyList<string> lines
    )
    {
        X = x;
        Y = y;
        BackgroundTexture = backgroundTexture;
        TitleTexture = titleTexture;
        Lines = new List<string>(
            lines ?? throw new ArgumentNullException(nameof(lines))
        ).AsReadOnly();
    }

    public int X { get; }

    public int Y { get; }

    public Texture2D BackgroundTexture { get; }

    public Texture2D TitleTexture { get; }

    public IReadOnlyList<string> Lines { get; }
}
