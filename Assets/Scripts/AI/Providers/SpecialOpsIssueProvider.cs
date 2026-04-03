using System.Collections.Generic;
using Rebellion.Game;

namespace Rebellion.AI.Providers
{
    /// <summary>
    /// Subtype 6: Special operations and advanced facilities.
    /// Handles standard military needs plus faction-specific facility construction.
    /// </summary>
    public class SpecialOpsIssueProvider : IssueProvider
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

            AddIssueIfDeficit(
                issues,
                planetId,
                IssueUnitType.CapitalShip,
                System.Math.Max(1, garrison),
                CountCapitalShips(planet, factionId),
                score
            );

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
