using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Systems
{
    /// <summary>
    /// Processes mine and refinery cycles and the material requests that feed them.
    /// </summary>
    public class ResourceProductionSystem : IGameSystem
    {
        private const int _percentScale = 100;

        private readonly GameRoot _game;

        /// <summary>
        /// Creates a new ResourceProductionSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public ResourceProductionSystem(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>
        /// Services pending material requests and advances every active resource facility.
        /// </summary>
        /// <returns>An empty result list.</returns>
        public List<GameResult> ProcessTick()
        {
            foreach (Faction faction in _game.GetFactions())
                ProcessFaction(faction);

            return new List<GameResult>();
        }

        /// <summary>
        /// Processes material delivery, maintenance allocation, and resource cycles for one faction.
        /// </summary>
        /// <param name="faction">The faction to process.</param>
        private void ProcessFaction(Faction faction)
        {
            ServicePendingRawMaterialRequests(faction);
            ServicePendingRefinedMaterialRequests(faction);

            List<Building> mines = GetActiveResourceFacilities(faction, BuildingType.Mine);
            List<Building> refineries = GetActiveResourceFacilities(faction, BuildingType.Refinery);

            int maintenanceDemand = faction.GetTotalProjectedMaintenanceCost();
            RebalanceResourceAllocations(mines, maintenanceDemand, faction);
            RebalanceResourceAllocations(refineries, maintenanceDemand, faction);

            foreach (Building mine in mines)
                ProcessMine(faction, mine);

            ServicePendingRawMaterialRequests(faction);

            foreach (Building refinery in refineries)
                ProcessRefinery(faction, refinery);

            ServicePendingRefinedMaterialRequests(faction);
        }

        /// <summary>
        /// Delivers available raw material to queued refineries in request order.
        /// </summary>
        /// <param name="faction">The faction whose requests are serviced.</param>
        private void ServicePendingRawMaterialRequests(Faction faction)
        {
            while (faction.PendingRawMaterialFacilityIDs.Count > 0)
            {
                if (faction.RawMaterialStockpile <= 0)
                    return;

                string facilityId = faction.PendingRawMaterialFacilityIDs[0];
                Building facility = GetPendingFacility(faction, facilityId, BuildingType.Refinery);
                if (facility == null)
                {
                    faction.PendingRawMaterialFacilityIDs.RemoveAt(0);
                    continue;
                }

                faction.PendingRawMaterialFacilityIDs.RemoveAt(0);
                faction.RawMaterialStockpile--;
                facility.ProductionInputReserved = true;
            }
        }

        /// <summary>
        /// Delivers available refined material to queued production facilities in request order.
        /// </summary>
        /// <param name="faction">The faction whose requests are serviced.</param>
        private void ServicePendingRefinedMaterialRequests(Faction faction)
        {
            while (faction.PendingRefinedMaterialFacilityIDs.Count > 0)
            {
                if (faction.RefinedMaterialStockpile <= 0)
                    return;

                string facilityId = faction.PendingRefinedMaterialFacilityIDs[0];
                Building facility = GetPendingProductionFacility(faction, facilityId);
                if (facility == null)
                {
                    faction.PendingRefinedMaterialFacilityIDs.RemoveAt(0);
                    continue;
                }

                faction.PendingRefinedMaterialFacilityIDs.RemoveAt(0);
                faction.RefinedMaterialStockpile--;
                facility.ProductionInputReserved = true;
            }
        }

        /// <summary>
        /// Resolves a valid pending resource facility owned by a faction.
        /// </summary>
        /// <param name="faction">The owning faction.</param>
        /// <param name="facilityId">The facility instance ID.</param>
        /// <param name="buildingType">The required resource facility type.</param>
        /// <returns>The live facility, or null when the request is stale.</returns>
        private Building GetPendingFacility(
            Faction faction,
            string facilityId,
            BuildingType buildingType
        )
        {
            Building facility = _game.GetSceneNodeByInstanceID<Building>(facilityId);
            return
                IsPendingFacilityValid(faction, facility) && facility.BuildingType == buildingType
                ? facility
                : null;
        }

        /// <summary>
        /// Resolves a valid pending manufacturing facility owned by a faction.
        /// </summary>
        /// <param name="faction">The owning faction.</param>
        /// <param name="facilityId">The facility instance ID.</param>
        /// <returns>The live facility, or null when the request is stale.</returns>
        private Building GetPendingProductionFacility(Faction faction, string facilityId)
        {
            Building facility = _game.GetSceneNodeByInstanceID<Building>(facilityId);
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
        /// Gets active mine or refinery facilities in stable planet and building order.
        /// </summary>
        /// <param name="faction">The owning faction.</param>
        /// <param name="buildingType">The resource facility type.</param>
        /// <returns>The active facilities.</returns>
        private static List<Building> GetActiveResourceFacilities(
            Faction faction,
            BuildingType buildingType
        )
        {
            List<Building> facilities = new List<Building>();
            foreach (Planet planet in faction.GetOwnedColonizedPlanets())
            {
                if (planet.IsProductionSuspended())
                    continue;

                IEnumerable<Building> planetFacilities = planet.Buildings.Where(building =>
                    building.BuildingType == buildingType
                    && building.ManufacturingStatus == ManufacturingStatus.Complete
                    && building.Movement == null
                    && building.ProcessRate > 0
                );
                if (buildingType == BuildingType.Mine)
                    planetFacilities = planetFacilities.Take(planet.NumRawResourceNodes);

                facilities.AddRange(planetFacilities);
            }

            return facilities;
        }

        /// <summary>
        /// Rebalances one resource lane toward the faction's maintenance demand.
        /// </summary>
        /// <param name="facilities">The mines or refineries to rebalance.</param>
        /// <param name="maintenanceDemand">The faction's reserved maintenance demand.</param>
        /// <param name="faction">The owning faction.</param>
        private static void RebalanceResourceAllocations(
            List<Building> facilities,
            int maintenanceDemand,
            Faction faction
        )
        {
            if (facilities.Count == 0)
                return;

            int facilityCapacity = faction.Settings.ResourceProcessingPointsPerFacility;
            int totalCapacity = facilities.Count * facilityCapacity;
            foreach (Building facility in facilities)
            {
                facility.ResourceMaintenanceAllocation = Math.Clamp(
                    facility.ResourceMaintenanceAllocation,
                    0,
                    facilityCapacity
                );
            }

            int targetAllocation = Math.Min(Math.Max(0, maintenanceDemand), totalCapacity);
            int currentAllocation = facilities.Sum(facility =>
                facility.ResourceMaintenanceAllocation
            );
            if (currentAllocation < targetAllocation)
            {
                IncreaseResourceAllocations(
                    facilities,
                    targetAllocation - currentAllocation,
                    facilityCapacity,
                    totalCapacity
                );
            }
            else if (currentAllocation > targetAllocation)
            {
                DecreaseResourceAllocations(
                    facilities,
                    currentAllocation - targetAllocation,
                    facilityCapacity,
                    totalCapacity
                );
            }
        }

        /// <summary>
        /// Adds resource maintenance allocation in stable facility order.
        /// </summary>
        /// <param name="facilities">The facilities receiving allocation.</param>
        /// <param name="remaining">The allocation still to add.</param>
        /// <param name="facilityCapacity">The capacity of each facility.</param>
        /// <param name="totalCapacity">The capacity of the resource lane.</param>
        private static void IncreaseResourceAllocations(
            List<Building> facilities,
            int remaining,
            int facilityCapacity,
            int totalCapacity
        )
        {
            bool changed;
            do
            {
                changed = false;
                foreach (Building facility in facilities)
                {
                    int idealAllocation = remaining * facilityCapacity / totalCapacity;
                    int added = Math.Clamp(
                        idealAllocation - facility.ResourceMaintenanceAllocation + 1,
                        0,
                        Math.Min(
                            remaining,
                            facilityCapacity - facility.ResourceMaintenanceAllocation
                        )
                    );
                    if (added <= 0)
                        continue;

                    facility.ResourceMaintenanceAllocation += added;
                    remaining -= added;
                    changed = true;
                    if (remaining == 0)
                        return;
                }
            } while (changed);
        }

        /// <summary>
        /// Removes resource maintenance allocation in stable facility order.
        /// </summary>
        /// <param name="facilities">The facilities losing allocation.</param>
        /// <param name="remaining">The allocation still to remove.</param>
        /// <param name="facilityCapacity">The capacity of each facility.</param>
        /// <param name="totalCapacity">The capacity of the resource lane.</param>
        private static void DecreaseResourceAllocations(
            List<Building> facilities,
            int remaining,
            int facilityCapacity,
            int totalCapacity
        )
        {
            bool changed;
            do
            {
                changed = false;
                foreach (Building facility in facilities)
                {
                    int idealAllocation = (remaining - 1) * facilityCapacity / totalCapacity;
                    int removed = Math.Clamp(
                        facility.ResourceMaintenanceAllocation - idealAllocation,
                        0,
                        Math.Min(remaining, facility.ResourceMaintenanceAllocation)
                    );
                    if (removed <= 0)
                        continue;

                    facility.ResourceMaintenanceAllocation -= removed;
                    remaining -= removed;
                    changed = true;
                    if (remaining == 0)
                        return;
                }
            } while (changed);
        }

        /// <summary>
        /// Advances one mine and deposits one raw material when its cycle completes.
        /// </summary>
        /// <param name="faction">The owning faction.</param>
        /// <param name="mine">The mine to process.</param>
        private void ProcessMine(Faction faction, Building mine)
        {
            if (!mine.ProductionInputReserved)
                mine.ProductionInputReserved = true;

            if (!AdvanceResourceCycle(faction, mine))
                return;

            faction.RawMaterialStockpile++;
            mine.ProductionInputReserved = true;
        }

        /// <summary>
        /// Advances one refinery and deposits one refined material when its cycle completes.
        /// </summary>
        /// <param name="faction">The owning faction.</param>
        /// <param name="refinery">The refinery to process.</param>
        private void ProcessRefinery(Faction faction, Building refinery)
        {
            if (!refinery.ProductionInputReserved)
            {
                if (!faction.RequestRawMaterial(refinery))
                    return;
            }

            if (!AdvanceResourceCycle(faction, refinery))
                return;

            faction.RefinedMaterialStockpile++;
            faction.RequestRawMaterial(refinery);
        }

        /// <summary>
        /// Advances a resource facility by one tick and resets it after a completed cycle.
        /// </summary>
        /// <param name="faction">The owning faction.</param>
        /// <param name="facility">The facility to advance.</param>
        /// <returns>True when the cycle completes this tick.</returns>
        private bool AdvanceResourceCycle(Faction faction, Building facility)
        {
            if (facility.ProductionCycleDuration <= 0)
                facility.ProductionCycleDuration = CalculateResourceCycleDuration(
                    faction,
                    facility
                );

            facility.ProductionCycleProgress++;
            if (facility.ProductionCycleProgress < facility.ProductionCycleDuration)
                return false;

            facility.ProductionCycleProgress = 0;
            facility.ProductionCycleDuration = 0;
            facility.ProductionInputReserved = false;
            facility.ResourceStartupCyclePending = false;
            return true;
        }

        /// <summary>
        /// Calculates a resource facility cycle from process rate, maintenance load, and support.
        /// </summary>
        /// <param name="faction">The owning faction.</param>
        /// <param name="facility">The facility whose cycle is calculated.</param>
        /// <returns>The cycle duration in ticks.</returns>
        private int CalculateResourceCycleDuration(Faction faction, Building facility)
        {
            GameConfig.ProductionConfig config = _game.Config.Production;
            int facilityCapacity = faction.Settings.ResourceProcessingPointsPerFacility;
            int scaledCapacity = Math.Max(
                1,
                facilityCapacity * config.ResourceMaintenanceLoadPercent / _percentScale
            );
            int maintenancePenalty = DivideRoundingUp(
                facility.ResourceMaintenanceAllocation,
                scaledCapacity
            );
            int baseDuration = Math.Max(1, facility.ProcessRate + maintenancePenalty);
            Planet planet = facility.GetParentOfType<Planet>();
            int support = Math.Max(1, planet?.GetPopularSupport(faction.InstanceID) ?? 0);
            int supportModifier = config.ResourceCollectionBasePercent * _percentScale / support;
            int duration = Math.Max(1, baseDuration * supportModifier / _percentScale);
            if (!facility.ResourceStartupCyclePending)
                return duration;

            int startupBase = duration * config.ResourceStartupBasePercent / _percentScale;
            int startupRandomMaximum =
                duration * config.ResourceStartupRandomPercent / _percentScale;
            return Math.Max(1, startupBase + _game.Random.NextInt(0, startupRandomMaximum + 1));
        }

        /// <summary>
        /// Divides non-negative integers while rounding any remainder upward.
        /// </summary>
        /// <param name="dividend">The value to divide.</param>
        /// <param name="divisor">The positive divisor.</param>
        /// <returns>The rounded-up quotient.</returns>
        private static int DivideRoundingUp(int dividend, int divisor)
        {
            return (dividend + divisor - 1) / divisor;
        }
    }
}
