using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Contains the immutable idle presentation and timing configuration for the strategy advisors.
/// </summary>
public sealed class StrategyAdvisorViewData
{
    /// <summary>
    /// Creates an immutable advisor presentation snapshot.
    /// </summary>
    /// <param name="visible">Whether advisor presentation and input are available.</param>
    /// <param name="protocolIdleTexture">The protocol advisor idle frame.</param>
    /// <param name="droidIdleTexture">The droid advisor idle frame.</param>
    /// <param name="protocolBounds">The protocol advisor source-space bounds.</param>
    /// <param name="droidBounds">The droid advisor source-space bounds.</param>
    /// <param name="frameIntervalSeconds">The unscaled interval between animation frames.</param>
    public StrategyAdvisorViewData(
        bool visible,
        Texture2D protocolIdleTexture,
        Texture2D droidIdleTexture,
        RectInt? protocolBounds,
        RectInt? droidBounds,
        float frameIntervalSeconds
    )
    {
        Visible = visible;
        ProtocolIdleTexture = protocolIdleTexture;
        DroidIdleTexture = droidIdleTexture;
        ProtocolBounds = protocolBounds;
        DroidBounds = droidBounds;
        FrameIntervalSeconds = frameIntervalSeconds;
    }

    /// <summary>
    /// Gets a value indicating whether the advisor is visible.
    /// </summary>
    public bool Visible { get; }

    /// <summary>
    /// Gets the protocol advisor idle texture.
    /// </summary>
    public Texture2D ProtocolIdleTexture { get; }

    /// <summary>
    /// Gets the droid advisor idle texture.
    /// </summary>
    public Texture2D DroidIdleTexture { get; }

    /// <summary>
    /// Gets the protocol advisor source-space bounds.
    /// </summary>
    public RectInt? ProtocolBounds { get; }

    /// <summary>
    /// Gets the droid advisor source-space bounds.
    /// </summary>
    public RectInt? DroidBounds { get; }

    /// <summary>
    /// Gets the unscaled interval between animation frames.
    /// </summary>
    public float FrameIntervalSeconds { get; }
}

/// <summary>
/// Defines one immutable advisor animation queued for local playback.
/// </summary>
public sealed class StrategyAdvisorAnimationViewData
{
    private readonly IReadOnlyList<Texture2D> frames;

    /// <summary>
    /// Creates immutable advisor animation presentation data.
    /// </summary>
    /// <param name="frames">The animation frames in playback order.</param>
    /// <param name="usesDroid">Whether the droid image presents the animation.</param>
    /// <param name="audioPath">The audio cue requested when playback starts.</param>
    public StrategyAdvisorAnimationViewData(
        IReadOnlyList<Texture2D> frames,
        bool usesDroid,
        string audioPath
    )
    {
        this.frames = Copy(frames);
        UsesDroid = usesDroid;
        AudioPath = audioPath;
    }

    /// <summary>
    /// Gets the frames.
    /// </summary>
    public IReadOnlyList<Texture2D> Frames => frames;

    /// <summary>
    /// Gets a value indicating whether the droid presentation is used.
    /// </summary>
    public bool UsesDroid { get; }

    /// <summary>
    /// Gets the audio path.
    /// </summary>
    public string AudioPath { get; }

    /// <summary>
    /// Copies animation frames into an isolated read-only snapshot.
    /// </summary>
    /// <param name="source">The source animation frames.</param>
    /// <returns>The isolated read-only frame list.</returns>
    private static IReadOnlyList<Texture2D> Copy(IReadOnlyList<Texture2D> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<Texture2D>();

        Texture2D[] copy = new Texture2D[source.Count];
        for (int i = 0; i < source.Count; i++)
            copy[i] = source[i];

        return Array.AsReadOnly(copy);
    }
}
