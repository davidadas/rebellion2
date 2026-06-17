using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

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
        _messageFactory = new MessageFactory(ResourceManager.GetGameData<MessageDefinition>());
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
            case TickSpeed.Paused:
                _stopwatch.Stop();
                _tickInterval = null;
                break;
        }

        if (_tickInterval != null)
            _stopwatch.Start();
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
        ProcessResults(_aiSystem.ProcessTick());

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
        ProcessMessageResults(results);

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

    private void ProcessMessageResults(List<GameResult> results)
    {
        ProcessArrivalMessages(results.OfType<UnitArrivedResult>());
        ProcessMissionMessages(results.OfType<MissionCompletedResult>());
        ProcessSabotageMessages(results.OfType<GameObjectSabotagedResult>());
        ProcessResearchMessages(
            results.OfType<ResearchOrderedResult>(),
            results.OfType<ResearchExhaustedResult>()
        );
        ProcessUprisingMessages(
            results.OfType<PlanetUprisingStartedResult>(),
            results.OfType<PlanetUprisingEndedResult>()
        );
        ProcessBlockadeMessages(
            results.OfType<BlockadeChangedResult>(),
            results.OfType<EvacuationLossesResult>()
        );
        ProcessMaintenanceMessages(results.OfType<GameObjectAutoscrappedResult>());
        ProcessCombatMessages(
            results.OfType<SpaceCombatResult>(),
            results.OfType<BombardmentResult>(),
            results.OfType<PlanetaryAssaultResult>()
        );

        foreach (GameObjectDeployedResult result in results.OfType<GameObjectDeployedResult>())
        {
            if (result.GameObject is not Building building || building.Movement != null)
                continue;

            Faction faction = GetFaction(building.GetOwnerInstanceID());
            AddMessage(
                faction,
                _messageFactory.CreateFacilityDeployed(
                    faction,
                    building,
                    building.GetParentOfType<Planet>()
                )
            );
        }

        foreach (
            ManufacturingCompletedResult result in results.OfType<ManufacturingCompletedResult>()
        )
        {
            AddMessage(
                result.Faction,
                _messageFactory.CreateManufacturingIdle(
                    result.Faction,
                    result.ProductType,
                    result.ProductionPlanet
                )
            );
        }

        foreach (SeatOfPowerChangedResult result in results.OfType<SeatOfPowerChangedResult>())
        {
            if (!result.IsAtSeat)
                continue;

            Faction faction = GetFaction(result.Officer?.GetOwnerInstanceID());
            AddMessage(faction, _messageFactory.CreateEmperorSeatOfPower(faction));
        }
    }

    private void ProcessResearchMessages(
        IEnumerable<ResearchOrderedResult> orderedResults,
        IEnumerable<ResearchExhaustedResult> exhaustedResults
    )
    {
        foreach (ResearchOrderedResult result in orderedResults)
            AddMessage(
                result.Faction,
                _messageFactory.CreateResearchComplete(result.Faction, result)
            );

        foreach (ResearchExhaustedResult result in exhaustedResults)
            AddMessage(
                result.Faction,
                _messageFactory.CreateResearchExhausted(result.Faction, result)
            );
    }

    private void ProcessUprisingMessages(
        IEnumerable<PlanetUprisingStartedResult> startedResults,
        IEnumerable<PlanetUprisingEndedResult> endedResults
    )
    {
        foreach (PlanetUprisingStartedResult result in startedResults)
        {
            Faction controller = GetFaction(result.Planet?.OwnerInstanceID);
            AddMessage(
                controller,
                _messageFactory.CreateUprisingStarted(controller, result, controller)
            );
            if (result.InstigatorFaction?.InstanceID != controller?.InstanceID)
            {
                AddMessage(
                    result.InstigatorFaction,
                    _messageFactory.CreateUprisingStarted(
                        result.InstigatorFaction,
                        result,
                        controller
                    )
                );
            }
        }

        foreach (PlanetUprisingEndedResult result in endedResults)
        {
            Faction controller = GetFaction(result.Planet?.OwnerInstanceID) ?? result.Faction;
            AddMessage(
                controller,
                _messageFactory.CreateUprisingEnded(controller, result, controller)
            );
        }
    }

    private void ProcessBlockadeMessages(
        IEnumerable<BlockadeChangedResult> blockadeResults,
        IEnumerable<EvacuationLossesResult> evacuationResults
    )
    {
        foreach (BlockadeChangedResult result in blockadeResults)
        {
            if (!result.Blockaded)
                continue;

            Faction blockadingFaction = GetFaction(result.BlockadingFleet?.GetOwnerInstanceID());
            Faction targetFaction = GetFaction(result.Planet?.OwnerInstanceID);
            AddMessage(
                blockadingFaction,
                _messageFactory.CreateBlockadeInitiated(blockadingFaction, result, targetFaction)
            );

            if (targetFaction?.InstanceID == blockadingFaction?.InstanceID)
                continue;

            AddMessage(
                targetFaction,
                _messageFactory.CreateBlockadeDetected(targetFaction, result, blockadingFaction)
            );
        }

        foreach (EvacuationLossesResult result in evacuationResults)
            AddMessage(
                result.Faction,
                _messageFactory.CreateEvacuationLosses(result.Faction, result)
            );
    }

    private void ProcessMaintenanceMessages(
        IEnumerable<GameObjectAutoscrappedResult> autoscrapResults
    )
    {
        foreach (GameObjectAutoscrappedResult result in autoscrapResults)
        {
            Planet location = GetResultPlanet(
                result.Context ?? result.Ref ?? result.DestroyedObject
            );
            Faction faction =
                GetOwnerFaction(result.DestroyedObject)
                ?? GetOwnerFaction(result.Ref)
                ?? GetFaction(location?.OwnerInstanceID);
            AddMessage(
                faction,
                _messageFactory.CreateMaintenanceAutoscrap(faction, result, location)
            );
        }
    }

    private void ProcessCombatMessages(
        IEnumerable<SpaceCombatResult> battleResults,
        IEnumerable<BombardmentResult> bombardmentResults,
        IEnumerable<PlanetaryAssaultResult> assaultResults
    )
    {
        foreach (SpaceCombatResult result in battleResults)
        {
            Faction attacker = GetFaction(result.AttackerFleet?.GetOwnerInstanceID());
            Faction defender = GetFaction(result.DefenderFleet?.GetOwnerInstanceID());
            AddMessage(attacker, _messageFactory.CreateSpaceBattle(attacker, result, defender));
            if (defender?.InstanceID != attacker?.InstanceID)
                AddMessage(defender, _messageFactory.CreateSpaceBattle(defender, result, attacker));
        }

        foreach (BombardmentResult result in bombardmentResults)
        {
            Faction defender = GetFaction(result.Planet?.OwnerInstanceID);
            AddMessage(
                result.AttackingFaction,
                _messageFactory.CreateBombardment(result.AttackingFaction, result, defender)
            );
            if (defender?.InstanceID != result.AttackingFaction?.InstanceID)
                AddMessage(defender, _messageFactory.CreateBombardment(defender, result, defender));
        }

        foreach (PlanetaryAssaultResult result in assaultResults)
        {
            Faction defender =
                result.OwnershipChange?.PreviousOwner ?? GetFaction(result.Planet?.OwnerInstanceID);
            AddMessage(
                result.AttackingFaction,
                _messageFactory.CreatePlanetaryAssault(result.AttackingFaction, result, defender)
            );
            if (defender?.InstanceID != result.AttackingFaction?.InstanceID)
                AddMessage(
                    defender,
                    _messageFactory.CreatePlanetaryAssault(defender, result, defender)
                );
        }
    }

    private void ProcessMissionMessages(IEnumerable<MissionCompletedResult> results)
    {
        foreach (MissionCompletedResult result in results)
        {
            Planet target = GetMissionTarget(result);
            Faction actorFaction = GetFaction(result.Mission?.OwnerInstanceID);
            AddMessage(
                actorFaction,
                _messageFactory.CreateMissionReport(actorFaction, result, target)
            );

            Faction targetFaction = GetFaction(target?.OwnerInstanceID);
            if (targetFaction?.InstanceID == actorFaction?.InstanceID)
                continue;

            AddMessage(
                targetFaction,
                _messageFactory.CreateEnemyMissionFoiled(targetFaction, result, target)
            );
        }
    }

    private void ProcessSabotageMessages(IEnumerable<GameObjectSabotagedResult> results)
    {
        foreach (GameObjectSabotagedResult result in results)
        {
            Planet target = GetSabotageTarget(result);
            string ownerInstanceID = GetOwnerInstanceID(result.SabotagedObject);
            if (string.IsNullOrEmpty(ownerInstanceID))
                ownerInstanceID = target?.OwnerInstanceID;

            Faction faction = GetFaction(ownerInstanceID);
            AddMessage(faction, _messageFactory.CreateSabotageStrike(faction, result, target));
        }
    }

    private void ProcessArrivalMessages(IEnumerable<UnitArrivedResult> arrivals)
    {
        Dictionary<
            (string OwnerInstanceID, string DestinationInstanceID),
            List<CapitalShip>
        > shipGroups =
            new Dictionary<
                (string OwnerInstanceID, string DestinationInstanceID),
                List<CapitalShip>
            >();
        Dictionary<
            (string OwnerInstanceID, string DestinationInstanceID),
            Planet
        > shipDestinations =
            new Dictionary<(string OwnerInstanceID, string DestinationInstanceID), Planet>();

        foreach (UnitArrivedResult arrival in arrivals)
        {
            if (arrival.Unit is Fleet fleet)
            {
                Faction faction = GetFaction(fleet.GetOwnerInstanceID());
                AddMessage(
                    faction,
                    _messageFactory.CreateFleetArrived(faction, fleet, arrival.Destination)
                );
                continue;
            }

            if (arrival.Unit is CapitalShip ship)
            {
                var key = (ship.GetOwnerInstanceID(), arrival.Destination?.GetInstanceID());
                if (!shipGroups.TryGetValue(key, out List<CapitalShip> ships))
                {
                    ships = new List<CapitalShip>();
                    shipGroups[key] = ships;
                    shipDestinations[key] = arrival.Destination;
                }

                ships.Add(ship);
                continue;
            }

            if (arrival.Unit is Building building)
            {
                Faction faction = GetFaction(building.GetOwnerInstanceID());
                AddMessage(
                    faction,
                    _messageFactory.CreateFacilityDeployed(faction, building, arrival.Destination)
                );
            }
        }

        foreach (
            KeyValuePair<
                (string OwnerInstanceID, string DestinationInstanceID),
                List<CapitalShip>
            > group in shipGroups
        )
        {
            Faction faction = GetFaction(group.Key.OwnerInstanceID);
            AddMessage(
                faction,
                _messageFactory.CreateShipsArrived(
                    faction,
                    group.Value,
                    shipDestinations[group.Key]
                )
            );
        }
    }

    private static Planet GetMissionTarget(MissionCompletedResult result)
    {
        return result?.Mission?.GetParent() as Planet ?? result?.Mission?.GetLastParent() as Planet;
    }

    private static Planet GetSabotageTarget(GameObjectSabotagedResult result)
    {
        if (result?.Context is Planet contextPlanet)
            return contextPlanet;

        if (result?.SabotagedObject is ISceneNode sceneNode)
            return sceneNode.GetParentOfType<Planet>() ?? sceneNode.GetLastParent() as Planet;

        return null;
    }

    private static Planet GetResultPlanet(IGameEntity entity)
    {
        if (entity is Planet planet)
            return planet;

        if (entity is ISceneNode sceneNode)
            return sceneNode.GetParentOfType<Planet>() ?? sceneNode.GetLastParent() as Planet;

        return null;
    }

    private static string GetOwnerInstanceID(IGameEntity entity)
    {
        return entity is ISceneNode sceneNode ? sceneNode.GetOwnerInstanceID() : null;
    }

    private Faction GetFaction(string ownerInstanceID)
    {
        return string.IsNullOrEmpty(ownerInstanceID)
            ? null
            : _game.GetFactions().FirstOrDefault(faction => faction.InstanceID == ownerInstanceID);
    }

    private Faction GetOwnerFaction(IGameEntity entity)
    {
        return GetFaction(GetOwnerInstanceID(entity));
    }

    private static void AddMessage(Faction faction, Message message)
    {
        if (faction == null || message == null)
            return;

        faction.AddMessage(message);
    }
}
