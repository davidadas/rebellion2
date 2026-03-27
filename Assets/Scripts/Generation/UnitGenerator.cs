using System;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;

namespace Rebellion.Generation
{
    /// <summary>
    /// Base class responsible for generating, selecting, decorating,
    /// and deploying units into the scene graph.
    /// </summary>
    /// <typeparam name="TUnit">The unit type to generate.</typeparam>
    public abstract class UnitGenerator<TUnit> : IUnitGenerator<TUnit, PlanetSystem>
        where TUnit : BaseGameEntity
    {
        private readonly GameSummary summary;
        private readonly IResourceManager resourceManager;
        private readonly GameGenerationRules rules;
        private readonly IRandomNumberProvider randomProvider;

        /// <summary>
        /// Constructs a UnitGenerator.
        /// </summary>
        /// <param name="summary">Player-selected game options.</param>
        /// <param name="resourceManager">Resource manager used to load entity definitions and rules.</param>
        /// <param name="randomProvider">Random number provider for deterministic generation.</param>
        public UnitGenerator(
            GameSummary summary,
            IResourceManager resourceManager,
            IRandomNumberProvider randomProvider
        )
        {
            this.summary = summary;
            this.resourceManager = resourceManager;
            this.randomProvider = randomProvider;

            // Load strongly-typed generation rules instead of JSON config
            this.rules = resourceManager.GetConfig<GameGenerationRules>();
        }

        /// <summary>
        /// Gets the GameSummary containing player-selected options.
        /// </summary>
        public GameSummary GetGameSummary()
        {
            return summary;
        }

        /// <summary>
        /// Gets the strongly-typed generation rules.
        /// </summary>
        public GameGenerationRules GetRules()
        {
            return rules;
        }

        /// <summary>
        /// Gets the random number provider for deterministic generation.
        /// </summary>
        protected IRandomNumberProvider GetRandomProvider()
        {
            return randomProvider;
        }

        /// <summary>
        /// Filters the available unit pool based on generation rules.
        /// </summary>
        public abstract TUnit[] SelectUnits(TUnit[] units);

        /// <summary>
        /// Optionally modifies selected units prior to deployment.
        /// </summary>
        public abstract TUnit[] DecorateUnits(TUnit[] units);

        /// <summary>
        /// Deploys units into the scene graph.
        /// </summary>
        public abstract TUnit[] DeployUnits(TUnit[] units, PlanetSystem[] destinations = default);

        /// <summary>
        /// Orchestrates the full unit generation pipeline:
        /// load → select → decorate → deploy.
        /// </summary>
        public IUnitGenerationResults<TUnit> GenerateUnits(PlanetSystem[] destinations = default)
        {
            // Load all unit definitions from static game data.
            TUnit[] unitPool = resourceManager.GetGameData<TUnit>();

            // Apply rule-based selection.
            TUnit[] selectedUnits = SelectUnits(unitPool);

            // Apply optional decoration.
            selectedUnits = DecorateUnits(selectedUnits);

            // Deploy units into the scene graph.
            TUnit[] deployedUnits = DeployUnits(selectedUnits, destinations);

            return new UnitGenerationResults<TUnit>(unitPool, selectedUnits, deployedUnits);
        }
    }
}
