using System;
using System.Collections.Generic;
using System.Linq;
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
                (results, processMessages) => Pipeline.ProcessResults(results, processMessages),
                results => Pipeline.ProcessMessageReactions(results)
            );
            ReplaceGame(game);
        }

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
            GameResultBus resultBus = new GameResultBus();
            _serviceScope = GameServiceRegistration.Create(
                Game,
                _gameData,
                _randomProvider,
                messageFactory
            );
            MessageObserver = GetService<MessageObserver>();
            GetService<MovementQueries>()
                .SetCompletedBuildingMovementPolicy(GetService<HeadquartersQueries>().CanMove);
            Tick.ConnectRuntime(_serviceScope);
            GameEventExecutor = GetService<GameEventExecutor>();
            GameEventExecutor.ValidateEvents(Game.GetEventPool());

            Results = resultBus;
            ConnectResults();
        }

        /// <summary>
        /// Connects result producers, typed reactions, and observers.
        /// </summary>
        private void ConnectResults()
        {
            _disconnect.Add(Results.Subscribe<GameResult>(GameEventExecutor.HandleResults).Dispose);
            GameServiceRegistration.ConnectObservers(_serviceScope, Results);

            MovementCommands movementSystem = GetService<MovementCommands>();
            movementSystem.ResultsProduced += ProcessImmediateResults;
            _disconnect.Add(() => movementSystem.ResultsProduced -= ProcessImmediateResults);
            MaintenanceCommands maintenanceSystem = GetService<MaintenanceCommands>();
            maintenanceSystem.ResultsProduced += ProcessImmediateResults;
            _disconnect.Add(() => maintenanceSystem.ResultsProduced -= ProcessImmediateResults);
            BombardmentCommands bombardmentSystem = GetService<BombardmentCommands>();
            bombardmentSystem.ResultsProduced += ProcessImmediateResults;
            _disconnect.Add(() => bombardmentSystem.ResultsProduced -= ProcessImmediateResults);
            PlanetaryAssaultCommands planetaryAssaultSystem =
                GetService<PlanetaryAssaultCommands>();
            planetaryAssaultSystem.ResultsProduced += ProcessImmediateResults;
            _disconnect.Add(() =>
                planetaryAssaultSystem.ResultsProduced -= ProcessImmediateResults
            );
        }

        /// <summary>
        /// Routes results produced outside the tick loop and then refreshes visible intelligence.
        /// </summary>
        /// <param name="results">The immediately produced game results.</param>
        private void ProcessImmediateResults(IReadOnlyList<GameResult> results)
        {
            Pipeline.ProcessImmediate(results);
            GetService<FogOfWarCommands>().RefreshVisibleKnowledge();
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
            GetService<FogOfWarCommands>().ReconcileKnowledge();
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
