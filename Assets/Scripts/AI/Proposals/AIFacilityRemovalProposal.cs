using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Removes an explicitly planned quantity of faction-wide surplus production facilities.
    /// </summary>
    public sealed class AIFacilityRemovalProposal : AIProposal
    {
        private List<Building> _surplus = new List<Building>();

        internal override AIProposalPriority Priority => AIProposalPriority.Mandatory;

        public Planet Planet { get; }

        public BuildingType BuildingType { get; }

        public int MaximumRemovalCount { get; }

        public int MinimumFactionFacilityCount { get; }

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
            return $"facility-removal:{Planet?.InstanceID}:{BuildingType}";
        }

        /// <summary>
        /// Returns whether the proposal still targets an owned planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the proposal can be selected.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return IsValid(context);
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

            _surplus = GetSurplus(context, Planet, BuildingType);
            return _surplus.Count > 0;
        }

        /// <summary>
        /// Scraps completed surplus facilities and cancels unfinished ones.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
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
                && BuildingType == BuildingType.Shipyard;
        }
    }
}
