using System;
using Rebellion.AI;
using Rebellion.AI.Tactical;
using Rebellion.Game;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Units;
using Rebellion.Gameplay.GameEvents;
using Rebellion.Gameplay.Scripting;
using Rebellion.Tactical;
using Rebellion.Util.DependencyInjection;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Registers the runtime components belonging to one active game.
    /// </summary>
    internal static class GameServiceRegistration
    {
        private static readonly Type[] _resultObserverTypes =
        {
            typeof(GameEventObserver),
            typeof(MovementObserver),
            typeof(RecoveryObserver),
            typeof(HeadquartersObserver),
            typeof(MissionObserver),
            typeof(JediCommands),
            typeof(CaptiveObserver),
            typeof(VictoryObserver),
            typeof(PlanetaryControlObserver),
            typeof(UprisingObserver),
            typeof(OfficerLoyaltyObserver),
            typeof(SpaceCombatObserver),
            typeof(FogOfWarObserver),
            typeof(ManufacturingObserver),
            typeof(AdvisorAutomationObserver),
            typeof(PlanetNotificationObserver),
        };

        /// <summary>
        /// Creates a locator for one game.
        /// </summary>
        /// <param name="game">The active game graph.</param>
        /// <param name="gameData">The content catalog used by game services.</param>
        /// <param name="random">The active game's random provider.</param>
        /// <param name="messageFactory">The message factory configured for this game.</param>
        /// <param name="results">The bus delivering completed game changes.</param>
        /// <param name="createTacticalAudio">Creates encounter audio from the active content catalog.</param>
        /// <returns>A locator that owns this game's runtime services.</returns>
        internal static ServiceLocator Create(
            GameRoot game,
            GameDataCatalog gameData,
            IRandomNumberProvider random,
            MessageFactory messageFactory,
            GameResultBus results,
            TacticalCombatAudioFactory createTacticalAudio
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
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            if (createTacticalAudio == null)
                throw new ArgumentNullException(nameof(createTacticalAudio));

            ServiceContainer services = new();
            services.AddSingletonInstance(game);
            services.AddSingletonInstance(gameData);
            services.AddSingletonInstance(random);
            services.AddSingletonInstance(messageFactory);
            services.AddSingletonInstance(results);
            services.AddSingleton<UnitFactory>(locator =>
            {
                GameDataCatalog content = locator.GetService<GameDataCatalog>();
                return new UnitFactory(
                    content.Buildings,
                    content.CapitalShips,
                    content.Starfighters,
                    content.Regiments,
                    content.SpecialForces,
                    content.TacticalCombat
                );
            });

            services.AddSingleton<MessageCommands>();
            services.AddSingleton<FogOfWarRecorder>();
            services.AddSingleton<FogOfWarCommands>(locator => new FogOfWarCommands(
                locator.GetService<GameRoot>(),
                locator.GetService<FogOfWarRecorder>()
            ));
            services.AddSingleton<FogOfWarQueries>();
            services.AddSingleton<FleetCommands>();
            services.AddSingleton<PersonnelQueries>();
            services.AddSingleton<PersonnelCommands>();
            services.AddSingleton<DuelCommands>(locator => new DuelCommands(
                locator.GetService<GameRoot>(),
                locator.GetService<IRandomNumberProvider>(),
                encounter =>
                    locator
                        .GetService<GameEventCommands>()
                        .ProcessTriggeredEvents(new[] { encounter }),
                locator.GetService<JediCommands>()
            ));
            services.AddSingleton<MovementQueries>();
            services.AddSingleton<BlockadeTransitionResolver>();
            services.AddSingleton<MovementCommands>();
            services.AddSingleton<HeadquartersQueries>();
            services.AddSingleton<HeadquartersCommands>();
            services.AddSingleton<ManufacturingQueries>();
            services.AddSingleton<ManufacturingCommands>();
            services.AddSingleton<ResourceProductionQueries>();
            services.AddSingleton<NamingCommands>();
            services.AddSingleton<CaptiveCommands>();
            services.AddSingleton<AdvisorAutomationCommands>(locator =>
            {
                GameDataCatalog catalog = locator.GetService<GameDataCatalog>();
                return new AdvisorAutomationCommands(
                    locator.GetService<GameRoot>(),
                    catalog.Buildings,
                    catalog.Regiments,
                    locator.GetService<ManufacturingCommands>()
                );
            });
            services.AddSingleton<MaintenanceCommands>();
            services.AddSingleton<SmugglingCommands>();
            services.AddSingleton<PlanetaryControlQueries>();
            services.AddSingleton<PlanetaryControlCommands>();
            services.AddSingleton<UprisingResolver>();
            services.AddSingleton<MissionQueries>();
            services.AddSingleton<MissionBetrayalResolver>();
            services.AddSingleton<MissionResolver>(locator => new MissionResolver(
                locator.GetService<GameRoot>(),
                locator.GetService<IRandomNumberProvider>(),
                locator.GetService<MovementCommands>(),
                locator.GetService<UprisingResolver>(),
                locator.GetService<MissionQueries>(),
                locator.GetService<DuelCommands>(),
                locator.GetService<MissionBetrayalResolver>(),
                locator.GetService<PersonnelCommands>(),
                locator.GetService<JediCommands>()
            ));
            services.AddSingleton<MissionCommands>(locator => new MissionCommands(
                locator.GetService<GameRoot>(),
                locator.GetService<MissionQueries>(),
                locator.GetService<MissionResolver>(),
                locator.GetService<PersonnelCommands>()
            ));
            services.AddSingleton<SpaceCombatQueries>();
            services.AddSingleton<TacticalCombatFactory>(locator => new TacticalCombatFactory(
                locator.GetService<GameRoot>(),
                locator.GetService<GameDataCatalog>().TacticalCombat,
                locator.GetService<SpaceCombatQueries>(),
                TacticalCombatPlanner.Attach,
                createTacticalAudio
            ));
            services.AddSingleton<SpaceCombatCommands>(locator => new SpaceCombatCommands(
                locator.GetService<GameRoot>(),
                locator.GetService<MovementCommands>(),
                locator.GetService<SpaceCombatQueries>(),
                locator.GetService<BlockadeTransitionResolver>(),
                locator.GetService<TacticalCombatFactory>(),
                locator.GetService<GameDataCatalog>().Starfighters,
                locator.GetService<SpaceCombatObserver>()
            ));
            services.AddSingleton<BombardmentQueries>();
            services.AddSingleton<BombardmentCommands>();
            services.AddSingleton<PlanetaryAssaultQueries>();
            services.AddSingleton<PlanetaryAssaultCommands>();
            services.AddSingleton<VictoryQueries>();
            services.AddSingleton<VictoryCommands>();
            services.AddSingleton<AIDirector>(locator => new AIDirector(
                locator.GetService<GameRoot>(),
                locator.GetService<MissionCommands>(),
                locator.GetService<MovementCommands>(),
                locator.GetService<ManufacturingCommands>(),
                locator.GetService<BombardmentCommands>(),
                locator.GetService<PlanetaryAssaultCommands>(),
                locator.GetService<IRandomNumberProvider>(),
                locator.GetService<MaintenanceCommands>(),
                locator.GetService<HeadquartersCommands>(),
                locator.GetService<PersonnelCommands>(),
                locator.GetService<GameResultBus>(),
                locator.GetService<GameDataCatalog>().Officers
            ));
            services.AddSingleton<MessageObserver>();
            foreach (Type observerType in _resultObserverTypes)
                if (observerType == typeof(SpaceCombatObserver))
                    services.AddSingleton<SpaceCombatObserver>(locator => new SpaceCombatObserver(
                        locator.GetService<GameRoot>(),
                        locator.GetService<GameDataCatalog>().TacticalCombat,
                        locator.GetService<PersonnelCommands>()
                    ));
                else
                    services.AddSingleton(observerType);
            services.AddSingleton<GameActionCommands>(locator => new GameActionCommands(
                locator.GetService<GameRoot>(),
                locator.GetService<IRandomNumberProvider>(),
                locator.GetService<UnitFactory>(),
                locator.GetService<MovementCommands>(),
                locator.GetService<PlanetaryControlCommands>(),
                locator.GetService<DuelCommands>(),
                locator.GetService<MessageCommands>(),
                locator.GetService<MissionResolver>(),
                locator.GetService<BlockadeTransitionResolver>()
            ));

            services.AddSingleton<GameEventCommands>();

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
