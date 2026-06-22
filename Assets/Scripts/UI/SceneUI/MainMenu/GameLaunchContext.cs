using System;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;

/// <summary>
/// Temporary transport container for launching a game.
/// </summary>
public static class GameLaunchContext
{
    public static GameSummary Summary = CreateDefaultSummary();
    public static string SaveFilePath = null;
    public static string SaveFileName = null;
    public static bool IsLoadGame = false;
    public static bool PlayIntroCutscene = false;

    public static void Reset()
    {
        Summary = CreateDefaultSummary();
        SaveFilePath = null;
        SaveFileName = null;
        IsLoadGame = false;
        PlayIntroCutscene = false;
    }

    private static GameSummary CreateDefaultSummary()
    {
        string[] startingFactionIds = GetDefaultStartingFactionIds();
        return new GameSummary
        {
            Difficulty = GameDifficulty.Easy,
            GalaxySize = GameSize.Small,
            VictoryCondition = GameVictoryCondition.Conquest,
            ResourceAvailability = GameResourceAvailability.Normal,
            StartingResearchLevel = 1,
            PlayerFactionID = startingFactionIds.FirstOrDefault(),
            StartingFactionIDs = startingFactionIds,
        };
    }

    private static string[] GetDefaultStartingFactionIds()
    {
        try
        {
            return ResourceManager
                .GetGameData<Faction>()
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
