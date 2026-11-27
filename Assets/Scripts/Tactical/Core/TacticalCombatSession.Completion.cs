using System;
using System.Collections.Generic;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Tactical
{
    public sealed partial class TacticalCombatSession
    {
        /// <summary>
        /// Starts the terminal assessment once active movement and object counting find an inactive faction.
        /// </summary>
        private void CheckCompletion()
        {
            if (_completionStarted)
                return;
            TacticalCombatForceAssessment first = CaptureForceAssessment(_sides[0]);
            TacticalCombatForceAssessment second = CaptureForceAssessment(_sides[1]);
            TacticalCombatForceAssessment alliance = _sides[0].FactionIndex == 0 ? first : second;
            TacticalCombatForceAssessment empire = _sides[0].FactionIndex == 1 ? first : second;
            if (
                !_ending.TryBegin(
                    alliance,
                    empire,
                    BattleStation?.IsOperational == true,
                    BattleStation?.HasDeparted == true
                )
            )
                return;
            AnnounceEnding(PlayerSide);
            AssignSurvivors();
            _completionStarted = true;
            bool attacker =
                first.ActiveCount != 0
                || BattleStation?.Unit.Side == CombatSide.Attacker && BattleStation.IsOperational;
            bool defender =
                second.ActiveCount != 0
                || BattleStation?.Unit.Side == CombatSide.Defender && BattleStation.IsOperational;
            Winner =
                attacker == defender ? SpaceCombatResult.BattleOutcome.Draw
                : attacker ? SpaceCombatResult.BattleOutcome.Attacker
                : SpaceCombatResult.BattleOutcome.Defender;
            _outcomes[0] = GetEndingOutcome(CombatSide.Attacker, attacker);
            _outcomes[1] = GetEndingOutcome(CombatSide.Defender, defender);
        }

        /// <summary>
        /// Requests the terminal cue for the indicated faction.
        /// </summary>
        /// <param name="recipient">The faction side receiving the terminal notification.</param>
        private void AnnounceEnding(CombatSide recipient)
        {
            int? message = _ending.GetTerminalNotification(GetSide(recipient).FactionIndex);
            if (message.HasValue)
                EnqueueFactionNotification(recipient, message.Value);
        }

        /// <summary>
        /// Counts active-root members separately from ordinary roster participation and completed departures.
        /// </summary>
        /// <param name="side">The faction whose withdrawal and object facts are captured.</param>
        /// <returns>The immutable terminal assessment.</returns>
        private TacticalCombatForceAssessment CaptureForceAssessment(TacticalCombatSide side)
        {
            int active = 0;
            bool stillActive = false;
            bool departed = false;
            foreach (CombatUnit unit in _units)
            {
                if (unit.Side != side.Side || unit.IsBattleStation)
                    continue;
                if (unit.IsEngaged)
                    active++;
                stillActive |=
                    unit.Status
                        is CombatUnitStatus.Active
                            or CombatUnitStatus.PreparingHyperspace
                            or CombatUnitStatus.EnteringHyperspace
                            or CombatUnitStatus.AwaitingWithdrawal;
                departed |= unit.Status == CombatUnitStatus.Withdrawn;
            }
            int withdrawalState = side.WithdrawalPhase.HasValue
                ? (int)side.WithdrawalPhase.Value
                : 0;
            return new TacticalCombatForceAssessment(
                active,
                withdrawalState,
                stillActive,
                departed
            );
        }

        /// <summary>
        /// Gets the resolved side outcome captured when active combat ended.
        /// </summary>
        /// <param name="side">The requested encounter side.</param>
        /// <returns>The side's resolved participation outcome.</returns>
        public SpaceCombatResult.SideOutcome GetOutcome(CombatSide side) => _outcomes[(int)side];

        /// <summary>
        /// Captures final losses, component damage, recovery assignments, and actual departures without mutating campaign units.
        /// </summary>
        /// <returns>The completed encounter's unit outcomes.</returns>
        public TacticalCombatOutcome CaptureOutcome()
        {
            if (!IsComplete)
                throw new InvalidOperationException("The tactical encounter has not completed.");
            List<TacticalCombatOutcome.ShipOutcome> ships = new();
            List<TacticalCombatOutcome.FighterOutcome> fighters = new();
            foreach (CombatUnit unit in _units)
            {
                bool withdrawn = HasDeparted(unit);
                if (!unit.IsFighter)
                {
                    int hull = GetCapitalResultHull(unit);
                    CapitalShipSystemDamage damage = unit.CaptureDamage();
                    ships.Add(
                        new TacticalCombatOutcome.ShipOutcome
                        {
                            UnitID = unit.InstanceID,
                            Hull = hull,
                            SystemDamage = damage,
                            Withdrawn = withdrawn,
                        }
                    );
                }
                else
                {
                    CombatUnit carrier = unit.Status
                        is CombatUnitStatus.Docked
                            or CombatUnitStatus.Recovered
                        ? GetUnit(unit.CarrierID)
                        : null;
                    int squadronSize = GetFighterResultCount(unit);
                    string carrierID = carrier?.IsFighter == false ? carrier.InstanceID : null;
                    fighters.Add(
                        new TacticalCombatOutcome.FighterOutcome
                        {
                            UnitID = unit.InstanceID,
                            SquadronSize = squadronSize,
                            CarrierID = carrierID,
                            Withdrawn = withdrawn,
                        }
                    );
                }
            }
            return new TacticalCombatOutcome
            {
                Winner = Winner,
                AttackerOutcome = _outcomes[0],
                DefenderOutcome = _outcomes[1],
                IterationsCompleted = unchecked((int)(_scheduler?.Progress ?? 0)),
                Ships = ships.ToArray(),
                Fighters = fighters.ToArray(),
            };
        }

        /// <summary>
        /// Captures hull for campaign import, retaining at least one point for a non-destroyed ordinary ship.
        /// Battle stations export their separate stored hull pool directly.
        /// </summary>
        /// <param name="unit">The capital ship whose final tactical state is being captured.</param>
        /// <returns>The truncated hull count carried into the campaign result.</returns>
        private static int GetCapitalResultHull(CombatUnit unit)
        {
            float hull = unit.Hull;
            if (unit.IsBattleStation)
                return TruncateResultCount(hull);
            if (unit.Status == CombatUnitStatus.Destroyed)
                return 0;
            return TruncateResultCount(hull < 1 ? 1 : hull);
        }

        /// <summary>
        /// Captures squadron strength from the retained participation state and final hull pool.
        /// </summary>
        /// <param name="unit">The fighter squadron whose final tactical state is being captured.</param>
        /// <returns>Zero for a destroyed squadron, or its truncated count with a zero count replaced by one.</returns>
        private static int GetFighterResultCount(CombatUnit unit)
        {
            if (unit.Status == CombatUnitStatus.Destroyed)
                return 0;
            int count = TruncateResultCount((double)unit.Hull / 8);
            return count == 0 ? 1 : count;
        }

        /// <summary>
        /// Truncates a final pool through a signed 64-bit count before retaining its low 32 bits.
        /// Nonfinite and out-of-range values carry a zero low word.
        /// </summary>
        /// <param name="value">The final hull or squadron count before integer conversion.</param>
        /// <returns>The low 32 bits of the truncated count.</returns>
        private static int TruncateResultCount(double value)
        {
            if (
                double.IsNaN(value)
                || value < -9223372036854775808d
                || value >= 9223372036854775808d
            )
                return 0;
            return unchecked((int)(long)value);
        }

        /// <summary>
        /// Checks whether a unit or its recovered carrier completed departure.
        /// </summary>
        /// <param name="unit">The surviving encounter unit.</param>
        /// <returns>Whether its final campaign location must follow a departure route.</returns>
        public bool HasDeparted(CombatUnit unit)
        {
            CombatUnit carrier =
                unit.IsFighter
                && unit.Status is CombatUnitStatus.Docked or CombatUnitStatus.Recovered
                    ? GetUnit(unit.CarrierID)
                    : null;
            return _departed.Contains(carrier ?? unit);
        }

        /// <summary>
        /// Captures whether an inactive side departed or lost all active forces.
        /// </summary>
        /// <param name="side">The resolved side.</param>
        /// <param name="active">Whether active units remain.</param>
        /// <returns>The resolved side outcome.</returns>
        private SpaceCombatResult.SideOutcome GetEndingOutcome(CombatSide side, bool active)
        {
            if (active)
                return SpaceCombatResult.SideOutcome.Active;
            foreach (CombatUnit unit in _departed)
                if (unit.Side == side)
                    return SpaceCombatResult.SideOutcome.Withdrawn;
            return SpaceCombatResult.SideOutcome.Destroyed;
        }

        /// <summary>
        /// Assigns retained fighters to ordinary carriers in registration order, then to the eligible station.
        /// </summary>
        private void AssignSurvivors()
        {
            _directCommandsSuspended = true;
            foreach (CombatUnit fighter in _registeredMobileUnits)
            {
                if (
                    !fighter.IsFighter
                    || !(
                        fighter.IsActiveDisplayObject
                        || fighter.Status
                            is CombatUnitStatus.Docked
                                or CombatUnitStatus.Recovered
                                or CombatUnitStatus.Withdrawn
                    )
                )
                    continue;
                bool attached = false;
                foreach (CombatUnit carrier in _registeredMobileUnits)
                {
                    if (
                        !carrier.IsFighter
                        && carrier.Side == fighter.Side
                        && (
                            carrier.IsActiveDisplayObject
                            || carrier.Status
                                is CombatUnitStatus.EnteringHyperspace
                                    or CombatUnitStatus.AwaitingWithdrawal
                                    or CombatUnitStatus.Withdrawn
                        )
                        && _carriers[carrier.InstanceID].AttachSurvivor(fighter)
                    )
                    {
                        attached = true;
                        break;
                    }
                }
                if (
                    !attached
                    && GetSide(fighter.Side).FactionIndex == 1
                    && BattleStation != null
                    && (BattleStation.IsOperational || BattleStation.HasDeparted)
                )
                    _carriers[BattleStation.Unit.InstanceID].AttachSurvivor(fighter);
            }
        }

        /// <summary>
        /// Exposes the final result after the end-of-combat presentation delay.
        /// </summary>
        private void Complete()
        {
            if (IsComplete)
                return;
            IsComplete = true;
            Completed?.Invoke();
        }
    }
}
