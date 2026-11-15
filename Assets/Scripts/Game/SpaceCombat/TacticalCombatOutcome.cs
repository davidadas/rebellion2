using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.SpaceCombat
{
    /// <summary>
    /// Transfers a completed authoritative encounter using the identities captured at battle entry.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCombatOutcome
    {
        public const int TerminalFlagCount = 7;

        public CombatSide Winner { get; set; }
        public SpaceCombatSideOutcome AttackerOutcome { get; set; }
        public SpaceCombatSideOutcome DefenderOutcome { get; set; }
        public int IterationsCompleted { get; set; }
        public bool[] TerminalFlags { get; set; }
        public TacticalCombatShipOutcome[] Ships { get; set; }
        public TacticalCombatFighterOutcome[] Fighters { get; set; }

        /// <summary>
        /// Captures final tactical results while retaining no mutable references to campaign units.
        /// </summary>
        /// <param name="result">The completed tactical encounter's uncommitted result.</param>
        /// <param name="terminalFlags">The complete encounter-ending flags.</param>
        /// <returns>The detached authoritative outcome.</returns>
        public static TacticalCombatOutcome Capture(
            SpaceCombatResult result,
            IReadOnlyList<bool> terminalFlags
        )
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (terminalFlags == null || terminalFlags.Count != TerminalFlagCount)
                throw new ArgumentException(
                    "A complete tactical ending state is required.",
                    nameof(terminalFlags)
                );

            HashSet<string> withdrawn = result
                .WithdrawnUnits.Select(unit => unit.InstanceID)
                .ToHashSet(StringComparer.Ordinal);
            Dictionary<string, string> carriers = result.FighterRecovery.ToDictionary(
                recovery => recovery.Fighter.InstanceID,
                recovery => recovery.Carrier?.InstanceID,
                StringComparer.Ordinal
            );
            TacticalCombatShipOutcome[] ships = result
                .ShipDamage.Select(damage =>
                {
                    if (damage.Ship == null || damage.SystemDamage == null)
                        throw new ArgumentException(
                            "Every tactical ship requires its captured identity and component state.",
                            nameof(result)
                        );
                    return new TacticalCombatShipOutcome
                    {
                        UnitID = damage.Ship.InstanceID,
                        Hull = damage.HullAfter,
                        SystemDamage = damage.SystemDamage.CreateCopy(),
                        Withdrawn = withdrawn.Contains(damage.Ship.InstanceID),
                    };
                })
                .ToArray();
            TacticalCombatFighterOutcome[] fighters = result
                .FighterLosses.Select(loss =>
                {
                    if (
                        loss.Fighter == null
                        || !carriers.TryGetValue(loss.Fighter.InstanceID, out string carrierID)
                    )
                        throw new ArgumentException(
                            "Every tactical fighter requires its captured identity and recovery state.",
                            nameof(result)
                        );
                    return new TacticalCombatFighterOutcome
                    {
                        UnitID = loss.Fighter.InstanceID,
                        SquadronSize = loss.SquadsAfter,
                        CarrierID = carrierID,
                        Withdrawn = withdrawn.Contains(loss.Fighter.InstanceID),
                    };
                })
                .ToArray();
            return new TacticalCombatOutcome
            {
                Winner = result.Winner,
                AttackerOutcome = result.AttackerOutcome,
                DefenderOutcome = result.DefenderOutcome,
                IterationsCompleted = result.IterationsCompleted,
                TerminalFlags = terminalFlags.ToArray(),
                Ships = ships,
                Fighters = fighters,
            };
        }
    }
}
