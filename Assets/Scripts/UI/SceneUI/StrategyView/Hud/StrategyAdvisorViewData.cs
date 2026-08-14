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

    public bool Visible { get; }

    public Texture2D ProtocolIdleTexture { get; }

    public Texture2D DroidIdleTexture { get; }

    public RectInt? ProtocolBounds { get; }

    public RectInt? DroidBounds { get; }

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
    /// <param name="delayBeforeSeconds">The unscaled delay before playback starts.</param>
    /// <param name="minimumPlaybackSeconds">The minimum time the playback remains active.</param>
    /// <param name="holdFinalFrame">Whether the final frame remains visible after playback completes.</param>
    public StrategyAdvisorAnimationViewData(
        IReadOnlyList<Texture2D> frames,
        bool usesDroid,
        string audioPath,
        float delayBeforeSeconds = 0f,
        float minimumPlaybackSeconds = 0f,
        bool holdFinalFrame = false
    )
    {
        this.frames = Copy(frames);
        UsesDroid = usesDroid;
        AudioPath = audioPath;
        DelayBeforeSeconds = Math.Max(0f, delayBeforeSeconds);
        MinimumPlaybackSeconds = Math.Max(0f, minimumPlaybackSeconds);
        HoldFinalFrame = holdFinalFrame;
    }

    public IReadOnlyList<Texture2D> Frames => frames;

    public bool UsesDroid { get; }

    public string AudioPath { get; }

    public float DelayBeforeSeconds { get; }

    public float MinimumPlaybackSeconds { get; }

    public bool HoldFinalFrame { get; }

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
