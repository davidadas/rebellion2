using System.Collections.Generic;
using Rebellion.Game.Units;

namespace Rebellion.Game.Results
{
    /// <summary>
    /// Contains the unapplied result of automatic space combat.
    /// </summary>
    internal sealed class SpaceCombatAutoResult
    {
        internal SpaceCombatSideOutcome AttackerOutcome { get; }
        internal SpaceCombatSideOutcome DefenderOutcome { get; }
        internal int IterationsCompleted { get; }
        internal IReadOnlyList<SpaceCombatAutoShipOutcome> Ships { get; }
        internal IReadOnlyList<SpaceCombatAutoFighterOutcome> Fighters { get; }

        /// <summary>
        /// Creates an automatic space-combat result.
        /// </summary>
        /// <param name="attackerOutcome">The attacker's final state.</param>
        /// <param name="defenderOutcome">The defender's final state.</param>
        /// <param name="iterationsCompleted">The number of resolution iterations.</param>
        /// <param name="ships">The resolved capital ships.</param>
        /// <param name="fighters">The resolved fighter squadrons.</param>
        internal SpaceCombatAutoResult(
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
    internal sealed class SpaceCombatAutoShipOutcome
    {
        internal CapitalShip Ship { get; }
        internal int HullBefore { get; }
        internal double HullAfter { get; }

        /// <summary>
        /// Creates a resolved capital-ship outcome.
        /// </summary>
        /// <param name="ship">The resolved capital ship.</param>
        /// <param name="hullBefore">The ship's initial hull strength.</param>
        /// <param name="hullAfter">The ship's final hull strength.</param>
        internal SpaceCombatAutoShipOutcome(CapitalShip ship, int hullBefore, double hullAfter)
        {
            Ship = ship;
            HullBefore = hullBefore;
            HullAfter = hullAfter;
        }
    }

    /// <summary>
    /// Contains one fighter squadron's resolved tactical strength.
    /// </summary>
    internal sealed class SpaceCombatAutoFighterOutcome
    {
        internal Starfighter Fighter { get; }
        internal int SquadronSizeBefore { get; }
        internal int SquadronSizeAfter { get; }

        /// <summary>
        /// Creates a resolved fighter-squadron outcome.
        /// </summary>
        /// <param name="fighter">The resolved fighter squadron.</param>
        /// <param name="squadronSizeBefore">The initial fighter count.</param>
        /// <param name="squadronSizeAfter">The final fighter count.</param>
        internal SpaceCombatAutoFighterOutcome(
            Starfighter fighter,
            int squadronSizeBefore,
            int squadronSizeAfter
        )
        {
            Fighter = fighter;
            SquadronSizeBefore = squadronSizeBefore;
            SquadronSizeAfter = squadronSizeAfter;
        }
    }
}
