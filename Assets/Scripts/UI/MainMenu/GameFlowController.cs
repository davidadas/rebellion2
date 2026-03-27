using System;
using Rebellion.Game;
using Rebellion.Generation;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Orchestrates high-level game startup flow within the Strategy scene.
/// This component consumes launch parameters from <see cref="GameLaunchContext"/>
/// and transitions into active gameplay.
/// </summary>
public sealed class GameFlowController : MonoBehaviour
{
    private GameRoot game;
    private FactionThemeLibrary themeLibrary;

    /// <summary>
    /// Entry point for scene initialization.
    /// Determines whether to start a new game or load an existing one.
    /// </summary>
    private void Start()
    {
        themeLibrary = new FactionThemeLibrary(ResourceManager.Instance);

        if (GameLaunchContext.IsLoadGame)
        {
            LoadGame();
        }
        else
        {
            StartNewGame();
        }
    }

    /// <summary>
    /// Builds a new <see cref="Game"/> instance using the launch summary,
    /// then begins the faction intro sequence if applicable.
    /// </summary>
    private void StartNewGame()
    {
        GameSummary summary = GameLaunchContext.Summary;

        if (summary == null)
        {
            throw new GameException("GameLaunchContext.Summary is null. Cannot start new game.");
        }

        GameBuilder builder = new GameBuilder(summary);
        game = builder.BuildGame();

        Faction playerFaction = game.GetPlayerFaction();

        PlayFactionIntro(playerFaction);

        summary.IsNewGame = false;
    }

    /// <summary>
    /// Loads a saved game from disk and enters gameplay immediately.
    /// </summary>
    private void LoadGame()
    {
        string fileName = GameLaunchContext.SaveFileName;

        if (string.IsNullOrEmpty(fileName))
        {
            throw new GameException("LoadGame called but SaveFileName is null.");
        }

        game = SaveGameManager.Instance.LoadGameData(fileName);

        EnterGameplay();
    }

    /// <summary>
    /// Plays the introductory cutscene for the specified faction, if defined.
    /// If no intro cutscene exists, gameplay begins immediately.
    /// </summary>
    /// <param name="faction">The player's faction.</param>
    private void PlayFactionIntro(Faction faction)
    {
        if (faction == null)
        {
            throw new GameException("Player faction is null.");
        }

        FactionTheme theme = themeLibrary.GetTheme(faction.InstanceID);

        if (string.IsNullOrEmpty(theme.IntroCutscenePath))
        {
            EnterGameplay();
            return;
        }

        VideoClip clip = ResourceManager.Instance.GetVideo(theme.IntroCutscenePath);

        CutsceneManager.Instance.Play(clip, EnterGameplay);
    }

    /// <summary>
    /// Finalizes initialization and transitions into active gameplay.
    /// Creates the <see cref="GameManager"/>, constructs the <see cref="UIContext"/>,
    /// and initializes the <see cref="StrategyController"/>.
    /// </summary>
    private void EnterGameplay()
    {
        StrategyController strategy = FindFirstObjectByType<StrategyController>();

        if (strategy == null)
        {
            throw new GameException("StrategyController not found in Strategy scene.");
        }

        // Get runtime from AppBootstrap (creates if missing for scene testing)
        AppBootstrap bootstrap = AppBootstrap.EnsureExists();
        GameRuntime runtime = bootstrap.GetRuntime();
        GameManager gameManager = runtime.StartGame(game);

        FactionThemeLibrary themeLibrary = new FactionThemeLibrary(ResourceManager.Instance);

        UIContext uiContext = new UIContext(gameManager.GetGame(), themeLibrary);

        strategy.Initialize(gameManager, uiContext);
    }
}
