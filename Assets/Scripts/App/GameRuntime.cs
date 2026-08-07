using System;
using System.IO;
using Rebellion.Game;
using UnityEngine;

/// <summary>
/// Application-level runtime controller.
/// Owns application state and decides execution strategy for global commands.
/// Owns the GameManager lifecycle.
/// </summary>
public sealed class GameRuntime
{
    private readonly Action<string> _loadScene;
    private readonly ContentPack _contentPack;
    private readonly SaveGameManager _saveGameManager;
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
    /// Creates an application runtime backed by the active content pack.
    /// </summary>
    /// <param name="loadScene">The application scene transition delegate.</param>
    /// <param name="contentPack">The active content pack.</param>
    internal GameRuntime(
        Action<string> loadScene,
        ContentPack contentPack,
        SaveGameManager saveGameManager = null
    )
    {
        _loadScene = loadScene ?? throw new ArgumentNullException(nameof(loadScene));
        _contentPack = contentPack ?? throw new ArgumentNullException(nameof(contentPack));
        _saveGameManager = saveGameManager ?? SaveGameManager.Instance;
    }

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

        ValidateGameContent(game);
        _activeGameSession = new GameManager(game, _contentPack.GameData);
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

        _saveGameManager.SaveQuickGameData(game);
        Debug.Log(
            $"Quick save completed: {_saveGameManager.GetSaveFilePath(SaveGameManager.QuickSaveFileName)}"
        );
    }

    /// <summary>
    /// Quick load a game.
    /// If active game exists: hot reload into current session.
    /// If no active game: cold start from main menu.
    /// </summary>
    public void QuickLoad()
    {
        if (LoadGame(SaveGameManager.QuickSaveFileName))
            Debug.Log("Quick load completed.");
        else
            Debug.LogWarning("Quick load skipped because no quick save exists.");
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

        string savePath = _saveGameManager.GetSaveFilePath(fileName);
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
        GameRoot loadedGame = _saveGameManager.LoadGameData(fileName);
        ValidateGameContent(loadedGame);
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
        _loadScene(SaveMenuLaunchContext.StrategyViewSceneName);
    }

    /// <summary>
    /// Verifies that a game was created by the active pack, version, and scenario.
    /// </summary>
    /// <param name="game">The game whose content identity is being validated.</param>
    internal void ValidateGameContent(GameRoot game)
    {
        if (game?.Summary == null)
            throw new InvalidOperationException("Game content identity is missing.");

        GameSummary summary = game.Summary;
        if (!_contentPack.MatchesContentIdentity(summary))
        {
            throw new InvalidOperationException(
                $"Save requires content pack '{summary.PackID}' version '{summary.PackVersion}' "
                    + $"scenario '{summary.ScenarioID}', but '{_contentPack.Definition.ID}' version "
                    + $"'{_contentPack.Definition.Version}' scenario '{_contentPack.Scenario.ID}' is active."
            );
        }
    }
}
