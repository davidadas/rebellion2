using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Core;
using Rebellion.AI.Fleets;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Production
{
    /// <summary>
    /// Generates requirements that establish battle and colonization fleets.
    /// </summary>
    internal sealed class AIFleetFormationRequirements
    {
        /// <summary>
        /// Adds demands that establish missing battle fleets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        internal void AddFleetSeedRequirements(
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
                    AIFleetProductionAllocationScorer.ScoreDeficit(
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
        internal void AddColonizationFleetSeedRequirements(
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
    }
}
