using UnityEngine;

/// <summary>
/// Controls one independently stoppable sound-effect playback.
/// </summary>
public sealed class AudioPlaybackHandle
{
    private AudioManager _owner;

    internal bool IsActive => _owner != null;

    internal Coroutine LoadCoroutine { get; set; }

    internal bool Paused { get; set; }

    internal AudioSource Source { get; set; }

    /// <summary>
    /// Creates a playback handle owned by an audio manager.
    /// </summary>
    /// <param name="owner">The manager controlling the playback.</param>
    internal AudioPlaybackHandle(AudioManager owner)
    {
        _owner = owner;
    }

    /// <summary>
    /// Stops this playback without affecting any other sound.
    /// </summary>
    public void Stop()
    {
        _owner?.StopPlayback(this);
    }

    /// <summary>
    /// Releases this handle after its playback has stopped.
    /// </summary>
    internal void Release()
    {
        _owner = null;
        LoadCoroutine = null;
        Paused = false;
        Source = null;
    }
}
