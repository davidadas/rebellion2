using UnityEngine;

public class GameInitializer : MonoBehaviour
{
    private Game game;
    private GameManager gameManager;

    /// <summary>
    ///
    /// </summary>
    private void InitializeGame()
    {
        // Create a new GameSummary object with specific configurations
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Large,
            Difficulty = GameDifficulty.Easy,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Abundant,
            PlayerFactionID = "FNALL1",
        };

        // Create a new GameBuilder instance with the summary
        GameBuilder builder = new GameBuilder(summary);

        // Build the game using the GameBuilder
        game = builder.BuildGame();

        SaveGameManager.Instance.SaveGameData(game, "test");
    }

    /// <summary>
    ///
    /// </summary>
    private void InitializeGameManager()
    {
        gameManager = new GameManager(game);

        gameManager.SetTickSpeed(TickSpeed.Fast);
    }

    /// <summary>
    ///
    /// </summary>
    public void Start()
    {
        InitializeGame();
        InitializeGameManager();
    }

    /// <summary>
    ///
    /// </summary>
    public void Update()
    {
        if (gameManager != null)
        {
            gameManager.Update();
        }
    }
}
