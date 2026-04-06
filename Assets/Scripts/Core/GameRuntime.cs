using System;
using Rebellion.Game;
using UnityEngine;
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

    public event Action ToggleSettingsMenuRequested;

    /// <summary>
    /// Get the current active game instance.
    /// </summary>
    /// <returns></returns>
    public GameRoot GetActiveGame()
    {
        return _activeGameSession?.GetGame();
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
        // _activeGameSession.InitializeSystems(); // TODO: Re-enable after migrating to Systems // Rebuild derived structures (queues, caches, etc.)

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
        string savePath = SaveGameManager.Instance.GetSaveFilePath("quicksave");

        if (!System.IO.File.Exists(savePath))
            return;

        if (HasActiveGame)
            HotReloadGame();
        else
            ColdStartFromSave();
    }

    /// <summary>
    ///
    /// </summary>
    private void HotReloadGame()
    {
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData("quicksave");
        _activeGameSession.ReplaceGame(loadedGame);
        // _activeGameSession.InitializeSystems(); // TODO: Re-enable after migrating to Systems // Rebuild derived structures after load
    }

    /// <summary>
    ///
    /// </summary>
    private void ColdStartFromSave()
    {
        GameLaunchContext.IsLoadGame = true;
        GameLaunchContext.SaveFileName = "quicksave";
        SceneManager.LoadScene("StrategyView");
    }

    /// <summary>
    /// Toggle settings menu.
    /// Execution depends on current scene context.
    /// </summary>
    public void ToggleSettingsMenu()
    {
        ToggleSettingsMenuRequested?.Invoke();
    }
}
