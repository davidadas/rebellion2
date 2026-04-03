using System.Collections.Generic;
using Rebellion.Game;

namespace Rebellion.AI.Providers
{
    /// <summary>
    /// Subtype 3: Combat fleet composition.
    /// The offensive warship mix: starfighters, capital ships, and heavy frigates.
    /// No ground troops — this is for space superiority.
    /// </summary>
    public class CombatFleetIssueProvider : IssueProvider
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
            int capitalShips = CountCapitalShips(planet, factionId);

            // Want at least 2 capital ships for a combat-capable fleet
            AddIssueIfDeficit(issues, planetId, IssueUnitType.CapitalShip, 2, capitalShips, score);

            // Starfighters: at least 2 per capital ship present
            AddIssueIfDeficit(
                issues,
                planetId,
                IssueUnitType.Starfighter,
                capitalShips * 2,
                CountStarfighters(planet, factionId),
                score
            );

            return issues;
        }
    }
}
