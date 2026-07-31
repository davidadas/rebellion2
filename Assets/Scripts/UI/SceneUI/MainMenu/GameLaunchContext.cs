using System;
using System.Linq;
using Rebellion.Game;

/// <summary>
/// Carries new-game and load-game selections between menu and strategy scenes.
/// </summary>
public static class GameLaunchContext
{
    /// <summary>
    /// Gets the launch settings for a new game.
    /// </summary>
    public static GameSummary Summary { get; private set; } = new GameSummary();

    public static string SaveFileName { get; set; }

    public static bool IsLoadGame { get; set; }

    public static bool PlayIntroCutscene { get; set; }

    /// <summary>
    /// Restores the launch context to the authored new-game defaults.
    /// </summary>
    /// <param name="contentPack">The active content pack.</param>
    public static void Reset(ContentPack contentPack)
    {
        Summary = CreateDefaultSummary(contentPack);
        SaveFileName = null;
        IsLoadGame = false;
        PlayIntroCutscene = false;
    }

    /// <summary>
    /// Creates the default launch summary.
    /// </summary>
    /// <param name="contentPack">The active content pack.</param>
    /// <returns>The default launch summary.</returns>
    private static GameSummary CreateDefaultSummary(ContentPack contentPack)
    {
        if (contentPack == null)
            throw new ArgumentNullException(nameof(contentPack));

        string[] startingFactionIds = contentPack.Scenario.PlayableFactionIDs.ToArray();
        return new GameSummary
        {
            Difficulty = GameDifficulty.Easy,
            GalaxySize = GameSize.Large,
            VictoryCondition = GameVictoryCondition.Conquest,
            ResourceAvailability = GameResourceAvailability.Normal,
            StartingResearchLevel = 1,
            PlayerFactionID = contentPack.Scenario.DefaultPlayerFactionID,
            StartingFactionIDs = startingFactionIds,
            PackID = contentPack.Definition.ID,
            PackVersion = contentPack.Definition.Version,
            ScenarioID = contentPack.Scenario.ID,
        };
    }
}
