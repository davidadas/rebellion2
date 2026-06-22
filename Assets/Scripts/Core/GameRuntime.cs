using Rebellion.Game;
using UnityEngine.SceneManagement;

/// <summary>
/// Application-level runtime controller.
/// Owns application state and decides execution strategy for global commands.
/// Owns the GameManager lifecycle.
/// </summary>
public sealed class GameRuntime
{
    private GameManager _activeGameSession;

    public bool HasActiveGame => _activeGameSession != null;

    /// <summary>
    /// Get the current active game instance.
    /// </summary>
    /// <returns></returns>
    public GameRoot GetActiveGame()
    {
        return _activeGameSession?.GetGame();
    }

    public GameManager GetActiveGameManager()
    {
        return _activeGameSession;
    }

    /// <summary>
    /// Start a new game session.
    /// Creates and owns the GameManager for this session.
    /// </summary>
    /// <param name="game">The game instance to manage.</param>
    /// <returns>The created GameManager.</returns>
    public GameManager StartGame(GameRoot game)
    {
        if (_activeGameSession != null)
        {
            EndGame();
        }

        _activeGameSession = new GameManager(game);
        return _activeGameSession;
    }

    /// <summary>
    /// End the current game session.
    /// Stops game logic and clears the active session.
    /// </summary>
    public void EndGame()
    {
        if (_activeGameSession == null)
            return;

        _activeGameSession.SetGameSpeed(TickSpeed.Paused);
        _activeGameSession = null;

        // @TODO: Notify UI systems, unload scene state, cleanup resources
    }

    /// <summary>
    /// Quick save the current game.
    /// If no active game, does nothing.
    /// </summary>
    public void QuickSave()
    {
        if (!HasActiveGame)
        {
            return;
        }

        GameRoot game = _activeGameSession.GetGame();
        if (game == null)
        {
            return;
        }

        // @TODO: Handle errors or issues during save (e.g. disk full, serialization error).
        SaveGameManager.Instance.SaveGameData(game, "quicksave");
    }

    /// <summary>
    /// Quick load a game.
    /// If active game exists: hot reload into current session.
    /// If no active game: cold start from main menu.
    /// </summary>
    public void QuickLoad()
    {
        LoadGame("quicksave");
    }

    public bool LoadGame(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;

        string savePath = SaveGameManager.Instance.GetSaveFilePath(fileName);
        if (!System.IO.File.Exists(savePath))
            return false;

        if (HasActiveGame)
            HotReloadGame(fileName);
        else
            ColdStartFromSave(fileName);

        return true;
    }

    public bool SaveGame(string fileName, string displayName)
    {
        if (string.IsNullOrEmpty(fileName) || !HasActiveGame)
            return false;

        GameRoot game = _activeGameSession.GetGame();
        if (game == null)
            return false;

        game.Metadata ??= new GameMetadata();
        game.Metadata.SaveDisplayName = displayName;
        game.Metadata.PlayerFactionID = game.Summary?.PlayerFactionID;
        SaveGameManager.Instance.SaveGameData(game, fileName);
        return true;
    }

    /// <summary>
    /// Loads the quicksave into the current active game session without reloading the scene.
    /// </summary>
    private void HotReloadGame(string fileName)
    {
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(fileName);
        _activeGameSession.ReplaceGame(loadedGame);
    }

    /// <summary>
    /// Loads the quicksave by setting the launch context and transitioning to the strategy scene.
    /// </summary>
    private void ColdStartFromSave(string fileName)
    {
        GameLaunchContext.IsLoadGame = true;
        GameLaunchContext.SaveFileName = fileName;
        GameLaunchContext.PlayIntroCutscene = false;
        SceneManager.LoadScene("StrategyView");
    }
}
