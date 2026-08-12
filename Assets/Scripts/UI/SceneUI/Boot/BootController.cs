using System;
using UnityEngine;

/// <summary>
/// Controls the initial boot sequence of the application.
/// Relies on <see cref="CutsceneManager"/> for video playback orchestration.
/// </summary>
public sealed class BootController : MonoBehaviour
{
    private static readonly string[] _bootVideoPaths =
    {
        "Application/Boot/Videos/intro",
        "Application/Boot/Videos/opening-crawl",
    };

    [SerializeField]
    private GameObject cutscenePlayerPrefab;

    private int currentVideoIndex;

    /// <summary>
    /// Begins the boot video sequence when the scene starts.
    /// </summary>
    private async void Start()
    {
        try
        {
            AppBootstrap bootstrap = AppBootstrap.EnsureExists();
            bootstrap.InitializeCutsceneManager(cutscenePlayerPrefab);
            await bootstrap.InitializeMainMenuContentAsync();
            PlayNext();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    /// <summary>
    /// Plays the next boot video in sequence.
    /// If all videos have been played, transitions to the MainMenu scene.
    /// </summary>
    private void PlayNext()
    {
        if (currentVideoIndex >= _bootVideoPaths.Length)
        {
            AppBootstrap.Instance.LoadScene(SceneNames.MainMenu);
            return;
        }

        string path = _bootVideoPaths[currentVideoIndex];
        currentVideoIndex++;

        AppBootstrap.Instance.GetCutsceneManager().Play(path, PlayNext);
    }
}
