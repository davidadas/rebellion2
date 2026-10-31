using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Manages the playback lifecycle of cutscenes within the active scene.
///
/// Responsibilities:
/// - Instantiates and controls a <see cref="CutscenePlayer"/>
/// - Ensures only one cutscene plays at a time
/// - Pauses gameplay time and application audio during playback
/// - Restores application state and invokes a completion callback when finished
/// </summary>
public sealed class CutsceneManager : MonoBehaviour
{
    private const float _pausedTimeScale = 0f;
    private const float _runningTimeScale = 1f;

    private GameObject cutscenePrefab;
    private ContentAssets contentAssets;
    private AudioManager audioManager;

    private CutscenePlayer activePlayer;
    private Coroutine pendingCompletion;
    private bool _audioPauseApplied;
    private bool _presentationPauseApplied;
    private bool ownsTimePause;
    private bool previousAudioPause;
    private float previousTimeScale = _runningTimeScale;

    /// <summary>
    /// Occurs when full-screen video takes ownership of presentation.
    /// </summary>
    internal event Action PlaybackStarting;

    /// <summary>
    /// Occurs when full-screen video releases presentation.
    /// </summary>
    internal event Action PlaybackFinished;

    /// <summary>
    /// Binds the authored player prefab used for cutscene instances.
    /// </summary>
    /// <param name="prefab">The authored cutscene player prefab.</param>
    internal void Initialize(GameObject prefab)
    {
        cutscenePrefab =
            prefab
            ?? throw new ArgumentNullException(nameof(prefab), "Cutscene prefab is missing.");
    }

    /// <summary>
    /// Binds the application-owned external content assets used for addressed videos.
    /// </summary>
    /// <param name="assets">The active content asset store.</param>
    internal void InitializeContent(ContentAssets assets)
    {
        contentAssets = assets ?? throw new ArgumentNullException(nameof(assets));
    }

    /// <summary>
    /// Binds the application audio settings used by cutscene playback.
    /// </summary>
    /// <param name="manager">The active application audio manager.</param>
    internal void InitializeAudio(AudioManager manager)
    {
        audioManager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    /// <summary>
    /// Cancels active playback and restores application state on destruction.
    /// </summary>
    private void OnDestroy()
    {
        CancelPendingCompletion();
        DestroyActivePlayer();
        RestoreApplicationAudio();
        RestoreTimeScale();
        RestorePresentation();
    }

    /// <summary>
    /// Plays a video from a content address and completes the supplied callback afterward.
    /// </summary>
    /// <param name="clipAddress">The video content address.</param>
    /// <param name="onFinished">The callback invoked after playback completes.</param>
    public void Play(string clipAddress, Action onFinished)
    {
        CancelPlayback();
        if (string.IsNullOrWhiteSpace(clipAddress))
        {
            onFinished?.Invoke();
            return;
        }

        if (contentAssets == null)
            throw new InvalidOperationException(
                "CutsceneManager has not been initialized with a content asset store."
            );

        StartPlayback(contentAssets.GetVideoUrl(clipAddress), onFinished);
    }

    /// <summary>
    /// Plays the specified <see cref="VideoClip"/> and invokes a callback
    /// when playback completes.
    ///
    /// If the provided clip is null, the callback is invoked immediately.
    /// </summary>
    /// <param name="clip">The video clip to play.</param>
    /// <param name="onFinished">
    /// Action invoked after playback completes and gameplay time is restored.
    /// </param>
    public void Play(VideoClip clip, Action onFinished)
    {
        CancelPlayback();
        if (clip == null)
        {
            onFinished?.Invoke();
            return;
        }

        StartPlayback(clip, onFinished);
    }

    /// <summary>
    /// Creates a player instance and begins playback from an imported video clip.
    /// </summary>
    /// <param name="clip">The imported video clip.</param>
    /// <param name="onFinished">The callback invoked after playback completes.</param>
    private void StartPlayback(VideoClip clip, Action onFinished)
    {
        if (cutscenePrefab == null)
            throw new InvalidOperationException("CutsceneManager has not been initialized.");

        GameObject playerObject = Instantiate(cutscenePrefab);
        CutscenePlayer player = playerObject.GetComponent<CutscenePlayer>();
        if (player == null)
        {
            Destroy(playerObject);
            throw new MissingComponentException(
                $"{cutscenePrefab.name} has no CutscenePlayer component."
            );
        }

        activePlayer = player;
        player.SetVolume(GetVideoVolume());
        try
        {
            PausePresentation();
            PauseApplicationAudio();
            PauseTimeScale();
            player.Play(clip, () => FinishPlayback(player, onFinished));
        }
        catch
        {
            DestroyActivePlayer();
            RestoreApplicationAudio();
            RestoreTimeScale();
            RestorePresentation();
            throw;
        }
    }

    /// <summary>
    /// Creates a player instance and begins playback from a local video URL.
    /// </summary>
    /// <param name="videoUrl">The local video file URL.</param>
    /// <param name="onFinished">The callback invoked after playback completes.</param>
    private void StartPlayback(string videoUrl, Action onFinished)
    {
        if (cutscenePrefab == null)
            throw new InvalidOperationException("CutsceneManager has not been initialized.");

        GameObject playerObject = Instantiate(cutscenePrefab);
        CutscenePlayer player = playerObject.GetComponent<CutscenePlayer>();
        if (player == null)
        {
            Destroy(playerObject);
            throw new MissingComponentException(
                $"{cutscenePrefab.name} has no CutscenePlayer component."
            );
        }

        activePlayer = player;
        player.SetVolume(GetVideoVolume());
        try
        {
            PausePresentation();
            PauseApplicationAudio();
            PauseTimeScale();
            player.Play(videoUrl, () => FinishPlayback(player, onFinished));
        }
        catch
        {
            DestroyActivePlayer();
            RestoreApplicationAudio();
            RestoreTimeScale();
            RestorePresentation();
            throw;
        }
    }

    /// <summary>
    /// Gets the effective volume for a newly created cutscene player.
    /// </summary>
    /// <returns>The master-scaled video volume.</returns>
    private float GetVideoVolume()
    {
        return audioManager?.EffectiveVideoVolume ?? 1f;
    }

    /// <summary>
    /// Restores application state, destroys the active player, and completes the request.
    /// </summary>
    /// <param name="player">The player that completed playback.</param>
    /// <param name="onFinished">The callback supplied with the playback request.</param>
    private void FinishPlayback(CutscenePlayer player, Action onFinished)
    {
        if (activePlayer != player)
            return;

        DestroyActivePlayer();
        RestoreApplicationAudio();
        RestoreTimeScale();
        RestorePresentation();

        if (onFinished != null)
            pendingCompletion = StartCoroutine(CompleteAfterPlayerRelease(onFinished));
    }

    /// <summary>
    /// Completes playback after Unity has destroyed the stopped video player at the frame boundary.
    /// </summary>
    /// <param name="onFinished">The callback supplied with the playback request.</param>
    /// <returns>An enumerator that waits for the stopped player to be released.</returns>
    private IEnumerator CompleteAfterPlayerRelease(Action onFinished)
    {
        yield return null;
        pendingCompletion = null;
        onFinished();
    }

    /// <summary>
    /// Notifies presentation owners before full-screen video starts.
    /// </summary>
    private void PausePresentation()
    {
        if (_presentationPauseApplied)
            return;

        _presentationPauseApplied = true;
        PlaybackStarting?.Invoke();
    }

    /// <summary>
    /// Notifies presentation owners after full-screen video ends or is cancelled.
    /// </summary>
    private void RestorePresentation()
    {
        if (!_presentationPauseApplied)
            return;

        _presentationPauseApplied = false;
        PlaybackFinished?.Invoke();
    }

    /// <summary>
    /// Pauses application audio while allowing the cutscene player to remain audible.
    /// </summary>
    private void PauseApplicationAudio()
    {
        if (!_audioPauseApplied)
            previousAudioPause = AudioListener.pause;

        AudioListener.pause = true;
        _audioPauseApplied = true;
    }

    /// <summary>
    /// Restores the audio-listener state that preceded cutscene playback.
    /// </summary>
    private void RestoreApplicationAudio()
    {
        if (!_audioPauseApplied)
            return;

        AudioListener.pause = previousAudioPause;
        _audioPauseApplied = false;
    }

    /// <summary>
    /// Pauses scaled gameplay time for the active cutscene.
    /// </summary>
    private void PauseTimeScale()
    {
        if (!ownsTimePause)
            previousTimeScale = Time.timeScale;

        Time.timeScale = _pausedTimeScale;
        ownsTimePause = true;
    }

    /// <summary>
    /// Restores scaled gameplay time after this manager's pause.
    /// </summary>
    private void RestoreTimeScale()
    {
        if (!ownsTimePause)
            return;

        Time.timeScale = previousTimeScale;
        ownsTimePause = false;
    }

    /// <summary>
    /// Destroys the current player without completing an interrupted request.
    /// </summary>
    private void DestroyActivePlayer()
    {
        if (activePlayer == null)
            return;

        GameObject playerObject = activePlayer.gameObject;
        activePlayer = null;
        if (Application.isPlaying)
            Destroy(playerObject);
        else
            DestroyImmediate(playerObject);
    }

    /// <summary>
    /// Cancels the current playback request without invoking its completion callback.
    /// </summary>
    private void CancelPlayback()
    {
        CancelPendingCompletion();
        DestroyActivePlayer();
        RestoreApplicationAudio();
        RestoreTimeScale();
        RestorePresentation();
    }

    /// <summary>
    /// Cancels a completion callback that is waiting for the previous player to be released.
    /// </summary>
    private void CancelPendingCompletion()
    {
        if (pendingCompletion == null)
            return;

        StopCoroutine(pendingCompletion);
        pendingCompletion = null;
    }
}
