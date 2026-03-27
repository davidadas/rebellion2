using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Handles playback of a single cutscene instance.
/// This component is intended to be instantiated and managed by
/// <see cref="CutsceneManager"/>.
/// </summary>
public sealed class CutscenePlayer : MonoBehaviour
{
    /// <summary>
    /// The target screen surface used for rendering the video.
    /// </summary>
    [SerializeField]
    private RawImage Screen;

    /// <summary>
    /// The <see cref="VideoPlayer"/> responsible for video playback.
    /// </summary>
    [SerializeField]
    private VideoPlayer VideoPlayer;

    /// <summary>
    /// Audio source used for cutscene audio output.
    /// </summary>
    [SerializeField]
    private AudioSource AudioSource;

    /// <summary>
    /// Callback invoked when playback completes or is skipped.
    /// </summary>
    public Action OnFinished;

    private bool isEnding;

    /// <summary>
    /// Configures video playback defaults.
    /// </summary>
    private void Awake()
    {
        VideoPlayer.playOnAwake = false;
        VideoPlayer.isLooping = false;
    }

    /// <summary>
    /// Begins playback of the specified <see cref="VideoClip"/>.
    /// </summary>
    /// <param name="clip">The video clip to play.</param>
    public void Play(VideoClip clip)
    {
        isEnding = false;

        VideoPlayer.loopPointReached += HandleFinished;
        VideoPlayer.clip = clip;

        VideoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        VideoPlayer.SetTargetAudioSource(0, AudioSource);

        VideoPlayer.Play();
        AudioSource.Play();
    }

    /// <summary>
    /// Monitors input to allow skipping the cutscene.
    /// </summary>
    private void Update()
    {
        if (isEnding)
            return;

        if (IsSkipPressed())
        {
            EndCutscene();
        }
    }

    /// <summary>
    /// Determines whether a user input event should skip the cutscene.
    /// </summary>
    /// <returns>
    /// True if a skip-triggering input was detected; otherwise false.
    /// </returns>
    private bool IsSkipPressed()
    {
        return Input.anyKeyDown
            || Input.GetMouseButtonDown(0)
            || Input.GetMouseButtonDown(1)
            || Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.Escape);
    }

    /// <summary>
    /// Invoked automatically when the video reaches its end.
    /// </summary>
    /// <param name="vp">The video player that finished playback.</param>
    private void HandleFinished(VideoPlayer vp)
    {
        EndCutscene();
    }

    /// <summary>
    /// Stops playback, prevents duplicate termination, and
    /// invokes the completion callback.
    /// </summary>
    private void EndCutscene()
    {
        if (isEnding)
            return;

        isEnding = true;

        VideoPlayer.loopPointReached -= HandleFinished;

        VideoPlayer.Stop();
        AudioSource.Stop();

        OnFinished?.Invoke();
    }
}
