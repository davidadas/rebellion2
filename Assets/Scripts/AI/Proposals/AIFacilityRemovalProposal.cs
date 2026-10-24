using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Removes surplus facilities or reclaims resource buildings for an allocated industrial hub.
    /// </summary>
    public sealed class AIFacilityRemovalProposal : AIProposal
    {
        private List<Building> _surplus = new List<Building>();

        internal override AIProposalPriority Priority => AIProposalPriority.Mandatory;

        public Planet Planet { get; }

        public BuildingType BuildingType { get; }

        public int MaximumRemovalCount { get; }

        public int MinimumFactionFacilityCount { get; }
        internal BuildingType ResourceHubType { get; }
        private int ResourceHubTarget { get; }

        /// <summary>
        /// Creates an exact resource-reclamation request for a full industrial hub.
        /// </summary>
        /// <param name="planet">The allocated industrial destination.</param>
        /// <param name="hubType">The production category requiring space.</param>
        /// <param name="targetCount">The finished hub size.</param>
        internal AIFacilityRemovalProposal(Planet planet, BuildingType hubType, int targetCount)
        {
            Planet = planet;
            ResourceHubType = hubType;
            ResourceHubTarget = targetCount;
        }

        /// <summary>
        /// Creates a facility-removal proposal.
        /// </summary>
        /// <param name="planet">The planet whose surplus facilities should be removed.</param>
        /// <param name="buildingType">The production-facility type to evaluate.</param>
        /// <param name="maximumRemovalCount">Maximum facilities this proposal may remove.</param>
        /// <param name="minimumFactionFacilityCount">Minimum faction-wide facilities to preserve.</param>
        public AIFacilityRemovalProposal(
            Planet planet,
            BuildingType buildingType,
            int maximumRemovalCount,
            int minimumFactionFacilityCount
        )
        {
            Planet = planet;
            BuildingType = buildingType;
            MaximumRemovalCount = Math.Max(0, maximumRemovalCount);
            MinimumFactionFacilityCount = Math.Max(0, minimumFactionFacilityCount);
        }

        /// <summary>
        /// Returns a stable key for the planet and facility type.
        /// </summary>
        /// <returns>The facility-removal sort key.</returns>
        public override string GetSortKey()
        {
            return ResourceHubType == BuildingType.None
                ? $"facility-removal:{Planet?.InstanceID}:{BuildingType}"
                : $"facility-removal:{Planet?.InstanceID}:resources:{ResourceHubType}";
        }

        /// <summary>
        /// Returns whether the proposal still targets an owned planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the proposal can be selected.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return IsValid(context)
                && (
                    ResourceHubType == BuildingType.None || GetResourceFacilities(context).Count > 0
                );
        }

        /// <summary>
        /// Returns whether surplus facilities still exist.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the proposal has facilities to retire.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            if (!IsValid(context))
            {
                _surplus.Clear();
                return false;
            }

            _surplus =
                ResourceHubType == BuildingType.None
                    ? GetSurplus(context, Planet, BuildingType)
                    : GetResourceFacilities(context, refresh: true).ToList();
            return _surplus.Count > 0;
        }

        /// <summary>
        /// Scraps completed surplus facilities and cancels unfinished ones.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (ResourceHubType != BuildingType.None)
            {
                if (!IsValid(context))
                    return;
                while (true)
                {
                    var resources = GetResourceFacilities(context, refresh: true);
                    int before = ResourceProductionQueries.CalculateMaintenanceHeadroom(
                        context.Game,
                        context.Faction
                    );
                    if (
                        resources.Count == 0
                        || !context.Maintenance.TryScrap(
                            new IManufacturable[] { resources[0] },
                            context.Faction.InstanceID
                        )
                    )
                        break;
                    int after = ResourceProductionQueries.CalculateMaintenanceHeadroom(
                        context.Game,
                        context.Faction
                    );
                    context.RecordManufacturingMutation(Math.Max(0, before - after), 0);
                }
                return;
            }
            if (_surplus.Count == 0 && !CanExecute(context))
                return;

            List<IManufacturable> completed = _surplus
                .Where(building => building.ManufacturingStatus == ManufacturingStatus.Complete)
                .Cast<IManufacturable>()
                .ToList();
            if (completed.Count > 0)
                context.Maintenance.TryScrap(completed, context.Faction.InstanceID);

            context.Manufacturing.CancelManufacturing(
                _surplus
                    .Where(building => building.ManufacturingStatus != ManufacturingStatus.Complete)
                    .Cast<IManufacturable>()
                    .ToList(),
                context.Faction.InstanceID
            );
            _surplus.Clear();
        }

        /// <summary>
        /// Gets surplus.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="buildingType">The building type.</param>
        /// <returns>The requested surplus.</returns>
        private List<Building> GetSurplus(
            AITurnContext context,
            Planet planet,
            BuildingType buildingType
        )
        {
            if (context?.Assessment == null || planet == null)
                return new List<Building>();

            return GetFacilities(context, planet)
                .Where(building =>
                    building.GetOwnerInstanceID() == context.Faction.InstanceID
                    && building.GetBuildingType() == buildingType
                )
                .OrderBy(building => building.GetProcessRate())
                .ThenByDescending(building =>
                    building.ManufacturingStatus == ManufacturingStatus.Complete
                )
                .ThenBy(building => building.InstanceID)
                .Take(MaximumRemovalCount)
                .ToList();
        }

        /// <summary>
        /// Returns the turn snapshot plus facilities queued after the assessment was built.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet whose facilities are requested.</param>
        /// <returns>The distinct completed and queued facilities.</returns>
        internal static IReadOnlyList<Building> GetFacilities(AITurnContext context, Planet planet)
        {
            if (context?.Assessment == null || planet == null)
                return new List<Building>();

            IEnumerable<Building> queued = planet
                .GetManufacturingQueue()
                .Values.SelectMany(items => items)
                .OfType<Building>()
                .Where(building => building.GetParentOfType<Planet>() == planet);
            return context
                .Assessment.GetPlanetBuildings(planet)
                .Concat(queued)
                .GroupBy(building => building.InstanceID, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
        }

        /// <summary>
        /// Checks whether the valid condition is met.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>True when the valid condition is met; otherwise false.</returns>
        private bool IsValid(AITurnContext context)
        {
            return context?.Game != null
                && context.Maintenance != null
                && context.Manufacturing != null
                && IsOwnedBy(context, Planet)
                && context.Game.GetSceneNodeByInstanceID<Planet>(Planet.InstanceID) == Planet
                && (
                    ResourceHubType == BuildingType.None
                        ? BuildingType == BuildingType.Shipyard
                        : context.StrategicPlan.GetAllocatedInfrastructureTarget(
                            Planet,
                            ResourceHubType
                        ) == ResourceHubTarget
                );
        }

        /// <summary>
        /// Revalidates the economy and exact space needed by the allocated hub.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="refresh">Whether cached planning results must be bypassed.</param>
        /// <returns>The resource facilities that can be retired together.</returns>
        internal IReadOnlyList<Building> GetResourceFacilities(
            AITurnContext context,
            bool refresh = false
        )
        {
            return context.StrategicPlan.GetResourceFacilitiesToReclaim(
                Planet,
                ResourceHubType,
                ResourceHubTarget,
                refresh
            );
        }
    }
}
