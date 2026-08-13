using System;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Generation;
using UnityEngine;

/// <summary>
/// Consumes launch state and initializes the strategy scene for a new or loaded game.
/// </summary>
[RequireComponent(typeof(StrategyController))]
public sealed class GameFlowController : MonoBehaviour
{
    [SerializeField]
    private StrategyController strategyController;

    private GameManager activeGameManager;
    private Task briefingContentTask = Task.CompletedTask;
    private GameRoot game;
    private FactionThemeLibrary themeLibrary;
    private UIContext uiContext;

    /// <summary>
    /// Resolves composed scene dependencies.
    /// </summary>
    private void Awake()
    {
        if (strategyController == null)
        {
            throw new MissingReferenceException(
                $"{name} must be composed with a StrategyController."
            );
        }
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
    private async void Start()
    {
        try
        {
            GameStartupTrace.Log("GameFlowController started.");
            AppBootstrap bootstrap = AppBootstrap.EnsureExists();
            await bootstrap.InitializeMainMenuContentAsync();
            GameStartupTrace.Log("Main Menu content dependency complete.");
            await bootstrap.InitializeStrategyContentAsync();
            GameStartupTrace.Log("Strategy content dependency complete.");
            ContentPack contentPack = bootstrap.GetContentPack();
            themeLibrary = new FactionThemeLibrary(contentPack.GameData.FactionThemes);
            GameStartupTrace.Log("Faction themes composed.");
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
        catch (Exception exception)
        {
            GameStartupTrace.Complete("Game flow startup failed.");
            Debug.LogException(exception);
        }
    }

    /// <summary>
    /// Advances the active game session from the strategy scene's Unity frame loop.
    /// </summary>
    private void Update()
    {
        activeGameManager?.AdvanceTime(Time.deltaTime);
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

        ContentPack contentPack = AppBootstrap.Instance.GetContentPack();
        GameBuilder builder = new GameBuilder(summary, contentPack.GameData);
        GameStartupTrace.Log("Game generation started.");
        game = builder.Build();
        GameStartupTrace.Log("Game generation complete.");
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
        GameStartupTrace.Log($"Save '{fileName}' deserialized.");
        AppBootstrap.Instance.GetRuntime().ValidateGameContent(game);
        GameStartupTrace.Log("Loaded game content validated.");
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
        briefingContentTask = PreloadBriefingContentAsync(theme.StrategyBriefing);
        GameStartupTrace.Log("Faction briefing preload requested.");
        if (string.IsNullOrEmpty(theme.IntroCutscenePath))
        {
            EnterGameplay();
            return;
        }

        GameStartupTrace.Log($"Faction introduction starting: '{theme.IntroCutscenePath}'.");
        AppBootstrap
            .EnsureExists()
            .GetCutsceneManager()
            .Play(theme.IntroCutscenePath, HandleFactionIntroFinished);
    }

    /// <summary>
    /// Continues startup after the faction introduction finishes.
    /// </summary>
    private void HandleFactionIntroFinished()
    {
        GameStartupTrace.Log("Faction introduction finished.");
        EnterGameplay();
    }

    /// <summary>
    /// Starts the built game in the active runtime and initializes strategy UI.
    /// </summary>
    private void EnterGameplay()
    {
        AppBootstrap bootstrap = AppBootstrap.EnsureExists();
        GameRuntime runtime = bootstrap.GetRuntime();
        GameStartupTrace.Log("Creating the active game session.");
        EnterGameplay(runtime.StartGame(game));
    }

    /// <summary>
    /// Initializes strategy UI for an active game manager.
    /// </summary>
    /// <param name="gameManager">The active game manager.</param>
    private async void EnterGameplay(GameManager gameManager)
    {
        try
        {
            GameStartupTrace.Log("Waiting for faction briefing content.");
            await briefingContentTask;
            GameStartupTrace.Log("Faction briefing content complete.");
            AppBootstrap bootstrap = AppBootstrap.Instance;
            ContentPack contentPack = bootstrap.GetContentPack();
            GameStartupTrace.Log("Building encyclopedia catalog.");
            EncyclopediaCatalog encyclopediaCatalog = new EncyclopediaCatalogBuilder().Build(
                contentPack.GameData
            );
            GameStartupTrace.Log("Encyclopedia catalog complete; creating UI context.");
            uiContext = new UIContext(
                gameManager.GetGame(),
                themeLibrary,
                encyclopediaCatalog,
                bootstrap.GetContentAssets().GetTexture
            );

            GameStartupTrace.Log("StrategyController initialization started.");
            strategyController.Initialize(gameManager, uiContext);
            GameStartupTrace.Log("StrategyController initialization complete.");
            GameRoot activeGame = gameManager.GetGame();
            GameMetadata metadata = activeGame.Metadata ??= new GameMetadata();
            bool playBriefing =
                GameLaunchContext.PlayIntroCutscene && !metadata.OpeningBriefingCompleted;
            GameLaunchContext.PlayIntroCutscene = false;
            if (playBriefing)
            {
                strategyController.PlayBriefing(() =>
                {
                    metadata.OpeningBriefingCompleted = true;
                    activeGameManager = gameManager;
                });
            }
            else
                activeGameManager = gameManager;
            GameStartupTrace.Complete(
                playBriefing ? "Opening briefing started." : "Strategy gameplay ready."
            );
        }
        catch (Exception exception)
        {
            GameStartupTrace.Complete("Strategy presentation startup failed.");
            Debug.LogException(exception);
        }
    }

    /// <summary>
    /// Preloads only the active faction's briefing media while its introduction video plays.
    /// </summary>
    /// <param name="briefing">The active faction briefing, or null.</param>
    /// <returns>A task that completes when the briefing media is resident.</returns>
    private static Task PreloadBriefingContentAsync(StrategyBriefingTheme briefing)
    {
        return briefing == null
            ? Task.CompletedTask
            : AppBootstrap
                .Instance.GetContentAssets()
                .PreloadAsync(briefing.CreatePreloadManifest());
    }
}
