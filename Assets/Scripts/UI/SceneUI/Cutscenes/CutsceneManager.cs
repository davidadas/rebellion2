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

    private CutscenePlayer activePlayer;
    private bool ownsTimePause;
    private float previousTimeScale = _runningTimeScale;

    internal void Initialize(GameObject prefab)
    {
        cutscenePrefab =
            prefab
            ?? throw new ArgumentNullException(nameof(prefab), "Cutscene prefab is missing.");
    }

    private void OnDestroy()
    {
        DestroyActivePlayer();
        RestoreTimeScale();
    }

    public void Play(string clipAddress, Action onFinished)
    {
        CancelPlayback();
        if (string.IsNullOrWhiteSpace(clipAddress))
        {
            onFinished?.Invoke();
            return;
        }

        StartPlayback(ResourceManager.GetVideoUrl(clipAddress), onFinished);
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

    private void CancelPlayback()
    {
        DestroyActivePlayer();
        RestoreTimeScale();
    }
}
