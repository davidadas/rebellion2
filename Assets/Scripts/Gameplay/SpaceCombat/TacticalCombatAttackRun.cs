using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Identifies a fighter group's progress through a battle-station attack.
    /// </summary>
    public enum TacticalCombatAttackRunState
    {
        Approaching,
        Starting,
        Succeeding,
        Failing,
        Succeeded,
        Failed,
        Cancelled,
    }

    /// <summary>
    /// Owns the staged approach, survivor selection, and two-minute attack sequence for one fighter group.
    /// </summary>
    public sealed class TacticalCombatAttackRun
    {
        private readonly IRandomNumberProvider _random;
        private readonly Func<float> _commandBudget;
        private readonly List<TacticalCombatUnit> _candidates = new();
        private readonly Dictionary<TacticalCombatUnit, Pools> _entryPools = new();
        private bool _transitionPending;
        private int _progressNotice;
        public TacticalCombatGroup Group { get; }
        public TacticalCombatAttackRunState State { get; private set; } =
            TacticalCombatAttackRunState.Approaching;
        public float RemainingMilliseconds { get; private set; }
        public bool IsInProgress =>
            State
                is TacticalCombatAttackRunState.Approaching
                    or TacticalCombatAttackRunState.Starting
                    or TacticalCombatAttackRunState.Succeeding
                    or TacticalCombatAttackRunState.Failing;
        public event Action<
            TacticalCombatAttackRun,
            TacticalCombatAttackRunState
        > StateChangeRequested;
        public event Action<TacticalCombatUnit> UnitDestroyed;
        public event Action<TacticalCombatUnit> UnitParticipationChanged;
        public event Action<TacticalCombatAttackRun, int> ProgressChanged;

        /// <summary>
        /// Creates the attack sequence after an eligible fighter group receives its order.
        /// </summary>
        /// <param name="group">The attacking fighter group.</param>
        /// <param name="random">The battle's attack-selection stream.</param>
        /// <param name="commandBudget">The live fighter command budget.</param>
        public TacticalCombatAttackRun(
            TacticalCombatGroup group,
            IRandomNumberProvider random,
            Func<float> commandBudget
        )
        {
            Group = group ?? throw new ArgumentNullException(nameof(group));
            this._random = random ?? throw new ArgumentNullException(nameof(random));
            this._commandBudget =
                commandBudget ?? throw new ArgumentNullException(nameof(commandBudget));
            if (!group.IsFighterGroup)
                throw new ArgumentException(
                    "An attack run requires a fighter group.",
                    nameof(group)
                );
        }

        /// <summary>
        /// Moves the group's reference point to its attack entry while its fighters remain targetable.
        /// </summary>
        /// <param name="elapsedMilliseconds">The elapsed movement interval.</param>
        /// <param name="battleDepth">The encounter's deployment span.</param>
        public void AdvanceMovement(float elapsedMilliseconds, float battleDepth)
        {
            if (State != TacticalCombatAttackRunState.Approaching || _transitionPending)
                return;
            Vector3 heading = new(-Group.SideLane.Z, 0, Group.SideLane.X);
            Vector3 point = heading * (battleDepth * 0.5f);
            if (!Group.Formation.MoveTowards(point, elapsedMilliseconds, 0.2f, heading))
                RequestState(TacticalCombatAttackRunState.Starting);
        }

        /// <summary>
        /// Advances the selected success or failure sequence and requests its queued conclusion.
        /// </summary>
        /// <param name="elapsedMilliseconds">The elapsed system interval.</param>
        public void Advance(float elapsedMilliseconds)
        {
            if (
                _transitionPending
                || State
                    is not TacticalCombatAttackRunState.Succeeding
                        and not TacticalCombatAttackRunState.Failing
            )
                return;
            if (_progressNotice < 9 && RemainingMilliseconds <= 120000 - _progressNotice * 12000)
                ProgressChanged?.Invoke(this, _progressNotice++);
            RemainingMilliseconds -= elapsedMilliseconds;
            if (RemainingMilliseconds <= 0)
            {
                RequestState(
                    State == TacticalCombatAttackRunState.Succeeding
                        ? TacticalCombatAttackRunState.Succeeded
                        : TacticalCombatAttackRunState.Failed
                );
                RemainingMilliseconds = 120000;
            }
        }

        /// <summary>
        /// Applies a queued attack-state transition and its unit participation changes.
        /// </summary>
        /// <param name="state">The requested attack state.</param>
        public void ApplyState(TacticalCombatAttackRunState state)
        {
            TacticalCombatAttackRunState previous = State;
            State = state;
            _transitionPending = false;
            if (state == TacticalCombatAttackRunState.Starting)
            {
                _entryPools.Clear();
                foreach (TacticalCombatUnit unit in Group.Members)
                {
                    _entryPools.Add(unit, new Pools(unit.Systems.Hull, unit.Systems.Shields));
                    unit.Status = TacticalCombatUnitStatus.AttackingBattleStation;
                    UnitParticipationChanged?.Invoke(unit);
                }
                RemainingMilliseconds = 120000;
                SelectSurvivor();
            }
            else if (state == TacticalCombatAttackRunState.Succeeded)
            {
                int count = Group.Members.Count;
                int losses =
                    count > 1
                        ? (count & ~1) < 3
                            ? 1
                            : count >> 1
                        : 0;
                TacticalCombatUnit[] members = new TacticalCombatUnit[count];
                for (int index = 0; index < count; index++)
                    members[index] = Group.Members[index];
                for (int index = 0; index < members.Length; index++)
                    if (index < losses)
                        UnitDestroyed?.Invoke(members[index]);
                    else
                        Restore(members[index]);
                Group.SetOrder(TacticalCombatOrder.None);
            }
            else if (state == TacticalCombatAttackRunState.Failed)
            {
                foreach (TacticalCombatUnit member in new List<TacticalCombatUnit>(Group.Members))
                    UnitDestroyed?.Invoke(member);
            }
            else if (state == TacticalCombatAttackRunState.Cancelled)
            {
                if (previous != TacticalCombatAttackRunState.Approaching)
                    foreach (TacticalCombatUnit unit in Group.Members)
                        Restore(unit);
                Group.SetOrder(TacticalCombatOrder.Withdraw);
            }
        }

        /// <summary>
        /// Selects attack success through ordered maneuver-loss and torpedo-command checks.
        /// </summary>
        private void SelectSurvivor()
        {
            _candidates.Clear();
            for (int index = 0; index < Group.Members.Count; index++)
                _candidates.Add(Group.Members[index]);
            int attempts = 0;
            while (_candidates.Count > 0)
            {
                TacticalCombatUnit unit = _candidates[0];
                _candidates.RemoveAt(0);
                int maneuver = (int)unit.GetManeuverability();
                if (maneuver < _random.NextInt(1, 11))
                {
                    int fighters = (int)(unit.Systems.Hull / 8);
                    unit.Systems.ApplyHullLoss(
                        fighters <= 1 ? 100000 : _random.NextInt(1, fighters + 1) * 8
                    );
                }
                if (unit.Systems.Hull > 0)
                {
                    float threshold = (float)(
                        (double)unit.GetTorpedoDamage() + Math.Min(_commandBudget(), 9)
                    );
                    int roll = _random.NextInt(1, 101);
                    if (roll <= (uint)(threshold * 0.5f))
                    {
                        State = TacticalCombatAttackRunState.Succeeding;
                        RemainingMilliseconds = 120000;
                        foreach (TacticalCombatUnit member in Group.Members)
                            RestorePools(member);
                        _candidates.Clear();
                        return;
                    }
                }
                if (++attempts > 1000)
                    break;
                if (unit.Systems.Hull > 0)
                    _candidates.Insert(0, unit);
            }
            State = TacticalCombatAttackRunState.Failing;
            RemainingMilliseconds = 120000;
            _candidates.Clear();
        }

        /// <summary>
        /// Queues a transition while preventing the same phase from issuing duplicate commands.
        /// </summary>
        /// <param name="state">The requested attack state.</param>
        private void RequestState(TacticalCombatAttackRunState state)
        {
            _transitionPending = true;
            StateChangeRequested?.Invoke(this, state);
        }

        /// <summary>
        /// Restores a returning squadron's entry pools and spatial participation.
        /// </summary>
        /// <param name="unit">The returning squadron.</param>
        private void Restore(TacticalCombatUnit unit)
        {
            RestorePools(unit);
            unit.Status = TacticalCombatUnitStatus.Active;
            UnitParticipationChanged?.Invoke(unit);
        }

        /// <summary>
        /// Restores the hull and shields captured when the squadron entered its attack sequence.
        /// </summary>
        /// <param name="unit">The squadron whose pools are restored.</param>
        private void RestorePools(TacticalCombatUnit unit)
        {
            if (_entryPools.TryGetValue(unit, out Pools pools))
                unit.Systems.RestorePools(pools.Hull, pools.Shields);
        }

        /// <summary>
        /// Captures the hull and shields hidden during attack-run selection.
        /// </summary>
        private readonly struct Pools
        {
            public float Hull { get; }
            public float Shields { get; }

            /// <summary>
            /// Captures a squadron's attack-entry pools.
            /// </summary>
            /// <param name="hull">The remaining hull.</param>
            /// <param name="shields">The current shields.</param>
            public Pools(float hull, float shields)
            {
                Hull = hull;
                Shields = shields;
            }
        }
    }
}
