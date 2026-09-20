using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Util.Common;

namespace Rebellion.AI.Director
{
    /// <summary>
    /// Defines the force required for strategic attacks from turn-scoped assessed facts.
    /// </summary>
    public sealed class AIAttackRequirements
    {
        private readonly AITurnContext _context;
        private readonly Dictionary<string, int> _combatByPlanetId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _combatBySystemId = new(StringComparer.Ordinal);

        /// <summary>
        /// Creates attack requirements for one AI turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public AIAttackRequirements(AITurnContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Returns combat strength required to attack a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The required attack combat strength.</returns>
        public int GetCombatStrength(Planet planet)
        {
            if (planet == null || _context?.Game?.Config == null)
                return 0;

            if (_combatByPlanetId.TryGetValue(planet.InstanceID, out int required))
                return required;

            required = IntegerMath.ScaleByPercent(
                Math.Max(
                    _context.Game.Config.AI.FleetDeployment.MinimumAttackStrength,
                    GetSystemOrbitalStrength(planet)
                ),
                GetIntelReservePercent(planet)
            );
            _combatByPlanetId[planet.InstanceID] = required;
            return required;
        }

        /// <summary>
        /// Returns the bounded combat reserve applied as attack-target intelligence ages.
        /// </summary>
        /// <param name="planet">Attack target whose intelligence age is evaluated.</param>
        /// <returns>Required combat percentage, where one hundred means no stale-intel reserve.</returns>
        public int GetIntelReservePercent(Planet planet)
        {
            if (planet == null || _context?.Game?.Config == null)
                return 100;

            int maximumAge = Math.Max(
                1,
                _context.Game.Config.AI.MissionPlanning.HostileMissionMaximumIntelAgeTicks
            );
            int age = _context.Assessment.GetPlanetIntelAge(planet);
            if (age <= maximumAge)
                return 100;

            GameConfig.AIFleetDeploymentConfig config = _context.Game.Config.AI.FleetDeployment;
            int maximumPercent = Math.Max(100, config.StaleIntelMaximumAttackStrengthPercent);
            int saturationAge =
                maximumAge * Math.Max(1, config.StaleIntelReserveSaturationIntervals);
            int staleAge = Math.Min(age - maximumAge, saturationAge - maximumAge);
            int staleRange = Math.Max(1, saturationAge - maximumAge);
            return 100 + (maximumPercent - 100) * staleAge / staleRange;
        }

        /// <summary>
        /// Returns the orbital strength required to defeat known forces at a planet.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>The required orbital strength.</returns>
        public int GetOrbitalStrength(Planet planet)
        {
            if (planet == null || _context?.Game?.Config == null)
                return 0;

            int hostileStrength =
                _context.Assessment.GetStrongestHostileFleetStrength(planet)
                + _context.Assessment.GetHostilePlanetaryStarfighterStrength(planet);
            return hostileStrength > 0
                ? IntegerMath.ScaleByPercent(
                    hostileStrength,
                    _context
                        .Game
                        .Config
                        .AI
                        .FleetDeployment
                        .AttackStrengthPercentOfStrongestHostileFleet
                )
                : 0;
        }

        /// <summary>
        /// Returns the largest orbital requirement among known planets in a target's system.
        /// </summary>
        /// <param name="targetPlanet">Planet identifying the target system.</param>
        /// <returns>The system orbital requirement.</returns>
        private int GetSystemOrbitalStrength(Planet targetPlanet)
        {
            string systemId = _context.Assessment.GetPlanetSystemId(targetPlanet);
            IReadOnlyList<Planet> planets = _context.Assessment.GetKnownSystemPlanets(systemId);
            if (string.IsNullOrEmpty(systemId) || planets.Count == 0)
                return GetOrbitalStrength(targetPlanet);

            if (_combatBySystemId.TryGetValue(systemId, out int required))
                return required;

            required = planets.Select(GetOrbitalStrength).DefaultIfEmpty().Max();
            _combatBySystemId[systemId] = required;
            return required;
        }
    }
}
