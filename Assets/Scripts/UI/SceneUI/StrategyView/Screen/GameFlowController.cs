using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Generation;
using Rebellion.Util.Common;
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
    private GameRoot game;
    private FactionThemeLibrary themeLibrary;
    private UIContext uiContext;
    private bool campaignEnding;
    private bool cutscenePlaying;
    private bool finishCampaignAfterCutscenes;
    private readonly Queue<string> cutsceneQueue = new Queue<string>();

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
                GameManager gameManager = runtime.GetActiveGameManager();
                InitializeStrategy(gameManager);
                ActivateGameplay(gameManager, false);
                return;
            }

            if (GameLaunchContext.IsLoadGame)
            {
                LoadGame();
                GameManager gameManager = StartGameSession(loadedGame: true);
                InitializeStrategy(gameManager);
                ActivateGameplay(gameManager, false);
            }
            else
                await StartNewGameAsync();
        }
        catch (Exception exception)
        {
            GameStartupTrace.Complete("Game flow startup failed.");
            FatalErrorScreen.Show(exception, "Game loading", allowMainMenuReturn: true);
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
    /// Detaches session callbacks when the strategy scene is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (activeGameManager != null)
        {
            activeGameManager.HeadquartersLost -= HandleHeadquartersLost;
            activeGameManager.VictoryDeclared -= HandleVictoryDeclared;
        }
    }

    /// <summary>
    /// Builds a new game and starts its configured faction introduction.
    /// </summary>
    private async Task StartNewGameAsync()
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
        bool playBriefing = GameLaunchContext.PlayIntroCutscene;
        Task intro = PlayFactionIntroAsync(game.GetPlayerFaction());
        GameManager gameManager = StartGameSession(loadedGame: false);
        InitializeStrategy(gameManager);
        Task briefingReady = playBriefing
            ? strategyController.PrepareBriefingAsync()
            : Task.CompletedTask;
        GameStartupTrace.Log("Briefing owner preparation requested.");
        await Task.WhenAll(intro, briefingReady);
        GameStartupTrace.Log("Introduction and briefing preparation complete.");
        ActivateGameplay(gameManager, playBriefing);
    }

    /// <summary>
    /// Loads and validates the requested save file.
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
    }

    /// <summary>
    /// Plays the configured faction introduction before entering gameplay.
    /// </summary>
    /// <param name="faction">The player faction.</param>
    private Task PlayFactionIntroAsync(Faction faction)
    {
        if (faction == null)
            throw new InvalidOperationException("Player faction is null.");

        if (!GameLaunchContext.PlayIntroCutscene)
            return Task.CompletedTask;

        FactionTheme theme = themeLibrary.GetTheme(faction.InstanceID);
        if (string.IsNullOrEmpty(theme.IntroCutscenePath))
            return Task.CompletedTask;

        TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
        GameStartupTrace.Log($"Faction introduction starting: '{theme.IntroCutscenePath}'.");
        AppBootstrap
            .EnsureExists()
            .GetCutsceneManager()
            .Play(
                theme.IntroCutscenePath,
                () =>
                {
                    GameStartupTrace.Log("Faction introduction finished.");
                    completion.TrySetResult(true);
                }
            );
        return completion.Task;
    }

    /// <summary>
    /// Starts the built game in the active runtime.
    /// </summary>
    /// <param name="loadedGame">Whether the game was restored from persisted state.</param>
    /// <returns>The active game manager.</returns>
    private GameManager StartGameSession(bool loadedGame)
    {
        AppBootstrap bootstrap = AppBootstrap.EnsureExists();
        GameRuntime runtime = bootstrap.GetRuntime();
        GameStartupTrace.Log("Creating the active game session.");
        return loadedGame ? runtime.StartLoadedGame(game) : runtime.StartGame(game);
    }

    /// <summary>
    /// Composes strategy UI for an active game manager without revealing it or starting music.
    /// </summary>
    /// <param name="gameManager">The active game manager.</param>
    private void InitializeStrategy(GameManager gameManager)
    {
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

        if (activeGameManager != null)
        {
            activeGameManager.HeadquartersLost -= HandleHeadquartersLost;
            activeGameManager.VictoryDeclared -= HandleVictoryDeclared;
        }
        gameManager.HeadquartersLost += HandleHeadquartersLost;
        gameManager.VictoryDeclared += HandleVictoryDeclared;

        GameStartupTrace.Log("StrategyController initialization started.");
        strategyController.Initialize(gameManager, uiContext);
        GameStartupTrace.Log("StrategyController initialization complete.");
    }

    /// <summary>
    /// Queues the losing faction's headquarters movie when its headquarters is captured or destroyed.
    /// </summary>
    /// <param name="result">The headquarters loss that selected the movie.</param>
    private void HandleHeadquartersLost(HeadquartersLostResult result)
    {
        string cutscenePath = GetHeadquartersDestroyedCutscenePath(themeLibrary, result);
        if (string.IsNullOrWhiteSpace(cutscenePath))
            return;

        cutsceneQueue.Enqueue(cutscenePath);
        PlayNextQueuedCutscene();
    }

    /// <summary>
    /// Reveals strategy UI and optionally begins the prepared opening briefing.
    /// </summary>
    /// <param name="gameManager">The active game manager.</param>
    /// <param name="requestBriefing">Whether launch state requested the opening briefing.</param>
    private void ActivateGameplay(GameManager gameManager, bool requestBriefing)
    {
        strategyController.ActivatePresentation();
        GameRoot activeGame = gameManager.GetGame();
        GameMetadata metadata = activeGame.Metadata ??= new GameMetadata();
        bool playBriefing = requestBriefing && !metadata.OpeningBriefingCompleted;
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

    /// <summary>
    /// Pauses the completed campaign and plays the configured ending for the player's outcome.
    /// </summary>
    /// <param name="result">The terminal victory result.</param>
    private void HandleVictoryDeclared(VictoryResult result)
    {
        if (campaignEnding || result == null)
            return;

        Faction playerFaction = activeGameManager?.GetPlayerFaction();
        if (playerFaction == null)
            return;

        campaignEnding = true;
        activeGameManager.SetGameSpeed(TickSpeed.Paused);

        string cutscenePath = null;
        if (
            TryGetOptionalCutsceneTheme(
                themeLibrary,
                playerFaction.InstanceID,
                out FactionTheme theme
            )
        )
            cutscenePath = GetCampaignEndingCutscenePath(theme, playerFaction, result);
        if (!string.IsNullOrWhiteSpace(cutscenePath))
            cutsceneQueue.Enqueue(cutscenePath);
        finishCampaignAfterCutscenes = true;
        PlayNextQueuedCutscene();
    }

    /// <summary>
    /// Selects the headquarters movie from the faction that lost the headquarters.
    /// </summary>
    internal static string GetHeadquartersDestroyedCutscenePath(
        FactionThemeLibrary themes,
        HeadquartersLostResult result
    )
    {
        if (themes == null || result?.Defender == null)
            return null;

        return TryGetOptionalCutsceneTheme(
            themes,
            result.Defender.InstanceID,
            out FactionTheme theme
        )
            ? theme.HeadquartersDestroyedCutscenePath
            : null;
    }

    /// <summary>
    /// Resolves an optional cutscene theme without allowing missing presentation data to interrupt
    /// headquarters or campaign-completion result handling.
    /// </summary>
    /// <param name="themes">The available faction themes.</param>
    /// <param name="factionInstanceId">The faction whose optional movie is being selected.</param>
    /// <param name="theme">The resolved faction theme, when configured.</param>
    /// <returns>True when the faction has a configured theme; otherwise false.</returns>
    private static bool TryGetOptionalCutsceneTheme(
        FactionThemeLibrary themes,
        string factionInstanceId,
        out FactionTheme theme
    )
    {
        if (themes?.TryGetTheme(factionInstanceId, out theme) == true)
            return true;

        theme = null;
        GameLogger.Warning(
            $"Skipping optional campaign cutscene because faction '{factionInstanceId}' has no theme."
        );
        return false;
    }

    /// <summary>
    /// Selects the configured victory or defeat movie from the player's perspective.
    /// </summary>
    internal static string GetCampaignEndingCutscenePath(
        FactionTheme theme,
        Faction playerFaction,
        VictoryResult result
    )
    {
        if (theme == null || playerFaction == null || result == null)
            return null;

        return result.Winner?.InstanceID == playerFaction.InstanceID
            ? theme.VictoryCutscenePath
            : theme.DefeatCutscenePath;
    }

    /// <summary>
    /// Plays queued event and campaign-ending movies in their result order.
    /// </summary>
    private void PlayNextQueuedCutscene()
    {
        if (cutscenePlaying)
            return;

        if (cutsceneQueue.Count == 0)
        {
            if (finishCampaignAfterCutscenes)
                FinishCampaign();
            return;
        }

        string cutscenePath = cutsceneQueue.Dequeue();
        cutscenePlaying = true;
        try
        {
            AppBootstrap
                .EnsureExists()
                .GetCutsceneManager()
                .Play(cutscenePath, HandleQueuedCutsceneFinished);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            HandleQueuedCutsceneFinished();
        }
    }

    /// <summary>
    /// Advances the ordered event and campaign-ending movie queue.
    /// </summary>
    private void HandleQueuedCutsceneFinished()
    {
        cutscenePlaying = false;
        PlayNextQueuedCutscene();
    }

    /// <summary>
    /// Ends the completed session and returns to the main menu after the ending movie.
    /// </summary>
    private void FinishCampaign()
    {
        AppBootstrap bootstrap = AppBootstrap.EnsureExists();
        bootstrap.GetRuntime()?.EndGame();
        bootstrap.LoadScene("MainMenu");
    }
}
