using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Scoring;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Generates fleet and colonization force requirements.
    /// </summary>
    internal sealed class AIForceRequirements
    {
        /// <summary>
        /// Adds demands that establish missing battle fleets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        internal void AddFleetSeedDemand(
            AITurnContext context,
            List<AIProductionRequirement> demands
        )
        {
            int targetCount = context.StrategicPlan.TargetBattleFleetCount;
            int committedCount = context.Assessment.OwnedFleets.Count(IsCommittedBattleFleet);
            int deficit = targetCount - committedCount;
            Planet unguardedHeadquarters = FindUnguardedHeadquarters(context);
            if (deficit <= 0 && unguardedHeadquarters == null)
                return;

            Planet destination = unguardedHeadquarters ?? FindFleetAssemblyPlanet(context);
            if (destination == null)
                return;

            int quantityNeeded = Math.Max(1, deficit);

            demands.Add(
                new AIProductionRequirement(
                    AIProductionRequirement.CreateId(
                        context.Faction.InstanceID,
                        AIProductionRequirementKind.FleetSeedCapitalShip
                    ),
                    AIProductionRequirementKind.FleetSeedCapitalShip,
                    ManufacturingType.Ship,
                    BuildingType.None,
                    destination,
                    quantityNeeded,
                    GetBasePressure(
                        context,
                        context.Game.Config.AI.Infrastructure.FleetSeedCapitalShipDemandPercent,
                        quantityNeeded,
                        Math.Max(1, targetCount)
                    ),
                    capitalShipRole: AICapitalShipProductionRole.General
                )
            );
        }

        /// <summary>
        /// Adds demand for a dedicated colonization fleet while settlement opportunities remain.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        internal void AddColonizationFleetSeedDemand(
            AITurnContext context,
            List<AIProductionRequirement> demands
        )
        {
            int targetCount = Math.Max(
                0,
                context.Game.Config.AI.FleetDeployment.ColonizationFleetTargetCount
            );
            int committedCount = context.Assessment.OwnedFleets.Count(fleet =>
                fleet.RoleType == FleetRoleType.Colonization
            );
            int deficit = targetCount - committedCount;
            if (!HasColonizationOpportunity(context) || deficit <= 0)
                return;

            Planet destination = FindFleetAssemblyPlanet(context);
            if (destination == null)
                return;

            demands.Add(
                new AIProductionRequirement(
                    AIProductionRequirement.CreateId(
                        context.Faction.InstanceID,
                        AIProductionRequirementKind.ColonizationFleetSeedCapitalShip
                    ),
                    AIProductionRequirementKind.ColonizationFleetSeedCapitalShip,
                    ManufacturingType.Ship,
                    BuildingType.None,
                    destination,
                    deficit,
                    context.Game.Config.AI.Infrastructure.ColonizationFleetDemandPercent,
                    capitalShipRole: AICapitalShipProductionRole.TroopTransport
                )
            );
        }

        /// <summary>
        /// Returns whether known unsettled territory or unexplored Outer Rim territory remains.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when a colonization fleet has useful work available.</returns>
        private static bool HasColonizationOpportunity(AITurnContext context)
        {
            return context.Assessment.KnownUncolonizedPlanets.Count > 0
                || context.Assessment.UnexploredPlanets.Any(planet =>
                    planet.GetParentOfType<PlanetSector>()?.SectorType == PlanetSectorType.OuterRim
                );
        }

        /// <summary>
        /// Finds the highest-priority headquarters lacking a defense fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The unguarded headquarters, or null.</returns>
        private Planet FindUnguardedHeadquarters(AITurnContext context)
        {
            return context
                .Assessment.OwnedPlanets.Where(planet =>
                    context.Assessment.IsFactionHeadquarters(planet)
                    && planet.IsColonized
                    && !planet.IsDestroyed
                    && !context.Assessment.HasCommittedHeadquartersFleet(planet)
                )
                .OrderByDescending(context.Assessment.GetPlanetValue)
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// Finds the preferred planet for assembling a new fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The assembly planet, or null.</returns>
        private Planet FindFleetAssemblyPlanet(AITurnContext context)
        {
            return context
                .Assessment.OwnedPlanets.Where(planet => planet.IsColonized && !planet.IsDestroyed)
                .OrderByDescending(context.Assessment.IsFactionHeadquarters)
                .ThenByDescending(planet =>
                    context.Assessment.GetPlanetProductionRate(planet, ManufacturingType.Ship)
                )
                .ThenByDescending(context.Assessment.GetPlanetValue)
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns whether a fleet counts toward the battle-fleet target.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>True when the fleet is committed.</returns>
        private static bool IsCommittedBattleFleet(Fleet fleet)
        {
            return fleet?.RoleType == FleetRoleType.Battle
                && fleet.GetChildren<CapitalShip>().Count > 0;
        }

        /// <summary>
        /// Adds reinforcement demand for owned fleets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        internal void AddFleetReinforcementDemands(
            AITurnContext context,
            List<AIProductionRequirement> demands
        )
        {
            Fleet defenseFleet = GetPriorityDefenseFleet(context);
            AddFleetDemands(context, demands, defenseFleet);

            IReadOnlyList<Fleet> attackFleets = GetPriorityAttackFleets(context);
            AddAttackShipDemands(context, demands, attackFleets);
            AddPriorityAttackRegimentDemand(context, demands, attackFleets);

            foreach (Fleet colonizationFleet in GetPriorityColonizationFleets(context))
                AddFleetDemands(context, demands, colonizationFleet);

            AddFleetDemands(context, demands, GetFleetAssemblyFleets(context).FirstOrDefault());
        }

        /// <summary>
        /// Adds every applicable reinforcement demand for one fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="fleet">The fleet to reinforce.</param>
        private void AddFleetDemands(
            AITurnContext context,
            List<AIProductionRequirement> demands,
            Fleet fleet
        )
        {
            if (fleet == null)
                return;

            AddFleetCapitalShipDemand(context, demands, fleet);
            AddFleetStarfighterDemand(context, demands, fleet);
            AddFleetRegimentDemand(context, demands, fleet);
        }

        /// <summary>
        /// Adds ship demands for attack fleets in reinforcement priority order.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="fleets">Attack fleets in reinforcement priority order.</param>
        private void AddAttackShipDemands(
            AITurnContext context,
            List<AIProductionRequirement> demands,
            IReadOnlyList<Fleet> fleets
        )
        {
            foreach (Fleet fleet in fleets)
            {
                AddFleetCapitalShipDemand(context, demands, fleet);
                AddFleetStarfighterDemand(context, demands, fleet);
            }
        }

        /// <summary>
        /// Adds regiment demand for the highest-priority attack fleet that still needs troops.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="fleets">Attack fleets in reinforcement priority order.</param>
        private void AddPriorityAttackRegimentDemand(
            AITurnContext context,
            List<AIProductionRequirement> demands,
            IReadOnlyList<Fleet> fleets
        )
        {
            foreach (Fleet fleet in fleets)
            {
                int initialCount = demands.Count;
                AddFleetRegimentDemand(context, demands, fleet);
                if (demands.Count > initialCount)
                    return;
            }
        }

        /// <summary>
        /// Returns the defense fleet with the greatest reinforcement need.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The priority defense fleet, or null.</returns>
        private Fleet GetPriorityDefenseFleet(AITurnContext context)
        {
            return context
                .Assessment.OwnedFleets.Where(CanReinforceFleet)
                .Select(fleet => new { Fleet = fleet, Target = GetDefenseTarget(context, fleet) })
                .Where(candidate => candidate.Target != null)
                .OrderByDescending(candidate =>
                    AIFleetReinforcementUtility.ScoreDefenseNeed(
                        context,
                        candidate.Target,
                        context.Assessment.GetProjectedFleetCombatValue(candidate.Fleet)
                    )
                )
                .ThenBy(candidate => candidate.Fleet.InstanceID, StringComparer.Ordinal)
                .Select(candidate => candidate.Fleet)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns active attack fleets ordered by proximity to campaign readiness.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The ordered attack fleets that can receive reinforcements.</returns>
        private IReadOnlyList<Fleet> GetPriorityAttackFleets(AITurnContext context)
        {
            return context
                .Assessment.AttackOrderedFleets.Where(CanReinforceFleet)
                .Select(fleet => new
                {
                    Fleet = fleet,
                    Target = GetAttackTargetPlanet(context, fleet),
                })
                .Where(candidate => candidate.Target != null)
                .OrderByDescending(candidate =>
                    AIFleetProductionAllocationScorer.ScoreAttack(
                        context,
                        candidate.Fleet,
                        candidate.Target,
                        GetProjectedAttackReadiness(context, candidate.Fleet, candidate.Target)
                    )
                )
                .ThenBy(candidate => candidate.Fleet.InstanceID, StringComparer.Ordinal)
                .Select(candidate => candidate.Fleet)
                .ToList();
        }

        /// <summary>
        /// Returns the weakest projected readiness ratio for an attack fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to assess.</param>
        /// <param name="target">The fleet's attack target.</param>
        /// <returns>The least-complete attack requirement, from zero through one.</returns>
        private double GetProjectedAttackReadiness(
            AITurnContext context,
            Fleet fleet,
            Planet target
        )
        {
            if (fleet == null || target == null)
                return 0;

            AIAssessment assessment = context.Assessment;
            int requiredRegiments = context.AttackRequirements.GetRegimentCount(
                fleet,
                target,
                projected: true
            );
            double readiness = GetFulfillmentRatio(
                assessment.GetProjectedFleetCombatValue(fleet),
                context.AttackRequirements.GetCombatStrength(target)
            );
            readiness = Math.Min(
                readiness,
                GetFulfillmentRatio(
                    assessment.GetFleetLoadedRegimentCount(fleet),
                    requiredRegiments
                )
            );
            readiness = Math.Min(
                readiness,
                GetFulfillmentRatio(assessment.GetFleetRegimentCapacity(fleet), requiredRegiments)
            );
            readiness = Math.Min(
                readiness,
                GetFulfillmentRatio(
                    assessment.GetProjectedFleetRegimentAttackStrength(fleet),
                    context.AttackRequirements.GetRegimentStrength(fleet, target, projected: true)
                )
            );
            return Math.Min(
                readiness,
                GetFulfillmentRatio(
                    assessment.GetProjectedFleetBombardmentStrength(fleet),
                    context.AttackRequirements.GetBombardmentStrength(target)
                )
            );
        }

        /// <summary>
        /// Returns the primary colonization fleet for reinforcement.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The colonization fleet, or null.</returns>
        private IReadOnlyList<Fleet> GetPriorityColonizationFleets(AITurnContext context)
        {
            return context
                .Assessment.OwnedFleets.Where(fleet =>
                    fleet.RoleType == FleetRoleType.Colonization && CanReinforceFleet(fleet)
                )
                .OrderByDescending(fleet =>
                    AIFleetProductionAllocationScorer.ScoreColonization(context, fleet)
                )
                .ThenBy(fleet => fleet.InstanceID, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Returns stationary battle fleets that can be developed for future campaigns.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The eligible assembly fleets, weakest first.</returns>
        private IReadOnlyList<Fleet> GetFleetAssemblyFleets(AITurnContext context)
        {
            return context
                .Assessment.OwnedFleets.Where(fleet =>
                    fleet.RoleType == FleetRoleType.Battle
                    && CanReinforceFleet(fleet)
                    && fleet.Order == null
                    && context.StrategicPlan.CanFleetDepart(fleet)
                )
                .OrderByDescending(fleet =>
                    AIFleetProductionAllocationScorer.ScoreAssembly(context, fleet)
                )
                .ThenBy(fleet => fleet.InstanceID, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Adds capital ship demand for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        private void AddFleetCapitalShipDemand(
            AITurnContext context,
            List<AIProductionRequirement> demands,
            Fleet fleet
        )
        {
            Planet targetPlanet = GetAttackTargetPlanet(context, fleet);
            bool isColonizationFleet = fleet.RoleType == FleetRoleType.Colonization;
            bool isColonizationOrder = fleet.Order?.OrderType == FleetOrderType.Colonize;
            Planet defenseTarget = GetDefenseTarget(context, fleet);
            bool isDefenseOrder = fleet.Order?.OrderType == FleetOrderType.Defend;
            if (
                targetPlanet == null
                && fleet.Order != null
                && !isColonizationOrder
                && defenseTarget == null
            )
                return;

            int projectedCombat = context.Assessment.GetProjectedFleetCombatValue(fleet);
            int targetCombat =
                targetPlanet != null ? context.AttackRequirements.GetCombatStrength(targetPlanet)
                : defenseTarget != null ? context.StrategicPlan.GetDefenseStrength(defenseTarget)
                : isColonizationFleet || isColonizationOrder ? projectedCombat
                : context.StrategicPlan.AssemblyFleetCombatStrength;
            int combatDeficit = targetCombat - projectedCombat;
            int targetRegimentCapacity =
                isDefenseOrder ? 0
                : isColonizationFleet || isColonizationOrder
                    ? context.Game.Config.AI.FleetDeployment.ColonizationFleetMaximumRegimentCount
                : targetPlanet == null
                    ? context.Game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount
                : GetDesiredRegimentCount(context, fleet);
            int regimentCapacityDeficit = targetRegimentCapacity - fleet.GetRegimentCapacity();
            int projectedBombardment = context.Assessment.GetProjectedFleetBombardmentStrength(
                fleet
            );
            int targetBombardment =
                targetPlanet == null || isColonizationFleet
                    ? 0
                    : context.AttackRequirements.GetBombardmentStrength(targetPlanet);
            int bombardmentDeficit = targetBombardment - projectedBombardment;
            AICapitalShipProductionRole capitalShipRole;
            int deficit;
            int target;
            if (regimentCapacityDeficit > 0)
            {
                capitalShipRole = AICapitalShipProductionRole.TroopTransport;
                deficit = regimentCapacityDeficit;
                target = targetRegimentCapacity;
            }
            else if (bombardmentDeficit > 0)
            {
                capitalShipRole = AICapitalShipProductionRole.Bombardment;
                deficit = bombardmentDeficit;
                target = targetBombardment;
            }
            else if (combatDeficit > 0)
            {
                capitalShipRole = AICapitalShipProductionRole.General;
                deficit = combatDeficit;
                target = targetCombat;
            }
            else if (!isColonizationFleet && NeedsInterdictionCapitalShip(context, fleet))
            {
                capitalShipRole = AICapitalShipProductionRole.Interdiction;
                deficit = 1;
                target = 1;
            }
            else
            {
                return;
            }

            demands.Add(
                CreateFleetDemand(
                    context,
                    AIProductionRequirementKind.FleetCapitalShip,
                    ManufacturingType.Ship,
                    fleet,
                    deficit,
                    target,
                    isColonizationFleet
                        ? context.Game.Config.AI.Infrastructure.ColonizationFleetDemandPercent
                        : context.Game.Config.AI.Infrastructure.FleetCapitalShipDemandPercent,
                    capitalShipRole
                )
            );
        }

        /// <summary>
        /// Returns whether a battle fleet should add an interdiction-capable capital ship.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True when an unlocked gravity-well ship is needed.</returns>
        private static bool NeedsInterdictionCapitalShip(AITurnContext context, Fleet fleet)
        {
            bool supportsInterdiction =
                fleet.Order == null
                || fleet.Order.OrderType is FleetOrderType.Attack or FleetOrderType.Defend;
            if (
                !supportsInterdiction
                || fleet.GetChildren<CapitalShip>().Any(capitalShip => capitalShip.HasGravityWell)
            )
                return false;

            return context
                .Faction.GetUnlockedTechnologies(ManufacturingType.Ship)
                .Any(technology =>
                    technology.GetReference() is CapitalShip capitalShip
                    && IManufacturable.CanBeManufacturedBy(capitalShip, context.Faction.InstanceID)
                    && capitalShip.HasGravityWell
                    && !capitalShip.CanDestroyPlanets
                );
        }

        /// <summary>
        /// Adds starfighter demand for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        private void AddFleetStarfighterDemand(
            AITurnContext context,
            List<AIProductionRequirement> demands,
            Fleet fleet
        )
        {
            if (fleet.RoleType == FleetRoleType.Colonization)
                return;

            int targetCount = GetTargetStarfighterCount(context, fleet);
            int deficit = targetCount - fleet.GetCurrentStarfighterCount();
            if (deficit <= 0)
                return;

            demands.Add(
                CreateFleetDemand(
                    context,
                    AIProductionRequirementKind.FleetStarfighter,
                    ManufacturingType.Ship,
                    fleet,
                    deficit,
                    targetCount,
                    context.Game.Config.AI.Infrastructure.FleetStarfighterDemandPercent
                )
            );
        }

        /// <summary>
        /// Adds regiment demand for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        private void AddFleetRegimentDemand(
            AITurnContext context,
            List<AIProductionRequirement> demands,
            Fleet fleet
        )
        {
            int targetCount = Math.Min(
                fleet.GetRegimentCapacity(),
                GetDesiredRegimentCount(context, fleet)
            );
            int deficit = targetCount - fleet.GetCurrentRegimentCount();
            if (deficit <= 0)
                return;

            demands.Add(
                CreateFleetDemand(
                    context,
                    AIProductionRequirementKind.FleetRegiment,
                    ManufacturingType.Troop,
                    fleet,
                    deficit,
                    targetCount,
                    fleet.RoleType == FleetRoleType.Colonization
                        ? context.Game.Config.AI.Infrastructure.ColonizationFleetDemandPercent
                        : context.Game.Config.AI.Infrastructure.FleetRegimentDemandPercent
                )
            );
        }

        /// <summary>
        /// Creates a fleet unit production demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">Demand kind.</param>
        /// <param name="manufacturingType">Manufacturing type required.</param>
        /// <param name="fleet">Fleet receiving the unit.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <param name="targetCount">Target count.</param>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <param name="capitalShipRole">Capital ship role required by the demand.</param>
        /// <returns>The production demand.</returns>
        private AIProductionRequirement CreateFleetDemand(
            AITurnContext context,
            AIProductionRequirementKind kind,
            ManufacturingType manufacturingType,
            Fleet fleet,
            int deficit,
            int targetCount,
            int baseDemandPercent,
            AICapitalShipProductionRole capitalShipRole = AICapitalShipProductionRole.None
        )
        {
            return new AIProductionRequirement(
                AIProductionRequirement.CreateId(
                    context.Faction.InstanceID,
                    kind,
                    fleet.InstanceID
                ),
                kind,
                manufacturingType,
                BuildingType.None,
                fleet,
                deficit,
                GetFleetDemandPressure(
                    context,
                    kind,
                    fleet,
                    deficit,
                    targetCount,
                    baseDemandPercent
                ),
                capitalShipRole: capitalShipRole
            );
        }

        /// <summary>
        /// Returns pressure for fleet production demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">Demand kind.</param>
        /// <param name="fleet">Fleet receiving production.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <param name="targetCount">Target count.</param>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <returns>The fleet demand pressure.</returns>
        private double GetFleetDemandPressure(
            AITurnContext context,
            AIProductionRequirementKind kind,
            Fleet fleet,
            int deficit,
            int targetCount,
            int baseDemandPercent
        )
        {
            double pressure = GetBasePressure(context, baseDemandPercent, deficit, targetCount);
            Planet targetPlanet = context.Assessment.GetAttackTargetPlanet(fleet);

            if (targetPlanet != null)
            {
                pressure += GetTargetValuePressure(context, targetPlanet);
                pressure += GetFleetReadinessPressure(context, kind, fleet, targetPlanet);
                pressure += GetFinalReadinessGatePressure(context, fleet, targetPlanet, deficit);
                if (
                    kind
                    is AIProductionRequirementKind.FleetCapitalShip
                        or AIProductionRequirementKind.FleetRegiment
                )
                {
                    pressure += AIUtility.EvaluatePressure(
                        1,
                        context.Game.Config.AI.Infrastructure.DemandUtility.AttackReinforcement
                    );
                }
            }

            if (kind == AIProductionRequirementKind.FleetStarfighter)
                pressure += GetStarfighterFillPressure(context, fleet, targetCount);

            return pressure;
        }

        /// <summary>
        /// Returns base pressure for a demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <param name="targetCount">Target count.</param>
        /// <returns>The base pressure.</returns>
        private static double GetBasePressure(
            AITurnContext context,
            int baseDemandPercent,
            int deficit,
            int targetCount
        )
        {
            double deficitRatio = deficit / (double)Math.Max(1, targetCount);
            return Math.Min(
                100,
                baseDemandPercent
                    + AIUtility.EvaluateDiscretePressure(
                        deficitRatio,
                        context.Game.Config.AI.Infrastructure.DemandUtility.Deficit
                    )
            );
        }

        /// <summary>
        /// Returns extra pressure from target planet value.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>The target value pressure.</returns>
        private double GetTargetValuePressure(AITurnContext context, Planet targetPlanet)
        {
            double highestValue = context.Assessment.GetHighestEnemyPlanetValue();
            if (highestValue <= 0)
                return 0;

            return AIUtility.EvaluatePressure(
                context.Assessment.GetPlanetValue(targetPlanet) / highestValue,
                context.Game.Config.AI.Infrastructure.DemandUtility.FleetTargetValue
            );
        }

        /// <summary>
        /// Returns extra pressure from fleet readiness gaps.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">Demand kind.</param>
        /// <param name="fleet">Fleet receiving production.</param>
        /// <param name="targetPlanet">Fleet attack target.</param>
        /// <returns>The fleet readiness pressure.</returns>
        private double GetFleetReadinessPressure(
            AITurnContext context,
            AIProductionRequirementKind kind,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            GameConfig.AIConsiderationConfig readiness = context
                .Game
                .Config
                .AI
                .Infrastructure
                .DemandUtility
                .FleetReadiness;
            int requiredCombat = context.AttackRequirements.GetCombatStrength(targetPlanet);
            int requiredRegiments = context.AttackRequirements.GetRegimentCount(
                fleet,
                targetPlanet,
                projected: true
            );
            double combatReadiness = GetFulfillmentRatio(
                context.Assessment.GetProjectedFleetCombatValue(fleet),
                requiredCombat
            );
            double regimentReadiness = GetFulfillmentRatio(
                context.Assessment.GetFleetLoadedRegimentCount(fleet),
                requiredRegiments
            );
            double capacityReadiness = GetFulfillmentRatio(
                context.Assessment.GetFleetRegimentCapacity(fleet),
                requiredRegiments
            );

            return kind switch
            {
                AIProductionRequirementKind.FleetRegiment => AIUtility.EvaluatePressure(
                    (combatReadiness + capacityReadiness) / 2,
                    readiness
                ),
                AIProductionRequirementKind.FleetCapitalShip => AIUtility.EvaluatePressure(
                    (regimentReadiness + capacityReadiness) / 2,
                    readiness
                ),
                AIProductionRequirementKind.FleetStarfighter => AIUtility.EvaluatePressure(
                    (combatReadiness + regimentReadiness + capacityReadiness) / 3,
                    readiness
                ),
                _ => 0,
            };
        }

        /// <summary>
        /// Returns extra pressure when a fleet is near final readiness.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet receiving production.</param>
        /// <param name="targetPlanet">Fleet attack target.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <returns>The final readiness pressure.</returns>
        private double GetFinalReadinessGatePressure(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet,
            int deficit
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            if (deficit > config.FleetFinalReadinessGateUnitCount)
                return 0;

            int requiredCombat = context.AttackRequirements.GetCombatStrength(targetPlanet);
            int requiredRegiments = context.AttackRequirements.GetRegimentCount(
                fleet,
                targetPlanet,
                projected: true
            );
            bool combatReady =
                context.Assessment.GetProjectedFleetCombatValue(fleet) >= requiredCombat;
            bool capacityReady =
                context.Assessment.GetFleetRegimentCapacity(fleet) >= requiredRegiments;
            bool bombardmentReady =
                context.Assessment.GetProjectedFleetBombardmentStrength(fleet)
                >= context.AttackRequirements.GetBombardmentStrength(targetPlanet);

            if (!combatReady || !capacityReady || !bombardmentReady)
                return 0;

            return AIUtility.EvaluateDiscretePressure(
                (config.FleetFinalReadinessGateUnitCount - deficit + 1)
                    / (double)config.FleetFinalReadinessGateUnitCount,
                config.DemandUtility.FinalReadiness
            );
        }

        /// <summary>
        /// Returns extra pressure for filling starfighter capacity.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet receiving starfighters.</param>
        /// <param name="targetCount">Target starfighter count.</param>
        /// <returns>The starfighter fill pressure.</returns>
        private double GetStarfighterFillPressure(
            AITurnContext context,
            Fleet fleet,
            int targetCount
        )
        {
            if (fleet == null || targetCount <= 0)
                return 0;

            int loadedCount = context.Assessment.GetFleetLoadedStarfighterCount(fleet);
            return AIUtility.EvaluateDiscretePressure(
                (targetCount - loadedCount) / (double)targetCount,
                context.Game.Config.AI.Infrastructure.DemandUtility.StarfighterFill
            );
        }

        /// <summary>
        /// Returns a bounded fulfillment ratio.
        /// </summary>
        /// <param name="value">Current value.</param>
        /// <param name="target">Target value.</param>
        /// <returns>The bounded fulfillment ratio.</returns>
        private double GetFulfillmentRatio(double value, double target)
        {
            if (target <= 0)
                return 1;

            return Math.Max(0, Math.Min(1, value / target));
        }

        /// <summary>
        /// Returns whether a fleet can receive reinforcement demand.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True if the fleet can receive reinforcement.</returns>
        private bool CanReinforceFleet(Fleet fleet)
        {
            return fleet?.RoleType is FleetRoleType.Battle or FleetRoleType.Colonization
                && fleet.Movement == null
                && (
                    HasPresentOrUnderConstructionCapitalShips(fleet)
                    || fleet.Order?.OrderType is FleetOrderType.Attack or FleetOrderType.Defend
                );
        }

        /// <summary>
        /// Resolves the defense target assigned to a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The defense target, or null.</returns>
        private Planet GetDefenseTarget(AITurnContext context, Fleet fleet)
        {
            if (fleet?.Order?.OrderType != FleetOrderType.Defend)
                return null;

            Planet target = context.Assessment.GetKnownPlanet(fleet.Order.TargetPlanetId);
            return
                context.Assessment.IsOwnedPlanet(target)
                && context.StrategicPlan.GetDefenseStrength(target) > 0
                ? target
                : null;
        }

        /// <summary>
        /// Returns whether a fleet has capital ships present or being built.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True if the fleet has present or under-construction capital ships.</returns>
        private static bool HasPresentOrUnderConstructionCapitalShips(Fleet fleet)
        {
            return fleet?.GetChildren<CapitalShip>().Count > 0;
        }

        /// <summary>
        /// Returns target starfighter count for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>The target starfighter count.</returns>
        private int GetTargetStarfighterCount(AITurnContext context, Fleet fleet)
        {
            int capacity = fleet.GetStarfighterCapacity();
            return Math.Min(
                capacity,
                IntegerMath.ScaleByPercentRoundedUp(
                    capacity,
                    context.Game.Config.AI.Infrastructure.StarfighterParentFillPercent
                )
            );
        }

        /// <summary>
        /// Returns desired regiment count for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>The desired regiment count.</returns>
        private int GetDesiredRegimentCount(AITurnContext context, Fleet fleet)
        {
            if (fleet.Order?.OrderType == FleetOrderType.Defend)
                return 0;

            if (fleet.RoleType == FleetRoleType.Colonization)
            {
                return Math.Max(
                    context.Game.Config.AI.FleetDeployment.ColonizationFleetMinimumRegimentCount,
                    context.Game.Config.AI.FleetDeployment.ColonizationFleetMaximumRegimentCount
                );
            }

            int capacity = fleet.GetRegimentCapacity();
            int fillTarget = IntegerMath.ScaleByPercentRoundedUp(
                capacity,
                context.Game.Config.AI.Infrastructure.AssaultRegimentLoadPercent
            );
            Planet targetPlanet = context.Assessment.GetAttackTargetPlanet(fleet);
            if (targetPlanet != null)
                fillTarget = Math.Max(
                    fillTarget,
                    context.AttackRequirements.GetRegimentCount(
                        fleet,
                        targetPlanet,
                        projected: true
                    )
                );

            if (
                targetPlanet != null
                && context.Assessment.GetProjectedFleetRegimentAttackStrength(fleet)
                    < context.AttackRequirements.GetRegimentStrength(
                        fleet,
                        targetPlanet,
                        projected: true
                    )
            )
            {
                int currentCount = fleet.GetCurrentRegimentCount();
                int currentStrength = context.Assessment.GetProjectedFleetRegimentAttackStrength(
                    fleet
                );
                int requiredStrength = context.AttackRequirements.GetRegimentStrength(
                    fleet,
                    targetPlanet,
                    projected: true
                );
                int estimatedStrengthPerRegiment = Math.Max(
                    1,
                    currentCount > 0 ? currentStrength / currentCount : requiredStrength
                );
                int strengthDeficitCount = IntegerMath.DivideRoundedUp(
                    requiredStrength - currentStrength,
                    estimatedStrengthPerRegiment
                );
                fillTarget = Math.Max(fillTarget, currentCount + strengthDeficitCount);
            }

            return fillTarget;
        }

        /// <summary>
        /// Returns the active attack target for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>The attack target planet, or null.</returns>
        private Planet GetAttackTargetPlanet(AITurnContext context, Fleet fleet)
        {
            string targetPlanetId = fleet.Order?.TargetPlanetId;
            if (
                fleet.Order?.OrderType != FleetOrderType.Attack
                || string.IsNullOrEmpty(targetPlanetId)
            )
                return null;

            Planet targetPlanet = context.Assessment.GetKnownPlanet(targetPlanetId);
            string targetOwnerId = targetPlanet?.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(targetOwnerId) || targetOwnerId == context.Faction.InstanceID)
                return null;

            return targetPlanet;
        }
    }
}
