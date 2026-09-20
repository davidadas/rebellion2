using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Scoring;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Generates requirements needed to restore resource balance.
    /// </summary>
    internal sealed class AIResourceRequirements
    {
        /// <summary>
        /// Adds mine and refinery requirements needed to restore resource balance.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="requirements">The requirement collection to update.</param>
        internal void AddResourceRequirements(
            AITurnContext context,
            ICollection<AIProductionRequirement> requirements
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            if (!NeedsExpansion(context))
                return;

            int batchSize = GetBatchSize(context, config);
            int rawResourceNodes = context.Faction.GetTotalRawResourceNodes();
            int plannedMines = context.Faction.GetTotalRawMinedResources();
            int plannedRefineries = context.Faction.GetTotalRawRefinementCapacity();
            int mineDeficit = GetMineDeficit(
                rawResourceNodes,
                plannedMines,
                plannedRefineries,
                batchSize
            );
            int refineryDeficit = GetRefineryDeficit(
                plannedMines,
                plannedRefineries,
                mineDeficit,
                batchSize
            );
            int demandPercent = GetDemandPercent(rawResourceNodes, plannedMines, config);
            List<Planet> mineTargets = FindMineTargets(context, mineDeficit).ToList();
            HashSet<string> mineTargetIds = new HashSet<string>(
                mineTargets.Select(planet => planet.InstanceID),
                StringComparer.Ordinal
            );
            List<Planet> refineryTargets = FindRefineryTargets(
                    context,
                    refineryDeficit,
                    mineTargetIds
                )
                .ToList();

            foreach (Planet target in mineTargets)
            {
                requirements.Add(
                    CreateBuildingRequirement(
                        context,
                        AIProductionRequirementKind.Mine,
                        BuildingType.Mine,
                        target,
                        plannedMines + mineDeficit,
                        demandPercent
                    )
                );
            }

            foreach (Planet target in refineryTargets)
            {
                requirements.Add(
                    CreateBuildingRequirement(
                        context,
                        AIProductionRequirementKind.Refinery,
                        BuildingType.Refinery,
                        target,
                        plannedRefineries + refineryDeficit,
                        demandPercent
                    )
                );
            }
        }

        /// <summary>
        /// Returns whether resource production currently constrains the faction.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when economy expansion is required.</returns>
        private static bool NeedsExpansion(AITurnContext context)
        {
            return context.Assessment.PendingRawMaterialRequestCount > 0
                || context.Assessment.PendingRefinedMaterialRequestCount > 0
                || GetProjectedRefinedMaterialPercent(context)
                    <= context.Game.Config.AI.Selection.RefinedMaterialEconomyWarningPercent
                || context.Assessment.ProjectedEconomyMaintenanceHeadroom
                    < context.Game.Config.AI.Selection.MaintenanceHeadroomTarget;
        }

        /// <summary>
        /// Returns the number of missing mines to request this turn.
        /// </summary>
        /// <param name="rawResourceNodes">Known raw resource nodes.</param>
        /// <param name="plannedMines">Current and queued mine capacity.</param>
        /// <param name="plannedRefineries">Current and queued refinery capacity.</param>
        /// <param name="batchSize">Maximum economy batch size.</param>
        /// <returns>The mine deficit.</returns>
        private static int GetMineDeficit(
            int rawResourceNodes,
            int plannedMines,
            int plannedRefineries,
            int batchSize
        )
        {
            if (rawResourceNodes <= plannedMines)
                return 0;
            if (plannedRefineries > plannedMines)
            {
                return Math.Min(
                    batchSize,
                    Math.Min(plannedRefineries - plannedMines, rawResourceNodes - plannedMines)
                );
            }
            return plannedRefineries == plannedMines
                ? Math.Min(batchSize, rawResourceNodes - plannedMines)
                : 0;
        }

        /// <summary>
        /// Returns the number of missing refineries to request this turn.
        /// </summary>
        /// <param name="plannedMines">Current and queued mine capacity.</param>
        /// <param name="plannedRefineries">Current and queued refinery capacity.</param>
        /// <param name="mineDeficit">Mine requirements selected for this pass.</param>
        /// <param name="batchSize">Maximum economy batch size.</param>
        /// <returns>The refinery deficit.</returns>
        private static int GetRefineryDeficit(
            int plannedMines,
            int plannedRefineries,
            int mineDeficit,
            int batchSize
        )
        {
            return Math.Min(batchSize, Math.Max(0, plannedMines + mineDeficit - plannedRefineries));
        }

        /// <summary>
        /// Returns the configured economy pressure for current mine coverage.
        /// </summary>
        /// <param name="rawResourceNodes">Known raw resource nodes.</param>
        /// <param name="plannedMines">Current and queued mine capacity.</param>
        /// <param name="config">AI infrastructure configuration.</param>
        /// <returns>The base economy demand percent.</returns>
        private static int GetDemandPercent(
            int rawResourceNodes,
            int plannedMines,
            GameConfig.AIInfrastructureConfig config
        )
        {
            if (rawResourceNodes <= 0)
                return config.EconomyDemandPercent;
            return plannedMines * 100 / rawResourceNodes <= config.EconomySevereDeficitPercent
                ? config.EconomySevereDemandPercent
                : config.EconomyDemandPercent;
        }

        /// <summary>
        /// Returns the economy requirement batch size for available construction lanes.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="config">AI infrastructure configuration.</param>
        /// <returns>The economy batch size.</returns>
        private static int GetBatchSize(
            AITurnContext context,
            GameConfig.AIInfrastructureConfig config
        )
        {
            int availableLanes = context.Assessment.GetAvailableProductionLaneCount(
                ManufacturingType.Building
            );
            return Math.Max(
                config.EconomyDefaultBatchSize,
                Math.Max(0, availableLanes - config.EconomyCompetingNeedSlotReserve)
            );
        }

        /// <summary>
        /// Creates one economy building requirement.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">The economy requirement kind.</param>
        /// <param name="buildingType">The required building type.</param>
        /// <param name="target">The destination planet.</param>
        /// <param name="targetCount">The projected faction-wide target count.</param>
        /// <param name="baseDemandPercent">The base economy pressure.</param>
        /// <returns>The production requirement.</returns>
        private static AIProductionRequirement CreateBuildingRequirement(
            AITurnContext context,
            AIProductionRequirementKind kind,
            BuildingType buildingType,
            Planet target,
            int targetCount,
            int baseDemandPercent
        )
        {
            return new AIProductionRequirement(
                AIProductionRequirement.CreateId(
                    context.Faction.InstanceID,
                    kind,
                    target.InstanceID
                ),
                kind,
                ManufacturingType.Building,
                buildingType,
                target,
                1,
                GetPressure(context, baseDemandPercent, targetCount)
            );
        }

        /// <summary>
        /// Returns candidate planets for new mines.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="count">Maximum number of planets.</param>
        /// <returns>Ranked mine destinations.</returns>
        private static IEnumerable<Planet> FindMineTargets(AITurnContext context, int count)
        {
            return count <= 0
                ? Enumerable.Empty<Planet>()
                : GetBuildingDestinations(context)
                    .Where(planet => planet.GetUnminedResourceNodeCount() > 0)
                    .OrderByDescending(planet => planet.GetUnminedResourceNodeCount())
                    .ThenByDescending(planet => planet.GetAvailableEnergy())
                    .ThenBy(planet => planet.InstanceID)
                    .Take(count);
        }

        /// <summary>
        /// Returns candidate planets for new refineries.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="count">Maximum number of planets.</param>
        /// <param name="excludedPlanetIds">Planets already selected for mines.</param>
        /// <returns>Ranked refinery destinations.</returns>
        private static IEnumerable<Planet> FindRefineryTargets(
            AITurnContext context,
            int count,
            HashSet<string> excludedPlanetIds
        )
        {
            if (count <= 0)
                return Enumerable.Empty<Planet>();

            List<Planet> targets = GetBuildingDestinations(context)
                .Where(planet => !excludedPlanetIds.Contains(planet.InstanceID))
                .OrderBy(planet => planet.GetTotalBuildingTypeCount(BuildingType.Refinery))
                .ThenByDescending(planet => planet.GetAvailableEnergy())
                .ThenBy(planet => planet.InstanceID)
                .Take(count)
                .ToList();
            if (targets.Count >= count)
                return targets;

            targets.AddRange(
                GetBuildingDestinations(context)
                    .Where(planet => excludedPlanetIds.Contains(planet.InstanceID))
                    .OrderBy(planet => planet.GetTotalBuildingTypeCount(BuildingType.Refinery))
                    .ThenByDescending(planet => planet.GetAvailableEnergy())
                    .ThenBy(planet => planet.InstanceID)
                    .Take(count - targets.Count)
            );
            return targets;
        }

        /// <summary>
        /// Returns owned planets that can receive an economy building.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Eligible building destinations.</returns>
        private static IEnumerable<Planet> GetBuildingDestinations(AITurnContext context)
        {
            return context.Assessment.OwnedPlanets.Where(planet =>
                planet?.IsDestroyed == false && planet.GetAvailableEnergy() > 0
            );
        }

        /// <summary>
        /// Returns the pressure for one economy building requirement.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="baseDemandPercent">The base economy pressure.</param>
        /// <param name="targetCount">The projected faction-wide target count.</param>
        /// <returns>The economy pressure.</returns>
        private static double GetPressure(
            AITurnContext context,
            int baseDemandPercent,
            int targetCount
        )
        {
            double pressure = Math.Min(
                100,
                baseDemandPercent
                    + AIUtility.EvaluateDiscretePressure(
                        1 / (double)Math.Max(1, targetCount),
                        context.Game.Config.AI.Infrastructure.DemandUtility.Deficit
                    )
            );
            pressure += GetMaintenancePressure(context);
            pressure += GetRefinedMaterialPressure(context);
            return pressure;
        }

        /// <summary>
        /// Returns economy pressure from projected maintenance headroom.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The maintenance pressure.</returns>
        private static double GetMaintenancePressure(AITurnContext context)
        {
            GameConfig.AIProductionDemandUtilityConfig utility = context
                .Game
                .Config
                .AI
                .Infrastructure
                .DemandUtility;
            int headroom = context.Assessment.ProjectedEconomyMaintenanceHeadroom;
            int floor = context.Game.Config.AI.Selection.MaintenanceHeadroomReserve;
            int target = Math.Max(
                floor,
                context.Game.Config.AI.Selection.MaintenanceHeadroomTarget
            );
            if (headroom < floor)
                return AIUtility.EvaluatePressure(1, utility.MaintenanceShortfall);
            return headroom >= target
                ? 0
                : AIUtility.EvaluateDiscretePressure(
                    (target - headroom) / (double)Math.Max(1, target - floor),
                    utility.MaintenanceReserve
                );
        }

        /// <summary>
        /// Returns economy pressure from projected refined-material reserves.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The refined-material pressure.</returns>
        private static double GetRefinedMaterialPressure(AITurnContext context)
        {
            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            int reservePercent = Math.Max(0, config.RefinedMaterialReservePercent);
            int warningPercent = Math.Max(
                reservePercent,
                config.RefinedMaterialEconomyWarningPercent
            );
            int projectedPercent = GetProjectedRefinedMaterialPercent(context);
            if (projectedPercent >= warningPercent)
                return 0;

            double urgency = Math.Min(
                1,
                Math.Max(0, warningPercent - projectedPercent)
                    / (double)Math.Max(1, warningPercent - reservePercent)
            );
            return AIUtility.EvaluatePressure(
                urgency,
                context.Game.Config.AI.Infrastructure.DemandUtility.ResourceShortage
            );
        }

        /// <summary>
        /// Returns projected uncommitted refined materials as a percentage of supply.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The projected refined-material percentage.</returns>
        private static int GetProjectedRefinedMaterialPercent(AITurnContext context)
        {
            long projectedStockpile = Math.Max(
                0,
                (long)context.Assessment.RefinedMaterialStockpile
                    - context.Assessment.NearTermRefinedMaterialCommitment
            );
            int supply = context.Assessment.RefinedMaterialSupply;
            if (supply <= 0)
                return projectedStockpile > 0 ? 100 : 0;
            return (int)Math.Min(100, projectedStockpile * 100 / supply);
        }
    }
}
