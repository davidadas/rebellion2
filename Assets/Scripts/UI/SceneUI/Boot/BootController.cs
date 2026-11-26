using System;
using Rebellion.Util.Logging;
using UnityEngine;

/// <summary>
/// Controls the initial boot sequence of the application.
/// Relies on <see cref="CutsceneManager"/> for video playback orchestration.
/// </summary>
public sealed class BootController : MonoBehaviour
{
    private string[] bootVideoPaths;
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
            bootVideoPaths = GetVideoPaths(bootstrap.GetContentPack().Definition);
            PlayNext();
        }
        catch (Exception exception)
        {
            GameLogger.LogException(exception);
            LoadingScreenController.ShowStartupFailure(exception);
        }
    }

    /// <summary>
    /// Resolves the two startup video addresses in playback order.
    /// </summary>
    /// <param name="definition">The active pack's settings.</param>
    /// <returns>The startup intro and opening crawl, using the built-in addresses for empty slots.</returns>
    internal static string[] GetVideoPaths(ContentPackDefinition definition) =>
        new[]
        {
            string.IsNullOrWhiteSpace(definition.StartupIntroVideoPath)
                ? ContentPackDefinition.DefaultStartupIntroVideoPath
                : definition.StartupIntroVideoPath,
            string.IsNullOrWhiteSpace(definition.OpeningCrawlVideoPath)
                ? ContentPackDefinition.DefaultOpeningCrawlVideoPath
                : definition.OpeningCrawlVideoPath,
        };

    /// <summary>
    /// Plays the next boot video in sequence.
    /// If all videos have been played, transitions to the MainMenu scene.
    /// </summary>
    private void PlayNext()
    {
        if (currentVideoIndex >= bootVideoPaths.Length)
        {
            AppBootstrap.Instance.LoadScene("MainMenu");
            return;
        }

        string path = bootVideoPaths[currentVideoIndex];
        currentVideoIndex++;

        AppBootstrap.Instance.GetCutsceneManager().Play(path, PlayNext);
    }
}
