using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;
using Rebellion.Util.Reflection;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Constructs and connects runtime components for the active game graph.
    /// </summary>
    public sealed class GameSession : IDisposable
    {
        private readonly GameDataCatalog _gameData;
        private readonly List<Action> _disconnect = new();
        private IRandomNumberProvider _randomProvider;
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

        internal MessageCommands MessageCommands { get; private set; }
        internal MessageObserver MessageObserver { get; private set; }

        internal GameEventExecutor GameEventExecutor { get; private set; }

        internal FogOfWarCommands FogOfWarCommands { get; private set; }
        internal FogOfWarQueries FogOfWarQueries { get; private set; }

        internal BlockadeCommands BlockadeCommands { get; private set; }

        internal FleetCommands FleetCommands { get; private set; }

        internal PersonnelCommands PersonnelCommands { get; private set; }

        internal PersonnelQueries PersonnelQueries { get; private set; }

        internal DuelCommands DuelCommands { get; private set; }

        internal MovementCommands MovementCommands { get; private set; }
        internal MovementQueries MovementQueries { get; private set; }

        internal HeadquartersCommands HeadquartersCommands { get; private set; }

        internal HeadquartersQueries HeadquartersQueries { get; private set; }

        internal NamingCommands NamingCommands { get; private set; }

        internal RecoveryCommands RecoveryCommands { get; private set; }

        internal CaptiveCommands CaptiveCommands { get; private set; }

        internal ManufacturingCommands ManufacturingCommands { get; private set; }
        internal ManufacturingQueries ManufacturingQueries { get; private set; }

        internal MaintenanceCommands MaintenanceCommands { get; private set; }

        internal ResourceProductionCommands ResourceProductionCommands { get; private set; }

        internal FactionAutomationCommands FactionAutomationCommands { get; private set; }

        internal PlanetaryControlCommands PlanetaryControlCommands { get; private set; }
        internal PlanetaryControlQueries PlanetaryControlQueries { get; private set; }

        internal UprisingCommands UprisingCommands { get; private set; }

        internal JediCommands JediCommands { get; private set; }

        internal MissionCommands MissionCommands { get; private set; }
        internal MissionQueries MissionQueries { get; private set; }

        internal SpaceCombatCommands SpaceCombatCommands { get; private set; }
        internal SpaceCombatQueries SpaceCombatQueries { get; private set; }

        internal BombardmentCommands BombardmentCommands { get; private set; }
        internal BombardmentQueries BombardmentQueries { get; private set; }

        internal PlanetaryAssaultCommands PlanetaryAssaultCommands { get; private set; }
        internal PlanetaryAssaultQueries PlanetaryAssaultQueries { get; private set; }

        internal ResearchCommands ResearchCommands { get; private set; }

        internal OfficerLoyaltyCommands OfficerLoyaltyCommands { get; private set; }

        internal VictoryCommands VictoryCommands { get; private set; }

        internal AIDirector AIDirector { get; private set; }

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
                () => Game,
                () => MessageCommands,
                () => FactionAutomationCommands,
                () => ResourceProductionCommands,
                () => ManufacturingCommands,
                () => MaintenanceCommands,
                () => RecoveryCommands,
                () => CaptiveCommands,
                () => MovementCommands,
                () => SpaceCombatCommands,
                () => MissionCommands,
                () => GameEventExecutor,
                () => NamingCommands,
                () => AIDirector,
                () => BlockadeCommands,
                () => PlanetaryControlCommands,
                () => UprisingCommands,
                () => ResearchCommands,
                () => JediCommands,
                () => VictoryCommands,
                (results, processMessages) => Pipeline.ProcessResults(results, processMessages),
                results => Pipeline.ProcessMessageReactions(results)
            );
            ReplaceGame(game);
        }

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
            InitializeComponents();
            RebuildDerivedState();

            foreach (Action disconnect in previousConnections)
            {
                disconnect();
                _disconnect.Remove(disconnect);
            }
        }

        /// <summary>
        /// Detaches the result subscriptions and producer connections owned by this runtime.
        /// </summary>
        public void Dispose()
        {
            foreach (Action disconnect in _disconnect)
                disconnect();
            _disconnect.Clear();
        }

        /// <summary>
        /// Constructs the runtime components and connects their explicit dependencies.
        /// </summary>
        private void InitializeComponents()
        {
            MessageFactory messageFactory = new MessageFactory(
                _gameData.MessageDefinitions.GetDeepCopy()
            );
            MessageCommands = new MessageCommands(Game, messageFactory);
            MessageObserver = new MessageObserver(Game, messageFactory, MessageCommands);
            UnitFactory unitFactory = new UnitFactory(
                _gameData.Buildings,
                _gameData.CapitalShips,
                _gameData.Starfighters,
                _gameData.Regiments,
                _gameData.SpecialForces
            );
            FogOfWarCommands = new FogOfWarCommands(Game);
            FogOfWarQueries = new FogOfWarQueries(Game);
            _fogOfWarObserver = new FogOfWarObserver(Game, FogOfWarCommands);
            BlockadeCommands = new BlockadeCommands(Game, _randomProvider);
            FleetCommands = new FleetCommands(Game);
            PersonnelQueries = new PersonnelQueries(Game);
            PersonnelCommands = new PersonnelCommands(PersonnelQueries);
            DuelCommands = new DuelCommands(Game, _randomProvider);
            MovementQueries = new MovementQueries(Game);
            MovementCommands = new MovementCommands(
                Game,
                FogOfWarCommands,
                FleetCommands,
                FogOfWarQueries,
                MovementQueries,
                BlockadeCommands
            );
            _movementObserver = new MovementObserver(MovementCommands);
            HeadquartersQueries = new HeadquartersQueries(Game);
            HeadquartersCommands = new HeadquartersCommands(
                Game,
                MovementCommands,
                HeadquartersQueries,
                MovementQueries
            );
            _headquartersObserver = new HeadquartersObserver(HeadquartersCommands);
            ManufacturingQueries = new ManufacturingQueries(Game);
            ManufacturingCommands = new ManufacturingCommands(
                Game,
                FleetCommands,
                ManufacturingQueries,
                MovementCommands
            );
            _manufacturingObserver = new ManufacturingObserver(ManufacturingCommands);
            NamingCommands = new NamingCommands(Game);
            RecoveryCommands = new RecoveryCommands(Game);
            CaptiveCommands = new CaptiveCommands(
                Game,
                _randomProvider,
                MovementCommands,
                FogOfWarCommands
            );
            _captiveObserver = new CaptiveObserver(Game, CaptiveCommands);
            FactionAutomationCommands = new FactionAutomationCommands(
                Game,
                _gameData,
                ManufacturingCommands
            );
            MaintenanceCommands = new MaintenanceCommands(Game, _randomProvider, FleetCommands);
            ResourceProductionCommands = new ResourceProductionCommands(Game);
            PlanetaryControlQueries = new PlanetaryControlQueries(Game);
            PlanetaryControlCommands = new PlanetaryControlCommands(
                Game,
                MovementCommands,
                ManufacturingCommands,
                FogOfWarCommands,
                PlanetaryControlQueries,
                FogOfWarQueries
            );
            _planetaryControlObserver = new PlanetaryControlObserver(PlanetaryControlCommands);
            UprisingCommands = new UprisingCommands(
                Game,
                _randomProvider,
                PlanetaryControlCommands
            );
            _uprisingObserver = new UprisingObserver(UprisingCommands);
            JediCommands = new JediCommands(Game, _randomProvider);
            _jediObserver = new JediObserver(JediCommands);
            OfficerLoyaltyCommands = new OfficerLoyaltyCommands(Game, _randomProvider);
            _officerLoyaltyObserver = new OfficerLoyaltyObserver(OfficerLoyaltyCommands);
            MissionQueries = new MissionQueries(Game);
            MissionCommands = new MissionCommands(
                Game,
                _randomProvider,
                MovementCommands,
                UprisingCommands,
                MissionQueries,
                MovementQueries,
                OfficerLoyaltyCommands,
                PersonnelCommands
            );
            _missionObserver = new MissionObserver(MissionCommands);
            SpaceCombatQueries = new SpaceCombatQueries(Game, MovementQueries);
            SpaceCombatCommands = new SpaceCombatCommands(
                Game,
                MovementCommands,
                SpaceCombatQueries
            );
            BombardmentQueries = new BombardmentQueries(Game);
            BombardmentCommands = new BombardmentCommands(
                Game,
                _randomProvider,
                MovementCommands,
                PlanetaryControlCommands,
                BombardmentQueries,
                PersonnelCommands
            );
            PlanetaryAssaultQueries = new PlanetaryAssaultQueries(Game);
            PlanetaryAssaultCommands = new PlanetaryAssaultCommands(
                Game,
                _randomProvider,
                PlanetaryControlCommands,
                PlanetaryAssaultQueries
            );
            ResearchCommands = new ResearchCommands(Game, _randomProvider);
            VictoryCommands = new VictoryCommands(Game);
            _victoryObserver = new VictoryObserver(VictoryCommands);
            GameEventExecutor = new GameEventExecutor(
                Game,
                _randomProvider,
                unitFactory,
                MovementCommands,
                PlanetaryControlCommands,
                DuelCommands,
                MessageCommands
            );
            GameEventExecutor.ValidateEvents(Game.GetEventPool());
            AIDirector = new AIDirector(
                Game,
                MissionCommands,
                MissionQueries,
                MovementCommands,
                ManufacturingCommands,
                BombardmentCommands,
                BombardmentQueries,
                PlanetaryAssaultCommands,
                PlanetaryAssaultQueries,
                _randomProvider,
                FogOfWarQueries,
                MaintenanceCommands
            );

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

            MovementCommands movementSystem = MovementCommands;
            movementSystem.ResultsProduced += Pipeline.ProcessImmediate;
            _disconnect.Add(() => movementSystem.ResultsProduced -= Pipeline.ProcessImmediate);
            MaintenanceCommands maintenanceSystem = MaintenanceCommands;
            maintenanceSystem.ResultsProduced += Pipeline.ProcessImmediate;
            _disconnect.Add(() => maintenanceSystem.ResultsProduced -= Pipeline.ProcessImmediate);
            BombardmentCommands bombardmentSystem = BombardmentCommands;
            bombardmentSystem.ResultsProduced += Pipeline.ProcessImmediate;
            _disconnect.Add(() => bombardmentSystem.ResultsProduced -= Pipeline.ProcessImmediate);
            PlanetaryAssaultCommands planetaryAssaultSystem = PlanetaryAssaultCommands;
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

            ManufacturingCommands.RebuildQueues();
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
