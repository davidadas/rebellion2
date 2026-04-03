using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Providers;
using Rebellion.Game;

namespace Rebellion.AI
{
    /// <summary>
    /// Orchestrates the 7 automation issue providers.
    /// Evaluates planets and returns a prioritized list of unmet needs
    /// for the AIManager to act on.
    /// </summary>
    public class AutomationIssueSystem
    {
        private readonly GameRoot game;
        private readonly List<IssueProvider> providers;

        public AutomationIssueSystem(GameRoot game)
        {
            this.game = game;
            providers = new List<IssueProvider>
            {
                new CapitalShipIssueProvider(),
                new FullGarrisonIssueProvider(),
                new CombatFleetIssueProvider(),
                new GarrisonDefenseIssueProvider(),
                new ManufacturingIssueProvider(),
                new SpecialOpsIssueProvider(),
                new SystemDefenseIssueProvider(),
            };
        }

        /// <summary>
        /// Evaluate a single planet across all providers and return issues sorted by deficit.
        /// </summary>
        /// <param name="faction">The faction being evaluated.</param>
        /// <param name="planet">The planet to evaluate.</param>
        public List<AutomationIssue> EvaluatePlanet(Faction faction, Planet planet)
        {
            List<AutomationIssue> allIssues = new List<AutomationIssue>();

            foreach (IssueProvider provider in providers)
            {
                allIssues.AddRange(provider.BuildIssues(game, faction, planet));
            }

            // Deduplicate: if multiple providers flagged the same (planet, unitType),
            // keep the one with the largest deficit.
            List<AutomationIssue> deduped = allIssues
                .GroupBy(i => (i.PlanetInstanceID, i.UnitType))
                .Select(g => g.OrderByDescending(i => i.Deficit).First())
                .OrderByDescending(i => i.Deficit)
                .ToList();

            return deduped;
        }

        /// <summary>
        /// Evaluate all owned planets and return aggregate issues sorted by deficit.
        /// </summary>
        /// <param name="faction">The faction being evaluated.</param>
        public List<AutomationIssue> EvaluateAll(Faction faction)
        {
            List<AutomationIssue> allIssues = new List<AutomationIssue>();

            foreach (Planet planet in faction.GetOwnedUnitsByType<Planet>())
            {
                allIssues.AddRange(EvaluatePlanet(faction, planet));
            }

            return allIssues.OrderByDescending(i => i.Deficit).ToList();
        }
    }
}
