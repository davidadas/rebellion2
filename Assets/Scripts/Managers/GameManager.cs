using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

/// <summary>
/// Coordinates all game systems each tick and routes results to cross-cutting handlers.
/// Owned by GameRuntime — do not create directly.
/// </summary>
public class GameManager
{
    private GameRoot _game;
    private AISystem _aiSystem;
    private GameEventSystem _eventManager;
    private MissionSystem _missionManager;
    private MovementSystem _movementManager;
    private ManufacturingSystem _manufacturingManager;
    private MaintenanceSystem _maintenanceManager;
    private ResourceProductionSystem _resourceProductionManager;
    private CombatSystem _combatManager;
    private FogOfWarSystem _fogOfWarManager;
    private BlockadeSystem _blockadeManager;
    private ResearchSystem _researchManager;
    private JediSystem _jediSystem;
    private BetrayalSystem _betrayalManager;
    private PlanetaryControlSystem _planetaryControlSystem;
    private UprisingSystem _uprisingManager;
    private VictorySystem _victoryManager;
    private MessageFactory _messageFactory;
    private IRandomNumberProvider _randomProvider;
    private CombatDecisionContext _pendingCombatDecision;
    private float? _tickInterval;
    private float _tickTimer;
    private readonly Stopwatch _stopwatch;

    /// <summary>
    /// Creates a new GameManager for the given game instance.
    /// </summary>
    /// <param name="game">The game instance to manage.</param>
    public GameManager(GameRoot game)
    {
        _stopwatch = new Stopwatch();

        SetGame(game);
        InitializeSystems();
        RebuildDerivedState();
        RehydrateMissions();
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
        RehydrateMissions();
        _tickTimer = 0f;
        _stopwatch.Restart();
        SetGameSpeed(_game.GetGameSpeed());
    }

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
        _messageFactory = new MessageFactory(
            ResourceManager.GetGameData<MessageDefinition>(),
            ResourceManager.GetData<EncyclopediaEntries>()
        );
        _eventManager = new GameEventSystem(_game, _randomProvider);
        _fogOfWarManager = new FogOfWarSystem(_game);
        _blockadeManager = new BlockadeSystem(_game, _randomProvider);
        _movementManager = new MovementSystem(_game, _fogOfWarManager, _blockadeManager);
        _manufacturingManager = new ManufacturingSystem(_game, _randomProvider, _movementManager);
        _maintenanceManager = new MaintenanceSystem(_game, _randomProvider);
        _resourceProductionManager = new ResourceProductionSystem(_game);
        _planetaryControlSystem = new PlanetaryControlSystem(
            _game,
            _movementManager,
            _manufacturingManager,
            _fogOfWarManager
        );
        _jediSystem = new JediSystem(_game, _randomProvider);
        _missionManager = new MissionSystem(_game, _randomProvider, _movementManager);
        _combatManager = new CombatSystem(
            _game,
            _randomProvider,
            _movementManager,
            _planetaryControlSystem
        );
        _researchManager = new ResearchSystem(_game, _randomProvider);
        _betrayalManager = new BetrayalSystem(_game);
        _uprisingManager = new UprisingSystem(_game, _randomProvider, _planetaryControlSystem);
        _victoryManager = new VictorySystem(_game);
        _aiSystem = new AISystem(
            _game,
            _missionManager,
            _movementManager,
            _manufacturingManager,
            _combatManager,
            _randomProvider
        );
    }

    /// <summary>
    /// Rebuilds derived state that is not persisted.
    /// </summary>
    private void RebuildDerivedState()
    {
        IManufacturable[] templates = ResourceManager
            .GetGameData<Building>()
            .Cast<IManufacturable>()
            .Concat(ResourceManager.GetGameData<CapitalShip>())
            .Concat(ResourceManager.GetGameData<Starfighter>())
            .Concat(ResourceManager.GetGameData<Regiment>())
            .Concat(ResourceManager.GetGameData<SpecialForces>())
            .ToArray();

        foreach (Faction faction in _game.GetFactions())
            faction.RebuildResearchCatalog(templates);

        _manufacturingManager.RebuildQueues();
    }

    /// <summary>
    /// Applies saved mission probability tables to missions already in the scene graph.
    /// </summary>
    private void RehydrateMissions()
    {
        GameConfig.MissionProbabilityTablesConfig missionTables = _game
            .Config
            ?.ProbabilityTables
            ?.Mission;
        if (missionTables == null)
            return;

        foreach (Mission mission in _game.GetSceneNodesByType<Mission>())
            MissionFactory.ConfigureMission(mission, missionTables);
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

    public bool StartManufacturing(
        Planet producer,
        IManufacturable template,
        Planet destination,
        int count
    ) => StartManufacturing(producer, template, (ISceneNode)destination, count);

    public bool StartManufacturing(
        Planet producer,
        IManufacturable template,
        ISceneNode destination,
        int count
    )
    {
        if (producer == null || template == null || destination == null || count <= 0)
            return false;

        bool started = false;
        Fleet capitalShipDestination = null;
        Planet destinationPlanet = destination as Planet;
        Fleet destinationFleet = destination as Fleet;
        CapitalShip destinationShip = destination as CapitalShip;
        for (int i = 0; i < count; i++)
        {
            IManufacturable item = template.GetDeepCopy();
            if (item is not ISceneNode sceneNode)
                return started;

            sceneNode.OwnerInstanceID = producer.GetOwnerInstanceID();
            item.ManufacturingStatus = ManufacturingStatus.Building;
            item.ManufacturingProgress = 0;
            if (item is IMovable movable)
                movable.Movement = null;

            bool enqueued;
            if (destinationFleet != null)
            {
                enqueued = _manufacturingManager.Enqueue(producer, item, destinationFleet);
            }
            else if (destinationShip != null)
            {
                enqueued = _manufacturingManager.Enqueue(producer, item, destinationShip);
            }
            else if (destinationPlanet != null && item is CapitalShip)
            {
                capitalShipDestination ??= CreateFleetAtPlanet(
                    destinationPlanet,
                    producer.GetOwnerInstanceID()
                );
                if (capitalShipDestination == null)
                    return started;

                enqueued = _manufacturingManager.Enqueue(producer, item, capitalShipDestination);
            }
            else if (destinationPlanet != null)
            {
                enqueued = _manufacturingManager.Enqueue(producer, item, destinationPlanet);
            }
            else
            {
                return started;
            }

            if (!enqueued)
            {
                DetachEmptyManufacturingFleet(capitalShipDestination);
                return started;
            }

            started = true;
        }

        return started;
    }

    public bool StopManufacturing(Planet producer, ManufacturingType type)
    {
        return _manufacturingManager.ClearQueue(producer, type);
    }

    public Fleet CreateFleetAtPlanet(Planet destination, string ownerInstanceId)
    {
        if (destination == null || string.IsNullOrEmpty(ownerInstanceId))
            return null;

        Faction faction = _game.GetFactionByOwnerInstanceID(ownerInstanceId);
        if (faction == null)
            return null;

        Fleet fleet = faction.CreateFleet();
        _game.AttachNode(fleet, destination);
        return fleet;
    }

    private void DetachEmptyManufacturingFleet(Fleet fleet)
    {
        if (fleet == null || fleet.CapitalShips.Count > 0 || fleet.GetParent() == null)
            return;

        _game.DetachNode(fleet);
    }

    public bool CanCreateMission(
        MissionType missionType,
        string ownerInstanceId,
        ISceneNode target,
        Officer targetOfficer = null,
        ResearchDiscipline? discipline = null,
        IMissionParticipant participant = null
    )
    {
        return _missionManager.CanCreateMission(
            missionType,
            ownerInstanceId,
            target,
            targetOfficer,
            discipline,
            participant
        );
    }

    public List<MissionOption> GetCreatableMissionOptions(
        string ownerInstanceId,
        List<IMissionParticipant> participants,
        Planet targetPlanet,
        ISceneNode specificTarget = null
    )
    {
        return _missionManager.GetCreatableMissionOptions(
            ownerInstanceId,
            participants,
            targetPlanet,
            specificTarget
        );
    }

    public bool HasCreatableMissionOptions(
        string ownerInstanceId,
        List<IMissionParticipant> participants
    )
    {
        return _missionManager.HasCreatableMissionOptions(ownerInstanceId, participants);
    }

    public bool InitiateMission(
        MissionType missionType,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        ISceneNode target,
        Officer targetOfficer = null,
        ResearchDiscipline? discipline = null
    )
    {
        return _missionManager.InitiateMission(
            missionType,
            mainParticipants,
            decoyParticipants,
            target,
            targetOfficer,
            discipline
        );
    }

    public bool InitiateMissionWithSpecificTarget(
        MissionType missionType,
        List<IMissionParticipant> mainParticipants,
        List<IMissionParticipant> decoyParticipants,
        Planet targetPlanet,
        ISceneNode specificTarget,
        Officer targetOfficer = null,
        ResearchDiscipline? discipline = null
    )
    {
        return _missionManager.InitiateMissionWithSpecificTarget(
            missionType,
            mainParticipants,
            decoyParticipants,
            targetPlanet,
            specificTarget,
            targetOfficer,
            discipline
        );
    }

    public void RequestMove(List<IMovable> units, ISceneNode destination)
    {
        _movementManager.RequestMove(units, destination);
    }

    /// <summary>
    /// Returns the fog of war system for building faction-specific galaxy views.
    /// </summary>
    /// <returns>The active FogOfWarSystem instance.</returns>
    public FogOfWarSystem GetFogOfWarSystem() => _fogOfWarManager;

    /// <summary>
    /// Sets the game speed and adjusts the tick interval accordingly.
    /// </summary>
    /// <param name="speed">The desired tick speed.</param>
    public void SetGameSpeed(TickSpeed speed)
    {
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
                _stopwatch.Stop();
                _tickInterval = null;
                break;
        }

        if (_tickInterval != null)
            _stopwatch.Start();
    }

    public TickSpeed GetGameSpeed()
    {
        return _game.GetGameSpeed();
    }

    /// <summary>
    /// Advances the tick timer and fires a tick when the interval is reached.
    /// No-ops while combat is pending player resolution or the game is paused.
    /// </summary>
    public void Update()
    {
        if (_pendingCombatDecision != null || _tickInterval == null)
            return;

        float deltaTime = (float)_stopwatch.Elapsed.TotalSeconds;
        _stopwatch.Restart();
        _tickTimer += deltaTime;

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
    public void ResolveCombat(bool autoResolve)
    {
        if (_pendingCombatDecision == null)
            throw new InvalidOperationException("No pending combat to resolve.");

        ProcessResults(_combatManager.Resolve(_pendingCombatDecision, autoResolve));
        _pendingCombatDecision = null;
    }

    /// <summary>
    /// Runs one game tick.
    /// </summary>
    public void ProcessTick()
    {
        _game.CurrentTick++;
        GameLogger.Debug("Tick: " + _game.CurrentTick);

        ProcessResults(_resourceProductionManager.ProcessTick());
        ProcessResults(_manufacturingManager.ProcessTick());
        ProcessResults(_maintenanceManager.ProcessTick());

        ProcessResults(_movementManager.ProcessTick());
        ProcessResults(_combatManager.ProcessTick());
        if (_pendingCombatDecision != null)
            return;

        ProcessResults(_missionManager.ProcessTick());
        _eventManager.ProcessEvents(_game.GetEventPool());
        // ProcessResults(_aiSystem.ProcessTick());

        ProcessResults(_blockadeManager.ProcessTick());
        ProcessResults(_planetaryControlSystem.ProcessTick());
        ProcessResults(_uprisingManager.ProcessTick());
        ProcessResults(_betrayalManager.ProcessTick());

        ProcessResults(_researchManager.ProcessTick());
        ProcessResults(_jediSystem.ProcessTick());
        ProcessResults(_victoryManager.ProcessTick());
    }

    /// <summary>
    /// Handles cross-cutting side effects for a batch of game results.
    /// Per-result logging is the responsibility of the system that produced the result.
    /// </summary>
    /// <param name="results">Batch of results from a system tick.</param>
    private void ProcessResults(List<GameResult> results)
    {
        ProcessMessages(results);

        foreach (VictoryResult result in results.OfType<VictoryResult>())
        {
            // TODO: Set game over flag, trigger victory screen.
        }

        foreach (PendingCombatResult result in results.OfType<PendingCombatResult>())
        {
            _pendingCombatDecision = new CombatDecisionContext
            {
                AttackerFleetInstanceID = result.AttackerFleet?.GetInstanceID(),
                DefenderFleetInstanceID = result.DefenderFleet?.GetInstanceID(),
            };
        }

        foreach (MissionCompletedResult result in results.OfType<MissionCompletedResult>())
        {
            if (result.Outcome == MissionOutcome.Success)
                ProcessResults(_jediSystem.ApplyForceGrowth(result.Mission.MainParticipants));
        }
    }

    private void ProcessMessages(List<GameResult> results)
    {
        foreach (MessageDelivery delivery in _messageFactory.CreateMessages(results, _game))
            AddMessage(delivery.Faction, delivery.Message);
    }

    private static void AddMessage(Faction faction, Message message)
    {
        if (faction == null || message == null)
            return;

        faction.AddMessage(message);
    }
}
