using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Removes production facilities outside a planet's sector allocation.
    /// </summary>
    public sealed class AIFacilityRemovalProposal : AIProposal
    {
        private List<Building> _surplus = new List<Building>();

        internal override AIProposalPriority Priority => AIProposalPriority.Mandatory;

        public Planet Planet { get; }

        public BuildingType BuildingType { get; }

        /// <summary>
        /// Creates a facility-removal proposal.
        /// </summary>
        /// <param name="planet">The planet whose surplus facilities should be removed.</param>
        /// <param name="buildingType">The production-facility type to evaluate.</param>
        public AIFacilityRemovalProposal(Planet planet, BuildingType buildingType)
        {
            Planet = planet;
            BuildingType = buildingType;
        }

        /// <summary>
        /// Returns the claim that prevents simultaneous construction of this facility type.
        /// </summary>
        /// <returns>The facility-allocation claim.</returns>
        public override IReadOnlyList<string> GetClaimKeys()
        {
            return Planet == null
                ? new List<string>()
                : new List<string>
                {
                    AIClaimKeys.FacilityAllocation(Planet.InstanceID, BuildingType),
                };
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

        private static List<Building> GetSurplus(
            AITurnContext context,
            Planet planet,
            BuildingType buildingType
        )
        {
            if (context?.Assessment == null || planet == null)
                return new List<Building>();

            int cap = context.FacilityAllocation.GetCap(planet, buildingType);
            return context
                .Assessment.GetPlanetBuildings(planet)
                .Where(building =>
                    building.GetOwnerInstanceID() == context.Faction.InstanceID
                    && building.GetBuildingType() == buildingType
                )
                .OrderByDescending(building => building.GetProcessRate())
                .ThenByDescending(building =>
                    building.ManufacturingStatus == ManufacturingStatus.Complete
                )
                .ThenBy(building => building.InstanceID)
                .Skip(cap)
                .ToList();
        }

        private bool IsValid(AITurnContext context)
        {
            return context?.Game != null
                && context.Maintenance != null
                && context.Manufacturing != null
                && IsOwnedBy(context, Planet)
                && context.Game.GetSceneNodeByInstanceID<Planet>(Planet.InstanceID) == Planet
                && BuildingType is BuildingType.Shipyard or BuildingType.ConstructionFacility;
        }
    }
}
