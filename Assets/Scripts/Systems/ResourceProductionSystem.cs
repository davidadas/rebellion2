using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;

namespace Rebellion.Systems
{
    /// <summary>
    /// Produces faction material stockpiles each tick from owned planet output.
    /// </summary>
    public class ResourceProductionSystem : IGameSystem
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Creates a new ResourceProductionSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public ResourceProductionSystem(GameRoot game)
        {
            _game = game;
        }

        /// <summary>
        /// Runs per-tick resource production for every faction.
        /// </summary>
        /// <returns>An empty result list.</returns>
        public List<GameResult> ProcessTick()
        {
            foreach (Faction faction in _game.GetFactions())
            {
                ApplyFactionProduction(faction);
            }
            return new List<GameResult>();
        }

        /// <summary>
        /// Sums production across the faction's planets and credits the material stockpiles.
        /// </summary>
        /// <param name="faction">The faction to update.</param>
        private void ApplyFactionProduction(Faction faction)
        {
            int multiplier = faction.Settings.RefinementMultiplier;
            int rawIncome = 0;
            int refinedIncome = 0;

            foreach (Planet planet in faction.GetOwnedColonizedPlanets())
            {
                (int planetRaw, int planetRefined) = ComputePlanetProduction(planet, faction);
                rawIncome += planetRaw;
                refinedIncome += planetRefined;
            }

            faction.RawMaterialStockpile += rawIncome;
            faction.RefinedMaterialStockpile += refinedIncome * multiplier;
        }

        /// <summary>
        /// Computes a single planet's raw and refined production for the owning faction,
        /// after support scaling and blockade penalty (if blockade-penalized). Only
        /// completed (non-under-construction, stationary) mines and refineries count.
        /// </summary>
        /// <param name="planet">The producing planet.</param>
        /// <param name="faction">The owning faction.</param>
        /// <returns>A tuple of raw and refined production for this planet, pre-multiplier.</returns>
        private (int raw, int refined) ComputePlanetProduction(Planet planet, Faction faction)
        {
            GameConfig.ProductionConfig production = _game.GetConfig().Production;
            int activeMines = planet.GetActiveMinedResources();
            int activeRefineries = planet.GetActiveRefinementCapacity();
            int planetRaw = activeMines;
            int planetRefined = System.Math.Min(activeMines, activeRefineries);

            int supportPercent = planet.GetPopularSupport(faction.InstanceID);
            planetRaw = planetRaw * supportPercent / 100;
            planetRefined = planetRefined * supportPercent / 100;

            if (planet.IsBlockadePenalized())
            {
                int blockadeMod = planet.GetBlockadeModifier(
                    production.BlockadeCapitalShipPenalty,
                    production.BlockadeFighterPenalty
                );
                planetRaw = planetRaw * blockadeMod / 100;
                planetRefined = planetRefined * blockadeMod / 100;
            }

            return (planetRaw, planetRefined);
        }
    }
}
