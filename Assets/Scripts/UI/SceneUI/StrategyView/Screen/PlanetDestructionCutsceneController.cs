using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;
using Rebellion.Gameplay;

/// <summary>
/// Requests ship-specific movies for planet destruction involving the viewing player.
/// </summary>
public sealed class PlanetDestructionCutsceneController : IDisposable
{
    private readonly GameResultPipeline pipeline;
    private readonly FactionThemeLibrary themes;
    private readonly Func<string> getPlayerFactionId;
    private readonly Action<string> enqueueCutscene;

    /// <summary>
    /// Subscribes to settled results before headquarters and campaign-ending presentations.
    /// </summary>
    /// <param name="pipeline">The active session's ordered result pipeline.</param>
    /// <param name="themes">The available presentation themes.</param>
    /// <param name="getPlayerFactionId">Resolves the current viewing faction.</param>
    /// <param name="enqueueCutscene">Adds a movie to the screen's existing playback queue.</param>
    public PlanetDestructionCutsceneController(
        GameResultPipeline pipeline,
        FactionThemeLibrary themes,
        Func<string> getPlayerFactionId,
        Action<string> enqueueCutscene
    )
    {
        this.pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        this.themes = themes ?? throw new ArgumentNullException(nameof(themes));
        this.getPlayerFactionId =
            getPlayerFactionId ?? throw new ArgumentNullException(nameof(getPlayerFactionId));
        this.enqueueCutscene =
            enqueueCutscene ?? throw new ArgumentNullException(nameof(enqueueCutscene));
        pipeline.ResultsResolved += HandleResults;
    }

    /// <summary>
    /// Detaches the presentation observer when the strategy scene or session is replaced.
    /// </summary>
    public void Dispose()
    {
        pipeline.ResultsResolved -= HandleResults;
    }

    /// <summary>
    /// Resolves the firing ship's movie from the participating player's theme.
    /// </summary>
    /// <param name="results">The settled gameplay results.</param>
    private void HandleResults(IReadOnlyList<GameResult> results)
    {
        string playerFactionId = getPlayerFactionId();
        if (
            string.IsNullOrEmpty(playerFactionId)
            || !themes.TryGetTheme(playerFactionId, out FactionTheme theme)
        )
            return;

        foreach (BombardmentResult result in results.OfType<BombardmentResult>())
        {
            if (
                !result.PlanetDestroyed
                || string.IsNullOrEmpty(result.PlanetDestroyerTypeID)
                || (
                    result.AttackerOwnerInstanceID != playerFactionId
                    && result.DefenderOwnerInstanceID != playerFactionId
                )
            )
                continue;

            string path = theme
                .PlanetDestructionCutscenes?.FirstOrDefault(cutscene =>
                    cutscene?.ShipTypeID == result.PlanetDestroyerTypeID
                )
                ?.Path;
            if (!string.IsNullOrWhiteSpace(path))
                enqueueCutscene(path);
        }
    }
}
