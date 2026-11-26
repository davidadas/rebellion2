using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Tactical;

namespace Rebellion.Gameplay
{
    public partial class SpaceCombatCommands
    {
        /// <summary>
        /// Validates a detached outcome completely before resolving any local campaign references for import.
        /// </summary>
        /// <param name="outcome">The host-issued final pools and participation state.</param>
        private void ValidateTacticalOutcome(TacticalCombatOutcome outcome)
        {
            if (
                outcome == null
                || !Enum.IsDefined(typeof(CombatSide), outcome.Winner)
                || !Enum.IsDefined(typeof(SpaceCombatSideOutcome), outcome.AttackerOutcome)
                || !Enum.IsDefined(typeof(SpaceCombatSideOutcome), outcome.DefenderOutcome)
                || outcome.TerminalFlags?.Length != TacticalCombatOutcome.TerminalFlagCount
                || outcome.TerminalFlags[6]
                || outcome.Ships == null
                || outcome.Fighters == null
            )
                throw new InvalidDataException("The tactical outcome is incomplete.");

            Dictionary<string, CombatUnit> units = _tacticalCombat.Units.ToDictionary(
                unit => unit.InstanceID,
                StringComparer.Ordinal
            );
            if (
                outcome.Ships.Length != units.Values.Count(unit => unit.Unit is CapitalShip)
                || outcome.Fighters.Length != units.Values.Count(unit => unit.Unit is Starfighter)
            )
                throw new InvalidDataException(
                    "The tactical outcome does not cover the captured roster."
                );

            HashSet<string> imported = new(StringComparer.Ordinal);
            foreach (TacticalCombatOutcome.ShipOutcome shipOutcome in outcome.Ships)
            {
                if (
                    shipOutcome == null
                    || string.IsNullOrEmpty(shipOutcome.UnitID)
                    || !units.TryGetValue(shipOutcome.UnitID, out CombatUnit unit)
                    || ResolveTacticalSource(unit) is not CapitalShip ship
                    || !imported.Add(shipOutcome.UnitID)
                    || shipOutcome.Hull < 0
                    || shipOutcome.Hull > Math.Max(1, (int)unit.Systems.MaximumHull)
                    || !IsValidTacticalComponentDamage(shipOutcome.SystemDamage, unit, ship)
                )
                    throw new InvalidDataException("A tactical ship outcome is invalid.");
            }

            foreach (TacticalCombatOutcome.FighterOutcome fighterOutcome in outcome.Fighters)
            {
                if (
                    fighterOutcome == null
                    || string.IsNullOrEmpty(fighterOutcome.UnitID)
                    || !units.TryGetValue(fighterOutcome.UnitID, out CombatUnit unit)
                    || ResolveTacticalSource(unit) is not Starfighter fighter
                    || !imported.Add(fighterOutcome.UnitID)
                    || fighterOutcome.SquadronSize < 0
                    || fighterOutcome.SquadronSize > Math.Max(1, fighter.MaxSquadronSize)
                )
                    throw new InvalidDataException("A tactical fighter outcome is invalid.");
                if (fighterOutcome.CarrierID != null)
                {
                    if (
                        !units.TryGetValue(fighterOutcome.CarrierID, out CombatUnit host)
                        || ResolveTacticalSource(host) is not CapitalShip
                        || host.Side != unit.Side
                    )
                        throw new InvalidDataException("A tactical recovery host is invalid.");
                }
            }
        }

        /// <summary>
        /// Admits only component levels and installed hyperdrives represented by the captured ship.
        /// </summary>
        /// <param name="damage">The authoritative component damage.</param>
        /// <param name="unit">The captured tactical participant.</param>
        /// <param name="ship">Its immutable campaign source during the encounter.</param>
        /// <returns>Whether all component fields are within their supported ranges.</returns>
        private static bool IsValidTacticalComponentDamage(
            CapitalShipSystemDamage damage,
            CombatUnit unit,
            CapitalShip ship
        ) =>
            damage?.Shields is >= 0 and <= 4
            && damage.Weapons is >= 0 and <= 4
            && damage.TractorBeam is >= 0 and <= 4
            && damage.Engines is >= 0 and <= 4
            && damage.PrimaryHyperdrive >= 0
            && damage.PrimaryHyperdrive <= (ship.Hyperdrive != 0 ? 1 : 0)
            && damage.BackupHyperdrive >= 0
            && damage.BackupHyperdrive <= (unit.Definition?.BackupHyperdrive > 0 ? 1 : 0);
    }
}
