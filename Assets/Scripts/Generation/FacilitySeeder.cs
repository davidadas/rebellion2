using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

namespace Rebellion.Generation
{
    /// <summary>
    /// Seeds facilities on populated planets using weighted probability tables.
    /// Reproduces the original game's seed_core_system and seed_rim_system functions.
    /// For each energy slot: check mine probability first, then roll on facility table.
    /// </summary>
    public class FacilitySeeder
    {
        /// <summary>
        /// Seeds all eligible planets with facilities using weighted probability tables.
        /// Configured HQ loadouts are placed before the random roll on their target planet.
        /// </summary>
        /// <param name="systems">All planet systems in the galaxy.</param>
        /// <param name="templates">Building templates to clone from.</param>
        /// <param name="rules">Generation rules containing facility tables and mine multipliers.</param>
        /// <param name="classification">Classification result used to resolve FACTION_HQ loadout entries.</param>
        /// <param name="rng">Random number provider.</param>
        /// <returns>All buildings that were placed during seeding.</returns>
        public List<Building> Seed(
            PlanetSystem[] systems,
            Building[] templates,
            GameGenerationRules rules,
            GalaxyClassificationResult classification,
            IRandomNumberProvider rng
        )
        {
            FacilityGenerationSection config = rules.FacilityGeneration;
            List<Building> deployedBuildings = new List<Building>();

            Dictionary<string, Building> templateMap = templates
                .GroupBy(b => b.TypeID)
                .ToDictionary(g => g.Key, g => g.First());

            WeightedTable<string> coreTable = BuildTable(config.CoreFacilityTable);
            WeightedTable<string> rimTable = BuildTable(config.RimFacilityTable);

            Dictionary<string, List<string>> loadoutsByPlanetId = ResolveHQLoadouts(
                config.HQLoadouts,
                classification
            );

            foreach (PlanetSystem system in systems)
            {
                bool isCore = system.SystemType == PlanetSystemType.CoreSystem;

                foreach (Planet planet in system.Planets)
                {
                    if (!ShouldSeedPlanet(planet, isCore))
                        continue;

                    if (loadoutsByPlanetId.TryGetValue(planet.InstanceID, out List<string> loadout))
                    {
                        PlaceLoadoutFacilities(planet, loadout, templateMap, deployedBuildings);
                    }

                    SeedPlanet(
                        planet,
                        isCore,
                        config,
                        templateMap,
                        isCore ? coreTable : rimTable,
                        rng,
                        deployedBuildings
                    );
                }
            }

            return deployedBuildings;
        }

        /// <summary>
        /// Returns true if the planet should receive facilities.
        /// Core planets are always seeded; rim planets require colonization and an owner.
        /// </summary>
        /// <param name="planet">The planet to check.</param>
        /// <param name="isCore">Whether the planet is in a core system.</param>
        /// <returns>True if the planet is eligible for facility seeding.</returns>
        private bool ShouldSeedPlanet(Planet planet, bool isCore)
        {
            return isCore || planet.IsColonized;
        }

        /// <summary>
        /// Fills a planet's energy slots with facilities. Iterates slots sequentially,
        /// rolling for mine or facility each time. Stops on first roll failure
        /// to match original behavior.
        /// </summary>
        /// <param name="planet">The planet to seed.</param>
        /// <param name="isCore">Whether the planet is in a core system.</param>
        /// <param name="config">Facility generation config (mine multipliers, mine type).</param>
        /// <param name="templateMap">Building templates keyed by TypeID.</param>
        /// <param name="facilityTable">Weighted table for non-mine facility rolls.</param>
        /// <param name="rng">Random number provider.</param>
        /// <param name="deployedBuildings">Accumulator for all placed buildings.</param>
        private void SeedPlanet(
            Planet planet,
            bool isCore,
            FacilityGenerationSection config,
            Dictionary<string, Building> templateMap,
            WeightedTable<string> facilityTable,
            IRandomNumberProvider rng,
            List<Building> deployedBuildings
        )
        {
            int mineCount = 0;
            int mineMultiplier = isCore ? config.CoreMineMultiplier : config.RimMineMultiplier;

            for (int slot = 0; slot < planet.EnergyCapacity; slot++)
            {
                if (planet.GetAvailableEnergy() <= 0)
                    break;

                string typeID = RollFacilityType(
                    planet,
                    config,
                    facilityTable,
                    rng,
                    mineMultiplier,
                    ref mineCount
                );

                // Original terminates the loop on first failure (null table
                // result or missing template), not continue to next slot
                if (typeID == null || !templateMap.TryGetValue(typeID, out Building template))
                    break;

                if (planet.GetAvailableEnergy() <= 0)
                    break;

                Building building = template.GetDeepCopy();
                building.SetOwnerInstanceID(planet.OwnerInstanceID);
                building.SetManufacturingStatus(ManufacturingStatus.Complete);
                building.Movement = null;

                planet.AddChild(building);
                deployedBuildings.Add(building);
            }
        }

        /// <summary>
        /// Determines the facility type for a single slot. Checks mine probability first
        /// (based on remaining raw resource nodes), then falls back to the weighted table.
        /// </summary>
        /// <param name="planet">The planet being seeded (used for raw resource count).</param>
        /// <param name="config">Facility generation config containing the mine TypeID.</param>
        /// <param name="facilityTable">Weighted table for non-mine facility rolls.</param>
        /// <param name="rng">Random number provider.</param>
        /// <param name="mineMultiplier">Core or rim mine probability multiplier.</param>
        /// <param name="mineCount">Running count of mines placed on this planet.</param>
        /// <returns>The TypeID of the facility to place, or null if the table roll failed.</returns>
        private string RollFacilityType(
            Planet planet,
            FacilityGenerationSection config,
            WeightedTable<string> facilityTable,
            IRandomNumberProvider rng,
            int mineMultiplier,
            ref int mineCount
        )
        {
            int mineProbability = (planet.NumRawResourceNodes - mineCount) * mineMultiplier;
            if (mineProbability > 0 && rng.NextInt(0, 100) < mineProbability)
            {
                mineCount++;
                return config.MineTypeID;
            }

            return facilityTable.Roll(rng);
        }

        /// <summary>
        /// Converts weighted facility entries into a WeightedTable for random selection.
        /// </summary>
        /// <param name="entries">Cumulative-weight facility entries from config.</param>
        /// <returns>A WeightedTable that maps rolls to facility TypeIDs.</returns>
        private WeightedTable<string> BuildTable(List<WeightedFacilityEntry> entries)
        {
            List<(int, string)> tableEntries = entries.ConvertAll(e =>
                (e.CumulativeWeight, e.TypeID)
            );
            return new WeightedTable<string>(tableEntries, rollMin: 0, rollMax: 100);
        }

        /// <summary>
        /// Resolves HQ loadout entries into a planet-keyed lookup. "FACTION_HQ"
        /// entries are resolved to the dynamically-picked HQ via the classification.
        /// </summary>
        /// <param name="loadouts">HQ loadout entries from config, may be null.</param>
        /// <param name="classification">Classification result with faction HQ assignments.</param>
        /// <returns>Facility TypeIDs keyed by resolved planet InstanceID.</returns>
        private Dictionary<string, List<string>> ResolveHQLoadouts(
            List<HQFacilityLoadout> loadouts,
            GalaxyClassificationResult classification
        )
        {
            Dictionary<string, List<string>> resolved = new Dictionary<string, List<string>>();
            if (loadouts == null)
                return resolved;

            foreach (HQFacilityLoadout loadout in loadouts)
            {
                string planetId = loadout.PlanetInstanceID;
                if (planetId == GameGenerationRules.FactionHqSentinel)
                {
                    if (string.IsNullOrEmpty(loadout.FactionID))
                        continue;
                    if (
                        !classification.FactionHQs.TryGetValue(
                            loadout.FactionID,
                            out Planet hqPlanet
                        )
                    )
                        continue;
                    planetId = hqPlanet.InstanceID;
                }

                resolved[planetId] = loadout.FacilityTypeIDs ?? new List<string>();
            }

            return resolved;
        }

        /// <summary>
        /// Places configured loadout facilities on a planet. Raises energy capacity and
        /// raw-resource count first so the forced facilities fit without tripping the
        /// Planet's capacity validator. Matches the original HQ seed path.
        /// </summary>
        /// <param name="planet">The target planet.</param>
        /// <param name="facilityTypeIDs">Facility TypeIDs to place in order.</param>
        /// <param name="templateMap">Building templates keyed by TypeID.</param>
        /// <param name="deployedBuildings">Accumulator for all placed buildings.</param>
        private void PlaceLoadoutFacilities(
            Planet planet,
            List<string> facilityTypeIDs,
            Dictionary<string, Building> templateMap,
            List<Building> deployedBuildings
        )
        {
            int validLoadoutCount = facilityTypeIDs.Count(id => templateMap.ContainsKey(id));
            int requiredCapacity = planet.Buildings.Count + validLoadoutCount;
            if (planet.EnergyCapacity < requiredCapacity)
                planet.EnergyCapacity = requiredCapacity;

            int loadoutMineCount = facilityTypeIDs.Count(id =>
                templateMap.TryGetValue(id, out Building template)
                && template.BuildingType == BuildingType.Mine
            );
            int existingMineCount = planet.Buildings.Count(b =>
                b.BuildingType == BuildingType.Mine
            );
            int requiredMines = existingMineCount + loadoutMineCount;
            if (planet.NumRawResourceNodes < requiredMines)
                planet.NumRawResourceNodes = requiredMines;

            foreach (string typeID in facilityTypeIDs)
            {
                if (!templateMap.TryGetValue(typeID, out Building template))
                    continue;

                Building building = template.GetDeepCopy();
                building.SetOwnerInstanceID(planet.OwnerInstanceID);
                building.SetManufacturingStatus(ManufacturingStatus.Complete);
                building.Movement = null;

                planet.AddChild(building);
                deployedBuildings.Add(building);
            }
        }
    }
}
