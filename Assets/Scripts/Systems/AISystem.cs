using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

/// <summary>
/// Manages AI behavior for factions in the game.
/// </summary>
namespace Rebellion.Systems
{
    public class AISystem
    {
        private readonly GameRoot game;
        private readonly MissionSystem missionManager;
        private readonly MovementSystem movementManager;
        private readonly ManufacturingSystem manufacturingManager;
        private readonly IRandomNumberProvider randomProvider;

        /// <summary>
        /// Creates a new AISystem.
        /// </summary>
        public AISystem(
            GameRoot game,
            MissionSystem missionManager,
            MovementSystem movementManager,
            ManufacturingSystem manufacturingManager,
            IRandomNumberProvider randomProvider
        )
        {
            this.game = game;
            this.missionManager = missionManager;
            this.movementManager = movementManager;
            this.manufacturingManager = manufacturingManager;
            this.randomProvider = randomProvider;
        }

        /// <summary>
        /// Advances AI logic for all AI-controlled factions by one tick.
        /// Only runs every TickInterval ticks, matching the original game.
        /// </summary>
        public void ProcessTick()
        {
            if (game.CurrentTick % game.Config.AI.TickInterval != 0)
                return;

            foreach (Faction faction in game.Factions.Where(f => f.IsAIControlled()))
            {
                UpdateFaction(faction);
            }
        }

        /// <summary>
        /// Runs the AI decision cycle for one faction.
        /// Order is intentional: crises first, economy before military, missions last.
        /// </summary>
        private void UpdateFaction(Faction faction)
        {
            HandleUprisings(faction);
            HandleBlockades(faction);
            UpdateEconomy(faction);
            UpdateCapitalShipProduction(faction);
            ProcessCapitalShipIssues(faction);
            UpdateStarfighterProduction(faction);
            UpdateTroopTraining(faction);
            ManageGarrisons(faction);
            EvaluateFleetDeployment(faction);
            UpdateOfficerMissions(faction);
            ProcessResourceRebalancing(faction);
        }

        /// <summary>
        /// Sends the best available leader to suppress each owned planet in uprising.
        /// </summary>
        private void HandleUprisings(Faction faction)
        {
            List<Planet> risingPlanets = faction
                .GetOwnedUnitsByType<Planet>()
                .Where(p => p.IsInUprising)
                .ToList();

            foreach (Planet planet in risingPlanets)
            {
                // Ownership may have changed since the list was built (e.g. a diplomacy mission
                // succeeded mid-tick). Skip rather than throw.
                if (planet.GetOwnerInstanceID() != faction.InstanceID)
                    continue;

                Officer leader = faction
                    .GetAvailableOfficers(faction)
                    .Where(o => o.IsMovable())
                    .OrderByDescending(o => o.GetSkillValue(MissionParticipantSkill.Leadership))
                    .FirstOrDefault();

                if (leader == null)
                    break;

                missionManager.InitiateMission(MissionType.SubdueUprising, leader, planet, randomProvider);
            }
        }

        /// <summary>
        /// Routes the nearest idle fleet toward each blockaded owned planet.
        /// Tracks dispatched fleets so the same fleet isn't sent twice.
        /// </summary>
        private void HandleBlockades(Faction faction)
        {
            List<Planet> blockaded = faction
                .GetOwnedUnitsByType<Planet>()
                .Where(p => p.IsBlockaded())
                .OrderByDescending(p => p.GetRawResourceNodes())
                .ToList();

            HashSet<string> dispatched = new HashSet<string>();

            foreach (Planet planet in blockaded)
            {
                Fleet relief = faction
                    .GetOwnedUnitsByType<Fleet>()
                    .Where(f =>
                        f.IsMovable()
                        && f.CapitalShips.Count > 0
                        && !dispatched.Contains(f.GetInstanceID())
                    )
                    .OrderBy(f => f.GetParentOfType<Planet>()?.GetRawDistanceTo(planet) ?? int.MaxValue)
                    .FirstOrDefault();

                if (relief == null)
                    break;

                movementManager.RequestMove(relief, planet);
                dispatched.Add(relief.GetInstanceID());
            }
        }

        /// <summary>
        /// Builds buildings in priority order: mines to match raw nodes, then
        /// refineries to match mine output, then production and defense facilities.
        /// </summary>
        private void UpdateEconomy(Faction faction)
        {
            if (faction.GetTotalRawMinedResources() < faction.GetTotalRawResourceNodes())
            {
                BuildOneOf(faction, BuildingType.Mine);
            }
            else if (faction.GetTotalRawRefinementCapacity() < faction.GetTotalRawMinedResources())
            {
                BuildOneOf(faction, BuildingType.Refinery);
            }
            else
            {
                foreach (
                    BuildingType facilityType in new[]
                    {
                        BuildingType.ConstructionFacility,
                        BuildingType.Shipyard,
                        BuildingType.TrainingFacility,
                        BuildingType.Defense,
                    }
                )
                {
                    if (BuildOneOf(faction, facilityType))
                        break;
                }
            }
        }

        /// <summary>
        /// Enqueues one building of the given type on the best available construction yard.
        /// Returns true if a building was successfully enqueued.
        /// </summary>
        private bool BuildOneOf(Faction faction, BuildingType buildingType)
        {
            Technology tech = GetHighestTierTechnology(
                faction,
                ManufacturingType.Building,
                buildingType
            );
            if (tech == null)
                return false;

            Building templateBuilding = (Building)tech.GetReference();

            if (game.GetRefinedMaterials(faction) < tech.GetReference().GetConstructionCost())
                return false;

            // Find planets that have both an idle construction yard AND energy capacity
            List<Planet> candidates = faction
                .GetIdleFacilities(ManufacturingType.Building)
                .Where(p => p.GetAvailableEnergy() > 0)
                .ToList();

            if (!candidates.Any())
                return false;

            // Pick the best candidate planet
            Planet target = candidates
                .OrderBy(p => CalculateBuildingPriority(p, buildingType))
                .ThenByDescending(p => p.GetAvailableEnergy())
                .FirstOrDefault();

            if (target == null)
                return false;

            IManufacturable item = tech.GetReferenceCopy();
            item.SetOwnerInstanceID(faction.GetInstanceID());

            manufacturingManager.Enqueue(target, item, target, ignoreCost: false);
            return true;
        }

        /// <summary>
        /// Builds capital ships at idle shipyards.
        /// Shipyards are selected randomly (matching original game behavior).
        /// Creates a new fleet at the planet if no idle fleet exists in the system.
        /// </summary>
        private void UpdateCapitalShipProduction(Faction faction)
        {
            // Original game selects randomly from ALL researched capital ship types,
            // not just the highest tier. Each shipyard gets a random ship type.
            List<Technology> availableTechs = faction
                .GetResearchedTechnologies(ManufacturingType.Ship)
                .Where(tech => tech.GetReference().GetType() == typeof(CapitalShip))
                .ToList();

            if (availableTechs.Count == 0)
                return;

            List<Planet> shipyards = faction.GetIdleFacilities(ManufacturingType.Ship);

            while (shipyards.Count > 0)
            {
                int index = randomProvider.NextInt(0, shipyards.Count);
                Planet shipyard = shipyards[index];
                shipyards.RemoveAt(index);

                Fleet fleet = FindOrCreateFleetInSystem(shipyard, faction);
                if (fleet == null)
                    continue;

                // Random ship type selection per shipyard
                Technology tech = availableTechs[randomProvider.NextInt(0, availableTechs.Count)];
                IManufacturable item = tech.GetReferenceCopy();
                item.SetOwnerInstanceID(faction.GetInstanceID());

                if (!manufacturingManager.Enqueue(shipyard, item, fleet, ignoreCost: false))
                    continue;
            }
        }

        /// <summary>
        /// Returns an existing movable fleet in the same system owned by the faction,
        /// or creates a new battle fleet at the given planet if none exists.
        /// </summary>
        private Fleet FindOrCreateFleetInSystem(Planet planet, Faction faction)
        {
            PlanetSystem system = planet.GetParent() as PlanetSystem;
            if (system == null)
                return null;

            string factionId = faction.GetInstanceID();
            Fleet existing = system
                .Planets.SelectMany(p => p.GetFleets())
                .FirstOrDefault(f => f.GetOwnerInstanceID() == factionId && f.IsMovable());

            if (existing != null)
                return existing;

            // Create a new fleet at this planet for the ship to be built into
            Fleet newFleet = faction.CreateFleet(game, roleType: FleetRoleType.Battle);
            game.AttachNode(newFleet, planet);
            return newFleet;
        }

        /// <summary>
        /// Fills fleets with starfighters from idle shipyards.
        /// Shipyards are selected randomly (matching original game behavior).
        /// </summary>
        private void UpdateStarfighterProduction(Faction faction)
        {
            List<Planet> idleShipyards = faction.GetIdleFacilities(ManufacturingType.Ship);
            if (!idleShipyards.Any())
                return;

            Technology starfighterTech = GetHighestTierTechnology(
                faction,
                ManufacturingType.Ship,
                typeof(Starfighter)
            );
            if (starfighterTech == null)
                return;

            IManufacturable prototype = starfighterTech.GetReference();
            if (game.GetRefinedMaterials(faction) <= prototype.GetConstructionCost())
                return;

            while (idleShipyards.Count > 0)
            {
                int index = randomProvider.NextInt(0, idleShipyards.Count);
                Planet shipyard = idleShipyards[index];
                idleShipyards.RemoveAt(index);

                AssignStarfightersToFleets(faction, shipyard, starfighterTech);
            }
        }

        /// <summary>
        /// Assigns starfighters to fleets with available capacity from a given shipyard.
        /// </summary>
        /// <param name="faction">The faction producing the starfighters.</param>
        /// <param name="shipyard">The planet with the idle shipyard.</param>
        /// <param name="starfighterTech">The starfighter technology to produce.</param>
        private void AssignStarfightersToFleets(
            Faction faction,
            Planet shipyard,
            Technology starfighterTech
        )
        {
            IEnumerable<Fleet> fleetsWithCapacity = faction
                .GetOwnedUnitsByType<Fleet>()
                .Where(fleet => fleet.GetExcessStarfighterCapacity() > 0)
                .OrderBy(fleet => fleet.GetExcessStarfighterCapacity());

            foreach (Fleet fleet in fleetsWithCapacity)
            {
                if (
                    game.GetRefinedMaterials(faction)
                    > starfighterTech.GetReference().GetConstructionCost()
                )
                {
                    IManufacturable item = starfighterTech.GetReferenceCopy();
                    item.SetOwnerInstanceID(faction.GetInstanceID());

                    manufacturingManager.Enqueue(shipyard, item, fleet, ignoreCost: false);
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Fills fleets with regiments from idle training facilities.
        /// Training facilities are selected randomly (matching original game behavior).
        /// </summary>
        private void UpdateTroopTraining(Faction faction)
        {
            List<Planet> idleTrainingFacilities = faction.GetIdleFacilities(ManufacturingType.Troop);
            if (!idleTrainingFacilities.Any())
                return;

            Technology regimentTech = GetHighestTierTechnology(
                faction,
                ManufacturingType.Troop,
                typeof(Regiment)
            );
            if (regimentTech == null)
                return;

            IManufacturable prototype = regimentTech.GetReference();
            if (game.GetRefinedMaterials(faction) <= prototype.GetConstructionCost())
                return;

            while (idleTrainingFacilities.Count > 0)
            {
                int index = randomProvider.NextInt(0, idleTrainingFacilities.Count);
                Planet trainingFacility = idleTrainingFacilities[index];
                idleTrainingFacilities.RemoveAt(index);

                AssignRegimentsToFleets(faction, trainingFacility, regimentTech);
            }
        }

        /// <summary>
        /// Assigns regiments to fleets with available capacity from a given training facility.
        /// </summary>
        /// <param name="faction">The faction producing the regiments.</param>
        /// <param name="trainingFacility">The planet with the idle training facility.</param>
        /// <param name="regimentTech">The regiment technology to produce.</param>
        private void AssignRegimentsToFleets(
            Faction faction,
            Planet trainingFacility,
            Technology regimentTech
        )
        {
            IEnumerable<Fleet> fleetsWithCapacity = faction
                .GetOwnedUnitsByType<Fleet>()
                .Where(fleet => fleet.GetExcessRegimentCapacity() > 0)
                .OrderBy(fleet => fleet.GetExcessRegimentCapacity());

            foreach (Fleet fleet in fleetsWithCapacity)
            {
                if (
                    game.GetRefinedMaterials(faction)
                    > regimentTech.GetReference().GetConstructionCost()
                )
                {
                    IManufacturable item = regimentTech.GetReferenceCopy();
                    item.SetOwnerInstanceID(faction.GetInstanceID());

                    manufacturingManager.Enqueue(trainingFacility, item, fleet, ignoreCost: false);
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Deploys regiments from idle fleets to under-garrisoned planets.
        /// Matches the original game's garrison requirement formula:
        /// garrison = ceil((SupportThreshold - popularSupport) / GarrisonDivisor)
        /// halved for core worlds (Empire), doubled during uprising.
        /// </summary>
        private void ManageGarrisons(Faction faction)
        {
            string factionId = faction.InstanceID;
            GameConfig.GarrisonConfig garrisonConfig = game.Config.AI.Garrison;

            foreach (Planet planet in faction.GetOwnedUnitsByType<Planet>())
            {
                int garrisonRequired = UprisingSystem.CalculateGarrisonRequirement(
                    planet,
                    faction,
                    garrisonConfig
                );

                int currentTroops = planet
                    .GetAllRegiments()
                    .Count(r => r.GetOwnerInstanceID() == factionId);

                int deficit = garrisonRequired - currentTroops;
                if (deficit <= 0)
                    continue;

                // Find fleets at this planet with regiments to deploy
                List<Fleet> localFleets = planet
                    .GetFleets()
                    .Where(f => f.GetOwnerInstanceID() == factionId && f.IsMovable())
                    .ToList();

                foreach (Fleet fleet in localFleets)
                {
                    if (deficit <= 0)
                        break;

                    foreach (Regiment regiment in fleet.GetRegiments().ToList())
                    {
                        if (deficit <= 0)
                            break;

                        game.DetachNode(regiment);
                        game.AttachNode(regiment, planet);
                        deficit--;

                        GameLogger.Debug(
                            $"[AI] Deployed {regiment.GetDisplayName()} as garrison at {planet.GetDisplayName()}"
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Per-system fleet evaluation pipeline matching the original game's behavior.
        /// For each system where the faction has idle battle fleets:
        ///   1. Accumulate total assault strength of idle fleets at the system.
        ///   2. Find reachable enemy planets, sorted by distance (nearest first).
        ///      Prioritize defending own HQ if contested.
        ///   3. Apply probabilistic deployment gate.
        ///   4. Deploy fleets proportionally to net strength surplus.
        /// </summary>
        private void EvaluateFleetDeployment(Faction faction)
        {
            string factionId = faction.InstanceID;
            HashSet<string> dispatched = new HashSet<string>();

            // HQ defense priority
            Planet hqPlanet = game.GetSceneNodeByInstanceID<Planet>(faction.GetHQInstanceID());
            bool hqNeedsRelief =
                hqPlanet != null
                && hqPlanet.IsContested()
                && !faction
                    .GetOwnedUnitsByType<Fleet>()
                    .Any(f => f.GetParentOfType<Planet>() == hqPlanet);

            // Enemy planets (cached once per evaluation)
            List<Planet> allEnemyPlanets = game.GetSceneNodesByType<Planet>(p =>
                p.IsColonized && p.GetOwnerInstanceID() != null && p.GetOwnerInstanceID() != factionId
            );

            if (!allEnemyPlanets.Any() && !hqNeedsRelief)
                return;

            int gateLow = game.Config.AI.DeploymentGateLow;
            int gateHigh = game.Config.AI.DeploymentGateHigh;

            foreach (PlanetSystem system in game.GetGalaxyMap().PlanetSystems)
            {
                // Collect idle battle fleets at this system
                List<Fleet> idleFleets = system
                    .Planets.SelectMany(p => p.GetFleets())
                    .Where(f =>
                        f.GetOwnerInstanceID() == factionId
                        && f.IsMovable()
                        && f.CapitalShips.Count > 0
                        && !dispatched.Contains(f.GetInstanceID())
                    )
                    .ToList();

                if (idleFleets.Count == 0)
                    continue;

                // Build sorted target list: HQ first if contested, then nearest enemies
                List<Planet> targets = new List<Planet>();

                if (hqNeedsRelief)
                    targets.Add(hqPlanet);

                int sysX = system.PositionX;
                int sysY = system.PositionY;
                List<Planet> sortedEnemies = allEnemyPlanets
                    .Where(p =>
                        !faction.GetOwnedUnitsByType<Fleet>().Any(f => f.GetParentOfType<Planet>() == p)
                    )
                    .OrderBy(p =>
                    {
                        int dx = sysX - p.PositionX;
                        int dy = sysY - p.PositionY;
                        return dx * dx + dy * dy;
                    })
                    .ToList();

                targets.AddRange(sortedEnemies);

                if (targets.Count == 0)
                    continue;

                // Per-target deployment loop
                List<Fleet> remainingFleets = new List<Fleet>(idleFleets);

                foreach (Planet target in targets)
                {
                    if (remainingFleets.Count == 0)
                        break;

                    // Calculate target defense: buildings + hostile fleets
                    int targetDefense = target.GetDefenseStrength(EntityStateFilter.All);
                    targetDefense += target
                        .GetFleets()
                        .Where(f =>
                            f.GetOwnerInstanceID() != null && f.GetOwnerInstanceID() != factionId
                        )
                        .Sum(f => f.GetCombatValue());

                    // Calculate available assault from remaining fleets
                    int availableAssault = remainingFleets.Sum(f => CalculateFleetAssaultStrength(f));
                    int netStrength = availableAssault - targetDefense;

                    if (netStrength <= 0)
                        continue;

                    // Probabilistic gate: roll must be below net strength to proceed
                    int roll = randomProvider.NextInt(gateLow, gateHigh + 1);
                    if (roll >= netStrength)
                        continue;

                    // Deploy fleets strongest-first until deployed strength exceeds defense
                    remainingFleets.Sort(
                        (a, b) =>
                            CalculateFleetAssaultStrength(b).CompareTo(CalculateFleetAssaultStrength(a))
                    );

                    int deployedStrength = 0;
                    foreach (Fleet fleet in remainingFleets.ToList())
                    {
                        if (deployedStrength > targetDefense)
                            break;

                        movementManager.RequestMove(fleet, target);
                        dispatched.Add(fleet.GetInstanceID());
                        deployedStrength += CalculateFleetAssaultStrength(fleet);
                        remainingFleets.Remove(fleet);

                        GameLogger.Log(
                            $"[AI] Deploying {fleet.GetDisplayName()} from {system.GetDisplayName()} to {target.GetDisplayName()}"
                        );
                    }

                    if (target == hqPlanet)
                        hqNeedsRelief = false;
                }
            }
        }

        private struct CachedMissionTables
        {
            public ProbabilityTable SubdueUprising;
            public ProbabilityTable Diplomacy;
            public ProbabilityTable InciteUprising;
            public ProbabilityTable Espionage;
            public ProbabilityTable Sabotage;
            public ProbabilityTable Abduction;
            public ProbabilityTable Assassination;
            public ProbabilityTable Rescue;

            public CachedMissionTables(GameConfig.AIMissionTablesConfig config)
            {
                SubdueUprising = new ProbabilityTable(config.SubdueUprising);
                Diplomacy = new ProbabilityTable(config.Diplomacy);
                InciteUprising = new ProbabilityTable(config.InciteUprising);
                Espionage = new ProbabilityTable(config.Espionage);
                Sabotage = new ProbabilityTable(config.Sabotage);
                Abduction = new ProbabilityTable(config.Abduction);
                Assassination = new ProbabilityTable(config.Assassination);
                Rescue = new ProbabilityTable(config.Rescue);
            }
        }

        /// <summary>
        /// Dispatches available officers to missions via table lookup.
        /// </summary>
        private void UpdateOfficerMissions(Faction faction)
        {
            List<Officer> available = faction
                .GetAvailableOfficers(faction)
                .Where(o => o.IsMovable())
                .ToList();

            GameLogger.Debug(
                $"[AI] {faction.GetDisplayName()}: {available.Count} officers available for missions."
            );

            if (!available.Any())
                return;

            CachedMissionTables tables = new CachedMissionTables(game.Config.AI.MissionTables);

            foreach (Officer officer in available)
            {
                Planet friendlyTarget = FindMissionTarget(faction);
                if (friendlyTarget != null)
                {
                    MissionType? missionType = SelectMissionType(
                        faction,
                        officer,
                        friendlyTarget,
                        tables
                    );
                    if (missionType != null)
                    {
                        GameLogger.Log(
                            $"Sending {officer.GetDisplayName()} on {missionType} mission to {friendlyTarget.GetDisplayName()}."
                        );
                        missionManager.InitiateMission(
                            missionType.Value,
                            officer,
                            friendlyTarget,
                            randomProvider
                        );
                        continue;
                    }
                }

                Planet enemyTarget = FindEnemyMissionTarget(faction);
                if (enemyTarget == null)
                    continue;

                MissionType? enemyMissionType = SelectEnemyMissionType(
                    faction,
                    officer,
                    enemyTarget,
                    tables
                );
                if (enemyMissionType == null)
                    continue;

                GameLogger.Log(
                    $"Sending {officer.GetDisplayName()} on {enemyMissionType} mission to {enemyTarget.GetDisplayName()}."
                );
                missionManager.InitiateMission(enemyMissionType.Value, officer, enemyTarget, randomProvider);
            }
        }

        /// <summary>
        /// Picks a random colonized planet owned by this faction or neutral.
        /// </summary>
        /// <param name="faction">The faction to find a target for.</param>
        /// <returns>A randomly selected candidate planet, or null if none exist.</returns>
        private Planet FindMissionTarget(Faction faction)
        {
            string factionId = faction.GetInstanceID();
            List<Planet> candidates = game.GetSceneNodesByType<Planet>(p =>
                p.IsColonized && (p.GetOwnerInstanceID() == factionId || p.GetOwnerInstanceID() == null)
            );

            if (candidates.Count == 0)
                return null;

            return candidates[randomProvider.NextInt(0, candidates.Count)];
        }

        /// <summary>
        /// Picks a random colonized planet owned by an enemy faction.
        /// </summary>
        /// <param name="faction">The faction to find an enemy target for.</param>
        /// <returns>A randomly selected enemy planet, or null if none exist.</returns>
        private Planet FindEnemyMissionTarget(Faction faction)
        {
            string factionId = faction.GetInstanceID();
            List<Planet> candidates = game.GetSceneNodesByType<Planet>(p =>
                p.IsColonized && p.GetOwnerInstanceID() != null && p.GetOwnerInstanceID() != factionId
            );

            if (candidates.Count == 0)
                return null;

            return candidates[randomProvider.NextInt(0, candidates.Count)];
        }

        /// <summary>
        /// Selects an enemy-targeted mission for the given officer at an enemy planet.
        /// Priority: InciteUprising > Espionage > Sabotage > Abduction > Rescue
        /// </summary>
        /// <param name="faction">The officer's faction.</param>
        /// <param name="officer">The officer to assign.</param>
        /// <param name="target">The enemy planet being targeted.</param>
        /// <param name="tables">Cached probability tables for mission dispatch.</param>
        private MissionType? SelectEnemyMissionType(
            Faction faction,
            Officer officer,
            Planet target,
            CachedMissionTables tables
        )
        {
            string factionId = faction.GetInstanceID();
            string owner = target.GetOwnerInstanceID();
            int enemySupport = target.GetPopularSupport(owner);
            int enemyStrength = target
                .GetChildren()
                .OfType<Regiment>()
                .Where(r => r.GetOwnerInstanceID() == owner)
                .Sum(r => r.DefenseRating);

            // InciteUprising: enemy planet not already in uprising
            // Score = (officer.espionage - enemy_popular_support) - enemy_regiment_strength
            if (!target.IsInUprising)
            {
                int score =
                    officer.GetSkillValue(MissionParticipantSkill.Espionage)
                    - enemySupport
                    - enemyStrength;
                if (tables.InciteUprising.Lookup(score) > 0)
                    return MissionType.InciteUprising;
            }

            // Espionage: any enemy planet
            // Score = officer.espionage (direct lookup)
            {
                int score = officer.GetSkillValue(MissionParticipantSkill.Espionage);
                if (tables.Espionage.Lookup(score) > 0)
                    return MissionType.Espionage;
            }

            // Sabotage: enemy planet with buildings
            // Score = (attacker.espionage + defender.combat) / 2 (SBTGMSTB_DAT)
            if (target.GetAllBuildings().Count > 0)
            {
                Officer defender = target
                    .GetChildren()
                    .OfType<Officer>()
                    .FirstOrDefault(o => o.GetOwnerInstanceID() == owner && !o.IsCaptured);
                int defenderCombat = defender?.GetSkillValue(MissionParticipantSkill.Combat) ?? 0;
                int score =
                    (officer.GetSkillValue(MissionParticipantSkill.Espionage) + defenderCombat) / 2;
                if (tables.Sabotage.Lookup(score) > 0)
                    return MissionType.Sabotage;
            }

            // Abduction: enemy planet with an un-captured enemy officer
            // Score = attacker.combat - target.combat (ABDCMSTB_DAT)
            Officer abductTarget = target
                .GetChildren()
                .OfType<Officer>()
                .FirstOrDefault(o => o.GetOwnerInstanceID() == owner && !o.IsCaptured);
            if (abductTarget != null)
            {
                int score =
                    officer.GetSkillValue(MissionParticipantSkill.Combat)
                    - abductTarget.GetSkillValue(MissionParticipantSkill.Combat);
                if (tables.Abduction.Lookup(score) > 0)
                    return MissionType.Abduction;
            }

            // Assassination: enemy planet with a live enemy officer
            // Score = attacker.combat - target.combat (ASSNMSTB_DAT)
            Officer assassinTarget = target
                .GetChildren()
                .OfType<Officer>()
                .FirstOrDefault(o => o.GetOwnerInstanceID() == owner && !o.IsCaptured && !o.IsKilled);
            if (assassinTarget != null)
            {
                int score =
                    officer.GetSkillValue(MissionParticipantSkill.Combat)
                    - assassinTarget.GetSkillValue(MissionParticipantSkill.Combat);
                if (tables.Assassination.Lookup(score) > 0)
                    return MissionType.Assassination;
            }

            // Rescue: enemy planet holding one of our captured officers
            // Score = captured_officer.combat (RESCMSTB_DAT)
            Officer captive = target
                .GetChildren()
                .OfType<Officer>()
                .FirstOrDefault(o => o.GetOwnerInstanceID() == factionId && o.IsCaptured);
            if (captive != null)
            {
                int score = captive.GetSkillValue(MissionParticipantSkill.Combat);
                if (tables.Rescue.Lookup(score) > 0)
                    return MissionType.Rescue;
            }

            return null;
        }

        /// <summary>
        /// Determines the best mission type for an officer at a given planet.
        /// Score formula: (officer_skill - popular_support) + leadership_rank.
        /// Returns null if no viable mission exists at this planet.
        /// Priority: SubdueUprising > Diplomacy > Recruitment
        /// </summary>
        /// <param name="faction">The officer's faction.</param>
        /// <param name="officer">The officer to assign.</param>
        /// <param name="target">The friendly or neutral planet being targeted.</param>
        /// <param name="tables">Cached probability tables for mission dispatch.</param>
        private MissionType? SelectMissionType(
            Faction faction,
            Officer officer,
            Planet target,
            CachedMissionTables tables
        )
        {
            string factionId = faction.GetInstanceID();
            string owner = target.GetOwnerInstanceID();
            double popularSupport = target.GetPopularSupport(factionId);
            int rank = officer.GetSkillValue(MissionParticipantSkill.Leadership);

            // SubdueUprising: owned planet in uprising (crisis — always first)
            // Score = (combat_skill - popular_support) + rank
            if (target.IsInUprising && owner == factionId)
            {
                int score =
                    officer.GetSkillValue(MissionParticipantSkill.Combat) - (int)popularSupport + rank;
                if (tables.SubdueUprising.Lookup(score) > 0)
                    return MissionType.SubdueUprising;
            }

            // Research: if the officer's best research skill exceeds their diplomacy,
            // prioritize research over diplomacy to avoid wasting research-focused officers
            int bestResearchSkill = GetBestResearchSkill(officer);
            int diplomacySkill = officer.GetSkillValue(MissionParticipantSkill.Diplomacy);

            if (bestResearchSkill > diplomacySkill && owner == factionId)
            {
                MissionType? researchMission = SelectResearchMissionType(officer, target);
                if (researchMission != null)
                    return researchMission;
            }

            // Diplomacy: owned or neutral planet with room for support growth
            if (
                (owner == null || owner == factionId)
                && popularSupport < game.Config.Planet.MaxPopularSupport
            )
            {
                int score = diplomacySkill - (int)popularSupport + rank;
                if (tables.Diplomacy.Lookup(score) > 0)
                    return MissionType.Diplomacy;
            }

            // Recruitment: owned planet with unrecruited officers available
            if (owner == factionId && game.GetUnrecruitedOfficers(factionId).Any())
                return MissionType.Recruitment;

            // Research: fallback for officers whose best skill isn't research
            // but who can still contribute if nothing else matched
            if (bestResearchSkill <= diplomacySkill && owner == factionId)
            {
                MissionType? researchMission = SelectResearchMissionType(officer, target);
                if (researchMission != null)
                    return researchMission;
            }

            return null;
        }

        /// <summary>
        /// Selects the best research mission type for the officer at the given planet.
        /// Picks the manufacturing type where the officer has the highest research skill
        /// and the planet has at least one idle facility of that type.
        /// </summary>
        /// <param name="officer">The officer to assign.</param>
        /// <param name="target">The owned planet being targeted.</param>
        /// <returns>The research mission type to dispatch, or null if none viable.</returns>
        private MissionType? SelectResearchMissionType(Officer officer, Planet target)
        {
            int bestSkill = 0;
            ManufacturingType bestType = ManufacturingType.Ship;

            foreach (ManufacturingType type in ResearchableTypes)
            {
                int skill = officer.GetResearchSkill(type);
                if (skill <= 0)
                    continue;
                if (target.GetIdleManufacturingFacilities(type) <= 0)
                    continue;
                if (skill > bestSkill)
                {
                    bestSkill = skill;
                    bestType = type;
                }
            }

            if (bestSkill <= 0)
                return null;

            return bestType switch
            {
                ManufacturingType.Ship => MissionType.ShipDesignResearch,
                ManufacturingType.Building => MissionType.FacilityDesignResearch,
                ManufacturingType.Troop => MissionType.TroopTrainingResearch,
                _ => null,
            };
        }

        /// <summary>
        /// Returns the officer's highest research skill across all manufacturing types.
        /// </summary>
        private static int GetBestResearchSkill(Officer officer)
        {
            int best = 0;
            foreach (ManufacturingType type in ResearchableTypes)
            {
                int skill = officer.GetResearchSkill(type);
                if (skill > best)
                    best = skill;
            }
            return best;
        }

        private static readonly ManufacturingType[] ResearchableTypes = new[]
        {
            ManufacturingType.Ship,
            ManufacturingType.Building,
            ManufacturingType.Troop,
        };

        /// <summary>
        /// Processes capital ship production issues for all systems with active construction.
        /// Executes all 4 variants (0x220-0x223) concurrently per system, matching the
        /// original game's Queue 3 processing where all registered issues run every tick.
        /// Replaces the flat ProcessCapitalShipContributions with the full issue-based pipeline:
        /// Setup -> KDY/LNR Contribution -> Assault Evaluation -> Finalize.
        /// See docs/generation-audit/03-ai-manufacturing.md sections 1.3-1.6.
        /// </summary>
        private void ProcessCapitalShipIssues(Faction faction)
        {
            string factionId = faction.InstanceID;

            // Find all systems where this faction has ships under construction
            HashSet<PlanetSystem> activeSystems = new HashSet<PlanetSystem>();

            foreach (Planet planet in faction.GetOwnedUnitsByType<Planet>())
            {
                Dictionary<ManufacturingType, List<IManufacturable>> queue =
                    planet.GetManufacturingQueue();
                if (!queue.TryGetValue(ManufacturingType.Ship, out List<IManufacturable> shipQueue))
                    continue;

                bool hasActiveShips = shipQueue
                    .OfType<CapitalShip>()
                    .Any(s => !((IManufacturable)s).IsManufacturingComplete());

                if (hasActiveShips)
                {
                    PlanetSystem system = planet.GetParentOfType<PlanetSystem>();
                    if (system != null)
                        activeSystems.Add(system);
                }
            }

            // Execute all 4 variants for each active system
            foreach (PlanetSystem system in activeSystems)
            {
                CapitalShipProductionIssue.ExecuteAllVariants(game, faction, system, randomProvider);
            }
        }

        /// <summary>
        /// Picks a random owned core system and applies one random resource adjustment.
        /// Matches ai_rebalance_raw_materials_and_energy (FUN_00558660).
        /// RESRC_TABLE has equal 25% probability for each of 4 cases.
        /// </summary>
        private void ProcessResourceRebalancing(Faction faction)
        {
            int maxEnergy = game.Config.ResourceRebalance.MaxEnergy;
            int maxRawMaterials = game.Config.ResourceRebalance.MaxRawMaterials;

            // Pick a random owned core system
            List<Planet> corePlanets = faction
                .GetOwnedUnitsByType<Planet>()
                .Where(p =>
                {
                    PlanetSystem system = p.GetParentOfType<PlanetSystem>();
                    return system != null && system.SystemType == PlanetSystemType.CoreSystem;
                })
                .ToList();

            if (corePlanets.Count == 0)
                return;

            Planet planet = corePlanets[randomProvider.NextInt(0, corePlanets.Count)];

            int mines = planet.GetBuildingTypeCount(BuildingType.Mine, EntityStateFilter.All);
            int facilities =
                planet.GetBuildingTypeCount(BuildingType.Shipyard, EntityStateFilter.All)
                + planet.GetBuildingTypeCount(BuildingType.TrainingFacility, EntityStateFilter.All)
                + planet.GetBuildingTypeCount(BuildingType.ConstructionFacility, EntityStateFilter.All);

            int rawMaterials = planet.NumRawResourceNodes;
            int energy = planet.EnergyCapacity;

            // RESRC_TABLE: [{0,1},{25,2},{50,3},{75,4}] — equal 25% probability per case
            switch (randomProvider.NextInt(1, 5))
            {
                case 1:
                    if (mines > 0 && rawMaterials > 0)
                        planet.NumRawResourceNodes = rawMaterials - 1;
                    break;
                case 2:
                    if (facilities > 0 && energy > 0)
                        planet.EnergyCapacity = energy - 1;
                    break;
                case 3:
                    if (rawMaterials < maxRawMaterials && rawMaterials < energy)
                        planet.NumRawResourceNodes = rawMaterials + 1;
                    break;
                case 4:
                    if (energy < maxEnergy)
                        planet.EnergyCapacity = energy + 1;
                    break;
            }
        }

        /// <summary>
        /// Returns a priority score for building the given type at the planet. Higher is more urgent.
        /// </summary>
        private int CalculateBuildingPriority(Planet planet, BuildingType buildingType)
        {
            return buildingType switch
            {
                BuildingType.Mine => CalculateMinePriority(planet),
                BuildingType.Defense => planet.GetBuildingTypeCount(BuildingType.Defense, EntityStateFilter.All),
                BuildingType.Refinery => CalculateRefineryPriority(planet),
                BuildingType.Shipyard
                or BuildingType.TrainingFacility
                or BuildingType.ConstructionFacility => CalculateFacilityPriority(planet, buildingType),
                _ => 0,
            };
        }

        /// <summary>
        /// Mine priority is proportional to the number of manufacturing facilities that need feeding.
        /// </summary>
        private int CalculateMinePriority(Planet planet)
        {
            return planet.GetBuildingTypeCount(BuildingType.TrainingFacility, EntityStateFilter.All)
                + planet.GetBuildingTypeCount(BuildingType.ConstructionFacility, EntityStateFilter.All)
                + planet.GetBuildingTypeCount(BuildingType.Shipyard, EntityStateFilter.All);
        }

        /// <summary>
        /// Refinery priority scales with raw resource nodes and manufacturing facility count.
        /// </summary>
        private int CalculateRefineryPriority(Planet planet)
        {
            int rawResourceNodeCount = planet.GetRawResourceNodes();
            int manufacturingFacilityScore = CalculateMinePriority(planet);
            return rawResourceNodeCount * 1000 + manufacturingFacilityScore;
        }

        /// <summary>
        /// Facility priority favors variety: penalizes duplicates of the same type,
        /// rewards having fewer of the complementary facility types.
        /// </summary>
        private int CalculateFacilityPriority(Planet planet, BuildingType facilityType)
        {
            int sameFacilityCount = planet.GetBuildingTypeCount(facilityType, EntityStateFilter.All);
            int otherFacilityCount = new[]
            {
                BuildingType.Shipyard,
                BuildingType.TrainingFacility,
                BuildingType.ConstructionFacility,
            }
                .Where(type => type != facilityType)
                .Sum(type => planet.GetBuildingTypeCount(type, EntityStateFilter.All));

            return otherFacilityCount * 1000 - sameFacilityCount;
        }

        /// <summary>
        /// Returns the highest-tier researched technology of the given manufacturing type
        /// whose reference object matches <paramref name="referenceType"/>.
        /// </summary>
        private Technology GetHighestTierTechnology(
            Faction faction,
            ManufacturingType manufacturingType,
            Type referenceType
        )
        {
            return faction
                .GetResearchedTechnologies(manufacturingType)
                .LastOrDefault(tech => tech.GetReference().GetType() == referenceType);
        }

        /// <summary>
        /// Returns the highest-tier researched technology of the given manufacturing type
        /// whose reference object is a building of <paramref name="buildingType"/>.
        /// </summary>
        private Technology GetHighestTierTechnology(
            Faction faction,
            ManufacturingType manufacturingType,
            BuildingType buildingType
        )
        {
            return faction
                .GetResearchedTechnologies(manufacturingType)
                .LastOrDefault(tech =>
                    (tech.GetReference() as Building)?.GetBuildingType() == buildingType
                );
        }

        /// <summary>
        /// Calculates fleet assault strength including personnel morale modifier.
        /// Formula: (personnel / divisor + 1) * fleet_combat_value.
        /// Personnel comes from fleet commander's leadership skill.
        /// </summary>
        /// <param name="fleet">The fleet to evaluate.</param>
        /// <returns>The calculated assault strength.</returns>
        private int CalculateFleetAssaultStrength(Fleet fleet)
        {
            int fleetCombatValue = fleet.GetCombatValue();

            // Get personnel morale from fleet commander
            Officer commander = fleet.GetOfficers().FirstOrDefault();
            int personnel = commander?.GetSkillValue(MissionParticipantSkill.Leadership) ?? 0;

            int divisor = game.Config.Combat.AssaultPersonnelDivisor;
            int assaultStrength = (personnel / divisor + 1) * fleetCombatValue;

            return assaultStrength;
        }
    }
}
