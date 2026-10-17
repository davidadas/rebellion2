using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
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

        public event Action PlayerBombardedByEnemy;
        public event Action PlayerInvadedByEnemy;

        internal MessageObserver MessageObserver { get; private set; }

        internal GameEventCommands GameEventCommands { get; private set; }

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
            Pipeline.BombardmentCompleted += HandleBombardmentCompleted;
            Pipeline.PlanetaryAssaultsResolved += HandlePlanetaryAssaultsResolved;
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
        /// Returns the pending encounter through the player's fog-of-war view.
        /// </summary>
        /// <returns>The detached pending encounter, or null when none is visible.</returns>
        public PendingCombatSnapshot GetPendingCombatForPlayer()
        {
            return GetService<CombatPresentationQueries>()
                .GetPendingCombat(Game.GetPlayerFaction()?.InstanceID);
        }

        /// <summary>
        /// Resolves pending combat and returns only the player's detached report.
        /// </summary>
        /// <param name="autoResolve">Whether to resolve without tactical combat.</param>
        /// <returns>The player's detached report, or null when no result was produced.</returns>
        public CombatReport ResolveCombatForPlayer(bool autoResolve)
        {
            SpaceCombatResult result = Tick.ResolveCombat(autoResolve);
            return GetService<CombatPresentationQueries>()
                .CreateReport(result, Game.GetPlayerFaction()?.InstanceID);
        }

        /// <summary>
        /// Resolves a player retreat and returns only the player's detached report.
        /// </summary>
        /// <returns>The player's detached report, or null when retreat was unavailable.</returns>
        public CombatReport ResolveCombatRetreatForPlayer()
        {
            string playerFactionId = Game.GetPlayerFaction()?.InstanceID;
            SpaceCombatResult result = Tick.ResolveCombatRetreat(playerFactionId);
            return GetService<CombatPresentationQueries>().CreateReport(result, playerFactionId);
        }

        /// <summary>
        /// Raises the player-facing bombardment notification without exposing its authoritative result.
        /// </summary>
        /// <param name="result">The completed bombardment.</param>
        private void HandleBombardmentCompleted(BombardmentResult result)
        {
            string playerFactionId = GetPlayerFactionId();
            if (
                result != null
                && !string.IsNullOrEmpty(playerFactionId)
                && result.AttackerOwnerInstanceID != playerFactionId
                && result.DefenderOwnerInstanceID == playerFactionId
            )
            {
                PlayerBombardedByEnemy?.Invoke();
            }
        }

        /// <summary>
        /// Raises the player-facing invasion notification without exposing authoritative results.
        /// </summary>
        /// <param name="results">The planetary assaults resolved in one batch.</param>
        private void HandlePlanetaryAssaultsResolved(IReadOnlyList<PlanetaryAssaultResult> results)
        {
            string playerFactionId = GetPlayerFactionId();
            if (
                !string.IsNullOrEmpty(playerFactionId)
                && results?.Any(result =>
                    result != null
                    && result.AttackerOwnerInstanceID != playerFactionId
                    && result.DefenderOwnerInstanceID == playerFactionId
                    && result.InitialAttackerRegimentCount > 0
                ) == true
            )
            {
                PlayerInvadedByEnemy?.Invoke();
            }
        }

        /// <summary>
        /// Returns the configured human player's faction identifier without requiring one to exist.
        /// </summary>
        /// <returns>The human-controlled faction identifier, or null when none is configured.</returns>
        private string GetPlayerFactionId()
        {
            return Game
                ?.GetPlayers()
                .FirstOrDefault(player => player.ControllerType == PlayerControllerType.Human)
                ?.FactionID;
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
            IManufacturable[] templates = CopyTemplates(_gameData.Buildings)
                .Cast<IManufacturable>()
                .Concat(CopyTemplates(_gameData.CapitalShips))
                .Concat(CopyTemplates(_gameData.Starfighters))
                .Concat(CopyTemplates(_gameData.Regiments))
                .Concat(CopyTemplates(_gameData.SpecialForces))
                .ToArray();

            foreach (Faction faction in Game.GetFactions())
                faction.RebuildResearchCatalog(templates);

            GetService<ManufacturingObserver>().RebuildQueues();
            GetService<FogOfWarObserver>().ReconcileKnowledge();
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
