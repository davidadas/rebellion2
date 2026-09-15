using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds proposals for production facilities outside the faction's sector allocation.
    /// </summary>
    public sealed class AIFacilityRemovalPlanner : IAIProposalPlanner
    {
        /// <summary>
        /// Returns retirement proposals for facility allocations that exceed their caps.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Facility-retirement proposals.</returns>
        public List<AIProposal> Plan(AITurnContext context)
        {
            List<AIProposal> proposals = new List<AIProposal>();
            if (
                context?.Assessment == null
                || context.Maintenance == null
                || context.Manufacturing == null
            )
                return proposals;

            foreach (Planet planet in context.Assessment.OwnedPlanets)
            {
                foreach (
                    IGrouping<BuildingType, Building> facilities in context
                        .Assessment.GetPlanetBuildings(planet)
                        .Where(building =>
                            building.GetOwnerInstanceID() == context.Faction.InstanceID
                            && building.GetBuildingType()
                                is BuildingType.Shipyard
                                    or BuildingType.ConstructionFacility
                        )
                        .GroupBy(building => building.GetBuildingType())
                )
                {
                    if (
                        facilities.Count()
                        > context.FacilityAllocation.GetCap(planet, facilities.Key)
                    )
                        proposals.Add(new AIFacilityRemovalProposal(planet, facilities.Key));
                }
            }

            return proposals;
        }
    }
}
