using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay.Scripting.Events;
using Rebellion.SceneGraph;
using Rebellion.Util.DependencyInjection;
using Rebellion.Util.Random;
using Rebellion.Util.Reflection;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Constructs and connects runtime components for the active game graph.
    /// </summary>
    public sealed partial class GameSession : IServiceLocator, IDisposable
    {
        private readonly GameDataCatalog _gameData;
        private readonly List<Action> _disconnect = new();
        private readonly List<ServiceLocator> _retiredServiceScopes = new();
        private IRandomNumberProvider _randomProvider;
        private ServiceLocator _serviceScope;
        private readonly SimulationCommandQueue _commands;
        private bool _disposed;

        public Exception CommandFailure => _commands.Failure;
        private bool IsOperationBoundaryClear =>
            !_disposed
            && CommandFailure == null
            && !_commands.IsExecuting
            && Results?.IsPublishing != true
            && !Pipeline.IsProcessing;

        /// <summary>
        /// Gets whether an external command can start outside tick execution and result callbacks.
        /// </summary>
        public bool CanExecuteCommands => IsOperationBoundaryClear && Tick.CanAcceptPlayerActions;

        /// <summary>
        /// Gets whether settled state can be saved, including the completed-tick callback before
        /// its iterator releases the execution guard. Executing commands remain excluded.
        /// </summary>
        public bool CanCaptureSettledState => IsOperationBoundaryClear && Tick.IsSettled;

        public GameRoot Game { get; private set; }
        public GameResultPipeline Pipeline { get; }
        public GameTickProcessor Tick { get; }

        internal MessageObserver MessageObserver { get; private set; }

        internal GameEventCommands GameEventCommands { get; private set; }

        internal GameResultBus Results { get; private set; }

        /// <summary>
        /// Prepares a game graph and constructs its connected runtime components.
        /// </summary>
        /// <param name="game">The active game graph.</param>
        /// <param name="gameData">The composed content catalog used to construct runtime dependencies.</param>
        public GameSession(GameRoot game, GameDataCatalog gameData)
            : this(gameData)
        {
            ReplaceGame(game);
        }

        /// <summary>
        /// Creates disconnected session owners before selecting initialization or recovery.
        /// </summary>
        /// <param name="gameData">The exact installed content catalog.</param>
        private GameSession(GameDataCatalog gameData)
        {
            _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
            Pipeline = new GameResultPipeline(() => Results, () => MessageObserver);
            Tick = new GameTickProcessor(
                (results, processMessages) => Pipeline.ProcessResults(results, processMessages),
                results => Pipeline.ProcessMessageReactions(results)
            );
            _commands = new SimulationCommandQueue(() =>
                Tick.CanAcceptPlayerActions
                && Results?.IsPublishing != true
                && !Pipeline.IsProcessing
            );
            Tick.CanStartTick = () => IsOperationBoundaryClear;
        }

        /// <summary>
        /// Submits a synchronous mutation on this session's owning thread, deferring it while
        /// a tick or result wave is executing. Completion means execution has actually finished.
        /// </summary>
        /// <typeparam name="T">The completed mutation result.</typeparam>
        /// <param name="command">The operation against this session's current graph.</param>
        /// <returns>The result, cancellation before execution on replacement, or the execution failure.</returns>
        public Task<T> ExecuteCommandAsync<T>(Func<T> command) => _commands.EnqueueAsync(command);

        /// <summary>
        /// Drains deferred commands from the application's existing owning-thread frame loop.
        /// This also runs while the game clock is paused.
        /// </summary>
        public void PumpCommands() => _commands.Pump();

        /// <summary>
        /// Cancels unstarted requests before the application's command authority changes.
        /// An executing mutation is never interrupted or transferred to another owner.
        /// </summary>
        public void CancelPendingCommands() => _commands.CancelPendingCommands();

        /// <summary>
        /// Resolves a runtime service from the current game's scope.
        /// </summary>
        /// <typeparam name="T">The requested runtime service type.</typeparam>
        /// <returns>The current game's service instance.</returns>
        public T GetService<T>() => _serviceScope.GetService<T>();

        /// <summary>
        /// Resolves a runtime service by its type.
        /// </summary>
        /// <param name="serviceType">The requested runtime service type.</param>
        /// <returns>The current game's service instance.</returns>
        public object GetService(Type serviceType) => _serviceScope.GetService(serviceType);

        /// <summary>
        /// Invalidates outgoing commands only at a writable graph-replacement boundary.
        /// A faulted command queue may recover through successful graph replacement.
        /// </summary>
        private void PrepareGameReplacement()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GameSession));
            if (
                _commands.IsExecuting
                || !Tick.CanAcceptPlayerActions
                || Results?.IsPublishing == true
                || Pipeline.IsProcessing
            )
                throw new InvalidOperationException(
                    "An executing simulation operation cannot replace its graph."
                );
            _commands.CancelPendingCommands();
        }

        /// <summary>
        /// Rebuilds components in place, preserving partial initialization on failure.
        /// </summary>
        /// <param name="game">The replacement graph to prepare.</param>
        internal void ReplaceGame(GameRoot game)
        {
            if (game == null)
                throw new InvalidOperationException("Cannot manage a null game.");

            PrepareGameReplacement();
            Game = game;
            EnsureGameConfiguration();
            Game.RebuildSceneState();
            RestoreSectorSuppressionDefaults();
            Tick.Reset();
            _randomProvider = game.Random;
            Action[] previousConnections = _disconnect.ToArray();
            ServiceLocator previousServiceScope = _serviceScope;
            try
            {
                InitializeComponents(Tick.ConnectRuntime);
                RebuildDerivedState();
            }
            catch
            {
                if (previousServiceScope != null && previousServiceScope != _serviceScope)
                    _retiredServiceScopes.Add(previousServiceScope);
                throw;
            }

            CompleteScopeReplacement(previousConnections, previousServiceScope);
        }

        /// <summary>
        /// Reconstructs runtime rules from the installed catalog and the campaign's saved settings.
        /// </summary>
        private void EnsureGameConfiguration()
        {
            if (Game.Config != null)
                return;
            GameConfig config = _gameData.GameConfig.GetDeepCopy();
            Game.Summary?.CustomSettings?.ApplyRuntimeRules(config);
            Game.SetConfig(config);
        }

        /// <summary>
        /// Releases previous runtime bindings after the replacement scope is completely prepared.
        /// </summary>
        /// <param name="previousConnections">The callbacks attached by the previous scope.</param>
        /// <param name="previousServiceScope">The previous owned scope, when one existed.</param>
        private void CompleteScopeReplacement(
            Action[] previousConnections,
            ServiceLocator previousServiceScope
        )
        {
            foreach (Action disconnect in previousConnections)
            {
                disconnect();
                _disconnect.Remove(disconnect);
            }
            previousServiceScope?.Dispose();
            foreach (ServiceLocator retiredScope in _retiredServiceScopes)
                retiredScope.Dispose();
            _retiredServiceScopes.Clear();
            _commands.Reset();
        }

        /// <summary>
        /// Detaches the result subscriptions and producer connections owned by this runtime.
        /// </summary>
        public void Dispose()
        {
            _commands.Dispose();
            _disposed = true;
            foreach (Action disconnect in _disconnect)
                disconnect();
            _disconnect.Clear();
            _serviceScope?.Dispose();
            foreach (ServiceLocator retiredScope in _retiredServiceScopes)
                retiredScope.Dispose();
            _retiredServiceScopes.Clear();
        }

        /// <summary>
        /// Restores newly authored suppression values on units from older saved games.
        /// </summary>
        private void RestoreSectorSuppressionDefaults()
        {
            Dictionary<string, CapitalShip> templates = _gameData
                .CapitalShips.Where(template => !string.IsNullOrEmpty(template?.TypeID))
                .ToDictionary(template => template.TypeID, StringComparer.Ordinal);
            foreach (
                CapitalShip ship in Game.GetRegisteredSceneNodesByType<CapitalShip>(
                    includeDisabled: true
                )
            )
            {
                if (
                    !string.IsNullOrEmpty(ship.TypeID)
                    && templates.TryGetValue(ship.TypeID, out CapitalShip template)
                )
                    ship.ApplySectorSuppressionDefault(template.SectorSuppression);
            }
        }

        /// <summary>
        /// Constructs the runtime components and connects their explicit dependencies.
        /// </summary>
        /// <param name="connectTick">Connects normal or recovered tick scheduling.</param>
        private void InitializeComponents(Action<IServiceLocator> connectTick)
        {
            MessageFactory messageFactory = new MessageFactory(
                _gameData.MessageDefinitions.GetDeepCopy()
            );
            GameResultBus resultBus = new GameResultBus();
            _serviceScope = GameServiceRegistration.Create(
                Game,
                _gameData,
                _randomProvider,
                messageFactory,
                resultBus
            );
            MessageObserver = GetService<MessageObserver>();
            GetService<MovementQueries>()
                .SetCompletedBuildingMovementPolicy(GetService<HeadquartersQueries>().CanMove);
            connectTick(_serviceScope);
            GameEventCommands = GetService<GameEventCommands>();
            GameEventCommands.ValidateEvents(Game.GetEventPool());

            Results = resultBus;
            ConnectResults();
        }

        /// <summary>
        /// Connects result producers, typed reactions, and observers.
        /// </summary>
        private void ConnectResults()
        {
            GameServiceRegistration.ConnectObservers(_serviceScope, Results);

            PlanetaryControlCommands planetaryControlSystem =
                GetService<PlanetaryControlCommands>();
            PlanetaryControlObserver planetaryControlObserver =
                GetService<PlanetaryControlObserver>();
            FogOfWarObserver fogOfWarObserver = GetService<FogOfWarObserver>();
            planetaryControlSystem.OwnershipChanging +=
                planetaryControlObserver.ObserveOwnershipChange;
            planetaryControlSystem.OwnershipChanged += fogOfWarObserver.ObserveOwnershipChange;
            _disconnect.Add(() =>
                planetaryControlSystem.OwnershipChanging -=
                    planetaryControlObserver.ObserveOwnershipChange
            );
            _disconnect.Add(() =>
                planetaryControlSystem.OwnershipChanged -= fogOfWarObserver.ObserveOwnershipChange
            );

            MovementCommands movementSystem = GetService<MovementCommands>();
            movementSystem.ResultsProduced += ProcessImmediateResults;
            _disconnect.Add(() => movementSystem.ResultsProduced -= ProcessImmediateResults);
            MissionCommands missionSystem = GetService<MissionCommands>();
            missionSystem.ResultsProduced += ProcessImmediateResults;
            _disconnect.Add(() => missionSystem.ResultsProduced -= ProcessImmediateResults);
            MaintenanceCommands maintenanceSystem = GetService<MaintenanceCommands>();
            maintenanceSystem.ResultsProduced += ProcessImmediateResults;
            _disconnect.Add(() => maintenanceSystem.ResultsProduced -= ProcessImmediateResults);
            OfficerCommandCommands officerCommandSystem = GetService<OfficerCommandCommands>();
            officerCommandSystem.ResultsProduced += ProcessImmediateResults;
            _disconnect.Add(() => officerCommandSystem.ResultsProduced -= ProcessImmediateResults);
            CaptiveCommands captiveSystem = GetService<CaptiveCommands>();
            captiveSystem.ResultsProduced += ProcessImmediateResults;
            _disconnect.Add(() => captiveSystem.ResultsProduced -= ProcessImmediateResults);
            BombardmentCommands bombardmentSystem = GetService<BombardmentCommands>();
            bombardmentSystem.ResultsProduced += ProcessImmediateResults;
            _disconnect.Add(() => bombardmentSystem.ResultsProduced -= ProcessImmediateResults);
            PlanetaryAssaultCommands planetaryAssaultSystem =
                GetService<PlanetaryAssaultCommands>();
            planetaryAssaultSystem.ResultsProduced += ProcessImmediateResults;
            _disconnect.Add(() =>
                planetaryAssaultSystem.ResultsProduced -= ProcessImmediateResults
            );
            VictoryCommands victorySystem = GetService<VictoryCommands>();
            victorySystem.ResultsProduced += ProcessImmediateResults;
            _disconnect.Add(() => victorySystem.ResultsProduced -= ProcessImmediateResults);
        }

        /// <summary>
        /// Routes results produced outside the tick loop and then refreshes visible intelligence.
        /// </summary>
        /// <param name="results">The immediately produced game results.</param>
        private void ProcessImmediateResults(IReadOnlyList<GameResult> results)
        {
            Pipeline.ProcessImmediate(results);
            GetService<FogOfWarObserver>().RefreshVisibleKnowledge();
        }

        /// <summary>
        /// Rebuilds derived state that is not persisted.
        /// </summary>
        private void RebuildDerivedState()
        {
            RebuildContentBindings();
            GetService<ManufacturingObserver>().RebuildQueues();
            GetService<FogOfWarObserver>().ReconcileKnowledge();
        }

        /// <summary>
        /// Rebinds immutable content catalogs without changing queues, knowledge, or runtime timers.
        /// </summary>
        private void RebuildContentBindings()
        {
            _gameData.BindOfficerTraits(GetPersistedOfficers());

            IManufacturable[] templates = CopyTemplates(_gameData.Buildings)
                .Cast<IManufacturable>()
                .Concat(CopyTemplates(_gameData.CapitalShips))
                .Concat(CopyTemplates(_gameData.Starfighters))
                .Concat(CopyTemplates(_gameData.Regiments))
                .Concat(CopyTemplates(_gameData.SpecialForces))
                .ToArray();

            foreach (Faction faction in Game.GetFactions())
                faction.RebuildResearchCatalog(templates);
        }

        /// <summary>
        /// Enumerates every authoritative and remembered officer whose derived trait definitions
        /// must be restored after loading a game.
        /// </summary>
        /// <returns>All officers persisted by the active game.</returns>
        private IEnumerable<Officer> GetPersistedOfficers()
        {
            foreach (
                Officer officer in Game.GetRegisteredSceneNodesByType<Officer>(
                    includeDisabled: true
                )
            )
            {
                yield return officer;
            }

            foreach (Officer officer in Game.GetUnrecruitedOfficers())
                yield return officer;

            foreach (Faction faction in Game.GetFactions())
            {
                foreach (
                    PlanetSectorSnapshot sectorSnapshot in faction.Fog?.Snapshots?.Values
                        ?? Enumerable.Empty<PlanetSectorSnapshot>()
                )
                {
                    foreach (
                        PlanetSnapshot planetSnapshot in sectorSnapshot?.Planets?.Values
                            ?? Enumerable.Empty<PlanetSnapshot>()
                    )
                    {
                        foreach (Officer officer in GetSnapshotOfficers(planetSnapshot))
                            yield return officer;
                    }
                }
            }
        }

        /// <summary>
        /// Enumerates officers stored directly or as cargo and mission participants in one fog
        /// snapshot.
        /// </summary>
        /// <param name="snapshot">The persisted planet snapshot.</param>
        /// <returns>Every officer retained by the snapshot.</returns>
        private static IEnumerable<Officer> GetSnapshotOfficers(PlanetSnapshot snapshot)
        {
            if (snapshot == null)
                yield break;

            foreach (Officer officer in snapshot.Officers ?? Enumerable.Empty<Officer>())
            {
                if (officer != null)
                    yield return officer;
            }

            foreach (Fleet fleet in snapshot.Fleets ?? Enumerable.Empty<Fleet>())
            {
                if (fleet == null)
                    continue;
                foreach (Officer officer in fleet.GetChildren<Officer>(true, true))
                    yield return officer;
            }

            foreach (Mission mission in snapshot.Missions ?? Enumerable.Empty<Mission>())
            {
                if (mission == null)
                    continue;
                foreach (Officer officer in mission.GetChildren<Officer>(true, true))
                    yield return officer;
            }
        }

        /// <summary>
        /// Creates detached template copies with fresh runtime identities.
        /// </summary>
        /// <typeparam name="T">The scene-node template type.</typeparam>
        /// <param name="templates">The templates to copy.</param>
        /// <returns>Detached copies suitable for derived catalogs.</returns>
        private static IEnumerable<T> CopyTemplates<T>(IEnumerable<T> templates)
            where T : class, ISceneNode
        {
            foreach (T template in templates)
            {
                T copy = (T)template.CreateCopy();
                copy.InstanceID = null;
                yield return copy;
            }
        }
    }
}
