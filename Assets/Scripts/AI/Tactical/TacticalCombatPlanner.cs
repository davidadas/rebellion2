using System;
using System.Collections.Generic;
using Rebellion.Game.Results;
using Rebellion.Tactical;

namespace Rebellion.AI.Tactical
{
    /// <summary>
    /// Owns autonomous regrouping, target assignment, retreat, and battle-station policy.
    /// Target assessment remains shared with explicit player orders.
    /// </summary>
    public sealed class TacticalCombatPlanner
    {
        private readonly TacticalCombatSession _session;
        private readonly TacticalCombatTargeting _targeting;
        private readonly List<TacticalCombatGroup> _remaining = new();
        private readonly List<Metric> _metrics = new();
        private readonly List<TacticalCombatGroup> _armedGroups = new();
        private readonly List<TacticalCombatGroup> _laserGroups = new();
        private readonly List<TargetCandidate> _capitalFirst = new();
        private readonly List<TargetCandidate> _fighterFirst = new();
        private readonly List<TacticalCombatDeployment>[] _deployments = { new(), new() };
        private readonly List<TacticalCombatUnit>[] _buckets = { new(), new(), new() };
        private readonly bool[] _partitioned = new bool[2];
        private readonly int[] _countdown = new int[2];

        /// <summary>
        /// Captures the shared assessment and indexes capital deployment once at encounter entry.
        /// </summary>
        /// <param name="session">The isolated tactical encounter.</param>
        /// <param name="targeting">The assessment also used by explicit attack orders.</param>
        internal TacticalCombatPlanner(
            TacticalCombatSession session,
            TacticalCombatTargeting targeting
        )
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
            foreach (TacticalCombatUnit unit in session.Units)
                if (!unit.IsFighter && !unit.IsBattleStation)
                    _deployments[(int)unit.Side].Add(new TacticalCombatDeployment(unit));
            foreach (List<TacticalCombatDeployment> deployments in _deployments)
                deployments.Sort(TacticalCombatDeployment.Compare);
            session.AutomationRequested += Plan;
        }

        /// <summary>
        /// Attaches the default tactical planner to an encounter for its lifetime.
        /// </summary>
        /// <param name="session">The isolated tactical encounter.</param>
        public static void Attach(TacticalCombatSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            _ = new TacticalCombatPlanner(session, session.Targeting);
        }

        /// <summary>
        /// Plans from the prepared assessment after side lifecycle maintenance, retaining the six-second interval.
        /// </summary>
        /// <param name="side">The side whose candidates were prepared for the current tick.</param>
        public void Plan(TacticalCombatSide side)
        {
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
            _targeting.AssessStrength(side);
            TacticalCombatSide opposingState = _session.GetSide(opposing);
            _targeting.AssessStrength(opposingState);
            _targeting.AssessStrength(opposingState);
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
                    IReadOnlyList<TargetCandidate> candidates = _targeting.GetCapitalCandidates(
                        side.Side
                    );
                    for (int index = 0; index < candidates.Count; index++)
                    {
                        TargetCandidate candidate = candidates[index];
                        if (candidate.Unit.IsTargetable)
                        {
                            _session.FireSuperlaser(candidate.Unit);
                            break;
                        }
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
        /// Assigns automated groups from the current turn-scoped candidate snapshot.
        /// </summary>
        /// <param name="side">The automated side.</param>
        private void Replan(TacticalCombatSide side)
        {
            _armedGroups.Clear();
            _laserGroups.Clear();
            foreach (TacticalCombatGroup group in _session.Groups)
                if (
                    group.Side == side.Side
                    && group.Members.Count > 0
                    && !_targeting.CanRetainTarget(group)
                )
                    (HasLasers(group.Members[0]) ? _laserGroups : _armedGroups).Add(group);
            IReadOnlyList<TargetCandidate> candidates = _targeting.GetCandidates(side.Side);
            _capitalFirst.Clear();
            _fighterFirst.Clear();
            for (int index = 0; index < candidates.Count; index++)
            {
                TargetCandidate candidate = candidates[index];
                (candidate.Unit.IsFighter ? _fighterFirst : _capitalFirst).Add(candidate);
            }
            AppendCandidateCopies(_capitalFirst, _fighterFirst);
            AppendCandidateCopies(_fighterFirst, _capitalFirst);
            Assign(_armedGroups, _capitalFirst);
            Assign(_laserGroups, _fighterFirst);
            _targeting.SetCandidateOrder(side.Side, _capitalFirst);
        }

        /// <summary>
        /// Appends copied candidates after the destination's captured final score using stored float offsets.
        /// </summary>
        /// <param name="source">The ordered records being copied.</param>
        /// <param name="destination">The root receiving the copied records.</param>
        private static void AppendCandidateCopies(
            IReadOnlyList<TargetCandidate> source,
            List<TargetCandidate> destination
        )
        {
            float last = destination.Count == 0 ? 0 : destination[destination.Count - 1].Score;
            float offset = 1;
            for (int index = 0; index < source.Count; index++)
            {
                TargetCandidate candidate = source[index];
                TacticalCombatTargeting.InsertCandidate(
                    destination,
                    new TargetCandidate(candidate.Unit, (float)((double)last + offset))
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
            IReadOnlyList<TargetCandidate> candidates
        )
        {
            if (groups.Count == 0 || candidates.Count == 0)
                return;
            _remaining.Clear();
            for (int index = 0; index < groups.Count; index++)
                _remaining.Add(groups[index]);
            foreach (TargetCandidate candidate in candidates)
            {
                if (!_targeting.IsCandidateAvailable(candidate.Unit) || _remaining.Count == 0)
                    continue;
                _metrics.Clear();
                foreach (TacticalCombatGroup group in _remaining)
                {
                    float attack = 0;
                    float metric = TacticalCombatTargeting.GetTargetMetric(
                        group,
                        candidate.Unit,
                        ref attack
                    );
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
                    float metric = TacticalCombatTargeting.GetTargetMetric(
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
            foreach (TargetCandidate candidate in candidates)
                if (_targeting.IsCandidateAvailable(candidate.Unit))
                {
                    fallback = candidate.Unit;
                    break;
                }
            foreach (TacticalCombatGroup group in _remaining)
                _session.SetTarget(group, fallback);
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
