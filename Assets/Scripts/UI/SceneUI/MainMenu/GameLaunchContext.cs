using System;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;

/// <summary>
/// Carries new-game and load-game selections between menu and strategy scenes.
/// </summary>
public static class GameLaunchContext
{
    /// <summary>
    /// Gets the launch settings for a new game.
    /// </summary>
    public static GameSummary Summary { get; private set; } = CreateDefaultSummary();

    public static string SaveFileName { get; set; }

    public static bool IsLoadGame { get; set; }

    public static bool PlayIntroCutscene { get; set; }

    /// <summary>
    /// Restores the launch context to the authored new-game defaults.
    /// </summary>
    public static void Reset()
    {
        Summary = CreateDefaultSummary();
        SaveFileName = null;
        IsLoadGame = false;
        PlayIntroCutscene = false;
    }

    /// <summary>
    /// Creates the default launch summary.
    /// </summary>
    /// <returns>The default launch summary.</returns>
    private static GameSummary CreateDefaultSummary()
    {
        string[] startingFactionIds = GetDefaultStartingFactionIds();
        return new GameSummary
        {
            Difficulty = GameDifficulty.Easy,
            GalaxySize = GameSize.Large,
            VictoryCondition = GameVictoryCondition.Conquest,
            ResourceAvailability = GameResourceAvailability.Normal,
            StartingResearchLevel = 1,
            PlayerFactionID = startingFactionIds.FirstOrDefault(),
            StartingFactionIDs = startingFactionIds,
        };
    }

    /// <summary>
    /// Loads the configured faction identifiers available to a new game.
    /// </summary>
    /// <returns>The configured faction identifiers, or an empty array when data is unavailable.</returns>
    private static string[] GetDefaultStartingFactionIds()
    {
        try
        {
            return ResourceManager
                .GetEntityData<Faction>()
                .Where(faction => !string.IsNullOrEmpty(faction.InstanceID))
                .Select(faction => faction.InstanceID)
                .ToArray();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }
}
