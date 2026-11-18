using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        /// <returns>The final result using only this encounter's captured campaign objects.</returns>
        private SpaceCombatResult RestoreTacticalOutcome(TacticalCombatOutcome outcome)
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

            Dictionary<string, TacticalCombatUnit> units = _tacticalCombat.Units.ToDictionary(
                unit => unit.InstanceID,
                StringComparer.Ordinal
            );
            if (
                outcome.Ships.Length
                    != units.Values.Count(unit => _tacticalSources[unit.InstanceID] is CapitalShip)
                || outcome.Fighters.Length
                    != units.Values.Count(unit => _tacticalSources[unit.InstanceID] is Starfighter)
            )
                throw new InvalidDataException(
                    "The tactical outcome does not cover the captured roster."
                );

            SpaceCombatResult result = new()
            {
                Winner = outcome.Winner,
                AttackerOutcome = outcome.AttackerOutcome,
                DefenderOutcome = outcome.DefenderOutcome,
                IterationsCompleted = outcome.IterationsCompleted,
            };
            HashSet<string> imported = new(StringComparer.Ordinal);
            foreach (TacticalCombatShipOutcome shipOutcome in outcome.Ships)
            {
                if (
                    shipOutcome == null
                    || string.IsNullOrEmpty(shipOutcome.UnitID)
                    || !units.TryGetValue(shipOutcome.UnitID, out TacticalCombatUnit unit)
                    || _tacticalSources[unit.InstanceID] is not CapitalShip ship
                    || !imported.Add(shipOutcome.UnitID)
                    || shipOutcome.Hull < 0
                    || shipOutcome.Hull > Math.Max(1, (int)unit.Systems.MaximumHull)
                    || !IsValidTacticalComponentDamage(shipOutcome.SystemDamage, unit, ship)
                )
                    throw new InvalidDataException("A tactical ship outcome is invalid.");
                result.ShipDamage.Add(
                    new ShipDamageResult
                    {
                        Ship = ship,
                        HullBefore = (int)unit.InitialHull,
                        HullAfter = shipOutcome.Hull,
                        SystemDamage = shipOutcome.SystemDamage.CreateCopy(),
                    }
                );
                if (shipOutcome.Withdrawn)
                    result.WithdrawnUnits.Add(ship);
            }

            foreach (TacticalCombatFighterOutcome fighterOutcome in outcome.Fighters)
            {
                if (
                    fighterOutcome == null
                    || string.IsNullOrEmpty(fighterOutcome.UnitID)
                    || !units.TryGetValue(fighterOutcome.UnitID, out TacticalCombatUnit unit)
                    || _tacticalSources[unit.InstanceID] is not Starfighter fighter
                    || !imported.Add(fighterOutcome.UnitID)
                    || fighterOutcome.SquadronSize < 0
                    || fighterOutcome.SquadronSize > Math.Max(1, fighter.MaxSquadronSize)
                )
                    throw new InvalidDataException("A tactical fighter outcome is invalid.");
                CapitalShip carrier = null;
                if (fighterOutcome.CarrierID != null)
                {
                    if (
                        !units.TryGetValue(fighterOutcome.CarrierID, out TacticalCombatUnit host)
                        || _tacticalSources[host.InstanceID] is not CapitalShip ship
                        || host.Side != unit.Side
                    )
                        throw new InvalidDataException("A tactical recovery host is invalid.");
                    carrier = ship;
                }
                result.FighterLosses.Add(
                    new FighterLossResult
                    {
                        Fighter = fighter,
                        SquadsBefore = unit.InitialSquadronSize,
                        SquadsAfter = fighterOutcome.SquadronSize,
                    }
                );
                result.FighterRecovery.Add(
                    new FighterRecoveryResult { Fighter = fighter, Carrier = carrier }
                );
                if (fighterOutcome.Withdrawn)
                    result.WithdrawnUnits.Add(fighter);
            }
            return result;
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
            TacticalCombatUnit unit,
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
