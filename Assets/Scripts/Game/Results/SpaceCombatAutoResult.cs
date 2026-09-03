using System.Collections.Generic;
using Rebellion.Game.Units;

namespace Rebellion.Game.Results
{
    /// <summary>
    /// Contains the unapplied result of automatic space combat.
    /// </summary>
    public sealed class SpaceCombatAutoResult
    {
        public SpaceCombatSideOutcome AttackerOutcome { get; }
        public SpaceCombatSideOutcome DefenderOutcome { get; }
        public int IterationsCompleted { get; }
        public IReadOnlyList<SpaceCombatAutoShipOutcome> Ships { get; }
        public IReadOnlyList<SpaceCombatAutoFighterOutcome> Fighters { get; }

        /// <summary>
        /// Creates an automatic space-combat result.
        /// </summary>
        /// <param name="attackerOutcome">The attacker's final state.</param>
        /// <param name="defenderOutcome">The defender's final state.</param>
        /// <param name="iterationsCompleted">The number of resolution iterations.</param>
        /// <param name="ships">The resolved capital ships.</param>
        /// <param name="fighters">The resolved fighter squadrons.</param>
        public SpaceCombatAutoResult(
            SpaceCombatSideOutcome attackerOutcome,
            SpaceCombatSideOutcome defenderOutcome,
            int iterationsCompleted,
            IReadOnlyList<SpaceCombatAutoShipOutcome> ships,
            IReadOnlyList<SpaceCombatAutoFighterOutcome> fighters
        )
        {
            AttackerOutcome = attackerOutcome;
            DefenderOutcome = defenderOutcome;
            IterationsCompleted = iterationsCompleted;
            Ships = ships;
            Fighters = fighters;
        }
    }

    /// <summary>
    /// Contains one capital ship's resolved tactical durability.
    /// </summary>
    public sealed class SpaceCombatAutoShipOutcome
    {
        public CapitalShip Ship { get; }
        public int HullBefore { get; }
        public double HullAfter { get; }
        public bool Withdrew { get; }

        /// <summary>
        /// Creates a resolved capital-ship outcome.
        /// </summary>
        /// <param name="ship">The resolved capital ship.</param>
        /// <param name="hullBefore">The ship's initial hull strength.</param>
        /// <param name="hullAfter">The ship's final hull strength.</param>
        /// <param name="withdrew">Whether the ship escaped from combat.</param>
        public SpaceCombatAutoShipOutcome(
            CapitalShip ship,
            int hullBefore,
            double hullAfter,
            bool withdrew
        )
        {
            Ship = ship;
            HullBefore = hullBefore;
            HullAfter = hullAfter;
            Withdrew = withdrew;
        }
    }

    /// <summary>
    /// Contains one fighter squadron's resolved tactical strength.
    /// </summary>
    public sealed class SpaceCombatAutoFighterOutcome
    {
        public Starfighter Fighter { get; }
        public int SquadronSizeBefore { get; }
        public int SquadronSizeAfter { get; }
        public bool Withdrew { get; }

        /// <summary>
        /// Creates a resolved fighter-squadron outcome.
        /// </summary>
        /// <param name="fighter">The resolved fighter squadron.</param>
        /// <param name="squadronSizeBefore">The initial fighter count.</param>
        /// <param name="squadronSizeAfter">The final fighter count.</param>
        /// <param name="withdrew">Whether the squadron escaped from combat.</param>
        public SpaceCombatAutoFighterOutcome(
            Starfighter fighter,
            int squadronSizeBefore,
            int squadronSizeAfter,
            bool withdrew
        )
        {
            Fighter = fighter;
            SquadronSizeBefore = squadronSizeBefore;
            SquadronSizeAfter = squadronSizeAfter;
            Withdrew = withdrew;
        }
    }
}
