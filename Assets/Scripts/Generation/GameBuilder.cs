using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;
using Rebellion.Util.Reflection;

namespace Rebellion.Generation
{
    /// <summary>
    /// Public entry point for new-game generation. Internally constructs a
    /// <see cref="GenerationContext"/>, walks each seeder in order, assembles the
    /// resulting <see cref="GameRoot"/>, and returns it ready for play.
    /// </summary>
    public sealed class GameBuilder
    {
        private const string _localPlayerID = "PLAYER1";
        private const string _aiPlayerIDPrefix = "AI_";

        private readonly GameSummary _summary;
        private readonly GameDataCatalog _gameData;
        private readonly IRandomNumberProvider _randomProvider;

        /// <summary>
        /// Creates a builder that will generate a game matching the given summary.
        /// The RNG seed is read from <see cref="GameSummary.Seed"/>, so the same
        /// summary always produces the same world.
        /// </summary>
        /// <param name="summary">The summary describing galaxy size, difficulty, factions, and starting research.</param>
        /// <param name="gameData">The active pack's composed game data.</param>
        public GameBuilder(GameSummary summary, GameDataCatalog gameData)
            : this(summary, gameData, CreateRandomProvider(summary)) { }

        /// <summary>
        /// Creates a builder that will generate a game with the given RNG provider.
        /// </summary>
        /// <param name="summary">The summary describing the game to generate.</param>
        /// <param name="gameData">The active pack's composed game data.</param>
        /// <param name="randomProvider">Random number provider used by the generation pipeline.</param>
        public GameBuilder(
            GameSummary summary,
            GameDataCatalog gameData,
            IRandomNumberProvider randomProvider
        )
        {
            _summary = summary ?? throw new ArgumentNullException(nameof(summary));
            _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
            _randomProvider =
                randomProvider ?? throw new ArgumentNullException(nameof(randomProvider));
        }

        /// <summary>
        /// Creates the deterministic RNG provider for a summary.
        /// </summary>
        /// <param name="summary">The summary whose seed is used.</param>
        /// <returns>The random number provider for generation.</returns>
        private static IRandomNumberProvider CreateRandomProvider(GameSummary summary)
        {
            return new SystemRandomProvider(
                (summary ?? throw new ArgumentNullException(nameof(summary))).Seed
            );
        }

        /// <summary>
        /// Runs the full game-generation pipeline and returns a fully populated
        /// <see cref="GameRoot"/>.
        /// </summary>
        /// <returns>A <see cref="GameRoot"/> ready for play.</returns>
        public GameRoot Build()
        {
            GenerationContext ctx = LoadContext();

            SetStartingFactionIDs(ctx);
            RunSeeders(ctx);
            AssembleGame(ctx);
            new FogOfWarSeeder().Seed(ctx);

            return ctx.Game;
        }

        /// <summary>
        /// Runs the game-generation pipeline.
        /// </summary>
        /// <returns>A <see cref="GameRoot"/> ready for play.</returns>
        public GameRoot BuildGame()
        {
            return Build();
        }

        /// <summary>
        /// Copies every configured generation input into a new <see cref="GenerationContext"/>.
        /// </summary>
        /// <returns>A context populated with config, templates, and world entities.</returns>
        private GenerationContext LoadContext()
        {
            int galaxySize = (int)_summary.GalaxySize;
            PlanetSector[] sectors = CopyTemplates(
                    _gameData.PlanetSectors,
                    recursive: true,
                    includeDisabled: true
                )
                .Where(s => (int)s.Visibility <= galaxySize)
                .ToArray();

            return new GenerationContext
            {
                Summary = _summary,
                Config = _gameData.GenerationConfig.GetDeepCopy(),
                GameConfig = _gameData.GameConfig.GetDeepCopy(),
                Rng = _randomProvider,

                Sectors = sectors,
                Factions = _gameData.Factions.GetDeepCopy(),
                Buildings = CopyTemplates(_gameData.Buildings),
                CapitalShips = CopyTemplates(_gameData.CapitalShips),
                Starfighters = CopyTemplates(_gameData.Starfighters),
                Regiments = CopyTemplates(_gameData.Regiments),
                SpecialForces = CopyTemplates(_gameData.SpecialForces),
                Officers = CopyTemplates(_gameData.Officers),
                Events = _gameData.GameEvents.GetDeepCopy(),
            };
        }

        /// <summary>
        /// Creates detached copies of authored scene nodes while preserving their identities.
        /// </summary>
        /// <typeparam name="T">The scene-node template type.</typeparam>
        /// <param name="templates">The authored templates to copy.</param>
        /// <param name="recursive">Whether descendants are copied.</param>
        /// <param name="includeDisabled">Whether disabled descendants are copied.</param>
        /// <returns>The detached scene-node copies.</returns>
        private static T[] CopyTemplates<T>(
            IEnumerable<T> templates,
            bool recursive = false,
            bool includeDisabled = false
        )
            where T : class, ISceneNode
        {
            return templates
                .Select(template =>
                {
                    return (T)template.CreateCopy(recursive, includeDisabled);
                })
                .ToArray();
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
        /// Runs the pre-assembly seeders in generation order.
        /// </summary>
        /// <param name="ctx">The generation context.</param>
        private static void RunSeeders(GenerationContext ctx)
        {
            new GalaxySeeder().Seed(ctx);
            new PlanetSeeder().Seed(ctx);
            new FactionSeeder().Seed(ctx);
            new FacilitySeeder().Seed(ctx);
            new UnitSeeder().Seed(ctx);
            new OfficerSeeder().Seed(ctx);
            new BalanceSeeder().Seed(ctx);
        }

        /// <summary>
        /// Constructs the <see cref="GameRoot"/> from the seeded context state, installs
        /// runtime configuration, and stores the result on the context.
        /// </summary>
        /// <param name="ctx">The generation context.</param>
        private static void AssembleGame(GenerationContext ctx)
        {
            GalaxyMap galaxy = new GalaxyMap();
            foreach (PlanetSector sector in ctx.Sectors)
                galaxy.AddChild(sector);
            GameRoot game = new GameRoot { Summary = ctx.Summary, Random = ctx.Rng };
            game.GetEventPool().AddRange(ctx.Events);
            game.GetFactions().AddRange(ctx.Factions);
            AddPlayers(game);
            game.GetUnrecruitedOfficers().AddRange(ctx.UnrecruitedOfficers);
            game.Galaxy = galaxy;
            game.SetConfig(ctx.GameConfig);
            ctx.Game = game;
        }

        /// <summary>
        /// Creates the participants controlling each generated faction.
        /// </summary>
        /// <param name="game">The generated game receiving its participants.</param>
        private static void AddPlayers(GameRoot game)
        {
            foreach (Faction faction in game.GetFactions())
            {
                bool isHuman = faction.InstanceID == game.Summary.PlayerFactionID;
                game.SetFactionController(
                    faction.InstanceID,
                    isHuman ? _localPlayerID : $"{_aiPlayerIDPrefix}{faction.InstanceID}",
                    isHuman ? PlayerControllerType.Human : PlayerControllerType.AI
                );
            }
        }
    }
}
