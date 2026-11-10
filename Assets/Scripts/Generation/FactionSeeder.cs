using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;

namespace Rebellion.Generation
{
    /// <summary>
    /// Seeds faction-side starting state: the highest unlocked research order across
    /// each research discipline, and the unioned research catalog of every buildable
    /// template a faction can ever progress toward.
    /// </summary>
    public sealed class FactionSeeder : IGameSeeder
    {
        /// <summary>
        /// Seeds starting research orders and rebuilds research catalogs for every
        /// faction in the generation context.
        /// </summary>
        /// <param name="ctx">The generation context.</param>
        public void Seed(GenerationContext ctx)
        {
            SetStartingResearch(ctx.Factions, ctx.Config.GalaxyClassification.FactionSetups);
            SetResearchCostScale(ctx.Factions, ctx.Sectors);
            RebuildResearchCatalogs(
                ctx.Factions,
                ctx.Buildings,
                ctx.CapitalShips,
                ctx.Starfighters,
                ctx.Regiments,
                ctx.SpecialForces
            );
        }

        /// <summary>
        /// Scales research costs by the number of generated core planets.
        /// </summary>
        /// <param name="factions">The factions whose research costs are initialized.</param>
        /// <param name="sectors">The generated sectors containing the active planets.</param>
        private static void SetResearchCostScale(Faction[] factions, PlanetSector[] sectors)
        {
            int corePlanetCount = sectors
                .Where(sector => sector.SectorType == PlanetSectorType.Core)
                .Sum(sector => sector.GetChildren<Planet>().Count);
            if (corePlanetCount <= 0)
                throw new InvalidOperationException(
                    "Research cost scaling requires at least one generated core planet."
                );

            foreach (Faction faction in factions)
                faction.ResearchState.CostScalePercent = corePlanetCount;
        }

        /// <summary>
        /// Sets each faction's highest unlocked research order from its generation setup
        /// across all three disciplines.
        /// </summary>
        /// <param name="factions">The factions to initialize.</param>
        /// <param name="setups">The faction-specific generation settings.</param>
        private void SetStartingResearch(Faction[] factions, IReadOnlyList<FactionSetup> setups)
        {
            foreach (Faction faction in factions)
            {
                FactionSetup setup = setups.Single(candidate =>
                    string.Equals(candidate.FactionID, faction.InstanceID, StringComparison.Ordinal)
                );
                int startingOrder = setup.StartingResearchLevel;
                faction.SetHighestUnlockedOrder(ResearchDiscipline.FacilityDesign, startingOrder);
                faction.SetHighestUnlockedOrder(ResearchDiscipline.ShipDesign, startingOrder);
                faction.SetHighestUnlockedOrder(ResearchDiscipline.TroopTraining, startingOrder);
            }
        }

        /// <summary>
        /// Rebuilds each faction's research catalog from the union of every buildable template.
        /// </summary>
        /// <param name="factions">The factions whose catalogs are rebuilt.</param>
        /// <param name="buildings">Building templates.</param>
        /// <param name="capitalShips">Capital-ship templates.</param>
        /// <param name="starfighters">Starfighter templates.</param>
        /// <param name="regiments">Regiment templates.</param>
        /// <param name="specialForces">Special-forces templates.</param>
        private void RebuildResearchCatalogs(
            Faction[] factions,
            Building[] buildings,
            CapitalShip[] capitalShips,
            Starfighter[] starfighters,
            Regiment[] regiments,
            SpecialForces[] specialForces
        )
        {
            IManufacturable[] allTech = buildings
                .Cast<IManufacturable>()
                .Concat(capitalShips)
                .Concat(starfighters)
                .Concat(regiments)
                .Concat(specialForces)
                .ToArray();

            foreach (Faction faction in factions)
            {
                faction.RebuildResearchCatalog(allTech);
            }
        }
    }
}
