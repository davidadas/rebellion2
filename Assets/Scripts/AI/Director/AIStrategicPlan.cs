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
            if (commitment.HoldAllLocalFleets || commitment.ReservedFleetId == fleet?.InstanceID)
                return false;

            if (commitment.RequiredStrength <= 0)
                return true;

            int remainingStrength = _context
                .Assessment.GetFriendlyFleets(planet)
                .Where(candidate => candidate != fleet && candidate.Movement == null)
                .Select(_context.Assessment.GetFleetCombatValue)
                .DefaultIfEmpty()
                .Max();
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
            string reservedFleetId =
                requiredStrength > 0 ? ChooseReservedFleetId(planet) : string.Empty;
            commitment = new AIPlanetDefenseCommitment(
                holdAllLocalFleets,
                requiredStrength,
                reservedFleetId
            );
            _defenseByPlanetId[planet.InstanceID] = commitment;
            return commitment;
        }

        /// <summary>
        /// Chooses one stable local fleet to satisfy the planet's defense commitment.
        /// </summary>
        /// <param name="planet">The defended planet.</param>
        /// <returns>The reserved fleet identifier, or an empty string.</returns>
        private string ChooseReservedFleetId(Planet planet)
        {
            Fleet reservedFleet = null;
            bool reservedHasDefenseOrder = false;
            int reservedStrength = int.MinValue;
            foreach (Fleet candidate in _context.Assessment.GetFriendlyFleets(planet))
            {
                if (
                    candidate.Movement != null
                    || candidate.IsInCombat
                    || !candidate.HasOperationalCapitalShips()
                )
                    continue;

                bool hasDefenseOrder =
                    candidate.Order?.OrderType == FleetOrderType.Defend
                    && candidate.Order.TargetPlanetId == planet.InstanceID;
                int strength = _context.Assessment.GetFleetCombatValue(candidate);
                if (
                    reservedFleet != null
                    && (!hasDefenseOrder || reservedHasDefenseOrder)
                    && (hasDefenseOrder != reservedHasDefenseOrder || strength < reservedStrength)
                )
                    continue;
                if (
                    reservedFleet != null
                    && hasDefenseOrder == reservedHasDefenseOrder
                    && strength == reservedStrength
                    && string.CompareOrdinal(candidate.InstanceID, reservedFleet.InstanceID) >= 0
                )
                    continue;

                reservedFleet = candidate;
                reservedHasDefenseOrder = hasDefenseOrder;
                reservedStrength = strength;
            }

            return reservedFleet?.InstanceID ?? string.Empty;
        }

        private readonly struct AIPlanetDefenseCommitment
        {
            internal static readonly AIPlanetDefenseCommitment None = new AIPlanetDefenseCommitment(
                false,
                0,
                string.Empty
            );

            internal bool HoldAllLocalFleets { get; }
            internal int RequiredStrength { get; }
            internal string ReservedFleetId { get; }

            internal AIPlanetDefenseCommitment(
                bool holdAllLocalFleets,
                int requiredStrength,
                string reservedFleetId
            )
            {
                HoldAllLocalFleets = holdAllLocalFleets;
                RequiredStrength = requiredStrength;
                ReservedFleetId = reservedFleetId;
            }
        }
    }
}
