using System;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Manages the playback lifecycle of cutscenes within the active scene.
///
/// Responsibilities:
/// - Instantiates and controls a <see cref="CutscenePlayer"/>
/// - Ensures only one cutscene plays at a time
/// - Pauses gameplay time during playback
/// - Restores time and invokes a completion callback when finished
/// </summary>
public sealed class CutsceneManager : MonoBehaviour
{
    private const float _pausedTimeScale = 0f;
    private const float _runningTimeScale = 1f;

    private GameObject cutscenePrefab;
    private ContentAssets contentAssets;

    private CutscenePlayer activePlayer;
    private bool ownsTimePause;
    private float previousTimeScale = _runningTimeScale;

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
    /// Cancels active playback and restores scaled application time on destruction.
    /// </summary>
    private void OnDestroy()
    {
        DestroyActivePlayer();
        RestoreTimeScale();
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
        PauseTimeScale();
        try
        {
            player.Play(clip, () => FinishPlayback(player, onFinished));
        }
        catch
        {
            DestroyActivePlayer();
            RestoreTimeScale();
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
        PauseTimeScale();
        try
        {
            player.Play(videoUrl, () => FinishPlayback(player, onFinished));
        }
        catch
        {
            DestroyActivePlayer();
            RestoreTimeScale();
            throw;
        }
    }

    /// <summary>
    /// Restores application time, destroys the active player, and completes the request.
    /// </summary>
    /// <param name="player">The player that completed playback.</param>
    /// <param name="onFinished">The callback supplied with the playback request.</param>
    private void FinishPlayback(CutscenePlayer player, Action onFinished)
    {
        if (activePlayer != player)
            return;

        RestoreTimeScale();
        DestroyActivePlayer();

        onFinished?.Invoke();
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
        DestroyActivePlayer();
        RestoreTimeScale();
    }
}
