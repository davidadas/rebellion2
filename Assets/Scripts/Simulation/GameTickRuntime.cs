using System;
using Rebellion.AI;
using Rebellion.Game;
using Rebellion.Util.DependencyInjection;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Prepares the complete set of tick dependencies before a session changes its active graph.
    /// </summary>
    internal sealed class GameTickRuntime
    {
        internal AIDirector AIDirector { get; }

        internal ITickProcessor Captive { get; }

        internal ITickProcessor FactionAutomation { get; }

        internal FogOfWarObserver FogOfWarObserver { get; }

        internal ITickProcessor GameEvents { get; }

        internal GameRoot Game { get; }

        internal ITickProcessor Jedi { get; }

        internal ITickProcessor Maintenance { get; }

        internal ManufacturingTickProcessor Manufacturing { get; }

        internal ITickProcessor Messages { get; }

        internal MissionTickProcessor Missions { get; }

        internal MovementCommands MovementCommands { get; }

        internal ITickProcessor Movement { get; }

        internal ITickProcessor Naming { get; }

        internal ITickProcessor PlanetaryControl { get; }

        internal ITickProcessor Recovery { get; }

        internal ITickProcessor Research { get; }

        internal ITickProcessor ResourceProduction { get; }

        internal SpaceCombatCommands SpaceCombatCommands { get; }

        internal ITickProcessor SpaceCombat { get; }

        internal ITickProcessor Uprising { get; }

        internal ITickProcessor Victory { get; }

        /// <summary>
        /// Constructs tick processors and restores research timers for one prepared game.
        /// </summary>
        /// <param name="services">The replacement game's runtime service scope.</param>
        internal GameTickRuntime(IServiceLocator services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            Game = services.GetService<GameRoot>();
            ResearchTickProcessor research = new ResearchTickProcessor(
                services.GetService<Rebellion.Util.Random.IRandomNumberProvider>()
            );
            research.InitializeTimers(Game);
            Research = research;

            MovementCommands = services.GetService<MovementCommands>();
            FogOfWarObserver = services.GetService<FogOfWarObserver>();
            SpaceCombatCommands = services.GetService<SpaceCombatCommands>();
            Messages = new MessageTickProcessor();
            FactionAutomation = new FactionAutomationTickProcessor(
                services.GetService<FactionAutomationCommands>()
            );
            SmugglingCommands smugglingCommands = services.GetService<SmugglingCommands>();
            SmugglingTickProcessor smuggling = new(smugglingCommands);
            ResourceProduction = new ResourceProductionTickProcessor(smuggling, smugglingCommands);
            Manufacturing = new ManufacturingTickProcessor(
                services.GetService<ManufacturingCommands>(),
                MovementCommands
            );
            Maintenance = new MaintenanceTickProcessor(services.GetService<MaintenanceCommands>());
            Recovery = new RecoveryTickProcessor();
            Captive = new CaptiveTickProcessor(services.GetService<CaptiveCommands>());
            Movement = new MovementTickProcessor(MovementCommands);
            SpaceCombat = new SpaceCombatTickProcessor(SpaceCombatCommands);
            Missions = new MissionTickProcessor(
                services.GetService<MissionCommands>(),
                services.GetService<MissionResolver>()
            );
            GameEvents = new GameEventTickProcessor(services.GetService<GameEventCommands>());
            Naming = new NamingTickProcessor(services.GetService<NamingCommands>());
            AIDirector = services.GetService<AIDirector>();
            PlanetaryControl = new PlanetaryControlTickProcessor(
                services.GetService<PlanetaryControlCommands>()
            );
            Uprising = new UprisingTickProcessor(services.GetService<UprisingResolver>());
            Jedi = new JediTickProcessor(services.GetService<JediCommands>());
            Victory = new VictoryTickProcessor(
                services.GetService<VictoryCommands>(),
                services.GetService<VictoryQueries>()
            );
        }
    }
}
