using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Mathematics;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Processes mine and refinery cycles and the material requests that feed them.
    /// </summary>
    internal sealed class ResourceProductionTickProcessor : ITickProcessor
    {
        private const int _percentScale = 100;

        private readonly SmugglingCommands _smugglingCommands;

        /// <summary>
        /// Creates resource-production tick processing.
        /// </summary>
        /// <param name="smugglingCommands">The smuggling rules applied to completed output.</param>
        public ResourceProductionTickProcessor(SmugglingCommands smugglingCommands)
        {
            _smugglingCommands =
                smugglingCommands ?? throw new ArgumentNullException(nameof(smugglingCommands));
        }

        /// <summary>
        /// Services pending material requests and advances every active resource facility.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <returns>State-change results produced while processing the tick.</returns>
        public IReadOnlyList<GameResult> ProcessTick(GameRoot game)
        {
            _smugglingCommands.BeginProduction();
            List<GameResult> results = new List<GameResult>();

            foreach (Faction faction in game.GetFactions())
                ProcessFaction(game, faction);

            return results;
        }

        /// <summary>
        /// Processes material delivery, maintenance allocation, and resource cycles for one faction.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <param name="faction">The faction to process.</param>
        private void ProcessFaction(GameRoot game, Faction faction)
        {
            ServicePendingRawMaterialRequests(game, faction);
            ServicePendingRefinedMaterialRequests(game, faction);

            List<Building> mines = ResourceProductionQueries.GetActiveFacilities(
                faction,
                BuildingType.Mine
            );
            List<Building> refineries = ResourceProductionQueries.GetActiveFacilities(
                faction,
                BuildingType.Refinery
            );

            int maintenanceDemand = faction.GetTotalProjectedMaintenanceCost();
            RebalanceResourceAllocations(game, mines, maintenanceDemand, faction);
            RebalanceResourceAllocations(game, refineries, maintenanceDemand, faction);

            foreach (Building mine in mines)
                ProcessMine(game, faction, mine);

            ServicePendingRawMaterialRequests(game, faction);

            foreach (Building refinery in refineries)
                ProcessRefinery(game, faction, refinery);

            ServicePendingRefinedMaterialRequests(game, faction);
        }

        /// <summary>
        /// Delivers available raw material to queued refineries in request order.
        /// </summary>
        /// <param name="game">The game containing the requested facilities.</param>
        /// <param name="faction">The faction whose requests are serviced.</param>
        private void ServicePendingRawMaterialRequests(GameRoot game, Faction faction)
        {
            faction.RawMaterialStockpile = ServicePendingMaterialRequests(
                faction.PendingRawMaterialFacilityIDs,
                faction.RawMaterialStockpile,
                facilityId => GetPendingFacility(game, faction, facilityId, BuildingType.Refinery)
            );
        }

        /// <summary>
        /// Delivers available refined material to queued production facilities in request order.
        /// </summary>
        /// <param name="game">The game containing the requested facilities.</param>
        /// <param name="faction">The faction whose requests are serviced.</param>
        private void ServicePendingRefinedMaterialRequests(GameRoot game, Faction faction)
        {
            faction.RefinedMaterialStockpile = ServicePendingMaterialRequests(
                faction.PendingRefinedMaterialFacilityIDs,
                faction.RefinedMaterialStockpile,
                facilityId => GetPendingProductionFacility(game, faction, facilityId)
            );
        }

        /// <summary>
        /// Delivers one material stockpile to its pending facilities in request order.
        /// </summary>
        /// <param name="pendingFacilityIDs">The ordered pending facility identifiers.</param>
        /// <param name="stockpile">The available material count.</param>
        /// <param name="resolveFacility">Resolves a pending identifier to an eligible facility.</param>
        /// <param name="getMinimumStockpile">Returns the stockpile floor for a resolved facility.</param>
        /// <returns>The material count remaining after pending requests are serviced.</returns>
        private static int ServicePendingMaterialRequests(
            List<string> pendingFacilityIDs,
            int stockpile,
            Func<string, Building> resolveFacility,
            Func<Building, int> getMinimumStockpile = null
        )
        {
            int requestCount = pendingFacilityIDs.Count;
            while (requestCount-- > 0 && pendingFacilityIDs.Count > 0 && stockpile > 0)
            {
                string facilityId = pendingFacilityIDs[0];
                Building facility = resolveFacility(facilityId);
                pendingFacilityIDs.RemoveAt(0);
                if (facility == null)
                    continue;

                int minimumStockpile = Math.Max(0, getMinimumStockpile?.Invoke(facility) ?? 0);
                if (stockpile <= minimumStockpile)
                {
                    pendingFacilityIDs.Add(facilityId);
                    continue;
                }

                stockpile--;
                facility.ProductionInputReserved = true;
            }

            return stockpile;
        }

        /// <summary>
        /// Resolves a valid pending resource facility owned by a faction.
        /// </summary>
        /// <param name="game">The game containing the requested facility.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="facilityId">The facility instance ID.</param>
        /// <param name="buildingType">The required resource facility type.</param>
        /// <returns>The live facility, or null when the request is stale.</returns>
        private Building GetPendingFacility(
            GameRoot game,
            Faction faction,
            string facilityId,
            BuildingType buildingType
        )
        {
            Building facility = game.GetSceneNodeByInstanceID<Building>(facilityId);
            return
                IsPendingFacilityValid(faction, facility) && facility.BuildingType == buildingType
                ? facility
                : null;
        }

        /// <summary>
        /// Resolves a valid pending manufacturing facility owned by a faction.
        /// </summary>
        /// <param name="game">The game containing the requested facility.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="facilityId">The facility instance ID.</param>
        /// <returns>The live facility, or null when the request is stale.</returns>
        private Building GetPendingProductionFacility(
            GameRoot game,
            Faction faction,
            string facilityId
        )
        {
            Building facility = game.GetSceneNodeByInstanceID<Building>(facilityId);
            return
                IsPendingFacilityValid(faction, facility)
                && facility.ProductionType != ManufacturingType.None
                && facility.ProcessRate > 0
                && HasQueuedProduction(facility)
                ? facility
                : null;
        }

        /// <summary>
        /// Returns whether a production facility still has queued work of its assigned type.
        /// </summary>
        /// <param name="facility">The production facility to inspect.</param>
        /// <returns>True when its planet has a non-empty matching manufacturing queue.</returns>
        private static bool HasQueuedProduction(Building facility)
        {
            Planet planet = facility.GetParent() as Planet;
            return planet != null
                && planet
                    .GetManufacturingQueue()
                    .TryGetValue(
                        facility.ProductionType,
                        out List<IManufacturable> manufacturingQueue
                    )
                && manufacturingQueue?.Count > 0;
        }

        /// <summary>
        /// Returns whether a material request still targets an eligible live facility.
        /// </summary>
        /// <param name="faction">The owning faction.</param>
        /// <param name="facility">The facility to validate.</param>
        /// <returns>True when the pending request remains valid.</returns>
        private static bool IsPendingFacilityValid(Faction faction, Building facility)
        {
            return facility != null
                && facility.OwnerInstanceID == faction.InstanceID
                && facility.ManufacturingStatus == ManufacturingStatus.Complete
                && facility.Movement == null
                && !facility.ProductionInputReserved
                && !facility.ProductionPointReady
                && facility.GetParent() is Planet;
        }

        /// <summary>
        /// Rebalances one resource lane toward the faction's maintenance demand.
        /// </summary>
        /// <param name="game">The game defining the faction's effective capacity.</param>
        /// <param name="facilities">The mines or refineries to rebalance.</param>
        /// <param name="maintenanceDemand">The faction's reserved maintenance demand.</param>
        /// <param name="faction">The owning faction.</param>
        private static void RebalanceResourceAllocations(
            GameRoot game,
            List<Building> facilities,
            int maintenanceDemand,
            Faction faction
        )
        {
            if (facilities.Count == 0)
                return;

            List<int> allocations = ResourceProductionQueries.CalculateMaintenanceAllocations(
                game,
                facilities,
                maintenanceDemand,
                faction
            );
            for (int index = 0; index < facilities.Count; index++)
            {
                facilities[index].ResourceMaintenanceAllocation = allocations[index];
            }
        }

        /// <summary>
        /// Advances one mine and produces one raw material when its cycle completes.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="mine">The mine to process.</param>
        private void ProcessMine(GameRoot game, Faction faction, Building mine)
        {
            if (!mine.ProductionInputReserved)
                mine.ProductionInputReserved = true;

            AdvanceResourceProgress(game, faction, mine);
            int completedCycles = ConsumeCompletedResourceCycles(game, faction, mine, int.MaxValue);
            DeliverRawMaterials(faction, mine, completedCycles);
            mine.ProductionInputReserved = true;
        }

        /// <summary>
        /// Advances one refinery and produces one refined material when its cycle completes.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="refinery">The refinery to process.</param>
        private void ProcessRefinery(GameRoot game, Faction faction, Building refinery)
        {
            if (!refinery.ProductionInputReserved)
            {
                if (!faction.RequestRawMaterial(refinery))
                    return;
            }

            AdvanceResourceProgress(game, faction, refinery);
            int availableInputs =
                faction.RawMaterialStockpile == int.MaxValue
                    ? int.MaxValue
                    : faction.RawMaterialStockpile + 1;
            int completedCycles = ConsumeCompletedResourceCycles(
                game,
                faction,
                refinery,
                availableInputs
            );
            if (completedCycles <= 0)
                return;

            faction.RawMaterialStockpile -= completedCycles - 1;
            DeliverRefinedMaterials(faction, refinery, completedCycles);
            faction.RequestRawMaterial(refinery);
        }

        /// <summary>
        /// Delivers completed mine output without iterating when no smuggling roll is active.
        /// </summary>
        /// <param name="controller">The faction controlling the mine.</param>
        /// <param name="mine">The producing mine.</param>
        /// <param name="quantity">The number of completed resources.</param>
        private void DeliverRawMaterials(Faction controller, Building mine, int quantity)
        {
            if (quantity <= 0)
                return;

            (Faction beneficiary, int divertedQuantity) = _smugglingCommands.ResolveProductionBatch(
                mine,
                quantity
            );
            controller.RawMaterialStockpile = IntegerMath.SaturatingAdd(
                controller.RawMaterialStockpile,
                quantity - divertedQuantity
            );
            if (beneficiary != null)
            {
                beneficiary.RawMaterialStockpile = IntegerMath.SaturatingAdd(
                    beneficiary.RawMaterialStockpile,
                    divertedQuantity
                );
            }
        }

        /// <summary>
        /// Delivers completed refinery output without iterating when no smuggling roll is active.
        /// </summary>
        /// <param name="controller">The faction controlling the refinery.</param>
        /// <param name="refinery">The producing refinery.</param>
        /// <param name="quantity">The number of completed resources.</param>
        private void DeliverRefinedMaterials(Faction controller, Building refinery, int quantity)
        {
            if (quantity <= 0)
                return;

            (Faction beneficiary, int divertedQuantity) = _smugglingCommands.ResolveProductionBatch(
                refinery,
                quantity
            );
            controller.RefinedMaterialStockpile = IntegerMath.SaturatingAdd(
                controller.RefinedMaterialStockpile,
                quantity - divertedQuantity
            );
            if (beneficiary != null)
            {
                beneficiary.RefinedMaterialStockpile = IntegerMath.SaturatingAdd(
                    beneficiary.RefinedMaterialStockpile,
                    divertedQuantity
                );
            }
        }

        /// <summary>
        /// Advances a resource facility by its configured output rate for one tick.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="facility">The facility to advance.</param>
        private static void AdvanceResourceProgress(
            GameRoot game,
            Faction faction,
            Building facility
        )
        {
            if (facility.ProductionCycleDuration <= 0)
                facility.ProductionCycleDuration = CalculateResourceCycleDuration(
                    game,
                    faction,
                    facility
                );

            DifficultyModifiers modifier = game.GetDifficultyModifier(faction);
            int outputPercent =
                facility.BuildingType == BuildingType.Mine
                    ? modifier.MineOutputPercent
                    : modifier.RefineryOutputPercent;
            if (outputPercent <= 0)
                return;

            facility.ProductionCycleProgress += (double)outputPercent / _percentScale;
        }

        /// <summary>
        /// Completes one ready resource cycle while preserving excess fractional progress.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="facility">The facility whose ready cycle is consumed.</param>
        /// <param name="maximumCycles">The greatest number of cycles backed by available input.</param>
        /// <returns>The number of completed cycles consumed from accumulated progress.</returns>
        private static int ConsumeCompletedResourceCycles(
            GameRoot game,
            Faction faction,
            Building facility,
            int maximumCycles
        )
        {
            if (!facility.ProductionInputReserved || maximumCycles <= 0)
                return 0;

            if (facility.ProductionCycleDuration <= 0)
                facility.ProductionCycleDuration = CalculateResourceCycleDuration(
                    game,
                    faction,
                    facility
                );

            if (facility.ProductionCycleProgress < facility.ProductionCycleDuration)
                return 0;

            facility.ProductionCycleProgress -= facility.ProductionCycleDuration;
            facility.ProductionInputReserved = false;
            facility.ResourceStartupCyclePending = false;
            int completedCycles = 1;

            int steadyDuration = CalculateResourceCycleDuration(game, faction, facility);
            long maximumAdditionalCycles = (long)maximumCycles - completedCycles;
            double availableAdditionalCycles = facility.ProductionCycleProgress / steadyDuration;
            long additionalCycles =
                availableAdditionalCycles >= maximumAdditionalCycles
                    ? maximumAdditionalCycles
                    : (long)availableAdditionalCycles;
            facility.ProductionCycleProgress -= additionalCycles * steadyDuration;
            facility.ProductionCycleDuration = steadyDuration;
            return IntegerMath.SaturateToInt(completedCycles + additionalCycles);
        }

        /// <summary>
        /// Calculates a resource facility cycle from process rate, maintenance load, and support.
        /// </summary>
        /// <param name="game">The game state being advanced.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="facility">The facility whose cycle is calculated.</param>
        /// <returns>The cycle duration in ticks.</returns>
        private static int CalculateResourceCycleDuration(
            GameRoot game,
            Faction faction,
            Building facility
        )
        {
            GameConfig.ProductionConfig config = game.Config.Production;
            int duration = ResourceProductionQueries.CalculateSteadyCycleDuration(
                game,
                faction,
                facility
            );
            if (!facility.ResourceStartupCyclePending)
                return duration;

            int startupBase = duration * config.ResourceStartupBasePercent / _percentScale;
            int startupRandomMaximum =
                duration * config.ResourceStartupRandomPercent / _percentScale;
            return Math.Max(1, startupBase + game.Random.NextInt(0, startupRandomMaximum + 1));
        }
    }
}
