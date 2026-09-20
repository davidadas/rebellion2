using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Systems;
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
        private readonly Dictionary<string, int> _regimentCountByPlanetId = new(
            StringComparer.Ordinal
        );

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
        /// Returns regiment strength required to capture a planet.
        /// </summary>
        /// <param name="planet">Target planet.</param>
        /// <returns>The required regiment strength.</returns>
        public int GetRegimentStrength(Planet planet)
        {
            if (planet == null || _context?.Game?.Config == null)
                return 0;

            return Math.Max(
                1,
                IntegerMath.ScaleByPercent(
                    _context.Assessment.GetDefendingRegimentDefenseStrength(planet),
                    _context.Game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense
                )
            );
        }

        /// <summary>
        /// Returns ground strength required after accounting for a fleet's bombardment.
        /// </summary>
        /// <param name="fleet">Fleet assigned to the attack.</param>
        /// <param name="planet">Planet being attacked.</param>
        /// <param name="projected">Whether queued fleet bombardment is included.</param>
        /// <returns>The required regiment attack strength.</returns>
        public int GetRegimentStrength(Fleet fleet, Planet planet, bool projected = false)
        {
            return CanBombardDefenders(fleet, planet, projected) ? 0 : GetRegimentStrength(planet);
        }

        /// <summary>
        /// Returns regiment count required to attack a planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The required regiment count.</returns>
        public int GetRegimentCount(Planet planet)
        {
            if (planet == null || _context?.Game?.Config == null)
                return 0;

            if (_regimentCountByPlanetId.TryGetValue(planet.InstanceID, out int required))
                return required;

            required = GetCombatRegimentCount(planet) + GetOccupationRegimentCount(planet);
            _regimentCountByPlanetId[planet.InstanceID] = required;
            return required;
        }

        /// <summary>
        /// Returns regiment count required after accounting for a fleet's bombardment.
        /// </summary>
        /// <param name="fleet">Fleet assigned to the attack.</param>
        /// <param name="planet">Planet being attacked.</param>
        /// <param name="projected">Whether queued fleet bombardment is included.</param>
        /// <returns>The required regiment count.</returns>
        public int GetRegimentCount(Fleet fleet, Planet planet, bool projected = false)
        {
            return CanBombardDefenders(fleet, planet, projected)
                ? GetOccupationRegimentCount(planet)
                : GetRegimentCount(planet);
        }

        /// <summary>
        /// Returns bombardment strength required to penetrate planetary shields.
        /// </summary>
        /// <param name="planet">Target planet.</param>
        /// <returns>The required bombardment strength.</returns>
        public int GetBombardmentStrength(Planet planet)
        {
            return IsAssaultBlockedByShields(planet)
                ? _context.Assessment.GetBombardmentShieldResistance(planet) + 1
                : 0;
        }

        /// <summary>
        /// Returns whether planetary shields currently block an assault.
        /// </summary>
        /// <param name="planet">Target planet.</param>
        /// <returns>True when shields block the assault.</returns>
        public bool IsAssaultBlockedByShields(Planet planet)
        {
            return planet != null
                && _context?.Game?.Config != null
                && PlanetaryAssaultResolver.IsBlockedByShields(
                    planet,
                    _context.Game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit
                );
        }

        /// <summary>
        /// Returns whether a fleet cannot penetrate shields that prevent its ground assault.
        /// </summary>
        /// <param name="fleet">Fleet assigned to the attack.</param>
        /// <param name="planet">Target planet.</param>
        /// <returns>True when the fleet must wait for sabotage or choose another target.</returns>
        public bool IsBlockedByShields(Fleet fleet, Planet planet)
        {
            return IsAssaultBlockedByShields(planet)
                && _context.Assessment.GetFleetBombardmentStrength(fleet)
                    < GetBombardmentStrength(planet);
        }

        /// <summary>
        /// Returns whether shields block a planet selected for attack preparation.
        /// </summary>
        /// <param name="planet">Target planet.</param>
        /// <returns>True when the target is blocked.</returns>
        public bool IsTargetBlockedByShields(Planet planet)
        {
            return IsAssaultBlockedByShields(planet)
                && _context.Assessment.IsAttackPreparationTarget(planet);
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

        /// <summary>
        /// Returns the regiment count needed to defeat current ground defenders.
        /// </summary>
        /// <param name="planet">Planet being attacked.</param>
        /// <returns>The required combat regiment count.</returns>
        private int GetCombatRegimentCount(Planet planet)
        {
            int defenderCount = _context.Assessment.GetDefendingRegimentCount(planet);
            return defenderCount == 0 ? 0 : defenderCount + 1;
        }

        /// <summary>
        /// Returns the force needed to hold a planet after its defenders are removed.
        /// </summary>
        /// <param name="planet">Planet being occupied.</param>
        /// <returns>The required occupation regiment count.</returns>
        private int GetOccupationRegimentCount(Planet planet)
        {
            if (planet == null || _context?.Game?.Config == null)
                return 0;

            int minimum = Math.Max(
                1,
                _context.Game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount
            );
            int stabilityRequirement = UprisingSystem.CalculateGarrisonRequirement(
                planet,
                _context.Faction,
                _context.Game.Config.AI.Garrison
            );
            int landingCapacity = _context.Game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount;
            return Math.Max(minimum, Math.Min(stabilityRequirement, landingCapacity));
        }

        /// <summary>
        /// Returns whether a fleet can penetrate shields and bombard defending regiments.
        /// </summary>
        /// <param name="fleet">Fleet assigned to the attack.</param>
        /// <param name="planet">Planet being attacked.</param>
        /// <param name="projected">Whether queued fleet bombardment is included.</param>
        /// <returns>True when bombardment can reach defending regiments.</returns>
        private bool CanBombardDefenders(Fleet fleet, Planet planet, bool projected)
        {
            if (
                fleet == null
                || planet == null
                || _context.Assessment.GetDefendingRegimentCount(planet) == 0
            )
                return false;

            int bombardmentStrength = projected
                ? _context.Assessment.GetProjectedFleetBombardmentStrength(fleet)
                : _context.Assessment.GetFleetBombardmentStrength(fleet);
            return bombardmentStrength > _context.Assessment.GetBombardmentShieldResistance(planet);
        }
    }
}
