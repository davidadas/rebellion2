using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Simulation;
using Rebellion.Util.Logging;
using Rebellion.Util.Reflection;
using UnityEngine;

/// <summary>
/// Owns the active game-session lifecycle, hot loading, and content-identity validation.
/// </summary>
public sealed class GameRuntime
{
    private readonly ContentPack _contentPack;
    private readonly SaveGameManager _saveGameManager;
    private readonly Func<UserGameplaySettings> _getGameplaySettings;
    private GameSession _activeGameSession;
    private GameManager _gameManager;

    public event Action<GameRoot> GameReplaced;

    /// <summary>
    /// Gets whether a game session is currently active.
    /// </summary>
    public bool HasActiveGame => _activeGameSession != null;

    /// <summary>
    /// Gets whether the active simulation is at a stable boundary that can be saved.
    /// </summary>
    public bool CanSave => _activeGameSession?.CanCaptureSettledState == true;

    /// <summary>
    /// Creates an application runtime backed by the active content pack.
    /// </summary>
    /// <param name="contentPack">The active content pack.</param>
    /// <param name="saveGameManager">The save manager, or null to use the application singleton.</param>
    /// <param name="getGameplaySettings">Returns the current gameplay settings.</param>
    internal GameRuntime(
        ContentPack contentPack,
        SaveGameManager saveGameManager = null,
        Func<UserGameplaySettings> getGameplaySettings = null
    )
    {
        _contentPack = contentPack ?? throw new ArgumentNullException(nameof(contentPack));
        _saveGameManager = saveGameManager ?? SaveGameManager.Instance;
        _getGameplaySettings = getGameplaySettings;
    }

    /// <summary>
    /// Get the current active game instance.
    /// </summary>
    /// <returns>The active game, or null when no game session is active.</returns>
    public GameRoot GetActiveGame()
    {
        return _activeGameSession?.Game;
    }

    /// <summary>
    /// Gets the active game manager.
    /// </summary>
    /// <returns>The active game manager, or null when no game session is active.</returns>
    public GameManager GetActiveGameManager()
    {
        return _gameManager;
    }

    /// <summary>
    /// Gets the active simulation for strategy composition.
    /// </summary>
    /// <returns>The active session, or null when no game is running.</returns>
    public GameSession GetActiveGameSession() => _activeGameSession;

    /// <summary>
    /// Start a new game session.
    /// Creates and owns the simulation and its application clock.
    /// </summary>
    /// <param name="game">The game instance to manage.</param>
    /// <returns>The created game session.</returns>
    public GameSession StartGame(GameRoot game)
    {
        return ReplaceSession(game);
    }

    /// <summary>
    /// Starts a game session from deserialized state and reconstructs unresolved runtime decisions.
    /// </summary>
    /// <param name="game">The loaded game instance to manage.</param>
    /// <returns>The created game session.</returns>
    public GameSession StartLoadedGame(GameRoot game)
    {
        GameSession session = ReplaceSession(game);
        session.Tick.ReconcileLoadedState();
        return session;
    }

    /// <summary>
    /// Replaces the active game session with one backed by the supplied game.
    /// </summary>
    /// <param name="game">The game instance to manage.</param>
    /// <returns>The created game session.</returns>
    private GameSession ReplaceSession(GameRoot game)
    {
        if (_activeGameSession != null)
        {
            EndGame();
        }

        ValidateGameContent(game);
        ReconcileStaticContent(game);
        GameSession session = new GameSession(game, _contentPack.GameData);
        GameManager clock = new GameManager(() => session.Game, session.Tick);
        _activeGameSession = session;
        _gameManager = clock;
        _activeGameSession.Tick.TickCompleted += HandleTickCompleted;
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

        _activeGameSession.Tick.TickCompleted -= HandleTickCompleted;
        _gameManager.SetGameSpeed(TickSpeed.Paused);
        _activeGameSession.Dispose();
        _activeGameSession = null;
        _gameManager = null;
    }

    /// <summary>
    /// Quick save the current game.
    /// </summary>
    /// <returns>True when the quicksave was written.</returns>
    public bool QuickSave()
    {
        if (!CanSave)
        {
            LogUnsettledSaveWarning();
            return false;
        }

        _saveGameManager.SaveQuickGameData(GetActiveGame());
        GameLogger.Log(
            $"Quick save completed: {_saveGameManager.GetSaveFilePath(SaveGameManager.QuickSaveFileName)}"
        );
        return true;
    }

    /// <summary>
    /// Saves the active game when its simulation state is settled.
    /// </summary>
    /// <param name="fileName">The save file name without its extension.</param>
    /// <param name="displayName">The display name stored with the save.</param>
    /// <returns>True when the save was written; otherwise false.</returns>
    public bool SaveGame(string fileName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        if (!CanSave)
        {
            LogUnsettledSaveWarning();
            return false;
        }

        _saveGameManager.SaveGameData(GetActiveGame(), fileName, displayName);
        return true;
    }

    /// <summary>
    /// Quick load a game.
    /// Reloads the quick save into the active session.
    /// </summary>
    public void QuickLoad()
    {
        if (LoadGame(SaveGameManager.QuickSaveFileName))
            Debug.Log("Quick load completed.");
        else
            Debug.LogWarning("Quick load skipped because no quick save exists.");
    }

    /// <summary>
    /// Forwards completed simulation ticks to the save manager's autosave scheduler.
    /// </summary>
    private void HandleTickCompleted()
    {
        if (!CanSave)
            return;

        _saveGameManager.ProcessAutosaveTick(GetActiveGame(), _getGameplaySettings?.Invoke());
    }

    /// <summary>
    /// Logs that a requested save cannot capture an unsettled simulation tick.
    /// </summary>
    private static void LogUnsettledSaveWarning()
    {
        GameLogger.Warning("Save skipped because the active game is not at a stable boundary.");
    }

    /// <summary>
    /// Loads a save file into the active game session.
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

        if (!HasActiveGame)
            return false;

        HotReloadGame(fileName);

        return true;
    }

    /// <summary>
    /// Loads the save into the current active game session without reloading the scene.
    /// </summary>
    /// <param name="fileName">The save file name to load.</param>
    private void HotReloadGame(string fileName)
    {
        GameRoot loadedGame = _saveGameManager.LoadGameData(fileName);
        ValidateGameContent(loadedGame);
        ReconcileStaticContent(loadedGame);
        _activeGameSession.ReplaceGame(loadedGame);
        _gameManager.Reset();
        GameReplaced?.Invoke(_activeGameSession.Game);
        _activeGameSession.Tick.ReconcileLoadedState();
    }

    /// <summary>
    /// Replaces the active graph with an authoritative multiplayer snapshot while preserving the
    /// scene's stable session and presentation subscriptions.
    /// </summary>
    /// <param name="game">The validated authoritative game state.</param>
    internal void ReplaceMultiplayerGame(GameRoot game)
    {
        if (_activeGameSession == null)
            throw new InvalidOperationException("No active game can receive a multiplayer state.");

        ValidateGameContent(game);
        ReconcileStaticContent(game);
        _activeGameSession.ReplaceGame(game);
        _gameManager.Reset();
        GameReplaced?.Invoke(_activeGameSession.Game);
        _activeGameSession.Tick.ReconcileLoadedState();
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
            string requiredMods = FormatModIdentity(summary.ModIDs, summary.ModVersions);
            string activeMods = FormatModIdentity(
                _contentPack.FileResolver.Mods.Select(mod => mod.ID).ToArray(),
                _contentPack.FileResolver.Mods.Select(mod => mod.Version).ToArray()
            );
            throw new InvalidOperationException(
                $"Save requires content pack '{summary.PackID}' version '{summary.PackVersion}' "
                    + $"scenario '{summary.ScenarioID}' with mods [{requiredMods}], but "
                    + $"'{_contentPack.Definition.ID}' version '{_contentPack.Definition.Version}' "
                    + $"scenario '{_contentPack.Scenario.ID}' with mods [{activeMods}] is active."
            );
        }
    }

    /// <summary>
    /// Restores immutable authored state from the active content pack.
    /// </summary>
    /// <param name="game">The game whose authored state will be restored.</param>
    private void ReconcileStaticContent(GameRoot game)
    {
        ReconcileFactionSettings(game);
        ReconcileGalaxyLayout(game);
    }

    /// <summary>
    /// Restores faction configuration from saved launch rules or the active content pack.
    /// </summary>
    /// <param name="game">The game whose faction settings will be restored.</param>
    private void ReconcileFactionSettings(GameRoot game)
    {
        Dictionary<string, FactionSettings> overrides = GetFactionSettingOverrides(game);
        foreach (Faction faction in game.GetFactions())
        {
            if (faction == null || string.IsNullOrWhiteSpace(faction.InstanceID))
                throw new InvalidOperationException(
                    "Loaded game contains an unidentified faction."
                );

            Faction template = _contentPack.GameData.Factions.FirstOrDefault(candidate =>
                candidate?.InstanceID == faction.InstanceID
            );
            if (template == null)
                throw new InvalidOperationException(
                    $"Loaded faction '{faction.InstanceID}' is missing from the active content pack."
                );

            FactionSettings settings = overrides.TryGetValue(
                faction.InstanceID,
                out FactionSettings factionOverride
            )
                ? factionOverride
                : template.Settings;
            faction.Settings = settings.GetDeepCopy();
        }
    }

    /// <summary>
    /// Reads and validates launch-specific faction settings stored with the game.
    /// </summary>
    /// <param name="game">The game whose launch configuration will be read.</param>
    /// <returns>Faction settings indexed by faction instance ID.</returns>
    private Dictionary<string, FactionSettings> GetFactionSettingOverrides(GameRoot game)
    {
        Dictionary<string, FactionSettings> overrides = new Dictionary<string, FactionSettings>(
            StringComparer.Ordinal
        );
        GameGenerationConfig config = game.Summary.CustomSettings?.GenerationConfig;
        if (config == null)
            return overrides;

        foreach (
            FactionModifierConfig modifier in config.FactionModifiers
                ?? Enumerable.Empty<FactionModifierConfig>()
        )
        {
            if (modifier == null || string.IsNullOrWhiteSpace(modifier.FactionID))
                throw new InvalidOperationException(
                    "Game generation config contains an unidentified faction modifier."
                );
            if (modifier.Settings == null)
                throw new InvalidOperationException(
                    $"Faction modifier '{modifier.FactionID}' has no settings."
                );
            if (
                !_contentPack.GameData.Factions.Any(faction =>
                    faction?.InstanceID == modifier.FactionID
                )
            )
                throw new InvalidOperationException(
                    $"Faction modifier '{modifier.FactionID}' is missing from the active content pack."
                );
            if (overrides.ContainsKey(modifier.FactionID))
                throw new InvalidOperationException(
                    $"Game generation config contains duplicate faction modifier '{modifier.FactionID}'."
                );

            overrides.Add(modifier.FactionID, modifier.Settings);
        }

        return overrides;
    }

    /// <summary>
    /// Restores authored sector and planet coordinates from the active content pack.
    /// </summary>
    /// <param name="game">The game whose galaxy layout will be restored.</param>
    private void ReconcileGalaxyLayout(GameRoot game)
    {
        foreach (PlanetSector sector in game.GetGalaxyMap().GetChildren<PlanetSector>())
        {
            PlanetSector template = _contentPack.GameData.PlanetSectors.FirstOrDefault(candidate =>
                candidate?.TypeID == sector.TypeID
            );
            if (template == null)
                continue;

            sector.PositionX = template.PositionX;
            sector.PositionY = template.PositionY;
            foreach (Planet planet in sector.GetChildren<Planet>())
            {
                Planet planetTemplate = template
                    .GetChildren<Planet>()
                    .FirstOrDefault(candidate => candidate?.InstanceID == planet.InstanceID);
                if (planetTemplate == null)
                    continue;

                planet.PositionX = planetTemplate.PositionX;
                planet.PositionY = planetTemplate.PositionY;
            }
        }
    }

    /// <summary>
    /// Formats ordered mod identifiers and versions for a compatibility error.
    /// </summary>
    /// <param name="modIDs">The ordered mod identifiers.</param>
    /// <param name="modVersions">The versions corresponding to the identifiers.</param>
    /// <returns>A comma-separated ordered mod identity.</returns>
    private static string FormatModIdentity(string[] modIDs, string[] modVersions)
    {
        string[] ids = modIDs ?? Array.Empty<string>();
        string[] versions = modVersions ?? Array.Empty<string>();
        return string.Join(
            ", ",
            ids.Select((id, index) => $"{id}@{(index < versions.Length ? versions[index] : "?")}")
        );
    }
}
