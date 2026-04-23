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
        /// <returns>Income results (currently empty; future hook for logging).</returns>
        public List<GameResult> ProcessTick()
        {
            int multiplier = _game.GetConfig().Production.RefinementMultiplier;
            GameConfig.ProductionConfig production = _game.GetConfig().Production;

            foreach (Faction faction in _game.GetFactions())
            {
                int rawIncome = 0;
                int refinedIncome = 0;

                foreach (Planet planet in GetOwnedColonizedPlanets(faction))
                {
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

                    rawIncome += planetRaw;
                    refinedIncome += planetRefined;
                }

                faction.RawMaterialStockpile += rawIncome;
                faction.RefinedMaterialStockpile += refinedIncome * multiplier;
                faction.RefinedMaterialStockpile -= faction.GetTotalMaintenanceCost();
            }

            return new List<GameResult>();
        }

        /// <summary>
        /// Returns owned, colonized planets for a faction.
        /// </summary>
        /// <param name="faction">The faction whose planets to return.</param>
        /// <returns>Enumerable of planets owned by the faction.</returns>
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
