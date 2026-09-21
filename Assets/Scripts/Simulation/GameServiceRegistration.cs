using System;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Util.DependencyInjection;
using Rebellion.Util.Random;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Registers the command and query implementations belonging to one active game.
    /// </summary>
    internal static class GameServiceRegistration
    {
        /// <summary>
        /// Creates a locator for one game without registering observers or changing result order.
        /// </summary>
        /// <param name="game">The active game graph.</param>
        /// <param name="gameData">The content catalog used by game services.</param>
        /// <param name="random">The active game's random provider.</param>
        /// <param name="messageFactory">The message factory configured for this game.</param>
        /// <returns>A locator that owns this game's commands and queries.</returns>
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
            services.AddSingleton<RecoveryCommands>();
            services.AddSingleton<CaptiveCommands>();
            services.AddSingleton<FactionAutomationCommands>();
            services.AddSingleton<MaintenanceCommands>();
            services.AddSingleton<ResourceProductionCommands>();
            services.AddSingleton<PlanetaryControlQueries>();
            services.AddSingleton<PlanetaryControlCommands>();
            services.AddSingleton<UprisingCommands>();
            services.AddSingleton<JediCommands>();
            services.AddSingleton<OfficerLoyaltyCommands>();
            services.AddSingleton<MissionQueries>();
            services.AddSingleton<MissionCommands>();
            services.AddSingleton<SpaceCombatQueries>();
            services.AddSingleton<SpaceCombatCommands>();
            services.AddSingleton<BombardmentQueries>();
            services.AddSingleton<BombardmentCommands>();
            services.AddSingleton<PlanetaryAssaultQueries>();
            services.AddSingleton<PlanetaryAssaultCommands>();
            services.AddSingleton<ResearchCommands>();
            services.AddSingleton<VictoryCommands>();
            services.AddSingleton<AIDirector>();

            return services.BuildServiceLocator();
        }
    }
}
