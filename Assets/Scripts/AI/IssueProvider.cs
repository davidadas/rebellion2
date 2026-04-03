using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;

namespace Rebellion.AI
{
    /// <summary>
    /// Base class for automation issue providers.
    /// Each subclass scans a planet and builds issue records for a specific
    /// mix of unit types (matching the original game's 7 automation subtypes).
    /// </summary>
    public abstract class IssueProvider
    {
        /// <summary>
        /// Scan game state for a planet and return issue records representing unmet needs.
        /// </summary>
        /// <param name="game">The game state.</param>
        /// <param name="faction">The faction being evaluated.</param>
        /// <param name="planet">The planet to evaluate.</param>
        public abstract List<AutomationIssue> BuildIssues(
            GameRoot game,
            Faction faction,
            Planet planet
        );

        /// <summary>
        /// Binary valid/invalid gate matching the original scoreTarget.
        /// Returns 0.9 if the planet is a valid target for this faction, 0.0 otherwise.
        /// </summary>
        /// <param name="faction">The faction being evaluated.</param>
        /// <param name="planet">The planet to score.</param>
        public float ScoreTarget(Faction faction, Planet planet)
        {
            if (planet.GetOwnerInstanceID() != faction.InstanceID)
                return 0f;
            if (!planet.IsColonized)
                return 0f;
            return 0.9f;
        }

        /// <summary>
        /// Calculates garrison requirement for a planet using the proven original formula:
        /// garrison = ceil((supportThreshold - popularSupport) / divisor),
        /// divided by GarrisonEfficiency on core worlds,
        /// doubled during uprising.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="config">Garrison configuration values.</param>
        public static int CalculateGarrisonRequirement(
            Planet planet,
            Faction faction,
            GameConfig.GarrisonConfig config
        )
        {
            int popularSupport = planet.GetPopularSupport(faction.InstanceID);

            if (popularSupport >= config.SupportThreshold)
                return 0;

            int garrison = (int)
                Math.Ceiling(
                    (config.SupportThreshold - popularSupport) / (double)config.GarrisonDivisor
                );

            // Core worlds get reduced garrison based on faction modifier (Empire: /2)
            PlanetSystem parentSystem = planet.GetParentOfType<PlanetSystem>();
            if (
                parentSystem != null
                && parentSystem.SystemType == PlanetSystemType.CoreSystem
                && faction.Modifiers.GarrisonEfficiency > 1
            )
            {
                garrison /= faction.Modifiers.GarrisonEfficiency;
            }

            if (planet.IsInUprising)
                garrison *= config.UprisingMultiplier;

            return garrison;
        }

        /// <summary>
        /// Counts friendly capital ships at a planet (across all friendly fleets).
        /// </summary>
        protected int CountCapitalShips(Planet planet, string factionId)
        {
            return planet
                .GetFleets()
                .Where(f => f.GetOwnerInstanceID() == factionId)
                .Sum(f => f.CapitalShips.Count);
        }

        /// <summary>
        /// Counts friendly starfighters at a planet (fleet-attached and loose).
        /// </summary>
        protected int CountStarfighters(Planet planet, string factionId)
        {
            int fleetFighters = planet
                .GetFleets()
                .Where(f => f.GetOwnerInstanceID() == factionId)
                .Sum(f => f.GetStarfighters().Count());

            int looseFighters = planet
                .GetAllStarfighters()
                .Count(s => s.GetOwnerInstanceID() == factionId);

            return fleetFighters + looseFighters;
        }

        /// <summary>
        /// Counts friendly regiments at a planet (fleet-attached and loose).
        /// </summary>
        protected int CountTroops(Planet planet, string factionId)
        {
            int fleetTroops = planet
                .GetFleets()
                .Where(f => f.GetOwnerInstanceID() == factionId)
                .Sum(f => f.GetRegiments().Count());

            int looseTroops = planet
                .GetAllRegiments()
                .Count(r => r.GetOwnerInstanceID() == factionId);

            return fleetTroops + looseTroops;
        }

        /// <summary>
        /// Counts friendly fleets at a planet.
        /// </summary>
        protected int CountFleets(Planet planet, string factionId)
        {
            return planet.GetFleets().Count(f => f.GetOwnerInstanceID() == factionId);
        }

        /// <summary>
        /// Creates an issue record if there is a deficit.
        /// </summary>
        protected void AddIssueIfDeficit(
            List<AutomationIssue> issues,
            string planetId,
            IssueUnitType unitType,
            int desired,
            int actual,
            float score
        )
        {
            if (desired > actual)
            {
                issues.Add(
                    new AutomationIssue
                    {
                        PlanetInstanceID = planetId,
                        UnitType = unitType,
                        DesiredCount = desired,
                        ActualCount = actual,
                        Score = score,
                    }
                );
            }
        }
    }
}
