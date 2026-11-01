using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
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
                        .TargetPlanet.GetAllBuildings()
                        .Any(building =>
                            building.IsPlanetaryShieldGenerator()
                            && (
                                building.ManufacturingStatus != ManufacturingStatus.Complete
                                || building.Movement != null
                            )
                        )
                    || demand
                        .TargetPlanet.GetAllStarfighters()
                        .Any(fighter => fighter.Movement != null);
                if (
                    !hasInbound
                    && !IsSupportOpportunityTarget(context, demand.TargetPlanet)
                    && !IsLocalAttackTarget(context, demand.TargetPlanet)
                )
                    continue;
                foreach (
                    Fleet fleet in context.Assessment.OwnedFleets.Where(fleet =>
                        fleet.RoleType == FleetRoleType.Battle
                    )
                )
                    context.SetFleetAttackDemand(fleet, BuildArrivalDemand(context, fleet, demand));
            }
        }

        /// <summary>
        /// Measures known defenders expected to be available when one fleet reaches its target.
        /// </summary>
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
            int arrivalTicks = context.GetAttackArrivalTicks(fleet, demand.TargetPlanet);
            var defense = context.Assessment.GetHostileOrbitalCapabilitiesAtArrival(
                demand.TargetPlanet,
                arrivalTicks
            );
            GameConfig.AIFleetDeploymentConfig config = context.Game.Config.AI.FleetDeployment;
            int orbital = IntegerMath.ScaleByPercent(
                defense.Combat,
                config.AttackStrengthPercentOfStrongestHostileFleet
            );
            bool supportOpportunity =
                IsSupportOpportunityTarget(context, demand.TargetPlanet)
                && arrivalTicks <= Math.Max(0, config.SupportOpportunityMaximumTravelTicks)
                && (long)context.Assessment.GetPlanetOrbitalIntelAge(demand.TargetPlanet)
                    + arrivalTicks
                    <= Math.Max(0, config.SupportOpportunityMaximumIntelAgeTicks);
            bool localAttack =
                IsLocalAttackTarget(context, demand.TargetPlanet)
                && arrivalTicks <= Math.Max(0, config.LocalAttackMaximumTravelTicks);
            int localMinimumPercent = 100;
            if (localAttack)
            {
                long graceAge = Math.Max(0, config.LocalAttackMaximumIntelAgeTicks);
                long arrivalAge =
                    (long)
                        Math.Max(
                            context.Assessment.GetPlanetIntelAge(demand.TargetPlanet),
                            context.Assessment.GetPlanetOrbitalIntelAge(demand.TargetPlanet)
                        ) + arrivalTicks;
                double staleRisk = Math.Clamp(
                    (arrivalAge - graceAge)
                        / (double)Math.Max(1L, GetLocalAttackIntelSaturationAge(config) - graceAge),
                    0,
                    1
                );
                int localFloorPercent = Math.Clamp(
                    config.LocalAttackMinimumStrengthPercent,
                    0,
                    100
                );
                localMinimumPercent = (int)
                    Math.Ceiling(localFloorPercent + (100 - localFloorPercent) * staleRisk);
            }
            int minimumPercent = Math.Min(
                supportOpportunity ? config.SupportOpportunityMinimumAttackStrengthPercent : 100,
                localMinimumPercent
            );
            int minimum = IntegerMath.ScaleByPercent(
                config.MinimumAttackStrength,
                Math.Clamp(minimumPercent, 0, 100)
            );
            int combat = Math.Max(
                minimum,
                IntegerMath.ScaleByPercent(
                    orbital,
                    GetIntelReservePercent(context, demand.TargetPlanet)
                )
            );
            var shields = context.Assessment.GetPlanetaryShieldsAtArrival(
                demand.TargetPlanet,
                arrivalTicks,
                context.MovementQueries
            );
            bool blockedByShields =
                shields.Count >= context.Game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit;
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
                blockedByShields ? (int)Math.Min(int.MaxValue, (long)shields.Strength + 1) : 0,
                blockedByShields,
                shields.Strength,
                supportOpportunity && HasBombardmentSupportReserve(context, demand.TargetPlanet)
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
            var shields = assessment.GetPlanetaryShieldsAtArrival(
                planet,
                0,
                context.MovementQueries
            );
            bool blockedByShields =
                shields.Count >= config.Combat.PlanetaryAssault.ShieldGeneratorLimit;
            int occupationCount = GetOccupationRegimentCount(context, planet);
            int defendingCount = assessment.GetDefendingRegimentCount(planet);
            int combatCount = defendingCount == 0 ? 0 : defendingCount + 1;
            int defendingStrength =
                defendingCount == 0 ? 0 : assessment.GetDefendingRegimentDefenseStrength(planet);
            int combatStrength = Math.Max(
                config.AI.FleetDeployment.MinimumAttackStrength,
                IntegerMath.ScaleByPercent(orbitalStrength, GetIntelReservePercent(context, planet))
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
                blockedByShields ? (int)Math.Min(int.MaxValue, (long)shields.Strength + 1) : 0,
                blockedByShields,
                shields.Strength
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
        /// Checks whether recent orbital intelligence permits a smaller local attack force.
        /// </summary>
        /// <param name="context">The faction's observed targets and local attack settings.</param>
        /// <param name="planet">The prospective target.</param>
        /// <returns>True for recently observed non-headquarters targets without known interdiction.</returns>
        private static bool IsLocalAttackTarget(AITurnContext context, Planet planet)
        {
            GameConfig.AIFleetDeploymentConfig config = context.Game.Config.AI.FleetDeployment;
            AIAssessment assessment = context.Assessment;
            int age = Math.Max(
                assessment.GetPlanetIntelAge(planet),
                assessment.GetPlanetOrbitalIntelAge(planet)
            );
            return config.LocalAttackMinimumStrengthPercent < 100
                && planet?.IsHeadquarters == false
                && assessment.IsEnemyPlanet(planet)
                && age < int.MaxValue
                && age <= GetLocalAttackIntelSaturationAge(config)
                && !assessment
                    .GetHostileFleets(planet)
                    .SelectMany(fleet => fleet.GetChildren<CapitalShip>())
                    .Any(ship => ship.HasGravityWell);
        }

        /// <summary>
        /// Returns the intelligence age at which a local attack needs the full baseline force.
        /// </summary>
        /// <param name="config">The local attack grace window and staleness reserve intervals.</param>
        /// <returns>The age where the local force reduction has completely expired.</returns>
        private static long GetLocalAttackIntelSaturationAge(
            GameConfig.AIFleetDeploymentConfig config
        )
        {
            return (long)Math.Max(1, config.LocalAttackMaximumIntelAgeTicks)
                * Math.Max(1, config.StaleIntelReserveSaturationIntervals);
        }

        /// <summary>
        /// Checks whether recent orbital memory exposes a garrison whose removal benefits support.
        /// </summary>
        /// <param name="context">The faction's known defenses and attack settings.</param>
        /// <param name="planet">The prospective target.</param>
        /// <returns>True when a nearby fleet may assess a limited bombardment opportunity.</returns>
        private static bool IsSupportOpportunityTarget(AITurnContext context, Planet planet)
        {
            GameConfig.AIFleetDeploymentConfig config = context.Game.Config.AI.FleetDeployment;
            AIAssessment assessment = context.Assessment;
            return config.SupportOpportunityMinimumAttackStrengthPercent < 100
                && assessment.HasSupportBombardmentOpportunity(planet)
                && assessment.GetPlanetOrbitalIntelAge(planet)
                    <= Math.Max(0, config.SupportOpportunityMaximumIntelAgeTicks)
                && !assessment
                    .GetHostileFleets(planet)
                    .SelectMany(fleet => fleet.GetChildren<CapitalShip>())
                    .Any(ship => ship.HasGravityWell);
        }

        /// <summary>
        /// Checks whether favorable support can survive a civilian hit before the garrison is cleared.
        /// </summary>
        /// <param name="context">The faction and configured political reactions.</param>
        /// <param name="planet">The prospective bombardment target.</param>
        /// <returns>True when an occupation force is unnecessary after one full support penalty.</returns>
        private static bool HasBombardmentSupportReserve(AITurnContext context, Planet planet)
        {
            Faction faction = context.Faction;
            int sectorPenalty =
                planet.GetParentOfType<PlanetSector>()?.SectorType == PlanetSectorType.Core
                    ? faction.Settings.CivilianBombardmentCoreSupportPenalty
                    : faction.Settings.CivilianBombardmentOuterRimSupportPenalty;
            long penalty =
                Math.Min(0, context.Game.Config.Combat.Bombardment.CivilianSupportPenalty)
                + (long)Math.Min(0, sectorPenalty);
            long projectedSupport = context.Assessment.GetFactionPopularSupport(planet) + penalty;
            long opposingSupport = planet.GetPopularSupport(planet.GetOwnerInstanceID()) - penalty;
            return projectedSupport > opposingSupport;
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
            int age = Math.Max(
                context.Assessment.GetPlanetIntelAge(planet),
                context.Assessment.GetPlanetOrbitalIntelAge(planet)
            );
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
        /// Calculates the regiment count needed to hold a captured planet at its known friendly support.
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
                config.AI.Garrison,
                popularSupport: context.Assessment.GetFactionPopularSupport(planet)
            );
            int landingCapacity = config.Combat.PlanetaryAssault.CaptureGarrisonCount;
            return Math.Max(minimum, Math.Min(stabilityRequirement, landingCapacity));
        }
    }
}
