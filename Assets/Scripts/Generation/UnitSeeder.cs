using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

namespace Rebellion.Generation
{
    /// <summary>
    /// Seeds starting units across the galaxy: uprising-prevention garrisons,
    /// configured fixed garrisons, configured fixed fleets, and budget-driven
    /// per-faction unit rolls.
    /// </summary>
    public sealed class UnitSeeder : IGameSeeder
    {
        /// <summary>
        /// Seeds starting units (regiments, starfighters, capital ships, fleets) across
        /// every planet in the generation context.
        /// </summary>
        /// <param name="ctx">The generation context.</param>
        public void Seed(GenerationContext ctx)
        {
            UnitFactory factory = new UnitFactory(
                ctx.Regiments,
                ctx.Starfighters,
                ctx.CapitalShips,
                ctx.SpecialForces
            );
            UnitDeploymentSection config = ctx.Config.UnitDeployment;
            Dictionary<string, Planet> planetsByTypeId = BuildPlanetMapByTypeID(ctx.Systems);

            SeedLowSupportGarrisons(ctx.Systems, config, ctx.Config.GalaxyClassification, factory);
            DeployFixedGarrisons(
                config.FixedGarrisons,
                planetsByTypeId,
                ctx.Classification,
                factory
            );
            DeployFixedFleets(
                config.FixedFleets,
                planetsByTypeId,
                ctx.Classification,
                factory,
                ctx.Factions,
                ctx.Rng
            );
            DeployBudgetUnits(ctx, factory, config);
        }

        /// <summary>
        /// Builds a planet lookup keyed by type ID.
        /// </summary>
        /// <param name="systems">All planet systems to scan.</param>
        /// <returns>Planets keyed by type ID.</returns>
        private Dictionary<string, Planet> BuildPlanetMapByTypeID(PlanetSystem[] systems)
        {
            return systems
                .SelectMany(s => s.Planets)
                .Where(p => p.TypeID != null)
                .ToDictionary(p => p.TypeID);
        }

        /// <summary>
        /// Places garrison troops on colonized planets where owner support is below the
        /// uprising threshold. Troop type is determined per-faction from FactionSetup config.
        /// </summary>
        /// <param name="systems">All planet systems to scan.</param>
        /// <param name="config">Unit deployment config containing the uprising threshold.</param>
        /// <param name="gcConfig">Galaxy classification config with per-faction garrison troop types.</param>
        /// <param name="factory">Unit factory for creating troop instances.</param>
        private void SeedLowSupportGarrisons(
            PlanetSystem[] systems,
            UnitDeploymentSection config,
            GalaxyClassificationSection gcConfig,
            UnitFactory factory
        )
        {
            Dictionary<string, string> garrisonTroopMap = BuildGarrisonTroopMap(gcConfig);

            foreach (PlanetSystem system in systems)
            {
                foreach (Planet planet in system.Planets)
                {
                    if (string.IsNullOrEmpty(planet.OwnerInstanceID) || !planet.IsColonized)
                        continue;

                    int ownerSupport = planet.GetPopularSupport(planet.OwnerInstanceID);
                    if (ownerSupport >= config.UprisingPreventionThreshold)
                        continue;

                    if (
                        !garrisonTroopMap.TryGetValue(
                            planet.OwnerInstanceID,
                            out string troopTypeID
                        )
                    )
                        continue;

                    int deficit = config.UprisingPreventionThreshold - ownerSupport;
                    int divisor = config.SupportDeficitPerGarrisonTroop;
                    int troopsNeeded = (deficit + divisor - 1) / divisor;

                    for (int i = 0; i < troopsNeeded; i++)
                    {
                        ISceneNode unit = factory.Create(troopTypeID, planet.OwnerInstanceID);
                        if (unit != null)
                            planet.AddChild(unit);
                    }
                }
            }
        }

        /// <summary>
        /// Builds a faction-to-garrison-troop lookup from generation config.
        /// </summary>
        /// <param name="config">Galaxy classification config.</param>
        /// <returns>Garrison troop TypeIDs keyed by faction ID.</returns>
        private Dictionary<string, string> BuildGarrisonTroopMap(GalaxyClassificationSection config)
        {
            Dictionary<string, string> garrisonTroopMap = new Dictionary<string, string>();
            foreach (FactionSetup setup in config.FactionSetups)
            {
                if (!string.IsNullOrEmpty(setup.GarrisonTroopTypeID))
                    garrisonTroopMap[setup.FactionID] = setup.GarrisonTroopTypeID;
            }

            return garrisonTroopMap;
        }

        /// <summary>
        /// Places configured garrisons on specific planets. Supports the FACTION_HQ
        /// placeholder which resolves to the faction's dynamically assigned headquarters.
        /// </summary>
        /// <param name="garrisons">Fixed garrison definitions from config.</param>
        /// <param name="planetsByTypeId">Planet lookup by type ID.</param>
        /// <param name="classification">Classification result containing faction HQ mappings.</param>
        /// <param name="factory">Unit factory for creating unit instances.</param>
        private void DeployFixedGarrisons(
            List<FixedGarrison> garrisons,
            Dictionary<string, Planet> planetsByTypeId,
            GalaxyClassificationResult classification,
            UnitFactory factory
        )
        {
            foreach (FixedGarrison garrison in garrisons)
            {
                if (
                    !TryResolveTargetPlanet(
                        garrison.PlanetTypeID,
                        garrison.FactionID,
                        classification,
                        planetsByTypeId,
                        out Planet planet
                    )
                )
                    continue;

                foreach (UnitEntry entry in garrison.Units)
                {
                    for (int i = 0; i < entry.Count; i++)
                    {
                        ISceneNode unit = factory.Create(entry.TypeID, garrison.FactionID);
                        if (unit != null)
                            planet.AddChild(unit);
                    }
                }
            }
        }

        /// <summary>
        /// Places configured fleets on specific planets. A TargetPlanets list selects
        /// one destination by type ID or FACTION_HQ; otherwise PlanetTypeID and
        /// SpawnChancePct are used.
        /// </summary>
        /// <param name="fleets">Fixed fleet definitions from config.</param>
        /// <param name="planetsByTypeId">Planet lookup by type ID.</param>
        /// <param name="classification">Classification result containing faction HQ mappings.</param>
        /// <param name="factory">Unit factory for creating unit instances.</param>
        /// <param name="factions">All factions (needed to call CreateFleet).</param>
        /// <param name="rng">Random number provider for destination and spawn rolls.</param>
        private void DeployFixedFleets(
            List<FixedFleet> fleets,
            Dictionary<string, Planet> planetsByTypeId,
            GalaxyClassificationResult classification,
            UnitFactory factory,
            Faction[] factions,
            IRandomNumberProvider rng
        )
        {
            foreach (FixedFleet fleetConfig in fleets)
            {
                if (
                    !TrySelectFixedFleetTarget(
                        fleetConfig,
                        classification,
                        planetsByTypeId,
                        rng,
                        out Planet planet
                    )
                )
                    continue;

                List<CapitalShip> capitalShips = new List<CapitalShip>();
                if (fleetConfig.ShipEntries?.Count > 0)
                {
                    CreateFixedFleetShips(fleetConfig, factory, capitalShips);
                }
                else
                {
                    CreateLegacyFixedFleetShips(fleetConfig, factory, capitalShips);
                    AttachCargoToShip(
                        capitalShips.FirstOrDefault(),
                        fleetConfig.Cargo,
                        fleetConfig,
                        factory
                    );
                }

                if (capitalShips.Count == 0)
                    continue;

                Faction faction = factions.First(f => f.InstanceID == fleetConfig.FactionID);
                Fleet fleet = faction.CreateFleet(capitalShips.ToArray(), FleetRoleType.Battle);
                planet.AddChild(fleet);
            }
        }

        /// <summary>
        /// Resolves the destination planet for a configured fixed fleet.
        /// </summary>
        /// <param name="fleetConfig">The fixed fleet configuration.</param>
        /// <param name="classification">Classification result containing faction HQ mappings.</param>
        /// <param name="planetsByTypeId">Planet lookup by type ID.</param>
        /// <param name="rng">Random number provider for target and spawn rolls.</param>
        /// <param name="planet">The resolved destination planet when selection succeeds.</param>
        /// <returns>True if the fleet should be deployed to a resolved planet.</returns>
        private bool TrySelectFixedFleetTarget(
            FixedFleet fleetConfig,
            GalaxyClassificationResult classification,
            Dictionary<string, Planet> planetsByTypeId,
            IRandomNumberProvider rng,
            out Planet planet
        )
        {
            planet = null;
            List<string> targetPlanets = fleetConfig.TargetPlanets;
            if (targetPlanets?.Count > 0)
            {
                string selectedTarget = targetPlanets[rng.NextInt(0, targetPlanets.Count)];
                if (
                    !TryResolveTargetPlanet(
                        selectedTarget,
                        fleetConfig.FactionID,
                        classification,
                        planetsByTypeId,
                        out planet
                    )
                )
                    return false;

                return true;
            }

            if (
                fleetConfig.SpawnChancePct < 100
                && rng.NextInt(0, 100) >= fleetConfig.SpawnChancePct
            )
                return false;

            if (
                !TryResolveTargetPlanet(
                    fleetConfig.PlanetTypeID,
                    fleetConfig.FactionID,
                    classification,
                    planetsByTypeId,
                    out planet
                )
            )
                return false;

            return true;
        }

        /// <summary>
        /// Resolves a configured planet target to a live planet.
        /// </summary>
        /// <param name="targetPlanetTypeId">The target planet type ID or HQ sentinel.</param>
        /// <param name="factionId">The faction used when resolving an HQ target.</param>
        /// <param name="classification">Classification result containing faction HQ mappings.</param>
        /// <param name="planetsByTypeId">Planet lookup by type ID.</param>
        /// <param name="planet">The resolved planet when lookup succeeds.</param>
        /// <returns>True if the target resolved to a planet.</returns>
        private bool TryResolveTargetPlanet(
            string targetPlanetTypeId,
            string factionId,
            GalaxyClassificationResult classification,
            Dictionary<string, Planet> planetsByTypeId,
            out Planet planet
        )
        {
            planet = null;
            if (string.IsNullOrEmpty(targetPlanetTypeId))
                return false;

            if (targetPlanetTypeId != GameGenerationConfig.FactionHqSentinel)
                return planetsByTypeId.TryGetValue(targetPlanetTypeId, out planet);

            if (!classification.FactionHQs.TryGetValue(factionId, out Planet hqPlanet))
                return false;

            planet = hqPlanet;
            return planet != null;
        }

        /// <summary>
        /// Creates ships from structured fixed-fleet entries.
        /// </summary>
        /// <param name="fleetConfig">The fixed fleet configuration.</param>
        /// <param name="factory">Unit factory for creating unit instances.</param>
        /// <param name="capitalShips">Accumulator for created capital ships.</param>
        private void CreateFixedFleetShips(
            FixedFleet fleetConfig,
            UnitFactory factory,
            List<CapitalShip> capitalShips
        )
        {
            foreach (FixedFleetShip entry in fleetConfig.ShipEntries)
            {
                for (int i = 0; i < entry.Count; i++)
                {
                    if (factory.Create(entry.TypeID, fleetConfig.FactionID) is not CapitalShip ship)
                        continue;

                    AttachCargoToShip(ship, entry.Cargo, fleetConfig, factory);
                    capitalShips.Add(ship);
                }
            }
        }

        /// <summary>
        /// Creates ships from legacy fixed-fleet ship entries.
        /// </summary>
        /// <param name="fleetConfig">The fixed fleet configuration.</param>
        /// <param name="factory">Unit factory for creating unit instances.</param>
        /// <param name="capitalShips">Accumulator for created capital ships.</param>
        private void CreateLegacyFixedFleetShips(
            FixedFleet fleetConfig,
            UnitFactory factory,
            List<CapitalShip> capitalShips
        )
        {
            foreach (UnitEntry entry in fleetConfig.Ships ?? new List<UnitEntry>())
            {
                for (int i = 0; i < entry.Count; i++)
                {
                    if (factory.Create(entry.TypeID, fleetConfig.FactionID) is CapitalShip ship)
                        capitalShips.Add(ship);
                }
            }
        }

        /// <summary>
        /// Attaches configured cargo units to a capital ship.
        /// </summary>
        /// <param name="ship">The ship receiving cargo.</param>
        /// <param name="cargo">The cargo unit entries.</param>
        /// <param name="fleetConfig">The owning fixed fleet configuration.</param>
        /// <param name="factory">Unit factory for creating unit instances.</param>
        private void AttachCargoToShip(
            CapitalShip ship,
            List<UnitEntry> cargo,
            FixedFleet fleetConfig,
            UnitFactory factory
        )
        {
            if (ship == null || cargo == null)
                return;

            foreach (UnitEntry entry in cargo)
            {
                for (int i = 0; i < entry.Count; i++)
                {
                    ISceneNode unit = factory.Create(entry.TypeID, fleetConfig.FactionID);
                    if (unit != null)
                        ship.AddChild(unit);
                }
            }
        }

        /// <summary>
        /// Deploys units for each faction using a maintenance-budget loop.
        /// Budget is calculated from available maintenance capacity, then units are
        /// rolled from a weighted table and placed on random owned core planets
        /// until the budget is exhausted.
        /// </summary>
        /// <param name="ctx">The generation context.</param>
        /// <param name="factory">Unit factory for creating and costing unit instances.</param>
        /// <param name="config">Unit deployment config.</param>
        private void DeployBudgetUnits(
            GenerationContext ctx,
            UnitFactory factory,
            UnitDeploymentSection config
        )
        {
            foreach (FactionBudget budget in config.FactionBudgets)
            {
                Faction faction = ctx.Factions.FirstOrDefault(f =>
                    f.InstanceID == budget.FactionID
                );
                if (faction == null)
                    continue;

                int deployBudget = CalculateDeployBudget(ctx, faction, budget, config);
                if (deployBudget <= 0)
                    continue;

                List<Planet> ownedCorePlanets = GetOwnedCorePlanets(ctx.Systems, faction);

                if (ownedCorePlanets.Count == 0)
                    continue;

                while (deployBudget > 0)
                {
                    bool deployed = TryDeployBudgetRoll(
                        ctx,
                        budget.UnitTable,
                        ownedCorePlanets,
                        faction,
                        factory,
                        ref deployBudget
                    );
                    if (!deployed)
                        break;
                }
            }
        }

        /// <summary>
        /// Rolls one budget deployment and places it if the remaining budget can pay for it.
        /// </summary>
        /// <param name="ctx">The generation context.</param>
        /// <param name="unitTable">Weighted unit table for the faction.</param>
        /// <param name="targetPlanets">Planets eligible to receive rolled units.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="factory">Unit factory for creating and costing unit instances.</param>
        /// <param name="deployBudget">Remaining deployment budget.</param>
        /// <returns>True when a roll was deployed and the loop may continue.</returns>
        private bool TryDeployBudgetRoll(
            GenerationContext ctx,
            List<WeightedUnitEntry> unitTable,
            List<Planet> targetPlanets,
            Faction faction,
            UnitFactory factory,
            ref int deployBudget
        )
        {
            List<UnitEntry> rolledUnits = RollBudgetUnits(unitTable, ctx.Rng);
            if (rolledUnits == null || rolledUnits.Count == 0)
                return false;

            int totalCost = CalculateRolledMaintenanceCost(rolledUnits, factory);
            if (totalCost <= 0 || totalCost > deployBudget)
                return false;

            deployBudget -= totalCost;
            DeployRolledUnits(
                SelectBudgetTargetPlanet(targetPlanets, ctx.Rng),
                rolledUnits,
                faction,
                factory
            );
            return true;
        }

        /// <summary>
        /// Rolls a unit bundle from a faction budget table.
        /// </summary>
        /// <param name="unitTable">Weighted unit table to roll against.</param>
        /// <param name="rng">Random number provider.</param>
        /// <returns>The selected unit entries, or null when no table is available.</returns>
        private List<UnitEntry> RollBudgetUnits(
            List<WeightedUnitEntry> unitTable,
            IRandomNumberProvider rng
        )
        {
            if (unitTable == null || unitTable.Count == 0)
                return null;

            int roll = rng.NextInt(1, 101);
            WeightedUnitEntry selected = unitTable[0];
            foreach (WeightedUnitEntry entry in unitTable)
            {
                if (roll < entry.CumulativeWeight)
                    return selected.Units;

                selected = entry;
            }

            return selected.Units;
        }

        /// <summary>
        /// Calculates the maintenance cost for a rolled unit bundle.
        /// </summary>
        /// <param name="entries">Rolled unit entries.</param>
        /// <param name="factory">Unit factory used to look up maintenance costs.</param>
        /// <returns>The total maintenance cost.</returns>
        private int CalculateRolledMaintenanceCost(List<UnitEntry> entries, UnitFactory factory)
        {
            return entries.Sum(e => factory.GetMaintenanceCost(e.TypeID) * e.Count);
        }

        /// <summary>
        /// Selects the planet that receives a budget deployment roll.
        /// </summary>
        /// <param name="targetPlanets">Planets eligible to receive rolled units.</param>
        /// <param name="rng">Random number provider.</param>
        /// <returns>The selected target planet.</returns>
        private Planet SelectBudgetTargetPlanet(
            List<Planet> targetPlanets,
            IRandomNumberProvider rng
        )
        {
            return targetPlanets[rng.NextInt(0, targetPlanets.Count)];
        }

        /// <summary>
        /// Returns owned, colonized core planets for a faction.
        /// </summary>
        /// <param name="systems">All planet systems to scan.</param>
        /// <param name="faction">The faction whose planets are returned.</param>
        /// <returns>Owned, colonized core planets.</returns>
        private List<Planet> GetOwnedCorePlanets(PlanetSystem[] systems, Faction faction)
        {
            return systems
                .Where(s => s.SystemType == PlanetSystemType.CoreSystem)
                .SelectMany(s => s.Planets)
                .Where(p => p.OwnerInstanceID == faction.InstanceID && p.IsColonized)
                .ToList();
        }

        /// <summary>
        /// Calculates the deployment budget for a faction based on available maintenance
        /// capacity. Selects the appropriate budget level from config using galaxy size,
        /// difficulty, and whether the faction is AI-controlled.
        /// </summary>
        /// <param name="ctx">The generation context.</param>
        /// <param name="faction">The faction to calculate budget for.</param>
        /// <param name="budget">The faction's budget config with level entries.</param>
        /// <param name="config">Unit deployment config.</param>
        /// <returns>The deployment budget in maintenance cost units.</returns>
        private int CalculateDeployBudget(
            GenerationContext ctx,
            Faction faction,
            FactionBudget budget,
            UnitDeploymentSection config
        )
        {
            bool isAI =
                ctx.Summary.PlayerFactionID != null
                && faction.InstanceID != ctx.Summary.PlayerFactionID;
            int effectiveDifficulty = ResolveBudgetDifficulty(config, (int)ctx.Summary.Difficulty);
            BudgetLevel level = ResolveBudgetLevel(
                budget,
                (int)ctx.Summary.GalaxySize,
                effectiveDifficulty,
                isAI
            );
            int maintenanceCapacity = CalculateMaintenanceCapacity(ctx.Systems, faction);
            int maintenanceUsed = CalculateDeployedMaintenanceCost(ctx.Systems, faction.InstanceID);
            int availableCapacity = Math.Max(0, maintenanceCapacity - maintenanceUsed);

            return availableCapacity * level.Percentage / 100;
        }

        /// <summary>
        /// Resolves the budget difficulty used by unit deployment.
        /// </summary>
        /// <param name="config">Unit deployment config.</param>
        /// <param name="difficulty">Requested game difficulty.</param>
        /// <returns>The mapped budget difficulty.</returns>
        private int ResolveBudgetDifficulty(UnitDeploymentSection config, int difficulty)
        {
            BudgetDifficultyMapping mapping = config.BudgetDifficultyMappings?.FirstOrDefault(m =>
                m.Difficulty == difficulty
            );

            return mapping?.BudgetDifficulty ?? difficulty;
        }

        /// <summary>
        /// Resolves the budget level that best matches the generation parameters.
        /// </summary>
        /// <param name="budget">The faction budget configuration.</param>
        /// <param name="galaxySize">Galaxy size index.</param>
        /// <param name="difficulty">Difficulty index.</param>
        /// <param name="isAI">Whether the faction is AI-controlled.</param>
        /// <returns>The selected budget level.</returns>
        private BudgetLevel ResolveBudgetLevel(
            FactionBudget budget,
            int galaxySize,
            int difficulty,
            bool isAI
        )
        {
            return budget.BudgetLevels.FirstOrDefault(b =>
                    b.GalaxySize == galaxySize && b.Difficulty == difficulty && b.IsAI == isAI
                )
                ?? budget.BudgetLevels.FirstOrDefault(b =>
                    b.GalaxySize == galaxySize && b.Difficulty == -1
                )
                ?? budget.BudgetLevels.FirstOrDefault(b => b.GalaxySize == galaxySize)
                ?? budget.BudgetLevels[0];
        }

        /// <summary>
        /// Calculates a faction's starting maintenance capacity from owned resources.
        /// </summary>
        /// <param name="systems">All planet systems to scan.</param>
        /// <param name="faction">The faction to calculate capacity for.</param>
        /// <returns>The starting maintenance capacity.</returns>
        private int CalculateMaintenanceCapacity(PlanetSystem[] systems, Faction faction)
        {
            int refinementMultiplier = faction.Settings.RefinementMultiplier;
            return systems
                    .SelectMany(s => s.Planets)
                    .Where(p => p.OwnerInstanceID == faction.InstanceID && p.IsColonized)
                    .Sum(GetPlanetMaintenanceCapacity) * refinementMultiplier;
        }

        /// <summary>
        /// Gets the maintenance capacity contributed by one planet before faction multiplier.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>The planet's maintenance capacity.</returns>
        private static int GetPlanetMaintenanceCapacity(Planet planet)
        {
            int resourceNodes = planet.NumRawResourceNodes;
            int mines = planet.GetRawMinedResources();
            int refineries = planet.GetRawRefinementCapacity();
            return Math.Min(Math.Min(resourceNodes, mines), refineries);
        }

        /// <summary>
        /// Places a rolled set of units on a planet. Capital ships are assembled into
        /// a fleet (joining an existing friendly fleet if present), with fighters and
        /// regiments loaded as cargo into the first ship. Ground-only rolls are placed
        /// directly on the planet.
        /// </summary>
        /// <param name="planet">The target planet.</param>
        /// <param name="entries">Unit entries rolled from the weighted table.</param>
        /// <param name="faction">The owning faction (needed to create fleets).</param>
        /// <param name="factory">Unit factory for creating unit instances.</param>
        private void DeployRolledUnits(
            Planet planet,
            List<UnitEntry> entries,
            Faction faction,
            UnitFactory factory
        )
        {
            (
                List<CapitalShip> capitalShips,
                List<Starfighter> starfighters,
                List<Regiment> regiments,
                List<SpecialForces> specialForces
            ) = CreateRolledUnits(entries, faction, factory);

            if (capitalShips.Count > 0)
                DeployToFleets(planet, faction, capitalShips, starfighters, regiments);
            else
                DeployPlanetUnits(planet, starfighters, regiments, specialForces);
        }

        /// <summary>
        /// Creates units for a rolled budget table entry and groups them by deployment role.
        /// </summary>
        /// <param name="entries">Unit entries rolled from the weighted table.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="factory">Unit factory for creating unit instances.</param>
        /// <returns>Created capital ships, starfighters, regiments, and special forces.</returns>
        private (
            List<CapitalShip> capitalShips,
            List<Starfighter> starfighters,
            List<Regiment> regiments,
            List<SpecialForces> specialForces
        ) CreateRolledUnits(List<UnitEntry> entries, Faction faction, UnitFactory factory)
        {
            List<CapitalShip> capitalShips = new List<CapitalShip>();
            List<Starfighter> starfighters = new List<Starfighter>();
            List<Regiment> regiments = new List<Regiment>();
            List<SpecialForces> specialForces = new List<SpecialForces>();

            foreach (UnitEntry entry in entries)
            {
                for (int i = 0; i < entry.Count; i++)
                {
                    ISceneNode unit = factory.Create(entry.TypeID, faction.InstanceID);
                    if (unit is CapitalShip ship)
                        capitalShips.Add(ship);
                    else if (unit is Regiment regiment)
                        regiments.Add(regiment);
                    else if (unit is Starfighter starfighter)
                        starfighters.Add(starfighter);
                    else if (unit is SpecialForces specialForce)
                        specialForces.Add(specialForce);
                }
            }

            return (capitalShips, starfighters, regiments, specialForces);
        }

        /// <summary>
        /// Deploys capital ships to an existing or newly created fleet.
        /// </summary>
        /// <param name="planet">The planet receiving the fleet units.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="capitalShips">Capital ships to attach to a fleet.</param>
        /// <param name="starfighters">Starfighters to load as cargo.</param>
        /// <param name="regiments">Regiments to load as cargo.</param>
        private void DeployToFleets(
            Planet planet,
            Faction faction,
            List<CapitalShip> capitalShips,
            List<Starfighter> starfighters,
            List<Regiment> regiments
        )
        {
            AttachUnitsToShip(capitalShips[0], starfighters, regiments);

            Fleet existingFleet = planet
                .GetFleets()
                .FirstOrDefault(f => f.OwnerInstanceID == faction.InstanceID);

            if (existingFleet != null)
            {
                foreach (CapitalShip ship in capitalShips)
                    existingFleet.AddChild(ship);
                return;
            }

            Fleet newFleet = faction.CreateFleet(capitalShips.ToArray(), FleetRoleType.Battle);
            planet.AddChild(newFleet);
        }

        /// <summary>
        /// Attaches starfighters and regiments to a capital ship.
        /// </summary>
        /// <param name="ship">The ship receiving cargo.</param>
        /// <param name="starfighters">Starfighters to load.</param>
        /// <param name="regiments">Regiments to load.</param>
        private void AttachUnitsToShip(
            CapitalShip ship,
            List<Starfighter> starfighters,
            List<Regiment> regiments
        )
        {
            foreach (Starfighter starfighter in starfighters)
                ship.AddChild(starfighter);
            foreach (Regiment regiment in regiments)
                ship.AddChild(regiment);
        }

        /// <summary>
        /// Deploys non-fleet rolled units directly to the planet.
        /// </summary>
        /// <param name="planet">The planet receiving the units.</param>
        /// <param name="starfighters">Starfighters to place.</param>
        /// <param name="regiments">Regiments to place.</param>
        /// <param name="specialForces">Special forces to place.</param>
        private void DeployPlanetUnits(
            Planet planet,
            List<Starfighter> starfighters,
            List<Regiment> regiments,
            List<SpecialForces> specialForces
        )
        {
            foreach (Regiment regiment in regiments)
                planet.AddChild(regiment);
            foreach (SpecialForces specialForce in specialForces)
                planet.AddChild(specialForce);
            foreach (Starfighter starfighter in starfighters)
                planet.AddChild(starfighter);
        }

        /// <summary>
        /// Sums the maintenance cost of all deployed units owned by a faction,
        /// including ground units on planets and all units inside fleets.
        /// Buildings are excluded (they provide capacity, not consume it).
        /// </summary>
        /// <param name="systems">All planet systems to scan.</param>
        /// <param name="factionId">The faction whose maintenance cost to calculate.</param>
        /// <returns>Total maintenance cost of all deployed units for the faction.</returns>
        private int CalculateDeployedMaintenanceCost(PlanetSystem[] systems, string factionId)
        {
            int total = 0;
            foreach (PlanetSystem system in systems)
            {
                foreach (Planet planet in system.Planets)
                {
                    if (planet.OwnerInstanceID != factionId)
                        continue;

                    foreach (ISceneNode child in planet.GetChildren())
                    {
                        total += GetPlanetChildMaintenanceCost(child);
                    }

                    foreach (Fleet fleet in planet.GetFleets())
                    {
                        if (fleet.OwnerInstanceID != factionId)
                            continue;
                        foreach (ISceneNode ship in fleet.GetChildren())
                        {
                            total += GetShipAndCargoMaintenanceCost(ship);
                        }
                    }
                }
            }
            return total;
        }

        /// <summary>
        /// Gets maintenance cost for a direct planet child during generation budgeting.
        /// </summary>
        /// <param name="child">The planet child to inspect.</param>
        /// <returns>The maintenance cost, or 0 for non-unit children.</returns>
        private static int GetPlanetChildMaintenanceCost(ISceneNode child)
        {
            if (child is Building)
                return 0;

            return child is IManufacturable manufacturable
                ? manufacturable.GetMaintenanceCost()
                : 0;
        }

        /// <summary>
        /// Gets maintenance cost for a ship and its loaded cargo.
        /// </summary>
        /// <param name="ship">The fleet child to inspect.</param>
        /// <returns>The ship and cargo maintenance cost.</returns>
        private static int GetShipAndCargoMaintenanceCost(ISceneNode ship)
        {
            if (ship is not IManufacturable manufacturable)
                return 0;

            int total = manufacturable.GetMaintenanceCost();
            foreach (ISceneNode cargo in ship.GetChildren())
            {
                if (cargo is IManufacturable cargoManufacturable)
                    total += cargoManufacturable.GetMaintenanceCost();
            }

            return total;
        }

        /// <summary>
        /// Encapsulates unit template lookups and the create-and-initialize boilerplate.
        /// Holds unit template dictionaries and provides
        /// a single Create method that clones, sets ownership, and marks as complete.
        /// </summary>
        private class UnitFactory
        {
            private readonly Dictionary<string, Regiment> _regimentMap;
            private readonly Dictionary<string, Starfighter> _fighterMap;
            private readonly Dictionary<string, CapitalShip> _shipMap;
            private readonly Dictionary<string, SpecialForces> _specialForcesMap;

            /// <summary>
            /// Creates a unit factory from generation templates.
            /// </summary>
            /// <param name="regiments">Regiment templates keyed during construction.</param>
            /// <param name="fighters">Starfighter templates keyed during construction.</param>
            /// <param name="ships">Capital ship templates keyed during construction.</param>
            /// <param name="specialForces">Special-forces templates keyed during construction.</param>
            public UnitFactory(
                Regiment[] regiments,
                Starfighter[] fighters,
                CapitalShip[] ships,
                SpecialForces[] specialForces
            )
            {
                _regimentMap = regiments
                    .GroupBy(r => r.TypeID)
                    .ToDictionary(g => g.Key, g => g.First());
                _fighterMap = fighters
                    .GroupBy(s => s.TypeID)
                    .ToDictionary(g => g.Key, g => g.First());
                _shipMap = ships.GroupBy(s => s.TypeID).ToDictionary(g => g.Key, g => g.First());
                _specialForcesMap = specialForces
                    .GroupBy(s => s.TypeID)
                    .ToDictionary(g => g.Key, g => g.First());
            }

            /// <summary>
            /// Creates a new unit instance from a template, setting ownership and marking complete.
            /// </summary>
            /// <param name="typeID">The template TypeID to look up.</param>
            /// <param name="ownerID">The faction ID to assign as owner.</param>
            /// <returns>The created unit.</returns>
            public ISceneNode Create(string typeID, string ownerID)
            {
                if (_shipMap.TryGetValue(typeID, out CapitalShip shipTemplate))
                {
                    CapitalShip ship = shipTemplate.GetDeepCopy();
                    ship.SetOwnerInstanceID(ownerID);
                    ship.ManufacturingStatus = ManufacturingStatus.Complete;
                    ship.Movement = null;
                    return ship;
                }

                if (_regimentMap.TryGetValue(typeID, out Regiment regTemplate))
                {
                    Regiment reg = regTemplate.GetDeepCopy();
                    reg.SetOwnerInstanceID(ownerID);
                    reg.ManufacturingStatus = ManufacturingStatus.Complete;
                    reg.Movement = null;
                    return reg;
                }

                if (_fighterMap.TryGetValue(typeID, out Starfighter sfTemplate))
                {
                    Starfighter sf = sfTemplate.GetDeepCopy();
                    sf.SetOwnerInstanceID(ownerID);
                    sf.ManufacturingStatus = ManufacturingStatus.Complete;
                    sf.Movement = null;
                    return sf;
                }

                if (_specialForcesMap.TryGetValue(typeID, out SpecialForces specialForcesTemplate))
                {
                    SpecialForces specialForces = specialForcesTemplate.GetDeepCopy();
                    specialForces.SetOwnerInstanceID(ownerID);
                    specialForces.ManufacturingStatus = ManufacturingStatus.Complete;
                    specialForces.Movement = null;
                    return specialForces;
                }

                throw new InvalidOperationException(
                    $"Unit type '{typeID}' is not present in generation unit templates."
                );
            }

            /// <summary>
            /// Returns the maintenance cost for a unit type without creating an instance.
            /// </summary>
            /// <param name="typeID">The template TypeID to look up.</param>
            /// <returns>The maintenance cost.</returns>
            public int GetMaintenanceCost(string typeID)
            {
                if (_shipMap.TryGetValue(typeID, out CapitalShip ship))
                    return ship.MaintenanceCost;
                if (_regimentMap.TryGetValue(typeID, out Regiment reg))
                    return reg.MaintenanceCost;
                if (_fighterMap.TryGetValue(typeID, out Starfighter sf))
                    return sf.MaintenanceCost;
                if (_specialForcesMap.TryGetValue(typeID, out SpecialForces specialForces))
                    return specialForces.MaintenanceCost;

                throw new InvalidOperationException(
                    $"Unit type '{typeID}' is not present in generation unit templates."
                );
            }
        }
    }
}
