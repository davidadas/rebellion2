using System.Collections.Generic;
using Rebellion.Game;

namespace Rebellion.AI.Providers
{
    /// <summary>
    /// Subtype 7: Comprehensive system defense.
    /// The most complete defensive record — covers garrison, starfighters,
    /// capital ships, escort ships, and troops.
    /// </summary>
    public class SystemDefenseIssueProvider : IssueProvider
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

            // Fleet presence
            AddIssueIfDeficit(
                issues,
                planetId,
                IssueUnitType.GarrisonBase,
                1,
                CountFleets(planet, factionId),
                score
            );

            // Capital ships
            int desiredCapitalShips = System.Math.Max(1, garrison);
            AddIssueIfDeficit(
                issues,
                planetId,
                IssueUnitType.CapitalShip,
                desiredCapitalShips,
                CountCapitalShips(planet, factionId),
                score
            );

            // Starfighters: 2 per capital ship
            int capitalShips = CountCapitalShips(planet, factionId);
            AddIssueIfDeficit(
                issues,
                planetId,
                IssueUnitType.Starfighter,
                capitalShips * 2,
                CountStarfighters(planet, factionId),
                score
            );

            // Troops
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
