using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Owns ordered target and force assessments shared by explicit orders and automated fleet policy.
    /// </summary>
    internal sealed class TacticalCombatTargeting
    {
        private static readonly Comparer<TargetCandidate> _candidateComparer =
            Comparer<TargetCandidate>.Create(CompareCandidates);
        private readonly TacticalCombatSession _session;
        private readonly CandidateSet[] _candidateSets = { new(), new() };
        private CombatSide _candidateSide;
        private List<TargetCandidate> FallbackCandidates =>
            _candidateSets[(int)_candidateSide].Fallback;
        private readonly List<CombatUnit> _assessedUnits = new();
        private readonly List<TacticalCombatGroup> _assessedGroups = new();
        private readonly Dictionary<CombatUnit, List<TacticalCombatGroup>> _targetingGroups = new();

        /// <summary>
        /// Indexes the captured battle participants once for subsequent side assessments.
        /// </summary>
        /// <param name="session">The isolated tactical encounter.</param>
        public TacticalCombatTargeting(TacticalCombatSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            foreach (CombatUnit unit in session.Units)
            {
                _targetingGroups.Add(unit, new List<TacticalCombatGroup>());
                if (!unit.IsBattleStation)
                    _assessedUnits.Add(unit);
            }
            _assessedUnits.Sort(CompareUnits);
        }

        /// <summary>
        /// Reads the prepared order without materializing another candidate collection.
        /// </summary>
        /// <param name="side">The assessing side.</param>
        /// <returns>The current retained candidate order.</returns>
        internal IReadOnlyList<TargetCandidate> GetCandidates(CombatSide side) =>
            _candidateSets[(int)side].Ordered;

        /// <summary>
        /// Reads the prepared capital category used by automated battle-station targeting.
        /// </summary>
        /// <param name="side">The assessing side.</param>
        /// <returns>The retained capital candidate order.</returns>
        internal IReadOnlyList<TargetCandidate> GetCapitalCandidates(CombatSide side) =>
            _candidateSets[(int)side].Capital;

        /// <summary>
        /// Retains policy assignment order for subsequent explicit attack behaviors.
        /// </summary>
        /// <param name="side">The assessing side.</param>
        /// <param name="order">The completed assignment order.</param>
        internal void SetCandidateOrder(CombatSide side, IReadOnlyList<TargetCandidate> order)
        {
            CandidateSet candidates = _candidateSets[(int)side];
            candidates.Ordered.Clear();
            candidates.Ordered.AddRange(order);
            RefreshCandidateCategories(candidates);
        }

        /// <summary>
        /// Refreshes the side's ordered target assessment before station and withdrawal maintenance.
        /// </summary>
        /// <param name="side">The side whose current candidates are prepared.</param>
        public void PrepareCandidates(TacticalCombatSide side)
        {
            _candidateSide = side.Side;
            BuildCandidates(side);
        }

        /// <summary>
        /// Calculates a group's target-elimination metric while adding its attack to a shared concentration budget.
        /// </summary>
        /// <param name="group">The attacking group.</param>
        /// <param name="target">The candidate target.</param>
        /// <param name="attack">The shared attack accumulator.</param>
        /// <returns>The elimination metric, or minus one when recharge exceeds available attack.</returns>
        public static float GetTargetMetric(
            TacticalCombatGroup group,
            CombatUnit target,
            ref float attack
        )
        {
            attack = (float)((double)attack + group.GetAttackRate(target));
            float weightedHull = (float)((double)target.Hull * 0.65f);
            float durability = (float)((double)weightedHull + target.Shields);
            if (durability < 1)
                durability = 1;
            float recharge = (float)target.GetShieldRecharge();
            float remaining = (float)((double)attack - recharge);
            if (!(remaining > 0))
                return -1;
            float metric = (float)((double)durability / remaining);
            Vector3 delta = target.Position - group.Formation.Position;
            if (
                (delta.X == 0 || float.IsNaN(delta.X))
                && (delta.Y == 0 || float.IsNaN(delta.Y))
                && (delta.Z == 0 || float.IsNaN(delta.Z))
            )
                return metric;
            float speed = group.Formation.MovementSpeed;
            float distanceRatio = 0;
            if (speed < 0 || speed > 0)
            {
                float squaredY = (float)((double)delta.Y * delta.Y);
                float squaredZ = (float)((double)delta.Z * delta.Z);
                float squaredX = (float)((double)delta.X * delta.X);
                float yz = (float)((double)squaredY + squaredZ);
                float squaredDistance = (float)((double)yz + squaredX);
                float distance = MathF.Sqrt(squaredDistance);
                distanceRatio = (float)((double)distance / speed);
            }
            if (distanceRatio < 1)
                distanceRatio = 1;
            float penalty = (float)((double)distanceRatio * 0.001f);
            return (float)((double)metric + penalty);
        }

        /// <summary>
        /// Builds category-specific priority lists and assigns groups that cannot retain their current target.
        /// </summary>
        /// <param name="side">The automated side.</param>
        private void BuildCandidates(TacticalCombatSide side)
        {
            CandidateSet candidates = _candidateSets[(int)side.Side];
            candidates.Ordered.Clear();
            FallbackCandidates.Clear();
            foreach (CombatUnit unit in _session.Units)
            {
                if (
                    unit.Side == side.Side
                    || unit.IsBattleStation
                    || !unit.IsTargetable && unit.Status != CombatUnitStatus.Docked
                )
                    continue;
                TargetCandidate candidate = new(unit, -GetPriority(unit));
                if (IsCandidateAvailable(unit))
                    candidates.Ordered.Add(candidate);
                else
                    FallbackCandidates.Add(candidate);
            }
            candidates.Ordered.Sort(CompareCandidates);
            FallbackCandidates.Sort(CompareCandidates);
            foreach (TargetCandidate candidate in FallbackCandidates)
            {
                float score =
                    candidates.Ordered.Count == 0
                        ? candidate.Score
                        : (float)(
                            (double)candidates.Ordered[candidates.Ordered.Count - 1].Score + 1
                        );
                InsertCandidate(candidates.Ordered, new TargetCandidate(candidate.Unit, score));
            }
            RefreshCandidateCategories(candidates);
        }

        /// <summary>
        /// Projects the ordered candidate root into the categories used by explicit attack behaviors.
        /// </summary>
        /// <param name="candidates">The side's captured candidate ordering.</param>
        private static void RefreshCandidateCategories(CandidateSet candidates)
        {
            candidates.Capital.Clear();
            candidates.Fighters.Clear();
            foreach (TargetCandidate candidate in candidates.Ordered)
                (candidate.Unit.IsFighter ? candidates.Fighters : candidates.Capital).Add(
                    candidate
                );
        }

        /// <summary>
        /// Inserts a stored candidate score with registration ordering and preserves repeated equal entries.
        /// </summary>
        /// <param name="candidates">The ordered candidate root.</param>
        /// <param name="candidate">The scored candidate to insert.</param>
        internal static void InsertCandidate(
            List<TargetCandidate> candidates,
            TargetCandidate candidate
        )
        {
            int index = candidates.BinarySearch(candidate, _candidateComparer);
            if (index < 0)
                index = ~index;
            else
                while (
                    index < candidates.Count && CompareCandidates(candidates[index], candidate) == 0
                )
                    index++;
            candidates.Insert(index, candidate);
        }

        /// <summary>
        /// Selects the first eligible priority candidate for an unassigned explicit attack behavior.
        /// </summary>
        /// <param name="group">The group running its attack behavior.</param>
        /// <returns>The first category-matching target, or null when its current target should be retained.</returns>
        internal CombatUnit SelectBehaviorTarget(TacticalCombatGroup group)
        {
            if (CanRetainTarget(group))
                return null;
            CandidateSet candidates = _candidateSets[(int)group.Side];
            List<TargetCandidate> ordered =
                group.ActiveOrder == TacticalCombatOrder.AttackFighters
                    ? candidates.Fighters
                    : candidates.Capital;
            foreach (TargetCandidate candidate in ordered)
                if (IsCandidateAvailable(candidate.Unit))
                    return candidate.Unit;
            return null;
        }

        /// <summary>
        /// Preserves special orders, docked fighter groups, and targets with sufficient concentrated attack.
        /// </summary>
        /// <param name="group">The group being considered for reassignment.</param>
        /// <returns>Whether its existing order or target should remain.</returns>
        internal bool CanRetainTarget(TacticalCombatGroup group)
        {
            if (
                group.ActiveOrder
                is TacticalCombatOrder.Withdraw
                    or TacticalCombatOrder.Recover
                    or TacticalCombatOrder.AttackBattleStation
            )
                return true;
            if (group.IsFighterGroup)
                foreach (CombatUnit unit in group.Members)
                    if (unit.Status == CombatUnitStatus.Docked)
                        return true;
            CombatUnit target = group.PrimaryTarget;
            if (target == null || !IsCandidateAvailable(target))
                return false;
            float attack = 0;
            float metric = GetTargetMetric(group, target, ref attack);
            if (metric >= 0 && metric < 300)
                return true;
            foreach (TacticalCombatGroup other in _session.Groups)
                if (
                    other.Side == group.Side
                    && other.ID != group.ID
                    && ReferenceEquals(other.PrimaryTarget, target)
                )
                    metric = GetTargetMetric(other, target, ref attack);
            return metric >= 0 && metric < 300;
        }

        /// <summary>
        /// Checks target availability against the attacking side's withdrawal state.
        /// </summary>
        /// <param name="target">The opposing unit.</param>
        /// <returns>Whether the unit is a preferred assignment candidate.</returns>
        internal bool IsCandidateAvailable(CombatUnit target)
        {
            if (!target.IsTargetable)
                return false;
            CombatSide attackingSide =
                target.Side == CombatSide.Attacker ? CombatSide.Defender : CombatSide.Attacker;
            if (!_session.GetSide(attackingSide).IsWithdrawing)
                return target.GetWeaponRecharge() != 0;
            return target.HasHyperdrive
                && (
                    target.GetSpeed() > 0
                    || target.GetDamage(TacticalShipSystem.Engines) < 4
                        && target.MovementDelay < 10000
                );
        }

        /// <summary>
        /// Scores a target from its sustained attack, carried units, and remaining shield and hull ratios.
        /// </summary>
        /// <param name="unit">The opposing candidate.</param>
        /// <returns>The candidate priority before category ordering.</returns>
        private float GetPriority(CombatUnit unit)
        {
            int payload = unit.PayloadCount;
            float priority = (float)(
                (double)TacticalCombatEngagement.GetAttackRate(unit, null) * 15 + payload
            );
            double rechargeRatio = unit.GetShieldRechargeRatio();
            priority = (float)((double)priority - rechargeRatio * 0.01f);
            priority = (float)((double)priority - unit.HullFraction * 0.01f);
            if (unit.Status == CombatUnitStatus.Docked)
                priority -= 0.01f;
            return priority;
        }

        /// <summary>
        /// Recomputes the retained side attack from displayed capitals, initial hangars, and displayed fighters.
        /// </summary>
        /// <param name="side">The side whose stored assessment is refreshed.</param>
        public void AssessStrength(TacticalCombatSide side)
        {
            foreach (List<TacticalCombatGroup> groups in _targetingGroups.Values)
                groups.Clear();
            _assessedGroups.Clear();
            foreach (TacticalCombatGroup group in _session.Groups)
                if (group.Side != side.Side && group.PrimaryTarget != null)
                    _assessedGroups.Add(group);
            _assessedGroups.Sort(CompareGroups);
            foreach (TacticalCombatGroup group in _assessedGroups)
                _targetingGroups[group.PrimaryTarget].Add(group);
            float total = 0;
            foreach (CombatUnit unit in _assessedUnits)
            {
                if (unit.Side != side.Side || unit.IsFighter || !unit.IsEngaged)
                    continue;
                bool suppressed = false;
                foreach (TacticalCombatGroup group in _targetingGroups[unit])
                {
                    float interaction = group.GetAttackRate(unit);
                    double recharge = unit.GetShieldRecharge();
                    if (!(recharge > interaction))
                    {
                        suppressed = true;
                        break;
                    }
                }
                if (!suppressed)
                    total = (float)(TacticalCombatEngagement.GetAttackRate(unit, null) + total);
            }
            if (side.FactionIndex == 1)
                foreach (CombatUnit unit in _session.InitiallyDockedFighters)
                    total = (float)(TacticalCombatEngagement.GetAttackRate(unit, null) + total);
            foreach (CombatUnit unit in _assessedUnits)
                if (unit.Side == side.Side && unit.IsFighter && unit.IsEngaged)
                    total = (float)(TacticalCombatEngagement.GetAttackRate(unit, null) + total);
            if (_session.BattleStation?.IsOperational == true)
            {
                bool includeStation =
                    side.FactionIndex == 1 || _session.CanStartBattleStationAttack;
                if (!includeStation)
                    foreach (TacticalCombatGroup group in _session.Groups)
                        if (
                            group.Side == side.Side
                            && (
                                group.Order == TacticalCombatOrder.AttackBattleStation
                                || group.ActiveOrder == TacticalCombatOrder.AttackBattleStation
                            )
                        )
                        {
                            includeStation = true;
                            break;
                        }
                if (includeStation)
                    total = (float)((double)total + 20000000);
            }
            side.AttackBudget = total;
        }

        /// <summary>
        /// Orders assessed units by their unsigned tactical object identities.
        /// </summary>
        /// <param name="first">The first unit.</param>
        /// <param name="second">The second unit.</param>
        /// <returns>The unsigned object-key comparison.</returns>
        private static int CompareUnits(CombatUnit first, CombatUnit second) =>
            unchecked((uint)first.Index).CompareTo(unchecked((uint)second.Index));

        /// <summary>
        /// Orders targeting sessions by their unsigned tactical group identities.
        /// </summary>
        /// <param name="first">The first group.</param>
        /// <param name="second">The second group.</param>
        /// <returns>The unsigned session-key comparison.</returns>
        private static int CompareGroups(TacticalCombatGroup first, TacticalCombatGroup second) =>
            unchecked((uint)first.ID).CompareTo(unchecked((uint)second.ID));

        /// <summary>
        /// Orders candidate records by their stored score, then unsigned registration.
        /// </summary>
        /// <param name="first">The first candidate.</param>
        /// <param name="second">The second candidate.</param>
        /// <returns>The candidate ordering.</returns>
        private static int CompareCandidates(TargetCandidate first, TargetCandidate second)
        {
            if (first.Score < second.Score)
                return -1;
            if (first.Score > second.Score)
                return 1;
            return ((uint)first.Unit.Index).CompareTo((uint)second.Unit.Index);
        }

        /// <summary>
        /// Retains each side's once-per-second target ordering for its group behaviors.
        /// </summary>
        private sealed class CandidateSet
        {
            public List<TargetCandidate> Ordered { get; } = new();
            public List<TargetCandidate> Capital { get; } = new();
            public List<TargetCandidate> Fighters { get; } = new();
            public List<TargetCandidate> Fallback { get; } = new();
        }
    }

    /// <summary>
    /// Captures a target and its current priority for one assessment.
    /// </summary>
    internal readonly struct TargetCandidate
    {
        public CombatUnit Unit { get; }
        public float Score { get; }

        /// <summary>
        /// Creates an assessed target.
        /// </summary>
        /// <param name="unit">The target unit.</param>
        /// <param name="score">Its stored ascending ordering score.</param>
        public TargetCandidate(CombatUnit unit, float score)
        {
            Unit = unit;
            Score = score;
        }
    }
}
