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

    private int currentVideoIndex;

    /// <summary>
    /// Begins the boot video sequence when the scene starts.
    /// </summary>
    private async void Start()
    {
        try
        {
            NewGameLaunchRequest request = NewGameLaunchRequest.Read(
                Environment.GetCommandLineArgs()
            );
            AppBootstrap bootstrap = AppBootstrap.EnsureExists(
                request?.OpenContent(ContentPackLoader.ResolveContentRootPath())
            );
            await bootstrap.InitializeMainMenuContentAsync();
            if (request != null)
            {
                GameLaunchContext.ApplyCustomGameSummary(request.Summary);
                bootstrap.LoadScene("StrategyView");
                return;
            }
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
            AppBootstrap.Instance.LoadScene("MainMenu");
            return;
        }

        string path = _bootVideoPaths[currentVideoIndex];
        currentVideoIndex++;

        AppBootstrap.Instance.GetCutsceneManager().Play(path, PlayNext);
    }
}
