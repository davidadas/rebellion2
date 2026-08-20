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
    [SerializeField]
    private RawImage screen;

    [SerializeField]
    private VideoPlayer videoPlayer;

    [SerializeField]
    private AudioSource audioSource;

    private Action onFinished;
    private bool isEnding;
    private Color authoredScreenColor;

    /// <summary>
    /// Applies the effective volume for video playback.
    /// </summary>
    /// <param name="volume">The master-scaled video volume.</param>
    internal void SetVolume(float volume)
    {
        audioSource.volume = Mathf.Clamp01(volume);
    }

    /// <summary>
    /// Configures video playback defaults.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.errorReceived += HandlePlaybackError;
        audioSource.playOnAwake = false;
        authoredScreenColor = screen.color;
        BlankScreen();
    }

    /// <summary>
    /// Releases playback callbacks without completing an interrupted request.
    /// </summary>
    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.errorReceived -= HandlePlaybackError;
            videoPlayer.loopPointReached -= HandleFinished;
        }

        BlankScreen();
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

        HideUntilFirstFrame();
        videoPlayer.loopPointReached += HandleFinished;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = clip;

        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.SetTargetAudioSource(0, audioSource);

        videoPlayer.Play();
        audioSource.Play();
    }

    /// <summary>
    /// Begins playback from a local video URL.
    /// </summary>
    /// <param name="videoUrl">The local video file URL.</param>
    /// <param name="finished">The callback invoked after playback ends or is skipped.</param>
    public void Play(string videoUrl, Action finished)
    {
        ConfigureUrlPlayback(videoUrl, finished);
        videoPlayer.Play();
        audioSource.Play();
    }

    /// <summary>
    /// Configures URL playback without starting the platform video decoder.
    /// </summary>
    /// <param name="videoUrl">The local video file URL.</param>
    /// <param name="finished">The callback invoked after playback ends or is skipped.</param>
    internal void ConfigureUrlPlayback(string videoUrl, Action finished)
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
            throw new ArgumentException("A video URL is required.", nameof(videoUrl));

        isEnding = false;
        onFinished = finished;

        HideUntilFirstFrame();
        videoPlayer.loopPointReached += HandleFinished;
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = videoUrl;

        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.SetTargetAudioSource(0, audioSource);
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
    /// Reports decoder failures and releases a cutscene that cannot begin playback.
    /// </summary>
    /// <param name="source">The video player that reported the failure.</param>
    /// <param name="message">The platform decoder's error message.</param>
    private void HandlePlaybackError(VideoPlayer source, string message)
    {
        Debug.LogWarning($"Cutscene playback failed: {message}", source);
        GameStartupTrace.Log($"Faction introduction decoder failed: {message}");
        EndCutscene();
    }

    /// <summary>
    /// Reveals the cutscene surface when the current clip produces its first frame.
    /// </summary>
    /// <param name="source">The video player that produced the frame.</param>
    /// <param name="frameIndex">The decoded frame index.</param>
    private void HandleFirstFrameReady(VideoPlayer source, long frameIndex)
    {
        Texture texture = source.texture;
        if (texture == null || texture.height <= 0)
            return;

        RevealFrame(texture, frameIndex);
    }

    /// <summary>
    /// Presents one decoded frame using its native texture and aspect ratio.
    /// </summary>
    /// <param name="texture">The decoder-owned video texture.</param>
    /// <param name="frameIndex">The decoded frame index.</param>
    private void RevealFrame(Texture texture, long frameIndex)
    {
        videoPlayer.frameReady -= HandleFirstFrameReady;
        videoPlayer.sendFrameReadyEvents = false;
        screen.texture = texture;
        screen.GetComponent<AspectRatioFitter>().aspectRatio =
            (float)texture.width / texture.height;
        screen.color = authoredScreenColor;
        GameStartupTrace.Log($"Faction introduction first frame displayed (frame {frameIndex}).");
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
        BlankScreen();

        videoPlayer.Stop();
        audioSource.Stop();

        Action finished = onFinished;
        onFinished = null;
        finished?.Invoke();
    }

    /// <summary>
    /// Blanks the cutscene surface until the current clip produces its first frame.
    /// </summary>
    private void HideUntilFirstFrame()
    {
        BlankScreen();
        videoPlayer.frameReady -= HandleFirstFrameReady;
        videoPlayer.frameReady += HandleFirstFrameReady;
        videoPlayer.sendFrameReadyEvents = true;
    }

    /// <summary>
    /// Blanks the cutscene surface and releases first-frame callbacks.
    /// </summary>
    private void BlankScreen()
    {
        if (screen != null)
        {
            screen.texture = null;
            screen.color = Color.black;
        }
        if (videoPlayer == null)
            return;

        videoPlayer.frameReady -= HandleFirstFrameReady;
        videoPlayer.sendFrameReadyEvents = false;
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
