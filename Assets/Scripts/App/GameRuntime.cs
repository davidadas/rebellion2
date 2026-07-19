using System;
using System.IO;
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

    /// <summary>
    /// Gets whether a game session is currently active.
    /// </summary>
    public bool HasActiveGame => _activeGameSession != null;

    /// <summary>
    /// Raised when global input requests the settings menu.
    /// </summary>
    public event Action ToggleSettingsMenuRequested;

    /// <summary>
    /// Get the current active game instance.
    /// </summary>
    /// <returns>The active game, or null when no game session is active.</returns>
    public GameRoot GetActiveGame()
    {
        return _activeGameSession?.GetGame();
    }

    /// <summary>
    /// Gets the active game manager.
    /// </summary>
    /// <returns>The active game manager, or null when no game session is active.</returns>
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
    }

    /// <summary>
    /// Quick save the current game.
    /// If no active game, does nothing.
    /// </summary>
    public void QuickSave()
    {
        if (!HasActiveGame)
            return;

        GameRoot game = GetActiveGame();
        if (game == null)
            return;

        SaveGameManager.Instance.SaveQuickGameData(game);
    }

    /// <summary>
    /// Quick load a game.
    /// If active game exists: hot reload into current session.
    /// If no active game: cold start from main menu.
    /// </summary>
    public void QuickLoad()
    {
        LoadGame(SaveGameManager.QuickSaveFileName);
    }

    /// <summary>
    /// Loads a save file into the active game session or starts the strategy scene.
    /// </summary>
    /// <param name="fileName">The save file name to load.</param>
    /// <returns>True if the save file exists and load started.</returns>
    public bool LoadGame(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;

        string savePath = SaveGameManager.Instance.GetSaveFilePath(fileName);
        if (!File.Exists(savePath))
            return false;

        if (HasActiveGame)
            HotReloadGame(fileName);
        else
            ColdStartFromSave(fileName);

        return true;
    }

    /// <summary>
    /// Toggle settings menu.
    /// Execution depends on current scene context.
    /// </summary>
    public void ToggleSettingsMenu()
    {
        ToggleSettingsMenuRequested?.Invoke();
    }

    /// <summary>
    /// Loads the save into the current active game session without reloading the scene.
    /// </summary>
    /// <param name="fileName">The save file name to load.</param>
    private void HotReloadGame(string fileName)
    {
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(fileName);
        _activeGameSession.ReplaceGame(loadedGame);
    }

    /// <summary>
    /// Loads the save by setting the launch context and transitioning to the strategy scene.
    /// </summary>
    /// <param name="fileName">The save file name to load.</param>
    private void ColdStartFromSave(string fileName)
    {
        GameLaunchContext.IsLoadGame = true;
        GameLaunchContext.SaveFileName = fileName;
        GameLaunchContext.PlayIntroCutscene = false;
        SceneManager.LoadScene("StrategyView");
    }
}
