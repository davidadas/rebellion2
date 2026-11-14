using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Gameplay;
using Rebellion.Generation;
using Rebellion.Multiplayer;
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
    private PlanetDestructionCutsceneController planetDestructionCutsceneController;
    private bool campaignEnding;
    private bool cutscenePlaying;
    private bool finishCampaignAfterCutscenes;
    private bool campaignPresentationCompleted;
    private Task<bool> campaignCompletion;
    private IEnumerator activeTick;
    private CancellationTokenSource presentationCancellation;
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
        CancellationToken cancellationToken = default;
        try
        {
            AppBootstrap bootstrap = AppBootstrap.EnsureExists();
            presentationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                destroyCancellationToken,
                bootstrap.destroyCancellationToken
            );
            cancellationToken = presentationCancellation.Token;
            await bootstrap.InitializeMainMenuContentAsync();
            if (!CanContinueStartup(bootstrap))
                return;

            await bootstrap.InitializeStrategyContentAsync();
            if (!CanContinueStartup(bootstrap))
                return;

            ContentPack contentPack = bootstrap.GetContentPack();
            themeLibrary = new FactionThemeLibrary(contentPack.GameData.FactionThemes);
            GameSessionManager runtime = bootstrap.GetGameSessionManager();
            if (runtime?.HasActiveGame == true)
            {
                GameSession session = runtime.GetActiveGameSession();
                InitializeStrategy(session, cancellationToken);
                await strategyController.PrepareInitialContentAsync();
                if (!CanContinueStartup(bootstrap))
                    return;

                await ActivateGameplayAsync(session, false);
                return;
            }

            if (GameLaunchContext.IsLoadGame)
            {
                await LoadGameAsync(runtime);
                if (!CanContinueStartup(bootstrap))
                    return;

                GameSession session = StartGameSession(loadedGame: true);
                InitializeStrategy(session, cancellationToken);
                await strategyController.PrepareInitialContentAsync();
                if (!CanContinueStartup(bootstrap))
                    return;

                await ActivateGameplayAsync(session, false);
            }
            else
                await StartNewGameAsync(cancellationToken);
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested
                && exception.CancellationToken == cancellationToken
            ) { }
        catch (Exception exception)
        {
            if (!Application.isPlaying || this == null)
                return;

            if (!LoadingScreenController.RecoverFromLoadFailure(exception))
                Debug.LogException(exception);
        }
    }

    /// <summary>
    /// Determines whether an asynchronous startup continuation still belongs to the active scene
    /// and application services.
    /// </summary>
    /// <param name="bootstrap">The bootstrap captured before the asynchronous operation.</param>
    /// <returns>True when startup may safely continue.</returns>
    private bool CanContinueStartup(AppBootstrap bootstrap)
    {
        return Application.isPlaying
            && this != null
            && bootstrap != null
            && AppBootstrap.Instance == bootstrap;
    }

    /// <summary>
    /// Advances the active game session from the strategy scene's Unity frame loop.
    /// </summary>
    private void Update()
    {
        if (
            activeSession == null
            || AppBootstrap.Instance?.GetGameSessionManager()?.GetActiveGameSession()
                != activeSession
        )
        {
            DisposeActiveTick();
            return;
        }

        if (activeTick != null)
        {
            AdvanceActiveTick();
            return;
        }

        GameSessionManager runtime = AppBootstrap.Instance.GetGameSessionManager();
        CapturePendingAutosave(runtime);
        activeSession.PumpCommands();
        if (activeSession.CommandFailure != null)
        {
            if (MultiplayerCoordinator.Instance?.IsMultiplayerGame == true)
                MultiplayerCoordinator.Instance.ReportGameplayFailure(activeSession.CommandFailure);
            else
                activeGameManager.SetGameSpeed(TickSpeed.Paused);
            return;
        }

        if (campaignEnding)
        {
            if (
                campaignPresentationCompleted
                && activeSession.CanCaptureSettledState
                && activeSession.CanExecuteCommands
            )
            {
                campaignCompletion ??= runtime.WaitForCampaignCompletionAsync();
                if (campaignCompletion.IsCompletedSuccessfully && campaignCompletion.Result)
                    FinishCampaign();
            }
            return;
        }

        bool beginTick = runtime.TryBeginGameTick(Time.deltaTime);
        if (beginTick)
        {
            activeTick = activeSession.Tick.ProcessTickIncrementally();
            AdvanceActiveTick();
        }
    }

    /// <summary>
    /// Cancels scene-owned presentation work and detaches session callbacks.
    /// </summary>
    private void OnDestroy()
    {
        presentationCancellation?.Cancel();
        presentationCancellation?.Dispose();
        DisposeActiveTick();
        planetDestructionCutsceneController?.Dispose();

        if (activeGameManager != null)
        {
            activeSession.Pipeline.HeadquartersLost -= HandleHeadquartersLost;
            activeSession.Pipeline.VictoryDeclared -= HandleVictoryDeclared;
        }
        MultiplayerCoordinator multiplayer = MultiplayerCoordinator.Instance;
        if (multiplayer?.IsMultiplayerGame == true)
        {
            if (multiplayer.IsTransferringScene)
                multiplayer.DetachGameForSceneTransfer(
                    AppBootstrap.Instance?.GetGameSessionManager()
                );
            else
                multiplayer.Disconnect();
        }
    }

    /// <summary>
    /// Captures a scheduled autosave once its indicator has been presented, and reports unexpected
    /// failures from its background publication.
    /// </summary>
    /// <param name="runtime">The runtime that owns the scheduled autosave.</param>
    private static async void CapturePendingAutosave(GameSessionManager runtime)
    {
        try
        {
            await runtime.ProcessPendingAutosaveAsync();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    /// <summary>
    /// Advances the current game tick by one scheduled step.
    /// </summary>
    private void AdvanceActiveTick()
    {
        try
        {
            if (
                activeGameManager != null
                    ? activeGameManager.AdvanceTick(activeTick)
                    : activeTick?.MoveNext() != false
            )
                return;

            DisposeActiveTick();
        }
        catch (Exception exception)
        {
            DisposeActiveTick();
            if (MultiplayerCoordinator.Instance?.IsMultiplayerGame == true)
                MultiplayerCoordinator.Instance.ReportGameplayFailure(exception);
            else
                throw;
        }
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
    /// <param name="cancellationToken">The scene-owned presentation lifetime.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task StartNewGameAsync(CancellationToken cancellationToken)
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
        GameRoot restoredGame = GameLaunchContext.IsMultiplayer
            ? AppBootstrap.EnsureExists().GetMultiplayerCoordinator().RestoredGame
            : null;
        game = restoredGame ?? builder.Build();
        bool briefingsDisabled = AppBootstrap
            .Instance.GetUserSettingsManager()
            .Settings.Gameplay.DisableBriefings;
        bool playBriefing = GameLaunchContext.PlayIntroCutscene && !briefingsDisabled;
        bool deferIntro =
            GameLaunchContext.PlayIntroCutscene && LoadingScreenController.IsLoadInProgress;
        GameSession session = StartGameSession(loadedGame: restoredGame != null);
        Task intro = deferIntro
            ? Task.CompletedTask
            : PlayFactionIntroAsync(game.GetPlayerFaction());
        InitializeStrategy(session, cancellationToken);
        Task briefingReady = playBriefing
            ? strategyController.PrepareBriefingAsync()
            : Task.CompletedTask;
        Task initialContentReady = strategyController.PrepareInitialContentAsync();
        AppBootstrap bootstrap = AppBootstrap.Instance;
        await Task.WhenAll(intro, briefingReady, initialContentReady);
        if (!CanContinueStartup(bootstrap))
            return;

        await ActivateGameplayAsync(session, playBriefing, deferIntro);
    }

    /// <summary>
    /// Loads and validates the requested save file.
    /// </summary>
    /// <param name="runtime">The runtime that owns the incoming game session.</param>
    /// <returns>A task representing asynchronous save deserialization.</returns>
    private async Task LoadGameAsync(GameSessionManager runtime)
    {
        string fileName = GameLaunchContext.SaveFileName;

        if (string.IsNullOrEmpty(fileName))
            throw new InvalidOperationException("LoadGame called but SaveFileName is null.");
        if (runtime == null)
            throw new InvalidOperationException(
                "The application runtime is unavailable before save deserialization."
            );

        game = await SaveGameManager.Instance.LoadGameDataAsync(fileName);
    }

    /// <summary>
    /// Plays the configured faction introduction before entering gameplay.
    /// </summary>
    /// <param name="faction">The player faction.</param>
    /// <returns>A task representing completion or cancellation of the introduction.</returns>
    private Task PlayFactionIntroAsync(Faction faction)
    {
        if (faction == null)
            throw new InvalidOperationException("Player faction is null.");

        if (!GameLaunchContext.PlayIntroCutscene)
            return Task.CompletedTask;

        CancellationToken cancellationToken = presentationCancellation.Token;
        cancellationToken.ThrowIfCancellationRequested();
        TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
        PlayFactionIntro(faction, () => completion.TrySetResult(true));
        return WaitForIntroCompletionAsync(completion, cancellationToken);
    }

    /// <summary>
    /// Waits for the introduction callback and releases its cancellation registration afterward.
    /// </summary>
    /// <param name="completion">The completion supplied to the introduction callback.</param>
    /// <param name="cancellationToken">The scene-owned presentation lifetime.</param>
    /// <returns>A task representing completion or cancellation of the introduction wait.</returns>
    private static async Task WaitForIntroCompletionAsync(
        TaskCompletionSource<bool> completion,
        CancellationToken cancellationToken
    )
    {
        using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            completion.TrySetCanceled(cancellationToken)
        );
        await completion.Task;
    }

    /// <summary>
    /// Plays an introduction after the loading overlay has released the screen, when deferred.
    /// </summary>
    /// <param name="faction">The faction whose introduction should play.</param>
    /// <param name="completed">The gameplay activation to resume after playback.</param>
    private void PlayFactionIntro(Faction faction, Action completed)
    {
        FactionTheme theme = themeLibrary.GetTheme(faction.InstanceID);
        if (string.IsNullOrEmpty(theme.IntroCutscenePath))
        {
            completed();
            return;
        }

        AppBootstrap.EnsureExists().GetCutsceneManager().Play(theme.IntroCutscenePath, completed);
    }

    /// <summary>
    /// Starts the built game in the active runtime.
    /// </summary>
    /// <param name="loadedGame">Whether the game was restored from persisted state.</param>
    /// <returns>The active game session.</returns>
    private GameSession StartGameSession(bool loadedGame)
    {
        AppBootstrap bootstrap = AppBootstrap.EnsureExists();
        GameSessionManager runtime = bootstrap.GetGameSessionManager();
        MultiplayerCoordinator multiplayer = GameLaunchContext.IsMultiplayer
            ? AppBootstrap.EnsureExists().GetMultiplayerCoordinator()
            : null;
        IGameSessionOwner owner = multiplayer;
        GameSession session = loadedGame
            ? runtime.StartLoadedGame(game, owner)
            : runtime.StartGame(game, owner);
        if (multiplayer?.IsRejoining != true)
            ApplyGameLaunchSpeed(
                runtime.GetActiveGameManager(),
                bootstrap.GetUserSettingsManager().Settings.Gameplay.GameLaunchSpeed
            );
        return session;
    }

    /// <summary>
    /// Applies a configured galaxy-view launch speed without replacing preserved game state when no
    /// override is selected.
    /// </summary>
    /// <param name="gameManager">The active game clock.</param>
    /// <param name="speed">The configured launch-speed behavior.</param>
    internal static void ApplyGameLaunchSpeed(GameManager gameManager, GameLaunchSpeed speed)
    {
        if (gameManager == null || speed == GameLaunchSpeed.Unchanged)
            return;

        TickSpeed tickSpeed = speed switch
        {
            GameLaunchSpeed.Paused => TickSpeed.Paused,
            GameLaunchSpeed.VerySlow => TickSpeed.VerySlow,
            GameLaunchSpeed.Slow => TickSpeed.Slow,
            GameLaunchSpeed.Medium => TickSpeed.Medium,
            GameLaunchSpeed.Fast => TickSpeed.Fast,
            _ => throw new ArgumentOutOfRangeException(nameof(speed), speed, null),
        };
        gameManager.ApplyGameSpeed(tickSpeed);
    }

    /// <summary>
    /// Composes strategy UI for an active game session without revealing it or starting music.
    /// </summary>
    /// <param name="session">The active game session.</param>
    /// <param name="cancellationToken">The scene-owned presentation lifetime.</param>
    private void InitializeStrategy(GameSession session, CancellationToken cancellationToken)
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
        ContentAssets contentAssets = bootstrap.GetContentAssets();
        uiContext = new UIContext(
            session.Game,
            themeLibrary,
            encyclopediaCatalog,
            contentAssets.GetTexture,
            contentAssets.GetTextureContentBounds,
            contentAssets.GetSmoothTexture
        );

        if (activeGameManager != null)
        {
            activeSession.Pipeline.HeadquartersLost -= HandleHeadquartersLost;
            activeSession.Pipeline.VictoryDeclared -= HandleVictoryDeclared;
        }
        session.Pipeline.HeadquartersLost += HandleHeadquartersLost;
        session.Pipeline.VictoryDeclared += HandleVictoryDeclared;
        planetDestructionCutsceneController?.Dispose();
        planetDestructionCutsceneController = new PlanetDestructionCutsceneController(
            session.Pipeline,
            themeLibrary,
            () => session.Game.GetPlayerFaction()?.InstanceID,
            EnqueueCutscene
        );

        strategyController.Initialize(
            session,
            bootstrap.GetGameSessionManager().GetActiveGameManager(),
            bootstrap.GetGameSessionManager(),
            uiContext,
            cancellationToken
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

        EnqueueCutscene(cutscenePath);
    }

    /// <summary>
    /// Adds an event movie to the ordered playback queue and starts playback when idle.
    /// </summary>
    /// <param name="cutscenePath">The configured movie content address.</param>
    private void EnqueueCutscene(string cutscenePath)
    {
        cutsceneQueue.Enqueue(cutscenePath);
        PlayNextQueuedCutscene();
    }

    /// <summary>
    /// Reveals strategy UI and optionally begins the prepared opening briefing.
    /// </summary>
    /// <param name="session">The active game session.</param>
    /// <param name="requestBriefing">Whether launch state requested the opening briefing.</param>
    /// <param name="deferIntro">Whether to play the faction introduction after the loading overlay exits.</param>
    /// <returns>A task that completes after gameplay is active.</returns>
    private async Task ActivateGameplayAsync(
        GameSession session,
        bool requestBriefing,
        bool deferIntro = false
    )
    {
        bool loadingTransitionActive = LoadingScreenController.IsLoadInProgress;
        AppBootstrap bootstrap = AppBootstrap.Instance;
        await strategyController.ActivatePresentationAsync(startMusic: !loadingTransitionActive);
        if (!CanContinueStartup(bootstrap))
            return;

        GameManager clock = bootstrap.GetGameSessionManager().GetActiveGameManager();
        GameRoot activeGame = session.Game;
        GameMetadata metadata = activeGame.Metadata ??= new GameMetadata();
        bool briefingsDisabled = bootstrap
            .GetUserSettingsManager()
            .Settings.Gameplay.DisableBriefings;
        bool playBriefing = ShouldPlayOpeningBriefing(
            requestBriefing,
            metadata.OpeningBriefingCompleted,
            briefingsDisabled
        );
        GameLaunchContext.PlayIntroCutscene = false;

        if (loadingTransitionActive)
        {
            LoadingScreenController.CompletePendingLoad(
                bootstrap,
                () =>
                {
                    Action activate = () =>
                    {
                        strategyController.ResumeMusic();
                        FinishGameplayActivation(session, clock, metadata, playBriefing);
                    };
                    if (deferIntro)
                        PlayFactionIntro(session.Game.GetPlayerFaction(), activate);
                    else
                        activate();
                },
                StrategyController.PlayStrategyReadySound,
                () => AudioManager.EnsureExists().PlaySfx(StrategyUISoundPaths.ControlPress)
            );
            return;
        }

        FinishGameplayActivation(session, clock, metadata, playBriefing);
    }

    /// <summary>
    /// Enables strategy ticking after any loading handoff and optional opening briefing completes.
    /// </summary>
    /// <param name="session">The prepared game session.</param>
    /// <param name="clock">The prepared game clock.</param>
    /// <param name="metadata">The active game's mutable metadata.</param>
    /// <param name="playBriefing">Whether the opening briefing should play first.</param>
    private void FinishGameplayActivation(
        GameSession session,
        GameManager clock,
        GameMetadata metadata,
        bool playBriefing
    )
    {
        if (playBriefing)
        {
            strategyController.PlayBriefing(() =>
            {
                metadata.OpeningBriefingCompleted = true;
                EnableSessionTicks(session, clock);
            });
        }
        else
        {
            EnableSessionTicks(session, clock);
        }
    }

    /// <summary>
    /// Marks presentation ready before advertising this session to the other multiplayer peer.
    /// </summary>
    /// <param name="session">The prepared simulation session.</param>
    /// <param name="clock">The prepared application clock.</param>
    private void EnableSessionTicks(GameSession session, GameManager clock)
    {
        if (!AppBootstrap.Instance.GetGameSessionManager().NotifyPresentationReady(session))
            return;
        activeSession = session;
        activeGameManager = clock;
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
                campaignPresentationCompleted = true;
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
        bootstrap.GetGameSessionManager()?.EndGame();
        MultiplayerCoordinator.Instance?.Disconnect();
        bootstrap.LoadScene("MainMenu");
    }
}
