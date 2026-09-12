using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.AI.Director
{
    /// <summary>
    /// Turn-scoped allocation decisions shared by strategic planners and proposals.
    /// </summary>
    public sealed class AIStrategicPlan
    {
        private readonly AITurnContext _context;
        private readonly int _targetBattleFleetCount;
        private readonly int _targetMobileCombatStrength;
        private readonly Dictionary<string, AIPlanetDefenseCommitment> _defenseByPlanetId =
            new Dictionary<string, AIPlanetDefenseCommitment>(StringComparer.Ordinal);

        /// <summary>
        /// Returns the number of independently deployable battle fleets allocated for this turn.
        /// Fleet count is a deployment constraint; it does not define total desired combat power.
        /// </summary>
        public int TargetBattleFleetCount => _targetBattleFleetCount;

        /// <summary>
        /// Returns total desired mobile combat power, independently of deployment fleet count.
        /// </summary>
        public int TargetMobileCombatStrength => _targetMobileCombatStrength;

        /// <summary>
        /// Returns the combat allocation for one fleet assembling without a campaign target.
        /// </summary>
        public int AssemblyFleetCombatStrength =>
            _targetBattleFleetCount > 0
                ? IntegerMath.DivideRoundedUp(_targetMobileCombatStrength, _targetBattleFleetCount)
                : 0;

        /// <summary>
        /// Builds the allocations shared by one faction AI turn.
        /// </summary>
        /// <param name="context">The turn context and cached assessment facts.</param>
        public AIStrategicPlan(AITurnContext context)
        {
            _context = context;
            if (_context?.Game?.Config == null)
                return;

            var config = _context.Game.Config.AI.FleetDeployment;
            int operationalPlanetCount = _context.Assessment.OwnedPlanets.Count(planet =>
                planet.IsColonized && !planet.IsDestroyed
            );
            _targetBattleFleetCount = Math.Max(
                config.MinimumBattleFleetCount,
                IntegerMath.DivideRoundedUp(operationalPlanetCount, config.PlanetsPerBattleFleet)
            );
            int minimumMobileCombatStrength =
                config.MinimumMobileCombatStrength > 0
                    ? config.MinimumMobileCombatStrength
                    : config.MinimumBattleFleetCount * config.MinimumAttackStrength;
            int mobileCombatStrengthPerPlanet =
                config.MobileCombatStrengthPerPlanet > 0
                    ? config.MobileCombatStrengthPerPlanet
                    : IntegerMath.DivideRoundedUp(
                        config.MinimumAttackStrength,
                        config.PlanetsPerBattleFleet
                    );
            _targetMobileCombatStrength = Math.Max(
                minimumMobileCombatStrength,
                operationalPlanetCount * mobileCombatStrengthPerPlanet
            );
        }

        /// <summary>
        /// Returns whether the fleet may leave without violating this turn's defense allocation.
        /// Fleets in hostile territory are never trapped by friendly-defense commitments.
        /// </summary>
        public bool CanFleetDepart(Fleet fleet)
        {
            Planet planet = _context?.Assessment?.GetFleetPlanet(fleet);
            if (!_context.Assessment.IsOwnedPlanet(planet))
                return true;

            AIPlanetDefenseCommitment commitment = GetDefenseCommitment(planet);
            if (commitment.HoldAllLocalFleets)
                return false;

            if (commitment.RequiredStrength <= 0)
                return true;

            int remainingStrength = _context
                .Assessment.GetFriendlyFleets(planet)
                .Where(candidate => candidate != fleet && candidate.Movement == null)
                .Sum(_context.Assessment.GetFleetCombatValue);
            return remainingStrength >= commitment.RequiredStrength;
        }

        /// <summary>
        /// Returns the cached defense allocation for an owned planet.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The planet's defense commitment.</returns>
        private AIPlanetDefenseCommitment GetDefenseCommitment(Planet planet)
        {
            if (planet == null)
                return AIPlanetDefenseCommitment.None;

            if (_defenseByPlanetId.TryGetValue(planet.InstanceID, out var commitment))
                return commitment;

            bool holdAllLocalFleets =
                _context.Game?.Config != null
                && _context.Assessment.GetFactionPopularSupport(planet)
                    < _context.Game.Config.AI.Garrison.SupportThreshold
                && !_context.Assessment.HasFullShields(planet)
                && _context.Assessment.GetDefensiveSupportRisk(planet) > 1;
            int requiredStrength = _context.Assessment.GetRequiredHeadquartersDefenseStrength(
                planet
            );
            commitment = new AIPlanetDefenseCommitment(holdAllLocalFleets, requiredStrength);
            _defenseByPlanetId[planet.InstanceID] = commitment;
            return commitment;
        }

        private readonly struct AIPlanetDefenseCommitment
        {
            internal static readonly AIPlanetDefenseCommitment None = new AIPlanetDefenseCommitment(
                false,
                0
            );

            internal bool HoldAllLocalFleets { get; }
            internal int RequiredStrength { get; }

            internal AIPlanetDefenseCommitment(bool holdAllLocalFleets, int requiredStrength)
            {
                HoldAllLocalFleets = holdAllLocalFleets;
                RequiredStrength = requiredStrength;
            }
        }
    }
}
