using System.Linq;
using Rebellion.Game;
using Rebellion.Util.Common;

namespace Rebellion.Generation
{
    /// <summary>
    /// Public entry point for new-game generation. Internally constructs a
    /// <see cref="GenerationContext"/>, walks each seeder in order, assembles the
    /// resulting <see cref="GameRoot"/>, and returns it ready for play.
    /// </summary>
    public sealed class GameBuilder
    {
        private readonly GameSummary _summary;
        private readonly IRandomNumberProvider _randomProvider;

        /// <summary>
        /// Creates a builder that will generate a game matching the given summary.
        /// The RNG seed is read from <see cref="GameSummary.Seed"/>, so the same
        /// summary always produces the same world.
        /// </summary>
        /// <param name="summary">The summary describing galaxy size, difficulty, factions, and starting research.</param>
        public GameBuilder(GameSummary summary)
        {
            _summary = summary;
            _randomProvider = new SystemRandomProvider(_summary.Seed);
        }

        /// <summary>
        /// Runs the full game-generation pipeline and returns a fully populated
        /// <see cref="GameRoot"/>.
        /// </summary>
        /// <returns>A <see cref="GameRoot"/> ready for play.</returns>
        public GameRoot Build()
        {
            GenerationContext ctx = LoadContext();

            // Set StartingFactionIDs if not specified.
            SetStartingFactionIDs(ctx);

            // Run each seeder in order.
            new GalaxySeeder().Seed(ctx);
            new PlanetSeeder().Seed(ctx);
            new FactionSeeder().Seed(ctx);
            new FacilitySeeder().Seed(ctx);
            new UnitSeeder().Seed(ctx);
            new OfficerSeeder().Seed(ctx);
            new BalanceSeeder().Seed(ctx);

            // Final assembly of the game root.
            AssembleGame(ctx);

            // Seed the fog of war after the game root is assembled.
            new FogOfWarSeeder().Seed(ctx);

            return ctx.Game;
        }

        /// <summary>
        /// Loads every input the pipeline needs from <see cref="ResourceManager"/> and
        /// packages them into a new <see cref="GenerationContext"/>.
        /// </summary>
        /// <returns>A context populated with config, templates, and world entities.</returns>
        private GenerationContext LoadContext()
        {
            int galaxySize = (int)_summary.GalaxySize;
            PlanetSystem[] systems = ResourceManager
                .GetGameData<PlanetSystem>()
                .Where(s => (int)s.Visibility <= galaxySize)
                .ToArray();

            return new GenerationContext
            {
                Summary = _summary,
                Config = ResourceManager.GetConfig<GameGenerationConfig>(),
                GameConfig = ResourceManager.GetConfig<GameConfig>(),
                Rng = _randomProvider,

                Systems = systems,
                Factions = ResourceManager.GetGameData<Faction>(),
                Buildings = ResourceManager.GetGameData<Building>(),
                CapitalShips = ResourceManager.GetGameData<CapitalShip>(),
                Starfighters = ResourceManager.GetGameData<Starfighter>(),
                Regiments = ResourceManager.GetGameData<Regiment>(),
                SpecialForces = ResourceManager.GetGameData<SpecialForces>(),
                Officers = ResourceManager.GetGameData<Officer>(),
                Events = ResourceManager.GetGameData<GameEvent>(),
            };
        }

        /// <summary>
        /// Populates <see cref="GameSummary.StartingFactionIDs"/> when the summary did
        /// not specify a subset, treating every faction as a starting faction.
        /// </summary>
        /// <param name="ctx">The generation context.</param>
        private static void SetStartingFactionIDs(GenerationContext ctx)
        {
            if (ctx.Summary.StartingFactionIDs?.Length > 0)
            {
                return;
            }

            ctx.Summary.StartingFactionIDs = ctx.Factions.Select(f => f.InstanceID).ToArray();
        }

        /// <summary>
        /// Constructs the <see cref="GameRoot"/> from the seeded context state, installs
        /// runtime configuration, and stores the result on the context.
        /// </summary>
        /// <param name="ctx">The generation context.</param>
        private static void AssembleGame(GenerationContext ctx)
        {
            GameRoot game = new GameRoot
            {
                EventPool = ctx.Events.ToList(),
                Summary = ctx.Summary,
                Factions = ctx.Factions.ToList(),
                Galaxy = new GalaxyMap { PlanetSystems = ctx.Systems.ToList() },
                UnrecruitedOfficers = ctx.UnrecruitedOfficers.ToList(),
                Random = ctx.Rng,
            };
            game.SetConfig(ctx.GameConfig);
            ctx.Game = game;
        }
    }
}
