using System;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Generation;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Consumes launch state and initializes the strategy scene for a new or loaded game.
/// </summary>
[RequireComponent(typeof(StrategyController))]
public sealed class GameFlowController : MonoBehaviour
{
    [SerializeField]
    private StrategyController strategyController;

    private GameRoot game;
    private FactionThemeLibrary themeLibrary;

    /// <summary>
    /// Resolves composed scene dependencies and creates the shared theme library.
    /// </summary>
    private void Awake()
    {
        if (strategyController == null)
        {
            throw new MissingReferenceException(
                $"{name} must be composed with a StrategyController."
            );
        }

        themeLibrary = new FactionThemeLibrary();
    }

    /// <summary>
    /// Initializes the serialized strategy-controller reference when authoring the component.
    /// </summary>
    private void Reset()
    {
        strategyController = GetComponent<StrategyController>();
    }

    /// <summary>
    /// Starts or resumes gameplay according to the current launch state.
    /// </summary>
    private void Start()
    {
        AppBootstrap bootstrap = AppBootstrap.EnsureExists();
        GameRuntime runtime = bootstrap.GetRuntime();
        if (runtime?.HasActiveGame == true)
        {
            EnterGameplay(runtime.GetActiveGameManager());
            return;
        }

        if (GameLaunchContext.IsLoadGame)
            LoadGame();
        else
            StartNewGame();
    }

    /// <summary>
    /// Builds a new game and starts its configured faction introduction.
    /// </summary>
    private void StartNewGame()
    {
        GameSummary summary = GameLaunchContext.Summary;

        if (summary == null)
        {
            throw new InvalidOperationException(
                "GameLaunchContext.Summary is null. Cannot start new game."
            );
        }

        GameBuilder builder = new GameBuilder(summary);
        game = builder.Build();
        PlayFactionIntro(game.GetPlayerFaction());
    }

    /// <summary>
    /// Loads the requested save file and enters gameplay.
    /// </summary>
    private void LoadGame()
    {
        string fileName = GameLaunchContext.SaveFileName;

        if (string.IsNullOrEmpty(fileName))
            throw new InvalidOperationException("LoadGame called but SaveFileName is null.");

        game = SaveGameManager.Instance.LoadGameData(fileName);
        EnterGameplay();
    }

    /// <summary>
    /// Plays the configured faction introduction before entering gameplay.
    /// </summary>
    /// <param name="faction">The player faction.</param>
    private void PlayFactionIntro(Faction faction)
    {
        if (faction == null)
            throw new InvalidOperationException("Player faction is null.");

        if (!GameLaunchContext.PlayIntroCutscene)
        {
            EnterGameplay();
            return;
        }

        FactionTheme theme = themeLibrary.GetTheme(faction.InstanceID);
        if (string.IsNullOrEmpty(theme.IntroCutscenePath))
        {
            EnterGameplay();
            return;
        }

        VideoClip clip = ResourceManager.GetVideo(theme.IntroCutscenePath);
        CutsceneManager.Instance.Play(clip, EnterGameplay);
    }

    /// <summary>
    /// Starts the built game in the active runtime and initializes strategy UI.
    /// </summary>
    private void EnterGameplay()
    {
        AppBootstrap bootstrap = AppBootstrap.EnsureExists();
        GameRuntime runtime = bootstrap.GetRuntime();
        EnterGameplay(runtime.StartGame(game));
    }

    /// <summary>
    /// Initializes strategy UI for an active game manager.
    /// </summary>
    /// <param name="gameManager">The active game manager.</param>
    private void EnterGameplay(GameManager gameManager)
    {
        EncyclopediaCatalog encyclopediaCatalog = new EncyclopediaCatalogBuilder().Build();
        UIContext uiContext = new UIContext(
            gameManager.GetGame(),
            themeLibrary,
            encyclopediaCatalog
        );

        strategyController.Initialize(gameManager, uiContext);
    }
}
