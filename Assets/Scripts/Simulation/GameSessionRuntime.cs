using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.DependencyInjection;
using Rebellion.Util.Reflection;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Owns the graph, services, and subscriptions prepared and released as one session runtime.
    /// </summary>
    internal sealed class GameSessionRuntime : IDisposable
    {
        private readonly GameDataCatalog _gameData;
        private readonly List<Action> _disconnect = new();

        internal GameRoot Game { get; }
        internal ServiceLocator Services { get; }
        internal GameResultBus Results { get; }
        internal MessageObserver MessageObserver { get; }
        internal GameEventCommands GameEventCommands { get; }
        internal GameTickRuntime TickRuntime { get; }

        /// <summary>
        /// Prepares a replacement graph without changing the active session.
        /// Releases its constructed services when any preparation step fails.
        /// </summary>
        /// <param name="game">The replacement game graph.</param>
        /// <param name="gameData">The catalog used to restore runtime dependencies.</param>
        internal GameSessionRuntime(GameRoot game, GameDataCatalog gameData)
        {
            Game = game ?? throw new InvalidOperationException("Cannot manage a null game.");
            _gameData = gameData;
            if (Game.Config == null)
                Game.SetConfig(gameData.GameConfig);
            Game.RebuildSceneState();
            MessageFactory messageFactory = new MessageFactory(
                gameData.MessageDefinitions.GetDeepCopy()
            );
            Services = GameServiceRegistration.Create(Game, gameData, Game.Random, messageFactory);
            try
            {
                MessageObserver = Services.GetService<MessageObserver>();
                Services
                    .GetService<MovementQueries>()
                    .SetCompletedBuildingMovementPolicy(
                        Services.GetService<HeadquartersQueries>().CanMove
                    );
                TickRuntime = new GameTickRuntime(Services);
                GameEventCommands = Services.GetService<GameEventCommands>();
                GameEventCommands.ValidateEvents(Game.GetEventPool());
                Results = new GameResultBus();
                GameServiceRegistration.ConnectObservers(Services, Results);
                RebuildDerivedState();
            }
            catch
            {
                Services.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Detaches command producers and releases the services and result subscriptions it owns.
        /// </summary>
        public void Dispose()
        {
            foreach (Action disconnect in _disconnect)
                disconnect();
            _disconnect.Clear();
            Services.Dispose();
        }

        /// <summary>
        /// Connects result producers, typed reactions, and observers.
        /// </summary>
        /// <param name="processImmediateResults">Routes completed commands through the active session.</param>
        internal void ConnectResults(Action<IReadOnlyList<GameResult>> processImmediateResults)
        {
            PlanetaryControlCommands planetaryControlSystem =
                Services.GetService<PlanetaryControlCommands>();
            PlanetaryControlObserver planetaryControlObserver =
                Services.GetService<PlanetaryControlObserver>();
            FogOfWarObserver fogOfWarObserver = Services.GetService<FogOfWarObserver>();
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

            MovementCommands movementSystem = Services.GetService<MovementCommands>();
            movementSystem.ResultsProduced += processImmediateResults;
            _disconnect.Add(() => movementSystem.ResultsProduced -= processImmediateResults);
            MaintenanceCommands maintenanceSystem = Services.GetService<MaintenanceCommands>();
            maintenanceSystem.ResultsProduced += processImmediateResults;
            _disconnect.Add(() => maintenanceSystem.ResultsProduced -= processImmediateResults);
            OfficerCommandCommands officerCommandSystem =
                Services.GetService<OfficerCommandCommands>();
            officerCommandSystem.ResultsProduced += processImmediateResults;
            _disconnect.Add(() => officerCommandSystem.ResultsProduced -= processImmediateResults);
            CaptiveCommands captiveSystem = Services.GetService<CaptiveCommands>();
            captiveSystem.ResultsProduced += processImmediateResults;
            _disconnect.Add(() => captiveSystem.ResultsProduced -= processImmediateResults);
            BombardmentCommands bombardmentSystem = Services.GetService<BombardmentCommands>();
            bombardmentSystem.ResultsProduced += processImmediateResults;
            _disconnect.Add(() => bombardmentSystem.ResultsProduced -= processImmediateResults);
            PlanetaryAssaultCommands planetaryAssaultSystem =
                Services.GetService<PlanetaryAssaultCommands>();
            planetaryAssaultSystem.ResultsProduced += processImmediateResults;
            _disconnect.Add(() =>
                planetaryAssaultSystem.ResultsProduced -= processImmediateResults
            );
            VictoryCommands victorySystem = Services.GetService<VictoryCommands>();
            victorySystem.ResultsProduced += processImmediateResults;
            _disconnect.Add(() => victorySystem.ResultsProduced -= processImmediateResults);
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

            Services.GetService<ManufacturingObserver>().RebuildQueues();
            Services.GetService<FogOfWarObserver>().ReconcileKnowledge();
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
