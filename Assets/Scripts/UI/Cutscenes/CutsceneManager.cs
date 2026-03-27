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
    /// <summary>
    /// Global singleton instance of the CutsceneManager.
    /// </summary>
    public static CutsceneManager Instance { get; private set; }

    /// <summary>
    /// Prefab used to create a runtime <see cref="CutscenePlayer"/> instance.
    /// </summary>
    [SerializeField]
    private CutscenePlayer CutscenePrefab;

    private CutscenePlayer ActivePlayer;

    /// <summary>
    /// Initializes the singleton instance.
    /// </summary>
    private void Awake()
    {
        Instance = this;
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

        // Ensure no previous cutscene remains active.
        if (ActivePlayer != null)
        {
            Destroy(ActivePlayer.gameObject);
            ActivePlayer = null;
        }

        // Pause gameplay while the cutscene is playing.
        Time.timeScale = 0f;

        ActivePlayer = Instantiate(CutscenePrefab);

        ActivePlayer.OnFinished = () =>
        {
            // Resume gameplay time.
            Time.timeScale = 1f;

            if (ActivePlayer != null)
            {
                Destroy(ActivePlayer.gameObject);
                ActivePlayer = null;
            }

            onFinished?.Invoke();
        };

        ActivePlayer.Play(clip);
    }
}
