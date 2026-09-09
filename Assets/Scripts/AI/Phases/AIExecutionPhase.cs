using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Executes proposals selected for the turn.
    /// </summary>
    public sealed class AIExecutionPhase : IAIIncrementalTurnPhase
    {
        /// <summary>
        /// Executes selected proposals that still pass validation.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public void Execute(AITurnContext context)
        {
            foreach (object _ in ExecuteIncrementally(context)) { }
        }

        /// <summary>
        /// Executes selected proposals one at a time.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>A sequence containing one marker per executed proposal.</returns>
        public IEnumerable<object> ExecuteIncrementally(AITurnContext context)
        {
            if (context?.SelectedProposals == null)
                yield break;

            foreach (AIProposal proposal in context.SelectedProposals)
            {
                if (proposal?.CanExecute(context) == true)
                    proposal.Execute(context);
                yield return null;
            }
        }
    }

    /// <summary>
    /// Removes production facilities outside the faction's sector allocation.
    /// </summary>
    public sealed class AIFacilityCleanupPhase : IAITurnPhase
    {
        public void Execute(AITurnContext context)
        {
            if (context?.Assessment == null || context.Maintenance == null)
                return;

            AIFacilityAllocationPolicy policy = context.FacilityAllocation;
            foreach (Planet planet in context.Assessment.OwnedPlanets)
            {
                foreach (
                    IGrouping<BuildingType, Building> group in context
                        .Assessment.GetPlanetBuildings(planet)
                        .Where(building =>
                            building.GetOwnerInstanceID() == context.Faction.InstanceID
                            && building.GetBuildingType()
                                is BuildingType.Shipyard
                                    or BuildingType.ConstructionFacility
                                    or BuildingType.TrainingFacility
                        )
                        .GroupBy(building => building.GetBuildingType())
                )
                {
                    int cap = policy.GetCap(planet, group.Key);
                    HashSet<string> retainedIds = group
                        .OrderByDescending(building => building.GetProcessRate())
                        .ThenBy(building => building.ManufacturingStatus)
                        .ThenBy(building => building.InstanceID)
                        .Take(cap)
                        .Select(building => building.InstanceID)
                        .ToHashSet();
                    List<Building> surplus = group
                        .Where(building => !retainedIds.Contains(building.InstanceID))
                        .ToList();
                    List<IManufacturable> completed = surplus
                        .Where(building =>
                            building.ManufacturingStatus == ManufacturingStatus.Complete
                        )
                        .Cast<IManufacturable>()
                        .ToList();
                    if (completed.Count > 0)
                        context.Maintenance.TryScrap(completed, context.Faction.InstanceID);

                    context.Manufacturing.CancelManufacturing(
                        surplus
                            .Where(building =>
                                building.ManufacturingStatus != ManufacturingStatus.Complete
                            )
                            .Cast<IManufacturable>()
                            .ToList(),
                        context.Faction.InstanceID
                    );
                }
            }
        }
    }
}
