using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Game.Combat;
using Rebellion.Util.Random;

namespace Rebellion.Tactical
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
        private readonly int _playerFactionIndex;
        private Action<int, int> _networkNotifications;
        private readonly List<CombatUnit> _candidates = new();
        private readonly Dictionary<CombatUnit, Pools> _entryPools = new();
        private bool _transitionPending;
        private bool _approachAnnounced;
        private bool _candidateDecayed;
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
        public event Action<CombatUnit> UnitDestroyed;
        public event Action<CombatUnit> UnitParticipationChanged;
        public event Action<TacticalCombatAttackRun, int> ProgressChanged;
        public event Action<int> NotificationRequested;
        public event Action<TacticalCombatAttackRun> MovieRequested;
        public event Action<TacticalCombatGroup, TacticalCombatOrder> OrderRequested;

        /// <summary>
        /// Routes both factions' existing attack cues through the shared presentation authority.
        /// </summary>
        /// <param name="notifications">The recipient-faction and authored-message sink.</param>
        internal void ConfigureSynchronizedNotifications(Action<int, int> notifications) =>
            _networkNotifications = notifications;

        /// <summary>
        /// Emits one faction's authored cue without changing the attack's random decisions.
        /// </summary>
        /// <param name="faction">The authored recipient faction index.</param>
        /// <param name="message">The authored message identity.</param>
        private void NotifyFaction(int faction, int message)
        {
            if (_networkNotifications != null)
                _networkNotifications(faction, message);
            else if (_playerFactionIndex == faction)
                NotificationRequested?.Invoke(message);
        }

        /// <summary>
        /// Emits the existing paired command cues for the two recipient factions.
        /// </summary>
        /// <param name="alliance">The Alliance message.</param>
        /// <param name="empire">The Empire message.</param>
        private void NotifyFactions(int alliance, int empire)
        {
            NotifyFaction(0, alliance);
            NotifyFaction(1, empire);
        }

        /// <summary>
        /// Restores visible attack progress without advancing or selecting a local outcome.
        /// </summary>
        /// <param name="state">The authoritative attack state.</param>
        /// <param name="remainingMilliseconds">The stored attack countdown.</param>
        internal void ApplySnapshot(TacticalCombatAttackRunState state, float remainingMilliseconds)
        {
            State = state;
            RemainingMilliseconds = remainingMilliseconds;
        }

        /// <summary>
        /// Creates the attack sequence after an eligible fighter group receives its order.
        /// </summary>
        /// <param name="group">The attacking fighter group.</param>
        /// <param name="random">The battle's attack-selection stream.</param>
        /// <param name="commandBudget">The live fighter command budget.</param>
        /// <param name="playerFactionIndex">The faction whose command notifications are selected.</param>
        public TacticalCombatAttackRun(
            TacticalCombatGroup group,
            IRandomNumberProvider random,
            Func<float> commandBudget,
            int playerFactionIndex
        )
        {
            Group = group ?? throw new ArgumentNullException(nameof(group));
            this._random = random ?? throw new ArgumentNullException(nameof(random));
            this._commandBudget =
                commandBudget ?? throw new ArgumentNullException(nameof(commandBudget));
            if (playerFactionIndex is not 0 and not 1)
                throw new ArgumentOutOfRangeException(nameof(playerFactionIndex));
            _playerFactionIndex = playerFactionIndex;
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
            AnnounceApproach();
            if (
                _transitionPending
                || State
                    is not TacticalCombatAttackRunState.Succeeding
                        and not TacticalCombatAttackRunState.Failing
            )
                return;
            if (_progressNotice < 9 && RemainingMilliseconds <= 120000 - _progressNotice * 12000)
            {
                int notice = _progressNotice++;
                int? message = GetProgressMessage(notice);
                if (message.HasValue)
                    NotifyFaction(0, message.Value);
                ProgressChanged?.Invoke(this, notice);
            }
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
                AnnounceApproach();
                _entryPools.Clear();
                foreach (CombatUnit unit in Group.Members)
                {
                    _entryPools.Add(unit, new Pools(unit.Systems.Hull, unit.Systems.Shields));
                    unit.Status = CombatUnitStatus.AttackingBattleStation;
                    UnitParticipationChanged?.Invoke(unit);
                }
                RemainingMilliseconds = 120000;
                _progressNotice = 0;
                NotifyFactions(GetCategoryMessage(314, 302, 306, 310), 285);
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
                CombatUnit[] members = new CombatUnit[count];
                for (int index = 0; index < count; index++)
                    members[index] = Group.Members[index];
                for (int index = 0; index < members.Length; index++)
                    if (index < losses)
                        UnitDestroyed?.Invoke(members[index]);
                    else
                        Restore(members[index]);
                MovieRequested?.Invoke(this);
                OrderRequested?.Invoke(Group, TacticalCombatOrder.None);
            }
            else if (state == TacticalCombatAttackRunState.Failed)
            {
                NotifyFaction(0, GetCategoryMessage(315, 303, 307, 311));
                MovieRequested?.Invoke(this);
                foreach (CombatUnit member in new List<CombatUnit>(Group.Members))
                    UnitDestroyed?.Invoke(member);
            }
            else if (state == TacticalCombatAttackRunState.Cancelled)
            {
                if (previous != TacticalCombatAttackRunState.Approaching)
                    foreach (CombatUnit unit in Group.Members)
                        Restore(unit);
                OrderRequested?.Invoke(Group, TacticalCombatOrder.Withdraw);
            }
        }

        /// <summary>
        /// Selects attack success through ordered maneuver-loss and torpedo-command checks.
        /// </summary>
        private void SelectSurvivor()
        {
            _candidateDecayed = false;
            _candidates.Clear();
            for (int index = 0; index < Group.Members.Count; index++)
                _candidates.Add(Group.Members[index]);
            int attempts = 0;
            while (_candidates.Count > 0)
            {
                CombatUnit unit = _candidates[0];
                _candidates.RemoveAt(0);
                int maneuver = (int)unit.GetManeuverability();
                if (maneuver < _random.NextInt(1, 11))
                {
                    _candidateDecayed = true;
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
                        foreach (CombatUnit member in Group.Members)
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
        /// Requests the approach cue once before this helper's first update or attack entry.
        /// </summary>
        private void AnnounceApproach()
        {
            if (_approachAnnounced)
                return;
            _approachAnnounced = true;
            NotifyFactions(GetCategoryMessage(313, 301, 305, 309), 277);
        }

        /// <summary>
        /// Resolves the fighter category's command message while retaining its registration label.
        /// </summary>
        /// <param name="first">The first category's message.</param>
        /// <param name="second">The second category's message.</param>
        /// <param name="third">The third category's message.</param>
        /// <param name="fourth">The fourth category's message.</param>
        /// <returns>The addressed category's message.</returns>
        private int GetCategoryMessage(int first, int second, int third, int fourth) =>
            Group.FighterCategory switch
            {
                0 => first,
                1 => second,
                2 => third,
                _ => fourth,
            };

        /// <summary>
        /// Selects a timed Alliance progress cue from the chosen outcome and candidate-loss sequence.
        /// Empire command notifications have no timed progress schedule.
        /// </summary>
        /// <param name="index">The due progress threshold, from zero through eight.</param>
        /// <returns>The progress message, or null for a silent threshold.</returns>
        private int? GetProgressMessage(int index)
        {
            bool losses = _candidateDecayed || State == TacticalCombatAttackRunState.Failing;
            return index switch
            {
                0 => 286,
                1 => losses ? 291 : 287,
                2 => losses ? 292 : 288,
                3 => losses ? 293 : 296,
                4 => losses ? 298 : 289,
                5 => losses ? 299 : 290,
                6 => losses ? 294 : 297,
                7 => State == TacticalCombatAttackRunState.Failing ? 300 : null,
                8 => State == TacticalCombatAttackRunState.Failing ? 295 : null,
                _ => null,
            };
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
        private void Restore(CombatUnit unit)
        {
            RestorePools(unit);
            unit.Status = CombatUnitStatus.Active;
            UnitParticipationChanged?.Invoke(unit);
        }

        /// <summary>
        /// Restores the hull and shields captured when the squadron entered its attack sequence.
        /// </summary>
        /// <param name="unit">The squadron whose pools are restored.</param>
        private void RestorePools(CombatUnit unit)
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
