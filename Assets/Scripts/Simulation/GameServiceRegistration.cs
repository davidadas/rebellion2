using System;
using Rebellion.AI;
using Rebellion.Game;
using Rebellion.Game.Units;
using Rebellion.Util.DependencyInjection;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Registers the runtime components belonging to one active game.
    /// </summary>
    internal static class GameServiceRegistration
    {
        private static readonly Type[] _resultObserverTypes =
        {
            typeof(MovementObserver),
            typeof(HeadquartersObserver),
            typeof(OfficerLoyaltyObserver),
            typeof(MissionObserver),
            typeof(CaptiveObserver),
            typeof(VictoryObserver),
            typeof(PlanetaryControlObserver),
            typeof(UprisingObserver),
            typeof(JediObserver),
            typeof(FogOfWarObserver),
            typeof(ManufacturingObserver),
        };

        /// <summary>
        /// Creates a locator for one game.
        /// </summary>
        /// <param name="game">The active game graph.</param>
        /// <param name="gameData">The content catalog used by game services.</param>
        /// <param name="random">The active game's random provider.</param>
        /// <param name="messageFactory">The message factory configured for this game.</param>
        /// <returns>A locator that owns this game's runtime services.</returns>
        internal static ServiceLocator Create(
            GameRoot game,
            GameDataCatalog gameData,
            IRandomNumberProvider random,
            MessageFactory messageFactory
        )
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            if (gameData == null)
                throw new ArgumentNullException(nameof(gameData));
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (messageFactory == null)
                throw new ArgumentNullException(nameof(messageFactory));

            ServiceContainer services = new();
            services.AddSingletonInstance(game);
            services.AddSingletonInstance(gameData);
            services.AddSingletonInstance(random);
            services.AddSingletonInstance(messageFactory);
            services.AddSingleton<UnitFactory>(locator =>
            {
                GameDataCatalog content = locator.GetService<GameDataCatalog>();
                return new UnitFactory(
                    content.Buildings,
                    content.CapitalShips,
                    content.Starfighters,
                    content.Regiments,
                    content.SpecialForces
                );
            });

            services.AddSingleton<MessageCommands>();
            services.AddSingleton<FogOfWarCommands>();
            services.AddSingleton<FogOfWarQueries>();
            services.AddSingleton<BlockadeCommands>();
            services.AddSingleton<FleetCommands>();
            services.AddSingleton<PersonnelQueries>();
            services.AddSingleton<PersonnelCommands>();
            services.AddSingleton<DuelCommands>();
            services.AddSingleton<MovementQueries>();
            services.AddSingleton<MovementCommands>();
            services.AddSingleton<HeadquartersQueries>();
            services.AddSingleton<HeadquartersCommands>();
            services.AddSingleton<ManufacturingQueries>();
            services.AddSingleton<ManufacturingCommands>();
            services.AddSingleton<NamingCommands>();
            services.AddSingleton<CaptiveCommands>();
            services.AddSingleton<FactionAutomationCommands>();
            services.AddSingleton<MaintenanceCommands>();
            services.AddSingleton<SmugglingCommands>();
            services.AddSingleton<PlanetaryControlQueries>();
            services.AddSingleton<PlanetaryControlCommands>();
            services.AddSingleton<UprisingCommands>();
            services.AddSingleton<JediCommands>();
            services.AddSingleton<OfficerCommandCommands>();
            services.AddSingleton<OfficerLoyaltyCommands>();
            services.AddSingleton<MissionQueries>();
            services.AddSingleton<MissionCommands>();
            services.AddSingleton<SpaceCombatQueries>();
            services.AddSingleton<SpaceCombatCommands>();
            services.AddSingleton<BombardmentQueries>();
            services.AddSingleton<BombardmentCommands>();
            services.AddSingleton<PlanetaryAssaultQueries>();
            services.AddSingleton<PlanetaryAssaultCommands>();
            services.AddSingleton<VictoryCommands>();
            services.AddSingleton<AIDirector>();
            services.AddSingleton<MessageObserver>();
            foreach (Type observerType in _resultObserverTypes)
                services.AddSingleton(observerType);
            services.AddSingleton<GameEventExecutor>();

            return services.BuildServiceLocator();
        }

        /// <summary>
        /// Connects the registered observers before results run.
        /// </summary>
        /// <param name="services">The active game's service scope.</param>
        /// <param name="results">The bus that delivers completed results.</param>
        internal static void ConnectObservers(ServiceLocator services, GameResultBus results)
        {
            foreach (Type observerType in _resultObserverTypes)
                ((IResultObserver)services.GetService(observerType)).Connect(results);
        }
    }
}
