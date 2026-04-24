using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Results;

namespace Rebellion.Systems
{
    /// <summary>
    /// Accumulates faction material stockpiles each tick from owned planet production.
    /// Income scales with planet popular support and is penalized by blockade unless
    /// the planet has a KDY defense facility. Maintenance cost is deducted from the
    /// refined stockpile.
    /// </summary>
    public class ResourceIncomeSystem : IGameSystem
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Creates a new ResourceIncomeSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public ResourceIncomeSystem(GameRoot game)
        {
            _game = game;
        }

        /// <summary>
        /// Runs per-tick income accumulation for every faction.
        /// </summary>
        /// <returns>An empty result list.</returns>
        public List<GameResult> ProcessTick()
        {
            foreach (Faction faction in _game.GetFactions())
            {
                ApplyFactionIncome(faction);
            }
            return new List<GameResult>();
        }

        /// <summary>
        /// Sums income across the faction's planets, credits the stockpiles, and
        /// deducts maintenance from the refined stockpile.
        /// </summary>
        /// <param name="faction">The faction to update.</param>
        private void ApplyFactionIncome(Faction faction)
        {
            int multiplier = _game.GetConfig().Production.RefinementMultiplier;
            int rawIncome = 0;
            int refinedIncome = 0;

            foreach (Planet planet in GetOwnedColonizedPlanets(faction))
            {
                (int planetRaw, int planetRefined) = ComputePlanetIncome(planet, faction);
                rawIncome += planetRaw;
                refinedIncome += planetRefined;
            }

            faction.RawMaterialStockpile += rawIncome;
            faction.RefinedMaterialStockpile += refinedIncome * multiplier;
            faction.RefinedMaterialStockpile -= faction.GetTotalMaintenanceCost();
        }

        /// <summary>
        /// Computes a single planet's raw and refined income for the owning faction,
        /// after support scaling and blockade penalty (if blockade-penalized).
        /// </summary>
        /// <param name="planet">The producing planet.</param>
        /// <param name="faction">The owning faction.</param>
        /// <returns>A tuple of (raw income, refined income) for this planet, pre-multiplier.</returns>
        private (int raw, int refined) ComputePlanetIncome(Planet planet, Faction faction)
        {
            GameConfig.ProductionConfig production = _game.GetConfig().Production;
            int planetRaw = planet.GetRawMinedResources();
            int planetRefined = ComputePlanetRefinedCapacity(planet);

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

        /// <summary>
        /// Returns the colonized planets owned by a faction.
        /// </summary>
        /// <param name="faction">The faction whose planets to return.</param>
        /// <returns>Enumerable of owned, colonized planets.</returns>
        private IEnumerable<Planet> GetOwnedColonizedPlanets(Faction faction)
        {
            return _game
                .Galaxy.PlanetSystems.SelectMany(s => s.Planets)
                .Where(p => p.IsColonized && p.OwnerInstanceID == faction.InstanceID);
        }

        /// <summary>
        /// Returns the per-tick refined capacity of a planet: min of mines, refineries, and resource nodes.
        /// </summary>
        /// <param name="planet">The planet to measure.</param>
        /// <returns>Refined capacity for this planet, before support and blockade modifiers.</returns>
        private int ComputePlanetRefinedCapacity(Planet planet)
        {
            int mines = planet.GetRawMinedResources();
            int refineries = planet.GetRawRefinementCapacity();
            int resourceNodes = planet.GetRawResourceNodes();
            return System.Math.Min(System.Math.Min(mines, refineries), resourceNodes);
        }
    }
}
