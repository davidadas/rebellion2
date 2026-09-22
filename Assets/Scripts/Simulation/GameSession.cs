using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.DependencyInjection;
using Rebellion.Util.Random;
using Rebellion.Util.Reflection;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Constructs and connects runtime components for the active game graph.
    /// </summary>
    public sealed class GameSession : IServiceLocator, IDisposable
    {
        private readonly GameDataCatalog _gameData;
        private readonly List<Action> _disconnect = new();
        private readonly List<ServiceLocator> _retiredServiceScopes = new();
        private IRandomNumberProvider _randomProvider;
        private ServiceLocator _serviceScope;
        private HeadquartersObserver _headquartersObserver;
        private VictoryObserver _victoryObserver;
        private JediObserver _jediObserver;
        private OfficerLoyaltyObserver _officerLoyaltyObserver;
        private CaptiveObserver _captiveObserver;
        private PlanetaryControlObserver _planetaryControlObserver;
        private UprisingObserver _uprisingObserver;
        private ManufacturingObserver _manufacturingObserver;
        private MissionObserver _missionObserver;
        private FogOfWarObserver _fogOfWarObserver;
        private MovementObserver _movementObserver;

        public GameRoot Game { get; private set; }
        public GameResultPipeline Pipeline { get; }
        public GameTickProcessor Tick { get; }

        internal MessageObserver MessageObserver { get; private set; }

        internal GameEventExecutor GameEventExecutor { get; private set; }

        internal GameResultBus Results { get; private set; }

        /// <summary>
        /// Prepares a game graph and constructs its connected runtime components.
        /// </summary>
        /// <param name="game">The active game graph.</param>
        /// <param name="gameData">The composed content catalog used to construct runtime dependencies.</param>
        public GameSession(GameRoot game, GameDataCatalog gameData)
        {
            _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
            Pipeline = new GameResultPipeline(() => Results, () => MessageObserver);
            Tick = new GameTickProcessor(
                this,
                () => GameEventExecutor,
                (results, processMessages) => Pipeline.ProcessResults(results, processMessages),
                results => Pipeline.ProcessMessageReactions(results)
            );
            ReplaceGame(game);
        }

        /// <summary>
        /// Resolves a command or query from the current game's service scope.
        /// </summary>
        /// <typeparam name="T">The requested command or query type.</typeparam>
        /// <returns>The current game's service instance.</returns>
        public T GetService<T>() => _serviceScope.GetService<T>();

        /// <summary>
        /// Resolves a command or query by its runtime type.
        /// </summary>
        /// <param name="serviceType">The requested command or query type.</param>
        /// <returns>The current game's service instance.</returns>
        public object GetService(Type serviceType) => _serviceScope.GetService(serviceType);

        /// <summary>
        /// Rebuilds components in place, preserving partial initialization on failure.
        /// </summary>
        /// <param name="game">The replacement graph to prepare.</param>
        internal void ReplaceGame(GameRoot game)
        {
            if (game == null)
                throw new InvalidOperationException("Cannot manage a null game.");

            Game = game;
            if (Game.Config == null)
                Game.SetConfig(_gameData.GameConfig);
            Game.RebuildSceneState();
            Tick.Reset();
            _randomProvider = game.Random;
            Action[] previousConnections = _disconnect.ToArray();
            ServiceLocator previousServiceScope = _serviceScope;
            try
            {
                InitializeComponents();
                RebuildDerivedState();
            }
            catch
            {
                if (previousServiceScope != null && previousServiceScope != _serviceScope)
                    _retiredServiceScopes.Add(previousServiceScope);
                throw;
            }

            foreach (Action disconnect in previousConnections)
            {
                disconnect();
                _disconnect.Remove(disconnect);
            }
            previousServiceScope?.Dispose();
            foreach (ServiceLocator retiredScope in _retiredServiceScopes)
                retiredScope.Dispose();
            _retiredServiceScopes.Clear();
        }

        /// <summary>
        /// Detaches the result subscriptions and producer connections owned by this runtime.
        /// </summary>
        public void Dispose()
        {
            foreach (Action disconnect in _disconnect)
                disconnect();
            _disconnect.Clear();
            _serviceScope?.Dispose();
            foreach (ServiceLocator retiredScope in _retiredServiceScopes)
                retiredScope.Dispose();
            _retiredServiceScopes.Clear();
        }

        /// <summary>
        /// Constructs the runtime components and connects their explicit dependencies.
        /// </summary>
        private void InitializeComponents()
        {
            MessageFactory messageFactory = new MessageFactory(
                _gameData.MessageDefinitions.GetDeepCopy()
            );
            _serviceScope = GameServiceRegistration.Create(
                Game,
                _gameData,
                _randomProvider,
                messageFactory
            );
            // Preserve construction order: some services initialize movement policy or derived state.
            GetService<MessageCommands>();
            MessageObserver = new MessageObserver(
                Game,
                messageFactory,
                GetService<MessageCommands>()
            );
            UnitFactory unitFactory = new UnitFactory(
                _gameData.Buildings,
                _gameData.CapitalShips,
                _gameData.Starfighters,
                _gameData.Regiments,
                _gameData.SpecialForces
            );
            GetService<FogOfWarCommands>();
            GetService<FogOfWarQueries>();
            _fogOfWarObserver = new FogOfWarObserver(Game, GetService<FogOfWarCommands>());
            GetService<BlockadeCommands>();
            GetService<FleetCommands>();
            GetService<PersonnelQueries>();
            GetService<PersonnelCommands>();
            GetService<DuelCommands>();
            GetService<MovementQueries>();
            GetService<MovementCommands>();
            _movementObserver = new MovementObserver(GetService<MovementCommands>());
            GetService<HeadquartersQueries>();
            GetService<HeadquartersCommands>();
            _headquartersObserver = new HeadquartersObserver(GetService<HeadquartersCommands>());
            GetService<ManufacturingQueries>();
            GetService<ManufacturingCommands>();
            _manufacturingObserver = new ManufacturingObserver(GetService<ManufacturingCommands>());
            GetService<NamingCommands>();
            GetService<RecoveryCommands>();
            GetService<CaptiveCommands>();
            _captiveObserver = new CaptiveObserver(Game, GetService<CaptiveCommands>());
            GetService<FactionAutomationCommands>();
            GetService<MaintenanceCommands>();
            GetService<ResourceProductionCommands>();
            GetService<PlanetaryControlQueries>();
            GetService<PlanetaryControlCommands>();
            _planetaryControlObserver = new PlanetaryControlObserver(
                GetService<PlanetaryControlCommands>()
            );
            GetService<UprisingCommands>();
            _uprisingObserver = new UprisingObserver(GetService<UprisingCommands>());
            GetService<JediCommands>();
            _jediObserver = new JediObserver(GetService<JediCommands>());
            GetService<OfficerLoyaltyCommands>();
            _officerLoyaltyObserver = new OfficerLoyaltyObserver(
                GetService<OfficerLoyaltyCommands>()
            );
            GetService<MissionQueries>();
            GetService<MissionCommands>();
            _missionObserver = new MissionObserver(GetService<MissionCommands>());
            GetService<SpaceCombatQueries>();
            GetService<SpaceCombatCommands>();
            GetService<BombardmentQueries>();
            GetService<BombardmentCommands>();
            GetService<PlanetaryAssaultQueries>();
            GetService<PlanetaryAssaultCommands>();
            GetService<ResearchCommands>();
            GetService<VictoryCommands>();
            _victoryObserver = new VictoryObserver(GetService<VictoryCommands>());
            GameEventExecutor = new GameEventExecutor(
                Game,
                _randomProvider,
                unitFactory,
                GetService<MovementCommands>(),
                GetService<PlanetaryControlCommands>(),
                GetService<DuelCommands>(),
                GetService<MessageCommands>()
            );
            GameEventExecutor.ValidateEvents(Game.GetEventPool());
            GetService<AIDirector>();

            ConnectResults();
        }

        /// <summary>
        /// Connects result producers, typed reactions, and observers.
        /// </summary>
        private void ConnectResults()
        {
            Results = new GameResultBus();
            _disconnect.Add(Results.Subscribe<GameResult>(GameEventExecutor.HandleResults).Dispose);
            _disconnect.Add(
                Results.Subscribe<BlockadeChangedResult>(_movementObserver.HandleResults).Dispose
            );
            _disconnect.Add(
                Results.Subscribe<UnitArrivedResult>(_headquartersObserver.HandleResults).Dispose
            );
            _disconnect.Add(
                Results
                    .Subscribe<PlanetOwnershipChangedResult>(_headquartersObserver.HandleResults)
                    .Dispose
            );
            _disconnect.Add(
                Results
                    .Subscribe<PlanetOwnershipChangedResult>(_officerLoyaltyObserver.HandleResults)
                    .Dispose
            );
            _disconnect.Add(
                Results
                    .Subscribe<PlanetOwnershipChangedResult>(_captiveObserver.HandleResults)
                    .Dispose
            );
            _disconnect.Add(
                Results.Subscribe<HeadquartersLostResult>(_victoryObserver.HandleResults).Dispose
            );
            _disconnect.Add(
                Results
                    .Subscribe<PlanetGarrisonChangedResult>(_planetaryControlObserver.HandleResults)
                    .Dispose
            );
            _disconnect.Add(
                Results
                    .Subscribe<PopularSupportShiftResult>(_planetaryControlObserver.HandleResults)
                    .Dispose
            );
            _disconnect.Add(
                Results
                    .Subscribe<PlanetGarrisonChangedResult>(_uprisingObserver.HandleResults)
                    .Dispose
            );
            _disconnect.Add(
                Results.Subscribe<MissionCompletedResult>(_jediObserver.HandleResults).Dispose
            );
            _disconnect.Add(
                Results.Subscribe<OfficerCaptureStateResult>(_missionObserver.HandleResults).Dispose
            );
            _disconnect.Add(
                Results.Subscribe<OfficerCaptureStateResult>(_captiveObserver.HandleResults).Dispose
            );
            _disconnect.Add(
                Results
                    .Subscribe<IntelligenceRevealedResult>(_fogOfWarObserver.HandleResults)
                    .Dispose
            );
            _disconnect.Add(
                Results
                    .Subscribe<GameObjectDestroyedResult>(_manufacturingObserver.HandleResults)
                    .Dispose
            );
            _disconnect.Add(
                Results
                    .Subscribe<GameObjectScrappedResult>(_manufacturingObserver.HandleResults)
                    .Dispose
            );
            _disconnect.Add(
                Results.Subscribe<BombardmentResult>(_manufacturingObserver.HandleResults).Dispose
            );
            _disconnect.Add(
                Results
                    .Subscribe<PlanetaryAssaultResult>(_manufacturingObserver.HandleResults)
                    .Dispose
            );
            _disconnect.Add(
                Results.Observe<GameObjectSabotagedResult>(_fogOfWarObserver.ProcessResults).Dispose
            );

            MovementCommands movementSystem = GetService<MovementCommands>();
            movementSystem.ResultsProduced += Pipeline.ProcessImmediate;
            _disconnect.Add(() => movementSystem.ResultsProduced -= Pipeline.ProcessImmediate);
            MaintenanceCommands maintenanceSystem = GetService<MaintenanceCommands>();
            maintenanceSystem.ResultsProduced += Pipeline.ProcessImmediate;
            _disconnect.Add(() => maintenanceSystem.ResultsProduced -= Pipeline.ProcessImmediate);
            BombardmentCommands bombardmentSystem = GetService<BombardmentCommands>();
            bombardmentSystem.ResultsProduced += Pipeline.ProcessImmediate;
            _disconnect.Add(() => bombardmentSystem.ResultsProduced -= Pipeline.ProcessImmediate);
            PlanetaryAssaultCommands planetaryAssaultSystem =
                GetService<PlanetaryAssaultCommands>();
            planetaryAssaultSystem.ResultsProduced += Pipeline.ProcessImmediate;
            _disconnect.Add(() =>
                planetaryAssaultSystem.ResultsProduced -= Pipeline.ProcessImmediate
            );
        }

        /// <summary>
        /// Rebuilds derived state that is not persisted.
        /// </summary>
        private void RebuildDerivedState()
        {
            IManufacturable[] templates = CopyTemplates(_gameData.Buildings)
                .Cast<IManufacturable>()
                .Concat(CopyTemplates(_gameData.CapitalShips))
                .Concat(CopyTemplates(_gameData.Starfighters))
                .Concat(CopyTemplates(_gameData.Regiments))
                .Concat(CopyTemplates(_gameData.SpecialForces))
                .ToArray();

            foreach (Faction faction in Game.GetFactions())
                faction.RebuildResearchCatalog(templates);

            GetService<ManufacturingCommands>().RebuildQueues();
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
