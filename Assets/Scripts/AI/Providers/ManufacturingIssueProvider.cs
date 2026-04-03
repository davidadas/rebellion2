using System.Collections.Generic;
using Rebellion.Game;

namespace Rebellion.AI.Providers
{
    /// <summary>
    /// Subtype 5: Manufacturing and construction.
    /// Uniquely tracks construction facility needs alongside military units.
    /// </summary>
    public class ManufacturingIssueProvider : IssueProvider
    {
        public override List<AutomationIssue> BuildIssues(
            GameRoot game,
            Faction faction,
            Planet planet
        )
        {
            List<AutomationIssue> issues = new List<AutomationIssue>();
            float score = ScoreTarget(faction, planet);
            if (score <= 0f)
                return issues;

            string factionId = faction.InstanceID;
            string planetId = planet.GetInstanceID();

            // Want at least 1 construction facility per planet that has building slots
            int constructionFacilities = planet.GetBuildingTypeCount(
                BuildingType.ConstructionFacility
            );
            if (planet.GetAvailableSlots(BuildingSlot.Ground) > 0)
            {
                AddIssueIfDeficit(
                    issues,
                    planetId,
                    IssueUnitType.ConstructionFacility,
                    1,
                    constructionFacilities,
                    score
                );
            }

            // Military presence proportional to facility value
            int desiredShips = constructionFacilities > 0 ? 1 : 0;
            AddIssueIfDeficit(
                issues,
                planetId,
                IssueUnitType.CapitalShip,
                desiredShips,
                CountCapitalShips(planet, factionId),
                score
            );

            int garrison = CalculateGarrisonRequirement(planet, faction, game.Config.AI.Garrison);

            AddIssueIfDeficit(
                issues,
                planetId,
                IssueUnitType.Troop,
                garrison,
                CountTroops(planet, factionId),
                score
            );

            return issues;
        }
    }
}
