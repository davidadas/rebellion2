using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Requests;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

/// <summary>
/// Coordinates all game systems each tick and routes results through domain reactions and observers.
/// </summary>
public sealed class GameManager
{
    // Game State.
    private GameRoot _game;
    private readonly GameDataCatalog _gameData;
    private IRandomNumberProvider _randomProvider;

    // Messaging and Events.
    private MessageSystem _messageSystem;
    private GameEventSystem _eventSystem;

    // Galaxy Systems.
    private FogOfWarSystem _fogOfWarSystem;
    private BlockadeSystem _blockadeSystem;

    // Unit Systems.
    private FleetSystem _fleetSystem;
    private PersonnelSystem _personnelSystem;
    private DuelSystem _duelSystem;
    private MovementSystem _movementSystem;
    private HeadquartersSystem _headquartersSystem;

    // Economy Systems.
    private ManufacturingSystem _manufacturingSystem;
    private MaintenanceSystem _maintenanceSystem;
    private ResourceProductionSystem _resourceProductionSystem;
    private FactionAutomationSystem _factionAutomationSystem;

    // Planetary Systems.
    private PlanetaryControlSystem _planetaryControlSystem;
    private UprisingSystem _uprisingSystem;

    // Mission Systems.
    private JediSystem _jediSystem;
    private MissionSystem _missionSystem;

    // Combat Systems.
    private SpaceCombatSystem _spaceCombatSystem;
    private BombardmentSystem _bombardmentSystem;
    private PlanetaryAssaultSystem _planetaryAssaultSystem;

    // Strategic Systems.
    private ResearchSystem _researchSystem;
    private OfficerLoyaltySystem _officerLoyaltySystem;
    private VictorySystem _victorySystem;
    private AISystem _aiSystem;

    // Result Processing.
    private GameResultProcessor _resultProcessor;
    private readonly List<GameResult> _deferredMessageResults = new List<GameResult>();

    // Tick State.
    private float? _tickInterval;
    private float _tickTimer;
    private bool _tickInProgress;

    // Game Events.
    public event Action GameSpeedChanged;
    public event Action TickCompleted;
    public event Action<GameRoot> GameReplaced;
    public event Action<HeadquartersLostResult> HeadquartersLost;
    public event Action<VictoryResult> VictoryDeclared;
    public event Action<MessageDeliveredResult> MessageDelivered;
    public event Action<BombardmentResult> BombardmentCompleted;

    /// <summary>
    /// Raised after planetary-assault results complete domain reaction processing.
    /// </summary>
    public event Action<IReadOnlyList<PlanetaryAssaultResult>> PlanetaryAssaultsResolved;

    /// <summary>
    /// Raised after victory results complete domain reaction processing.
    /// </summary>
    public event Action<IReadOnlyList<VictoryResult>> VictoriesResolved;

    // Exposed Game Systems.
    internal MessageSystem MessageSystem => _messageSystem;

    internal FleetSystem FleetSystem => _fleetSystem;

    internal PersonnelSystem PersonnelSystem => _personnelSystem;

    internal MovementSystem MovementSystem => _movementSystem;
    internal HeadquartersSystem HeadquartersSystem => _headquartersSystem;

    internal ManufacturingSystem ManufacturingSystem => _manufacturingSystem;

    internal MaintenanceSystem MaintenanceSystem => _maintenanceSystem;

    internal MissionSystem MissionSystem => _missionSystem;

    internal SpaceCombatSystem SpaceCombatSystem => _spaceCombatSystem;

    internal BombardmentSystem BombardmentSystem => _bombardmentSystem;

    internal PlanetaryAssaultSystem PlanetaryAssaultSystem => _planetaryAssaultSystem;

    /// <summary>
    /// Creates a new GameManager for the given game instance.
    /// </summary>
    /// <param name="game">The game instance to manage.</param>
    /// <param name="gameData">The active pack's composed game data.</param>
    public GameManager(GameRoot game, GameDataCatalog gameData)
    {
        _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
        InitializeGame(game);
    }

    /// <summary>
    /// Replaces the current game instance and reinitializes all systems.
    /// </summary>
    /// <param name="game">The replacement game instance.</param>
    public void ReplaceGame(GameRoot game)
    {
        InitializeGame(game);
        GameReplaced?.Invoke(_game);
    }

    /// <summary>
    /// Returns the current game instance.
    /// </summary>
    /// <returns>The active GameRoot.</returns>
    public GameRoot GetGame() => _game;

    /// <summary>
    /// Returns the current tick count.
    /// </summary>
    /// <returns>The current tick number.</returns>
    public int GetCurrentTick() => _game.CurrentTick;

    /// <summary>
    /// Returns the player-controlled faction.
    /// </summary>
    /// <returns>The faction whose PlayerID is set.</returns>
    public Faction GetPlayerFaction() => _game.GetPlayerFaction();

    /// <summary>
    /// Returns the fog of war system for building faction-specific galaxy views.
    /// </summary>
    /// <returns>The active FogOfWarSystem instance.</returns>
    public FogOfWarSystem GetFogOfWarSystem() => _fogOfWarSystem;

    /// <summary>
    /// Immediately applies the current advisor automation choices for one faction.
    /// </summary>
    /// <param name="faction">The faction whose delegated work should run.</param>
    public void ProcessFactionAutomation(Faction faction)
    {
        _factionAutomationSystem?.ProcessFaction(faction);
    }

    /// <summary>
    /// Returns the active game speed.
    /// </summary>
    /// <returns>The active game speed.</returns>
    public TickSpeed GetGameSpeed() => _game.GetGameSpeed();

    /// <summary>
    /// Sets the game speed and adjusts the tick interval accordingly.
    /// </summary>
    /// <param name="speed">The desired tick speed.</param>
    public void SetGameSpeed(TickSpeed speed)
    {
        TickSpeed previousSpeed = _game.GetGameSpeed();
        _game.SetGameSpeed(speed);

        switch (speed)
        {
            case TickSpeed.Fast:
                _tickInterval = _game.Config.GameSpeed.FastTickIntervalSeconds;
                break;
            case TickSpeed.Medium:
                _tickInterval = _game.Config.GameSpeed.MediumTickIntervalSeconds;
                break;
            case TickSpeed.Slow:
                _tickInterval = _game.Config.GameSpeed.SlowTickIntervalSeconds;
                break;
            case TickSpeed.VerySlow:
                _tickInterval = _game.Config.GameSpeed.VerySlowTickIntervalSeconds;
                break;
            case TickSpeed.Paused:
                _tickInterval = null;
                break;
        }

        if (previousSpeed != speed)
            GameSpeedChanged?.Invoke();
    }

    /// <summary>
    /// Advances the tick timer by elapsed game-loop time and fires a tick when the interval is reached.
    /// No-ops while combat is pending player resolution or the game is paused.
    /// </summary>
    /// <param name="elapsedSeconds">The elapsed game-loop time in seconds.</param>
    public void AdvanceTime(float elapsedSeconds)
    {
        if (TryAdvanceTickTimer(elapsedSeconds))
            ProcessTick();
    }

    /// <summary>
    /// Advances the tick timer without immediately processing a completed interval.
    /// </summary>
    /// <param name="elapsedSeconds">The elapsed game-loop time in seconds.</param>
    /// <returns>True when a game tick is ready to process.</returns>
    public bool TryAdvanceTickTimer(float elapsedSeconds)
    {
        if (
            elapsedSeconds <= 0f
            || _tickInProgress
            || _spaceCombatSystem.HasPendingDecision
            || _tickInterval == null
        )
            return false;

        _tickTimer += elapsedSeconds;
        if (_tickTimer < _tickInterval)
            return false;

        _tickTimer = 0f;
        return true;
    }

    /// <summary>
    /// Runs one game tick.
    /// </summary>
    public void ProcessTick()
    {
        IEnumerator tick = ProcessTickIncrementally();
        try
        {
            while (tick.MoveNext()) { }
        }
        finally
        {
            (tick as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Runs one game tick and yields after each AI phase.
    /// </summary>
    /// <returns>A sequence containing one step per completed AI phase.</returns>
    public IEnumerator ProcessTickIncrementally()
    {
        if (
            _tickInProgress
            || _spaceCombatSystem.HasPendingDecision
            || _game.GetGameSpeed() == TickSpeed.Paused
        )
            yield break;

        _tickInProgress = true;
        try
        {
            foreach (object step in ProcessTickCore())
                yield return step;
        }
        finally
        {
            _tickInProgress = false;
        }
    }

    /// <summary>
    /// Processes the systems contained in one game tick.
    /// </summary>
    /// <returns>A sequence containing one step per completed AI phase.</returns>
    private IEnumerable<object> ProcessTickCore()
    {
        _game.CurrentTick++;
        TickProfiler.Begin("message");
        _messageSystem.ProcessTick();
        TickProfiler.End();
        GameLogger.Debug("Tick: " + _game.CurrentTick);

        TickProfiler.Begin("automation");
        _factionAutomationSystem.ProcessTick();
        TickProfiler.End();
        TickProfiler.Begin("resources");
        ProcessResults(_resourceProductionSystem.ProcessTick());
        TickProfiler.End();
        TickProfiler.Begin("manufacturing");
        ProcessResults(_manufacturingSystem.ProcessTick());
        TickProfiler.End();
        TickProfiler.Begin("maintenance");
        ProcessResults(_maintenanceSystem.ProcessTick());
        TickProfiler.End();

        TickProfiler.Begin("movement");
        List<GameResult> movementResults = ProcessResults(
            _movementSystem.ProcessTick(),
            processMessages: false
        );
        TickProfiler.End();

        TickProfiler.Begin("combat");
        List<GameResult> combatResults = ProcessResults(
            _spaceCombatSystem.ProcessTick(),
            processMessages: false
        );
        TickProfiler.End();

        TickProfiler.Begin("waypoints");
        List<GameResult> waypointResults = ProcessAvailableWaypointContinuations();
        TickProfiler.End();

        List<GameResult> movementPhaseResults = CombineResults(
            movementResults,
            combatResults,
            waypointResults
        );
        if (_spaceCombatSystem.HasPendingDecision)
        {
            StoreDeferredMessageResults(movementPhaseResults);
            TickCompleted?.Invoke();
            yield break;
        }

        TickProfiler.Begin("reactions");
        ProcessMessageReactions(movementPhaseResults);
        TickProfiler.End();

        TickProfiler.Begin("missions");
        ProcessResults(_missionSystem.ProcessTick());
        TickProfiler.End();
        TickProfiler.Begin("events");
        ProcessResults(_eventSystem.ProcessEvents(_game.GetEventPool()));
        TickProfiler.End();
        List<GameResult> aiResults = new List<GameResult>();
        foreach (object step in _aiSystem.ProcessTickIncrementally(aiResults))
            yield return step;
        TickProfiler.Begin("ai.results");
        ProcessResults(aiResults);
        TickProfiler.End();

        TickProfiler.Begin("blockade");
        ProcessResults(_blockadeSystem.ProcessTick());
        TickProfiler.End();
        TickProfiler.Begin("planetaryControl");
        ProcessResults(_planetaryControlSystem.ProcessTick());
        TickProfiler.End();
        TickProfiler.Begin("uprising");
        ProcessResults(_uprisingSystem.ProcessTick());
        TickProfiler.End();

        TickProfiler.Begin("research");
        ProcessResults(_researchSystem.ProcessTick());
        TickProfiler.End();
        TickProfiler.Begin("jedi");
        ProcessResults(_jediSystem.ProcessTick());
        TickProfiler.End();
        TickProfiler.Begin("victory");
        ProcessResults(_victorySystem.ProcessTick());
        TickProfiler.End();
        TickProfiler.Report(_game.CurrentTick, 100);
        TickCompleted?.Invoke();
    }

    /// <summary>
    /// Resolves the pending combat encounter and resumes ticking.
    /// </summary>
    /// <param name="autoResolve">Whether to auto-resolve instead of tactical combat.</param>
    /// <returns>The space combat result generated by the encounter, when present.</returns>
    public SpaceCombatResult ResolveCombat(bool autoResolve)
    {
        List<GameResult> combatResults = _spaceCombatSystem.ResolvePending(autoResolve);
        return CompleteCombatResolution(combatResults);
    }

    /// <summary>
    /// Resolves a pending retreat and routes its results before resuming ticks.
    /// </summary>
    /// <param name="retreatingFactionInstanceId">The faction withdrawing from combat.</param>
    /// <returns>The resulting space-combat summary, or null when retreat is unavailable.</returns>
    public SpaceCombatResult ResolveCombatRetreat(string retreatingFactionInstanceId)
    {
        List<GameResult> combatResults = _spaceCombatSystem.ResolvePendingRetreat(
            retreatingFactionInstanceId
        );
        if (combatResults == null)
            return null;

        return CompleteCombatResolution(combatResults);
    }

    /// <summary>
    /// Initializes a game and rebuilds its runtime systems and derived state.
    /// </summary>
    /// <param name="game">The game instance to initialize.</param>
    private void InitializeGame(GameRoot game)
    {
        if (game == null)
            throw new InvalidOperationException("Cannot manage a null game.");

        _game = game;
        if (_game.Config == null)
            _game.SetConfig(_gameData.GameConfig);
        _game.RebuildSceneState();

        _randomProvider = _game.Random;
        InitializeSystems();
        RebuildDerivedState();
        _tickTimer = 0f;
        SetGameSpeed(_game.GetGameSpeed());
    }

    /// <summary>
    /// Initializes all systems in dependency order.
    /// </summary>
    private void InitializeSystems()
    {
        _messageSystem = new MessageSystem(_game, _gameData.MessageDefinitions.GetDeepCopy());
        UnitFactory unitFactory = new UnitFactory(
            _gameData.Buildings,
            _gameData.CapitalShips,
            _gameData.Starfighters,
            _gameData.Regiments,
            _gameData.SpecialForces
        );
        _fogOfWarSystem = new FogOfWarSystem(_game);
        _blockadeSystem = new BlockadeSystem(_game, _randomProvider);
        _fleetSystem = new FleetSystem(_game);
        _personnelSystem = new PersonnelSystem(_game);
        _duelSystem = new DuelSystem(_game, _randomProvider);
        _movementSystem = new MovementSystem(_game, _fogOfWarSystem, _fleetSystem, _blockadeSystem);
        _headquartersSystem = new HeadquartersSystem(_game, _movementSystem);
        _manufacturingSystem = new ManufacturingSystem(_game, _fleetSystem, _movementSystem);
        _factionAutomationSystem = new FactionAutomationSystem(
            _game,
            _gameData,
            _manufacturingSystem
        );
        _maintenanceSystem = new MaintenanceSystem(_game, _randomProvider, _fleetSystem);
        _resourceProductionSystem = new ResourceProductionSystem(_game);
        _planetaryControlSystem = new PlanetaryControlSystem(
            _game,
            _movementSystem,
            _manufacturingSystem,
            _fogOfWarSystem
        );
        _uprisingSystem = new UprisingSystem(_game, _randomProvider, _planetaryControlSystem);
        _jediSystem = new JediSystem(_game, _randomProvider);
        _officerLoyaltySystem = new OfficerLoyaltySystem(_game, _randomProvider);
        _missionSystem = new MissionSystem(
            _game,
            _randomProvider,
            _movementSystem,
            _uprisingSystem,
            _officerLoyaltySystem,
            _personnelSystem
        );
        _spaceCombatSystem = new SpaceCombatSystem(_game, _randomProvider, _movementSystem);
        _bombardmentSystem = new BombardmentSystem(
            _game,
            _randomProvider,
            _movementSystem,
            _planetaryControlSystem,
            _personnelSystem
        );
        _planetaryAssaultSystem = new PlanetaryAssaultSystem(
            _game,
            _randomProvider,
            _planetaryControlSystem
        );
        _researchSystem = new ResearchSystem(_game, _randomProvider);
        _victorySystem = new VictorySystem(_game);
        GameRequestDispatcher requestDispatcher = new GameRequestDispatcher();
        requestDispatcher.Subscribe<UnitMovementRequest>(_movementSystem);
        requestDispatcher.Subscribe<UnitPlacementRequest>(_movementSystem);
        requestDispatcher.Subscribe<OwnershipChangeRequest>(_planetaryControlSystem);
        requestDispatcher.Subscribe<DuelRequest>(_duelSystem);
        requestDispatcher.Subscribe<MessageDeliveryRequest>(_messageSystem);
        _eventSystem = new GameEventSystem(_game, _randomProvider, unitFactory, requestDispatcher);
        _eventSystem.ValidateEvents(_game.GetEventPool());
        _aiSystem = new AISystem(
            _game,
            _missionSystem,
            _movementSystem,
            _manufacturingSystem,
            _bombardmentSystem,
            _planetaryAssaultSystem,
            _randomProvider,
            _fogOfWarSystem
        );

        InitializeResultProcessing();
    }

    /// <summary>
    /// Connects result producers, typed reactions, and observers.
    /// </summary>
    private void InitializeResultProcessing()
    {
        _resultProcessor = new GameResultProcessor();
        _resultProcessor.Subscribe<GameResult>(_eventSystem);
        _resultProcessor.Subscribe<BlockadeChangedResult>(_movementSystem);
        _resultProcessor.Subscribe<UnitArrivedResult>(_headquartersSystem);
        _resultProcessor.Subscribe<PlanetOwnershipChangedResult>(_headquartersSystem);
        _resultProcessor.Subscribe<PlanetOwnershipChangedResult>(_officerLoyaltySystem);
        _resultProcessor.Subscribe<HeadquartersLostResult>(_victorySystem);
        _resultProcessor.Subscribe<PlanetGarrisonChangedResult>(_planetaryControlSystem);
        _resultProcessor.Subscribe<PlanetGarrisonChangedResult>(_uprisingSystem);
        _resultProcessor.Subscribe<MissionCompletedResult>(_jediSystem);
        _resultProcessor.Subscribe<IntelligenceRevealedResult>(_fogOfWarSystem);
        _resultProcessor.Observe<GameObjectSabotagedResult>(_fogOfWarSystem.ProcessResults);

        _movementSystem.ResultsProduced += HandleSystemResultsProduced;
        _maintenanceSystem.ResultsProduced += HandleSystemResultsProduced;
        _bombardmentSystem.ResultsProduced += HandleSystemResultsProduced;
        _planetaryAssaultSystem.ResultsProduced += HandleSystemResultsProduced;
    }

    /// <summary>
    /// Rebuilds derived state that is not persisted.
    /// </summary>
    private void RebuildDerivedState()
    {
        IManufacturable[] templates = _gameData
            .Buildings.GetDeepCopy()
            .Cast<IManufacturable>()
            .Concat(_gameData.CapitalShips.GetDeepCopy())
            .Concat(_gameData.Starfighters.GetDeepCopy())
            .Concat(_gameData.Regiments.GetDeepCopy())
            .Concat(_gameData.SpecialForces.GetDeepCopy())
            .ToArray();

        foreach (Faction faction in _game.GetFactions())
            faction.RebuildResearchCatalog(templates);

        _manufacturingSystem.RebuildQueues();
    }

    /// <summary>
    /// Routes resolved combat results and restores the tick timer.
    /// </summary>
    /// <param name="combatResults">The results produced by combat resolution.</param>
    /// <returns>The space-combat result in the routed batch, when present.</returns>
    private SpaceCombatResult CompleteCombatResolution(List<GameResult> combatResults)
    {
        combatResults = ProcessResults(combatResults, processMessages: false);

        List<GameResult> waypointResults = ProcessAvailableWaypointContinuations();

        List<GameResult> movementPhaseResults = TakeDeferredMessageResults();
        movementPhaseResults.AddRange(combatResults);
        movementPhaseResults.AddRange(waypointResults);
        _messageSystem.ProcessResults(movementPhaseResults);
        _tickTimer = 0f;

        return combatResults.OfType<SpaceCombatResult>().FirstOrDefault();
    }

    /// <summary>
    /// Continues waypoint routes when no combat decision is blocking movement.
    /// </summary>
    /// <returns>The results produced while starting the next route legs.</returns>
    private List<GameResult> ProcessAvailableWaypointContinuations()
    {
        if (_spaceCombatSystem.HasPendingDecision)
            return new List<GameResult>();

        return ProcessResults(
            _movementSystem.ContinueFleetWaypointRoutes(),
            processMessages: false
        );
    }

    /// <summary>
    /// Resolves domain reactions and then presents the completed result batch to observers.
    /// Per-result logging is the responsibility of the system that produced the result.
    /// </summary>
    /// <param name="results">Batch of results from a system tick.</param>
    /// <param name="processMessages">Whether to create faction messages for this batch.</param>
    /// <returns>The initial results followed by every result produced by their reactions.</returns>
    private List<GameResult> ProcessResults(
        IEnumerable<GameResult> results,
        bool processMessages = true
    )
    {
        List<GameResult> resolvedResults = _resultProcessor.Process(results);
        List<PlanetaryAssaultResult> assaultResults = resolvedResults
            .OfType<PlanetaryAssaultResult>()
            .ToList();
        if (assaultResults.Count > 0)
            PlanetaryAssaultsResolved?.Invoke(assaultResults);

        List<VictoryResult> victoryResults = resolvedResults.OfType<VictoryResult>().ToList();
        if (victoryResults.Count > 0)
            VictoriesResolved?.Invoke(victoryResults);

        if (processMessages)
            ProcessMessageReactions(resolvedResults);

        foreach (
            HeadquartersLostResult headquarters in resolvedResults.OfType<HeadquartersLostResult>()
        )
            HeadquartersLost?.Invoke(headquarters);

        foreach (VictoryResult victory in resolvedResults.OfType<VictoryResult>())
            VictoryDeclared?.Invoke(victory);
        return resolvedResults;
    }

    /// <summary>
    /// Delivers messages and drains any result reactions that request additional messages.
    /// </summary>
    /// <param name="resolvedResults">The already-processed results to extend in place.</param>
    private void ProcessMessageReactions(List<GameResult> resolvedResults)
    {
        List<GameResult> pendingMessageResults = new List<GameResult>(resolvedResults);
        while (pendingMessageResults.Count > 0)
        {
            List<GameResult> deliveredResults = _messageSystem.ProcessResults(
                pendingMessageResults
            );
            if (deliveredResults.Count == 0)
                return;
            List<GameResult> deliveryReactions = _resultProcessor.Process(deliveredResults);
            resolvedResults.AddRange(deliveryReactions);
            foreach (
                MessageDeliveredResult delivery in deliveredResults.OfType<MessageDeliveredResult>()
            )
                MessageDelivered?.Invoke(delivery);
            pendingMessageResults = deliveryReactions;
        }
    }

    /// <summary>
    /// Routes results emitted by an immediate system command.
    /// </summary>
    /// <param name="results">The results emitted by the system.</param>
    private void HandleSystemResultsProduced(IReadOnlyList<GameResult> results)
    {
        List<GameResult> resolvedResults = ProcessResults(results);
        foreach (BombardmentResult result in resolvedResults.OfType<BombardmentResult>())
            BombardmentCompleted?.Invoke(result);
    }

    /// <summary>
    /// Stores movement and combat results until the pending combat decision is resolved.
    /// </summary>
    /// <param name="results">The results whose messages must wait for combat resolution.</param>
    private void StoreDeferredMessageResults(List<GameResult> results)
    {
        _deferredMessageResults.Clear();
        if (results != null)
            _deferredMessageResults.AddRange(results);
    }

    /// <summary>
    /// Returns and clears movement and combat results waiting on a combat decision.
    /// </summary>
    /// <returns>The pending message result batch.</returns>
    private List<GameResult> TakeDeferredMessageResults()
    {
        List<GameResult> results = new List<GameResult>(_deferredMessageResults);
        _deferredMessageResults.Clear();
        return results;
    }

    /// <summary>
    /// Combines result batches while preserving their original order.
    /// </summary>
    /// <param name="resultBatches">The result batches to combine.</param>
    /// <returns>A single ordered result list.</returns>
    private static List<GameResult> CombineResults(params List<GameResult>[] resultBatches)
    {
        List<GameResult> results = new List<GameResult>();
        foreach (List<GameResult> resultBatch in resultBatches)
        {
            if (resultBatch != null)
                results.AddRange(resultBatch);
        }

        return results;
    }
}
