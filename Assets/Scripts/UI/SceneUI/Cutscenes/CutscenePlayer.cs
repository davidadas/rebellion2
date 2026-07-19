using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Handles playback of a single cutscene instance.
/// This component is intended to be instantiated and managed by
/// <see cref="CutsceneManager"/>.
/// </summary>
public sealed class CutscenePlayer : MonoBehaviour
{
    [SerializeField]
    [FormerlySerializedAs("Screen")]
    private RawImage screen;

    [SerializeField]
    [FormerlySerializedAs("VideoPlayer")]
    private VideoPlayer videoPlayer;

    [SerializeField]
    [FormerlySerializedAs("AudioSource")]
    private AudioSource audioSource;

    private Action onFinished;
    private bool isEnding;

    /// <summary>
    /// Configures video playback defaults.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        audioSource.playOnAwake = false;
    }

    /// <summary>
    /// Releases playback callbacks without completing an interrupted request.
    /// </summary>
    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= HandleFinished;

        onFinished = null;
    }

    /// <summary>
    /// Begins playback of the specified <see cref="VideoClip"/>.
    /// </summary>
    /// <param name="clip">The video clip to play.</param>
    /// <param name="finished">The callback invoked after playback ends or is skipped.</param>
    public void Play(VideoClip clip, Action finished)
    {
        isEnding = false;
        onFinished = finished;

        videoPlayer.loopPointReached += HandleFinished;
        videoPlayer.clip = clip;

        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.SetTargetAudioSource(0, audioSource);

        videoPlayer.Play();
        audioSource.Play();
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

        videoPlayer.loopPointReached -= HandleFinished;

        videoPlayer.Stop();
        audioSource.Stop();

        Action finished = onFinished;
        onFinished = null;
        finished?.Invoke();
    }

    /// <summary>
    /// Verifies the authored playback components required by this player.
    /// </summary>
    private void VerifyReferences()
    {
        if (screen == null)
            throw new MissingReferenceException($"{name}/VideoScreenImage is missing.");
        if (videoPlayer == null)
            throw new MissingReferenceException($"{name}/VideoPlayer is missing.");
        if (audioSource == null)
            throw new MissingReferenceException($"{name}/AudioSource is missing.");
    }
}
