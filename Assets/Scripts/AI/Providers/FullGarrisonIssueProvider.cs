using System.Collections.Generic;
using Rebellion.Game;

namespace Rebellion.AI.Providers
{
    /// <summary>
    /// Subtype 2: Full military garrison.
    /// Covers all branches: garrison base, capital ships, escort ships, and troops.
    /// </summary>
    public class FullGarrisonIssueProvider : IssueProvider
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
            int garrison = CalculateGarrisonRequirement(planet, faction, game.Config.AI.Garrison);

            // Fleet presence: at least 1 fleet
            AddIssueIfDeficit(
                issues,
                planetId,
                IssueUnitType.GarrisonBase,
                1,
                CountFleets(planet, factionId),
                score
            );

            // Capital ships
            AddIssueIfDeficit(
                issues,
                planetId,
                IssueUnitType.CapitalShip,
                System.Math.Max(1, garrison),
                CountCapitalShips(planet, factionId),
                score
            );

            // Troops: garrison requirement
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
