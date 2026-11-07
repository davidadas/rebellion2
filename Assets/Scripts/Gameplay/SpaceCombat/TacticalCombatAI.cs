using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Assigns tactical groups using target durability, sustained attack, movement, and command priorities.
    /// All assessments use the captured battle roster rather than traversing the campaign graph.
    /// </summary>
    public sealed class TacticalCombatAI
    {
        private static readonly Comparer<Candidate> _candidateComparer = Comparer<Candidate>.Create(
            CompareCandidates
        );
        private readonly TacticalCombatSession _session;
        private readonly CandidateSet[] _candidateSets = { new(), new() };
        private CombatSide _candidateSide;
        private List<Candidate> CapitalCandidates => _candidateSets[(int)_candidateSide].Capital;
        private List<Candidate> FallbackCandidates => _candidateSets[(int)_candidateSide].Fallback;
        private readonly List<TacticalCombatGroup> _remaining = new();
        private readonly List<Metric> _metrics = new();
        private readonly List<TacticalCombatGroup> _armedGroups = new();
        private readonly List<TacticalCombatGroup> _laserGroups = new();
        private readonly List<Candidate> _capitalFirst = new();
        private readonly List<Candidate> _fighterFirst = new();
        private readonly List<TacticalCombatDeployment>[] _deployments = { new(), new() };
        private readonly List<TacticalCombatUnit>[] _buckets = { new(), new(), new() };
        private readonly bool[] _partitioned = new bool[2];
        private readonly int[] _countdown = new int[2];
        private readonly List<TacticalCombatUnit> _assessedUnits = new();
        private readonly List<TacticalCombatGroup> _assessedGroups = new();
        private readonly Dictionary<
            TacticalCombatUnit,
            List<TacticalCombatGroup>
        > _targetingGroups = new();

        /// <summary>
        /// Uses the encounter’s captured roster for every target assessment.
        /// </summary>
        /// <param name="session">The isolated tactical encounter.</param>
        public TacticalCombatAI(TacticalCombatSession session)
        {
            this._session = session ?? throw new ArgumentNullException(nameof(session));
            foreach (TacticalCombatUnit unit in session.Units)
            {
                _targetingGroups.Add(unit, new List<TacticalCombatGroup>());
                if (!unit.IsBattleStation)
                    _assessedUnits.Add(unit);
                if (!unit.IsFighter && !unit.IsBattleStation)
                    _deployments[(int)unit.Side].Add(new TacticalCombatDeployment(unit));
            }
            _assessedUnits.Sort(CompareUnits);
            foreach (List<TacticalCombatDeployment> deployments in _deployments)
                deployments.Sort(TacticalCombatDeployment.Compare);
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
        /// Plans from the prepared assessment after side lifecycle maintenance, retaining the six-second interval.
        /// </summary>
        /// <param name="side">The side whose candidates were prepared for the current tick.</param>
        public void Plan(TacticalCombatSide side)
        {
            _candidateSide = side.Side;
            if (!side.IsAutomated || side.IsWithdrawing || --_countdown[(int)side.Side] > 0)
                return;
            _countdown[(int)side.Side] = 6;
            if (!_partitioned[(int)side.Side])
            {
                _partitioned[(int)side.Side] = true;
                Partition(side.Side);
            }
            Replan(side);
            CombatSide opposing =
                side.Side == CombatSide.Attacker ? CombatSide.Defender : CombatSide.Attacker;
            AssessStrength(side);
            TacticalCombatSide opposingState = _session.GetSide(opposing);
            AssessStrength(opposingState);
            AssessStrength(opposingState);
            float own = side.AttackBudget;
            float enemy = opposingState.AttackBudget;
            if (
                enemy > 0
                && (own / enemy < side.RetreatStrengthRatio || own == 0)
                && _session.ActiveAttackRun?.IsInProgress != true
            )
                _session.BeginWithdrawal(side.Side);
            if (_session.BattleStation?.IsOperational == true && !side.IsWithdrawing)
            {
                if (
                    _session.BattleStation.Unit.Side == side.Side
                    && (
                        _session.BattleStation.Charge == 100
                        || float.IsNaN(_session.BattleStation.Charge)
                    )
                )
                {
                    foreach (Candidate candidate in CapitalCandidates)
                        if (candidate.Unit.IsTargetable)
                        {
                            _session.FireSuperlaser(candidate.Unit);
                            break;
                        }
                }
                else if (_session.CanAttackBattleStation(side.Side))
                    foreach (TacticalCombatUnit unit in _session.Units)
                        if (
                            unit.Side == side.Side
                            && unit.IsFighter
                            && unit.IsTargetable
                            && _session.GetGroup(unit) is TacticalCombatGroup group
                        )
                        {
                            _session.SetOrder(group, TacticalCombatOrder.AttackBattleStation);
                            break;
                        }
            }
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
            TacticalCombatUnit target,
            ref float attack
        )
        {
            attack = (float)((double)attack + group.GetAttackRate(target));
            float durability = Math.Max(
                (float)((double)target.Systems.Hull * 0.65f + target.Systems.Shields),
                1
            );
            double remaining = (double)attack - target.Systems.GetShieldRecharge();
            if (!(remaining > 0))
                return -1;
            float metric = (float)(durability / remaining);
            Vector3 delta = target.Position - group.Formation.Position;
            if (delta == System.Numerics.Vector3.Zero)
                return metric;
            float speed = group.Formation.MovementSpeed;
            double distance = speed != 0 ? TacticalCombatGeometry.MeasureLength(delta) / speed : 0;
            return (float)(metric + Math.Max(distance, 1) * 0.001f);
        }

        /// <summary>
        /// Partitions capital ships into range-ordered weapon categories before initial automatic targeting.
        /// </summary>
        /// <param name="side">The automated side.</param>
        private void Partition(CombatSide side)
        {
            foreach (TacticalCombatGroup group in _session.Groups)
                if (group.Side == side && !group.IsFighterGroup)
                    _session.DeleteGroup(group);
            foreach (List<TacticalCombatUnit> bucket in _buckets)
                bucket.Clear();
            foreach (TacticalCombatDeployment deployment in _deployments[(int)side])
                if (deployment.Unit.IsTargetable)
                    _buckets[deployment.Category].Add(deployment.Unit);
            int first = PartitionBucket(_buckets[0], 0, 3);
            int second = PartitionBucket(_buckets[1], first, first + 3);
            PartitionBucket(_buckets[2], first + second, 7);
        }

        /// <summary>
        /// Splits a weapon category into the configured square-root count of contiguous groups.
        /// </summary>
        /// <param name="bucket">The ordered category members.</param>
        /// <param name="lower">The first available group slot.</param>
        /// <param name="upper">The final available group slot.</param>
        /// <returns>The number of created groups.</returns>
        private int PartitionBucket(List<TacticalCombatUnit> bucket, int lower, int upper)
        {
            uint count = (uint)bucket.Count & 0xffffu;
            uint limit = 1;
            while (limit <= (uint)upper && limit * limit <= count)
                limit++;
            uint selected = count < 2 ? 1 : limit - 1;
            uint available = (uint)(upper - lower + 1);
            if (selected >= available)
                selected = available;
            uint spacing = count / selected;
            uint created = 0;
            TacticalCombatGroup group = null;
            for (int index = 0; index < bucket.Count; index++)
            {
                TacticalCombatUnit unit = bucket[index];
                if (index % (int)spacing == 0 && created < selected)
                {
                    group = _session.CreateGroup(new[] { unit });
                    if (unit.Side == _session.PlayerSide)
                        _session.SetObjectGroup(unit.Side, lower + (int)created, group);
                    created++;
                }
                else
                    _session.AddGroupMember(group, unit);
            }
            return bucket.Count == 0 ? 0 : (int)selected;
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
            foreach (TacticalCombatUnit unit in _session.Units)
            {
                if (
                    unit.Side == side.Side
                    || unit.IsBattleStation
                    || !unit.IsTargetable && unit.Status != TacticalCombatUnitStatus.Docked
                )
                    continue;
                Candidate candidate = new(unit, -GetPriority(unit));
                if (IsCandidateAvailable(unit))
                    candidates.Ordered.Add(candidate);
                else
                    FallbackCandidates.Add(candidate);
            }
            candidates.Ordered.Sort(CompareCandidates);
            FallbackCandidates.Sort(CompareCandidates);
            foreach (Candidate candidate in FallbackCandidates)
            {
                float score =
                    candidates.Ordered.Count == 0
                        ? candidate.Score
                        : (float)(
                            (double)candidates.Ordered[candidates.Ordered.Count - 1].Score + 1
                        );
                InsertCandidate(candidates.Ordered, new Candidate(candidate.Unit, score));
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
            foreach (Candidate candidate in candidates.Ordered)
                (candidate.Unit.IsFighter ? candidates.Fighters : candidates.Capital).Add(
                    candidate
                );
        }

        /// <summary>
        /// Inserts a stored candidate score with registration ordering and preserves repeated equal entries.
        /// </summary>
        /// <param name="candidates">The ordered candidate root.</param>
        /// <param name="candidate">The scored candidate to insert.</param>
        private static void InsertCandidate(List<Candidate> candidates, Candidate candidate)
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
        internal TacticalCombatUnit SelectBehaviorTarget(TacticalCombatGroup group)
        {
            if (CanRetainTarget(group))
                return null;
            CandidateSet candidates = _candidateSets[(int)group.Side];
            List<Candidate> ordered =
                group.ActiveOrder == TacticalCombatOrder.AttackFighters
                    ? candidates.Fighters
                    : candidates.Capital;
            foreach (Candidate candidate in ordered)
                if (IsCandidateAvailable(candidate.Unit))
                    return candidate.Unit;
            return null;
        }

        /// <summary>
        /// Assigns automated groups from the current turn-scoped candidate snapshot.
        /// </summary>
        /// <param name="side">The automated side.</param>
        private void Replan(TacticalCombatSide side)
        {
            _armedGroups.Clear();
            _laserGroups.Clear();
            foreach (TacticalCombatGroup group in _session.Groups)
                if (group.Side == side.Side && group.Members.Count > 0 && !CanRetainTarget(group))
                    (HasLasers(group.Members[0]) ? _laserGroups : _armedGroups).Add(group);
            CandidateSet candidates = _candidateSets[(int)side.Side];
            _capitalFirst.Clear();
            _fighterFirst.Clear();
            foreach (Candidate candidate in candidates.Ordered)
                (candidate.Unit.IsFighter ? _fighterFirst : _capitalFirst).Add(candidate);
            AppendCandidateCopies(_capitalFirst, _fighterFirst);
            AppendCandidateCopies(_fighterFirst, _capitalFirst);
            Assign(_armedGroups, _capitalFirst);
            Assign(_laserGroups, _fighterFirst);
            candidates.Ordered.Clear();
            candidates.Ordered.AddRange(_capitalFirst);
            RefreshCandidateCategories(candidates);
        }

        /// <summary>
        /// Appends copied candidates after the destination's captured final score using stored float offsets.
        /// </summary>
        /// <param name="source">The ordered records being copied.</param>
        /// <param name="destination">The root receiving the copied records.</param>
        private static void AppendCandidateCopies(
            IReadOnlyList<Candidate> source,
            List<Candidate> destination
        )
        {
            float last = destination.Count == 0 ? 0 : destination[destination.Count - 1].Score;
            float offset = 1;
            for (int index = 0; index < source.Count; index++)
            {
                Candidate candidate = source[index];
                InsertCandidate(
                    destination,
                    new Candidate(candidate.Unit, (float)((double)last + offset))
                );
                offset = (float)((double)offset + 1);
            }
        }

        /// <summary>
        /// Assigns target concentration using stable target and group metric ordering.
        /// </summary>
        /// <param name="groups">The unassigned groups.</param>
        /// <param name="candidates">The category-ordered target candidates.</param>
        private void Assign(
            IReadOnlyList<TacticalCombatGroup> groups,
            IReadOnlyList<Candidate> candidates
        )
        {
            if (groups.Count == 0 || candidates.Count == 0)
                return;
            _remaining.Clear();
            for (int index = 0; index < groups.Count; index++)
                _remaining.Add(groups[index]);
            foreach (Candidate candidate in candidates)
            {
                if (!IsCandidateAvailable(candidate.Unit) || _remaining.Count == 0)
                    continue;
                _metrics.Clear();
                foreach (TacticalCombatGroup group in _remaining)
                {
                    float attack = 0;
                    float metric = GetTargetMetric(group, candidate.Unit, ref attack);
                    _metrics.Add(
                        new Metric(group, !(metric >= 0) || metric > 180 ? 100000 - attack : metric)
                    );
                }
                _metrics.Sort(CompareMetrics);
                if (_metrics.Count == 1)
                {
                    _session.SetTarget(_metrics[0].Group, candidate.Unit);
                    _remaining.Remove(_metrics[0].Group);
                    return;
                }
                float concentrated = _metrics[0].Group.GetAttackRate(candidate.Unit);
                for (int qualifying = _metrics.Count - 1; qualifying > 0; qualifying--)
                {
                    float metric = GetTargetMetric(
                        _metrics[qualifying].Group,
                        candidate.Unit,
                        ref concentrated
                    );
                    if (!(metric >= 0) || !(metric <= 180))
                        continue;
                    for (int assigned = _metrics.Count - 1; assigned >= qualifying; assigned--)
                    {
                        _session.SetTarget(_metrics[assigned].Group, candidate.Unit);
                        _remaining.Remove(_metrics[assigned].Group);
                    }
                    break;
                }
            }
            TacticalCombatUnit fallback = candidates[0].Unit;
            foreach (Candidate candidate in candidates)
                if (IsCandidateAvailable(candidate.Unit))
                {
                    fallback = candidate.Unit;
                    break;
                }
            foreach (TacticalCombatGroup group in _remaining)
                _session.SetTarget(group, fallback);
        }

        /// <summary>
        /// Preserves special orders, docked fighter groups, and targets with sufficient concentrated attack.
        /// </summary>
        /// <param name="group">The group being considered for reassignment.</param>
        /// <returns>Whether its existing order or target should remain.</returns>
        private bool CanRetainTarget(TacticalCombatGroup group)
        {
            if (
                group.ActiveOrder
                is TacticalCombatOrder.Withdraw
                    or TacticalCombatOrder.Recover
                    or TacticalCombatOrder.AttackBattleStation
            )
                return true;
            if (group.IsFighterGroup)
                foreach (TacticalCombatUnit unit in group.Members)
                    if (unit.Status == TacticalCombatUnitStatus.Docked)
                        return true;
            TacticalCombatUnit target = group.PrimaryTarget;
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
        private bool IsCandidateAvailable(TacticalCombatUnit target)
        {
            if (!target.IsTargetable)
                return false;
            CombatSide attackingSide =
                target.Side == CombatSide.Attacker ? CombatSide.Defender : CombatSide.Attacker;
            if (!_session.GetSide(attackingSide).IsWithdrawing)
                return target.Systems.GetWeaponRecharge() != 0;
            return target.Systems.HasHyperdrive
                && (
                    target.GetSpeed() > 0
                    || target.Systems.GetDamage(TacticalShipSystem.Engines) < 4
                        && target.Systems.MovementDelay < 10000
                );
        }

        /// <summary>
        /// Scores a target from its sustained attack, carried units, and remaining shield and hull ratios.
        /// </summary>
        /// <param name="unit">The opposing candidate.</param>
        /// <returns>The candidate priority before category ordering.</returns>
        private float GetPriority(TacticalCombatUnit unit)
        {
            int payload = unit.PayloadCount;
            float priority = (float)(
                (double)TacticalCombatEngagement.GetAttackRate(unit, null) * 15 + payload
            );
            double rechargeRatio = unit.Systems.GetShieldRechargeRatio();
            priority = (float)((double)priority - rechargeRatio * 0.01f);
            priority = (float)((double)priority - unit.Systems.HullFraction * 0.01f);
            if (unit.Status == TacticalCombatUnitStatus.Docked)
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
            foreach (TacticalCombatUnit unit in _assessedUnits)
            {
                if (unit.Side != side.Side || unit.IsFighter || !unit.IsEngaged)
                    continue;
                bool suppressed = false;
                foreach (TacticalCombatGroup group in _targetingGroups[unit])
                {
                    float interaction = group.GetAttackRate(unit);
                    double recharge = unit.Systems.GetShieldRecharge();
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
                foreach (TacticalCombatUnit unit in _session.InitiallyDockedFighters)
                    total = (float)(TacticalCombatEngagement.GetAttackRate(unit, null) + total);
            foreach (TacticalCombatUnit unit in _assessedUnits)
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
        private static int CompareUnits(TacticalCombatUnit first, TacticalCombatUnit second) =>
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
        /// Checks whether any battery contains a laser lane.
        /// </summary>
        /// <param name="unit">The assessed unit.</param>
        /// <returns>Whether a laser lane exists.</returns>
        private static bool HasLasers(TacticalCombatUnit unit)
        {
            for (int arc = 0; arc < 4; arc++)
                if (
                    unit.Weapons.GetStrength((TacticalWeaponArc)arc, TacticalWeaponType.LaserCannon)
                    != 0
                )
                    return true;
            return false;
        }

        /// <summary>
        /// Orders candidate records by their stored score, then unsigned registration.
        /// </summary>
        /// <param name="first">The first candidate.</param>
        /// <param name="second">The second candidate.</param>
        /// <returns>The candidate ordering.</returns>
        private static int CompareCandidates(Candidate first, Candidate second)
        {
            if (first.Score < second.Score)
                return -1;
            if (first.Score > second.Score)
                return 1;
            return ((uint)first.Unit.Index).CompareTo((uint)second.Unit.Index);
        }

        /// <summary>
        /// Orders candidate group metrics by ascending value, then registration.
        /// </summary>
        /// <param name="first">The first group metric.</param>
        /// <param name="second">The second group metric.</param>
        /// <returns>The metric ordering.</returns>
        private static int CompareMetrics(Metric first, Metric second)
        {
            if (first.Value < second.Value)
                return -1;
            if (first.Value > second.Value)
                return 1;
            return ((uint)first.Group.ID).CompareTo((uint)second.Group.ID);
        }

        /// <summary>
        /// Retains each side's once-per-second target ordering for its group behaviors.
        /// </summary>
        private sealed class CandidateSet
        {
            public List<Candidate> Ordered { get; } = new();
            public List<Candidate> Capital { get; } = new();
            public List<Candidate> Fighters { get; } = new();
            public List<Candidate> Fallback { get; } = new();
        }

        /// <summary>
        /// Captures a target and its current priority for one assessment.
        /// </summary>
        private readonly struct Candidate
        {
            public TacticalCombatUnit Unit { get; }
            public float Score { get; }

            /// <summary>
            /// Creates an assessed target.
            /// </summary>
            /// <param name="unit">The target unit.</param>
            /// <param name="score">Its stored ascending ordering score.</param>
            public Candidate(TacticalCombatUnit unit, float score)
            {
                Unit = unit;
                Score = score;
            }
        }

        /// <summary>
        /// Captures a group's target-elimination metric for stable assignment.
        /// </summary>
        private readonly struct Metric
        {
            public TacticalCombatGroup Group { get; }
            public float Value { get; }

            /// <summary>
            /// Creates an assessed group metric.
            /// </summary>
            /// <param name="group">The attacking group.</param>
            /// <param name="value">The elimination metric.</param>
            public Metric(TacticalCombatGroup group, float value)
            {
                Group = group;
                Value = value;
            }
        }
    }
}
