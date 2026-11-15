using UnityEngine;

/// <summary>
/// Retains the active music position while playback is interrupted by application or audio-device
/// lifecycle changes.
/// </summary>
internal sealed class MusicPlaybackInterruption
{
    private AudioClip clip;
    private int timeSamples;

    /// <summary>
    /// Gets whether an interrupted music position is waiting to be restored.
    /// </summary>
    internal bool IsSuspended => clip != null;

    /// <summary>
    /// Returns whether playlist sequencing should continue waiting for the assigned clip.
    /// </summary>
    /// <param name="sourceIsPlaying">Whether Unity reports that the source is currently playing.</param>
    /// <returns>True while the source is playing or an interruption is retaining its position.</returns>
    internal bool IsPlaybackPending(bool sourceIsPlaying)
    {
        return sourceIsPlaying || IsSuspended;
    }

    /// <summary>
    /// Captures the clip and sample that should resume after an interruption.
    /// </summary>
    /// <param name="activeClip">The clip that was active when playback was interrupted.</param>
    /// <param name="activeTimeSamples">The clip's current sample position.</param>
    internal void Capture(AudioClip activeClip, int activeTimeSamples)
    {
        clip = activeClip;
        timeSamples = Mathf.Max(0, activeTimeSamples);
    }

    /// <summary>
    /// Consumes the retained position when it still belongs to the assigned music clip.
    /// </summary>
    /// <param name="activeClip">The clip currently assigned to the music source.</param>
    /// <param name="resumeTimeSamples">The bounded sample position to restore.</param>
    /// <returns>True when the assigned clip has a retained position.</returns>
    internal bool TryConsume(AudioClip activeClip, out int resumeTimeSamples)
    {
        if (clip == null || clip != activeClip)
        {
            Clear();
            resumeTimeSamples = 0;
            return false;
        }

        resumeTimeSamples = Mathf.Clamp(timeSamples, 0, Mathf.Max(0, clip.samples - 1));
        Clear();
        return true;
    }

    /// <summary>
    /// Discards any retained playback position.
    /// </summary>
    internal void Clear()
    {
        clip = null;
        timeSamples = 0;
    }
}
