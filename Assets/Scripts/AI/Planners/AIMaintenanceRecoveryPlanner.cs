using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Demands;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Util.Mathematics;
using Candidate = Rebellion.AI.Proposals.AIMaintenanceRecoveryProposal.Candidate;
using Requirement = Rebellion.AI.Proposals.AIMaintenanceRecoveryProposal.Requirement;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Assesses retirement alternatives against current military and industrial requirements.
    /// </summary>
    public sealed class AIMaintenanceRecoveryPlanner : IAIProposalPlanner
    {
        /// <summary>
        /// Builds one recovery request without selecting or mutating any units.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <returns>The available recovery request, or no proposals when solvent.</returns>
        public List<AIProposal> Plan(AITurnContext context)
        {
            if (
                context?.Maintenance == null
                || !context.Game.Config.AI.Selection.MaintenanceRecovery.Enabled
                || ResourceProductionQueries.CalculateMaintenanceHeadroom(
                    context.Game,
                    context.Faction
                ) >= 0
            )
                return new List<AIProposal>();
            List<IManufacturable> units = context.Faction.GetOwnedUnitsByType<IManufacturable>();
            Dictionary<IManufacturable, List<KeyValuePair<Requirement, double>>> contributions =
                units
                    .Where(unit => AIMaintenanceRecoveryProposal.CanRetire(context, unit))
                    .ToDictionary(unit => unit, _ => new List<KeyValuePair<Requirement, double>>());
            AddFleetRequirements(context, units, contributions);
            AddPlanetRequirements(context, units, contributions);
            AddIndustryRequirements(context, units, contributions);
            AddAgentRequirements(context, units, contributions);
            List<Candidate> candidates = contributions
                .OrderBy(entry => entry.Key.InstanceID, StringComparer.Ordinal)
                .Select(entry => new Candidate(entry.Key, entry.Value))
                .ToList();
            return candidates.Count == 0
                ? new List<AIProposal>()
                : new List<AIProposal> { new AIMaintenanceRecoveryProposal(candidates) };
        }

        /// <summary>
        /// Records separate fleet requirements for combat, loading, and specialist capabilities.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="units">All owned manufacturable units.</param>
        /// <param name="contributions">The candidate contribution map.</param>
        private static void AddFleetRequirements(
            AITurnContext context,
            List<IManufacturable> units,
            Dictionary<IManufacturable, List<KeyValuePair<Requirement, double>>> contributions
        )
        {
            var config = context.Game.Config.AI.Selection.MaintenanceRecovery;
            foreach (Fleet fleet in context.Assessment.OwnedFleets)
            {
                List<IManufacturable> members = units
                    .Where(unit =>
                        unit.GetParentOfType<Fleet>() == fleet
                        && unit.GetManufacturingStatus() == ManufacturingStatus.Complete
                    )
                    .ToList();
                Planet target = context.Assessment.GetKnownPlanet(fleet.Order?.TargetPlanetId);
                AIAttackDemand attack = context.Assessment.IsEnemyPlanet(target)
                    ? context.GetAttackDemand(target, fleet)
                    : null;
                Planet defense =
                    fleet.Order?.OrderType == FleetOrderType.Defend
                    && context.Assessment.IsOwnedPlanet(target)
                        ? target
                        : null;
                double weight = defense != null ? config.DefenseWeight : config.AttackWeight;
                int combatTarget =
                    attack?.CombatStrength
                    ?? (
                        defense != null
                            ? context.StrategicPlan.GetDefenseStrength(defense)
                            : context.StrategicPlan.AssemblyFleetCombatStrength
                    );
                SpaceCombatCommandModifiers command = SpaceCombatCommands.GetCommandModifiers(
                    new[] { fleet },
                    null,
                    context.Faction.InstanceID
                );
                AddRequirement(
                    contributions,
                    members,
                    combatTarget,
                    weight,
                    unit =>
                        attack == null ? GetCombatValue(unit)
                        : unit is CapitalShip ship
                            ? SpaceCombatStrengthCalculator.GetProjectedCapitalShipCombatValueAgainstCapitalShips(
                                ship,
                                context.Game.Config.Combat.SpaceCombat,
                                command
                            )
                        : unit is Starfighter fighter
                            ? SpaceCombatStrengthCalculator.GetProjectedCarriedStarfighterCombatValue(
                                fighter,
                                context.Game.Config.Combat.SpaceCombat,
                                command
                            )
                        : 0,
                    capitalIncludesFighters: attack != null
                );
                AddRequirement(
                    contributions,
                    members,
                    attack?.CapitalShipFirepower ?? 0,
                    weight,
                    unit =>
                        unit is CapitalShip ship
                            ? SpaceCombatStrengthCalculator.GetCapitalShipFirepowerAgainstCapitalShips(
                                ship
                            )
                        : unit is Starfighter fighter
                            ? SpaceCombatStrengthCalculator.GetStarfighterFirepowerAgainstCapitalShips(
                                fighter
                            )
                        : 0
                );
                AddRequirement(
                    contributions,
                    members,
                    attack?.StarfighterFirepower ?? 0,
                    weight,
                    unit =>
                        unit is CapitalShip ship
                            ? SpaceCombatStrengthCalculator.GetCapitalShipFirepowerAgainstStarfighters(
                                ship
                            )
                        : unit is Starfighter fighter
                            ? SpaceCombatStrengthCalculator.GetStarfighterFirepowerAgainstStarfighters(
                                fighter
                            )
                        : 0
                );
                int troops =
                    defense != null
                        ? 0
                        : Math.Max(
                            attack?.RegimentCount ?? 0,
                            context
                                .Game
                                .Config
                                .AI
                                .FleetDeployment
                                .MinimumPlanetaryAssaultRegimentCount
                        );
                AddRequirement(
                    contributions,
                    members,
                    troops,
                    weight,
                    unit => unit is Regiment ? 1 : 0
                );
                AddRequirement(
                    contributions,
                    members,
                    troops,
                    weight,
                    unit => unit is CapitalShip ship ? ship.RegimentCapacity : 0
                );
                int generalBonus = PlanetaryAssaultResolver.GetLeadershipBonus(
                    fleet.GetOfficers(),
                    OfficerRank.General,
                    context.Faction.InstanceID,
                    context.Game.Config.Combat.PlanetaryAssault
                );
                AddRequirement(
                    contributions,
                    members,
                    attack?.RegimentStrength ?? 0,
                    weight,
                    unit => unit is Regiment regiment ? regiment.AttackRating + generalBonus : 0
                );
                int fighters = Math.Min(
                    fleet.GetStarfighterCapacity(),
                    IntegerMath.ScaleByPercentRoundedUp(
                        fleet.GetStarfighterCapacity(),
                        context.Game.Config.AI.Infrastructure.StarfighterParentFillPercent
                    )
                );
                AddRequirement(
                    contributions,
                    members,
                    fighters,
                    weight,
                    unit => unit is Starfighter ? 1 : 0
                );
                AddRequirement(
                    contributions,
                    members,
                    Math.Max(fighters, attack?.StarfighterCount ?? 0),
                    weight,
                    unit => unit is CapitalShip ship ? ship.StarfighterCapacity : 0
                );
                AddRequirement(
                    contributions,
                    members,
                    attack?.BombardmentStrength ?? 0,
                    weight,
                    unit =>
                        unit is CapitalShip ship
                            ? context.Assessment.GetProjectedCapitalShipBombardmentStrength(
                                fleet,
                                ship
                            )
                            : 0
                );
                AddRequirement(
                    contributions,
                    members,
                    attack != null ? 1 : 0,
                    weight,
                    unit => unit is CapitalShip ship && ship.HasGravityWell ? 1 : 0
                );
            }
            Planet headquarters = context.Assessment.FactionHeadquarters;
            if (headquarters != null)
            {
                List<IManufacturable> guards = units
                    .Where(unit =>
                        unit.GetParentOfType<Fleet>() is Fleet fleet
                        && fleet.GetParentOfType<Planet>() == headquarters
                        && fleet.Movement == null
                        && unit.GetManufacturingStatus() == ManufacturingStatus.Complete
                        && unit is IMovable movable
                        && movable.GetTransitMovement() == null
                    )
                    .ToList();
                double minimum = context.StrategicPlan.GetDefenseStrength(headquarters);
                AddRequirement(
                    contributions,
                    guards,
                    minimum,
                    config.DefenseWeight,
                    GetCombatValue,
                    protectedMinimum: minimum
                );
            }
        }

        /// <summary>
        /// Records garrison and static-defense needs without counting embarked units as garrisons.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="units">All owned manufacturable units.</param>
        /// <param name="contributions">The candidate contribution map.</param>
        private static void AddPlanetRequirements(
            AITurnContext context,
            List<IManufacturable> units,
            Dictionary<IManufacturable, List<KeyValuePair<Requirement, double>>> contributions
        )
        {
            var config = context.Game.Config.AI.Selection.MaintenanceRecovery;
            foreach (Planet planet in context.Assessment.OwnedPlanets)
            {
                List<IManufacturable> local = units
                    .Where(unit =>
                        unit.GetParent() == planet
                        && unit.GetManufacturingStatus() == ManufacturingStatus.Complete
                        && unit is IMovable movable
                        && movable.GetTransitMovement() == null
                    )
                    .ToList();
                AddRequirement(
                    contributions,
                    local,
                    context.StrategicPlan.GetPlanetaryGarrisonTarget(planet),
                    config.GarrisonWeight,
                    unit => unit is Regiment ? 1 : 0,
                    protectedMinimum: planet.IsHeadquarters
                            ? context.StrategicPlan.GetPlanetaryGarrisonReserve(planet)
                        : planet.IsColonized ? 0
                        : 1
                );
                AddRequirement(
                    contributions,
                    local,
                    context.StrategicPlan.GetPlanetaryStarfighterTarget(planet),
                    config.DefenseWeight,
                    unit => unit is Starfighter ? 1 : 0,
                    protectedMinimum: planet.IsHeadquarters
                        ? context.StrategicPlan.GetPlanetaryStarfighterTarget(planet)
                        : 0
                );
                AddRequirement(
                    contributions,
                    local,
                    context.StrategicPlan.GetPlanetaryShieldTarget(planet),
                    config.DefenseWeight,
                    unit =>
                        unit is Building building && building.IsPlanetaryShieldGenerator() ? 1 : 0,
                    protectedMinimum: planet.IsHeadquarters
                        ? context.StrategicPlan.GetPlanetaryShieldTarget(planet)
                        : 0
                );
                AddRequirement(
                    contributions,
                    local,
                    context.StrategicPlan.GetPlanetaryWeaponTarget(planet),
                    config.DefenseWeight,
                    unit =>
                        unit is Building building && building.BuildingType == BuildingType.Weapon
                            ? 1
                            : 0,
                    protectedMinimum: planet.IsHeadquarters
                        ? context.StrategicPlan.GetPlanetaryWeaponTarget(planet)
                        : 0
                );
            }
        }

        /// <summary>
        /// Records faction-wide infrastructure needs and the throughput of active production sites.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="units">All owned manufacturable units.</param>
        /// <param name="contributions">The candidate contribution map.</param>
        private static void AddIndustryRequirements(
            AITurnContext context,
            List<IManufacturable> units,
            Dictionary<IManufacturable, List<KeyValuePair<Requirement, double>>> contributions
        )
        {
            double weight = context.Game.Config.AI.Selection.MaintenanceRecovery.ProductionWeight;
            foreach (
                BuildingType type in new[]
                {
                    BuildingType.Shipyard,
                    BuildingType.ConstructionFacility,
                    BuildingType.TrainingFacility,
                }
            )
            {
                List<IManufacturable> facilities = units
                    .OfType<Building>()
                    .Where(building =>
                        building.BuildingType == type
                        && building.ManufacturingStatus == ManufacturingStatus.Complete
                    )
                    .Cast<IManufacturable>()
                    .ToList();
                AddRequirement(
                    contributions,
                    facilities,
                    context.StrategicPlan.GetInfrastructureTarget(type),
                    weight,
                    _ => 1
                );
                foreach (var group in facilities.GroupBy(unit => unit.GetParentOfType<Planet>()))
                {
                    Planet planet = group.Key;
                    ManufacturingType production = ((Building)group.First()).ProductionType;
                    bool busy =
                        planet != null
                        && planet
                            .GetManufacturingQueue()
                            .TryGetValue(production, out List<IManufacturable> queue)
                        && queue.Count > 0;
                    if (!busy)
                        continue;
                    AddRequirement(
                        contributions,
                        group.ToList(),
                        group.Sum(unit => ((Building)unit).GetProcessRate()),
                        weight,
                        unit => ((Building)unit).GetProcessRate()
                    );
                }
            }
        }

        /// <summary>
        /// Records the same role reserves used by production and protects a mobile headquarters spy.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="units">All owned manufacturable units.</param>
        /// <param name="contributions">The candidate contribution map.</param>
        private static void AddAgentRequirements(
            AITurnContext context,
            List<IManufacturable> units,
            Dictionary<IManufacturable, List<KeyValuePair<Requirement, double>>> contributions
        )
        {
            double weight = context.Game.Config.AI.Selection.MaintenanceRecovery.AgentWeight;
            foreach (
                var role in units
                    .OfType<SpecialForces>()
                    .Where(unit => !unit.IsRetired)
                    .GroupBy(unit =>
                        string.Join(
                            "|",
                            unit.AllowedMissionTypeIDs.OrderBy(id => id, StringComparer.Ordinal)
                        )
                    )
            )
                AddRequirement(
                    contributions,
                    role.Cast<IManufacturable>().ToList(),
                    context.StrategicPlan.GetSpecialForcesReserveTarget(role.First()),
                    weight,
                    _ => 1
                );
            Planet headquarters = context.Assessment.FactionHeadquarters;
            if (headquarters != null && context.Faction.Settings.Headquarters.IsMobile)
                AddRequirement(
                    contributions,
                    units
                        .OfType<SpecialForces>()
                        .Where(unit =>
                            !unit.IsRetired
                            && unit.GetParentOfType<Planet>() == headquarters
                            && unit.CanPerformMission(MissionTypeIDs.Espionage)
                        )
                        .Cast<IManufacturable>()
                        .ToList(),
                    1,
                    weight,
                    _ => 1,
                    protectedMinimum: 1
                );
        }

        /// <summary>
        /// Adds one shared requirement and caches each candidate's independent contribution.
        /// </summary>
        /// <param name="contributions">The candidate contribution map.</param>
        /// <param name="supply">All units providing this capability, including ineligible retirements.</param>
        /// <param name="target">The capability required.</param>
        /// <param name="weight">The configured importance of the goal.</param>
        /// <param name="measure">The additive contribution of one unit.</param>
        /// <param name="protectedMinimum">The hard safety floor.</param>
        /// <param name="capitalIncludesFighters">Whether the capital-ship measure includes its fighter children.</param>
        private static void AddRequirement(
            Dictionary<IManufacturable, List<KeyValuePair<Requirement, double>>> contributions,
            List<IManufacturable> supply,
            double target,
            double weight,
            Func<IManufacturable, double> measure,
            double protectedMinimum = 0,
            bool capitalIncludesFighters = false
        )
        {
            if (target <= 0 && protectedMinimum <= 0)
                return;
            double available = supply
                .Where(unit => !capitalIncludesFighters || unit is not Starfighter)
                .Sum(unit => Math.Max(0, measure(unit)));
            Requirement requirement = new(target, available, weight, protectedMinimum);
            foreach (IManufacturable unit in supply)
            {
                if (!contributions.TryGetValue(unit, out var effects))
                    continue;
                double value = Math.Max(0, measure(unit));
                if (value > 0)
                    effects.Add(new KeyValuePair<Requirement, double>(requirement, value));
            }
        }

        /// <summary>
        /// Returns additive general space-combat strength without counting carrier occupants twice.
        /// </summary>
        /// <param name="unit">The capital ship or squadron.</param>
        /// <returns>The unit's general combat strength.</returns>
        private static double GetCombatValue(IManufacturable unit) =>
            unit is CapitalShip ship ? ship.GetCombatValue()
            : unit is Starfighter fighter ? fighter.GetCombatValue()
            : 0;
    }
}
