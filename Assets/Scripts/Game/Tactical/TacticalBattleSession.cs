using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Owns the isolated tactical state for one pending strategic encounter.
    /// </summary>
    public sealed class TacticalBattleSession
    {
        private readonly ReadOnlyCollection<TacticalUnitState> units;

        /// <summary>
        /// Gets the pending strategic encounter represented by this session.
        /// </summary>
        public PendingCombatResult Encounter { get; }

        /// <summary>
        /// Gets every tactical unit participating in the encounter.
        /// </summary>
        public IReadOnlyList<TacticalUnitState> Units => units;

        private TacticalBattleSession(PendingCombatResult encounter, IList<TacticalUnitState> units)
        {
            Encounter = encounter;
            this.units = new ReadOnlyCollection<TacticalUnitState>(units);
        }

        /// <summary>
        /// Creates an isolated tactical session from a pending fleet encounter.
        /// </summary>
        /// <param name="encounter">The pending strategic encounter.</param>
        /// <returns>The initialized tactical session.</returns>
        public static TacticalBattleSession Create(PendingCombatResult encounter)
        {
            if (encounter == null)
                throw new ArgumentNullException(nameof(encounter));
            if (encounter.AttackerFleet == null)
                throw new ArgumentException("Attacking fleet is required.", nameof(encounter));
            if (encounter.DefenderFleet == null)
                throw new ArgumentException("Defending fleet is required.", nameof(encounter));

            List<TacticalUnitState> units = new List<TacticalUnitState>();
            AddFleet(units, encounter.AttackerFleet, TacticalBattleSide.Attacker);
            AddFleet(units, encounter.DefenderFleet, TacticalBattleSide.Defender);
            return new TacticalBattleSession(encounter, units);
        }

        /// <summary>
        /// Adds the operational ships and embarked fighter squadrons from one fleet.
        /// </summary>
        /// <param name="units">The tactical unit collection.</param>
        /// <param name="fleet">The fleet to enumerate.</param>
        /// <param name="side">The fleet's tactical side.</param>
        private static void AddFleet(
            ICollection<TacticalUnitState> units,
            Fleet fleet,
            TacticalBattleSide side
        )
        {
            foreach (CapitalShip ship in fleet.CapitalShips)
            {
                if (
                    ship == null
                    || ship.ManufacturingStatus != ManufacturingStatus.Complete
                    || ship.Movement != null
                    || ship.CurrentHullStrength <= 0
                )
                    continue;

                units.Add(TacticalUnitState.FromCapitalShip(ship, side));
                foreach (Starfighter fighters in ship.Starfighters)
                {
                    if (
                        fighters == null
                        || fighters.ManufacturingStatus != ManufacturingStatus.Complete
                        || fighters.Movement != null
                        || fighters.CurrentSquadronSize <= 0
                    )
                        continue;

                    units.Add(TacticalUnitState.FromFighters(fighters, side));
                }
            }
        }
    }
}
