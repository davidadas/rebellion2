using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Units;
using Rebellion.Game.World;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds production demand from faction state and current force needs.
    /// </summary>
    public sealed class AIProductionDemandGenerator
    {
        /// <summary>
        /// Returns production demand for the current AI turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Production demand generated for this faction.</returns>
        public List<AIProductionDemand> Generate(AITurnContext context)
        {
            List<AIProductionDemand> demands = new List<AIProductionDemand>();

            if (context?.Game == null || context.Faction == null || context.Assessment == null)
                return demands;

            AddResourceBalanceDemand(context, demands);
            AddInfrastructureFacilityDemands(context, demands);
            AddFleetReinforcementDemands(context, demands);
            AddLocalReserveDemands(context, demands);

            return demands;
        }

        /// <summary>
        /// Adds production facility demands.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddInfrastructureFacilityDemands(
            AITurnContext context,
            List<AIProductionDemand> demands
        )
        {
            AddConstructionFacilityDemand(context, demands);
            AddInfrastructureFacilityDemand(
                context,
                demands,
                AIProductionDemandKind.Shipyard,
                BuildingType.Shipyard,
                context.Game.Config.AI.Infrastructure.PlanetsPerShipyard,
                context.Game.Config.AI.Infrastructure.ShipyardDemandPercent
            );
            AddInfrastructureFacilityDemand(
                context,
                demands,
                AIProductionDemandKind.TrainingFacility,
                BuildingType.TrainingFacility,
                context.Game.Config.AI.Infrastructure.PlanetsPerTrainingFacility,
                context.Game.Config.AI.Infrastructure.TrainingFacilityDemandPercent
            );
        }

        /// <summary>
        /// Adds construction facility demand when construction capacity is short.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddConstructionFacilityDemand(
            AITurnContext context,
            List<AIProductionDemand> demands
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int desiredCount = GetDesiredConstructionFacilityCount(context, config);
            int currentCount = GetOwnedFacilityCount(context, BuildingType.ConstructionFacility);
            if (ShouldAddConstructionFacilityForAssessment(context, config, currentCount))
                desiredCount = System.Math.Max(desiredCount, currentCount + 1);

            int deficit = desiredCount - currentCount;
            if (deficit <= 0)
                return;

            Planet target = FindFacilityTargetPlanet(context, BuildingType.ConstructionFacility);
            if (target != null)
                demands.Add(
                    CreateBuildingDemand(
                        context,
                        AIProductionDemandKind.ConstructionFacility,
                        BuildingType.ConstructionFacility,
                        target,
                        deficit,
                        desiredCount,
                        config.ConstructionFacilityDemandPercent
                    )
                );
        }

        /// <summary>
        /// Adds demand for one infrastructure facility type.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="kind">Demand kind to add.</param>
        /// <param name="buildingType">Building type to request.</param>
        /// <param name="planetsPerFacility">Planet count represented by one facility.</param>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        private void AddInfrastructureFacilityDemand(
            AITurnContext context,
            List<AIProductionDemand> demands,
            AIProductionDemandKind kind,
            BuildingType buildingType,
            int planetsPerFacility,
            int baseDemandPercent
        )
        {
            int desiredCount = GetDesiredFacilityCount(context, planetsPerFacility);
            int currentCount = GetOwnedFacilityCount(context, buildingType);
            int deficit = desiredCount - currentCount;
            if (deficit <= 0)
                return;

            Planet target = FindFacilityTargetPlanet(context, buildingType);
            if (target == null)
                return;

            demands.Add(
                CreateBuildingDemand(
                    context,
                    kind,
                    buildingType,
                    target,
                    deficit,
                    desiredCount,
                    baseDemandPercent
                )
            );
        }

        /// <summary>
        /// Adds local reserve unit demands for owned planets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddLocalReserveDemands(AITurnContext context, List<AIProductionDemand> demands)
        {
            foreach (Planet planet in context.Assessment.OwnedPlanets)
            {
                AddLocalStarfighterReserveDemand(context, demands, planet);
                AddGarrisonRegimentReserveDemand(context, demands, planet);
            }
        }

        /// <summary>
        /// Adds local starfighter reserve demand for a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="planet">The planet to inspect.</param>
        private void AddLocalStarfighterReserveDemand(
            AITurnContext context,
            List<AIProductionDemand> demands,
            Planet planet
        )
        {
            int targetCount = GetTargetLocalStarfighterReserveCount(context, planet);
            int deficit = targetCount - planet.GetAllStarfighters().Count;
            if (deficit <= 0)
                return;

            demands.Add(
                CreateUnitDemand(
                    context,
                    AIProductionDemandKind.LocalStarfighterReserve,
                    ManufacturingType.Ship,
                    planet,
                    deficit,
                    targetCount,
                    context.Game.Config.AI.Infrastructure.FleetStarfighterDemandPercent
                )
            );
        }

        /// <summary>
        /// Adds local garrison regiment demand for a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="planet">The planet to inspect.</param>
        private void AddGarrisonRegimentReserveDemand(
            AITurnContext context,
            List<AIProductionDemand> demands,
            Planet planet
        )
        {
            int targetCount = GetTargetGarrisonRegimentReserveCount(context, planet);
            int deficit = targetCount - planet.GetAllRegiments().Count;
            if (deficit <= 0)
                return;

            demands.Add(
                CreateUnitDemand(
                    context,
                    AIProductionDemandKind.GarrisonRegimentReserve,
                    ManufacturingType.Troop,
                    planet,
                    deficit,
                    targetCount,
                    context.Game.Config.AI.Infrastructure.FleetRegimentDemandPercent
                )
            );
        }

        /// <summary>
        /// Adds reinforcement demand for owned fleets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddFleetReinforcementDemands(
            AITurnContext context,
            List<AIProductionDemand> demands
        )
        {
            foreach (Fleet fleet in context.Assessment.OwnedFleets)
            {
                if (!CanReinforceFleet(fleet))
                    continue;

                AddFleetCapitalShipDemand(context, demands, fleet);
                AddFleetStarfighterDemand(context, demands, fleet);
                AddFleetRegimentDemand(context, demands, fleet);
            }
        }

        /// <summary>
        /// Adds capital ship demand for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        private void AddFleetCapitalShipDemand(
            AITurnContext context,
            List<AIProductionDemand> demands,
            Fleet fleet
        )
        {
            Planet targetPlanet = GetAttackTargetPlanet(context, fleet);
            if (targetPlanet == null)
                return;

            int targetCombat = context.Assessment.GetRequiredAttackCombatStrength(targetPlanet);
            int projectedCombat = GetProjectedFleetCombatValue(fleet);
            int combatDeficit = targetCombat - projectedCombat;
            int targetRegimentCapacity = context.Assessment.GetRequiredAttackRegimentCount(
                targetPlanet
            );
            int regimentCapacityDeficit = targetRegimentCapacity - fleet.GetRegimentCapacity();
            int deficit = System.Math.Max(combatDeficit, regimentCapacityDeficit);
            if (deficit <= 0)
                return;

            demands.Add(
                CreateFleetDemand(
                    context,
                    AIProductionDemandKind.FleetCapitalShip,
                    ManufacturingType.Ship,
                    fleet,
                    deficit,
                    combatDeficit > 0 ? targetCombat : targetRegimentCapacity,
                    context.Game.Config.AI.Infrastructure.FleetCapitalShipDemandPercent
                )
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
            List<AIProductionDemand> demands,
            Fleet fleet
        )
        {
            int targetCount = GetTargetStarfighterCount(context, fleet);
            int deficit = targetCount - fleet.GetCurrentStarfighterCount();
            if (deficit <= 0)
                return;

            demands.Add(
                CreateFleetDemand(
                    context,
                    AIProductionDemandKind.FleetStarfighter,
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
            List<AIProductionDemand> demands,
            Fleet fleet
        )
        {
            int targetCount = GetTargetRegimentCount(context, fleet);
            int deficit = targetCount - fleet.GetCurrentRegimentCount();
            if (deficit <= 0)
                return;

            demands.Add(
                CreateFleetDemand(
                    context,
                    AIProductionDemandKind.FleetRegiment,
                    ManufacturingType.Troop,
                    fleet,
                    deficit,
                    targetCount,
                    context.Game.Config.AI.Infrastructure.FleetRegimentDemandPercent
                )
            );
        }

        /// <summary>
        /// Adds mine and refinery demands.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddResourceBalanceDemand(
            AITurnContext context,
            List<AIProductionDemand> demands
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int economyBatchSize = GetEconomyBatchSize(context, config);
            int rawResourceNodes = context.Faction.GetTotalRawResourceNodes();
            int plannedMines = context.Faction.GetTotalRawMinedResources();
            int plannedRefineries = context.Faction.GetTotalRawRefinementCapacity();
            int mineDeficit = GetMineDeficit(
                rawResourceNodes,
                plannedMines,
                plannedRefineries,
                economyBatchSize
            );
            int refineryDeficit = GetRefineryDeficit(
                plannedMines,
                plannedRefineries,
                mineDeficit,
                economyBatchSize
            );
            int economyDemandPercent = GetEconomyDemandPercent(
                rawResourceNodes,
                plannedMines,
                config
            );
            List<Planet> mineTargets = FindMineTargetPlanets(context, mineDeficit).ToList();
            HashSet<string> mineTargetIds = new HashSet<string>(
                mineTargets.Select(planet => planet.InstanceID),
                StringComparer.Ordinal
            );
            List<Planet> refineryTargets = FindRefineryTargetPlanets(
                    context,
                    refineryDeficit,
                    mineTargetIds
                )
                .ToList();

            foreach (Planet target in mineTargets)
            {
                demands.Add(
                    CreateBuildingDemand(
                        context,
                        AIProductionDemandKind.Mine,
                        BuildingType.Mine,
                        target,
                        mineDeficit,
                        plannedMines + mineDeficit,
                        economyDemandPercent
                    )
                );
            }

            foreach (Planet target in refineryTargets)
            {
                demands.Add(
                    CreateBuildingDemand(
                        context,
                        AIProductionDemandKind.Refinery,
                        BuildingType.Refinery,
                        target,
                        refineryDeficit,
                        plannedRefineries + refineryDeficit,
                        economyDemandPercent
                    )
                );
            }
        }

        /// <summary>
        /// Returns how many mine demands should be generated.
        /// </summary>
        /// <param name="rawResourceNodes">Known raw resource nodes.</param>
        /// <param name="plannedMines">Current and queued mine capacity.</param>
        /// <param name="plannedRefineries">Current and queued refinery capacity.</param>
        /// <param name="economyBatchSize">Maximum economy batch size.</param>
        /// <returns>The mine deficit.</returns>
        private int GetMineDeficit(
            int rawResourceNodes,
            int plannedMines,
            int plannedRefineries,
            int economyBatchSize
        )
        {
            if (rawResourceNodes <= plannedMines)
                return 0;

            if (plannedRefineries > plannedMines)
                return System.Math.Min(
                    economyBatchSize,
                    System.Math.Min(
                        plannedRefineries - plannedMines,
                        rawResourceNodes - plannedMines
                    )
                );

            if (plannedRefineries == plannedMines)
                return System.Math.Min(economyBatchSize, rawResourceNodes - plannedMines);

            return 0;
        }

        /// <summary>
        /// Returns how many refinery demands should be generated.
        /// </summary>
        /// <param name="plannedMines">Current and queued mine capacity.</param>
        /// <param name="plannedRefineries">Current and queued refinery capacity.</param>
        /// <param name="selectedMineDeficit">Mine demand selected for this pass.</param>
        /// <param name="economyBatchSize">Maximum economy batch size.</param>
        /// <returns>The refinery deficit.</returns>
        private int GetRefineryDeficit(
            int plannedMines,
            int plannedRefineries,
            int selectedMineDeficit,
            int economyBatchSize
        )
        {
            int desiredRefineries = plannedMines + selectedMineDeficit;
            if (desiredRefineries <= plannedRefineries)
                return 0;

            return System.Math.Min(economyBatchSize, desiredRefineries - plannedRefineries);
        }

        /// <summary>
        /// Returns the demand pressure for economy buildings.
        /// </summary>
        /// <param name="rawResourceNodes">Known raw resource nodes.</param>
        /// <param name="plannedMines">Current and queued mine capacity.</param>
        /// <param name="config">AI infrastructure configuration.</param>
        /// <returns>The economy demand pressure.</returns>
        private int GetEconomyDemandPercent(
            int rawResourceNodes,
            int plannedMines,
            GameConfig.AIInfrastructureConfig config
        )
        {
            if (rawResourceNodes <= 0)
                return config.EconomyDemandPercent;

            int minedCoveragePercent = plannedMines * 100 / rawResourceNodes;
            if (minedCoveragePercent <= config.EconomySevereDeficitPercent)
                return config.EconomySevereDemandPercent;

            return config.EconomyDemandPercent;
        }

        /// <summary>
        /// Returns how many economy demands may be generated this turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="config">AI infrastructure configuration.</param>
        /// <returns>The economy batch size.</returns>
        private int GetEconomyBatchSize(
            AITurnContext context,
            GameConfig.AIInfrastructureConfig config
        )
        {
            int availableBuildingLanes = context.Assessment.GetAvailableProductionLaneCount(
                ManufacturingType.Building
            );
            int economyLaneBudget = availableBuildingLanes - config.EconomyCompetingNeedSlotReserve;
            return System.Math.Max(
                config.EconomyDefaultBatchSize,
                System.Math.Max(0, economyLaneBudget)
            );
        }

        /// <summary>
        /// Creates a building production demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">Demand kind.</param>
        /// <param name="buildingType">Building type requested.</param>
        /// <param name="target">Planet receiving the building.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <param name="targetCount">Target count.</param>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <returns>The production demand.</returns>
        private AIProductionDemand CreateBuildingDemand(
            AITurnContext context,
            AIProductionDemandKind kind,
            BuildingType buildingType,
            Planet target,
            int deficit,
            int targetCount,
            int baseDemandPercent
        )
        {
            return new AIProductionDemand(
                $"production:{context.Faction.InstanceID}:{kind}:{target.InstanceID}",
                kind,
                ManufacturingType.Building,
                buildingType,
                target,
                deficit,
                GetDemandPressure(context, kind, deficit, targetCount, baseDemandPercent)
            );
        }

        /// <summary>
        /// Creates a planet unit production demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">Demand kind.</param>
        /// <param name="manufacturingType">Manufacturing type required.</param>
        /// <param name="planet">Planet receiving the unit.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <param name="targetCount">Target count.</param>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <returns>The production demand.</returns>
        private AIProductionDemand CreateUnitDemand(
            AITurnContext context,
            AIProductionDemandKind kind,
            ManufacturingType manufacturingType,
            Planet planet,
            int deficit,
            int targetCount,
            int baseDemandPercent
        )
        {
            return new AIProductionDemand(
                $"production:{context.Faction.InstanceID}:{kind}:{planet.InstanceID}",
                kind,
                manufacturingType,
                BuildingType.None,
                planet,
                deficit,
                GetDemandPressure(context, kind, deficit, targetCount, baseDemandPercent)
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
        /// <returns>The production demand.</returns>
        private AIProductionDemand CreateFleetDemand(
            AITurnContext context,
            AIProductionDemandKind kind,
            ManufacturingType manufacturingType,
            Fleet fleet,
            int deficit,
            int targetCount,
            int baseDemandPercent
        )
        {
            return new AIProductionDemand(
                $"production:{context.Faction.InstanceID}:{kind}:{fleet.InstanceID}",
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
                )
            );
        }

        /// <summary>
        /// Returns mine destination planets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="count">Maximum number of planets to return.</param>
        /// <returns>Mine destination planets.</returns>
        private IEnumerable<Planet> FindMineTargetPlanets(AITurnContext context, int count)
        {
            if (count <= 0)
                return Enumerable.Empty<Planet>();

            return GetEconomyDestinationPlanets(context)
                .Where(planet => planet.GetUnminedResourceNodeCount() > 0)
                .OrderByDescending(planet => planet.GetUnminedResourceNodeCount())
                .ThenByDescending(planet => planet.GetAvailableEnergy())
                .ThenBy(planet => planet.InstanceID)
                .Take(count);
        }

        /// <summary>
        /// Returns refinery destination planets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="count">Maximum number of planets to return.</param>
        /// <param name="excludedPlanetIds">Planet ids already selected for mine demand.</param>
        /// <returns>Refinery destination planets.</returns>
        private IEnumerable<Planet> FindRefineryTargetPlanets(
            AITurnContext context,
            int count,
            HashSet<string> excludedPlanetIds
        )
        {
            if (count <= 0)
                return Enumerable.Empty<Planet>();

            List<Planet> preferredTargets = GetEconomyDestinationPlanets(context)
                .Where(planet => !excludedPlanetIds.Contains(planet.InstanceID))
                .OrderBy(planet => planet.GetTotalBuildingTypeCount(BuildingType.Refinery))
                .ThenByDescending(planet => planet.GetAvailableEnergy())
                .ThenBy(planet => planet.InstanceID)
                .Take(count)
                .ToList();

            if (preferredTargets.Count >= count)
                return preferredTargets;

            preferredTargets.AddRange(
                GetEconomyDestinationPlanets(context)
                    .Where(planet => excludedPlanetIds.Contains(planet.InstanceID))
                    .OrderBy(planet => planet.GetTotalBuildingTypeCount(BuildingType.Refinery))
                    .ThenByDescending(planet => planet.GetAvailableEnergy())
                    .ThenBy(planet => planet.InstanceID)
                    .Take(count - preferredTargets.Count)
            );

            return preferredTargets;
        }

        /// <summary>
        /// Returns a planet for a facility demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="buildingType">Facility type requested.</param>
        /// <returns>The target planet, or null.</returns>
        private Planet FindFacilityTargetPlanet(AITurnContext context, BuildingType buildingType)
        {
            return GetBuildingDestinationPlanets(context)
                .OrderBy(planet => planet.GetTotalBuildingTypeCount(buildingType))
                .ThenByDescending(planet => planet.GetAvailableEnergy())
                .ThenByDescending(planet => GetReplaceableExcessFacilityCount(context, planet))
                .ThenBy(planet => planet.InstanceID)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns planets that can receive buildings.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Building destination planets.</returns>
        private IEnumerable<Planet> GetBuildingDestinationPlanets(AITurnContext context)
        {
            return context.Assessment.OwnedPlanets.Where(planet =>
                IsBuildingDestinationPlanet(context, planet)
            );
        }

        /// <summary>
        /// Returns planets that can receive economy buildings.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Economy destination planets.</returns>
        private IEnumerable<Planet> GetEconomyDestinationPlanets(AITurnContext context)
        {
            return context.Assessment.OwnedPlanets.Where(IsOwnedUsablePlanet);
        }

        /// <summary>
        /// Returns whether a planet can receive a building demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True if the planet can receive a building.</returns>
        private bool IsBuildingDestinationPlanet(AITurnContext context, Planet planet)
        {
            if (planet == null)
                return false;

            return IsOwnedUsablePlanet(planet)
                && (
                    planet.GetAvailableEnergy() > 0
                    || GetReplaceableExcessFacilityCount(context, planet) > 0
                );
        }

        /// <summary>
        /// Returns whether a planet is an owned usable colony.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True if the planet is usable.</returns>
        private bool IsOwnedUsablePlanet(Planet planet)
        {
            return planet?.IsDestroyed == false;
        }

        /// <summary>
        /// Returns the desired facility count for a ratio.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planetsPerFacility">Planet count represented by one facility.</param>
        /// <returns>The desired facility count.</returns>
        private int GetDesiredFacilityCount(AITurnContext context, int planetsPerFacility)
        {
            return CeilingDivide(context.Assessment.OwnedPlanets.Count, planetsPerFacility);
        }

        /// <summary>
        /// Returns the desired construction facility count.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="config">AI infrastructure configuration.</param>
        /// <returns>The desired construction facility count.</returns>
        private int GetDesiredConstructionFacilityCount(
            AITurnContext context,
            GameConfig.AIInfrastructureConfig config
        )
        {
            int ratioCount = GetDesiredFacilityCount(
                context,
                config.PlanetsPerConstructionFacility
            );
            int laneCount = System.Math.Min(
                context.Assessment.OwnedPlanets.Count,
                config.MinimumConstructionFacilityLanes
            );
            return System.Math.Max(ratioCount, laneCount);
        }

        /// <summary>
        /// Returns whether assessment pressure should add construction demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="config">AI infrastructure configuration.</param>
        /// <param name="currentCount">Current construction facility count.</param>
        /// <returns>True if construction demand should increase.</returns>
        private bool ShouldAddConstructionFacilityForAssessment(
            AITurnContext context,
            GameConfig.AIInfrastructureConfig config,
            int currentCount
        )
        {
            if (currentCount <= 0)
                return context.Assessment.OwnedPlanets.Count > 0;

            return HasConstructionBacklogPressure(context, config);
        }

        /// <summary>
        /// Returns whether building backlog pressure needs more construction capacity.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="config">AI infrastructure configuration.</param>
        /// <returns>True if construction backlog pressure is high.</returns>
        private bool HasConstructionBacklogPressure(
            AITurnContext context,
            GameConfig.AIInfrastructureConfig config
        )
        {
            if (context.Assessment.GetQueuedProductionWork(ManufacturingType.Building) <= 0)
                return false;

            if (
                context.Assessment.GetQueuedProductionClearTicks(ManufacturingType.Building)
                <= config.ConstructionFacilityTargetClearTicks
            )
                return false;

            return context.Assessment.GetIdleProductionThroughput(ManufacturingType.Building) <= 0;
        }

        /// <summary>
        /// Returns current and queued facility count for a building type.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="buildingType">Building type to count.</param>
        /// <returns>The owned facility count.</returns>
        private int GetOwnedFacilityCount(AITurnContext context, BuildingType buildingType)
        {
            return context.Assessment.OwnedPlanets.Sum(planet =>
                planet.GetBuildingTypeCount(buildingType)
                + GetQueuedBuildingCount(planet, buildingType)
            );
        }

        /// <summary>
        /// Returns total replaceable excess facility count on a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The replaceable excess facility count.</returns>
        private int GetReplaceableExcessFacilityCount(AITurnContext context, Planet planet)
        {
            if (context?.Assessment == null || planet == null)
                return 0;

            return GetReplaceableExcessFacilityCount(
                    context,
                    planet,
                    BuildingType.ConstructionFacility
                )
                + GetReplaceableExcessFacilityCount(context, planet, BuildingType.Shipyard)
                + GetReplaceableExcessFacilityCount(context, planet, BuildingType.TrainingFacility);
        }

        /// <summary>
        /// Returns replaceable excess count for one facility type on a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="buildingType">Facility type to inspect.</param>
        /// <returns>The replaceable excess facility count.</returns>
        private int GetReplaceableExcessFacilityCount(
            AITurnContext context,
            Planet planet,
            BuildingType buildingType
        )
        {
            int localReplaceableCount = System.Math.Max(
                0,
                planet.GetBuildingTypeCount(buildingType) - 1
            );
            if (localReplaceableCount <= 0)
                return 0;

            int globalExcessCount =
                GetCurrentFacilityCount(context, buildingType)
                - GetDesiredReplacementFloor(context, buildingType);
            if (globalExcessCount <= 0)
                return 0;

            return System.Math.Min(localReplaceableCount, globalExcessCount);
        }

        /// <summary>
        /// Returns current completed facility count for a building type.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="buildingType">Facility type to count.</param>
        /// <returns>The completed facility count.</returns>
        private int GetCurrentFacilityCount(AITurnContext context, BuildingType buildingType)
        {
            return context.Assessment.OwnedPlanets.Sum(planet =>
                planet.GetBuildingTypeCount(buildingType)
            );
        }

        /// <summary>
        /// Returns the facility floor used before replacement.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="buildingType">Facility type to inspect.</param>
        /// <returns>The desired replacement floor.</returns>
        private int GetDesiredReplacementFloor(AITurnContext context, BuildingType buildingType)
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            return buildingType switch
            {
                BuildingType.Shipyard => GetDesiredFacilityCount(
                    context,
                    config.PlanetsPerShipyard
                ),
                BuildingType.TrainingFacility => GetDesiredFacilityCount(
                    context,
                    config.PlanetsPerTrainingFacility
                ),
                BuildingType.ConstructionFacility => GetDesiredConstructionFacilityCount(
                    context,
                    config
                ),
                _ => 0,
            };
        }

        /// <summary>
        /// Returns queued building count for a building type on a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="buildingType">Building type to count.</param>
        /// <returns>The queued building count.</returns>
        private int GetQueuedBuildingCount(Planet planet, BuildingType buildingType)
        {
            if (
                planet
                    .GetManufacturingQueue()
                    .TryGetValue(ManufacturingType.Building, out List<IManufacturable> queue)
            )
            {
                return queue
                    .OfType<Building>()
                    .Count(building => building.GetBuildingType() == buildingType);
            }

            return 0;
        }

        /// <summary>
        /// Returns pressure for non-fleet production demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">Demand kind.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <param name="targetCount">Target count.</param>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <returns>The demand pressure.</returns>
        private double GetDemandPressure(
            AITurnContext context,
            AIProductionDemandKind kind,
            int deficit,
            int targetCount,
            int baseDemandPercent
        )
        {
            double pressure = GetBasePressure(baseDemandPercent, deficit, targetCount);

            if (kind is AIProductionDemandKind.Mine or AIProductionDemandKind.Refinery)
                pressure += GetEconomyMaintenancePressure(context);

            return ClampPressure(pressure);
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
            AIProductionDemandKind kind,
            Fleet fleet,
            int deficit,
            int targetCount,
            int baseDemandPercent
        )
        {
            double pressure = GetBasePressure(baseDemandPercent, deficit, targetCount);
            Planet targetPlanet = GetAttackTargetPlanet(context, fleet);

            if (targetPlanet != null)
            {
                pressure += GetTargetValuePressure(context, targetPlanet);
                pressure += GetFleetReadinessPressure(context, kind, fleet, targetPlanet);
                pressure += GetFinalReadinessGatePressure(context, fleet, targetPlanet, deficit);
            }

            if (kind == AIProductionDemandKind.FleetStarfighter)
                pressure += GetStarfighterFillPressure(context, fleet, targetCount);

            return ClampPressure(pressure);
        }

        /// <summary>
        /// Returns base pressure for a demand.
        /// </summary>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <param name="targetCount">Target count.</param>
        /// <returns>The base pressure.</returns>
        private double GetBasePressure(int baseDemandPercent, int deficit, int targetCount)
        {
            int deficitPercent = deficit * 100 / System.Math.Max(1, targetCount);
            return System.Math.Min(100, baseDemandPercent + deficitPercent);
        }

        /// <summary>
        /// Returns extra economy pressure from maintenance headroom.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The economy maintenance pressure.</returns>
        private double GetEconomyMaintenancePressure(AITurnContext context)
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int headroom = context.Faction.ProjectedMaintenanceHeadroom;
            int reserve = context
                .Game
                .Config
                .AI
                .Selection
                .MinimumMaintenanceHeadroomAfterProduction;

            if (headroom < 0)
                return config.EconomyMaintenanceShortfallPressure;

            if (headroom >= reserve)
                return 0;

            return config.EconomyMaintenanceReservePressure
                * (reserve - headroom)
                / System.Math.Max(1, reserve);
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

            return context.Game.Config.AI.Infrastructure.FleetTargetValuePressureWeight
                * context.Assessment.GetPlanetValue(targetPlanet)
                / highestValue;
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
            AIProductionDemandKind kind,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int requiredCombat = context.Assessment.GetRequiredAttackCombatStrength(targetPlanet);
            int requiredRegiments = context.Assessment.GetRequiredAttackRegimentCount(targetPlanet);
            double combatReadiness = GetFulfillmentRatio(
                GetProjectedFleetCombatValue(fleet),
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
                AIProductionDemandKind.FleetRegiment => config.FleetReadinessPressureWeight
                    * (combatReadiness + capacityReadiness)
                    / 2,
                AIProductionDemandKind.FleetCapitalShip => config.FleetReadinessPressureWeight
                    * (regimentReadiness + capacityReadiness)
                    / 2,
                AIProductionDemandKind.FleetStarfighter => config.FleetReadinessPressureWeight
                    * (combatReadiness + regimentReadiness + capacityReadiness)
                    / 3,
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

            int requiredCombat = context.Assessment.GetRequiredAttackCombatStrength(targetPlanet);
            int requiredRegiments = context.Assessment.GetRequiredAttackRegimentCount(targetPlanet);
            bool combatReady = GetProjectedFleetCombatValue(fleet) >= requiredCombat;
            bool capacityReady =
                context.Assessment.GetFleetRegimentCapacity(fleet) >= requiredRegiments;

            if (!combatReady || !capacityReady)
                return 0;

            return config.FleetFinalReadinessGatePressure
                * (config.FleetFinalReadinessGateUnitCount - deficit + 1)
                / config.FleetFinalReadinessGateUnitCount;
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
            return context.Game.Config.AI.Infrastructure.FleetStarfighterFillPressureWeight
                * (targetCount - loadedCount)
                / targetCount;
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

            return System.Math.Max(0, System.Math.Min(1, value / target));
        }

        /// <summary>
        /// Clamps pressure to the scoring range.
        /// </summary>
        /// <param name="pressure">Pressure to clamp.</param>
        /// <returns>The clamped pressure.</returns>
        private double ClampPressure(double pressure)
        {
            return System.Math.Max(0, System.Math.Min(100, pressure));
        }

        /// <summary>
        /// Returns whether a fleet can receive reinforcement demand.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True if the fleet can receive reinforcement.</returns>
        private bool CanReinforceFleet(Fleet fleet)
        {
            return fleet != null
                && fleet.RoleType == FleetRoleType.Battle
                && (
                    HasCommittedCapitalShips(fleet)
                    || fleet.Order?.OrderType == FleetOrderType.Attack
                );
        }

        private static int GetProjectedFleetCombatValue(Fleet fleet)
        {
            if (fleet == null)
                return 0;

            int committedCapitalCombat = fleet
                .CapitalShips.Where(ship => ship?.IsCommittedToFleet() == true)
                .Sum(ship => ship.GetPrimaryWeaponStrength());
            return System.Math.Max(fleet.GetCombatValue(), committedCapitalCombat);
        }

        private static bool HasCommittedCapitalShips(Fleet fleet)
        {
            return fleet?.CapitalShips.Any(ship => ship?.IsCommittedToFleet() == true) == true;
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
            return System.Math.Min(
                capacity,
                ScaleByPercent(
                    capacity,
                    context.Game.Config.AI.Infrastructure.StarfighterParentFillPercent
                )
            );
        }

        /// <summary>
        /// Returns target regiment count for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>The target regiment count.</returns>
        private int GetTargetRegimentCount(AITurnContext context, Fleet fleet)
        {
            int capacity = fleet.GetRegimentCapacity();
            int fillTarget = ScaleByPercent(
                capacity,
                context.Game.Config.AI.Infrastructure.AssaultRegimentLoadPercent
            );
            Planet targetPlanet = GetAttackTargetPlanet(context, fleet);
            if (targetPlanet != null)
                fillTarget = System.Math.Max(
                    fillTarget,
                    context.Assessment.GetRequiredAttackRegimentCount(targetPlanet)
                );

            return System.Math.Min(capacity, fillTarget);
        }

        /// <summary>
        /// Returns local starfighter reserve target for a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>The target local starfighter reserve count.</returns>
        private int GetTargetLocalStarfighterReserveCount(AITurnContext context, Planet planet)
        {
            return ScaleByPercent(
                planet.GetProductionFacilityCount(ManufacturingType.Ship),
                context.Game.Config.AI.Infrastructure.StarfighterLocalReservePercent
            );
        }

        /// <summary>
        /// Returns garrison regiment reserve target for a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>The target garrison regiment reserve count.</returns>
        private int GetTargetGarrisonRegimentReserveCount(AITurnContext context, Planet planet)
        {
            return ScaleByPercent(
                planet.GetProductionFacilityCount(ManufacturingType.Troop),
                context.Game.Config.AI.Infrastructure.GarrisonRegimentReservePercent
            );
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

            Planet targetPlanet = context.Game.GetSceneNodeByInstanceID<Planet>(targetPlanetId);
            string targetOwnerId = targetPlanet?.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(targetOwnerId) || targetOwnerId == context.Faction.InstanceID)
                return null;

            return targetPlanet;
        }

        /// <summary>
        /// Scales an integer by a percent value and rounds up.
        /// </summary>
        /// <param name="value">Value to scale.</param>
        /// <param name="percent">Percent to apply.</param>
        /// <returns>The scaled value.</returns>
        private int ScaleByPercent(int value, int percent)
        {
            return (value * percent + 99) / 100;
        }

        /// <summary>
        /// Divides two integers and rounds up.
        /// </summary>
        /// <param name="value">Value to divide.</param>
        /// <param name="divisor">Divisor to use.</param>
        /// <returns>The rounded-up quotient.</returns>
        private int CeilingDivide(int value, int divisor)
        {
            if (divisor <= 0)
                return 0;

            return (value + divisor - 1) / divisor;
        }
    }
}
