using Rebellion.Game;

/// <summary>
/// Temporary transport container for launching a game.
/// </summary>
public static class GameLaunchContext
{
    public static GameSummary Summary = CreateDefaultSummary();
    public static string SaveFilePath = null;
    public static string SaveFileName = null;
    public static bool IsLoadGame = false;

    public static void Reset()
    {
        Summary = CreateDefaultSummary();
        SaveFilePath = null;
        SaveFileName = null;
        IsLoadGame = false;
    }

    private static GameSummary CreateDefaultSummary()
    {
        return new GameSummary
        {
            Difficulty = GameDifficulty.Easy,
            GalaxySize = GameSize.Small,
            VictoryCondition = GameVictoryCondition.Conquest,
            ResourceAvailability = GameResourceAvailability.Abundant,
            StartingResearchLevel = 1,
            PlayerFactionID = "FNALL1",
            StartingFactionIDs = new string[] { "FNALL1", "FNEMP1" },
        };
    }
}
