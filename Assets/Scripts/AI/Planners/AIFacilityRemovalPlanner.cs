using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds proposals for genuinely surplus shipyards during maintenance distress.
    /// </summary>
    public sealed class AIFacilityRemovalPlanner : IAIProposalPlanner
    {
        private readonly AIInfrastructureDemandPlanner _demandPlanner = new();

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

            if (context.Assessment.ProjectedMaintenanceHeadroom >= 0)
                return proposals;

            int minimumShipyardCount = _demandPlanner.GetDesiredFacilityCount(
                context,
                BuildingType.Shipyard
            );
            List<(Planet Planet, int Count, int Rate)> sites = context
                .Assessment.OwnedPlanets.Select(planet =>
                {
                    List<Building> facilities = AIFacilityRemovalProposal
                        .GetFacilities(context, planet)
                        .Where(building =>
                            building.GetOwnerInstanceID() == context.Faction.InstanceID
                            && building.GetBuildingType() == BuildingType.Shipyard
                        )
                        .ToList();
                    return (
                        Planet: planet,
                        Count: facilities.Count,
                        Rate: facilities.Sum(building => building.GetProcessRate())
                    );
                })
                .Where(site => site.Count > 0)
                .OrderBy(site => site.Rate)
                .ThenBy(site => context.Assessment.GetPlanetValue(site.Planet))
                .ThenBy(site => site.Planet.InstanceID, System.StringComparer.Ordinal)
                .ToList();
            int surplusCount = Math.Max(0, sites.Sum(site => site.Count) - minimumShipyardCount);
            foreach ((Planet planet, int count, int _) in sites)
            {
                if (surplusCount <= 0)
                    break;

                int removalCount = Math.Min(count, surplusCount);
                proposals.Add(
                    new AIFacilityRemovalProposal(
                        planet,
                        BuildingType.Shipyard,
                        removalCount,
                        minimumShipyardCount
                    )
                );
                surplusCount -= removalCount;
            }

            return proposals;
        }
    }
}
