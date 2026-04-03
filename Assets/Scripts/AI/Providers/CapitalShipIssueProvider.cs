using System.Collections.Generic;
using Rebellion.Game;

namespace Rebellion.AI.Providers
{
    /// <summary>
    /// Subtype 1: Capital ship fleet presence.
    /// Simplest provider — only tracks whether the planet has enough capital ships.
    /// </summary>
    public class CapitalShipIssueProvider : IssueProvider
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

            // Desire at least 1 capital ship per planet, more if garrison demands it
            int desired = System.Math.Max(1, garrison);
            int actual = CountCapitalShips(planet, factionId);

            AddIssueIfDeficit(issues, planetId, IssueUnitType.CapitalShip, desired, actual, score);

            return issues;
        }
    }
}
