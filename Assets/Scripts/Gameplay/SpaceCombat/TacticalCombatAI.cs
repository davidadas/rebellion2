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
        private readonly TacticalCombatSession _session;
        private readonly CandidateSet[] _candidateSets = { new(), new() };
        private CombatSide _candidateSide;
        private List<Candidate> CapitalCandidates => _candidateSets[(int)_candidateSide].Capital;
        private List<Candidate> FighterCandidates => _candidateSets[(int)_candidateSide].Fighters;
        private List<Candidate> FallbackCandidates => _candidateSets[(int)_candidateSide].Fallback;
        private readonly List<TacticalCombatGroup> _remaining = new();
        private readonly List<Metric> _metrics = new();
        private readonly List<TacticalCombatGroup> _armedGroups = new();
        private readonly List<TacticalCombatGroup> _laserGroups = new();
        private readonly List<TacticalCombatUnit>[] _buckets = { new(), new(), new() };
        private readonly bool[] _partitioned = new bool[2];
        private readonly int[] _countdown = new int[2];

        /// <summary>
        /// Uses the encounter’s captured roster for every target assessment.
        /// </summary>
        /// <param name="session">The isolated tactical encounter.</param>
        public TacticalCombatAI(TacticalCombatSession session)
        {
            this._session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>
        /// Reconsiders automated groups at six-second intervals and evaluates the side's withdrawal threshold.
        /// </summary>
        /// <param name="side">The side being updated.</param>
        public void Advance(TacticalCombatSide side)
        {
            _candidateSide = side.Side;
            BuildCandidates(side);
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
            float own = GetSideAttackBudget(side.Side);
            float enemy = GetSideAttackBudget(opposing);
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
                    && _session.BattleStation.Charge == 100
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
            float remaining = attack - target.Systems.GetShieldRecharge();
            if (remaining <= 0)
                return -1;
            float metric = durability / remaining;
            Vector3 delta = target.Position - group.Formation.Position;
            if (delta == System.Numerics.Vector3.Zero)
                return metric;
            float speed = group.Formation.MovementSpeed;
            float distance = speed != 0 ? TacticalCombatGeometry.GetLength(delta) / speed : 0;
            return (float)((double)metric + Math.Max(distance, 1) * 0.001f);
        }

        /// <summary>
        /// Partitions capital ships into range-ordered weapon categories before initial automatic targeting.
        /// </summary>
        /// <param name="side">The automated side.</param>
        private void Partition(CombatSide side)
        {
            foreach (List<TacticalCombatUnit> bucket in _buckets)
                bucket.Clear();
            foreach (TacticalCombatUnit unit in _session.Units)
                if (
                    unit.Side == side
                    && !unit.IsFighter
                    && !unit.IsBattleStation
                    && unit.IsTargetable
                )
                    _buckets[
                        HasWeapons(unit)
                            ? HasLasers(unit)
                                ? 1
                                : 0
                            : 2
                    ]
                        .Add(unit);
            foreach (List<TacticalCombatUnit> bucket in _buckets)
                bucket.Sort(CompareDeployment);
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
            int count = bucket.Count;
            if (count == 0)
                return 0;
            int limit = 1;
            if (upper >= limit)
                do
                {
                    if (limit * limit > count)
                        break;
                    limit++;
                } while (limit <= upper);
            int selected = Math.Min(count <= 1 ? 1 : limit - 1, upper - lower + 1);
            int spacing = count / selected;
            int created = 0;
            for (int start = 0; start < count; )
            {
                int end = created + 1 < selected ? start + spacing : count;
                _session.CreateGroup(bucket.GetRange(start, end - start));
                created++;
                start = end;
            }
            return created;
        }

        /// <summary>
        /// Builds category-specific priority lists and assigns groups that cannot retain their current target.
        /// </summary>
        /// <param name="side">The automated side.</param>
        private void BuildCandidates(TacticalCombatSide side)
        {
            CapitalCandidates.Clear();
            FighterCandidates.Clear();
            FallbackCandidates.Clear();
            foreach (TacticalCombatUnit unit in _session.Units)
            {
                if (
                    unit.Side == side.Side
                    || unit.IsBattleStation
                    || !unit.IsTargetable && unit.Status != TacticalCombatUnitStatus.Docked
                )
                    continue;
                Candidate candidate = new(unit, GetPriority(unit));
                if (IsCandidateAvailable(unit))
                    (unit.IsFighter ? FighterCandidates : CapitalCandidates).Add(candidate);
                else
                    FallbackCandidates.Add(candidate);
            }
            CapitalCandidates.Sort(CompareCandidates);
            FighterCandidates.Sort(CompareCandidates);
            FallbackCandidates.Sort(CompareCandidates);
            foreach (Candidate candidate in FallbackCandidates)
                (candidate.Unit.IsFighter ? FighterCandidates : CapitalCandidates).Add(candidate);
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
            List<Candidate> capitalFirst = new(CapitalCandidates);
            capitalFirst.AddRange(FighterCandidates);
            capitalFirst.AddRange(CapitalCandidates);
            List<Candidate> fighterFirst = new(FighterCandidates);
            fighterFirst.AddRange(CapitalCandidates);
            Assign(_armedGroups, capitalFirst);
            Assign(_laserGroups, fighterFirst);
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
                        new Metric(group, metric < 0 || metric > 180 ? 100000 - attack : metric)
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
                    if (metric < 0 || metric > 180)
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
            float rechargeRatio = unit.IsFighter
                ? unit.Systems.IsAlive
                    ? 1
                    : 0
                : unit.Systems.BaseShieldRecharge != 0
                    ? unit.Systems.GetShieldRecharge() / unit.Systems.BaseShieldRecharge
                    : 0;
            float priority = (float)(
                (double)TacticalCombatEngagement.GetAttackRate(unit, null) * 15 + payload
            );
            priority = (float)((double)priority - rechargeRatio * 0.01f);
            priority = (float)((double)priority - unit.Systems.HullFraction * 0.01f);
            if (unit.Status == TacticalCombatUnitStatus.Docked)
                priority -= 0.01f;
            return priority;
        }

        /// <summary>
        /// Calculates unopposed side attack, excluding capital ships already under sufficient concentrated attack.
        /// </summary>
        /// <param name="side">The assessed side.</param>
        /// <returns>The available side attack budget.</returns>
        private float GetSideAttackBudget(CombatSide side)
        {
            float total = 0;
            foreach (TacticalCombatUnit unit in _session.Units)
            {
                if (
                    unit.Side != side
                    || !unit.IsTargetable && unit.Status != TacticalCombatUnitStatus.Docked
                )
                    continue;
                bool engaged = false;
                if (!unit.IsFighter)
                    foreach (TacticalCombatGroup opposing in _session.Groups)
                        if (
                            opposing.Side != side
                            && ReferenceEquals(opposing.PrimaryTarget, unit)
                            && opposing.GetAttackRate(unit) >= unit.Systems.GetShieldRecharge()
                        )
                        {
                            engaged = true;
                            break;
                        }
                if (!engaged)
                    total = (float)(
                        (double)total + TacticalCombatEngagement.GetAttackRate(unit, null)
                    );
            }
            if (
                _session.BattleStation?.IsOperational == true
                && (
                    _session.BattleStation.Unit.Side == side
                    || _session.CanAttackBattleStation(side)
                    || _session.ActiveAttackRun?.IsInProgress == true
                        && _session.ActiveAttackRun.Group.Side == side
                )
            )
                total = (float)((double)total + 20000000);
            return total;
        }

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
        /// Checks whether the unit has any primary weapon lane.
        /// </summary>
        /// <param name="unit">The assessed unit.</param>
        /// <returns>Whether any weapon lane exists.</returns>
        private static bool HasWeapons(TacticalCombatUnit unit) =>
            TacticalCombatFireControl.GetMaximumRange(unit) != 0;

        /// <summary>
        /// Orders deployment members by descending weapon range and movement, then registration.
        /// </summary>
        /// <param name="first">The first member.</param>
        /// <param name="second">The second member.</param>
        /// <returns>The deployment ordering.</returns>
        private static int CompareDeployment(TacticalCombatUnit first, TacticalCombatUnit second)
        {
            float a = (float)(
                (double)first.GetSpeed() + TacticalCombatFireControl.GetMaximumRange(first) * 10000
            );
            float b = (float)(
                (double)second.GetSpeed()
                + TacticalCombatFireControl.GetMaximumRange(second) * 10000
            );
            int comparison = b.CompareTo(a);
            return comparison != 0 ? comparison : first.Index.CompareTo(second.Index);
        }

        /// <summary>
        /// Orders candidate targets by descending priority, then stable registration.
        /// </summary>
        /// <param name="first">The first candidate.</param>
        /// <param name="second">The second candidate.</param>
        /// <returns>The candidate ordering.</returns>
        private static int CompareCandidates(Candidate first, Candidate second)
        {
            int comparison = second.Priority.CompareTo(first.Priority);
            return comparison != 0 ? comparison : first.Unit.Index.CompareTo(second.Unit.Index);
        }

        /// <summary>
        /// Orders candidate group metrics by ascending value, then registration.
        /// </summary>
        /// <param name="first">The first group metric.</param>
        /// <param name="second">The second group metric.</param>
        /// <returns>The metric ordering.</returns>
        private static int CompareMetrics(Metric first, Metric second)
        {
            int comparison = first.Value.CompareTo(second.Value);
            return comparison != 0 ? comparison : first.Group.ID.CompareTo(second.Group.ID);
        }

        /// <summary>
        /// Retains each side's once-per-second target ordering for its group behaviors.
        /// </summary>
        private sealed class CandidateSet
        {
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
            public float Priority { get; }

            /// <summary>
            /// Creates an assessed target.
            /// </summary>
            /// <param name="unit">The target unit.</param>
            /// <param name="priority">Its assessment priority.</param>
            public Candidate(TacticalCombatUnit unit, float priority)
            {
                Unit = unit;
                Priority = priority;
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
