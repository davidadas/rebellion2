using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class Startup
{
    /// <summary>
    ///
    /// </summary>
    static Startup()
    {
        TestSaveGame();
        TestLoadGame();
        TestEvents();
    }

    /// <summary>
    ///
    /// </summary>
    static void TestSaveGame()
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

    /// <summary>
    ///
    /// </summary>
    static void TestLoadGame()
    {
        Game game = SaveGameManager.Instance.LoadGameData("Save 1");
    }

    /// <summary>
    ///
    /// </summary>
    static void TestEvents()
    {
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

        Officer luke = game.FindNodesByGameID<Officer>("OFAL003")[0];
        Planet yavin = game.FindNodesByGameID<Planet>("PLSUM06")[0];
        Debug.Log(luke.DisplayName);
        Debug.Log(yavin.DisplayName);

        game.AddGameEvent(1, new MoveUnitEvent(luke, yavin));
        game.IncrementTick();
        Debug.Log(yavin.Officers[yavin.Officers.Count - 1].DisplayName);
    }
}
