using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class Startup
{
    static Startup()
    {
        TestGame();
    }

    static void TestGame()
    {
        // Generate a game given a summary.
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Large,
            Difficulty = GameDifficulty.Easy,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Abundant,
            PlayerFactionID = "FNALL1",
        };
        GameBuilder builder = new GameBuilder(summary);
        Game game = builder.BuildGame();

        // Save the file to disk for testing.
        SaveGameManager.Instance.SaveGameData(game, "Save 1");
    }
}
