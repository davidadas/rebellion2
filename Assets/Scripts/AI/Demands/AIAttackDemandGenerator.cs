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
            IReadOnlyList<AIAttackDemand> demands = BuildDemands(context);
            context?.SetAttackDemands(demands);
            if (context?.Assessment == null || context.MovementQueries == null)
                return;
            foreach (AIAttackDemand demand in demands)
            {
                bool hasInbound =
                    context
                        .Assessment.GetHostileFleets(demand.TargetPlanet)
                        .Any(fleet =>
                            fleet.Movement != null
                            || fleet
                                .GetChildren<CapitalShip>()
                                .Any(ship =>
                                    ship.Movement != null
                                    || ship.GetChildren<Starfighter>()
                                        .Any(fighter => fighter.Movement != null)
                                )
                        )
                    || demand
                        .TargetPlanet.GetAllStarfighters()
                        .Any(fighter => fighter.Movement != null);
                if (!hasInbound)
                    continue;
                foreach (
                    Fleet fleet in context.Assessment.OwnedFleets.Where(fleet =>
                        fleet.RoleType == FleetRoleType.Battle
                    )
                )
                    context.SetFleetAttackDemand(fleet, BuildArrivalDemand(context, fleet, demand));
            }
        }

        /// <summary>Measures known defenders expected to be available when one fleet reaches its target.</summary>
        /// <param name="context">The current faction assessment.</param>
        /// <param name="fleet">The prospective attacking fleet.</param>
        /// <param name="demand">The target's current ground and orbital requirements.</param>
        /// <returns>The requirements for this fleet's arrival, without changing any orders.</returns>
        private static AIAttackDemand BuildArrivalDemand(
            AITurnContext context,
            Fleet fleet,
            AIAttackDemand demand
        )
        {
            Planet origin = context.Assessment.GetFleetPlanet(fleet);
            if (origin == null)
                return demand;
            int arrivalTicks = fleet.Movement?.TicksRemaining() ?? 0;
            if (origin.InstanceID != demand.TargetPlanet.InstanceID)
                arrivalTicks = (int)
                    Math.Min(
                        int.MaxValue,
                        (long)arrivalTicks
                            + context.MovementQueries.CalculateTransitTicks(
                                fleet,
                                origin,
                                demand.TargetPlanet
                            )
                    );
            var defense = context.Assessment.GetHostileOrbitalCapabilitiesAtArrival(
                demand.TargetPlanet,
                arrivalTicks
            );
            GameConfig.AIFleetDeploymentConfig config = context.Game.Config.AI.FleetDeployment;
            int orbital = IntegerMath.ScaleByPercent(
                defense.Combat,
                config.AttackStrengthPercentOfStrongestHostileFleet
            );
            int combat = IntegerMath.ScaleByPercent(
                Math.Max(config.MinimumAttackStrength, orbital),
                GetIntelReservePercent(context, demand.TargetPlanet)
            );
            return new AIAttackDemand(
                demand.TargetPlanet,
                combat,
                orbital,
                IntegerMath.ScaleByPercent(
                    defense.CapitalFirepower,
                    config.AssaultFleetCompositionReservePercent
                ),
                IntegerMath.ScaleByPercent(
                    defense.FighterFirepower,
                    config.AssaultFleetCompositionReservePercent
                ),
                IntegerMath.ScaleByPercent(
                    defense.Fighters,
                    config.AssaultFleetCompositionReservePercent
                ),
                demand.RegimentStrength,
                demand.RegimentCount,
                demand.OccupationRegimentCount,
                demand.BombardmentStrength,
                demand.IsAssaultBlockedByShields
            );
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
                GetCapitalShipFirepower(context, planet),
                GetStarfighterFirepower(context, planet),
                GetStarfighterCount(context, planet),
                Math.Max(
                    1,
                    IntegerMath.ScaleByPercent(
                        defendingStrength,
                        config.AI.FleetDeployment.AttackStrengthPercentOfDefense
                    )
                ),
                combatCount + occupationCount,
                occupationCount,
                blockedByShields ? assessment.GetBombardmentShieldStrength(planet) + 1 : 0,
                blockedByShields
            );
        }

        /// <summary>
        /// Calculates required carried-starfighter capacity at one planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The target planet.</param>
        /// <returns>The required carried-starfighter capacity.</returns>
        private static int GetStarfighterCount(AITurnContext context, Planet planet)
        {
            int fleetCount = context.Assessment.GetStrongestHostileFleetStarfighterCount(planet);
            int planetaryCount = context.Assessment.GetHostilePlanetaryStarfighterCount(planet);
            return IntegerMath.ScaleByPercent(
                fleetCount + planetaryCount,
                context.Game.Config.AI.FleetDeployment.AssaultFleetCompositionReservePercent
            );
        }

        /// <summary>
        /// Calculates required fighter-target strength at one planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The target planet.</param>
        /// <returns>The required fighter-target strength.</returns>
        private static int GetStarfighterFirepower(AITurnContext context, Planet planet)
        {
            int fleetFirepower =
                context.Assessment.GetStrongestHostileFleetFirepowerAgainstStarfighters(planet);
            int planetaryFirepower =
                context.Assessment.GetHostilePlanetaryFirepowerAgainstStarfighters(planet);
            return IntegerMath.ScaleByPercent(
                    fleetFirepower,
                    context.Game.Config.AI.FleetDeployment.AssaultFleetCompositionReservePercent
                )
                + IntegerMath.ScaleByPercent(
                    planetaryFirepower,
                    context.Game.Config.AI.FleetDeployment.AssaultFleetCompositionReservePercent
                );
        }

        /// <summary>
        /// Calculates required capital-ship firepower at one planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The target planet.</param>
        /// <returns>The required capital-ship firepower.</returns>
        private static int GetCapitalShipFirepower(AITurnContext context, Planet planet)
        {
            return IntegerMath.ScaleByPercent(
                context.Assessment.GetStrongestHostileFleetFirepowerAgainstCapitalShips(planet),
                context.Game.Config.AI.FleetDeployment.AssaultFleetCompositionReservePercent
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
                context.Assessment.GetStrongestHostileFleetCombatValueAgainstCapitalShips(planet)
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
