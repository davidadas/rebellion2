using System.Collections.Generic;
using Rebellion.Game;

namespace Rebellion.AI.Providers
{
    /// <summary>
    /// Subtype 4: Garrison defense force.
    /// Like FullGarrison but with a system-state precheck — only generates
    /// issues for planets that are contested or have low popular support.
    /// </summary>
    public class GarrisonDefenseIssueProvider : IssueProvider
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
            int support = planet.GetPopularSupport(factionId);

            // System-state gate: only flag defense issues for threatened planets
            if (
                !planet.IsContested()
                && !planet.IsInUprising
                && support >= game.Config.AI.Garrison.SupportThreshold
            )
                return issues;

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
