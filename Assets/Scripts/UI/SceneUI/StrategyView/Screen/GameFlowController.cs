using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Generation;
using Rebellion.Simulation;
using Rebellion.Util.Logging;
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
    private GameSession activeSession;
    private GameRoot game;
    private FactionThemeLibrary themeLibrary;
    private UIContext uiContext;
    private bool campaignEnding;
    private bool cutscenePlaying;
    private bool finishCampaignAfterCutscenes;
    private IEnumerator activeTick;
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
            AppBootstrap bootstrap = AppBootstrap.EnsureExists();
            await bootstrap.InitializeMainMenuContentAsync();
            await bootstrap.InitializeStrategyContentAsync();
            ContentPack contentPack = bootstrap.GetContentPack();
            themeLibrary = new FactionThemeLibrary(contentPack.GameData.FactionThemes);
            GameRuntime runtime = bootstrap.GetRuntime();
            if (runtime?.HasActiveGame == true)
            {
                GameSession session = runtime.GetActiveGameSession();
                InitializeStrategy(session);
                ActivateGameplay(session, false);
                return;
            }

            if (GameLaunchContext.IsLoadGame)
            {
                LoadGame();
                GameSession session = StartGameSession(loadedGame: true);
                InitializeStrategy(session);
                ActivateGameplay(session, false);
            }
            else
                await StartNewGameAsync();
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
        if (activeTick != null)
        {
            AdvanceActiveTick();
            return;
        }

        if (activeGameManager?.TryAdvanceTickTimer(Time.deltaTime) == true)
        {
            activeTick = activeSession.Tick.ProcessTickIncrementally();
            AdvanceActiveTick();
        }
    }

    /// <summary>
    /// Detaches session callbacks when the strategy scene is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        DisposeActiveTick();

        if (activeGameManager != null)
        {
            activeSession.Pipeline.HeadquartersLost -= HandleHeadquartersLost;
            activeSession.Pipeline.VictoryDeclared -= HandleVictoryDeclared;
        }
    }

    /// <summary>
    /// Advances the current game tick by one scheduled step.
    /// </summary>
    private void AdvanceActiveTick()
    {
        if (activeTick?.MoveNext() != false)
            return;

        DisposeActiveTick();
    }

    /// <summary>
    /// Disposes the current tick enumerator and clears it.
    /// </summary>
    private void DisposeActiveTick()
    {
        (activeTick as IDisposable)?.Dispose();
        activeTick = null;
    }

    /// <summary>
    /// Builds a new game and starts its configured faction introduction.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
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
        game = builder.Build();
        bool briefingsDisabled = AppBootstrap
            .Instance.GetUserSettingsManager()
            .Settings.Gameplay.DisableBriefings;
        bool playBriefing = GameLaunchContext.PlayIntroCutscene && !briefingsDisabled;
        Task intro = PlayFactionIntroAsync(game.GetPlayerFaction());
        GameSession session = StartGameSession(loadedGame: false);
        InitializeStrategy(session);
        Task briefingReady = playBriefing
            ? strategyController.PrepareBriefingAsync()
            : Task.CompletedTask;
        await Task.WhenAll(intro, briefingReady);
        ActivateGameplay(session, playBriefing);
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
        AppBootstrap.Instance.GetRuntime().ValidateGameContent(game);
    }

    /// <summary>
    /// Plays the configured faction introduction before entering gameplay.
    /// </summary>
    /// <param name="faction">The player faction.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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
        AppBootstrap
            .EnsureExists()
            .GetCutsceneManager()
            .Play(theme.IntroCutscenePath, () => completion.TrySetResult(true));
        return completion.Task;
    }

    /// <summary>
    /// Starts the built game in the active runtime.
    /// </summary>
    /// <param name="loadedGame">Whether the game was restored from persisted state.</param>
    /// <returns>The active game session.</returns>
    private GameSession StartGameSession(bool loadedGame)
    {
        AppBootstrap bootstrap = AppBootstrap.EnsureExists();
        GameRuntime runtime = bootstrap.GetRuntime();
        return loadedGame ? runtime.StartLoadedGame(game) : runtime.StartGame(game);
    }

    /// <summary>
    /// Composes strategy UI for an active game session without revealing it or starting music.
    /// </summary>
    /// <param name="session">The active game session.</param>
    private void InitializeStrategy(GameSession session)
    {
        AppBootstrap bootstrap = AppBootstrap.Instance;
        ContentPack contentPack = bootstrap.GetContentPack();
        GameDataCatalog gameData = contentPack.GameData;
        EncyclopediaCatalog encyclopediaCatalog = new EncyclopediaCatalogBuilder().Build(
            gameData.EncyclopediaEntries,
            gameData.PlanetSectors,
            gameData.Buildings,
            gameData.CapitalShips,
            gameData.Starfighters,
            gameData.Regiments,
            gameData.SpecialForces,
            gameData.Officers
        );
        uiContext = new UIContext(
            session.Game,
            themeLibrary,
            encyclopediaCatalog,
            bootstrap.GetContentAssets().GetTexture
        );

        if (activeGameManager != null)
        {
            activeSession.Pipeline.HeadquartersLost -= HandleHeadquartersLost;
            activeSession.Pipeline.VictoryDeclared -= HandleVictoryDeclared;
        }
        session.Pipeline.HeadquartersLost += HandleHeadquartersLost;
        session.Pipeline.VictoryDeclared += HandleVictoryDeclared;

        strategyController.Initialize(
            session,
            bootstrap.GetRuntime().GetActiveGameManager(),
            bootstrap.GetRuntime(),
            uiContext
        );
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
    /// <param name="session">The active game session.</param>
    /// <param name="requestBriefing">Whether launch state requested the opening briefing.</param>
    private void ActivateGameplay(GameSession session, bool requestBriefing)
    {
        strategyController.ActivatePresentation();
        GameManager clock = AppBootstrap.Instance.GetRuntime().GetActiveGameManager();
        GameRoot activeGame = session.Game;
        GameMetadata metadata = activeGame.Metadata ??= new GameMetadata();
        bool briefingsDisabled = AppBootstrap
            .Instance.GetUserSettingsManager()
            .Settings.Gameplay.DisableBriefings;
        bool playBriefing = ShouldPlayOpeningBriefing(
            requestBriefing,
            metadata.OpeningBriefingCompleted,
            briefingsDisabled
        );
        GameLaunchContext.PlayIntroCutscene = false;
        if (playBriefing)
        {
            strategyController.PlayBriefing(() =>
            {
                metadata.OpeningBriefingCompleted = true;
                activeSession = session;
                activeGameManager = clock;
            });
        }
        else
        {
            activeSession = session;
            activeGameManager = clock;
        }
    }

    /// <summary>
    /// Determines whether the requested opening briefing should play.
    /// </summary>
    /// <param name="requested">Whether launch state requested the briefing.</param>
    /// <param name="completed">Whether the briefing has already completed for this game.</param>
    /// <param name="disabled">Whether the user disabled briefings.</param>
    /// <returns>True when the opening briefing should play.</returns>
    internal static bool ShouldPlayOpeningBriefing(bool requested, bool completed, bool disabled)
    {
        return requested && !completed && !disabled;
    }

    /// <summary>
    /// Pauses the completed campaign and plays the configured ending for the player's outcome.
    /// </summary>
    /// <param name="result">The terminal victory result.</param>
    private void HandleVictoryDeclared(VictoryResult result)
    {
        if (campaignEnding || result == null)
            return;

        Faction playerFaction = activeSession?.Game.GetPlayerFaction();
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
    /// <param name="themes">The themes.</param>
    /// <param name="result">The result.</param>
    /// <returns>The requested headquarters destroyed cutscene path.</returns>
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
    /// <param name="theme">The theme.</param>
    /// <param name="playerFaction">The player faction.</param>
    /// <param name="result">The result.</param>
    /// <returns>The requested campaign ending cutscene path.</returns>
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
