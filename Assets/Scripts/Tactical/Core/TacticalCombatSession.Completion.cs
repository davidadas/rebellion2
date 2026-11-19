using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;

namespace Rebellion.Tactical
{
    public sealed partial class TacticalCombatSession
    {
        /// <summary>
        /// Captures the station, faction fighter roots, and retained behavior helpers for notification sequencing.
        /// </summary>
        /// <returns>The current admission and briefing facts, independent of hull and simulation pause.</returns>
        private TacticalCombatBriefingState CaptureBriefingState()
        {
            int allianceFighters = 0;
            int empireFighters = 0;
            int allianceOutsideAttackRuns = 0;
            foreach (TacticalCombatUnit unit in _units)
            {
                if (!unit.IsFighter || !unit.IsEngaged)
                    continue;
                if (GetSide(unit.Side).FactionIndex == 0)
                {
                    allianceFighters++;
                    if (unit.Status != TacticalCombatUnitStatus.AttackingBattleStation)
                        allianceOutsideAttackRuns++;
                }
                else
                    empireFighters++;
            }
            bool attackRunActive = false;
            bool allianceAttackRunOrdered = false;
            foreach (TacticalCombatGroup group in _groups)
            {
                attackRunActive |=
                    group.ActiveOrder == TacticalCombatOrder.AttackBattleStation
                    && group.AttackRun != null;
                if (GetSide(group.Side).FactionIndex == 0)
                    allianceAttackRunOrdered |=
                        group.Order == TacticalCombatOrder.AttackBattleStation
                        || group.ActiveOrder == TacticalCombatOrder.AttackBattleStation;
            }
            return new TacticalCombatBriefingState(
                IsArriving,
                BattleStation != null,
                BattleStation?.IsOperational == true,
                BattleStation?.HasPlanetaryProtection == true,
                attackRunActive,
                allianceFighters != 0,
                empireFighters != 0,
                allianceAttackRunOrdered,
                allianceFighters,
                empireFighters,
                allianceOutsideAttackRuns
            );
        }

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
            _remoteNotificationEnding?.TryBegin(
                alliance,
                empire,
                BattleStation?.IsOperational == true,
                BattleStation?.HasDeparted == true
            );
            if (
                !_ending.TryBegin(
                    alliance,
                    empire,
                    BattleStation?.IsOperational == true,
                    BattleStation?.HasDeparted == true
                )
            )
                return;
            _completionStarted = true;
            bool attacker =
                first.ActiveCount != 0
                || BattleStation?.Unit.Side == CombatSide.Attacker && BattleStation.IsOperational;
            bool defender =
                second.ActiveCount != 0
                || BattleStation?.Unit.Side == CombatSide.Defender && BattleStation.IsOperational;
            Winner =
                attacker == defender ? CombatSide.Draw
                : attacker ? CombatSide.Attacker
                : CombatSide.Defender;
            _outcomes[0] = GetEndingOutcome(CombatSide.Attacker, attacker);
            _outcomes[1] = GetEndingOutcome(CombatSide.Defender, defender);
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
            foreach (TacticalCombatUnit unit in _units)
            {
                if (unit.Side != side.Side || unit.IsBattleStation)
                    continue;
                if (unit.IsEngaged)
                    active++;
                stillActive |=
                    unit.Status
                        is TacticalCombatUnitStatus.Active
                            or TacticalCombatUnitStatus.PreparingHyperspace
                            or TacticalCombatUnitStatus.EnteringHyperspace
                            or TacticalCombatUnitStatus.AwaitingWithdrawal;
                departed |= unit.Status == TacticalCombatUnitStatus.Withdrawn;
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
        public SpaceCombatSideOutcome GetOutcome(CombatSide side) => _outcomes[(int)side];

        /// <summary>
        /// Captures final losses, component damage, recovery assignments, and actual departures without mutating campaign units.
        /// </summary>
        /// <returns>The completed encounter's unit outcomes.</returns>
        public TacticalCombatOutcome CaptureOutcome()
        {
            if (!IsComplete)
                throw new InvalidOperationException("The tactical encounter has not completed.");
            List<TacticalCombatShipOutcome> ships = new();
            List<TacticalCombatFighterOutcome> fighters = new();
            foreach (TacticalCombatUnit unit in _units)
            {
                bool withdrawn = HasDeparted(unit);
                unit.Combatant.HasRetreated = withdrawn;
                if (!unit.IsFighter)
                {
                    int hull = GetCapitalResultHull(unit);
                    CapitalShipSystemDamage damage = unit.Systems.CaptureDamage(
                        unit.HasPrimaryHyperdrive,
                        unit.Definition?.BackupHyperdrive > 0
                    );
                    unit.Combatant.ApplyCapitalShipState(hull, (int)unit.Systems.Shields, damage);
                    ships.Add(
                        new TacticalCombatShipOutcome
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
                    TacticalCombatUnit carrier = unit.Status
                        is TacticalCombatUnitStatus.Docked
                            or TacticalCombatUnitStatus.Recovered
                        ? GetUnit(unit.CarrierID)
                        : null;
                    int squadronSize = GetFighterResultCount(unit);
                    string carrierID = carrier?.IsFighter == false ? carrier.InstanceID : null;
                    unit.Combatant.ApplyStarfighterState(squadronSize, carrierID);
                    fighters.Add(
                        new TacticalCombatFighterOutcome
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
                IterationsCompleted =
                    _scheduler == null ? _progress : unchecked((int)_scheduler.Progress),
                TerminalFlags = _ending.Flags.ToArray(),
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
        private static int GetCapitalResultHull(TacticalCombatUnit unit)
        {
            float hull = unit.Systems.Hull;
            if (unit.IsBattleStation)
                return TruncateResultCount(hull);
            if (unit.Status == TacticalCombatUnitStatus.Destroyed)
                return 0;
            return TruncateResultCount(hull < 1 ? 1 : hull);
        }

        /// <summary>
        /// Captures squadron strength from the retained participation state and final hull pool.
        /// </summary>
        /// <param name="unit">The fighter squadron whose final tactical state is being captured.</param>
        /// <returns>Zero for a destroyed squadron, or its truncated count with a zero count replaced by one.</returns>
        private static int GetFighterResultCount(TacticalCombatUnit unit)
        {
            if (unit.Status == TacticalCombatUnitStatus.Destroyed)
                return 0;
            int count = TruncateResultCount((double)unit.Systems.Hull / 8);
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
        public bool HasDeparted(TacticalCombatUnit unit)
        {
            TacticalCombatUnit carrier =
                unit.IsFighter
                && unit.Status
                    is TacticalCombatUnitStatus.Docked
                        or TacticalCombatUnitStatus.Recovered
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
        private SpaceCombatSideOutcome GetEndingOutcome(CombatSide side, bool active)
        {
            if (active)
                return SpaceCombatSideOutcome.Active;
            foreach (TacticalCombatUnit unit in _departed)
                if (unit.Side == side)
                    return SpaceCombatSideOutcome.Withdrawn;
            return SpaceCombatSideOutcome.Destroyed;
        }

        /// <summary>
        /// Assigns retained fighters to ordinary carriers in registration order, then to the eligible station.
        /// </summary>
        private void AssignSurvivors()
        {
            _directCommandsSuspended = true;
            foreach (WithdrawalMember fighterMember in _orderedWithdrawalMembers)
            {
                TacticalCombatUnit fighter = fighterMember.Unit;
                if (
                    !fighter.IsFighter
                    || !(
                        fighter.IsActiveDisplayObject
                        || fighter.Status
                            is TacticalCombatUnitStatus.Docked
                                or TacticalCombatUnitStatus.Recovered
                                or TacticalCombatUnitStatus.Withdrawn
                    )
                )
                    continue;
                bool attached = false;
                foreach (WithdrawalMember carrierMember in _orderedWithdrawalMembers)
                {
                    TacticalCombatUnit carrier = carrierMember.Unit;
                    if (
                        !carrier.IsFighter
                        && carrier.Side == fighter.Side
                        && (
                            carrier.IsActiveDisplayObject
                            || carrier.Status
                                is TacticalCombatUnitStatus.EnteringHyperspace
                                    or TacticalCombatUnitStatus.AwaitingWithdrawal
                                    or TacticalCombatUnitStatus.Withdrawn
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
