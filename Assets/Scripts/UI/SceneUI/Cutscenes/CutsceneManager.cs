using System;
using UnityEngine;
using UnityEngine.Serialization;
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

    public static CutsceneManager Instance { get; private set; }

    [SerializeField]
    [FormerlySerializedAs("CutscenePrefab")]
    private CutscenePlayer cutscenePrefab;

    private CutscenePlayer activePlayer;
    private bool ownsTimePause;
    private float previousTimeScale = _runningTimeScale;

    /// <summary>
    /// Initializes the singleton instance.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
            throw new InvalidOperationException("Only one CutsceneManager may be active.");
        if (cutscenePrefab == null)
            throw new MissingReferenceException($"{name}/CutscenePrefab is missing.");

        Instance = this;
    }

    /// <summary>
    /// Clears the global reference when this manager leaves the active scene.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance != this)
            return;

        DestroyActivePlayer();
        RestoreTimeScale();
        Instance = null;
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
        if (clip == null)
        {
            onFinished?.Invoke();
            return;
        }

        if (activePlayer != null)
            DestroyActivePlayer();

        CutscenePlayer player = Instantiate(cutscenePrefab);
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
}
