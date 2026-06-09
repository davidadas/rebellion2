using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

namespace Rebellion.Generation
{
    /// <summary>
    /// Seeds facilities on populated planets using weighted probability tables.
    /// </summary>
    public sealed class FacilitySeeder : IGameSeeder
    {
        /// <summary>
        /// Seeds initial facilities into every eligible planet in the generation context.
        /// </summary>
        /// <param name="ctx">The generation context.</param>
        public void Seed(GenerationContext ctx)
        {
            ctx.DeployedBuildings = SeedFacilities(
                ctx.Systems,
                ctx.Buildings,
                ctx.Config,
                ctx.Classification,
                ctx.Rng
            );
        }

        /// <summary>
        /// Seeds all eligible planets with facilities using weighted probability tables.
        /// HQ loadouts are placed after random seeding on their target planet.
        /// </summary>
        /// <param name="systems">All planet systems in the galaxy.</param>
        /// <param name="templates">Building templates to clone from.</param>
        /// <param name="rules">Generation rules containing facility tables and mine multipliers.</param>
        /// <param name="classification">Classification result used to resolve FACTION_HQ loadout entries.</param>
        /// <param name="rng">Random number provider.</param>
        /// <returns>All buildings placed during seeding.</returns>
        private List<Building> SeedFacilities(
            PlanetSystem[] systems,
            Building[] templates,
            GameGenerationConfig rules,
            GalaxyClassificationResult classification,
            IRandomNumberProvider rng
        )
        {
            FacilityGenerationSection config = rules.FacilityGeneration;
            List<Building> deployedBuildings = new List<Building>();

            Dictionary<string, Building> templateMap = templates
                .GroupBy(b => b.TypeID)
                .ToDictionary(g => g.Key, g => g.First());

            Dictionary<string, List<string>> loadoutsByPlanetId = ResolveHQLoadouts(
                config.HQLoadouts,
                classification,
                systems
            );

            foreach (PlanetSystem system in systems)
            {
                bool isCore = system.SystemType == PlanetSystemType.CoreSystem;

                foreach (Planet planet in system.Planets)
                {
                    if (!isCore && !planet.IsColonized)
                        continue;

                    SeedPlanet(
                        planet,
                        isCore,
                        config,
                        templateMap,
                        isCore ? config.CoreFacilityTable : config.RimFacilityTable,
                        rng,
                        deployedBuildings
                    );
                }
            }

            foreach (PlanetSystem system in systems)
            {
                bool isCore = system.SystemType == PlanetSystemType.CoreSystem;

                foreach (Planet planet in system.Planets)
                {
                    if (!isCore && !planet.IsColonized)
                        continue;

                    if (loadoutsByPlanetId.TryGetValue(planet.InstanceID, out List<string> loadout))
                    {
                        PlaceLoadoutFacilities(planet, loadout, templateMap, deployedBuildings);
                    }
                }
            }

            return deployedBuildings;
        }

        /// <summary>
        /// Fills a planet's energy slots with facilities.
        /// </summary>
        /// <param name="planet">The planet to seed.</param>
        /// <param name="isCore">Whether the planet is in a core system.</param>
        /// <param name="config">Facility generation config (mine multipliers, mine type).</param>
        /// <param name="templateMap">Building templates keyed by TypeID.</param>
        /// <param name="facilityTable">Facility entries for non-mine facility rolls.</param>
        /// <param name="rng">Random number provider.</param>
        /// <param name="deployedBuildings">Accumulator for all placed buildings.</param>
        private void SeedPlanet(
            Planet planet,
            bool isCore,
            FacilityGenerationSection config,
            Dictionary<string, Building> templateMap,
            List<WeightedFacilityEntry> facilityTable,
            IRandomNumberProvider rng,
            List<Building> deployedBuildings
        )
        {
            int mineCount = planet.Buildings.Count(b => b.BuildingType == BuildingType.Mine);
            int mineMultiplier = isCore ? config.CoreMineMultiplier : config.RimMineMultiplier;

            for (int slot = 0; slot < planet.EnergyCapacity; slot++)
            {
                if (planet.GetAvailableEnergy() <= 0)
                    break;

                bool rolled = TryRollFacilityType(
                    planet,
                    config,
                    facilityTable,
                    rng,
                    mineMultiplier,
                    ref mineCount,
                    out string typeID
                );

                if (!rolled)
                    break;

                if (typeID == null)
                    continue;

                if (!templateMap.TryGetValue(typeID, out Building template))
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
        /// <param name="facilityTable">Facility entries for non-mine facility rolls.</param>
        /// <param name="rng">Random number provider.</param>
        /// <param name="mineMultiplier">Core or rim mine probability multiplier.</param>
        /// <param name="mineCount">Running count of mines placed on this planet.</param>
        /// <param name="typeID">The selected TypeID, or null for an empty table result.</param>
        /// <returns>Whether a source table entry was resolved.</returns>
        private bool TryRollFacilityType(
            Planet planet,
            FacilityGenerationSection config,
            List<WeightedFacilityEntry> facilityTable,
            IRandomNumberProvider rng,
            int mineMultiplier,
            ref int mineCount,
            out string typeID
        )
        {
            int mineProbability = (planet.NumRawResourceNodes - mineCount) * mineMultiplier;
            if (mineProbability > 0 && rng.NextInt(0, 100) < mineProbability)
            {
                mineCount++;
                typeID = config.MineTypeID;
                return true;
            }

            return TryRollFacilityTable(config, facilityTable, rng, out typeID);
        }

        /// <summary>
        /// Rolls the facility entry table and returns the selected TypeID.
        /// </summary>
        /// <param name="entries">Cumulative-weight facility entries from config.</param>
        /// <param name="rng">Random number provider.</param>
        /// <param name="typeID">The selected TypeID, or null for an empty table result.</param>
        /// <returns>Whether a table entry was resolved.</returns>
        private bool TryRollFacilityTable(
            FacilityGenerationSection config,
            List<WeightedFacilityEntry> entries,
            IRandomNumberProvider rng,
            out string typeID
        )
        {
            typeID = null;

            if (entries == null || entries.Count == 0)
                return false;

            int roll = rng.NextInt(
                config.FacilityTableRollMin,
                config.FacilityTableRollMaxExclusive
            );
            WeightedFacilityEntry selected = entries[0];

            foreach (WeightedFacilityEntry entry in entries)
            {
                if (roll < entry.CumulativeWeight)
                {
                    typeID = selected.TypeID;
                    return true;
                }

                selected = entry;
            }

            typeID = selected.TypeID;
            return true;
        }

        /// <summary>
        /// Resolves HQ loadout entries into a planet-keyed lookup. "FACTION_HQ"
        /// entries are resolved to the dynamically-picked HQ via the classification.
        /// </summary>
        /// <param name="loadouts">HQ loadout entries from config, may be null.</param>
        /// <param name="classification">Classification result with faction HQ assignments.</param>
        /// <param name="systems">All planet systems containing the target planets.</param>
        /// <returns>Facility TypeIDs keyed by resolved planet InstanceID.</returns>
        private Dictionary<string, List<string>> ResolveHQLoadouts(
            List<HQFacilityLoadout> loadouts,
            GalaxyClassificationResult classification,
            PlanetSystem[] systems
        )
        {
            Dictionary<string, List<string>> resolved = new Dictionary<string, List<string>>();
            if (loadouts == null)
                return resolved;

            Dictionary<string, Planet> planetsByTypeId = systems
                .SelectMany(system => system.Planets)
                .Where(planet => !string.IsNullOrEmpty(planet.TypeID))
                .ToDictionary(planet => planet.TypeID);

            foreach (HQFacilityLoadout loadout in loadouts)
            {
                string planetId = null;
                if (loadout.PlanetTypeID == GameGenerationConfig.FactionHqSentinel)
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
                else
                {
                    if (
                        string.IsNullOrEmpty(loadout.PlanetTypeID)
                        || !planetsByTypeId.TryGetValue(loadout.PlanetTypeID, out Planet planet)
                    )
                        continue;

                    planetId = planet.InstanceID;
                }

                resolved[planetId] = loadout.FacilityTypeIDs ?? new List<string>();
            }

            return resolved;
        }

        /// <summary>
        /// Places configured loadout facilities on a planet. Energy capacity and
        /// raw-resource count are raised so the forced facilities fit without
        /// tripping the planet's capacity validator.
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
