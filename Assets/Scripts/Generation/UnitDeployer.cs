using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Configuration;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Extensions;

namespace Rebellion.Generation
{
    /// <summary>
    /// Handles all unit deployment during game generation.
    /// Reproduces the original game's 8-step unit seeding pipeline:
    /// 1. Uprising prevention garrisons
    /// 2-4. Fixed garrisons (configured per planet)
    /// 5-6. Fixed fleets (configured per planet)
    /// 7-8. Budget-based deployment (per faction)
    /// </summary>
    public class UnitDeployer
    {
        /// <summary>
        /// Runs the full unit deployment pipeline across all systems.
        /// </summary>
        /// <param name="systems">All planet systems in the galaxy.</param>
        /// <param name="factions">All factions in the game.</param>
        /// <param name="regimentTemplates">Regiment templates to clone from.</param>
        /// <param name="fighterTemplates">Starfighter templates to clone from.</param>
        /// <param name="shipTemplates">Capital ship templates to clone from.</param>
        /// <param name="rules">Generation rules containing garrison/fleet/budget config.</param>
        /// <param name="classification">Galaxy classification result with HQ and bucket mappings.</param>
        /// <param name="gameConfig">Game config for production/maintenance multipliers.</param>
        /// <param name="rng">Random number provider.</param>
        /// <param name="galaxySize">Galaxy size index (0=Small, 1=Medium, 2=Large).</param>
        /// <param name="difficulty">Difficulty index (0=Easy, 1=Medium, 2=Hard).</param>
        /// <param name="playerFactionID">The player's faction ID, used for AI budget scaling.</param>
        public void Deploy(
            PlanetSystem[] systems,
            Faction[] factions,
            Regiment[] regimentTemplates,
            Starfighter[] fighterTemplates,
            CapitalShip[] shipTemplates,
            GameGenerationRules rules,
            GalaxyClassificationResult classification,
            GameConfig gameConfig,
            IRandomNumberProvider rng,
            int galaxySize,
            int difficulty,
            string playerFactionID
        )
        {
            UnitFactory factory = new UnitFactory(regimentTemplates, fighterTemplates, shipTemplates);
            UnitDeploymentSection config = rules.UnitDeployment;

            Dictionary<string, Planet> planetMap = systems
                .SelectMany(s => s.Planets)
                .Where(p => p.InstanceID != null)
                .ToDictionary(p => p.InstanceID);

            DeployUprisingPreventionGarrisons(systems, config, rules.GalaxyClassification, factory, rng);
            DeployFixedGarrisons(config.FixedGarrisons, planetMap, classification, factory);
            DeployFixedFleets(config.FixedFleets, planetMap, factory, factions, rng);
            DeployBudgetUnits(systems, factions, config.FactionBudgets, factory, gameConfig, rng,
                galaxySize, difficulty, playerFactionID);
        }

        /// <summary>
        /// Places garrison troops on colonized planets where owner support is below the
        /// uprising threshold. Troop type is determined per-faction from FactionSetup config.
        /// </summary>
        /// <param name="systems">All planet systems to scan.</param>
        /// <param name="config">Unit deployment config containing the uprising threshold.</param>
        /// <param name="gcConfig">Galaxy classification config with per-faction garrison troop types.</param>
        /// <param name="factory">Unit factory for creating troop instances.</param>
        /// <param name="rng">Random number provider.</param>
        private void DeployUprisingPreventionGarrisons(
            PlanetSystem[] systems,
            UnitDeploymentSection config,
            GalaxyClassificationSection gcConfig,
            UnitFactory factory,
            IRandomNumberProvider rng
        )
        {
            Dictionary<string, string> garrisonTroopMap = new Dictionary<string, string>();
            foreach (FactionSetup setup in gcConfig.FactionSetups)
            {
                if (!string.IsNullOrEmpty(setup.GarrisonTroopTypeID))
                    garrisonTroopMap[setup.FactionID] = setup.GarrisonTroopTypeID;
            }

            foreach (PlanetSystem system in systems)
            {
                foreach (Planet planet in system.Planets)
                {
                    if (string.IsNullOrEmpty(planet.OwnerInstanceID) || !planet.IsColonized)
                        continue;

                    int ownerSupport = planet.GetPopularSupport(planet.OwnerInstanceID);
                    if (ownerSupport >= config.UprisingPreventionThreshold)
                        continue;

                    if (!garrisonTroopMap.TryGetValue(planet.OwnerInstanceID, out string troopTypeID))
                        continue;

                    // Original uses ceiling division: (divisor - 1 + dividend) / divisor
                    int deficit = config.UprisingPreventionThreshold - ownerSupport;
                    int troopsNeeded = (9 + deficit) / 10;

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
        /// Places configured garrisons on specific planets. Supports the FACTION_HQ
        /// placeholder which resolves to the faction's dynamically assigned headquarters.
        /// </summary>
        /// <param name="garrisons">Fixed garrison definitions from config.</param>
        /// <param name="planetMap">Planet lookup by instance ID.</param>
        /// <param name="classification">Classification result containing faction HQ mappings.</param>
        /// <param name="factory">Unit factory for creating unit instances.</param>
        private void DeployFixedGarrisons(
            List<FixedGarrison> garrisons,
            Dictionary<string, Planet> planetMap,
            GalaxyClassificationResult classification,
            UnitFactory factory
        )
        {
            foreach (FixedGarrison garrison in garrisons)
            {
                string planetId = garrison.PlanetInstanceID;
                if (planetId == "FACTION_HQ")
                {
                    if (classification.FactionHQs.TryGetValue(garrison.FactionID, out Planet hqPlanet))
                        planetId = hqPlanet.InstanceID;
                    else
                        continue;
                }

                if (!planetMap.TryGetValue(planetId, out Planet planet))
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
        /// Places configured fleets on specific planets. Each fleet config specifies
        /// capital ships and cargo (fighters/troops loaded into the first ship).
        /// Fleets with a SpawnChancePct below 100 are rolled for inclusion.
        /// </summary>
        /// <param name="fleets">Fixed fleet definitions from config.</param>
        /// <param name="planetMap">Planet lookup by instance ID.</param>
        /// <param name="factory">Unit factory for creating unit instances.</param>
        /// <param name="factions">All factions (needed to call CreateFleet).</param>
        /// <param name="rng">Random number provider for spawn chance rolls.</param>
        private void DeployFixedFleets(
            List<FixedFleet> fleets,
            Dictionary<string, Planet> planetMap,
            UnitFactory factory,
            Faction[] factions,
            IRandomNumberProvider rng
        )
        {
            foreach (FixedFleet fleetConfig in fleets)
            {
                if (!planetMap.TryGetValue(fleetConfig.PlanetInstanceID, out Planet planet))
                    continue;

                if (fleetConfig.SpawnChancePct < 100 && rng.NextInt(0, 100) >= fleetConfig.SpawnChancePct)
                    continue;

                List<CapitalShip> capitalShips = new List<CapitalShip>();
                foreach (UnitEntry entry in fleetConfig.Ships)
                {
                    for (int i = 0; i < entry.Count; i++)
                    {
                        if (factory.Create(entry.TypeID, fleetConfig.FactionID) is CapitalShip ship)
                            capitalShips.Add(ship);
                    }
                }

                if (capitalShips.Count == 0)
                    continue;

                Faction faction = factions.First(f => f.InstanceID == fleetConfig.FactionID);
                Fleet fleet = faction.CreateFleet(null, capitalShips.ToArray(), FleetRoleType.Battle);
                planet.AddChild(fleet);

                CapitalShip cargoShip = capitalShips[0];
                foreach (UnitEntry entry in fleetConfig.Cargo)
                {
                    for (int i = 0; i < entry.Count; i++)
                    {
                        ISceneNode unit = factory.Create(entry.TypeID, fleetConfig.FactionID);
                        if (unit != null)
                            cargoShip.AddChild(unit);
                    }
                }
            }
        }

        /// <summary>
        /// Deploys units for each faction using a maintenance-budget loop.
        /// Budget is calculated from available maintenance capacity, then units are
        /// rolled from a weighted table and placed on random owned core planets
        /// until the budget is exhausted.
        /// </summary>
        /// <param name="systems">All planet systems (for maintenance and planet queries).</param>
        /// <param name="factions">All factions in the game.</param>
        /// <param name="budgets">Per-faction budget configs with unit tables and budget levels.</param>
        /// <param name="factory">Unit factory for creating and costing unit instances.</param>
        /// <param name="gameConfig">Game config for the refinement multiplier.</param>
        /// <param name="rng">Random number provider.</param>
        /// <param name="galaxySize">Galaxy size index for budget level lookup.</param>
        /// <param name="difficulty">Difficulty index for budget level lookup.</param>
        /// <param name="playerFactionID">Player's faction ID for AI budget scaling.</param>
        private void DeployBudgetUnits(
            PlanetSystem[] systems,
            Faction[] factions,
            List<FactionBudget> budgets,
            UnitFactory factory,
            GameConfig gameConfig,
            IRandomNumberProvider rng,
            int galaxySize,
            int difficulty,
            string playerFactionID
        )
        {
            foreach (FactionBudget budget in budgets)
            {
                Faction faction = factions.FirstOrDefault(f => f.InstanceID == budget.FactionID);
                if (faction == null)
                    continue;

                int deployBudget = CalculateDeployBudget(
                    systems, faction, budget, factory, gameConfig, galaxySize, difficulty, playerFactionID
                );
                if (deployBudget <= 0)
                    continue;

                List<Planet> ownedCorePlanets = systems
                    .Where(s => s.SystemType == PlanetSystemType.CoreSystem)
                    .SelectMany(s => s.Planets)
                    .Where(p => p.OwnerInstanceID == faction.InstanceID && p.IsColonized)
                    .ToList();

                if (ownedCorePlanets.Count == 0)
                    continue;

                WeightedTable<List<UnitEntry>> unitTable = new WeightedTable<List<UnitEntry>>(
                    budget.UnitTable.Select(e => (e.CumulativeWeight, e.Units)).ToList(),
                    rollMin: 1,
                    rollMax: 101,
                    fallbackToLast: true
                );

                while (deployBudget > 0)
                {
                    List<UnitEntry> rolledUnits = unitTable.Roll(rng);
                    if (rolledUnits == null || rolledUnits.Count == 0)
                        break;

                    int totalCost = rolledUnits.Sum(e => factory.GetMaintenanceCost(e.TypeID) * e.Count);
                    deployBudget -= totalCost;
                    if (deployBudget < 0)
                        break;

                    Planet targetPlanet = ownedCorePlanets[rng.NextInt(0, ownedCorePlanets.Count)];
                    DeployRolledUnits(targetPlanet, rolledUnits, faction, factory);
                }
            }
        }

        /// <summary>
        /// Calculates the deployment budget for a faction based on available maintenance
        /// capacity. Selects the appropriate budget level from config using galaxy size,
        /// difficulty, and whether the faction is AI-controlled.
        /// </summary>
        /// <param name="systems">All planet systems (for maintenance capacity calculation).</param>
        /// <param name="faction">The faction to calculate budget for.</param>
        /// <param name="budget">The faction's budget config with level entries.</param>
        /// <param name="factory">Unit factory (unused directly, reserved for cost queries).</param>
        /// <param name="gameConfig">Game config for the refinement multiplier.</param>
        /// <param name="galaxySize">Galaxy size index for budget level lookup.</param>
        /// <param name="difficulty">Difficulty index for budget level lookup.</param>
        /// <param name="playerFactionID">Player's faction ID to determine AI status.</param>
        /// <returns>The deployment budget in maintenance cost units.</returns>
        private int CalculateDeployBudget(
            PlanetSystem[] systems,
            Faction faction,
            FactionBudget budget,
            UnitFactory factory,
            GameConfig gameConfig,
            int galaxySize,
            int difficulty,
            string playerFactionID
        )
        {
            bool isAI = playerFactionID != null && faction.InstanceID != playerFactionID;
            int effectiveDifficulty = difficulty >= 2 ? 1 : difficulty;

            BudgetLevel level =
                budget.BudgetLevels.FirstOrDefault(b =>
                    b.GalaxySize == galaxySize && b.Difficulty == effectiveDifficulty && b.IsAI == isAI
                )
                ?? budget.BudgetLevels.FirstOrDefault(b =>
                    b.GalaxySize == galaxySize && b.Difficulty == -1
                )
                ?? budget.BudgetLevels.FirstOrDefault(b => b.GalaxySize == galaxySize)
                ?? budget.BudgetLevels[0];

            int refinementMultiplier = gameConfig.Production.RefinementMultiplier;
            List<Planet> allOwnedPlanets = systems
                .SelectMany(s => s.Planets)
                .Where(p => p.OwnerInstanceID == faction.InstanceID && p.IsColonized)
                .ToList();

            int maintenanceCapacity =
                allOwnedPlanets.Sum(p =>
                {
                    int resourceNodes = p.NumRawResourceNodes;
                    int mines = p.GetRawMinedResources();
                    int refineries = p.GetRawRefinementCapacity();
                    return Math.Min(Math.Min(resourceNodes, mines), refineries);
                }) * refinementMultiplier;

            int maintenanceUsed = CalculateDeployedMaintenanceCost(systems, faction.InstanceID);
            int availableCapacity = Math.Max(0, maintenanceCapacity - maintenanceUsed);

            return availableCapacity * level.Percentage / 100;
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
            List<CapitalShip> shipsForFleet = new List<CapitalShip>();
            List<Starfighter> cargoFighters = new List<Starfighter>();
            List<Regiment> cargoRegiments = new List<Regiment>();

            foreach (UnitEntry entry in entries)
            {
                for (int i = 0; i < entry.Count; i++)
                {
                    ISceneNode unit = factory.Create(entry.TypeID, faction.InstanceID);
                    if (unit is CapitalShip ship)
                        shipsForFleet.Add(ship);
                    else if (unit is Regiment reg)
                        cargoRegiments.Add(reg);
                    else if (unit is Starfighter sf)
                        cargoFighters.Add(sf);
                }
            }

            if (shipsForFleet.Count > 0)
            {
                CapitalShip cargoShip = shipsForFleet[0];
                foreach (Starfighter fighter in cargoFighters)
                    cargoShip.AddChild(fighter);
                foreach (Regiment troop in cargoRegiments)
                    cargoShip.AddChild(troop);

                Fleet existingFleet = planet
                    .GetFleets()
                    .FirstOrDefault(f => f.OwnerInstanceID == faction.InstanceID);

                if (existingFleet != null)
                {
                    foreach (CapitalShip ship in shipsForFleet)
                        existingFleet.AddChild(ship);
                }
                else
                {
                    Fleet newFleet = faction.CreateFleet(null, shipsForFleet.ToArray(), FleetRoleType.Battle);
                    planet.AddChild(newFleet);
                }
            }
            else
            {
                foreach (Regiment troop in cargoRegiments)
                    planet.AddChild(troop);
                foreach (Starfighter fighter in cargoFighters)
                    planet.AddChild(fighter);
            }
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
                        if (child is Building)
                            continue;
                        if (child is IManufacturable m)
                            total += m.GetMaintenanceCost();
                    }

                    foreach (Fleet fleet in planet.GetFleets())
                    {
                        if (fleet.OwnerInstanceID != factionId)
                            continue;
                        foreach (ISceneNode ship in fleet.GetChildren())
                        {
                            if (ship is IManufacturable ms)
                            {
                                total += ms.GetMaintenanceCost();
                                foreach (ISceneNode cargo in ship.GetChildren())
                                {
                                    if (cargo is IManufacturable mc)
                                        total += mc.GetMaintenanceCost();
                                }
                            }
                        }
                    }
                }
            }
            return total;
        }

        /// <summary>
        /// Encapsulates unit template lookups and the create-and-initialize boilerplate.
        /// Holds regiment, starfighter, and capital ship template dictionaries and provides
        /// a single Create method that clones, sets ownership, and marks as complete.
        /// </summary>
        private class UnitFactory
        {
            private readonly Dictionary<string, Regiment> regimentMap;
            private readonly Dictionary<string, Starfighter> fighterMap;
            private readonly Dictionary<string, CapitalShip> shipMap;

            public UnitFactory(
                Regiment[] regiments,
                Starfighter[] fighters,
                CapitalShip[] ships
            )
            {
                regimentMap = regiments.GroupBy(r => r.TypeID).ToDictionary(g => g.Key, g => g.First());
                fighterMap = fighters.GroupBy(s => s.TypeID).ToDictionary(g => g.Key, g => g.First());
                shipMap = ships.GroupBy(s => s.TypeID).ToDictionary(g => g.Key, g => g.First());
            }

            /// <summary>
            /// Creates a new unit instance from a template, setting ownership and marking complete.
            /// </summary>
            /// <param name="typeID">The template TypeID to look up.</param>
            /// <param name="ownerID">The faction ID to assign as owner.</param>
            /// <returns>The created unit, or null if the TypeID was not found in any template map.</returns>
            public ISceneNode Create(string typeID, string ownerID)
            {
                if (shipMap.TryGetValue(typeID, out CapitalShip shipTemplate))
                {
                    CapitalShip ship = shipTemplate.GetDeepCopy();
                    ship.SetOwnerInstanceID(ownerID);
                    ship.ManufacturingStatus = ManufacturingStatus.Complete;
                    ship.Movement = null;
                    return ship;
                }

                if (regimentMap.TryGetValue(typeID, out Regiment regTemplate))
                {
                    Regiment reg = regTemplate.GetDeepCopy();
                    reg.SetOwnerInstanceID(ownerID);
                    reg.ManufacturingStatus = ManufacturingStatus.Complete;
                    reg.Movement = null;
                    return reg;
                }

                if (fighterMap.TryGetValue(typeID, out Starfighter sfTemplate))
                {
                    Starfighter sf = sfTemplate.GetDeepCopy();
                    sf.SetOwnerInstanceID(ownerID);
                    sf.ManufacturingStatus = ManufacturingStatus.Complete;
                    sf.Movement = null;
                    return sf;
                }

                return null;
            }

            /// <summary>
            /// Returns the maintenance cost for a unit type without creating an instance.
            /// </summary>
            /// <param name="typeID">The template TypeID to look up.</param>
            /// <returns>The maintenance cost, or 1 if the TypeID was not found.</returns>
            public int GetMaintenanceCost(string typeID)
            {
                if (shipMap.TryGetValue(typeID, out CapitalShip ship))
                    return ship.MaintenanceCost;
                if (regimentMap.TryGetValue(typeID, out Regiment reg))
                    return reg.MaintenanceCost;
                if (fighterMap.TryGetValue(typeID, out Starfighter sf))
                    return sf.MaintenanceCost;
                return 1;
            }
        }
    }
}
