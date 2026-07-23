using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

/// <summary>
/// Coordinates all game systems each tick and routes results through domain reactions and observers.
/// Owned by GameRuntime — do not create directly.
/// </summary>
public class GameManager
{
    private GameRoot _game;
    private AISystem _aiSystem;
    private GameEventSystem _eventManager;
    private MissionSystem _missionManager;
    private MovementSystem _movementManager;
    private FleetSystem _fleetSystem;
    private PersonnelSystem _personnelSystem;
    private ManufacturingSystem _manufacturingManager;
    private MaintenanceSystem _maintenanceManager;
    private ResourceProductionSystem _resourceProductionManager;
    private SpaceCombatSystem _spaceCombatSystem;
    private BombardmentSystem _bombardmentSystem;
    private PlanetaryAssaultSystem _planetaryAssaultSystem;
    private FogOfWarSystem _fogOfWarManager;
    private BlockadeSystem _blockadeManager;
    private ResearchSystem _researchManager;
    private JediSystem _jediSystem;
    private BetrayalSystem _betrayalManager;
    private PlanetaryControlSystem _planetaryControlSystem;
    private UprisingSystem _uprisingManager;
    private VictorySystem _victoryManager;
    private MessageSystem _messageSystem;
    private IRandomNumberProvider _randomProvider;
    private IReadOnlyList<IGameResultHandler> _resultHandlers;
    private readonly List<GameResult> _resultsWaitingForCombatResolution = new List<GameResult>();
    private float? _tickInterval;
    private float _tickTimer;

    /// <summary>
    /// Raised when the active game speed changes.
    /// </summary>
    public event Action GameSpeedChanged;

    /// <summary>
    /// Raised after a game tick advances, including when processing suspends for pending combat.
    /// </summary>
    public event Action TickCompleted;

    /// <summary>
    /// Raised after a hot load replaces the active game and its systems.
    /// </summary>
    public event Action<GameRoot> GameReplaced;

    internal ManufacturingSystem ManufacturingSystem => _manufacturingManager;

    internal MovementSystem MovementSystem => _movementManager;

    internal FleetSystem FleetSystem => _fleetSystem;

    internal PersonnelSystem PersonnelSystem => _personnelSystem;

    internal MissionSystem MissionSystem => _missionManager;

    internal SpaceCombatSystem SpaceCombatSystem => _spaceCombatSystem;

    internal BombardmentSystem BombardmentSystem => _bombardmentSystem;

    internal PlanetaryAssaultSystem PlanetaryAssaultSystem => _planetaryAssaultSystem;

    internal MessageSystem MessageSystem => _messageSystem;

    /// <summary>
    /// Creates a new GameManager for the given game instance.
    /// </summary>
    /// <param name="game">The game instance to manage.</param>
    public GameManager(GameRoot game)
    {
        SetGame(game);
        InitializeSystems();
        RebuildDerivedState();
        SetGameSpeed(_game.GetGameSpeed());
    }

    /// <summary>
    /// Replaces the current game instance and reinitializes all systems.
    /// </summary>
    /// <param name="newGame">The replacement game instance.</param>
    public void ReplaceGame(GameRoot newGame)
    {
        SetGame(newGame);
        InitializeSystems();
        RebuildDerivedState();
        _tickTimer = 0f;
        SetGameSpeed(_game.GetGameSpeed());
        GameReplaced?.Invoke(_game);
    }

    /// <summary>
    /// Sets the active game and ensures required runtime state exists.
    /// </summary>
    /// <param name="game">The game instance to make active.</param>
    private void SetGame(GameRoot game)
    {
        if (game == null)
            throw new InvalidOperationException("Cannot manage a null game.");

        _game = game;

        if (_game.Config == null)
            _game.SetConfig(ResourceManager.GetConfig<GameConfig>());

        _randomProvider = _game.Random;
    }

    /// <summary>
    /// Initializes all systems in dependency order.
    /// </summary>
    private void InitializeSystems()
    {
        _messageSystem = new MessageSystem(
            _game,
            ResourceManager.GetEntityData<MessageDefinition>()
        );
        _eventManager = new GameEventSystem(_game, _randomProvider);
        _fogOfWarManager = new FogOfWarSystem(_game);
        _blockadeManager = new BlockadeSystem(_game, _randomProvider);
        _fleetSystem = new FleetSystem(_game);
        _personnelSystem = new PersonnelSystem(_game);
        _movementManager = new MovementSystem(
            _game,
            _fogOfWarManager,
            _fleetSystem,
            _blockadeManager
        );
        _manufacturingManager = new ManufacturingSystem(_game, _fleetSystem, _movementManager);
        _maintenanceManager = new MaintenanceSystem(_game, _randomProvider, _fleetSystem);
        _resourceProductionManager = new ResourceProductionSystem(_game);
        _planetaryControlSystem = new PlanetaryControlSystem(
            _game,
            _movementManager,
            _manufacturingManager,
            _fogOfWarManager
        );
        _uprisingManager = new UprisingSystem(_game, _randomProvider, _planetaryControlSystem);
        _jediSystem = new JediSystem(_game, _randomProvider);
        _missionManager = new MissionSystem(
            _game,
            _randomProvider,
            _movementManager,
            _uprisingManager
        );
        _spaceCombatSystem = new SpaceCombatSystem(_game, _randomProvider, _movementManager);
        _bombardmentSystem = new BombardmentSystem(
            _game,
            _randomProvider,
            _movementManager,
            _planetaryControlSystem
        );
        _planetaryAssaultSystem = new PlanetaryAssaultSystem(
            _game,
            _randomProvider,
            _planetaryControlSystem
        );
        _researchManager = new ResearchSystem(_game, _randomProvider);
        _betrayalManager = new BetrayalSystem(_game);
        _victoryManager = new VictorySystem(_game);
        _aiSystem = new AISystem(
            _game,
            _missionManager,
            _movementManager,
            _manufacturingManager,
            _bombardmentSystem,
            _planetaryAssaultSystem,
            _randomProvider
        );
        _resultHandlers = new IGameResultHandler[]
        {
            _planetaryControlSystem,
            _uprisingManager,
            _missionManager,
            _jediSystem,
        };
    }

    /// <summary>
    /// Rebuilds derived state that is not persisted.
    /// </summary>
    private void RebuildDerivedState()
    {
        IManufacturable[] templates = ResourceManager
            .GetEntityData<Building>()
            .Cast<IManufacturable>()
            .Concat(ResourceManager.GetEntityData<CapitalShip>())
            .Concat(ResourceManager.GetEntityData<Starfighter>())
            .Concat(ResourceManager.GetEntityData<Regiment>())
            .Concat(ResourceManager.GetEntityData<SpecialForces>())
            .ToArray();

        foreach (Faction faction in _game.GetFactions())
            faction.RebuildResearchCatalog(templates);

        _manufacturingManager.RebuildQueues();
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
    public FogOfWarSystem GetFogOfWarSystem() => _fogOfWarManager;

    /// <summary>
    /// Executes a validated movement order and processes its immediate results.
    /// </summary>
    /// <param name="items">The selected scene nodes or their snapshots.</param>
    /// <param name="destination">The requested destination or its snapshot.</param>
    /// <param name="ownerInstanceId">The faction authorized to move the selection.</param>
    /// <returns>True when the complete movement order was accepted.</returns>
    public bool TryRequestMove(
        IReadOnlyList<ISceneNode> items,
        ContainerNode destination,
        string ownerInstanceId
    )
    {
        if (
            !_movementManager.TryRequestMove(
                items,
                destination,
                ownerInstanceId,
                out List<GameResult> results
            )
        )
            return false;

        ProcessResults(results);
        return true;
    }

    /// <summary>
    /// Scraps a validated unit selection and processes its immediate results.
    /// </summary>
    /// <param name="items">The units selected for scrapping.</param>
    /// <param name="ownerInstanceId">The faction authorized to scrap the selection.</param>
    /// <returns>True when every selected unit was scrapped.</returns>
    public bool TryScrap(IReadOnlyList<IManufacturable> items, string ownerInstanceId)
    {
        if (!_maintenanceManager.TryScrap(items, ownerInstanceId, out List<GameResult> results))
            return false;

        ProcessResults(results);
        return true;
    }

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
    /// Returns the active game speed.
    /// </summary>
    /// <returns>The active game speed.</returns>
    public TickSpeed GetGameSpeed()
    {
        return _game.GetGameSpeed();
    }

    /// <summary>
    /// Advances the tick timer by elapsed game-loop time and fires a tick when the interval is reached.
    /// No-ops while combat is pending player resolution or the game is paused.
    /// </summary>
    /// <param name="elapsedSeconds">The elapsed game-loop time in seconds.</param>
    public void AdvanceTime(float elapsedSeconds)
    {
        if (elapsedSeconds <= 0f || _spaceCombatSystem.HasPendingDecision || _tickInterval == null)
            return;

        _tickTimer += elapsedSeconds;

        if (_tickTimer >= _tickInterval)
        {
            _tickTimer = 0f;
            ProcessTick();
        }
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
    /// Routes resolved combat results and restores the tick timer.
    /// </summary>
    /// <param name="combatResults">The results produced by combat resolution.</param>
    /// <returns>The space-combat result in the routed batch, when present.</returns>
    private SpaceCombatResult CompleteCombatResolution(List<GameResult> combatResults)
    {
        combatResults = ProcessResults(combatResults, processMessages: false);

        List<GameResult> messageResults = FlushDeferredResults();
        messageResults.AddRange(combatResults);
        _messageSystem.ProcessResults(messageResults);
        _tickTimer = 0f;

        return combatResults.OfType<SpaceCombatResult>().FirstOrDefault();
    }

    /// <summary>
    /// Executes orbital bombardment and processes the resulting game effects.
    /// </summary>
    /// <param name="attackingFleets">The attacking fleets.</param>
    /// <param name="targetPlanet">The bombardment target planet.</param>
    /// <param name="type">The bombardment target profile.</param>
    /// <returns>The bombardment result, or null when bombardment cannot execute.</returns>
    public BombardmentResult ExecuteOrbitalBombardment(
        IReadOnlyList<Fleet> attackingFleets,
        Planet targetPlanet,
        BombardmentType type
    )
    {
        if (targetPlanet == null)
            return null;

        List<Fleet> fleets =
            attackingFleets?.Where(fleet => fleet != null).ToList() ?? new List<Fleet>();
        if (!_bombardmentSystem.CanExecute(fleets, targetPlanet, type))
            return null;

        BombardmentResult result = _bombardmentSystem.Execute(fleets, targetPlanet, type);
        List<GameResult> results = new List<GameResult> { result };
        results.AddRange(result.Events);
        if (result.OwnershipChange != null)
            results.Add(result.OwnershipChange);

        ProcessResults(results);
        return result;
    }

    /// <summary>
    /// Executes a planetary assault and processes the resulting game effects.
    /// </summary>
    /// <param name="attackingFleets">The attacking fleets.</param>
    /// <param name="targetPlanet">The assault target planet.</param>
    /// <returns>The assault result, or null when the assault cannot execute.</returns>
    public PlanetaryAssaultResult ExecutePlanetaryAssault(
        IReadOnlyList<Fleet> attackingFleets,
        Planet targetPlanet
    )
    {
        if (targetPlanet == null)
            return null;

        List<Fleet> fleets =
            attackingFleets?.Where(fleet => fleet != null).ToList() ?? new List<Fleet>();
        if (!_planetaryAssaultSystem.CanExecute(fleets, targetPlanet))
            return null;

        PlanetaryAssaultResult result = _planetaryAssaultSystem.Execute(fleets, targetPlanet);
        List<GameResult> results = new List<GameResult> { result };
        results.AddRange(result.Events);
        if (result.OwnershipChange != null)
            results.Add(result.OwnershipChange);

        ProcessResults(results);
        return result;
    }

    /// <summary>
    /// Runs one game tick.
    /// </summary>
    public void ProcessTick()
    {
        if (_spaceCombatSystem.HasPendingDecision || _game.GetGameSpeed() == TickSpeed.Paused)
            return;

        _game.CurrentTick++;
        GameLogger.Debug("Tick: " + _game.CurrentTick);

        ProcessResults(_resourceProductionManager.ProcessTick());
        ProcessResults(_manufacturingManager.ProcessTick());
        ProcessResults(_maintenanceManager.ProcessTick());

        List<GameResult> movementResults = ProcessResults(
            _movementManager.ProcessTick(),
            processMessages: false
        );

        List<GameResult> combatResults = ProcessResults(
            _spaceCombatSystem.ProcessTick(),
            processMessages: false
        );

        List<GameResult> resultsWaitingForCombatResolution = CombineResults(
            movementResults,
            combatResults
        );
        if (_spaceCombatSystem.HasPendingDecision)
        {
            DeferResultsUntilCombatResolution(resultsWaitingForCombatResolution);
            TickCompleted?.Invoke();
            return;
        }

        _messageSystem.ProcessResults(resultsWaitingForCombatResolution);

        ProcessResults(_missionManager.ProcessTick());
        ProcessResults(_eventManager.ProcessEvents(_game.GetEventPool()));
        ProcessResults(_aiSystem.ProcessTick());

        ProcessResults(_blockadeManager.ProcessTick());
        ProcessResults(_planetaryControlSystem.ProcessTick());
        ProcessResults(_uprisingManager.ProcessTick());
        ProcessResults(_betrayalManager.ProcessTick());

        ProcessResults(_researchManager.ProcessTick());
        ProcessResults(_jediSystem.ProcessTick());
        ProcessResults(_victoryManager.ProcessTick());
        TickCompleted?.Invoke();
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
        List<GameResult> resolvedResults =
            results?.Where(result => result != null).ToList() ?? new List<GameResult>();
        List<GameResult> pendingResults = new List<GameResult>(resolvedResults);

        while (pendingResults.Count > 0)
        {
            List<GameResult> reactionResults = new List<GameResult>();
            foreach (IGameResultHandler handler in _resultHandlers)
            {
                List<GameResult> handlerResults = handler.HandleResults(pendingResults);
                if (handlerResults != null)
                    reactionResults.AddRange(handlerResults.Where(result => result != null));
            }

            resolvedResults.AddRange(reactionResults);
            pendingResults = reactionResults;
        }

        _fogOfWarManager.ProcessResults(resolvedResults);
        if (processMessages)
            _messageSystem.ProcessResults(resolvedResults);

        return resolvedResults;
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

    /// <summary>
    /// Stores movement and combat results until the pending combat decision is resolved.
    /// </summary>
    /// <param name="results">The results whose messages must wait for combat resolution.</param>
    private void DeferResultsUntilCombatResolution(List<GameResult> results)
    {
        _resultsWaitingForCombatResolution.Clear();
        if (results != null)
            _resultsWaitingForCombatResolution.AddRange(results);
    }

    /// <summary>
    /// Returns and clears movement and combat results waiting on a combat decision.
    /// </summary>
    /// <returns>The pending message result batch.</returns>
    private List<GameResult> FlushDeferredResults()
    {
        List<GameResult> results = new List<GameResult>(_resultsWaitingForCombatResolution);
        _resultsWaitingForCombatResolution.Clear();
        return results;
    }
}
