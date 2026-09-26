using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Util.Mathematics;

namespace Rebellion.AI.Demands
{
    /// <summary>
    /// Generates score-free attack capability demands from assessed target facts.
    /// </summary>
    internal sealed class AIAttackDemandGenerator : IAIDemandGenerator
    {
        /// <summary>
        /// Generates attack demands for every planet in the faction view.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public void Generate(AITurnContext context)
        {
            context?.SetAttackDemands(BuildDemands(context));
        }

        /// <summary>
        /// Builds attack demands for every planet in the faction view.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The generated attack demands.</returns>
        internal IReadOnlyList<AIAttackDemand> BuildDemands(AITurnContext context)
        {
            if (
                context?.Game?.Config == null
                || context.Faction == null
                || context.Assessment == null
            )
                return Array.Empty<AIAttackDemand>();

            AIAssessment assessment = context.Assessment;
            Dictionary<string, int> orbitalStrengths = assessment.FactionViewPlanets.ToDictionary(
                planet => planet.InstanceID,
                planet => GetOrbitalStrength(context, planet),
                StringComparer.Ordinal
            );

            return assessment
                .KnownColonizedPlanets.Select(planet =>
                    BuildDemand(context, planet, orbitalStrengths[planet.InstanceID])
                )
                .ToList();
        }

        /// <summary>
        /// Builds one planet's attack demand from cached orbital facts.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The known target planet.</param>
        /// <param name="orbitalStrength">The target's required orbital strength.</param>
        /// <returns>The target's attack demand.</returns>
        private static AIAttackDemand BuildDemand(
            AITurnContext context,
            Planet planet,
            int orbitalStrength
        )
        {
            GameConfig config = context.Game.Config;
            AIAssessment assessment = context.Assessment;
            bool blockedByShields = PlanetaryAssaultResolver.IsBlockedByShields(
                planet,
                config.Combat.PlanetaryAssault.ShieldGeneratorLimit
            );
            int occupationCount = GetOccupationRegimentCount(context, planet);
            int defendingCount = assessment.GetDefendingRegimentCount(planet);
            int combatCount = defendingCount == 0 ? 0 : defendingCount + 1;
            int defendingStrength =
                defendingCount == 0 ? 0 : assessment.GetDefendingRegimentDefenseStrength(planet);
            int combatStrength = IntegerMath.ScaleByPercent(
                Math.Max(config.AI.FleetDeployment.MinimumAttackStrength, orbitalStrength),
                GetIntelReservePercent(context, planet)
            );
            return new AIAttackDemand(
                planet,
                combatStrength,
                orbitalStrength,
                Math.Max(
                    1,
                    IntegerMath.ScaleByPercent(
                        defendingStrength,
                        config.AI.FleetDeployment.AttackStrengthPercentOfDefense
                    )
                ),
                combatCount + occupationCount,
                occupationCount,
                blockedByShields ? assessment.GetBombardmentShieldResistance(planet) + 1 : 0,
                blockedByShields
            );
        }

        /// <summary>
        /// Calculates required orbital strength at one planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The target planet.</param>
        /// <returns>The required orbital strength.</returns>
        private static int GetOrbitalStrength(AITurnContext context, Planet planet)
        {
            int hostileStrength =
                planet
                    .GetChildren<Fleet>()
                    .Where(fleet =>
                        fleet.Movement == null
                        && !string.IsNullOrEmpty(fleet.GetOwnerInstanceID())
                        && fleet.GetOwnerInstanceID() != context.Faction.InstanceID
                    )
                    .Select(fleet => fleet.GetCombatValue())
                    .DefaultIfEmpty()
                    .Max()
                + planet
                    .GetAllStarfighters()
                    .Where(starfighter =>
                        !string.IsNullOrEmpty(starfighter.GetOwnerInstanceID())
                        && starfighter.GetOwnerInstanceID() != context.Faction.InstanceID
                    )
                    .Sum(starfighter => starfighter.GetCombatValue());
            return hostileStrength > 0
                ? IntegerMath.ScaleByPercent(
                    hostileStrength,
                    context
                        .Game
                        .Config
                        .AI
                        .FleetDeployment
                        .AttackStrengthPercentOfStrongestHostileFleet
                )
                : 0;
        }

        /// <summary>
        /// Calculates the stale-intelligence reserve for one target.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The target planet.</param>
        /// <returns>The required combat percentage.</returns>
        private static int GetIntelReservePercent(AITurnContext context, Planet planet)
        {
            int maximumAge = Math.Max(
                1,
                context.Game.Config.AI.MissionPlanning.HostileMissionMaximumIntelAgeTicks
            );
            int age = context.Assessment.GetPlanetIntelAge(planet);
            if (age <= maximumAge)
                return 100;

            GameConfig.AIFleetDeploymentConfig config = context.Game.Config.AI.FleetDeployment;
            int maximumPercent = Math.Max(100, config.StaleIntelMaximumAttackStrengthPercent);
            int saturationAge =
                maximumAge * Math.Max(1, config.StaleIntelReserveSaturationIntervals);
            int staleAge = Math.Min(age - maximumAge, saturationAge - maximumAge);
            int staleRange = Math.Max(1, saturationAge - maximumAge);
            return 100 + (maximumPercent - 100) * staleAge / staleRange;
        }

        /// <summary>
        /// Calculates the regiment count needed to hold a captured planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The target planet.</param>
        /// <returns>The required occupation regiment count.</returns>
        private static int GetOccupationRegimentCount(AITurnContext context, Planet planet)
        {
            Faction faction = context.Faction;
            GameConfig config = context.Game.Config;
            int minimum = Math.Max(
                1,
                config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount
            );
            int stabilityRequirement = UprisingQueries.CalculateGarrisonRequirement(
                planet,
                faction,
                config.AI.Garrison
            );
            int landingCapacity = config.Combat.PlanetaryAssault.CaptureGarrisonCount;
            return Math.Max(minimum, Math.Min(stabilityRequirement, landingCapacity));
        }
    }
}
