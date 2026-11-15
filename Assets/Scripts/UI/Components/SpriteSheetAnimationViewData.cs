using System;
using UnityEngine;

/// <summary>
/// Supplies one resolved texture and its playback layout to a sprite animation view.
/// </summary>
public sealed class SpriteSheetAnimationViewData
{
    public Texture2D Texture { get; }

    public int FrameCount { get; }

    public int Columns { get; }

    public int Rows => (FrameCount - 1) / Columns + 1;

    public float FramesPerSecond { get; }

    /// <summary>
    /// Captures the texture, grid, and playback rate of a sprite-sheet animation.
    /// </summary>
    /// <param name="texture">The texture containing the animation frames.</param>
    /// <param name="frameCount">The number of playable frames in row order.</param>
    /// <param name="columns">The number of frames in each texture row.</param>
    /// <param name="framesPerSecond">The positive, finite playback rate.</param>
    public SpriteSheetAnimationViewData(
        Texture2D texture,
        int frameCount,
        int columns,
        float framesPerSecond
    )
    {
        Texture = texture ?? throw new ArgumentNullException(nameof(texture));
        if (frameCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameCount));
        if (columns <= 0)
            throw new ArgumentOutOfRangeException(nameof(columns));
        if (
            framesPerSecond <= 0f
            || float.IsNaN(framesPerSecond)
            || float.IsInfinity(framesPerSecond)
        )
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        FrameCount = frameCount;
        Columns = columns;
        FramesPerSecond = framesPerSecond;
    }
}
