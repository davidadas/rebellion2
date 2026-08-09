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
            AppBootstrap bootstrap = AppBootstrap.EnsureExists();
            await bootstrap.InitializeMainMenuContentAsync();
            await bootstrap.InitializeStrategyContentAsync();
            ContentPack contentPack = bootstrap.GetContentPack();
            themeLibrary = new FactionThemeLibrary(contentPack.GameData.FactionThemes);
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
        AppBootstrap.Instance.GetRuntime().ValidateGameContent(game);
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
        if (string.IsNullOrEmpty(theme.IntroCutscenePath))
        {
            EnterGameplay();
            return;
        }

        AppBootstrap
            .EnsureExists()
            .GetCutsceneManager()
            .Play(theme.IntroCutscenePath, EnterGameplay);
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
    private async void EnterGameplay(GameManager gameManager)
    {
        try
        {
            await briefingContentTask;
            AppBootstrap bootstrap = AppBootstrap.Instance;
            ContentPack contentPack = bootstrap.GetContentPack();
            EncyclopediaCatalog encyclopediaCatalog = new EncyclopediaCatalogBuilder().Build(
                contentPack.GameData
            );
            uiContext = new UIContext(
                gameManager.GetGame(),
                themeLibrary,
                encyclopediaCatalog,
                bootstrap.GetContentAssets().GetTexture
            );

            strategyController.Initialize(gameManager, uiContext);
            GameRoot activeGame = gameManager.GetGame();
            GameMetadata metadata = activeGame.Metadata ??= new GameMetadata();
            bool playBriefing = GameLaunchContext.PlayIntroCutscene && !metadata.BriefingCompleted;
            GameLaunchContext.PlayIntroCutscene = false;
            if (playBriefing)
            {
                strategyController.PlayBriefing(() =>
                {
                    metadata.BriefingCompleted = true;
                    activeGameManager = gameManager;
                });
            }
            else
                activeGameManager = gameManager;
        }
        catch (Exception exception)
        {
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
