using System;
using System.Collections.Generic;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Tactical.Synchronization;

namespace Rebellion.Tactical
{
    public sealed partial class TacticalCombatSession
    {
        /// <summary>
        /// Checks withdrawal eligibility against the live battle's gravity wells and movement systems.
        /// </summary>
        /// <param name="side">The side requesting withdrawal.</param>
        /// <returns>Whether a legal route and at least one mobile hyperdrive unit remain.</returns>
        public bool CanWithdraw(CombatSide side)
        {
            if (side is not (CombatSide.Attacker or CombatSide.Defender))
                return false;
            TacticalCombatSide state = GetSide(side);
            if (!state.HasWithdrawalDestination || state.IsWithdrawing)
                return false;
            foreach (WithdrawalMember member in _orderedWithdrawalMembers)
                if (
                    member.Unit.Side != side
                    && !member.Unit.IsFighter
                    && IsDisplayedMember(member.Unit)
                    && member.Unit.HasGravityWell
                )
                    return false;
            for (int pass = 0; pass < 2; pass++)
                foreach (WithdrawalMember member in _orderedWithdrawalMembers)
                    if (
                        member.Unit.Side == side
                        && member.Unit.IsFighter == (pass == 0)
                        && IsDisplayedMember(member.Unit)
                        && member.CheckWithdrawalReadiness()
                    )
                        return true;
            return state.FactionIndex == 1
                && BattleStation != null
                && (BattleStation.IsOperational || !BattleStation.IsConstructionComplete);
        }

        /// <summary>
        /// Checks player departure admission and announces the first unready capital before confirmation opens.
        /// </summary>
        /// <returns>Whether the player may open the withdrawal confirmation.</returns>
        public bool PreparePlayerWithdrawal()
        {
            if (!CanWithdraw(PlayerSide))
                return false;
            foreach (WithdrawalMember member in _orderedWithdrawalMembers)
                if (
                    member.Unit.Side == PlayerSide
                    && !member.Unit.IsFighter
                    && IsDisplayedMember(member.Unit)
                    && !member.CheckWithdrawalReadiness()
                )
                {
                    Audio.EnqueueNotification(GetSide(PlayerSide).FactionIndex == 0 ? 149 : 270);
                    break;
                }
            return true;
        }

        /// <summary>
        /// Rechecks the confirmed player departure before queuing its semantic input command.
        /// Dispatch rechecks current availability when the queued command is drained.
        /// </summary>
        /// <returns>Whether the withdrawal command was queued for the player.</returns>
        public bool ConfirmPlayerWithdrawal()
        {
            if (!CanWithdraw(PlayerSide))
                return false;
            Selection.QueueWithdrawal();
            return true;
        }

        /// <summary>
        /// Queues automatic continuation through the same ordered input boundary as withdrawal.
        /// </summary>
        /// <returns>Whether the current encounter admits the request.</returns>
        public bool RequestImmediateResult()
        {
            if (!CanRequestImmediateResult)
                return false;
            Selection.QueueImmediateResult();
            return true;
        }

        /// <summary>
        /// Rechecks queued continuation before releasing the tactical encounter and its command authority.
        /// </summary>
        internal void ApplyImmediateResult()
        {
            if (!CanRequestImmediateResult)
                return;
            SuspendDirectCommands();
            _ending.FinishWithoutAssessment();
        }

        /// <summary>
        /// Checks station protection, attack availability, and current squadron superiority.
        /// </summary>
        /// <param name="side">The side requesting a fighter attack.</param>
        /// <returns>Whether a battle-station attack is currently available.</returns>
        public bool CanAttackBattleStation(CombatSide side)
        {
            if (
                BattleStation?.CanReceiveAttackRun != true
                || BattleStation.Unit.Side == side
                || GetSide(side).FactionIndex != 0
                || GetSide(side).IsWithdrawing
            )
                return false;
            bool hasAttackRun = false;
            foreach (TacticalCombatGroup group in _groups)
                hasAttackRun |= IsBattleStationAttackOrdered(group);
            int own = 0;
            int opposing = 0;
            foreach (CombatUnit unit in _units)
                if (unit.IsFighter && unit.IsEngaged)
                {
                    if (unit.Side == side)
                        own++;
                    else
                        opposing++;
                }
            return TacticalCombatBattleStation.CanStartAttackRun(
                BattleStation.CanReceiveAttackRun,
                hasAttackRun,
                own,
                opposing
            );
        }

        private static bool IsBattleStationAttackOrdered(TacticalCombatGroup group) =>
            group.Order == TacticalCombatOrder.AttackBattleStation
            || group.ActiveOrder == TacticalCombatOrder.AttackBattleStation;

        /// <summary>
        /// Queues the superlaser visual and delayed destruction for an admitted opposing target.
        /// </summary>
        /// <param name="target">The opposing tactical unit.</param>
        /// <returns>Whether the target resolves within this encounter.</returns>
        public bool FireSuperlaser(CombatUnit target)
        {
            RequireSimulationAuthority();
            if (
                BattleStation == null
                || target == null
                || target.Side == BattleStation.Unit.Side
                || !_unitsByID.TryGetValue(target.InstanceID, out CombatUnit registered)
                || !ReferenceEquals(target, registered)
            )
                return false;
            BattleStation.Fire();
            QueueCommand(new Command(CommandKind.Superlaser, BattleStation.Unit, target));
            QueueCommand(new Command(CommandKind.Destroy, target, BattleStation.Unit), 1);
            return true;
        }

        /// <summary>
        /// Starts the side's departure countdown and assigns withdrawal or recovery orders.
        /// </summary>
        /// <param name="side">The side leaving the encounter.</param>
        /// <returns>Whether withdrawal was started.</returns>
        public bool BeginWithdrawal(CombatSide side)
        {
            RequireSimulationAuthority();
            if (!CanWithdraw(side))
                return false;
            ApplyWithdrawal(side);
            return true;
        }

        /// <summary>
        /// Suspends direct player orders while preserving camera and inspection ownership.
        /// </summary>
        internal void SuspendDirectCommands() => _directCommandsSuspended = true;

        /// <summary>
        /// Applies a withdrawal admitted by the caller's current eligibility check.
        /// </summary>
        /// <param name="side">The side whose admitted departure is being applied.</param>
        internal void ApplyWithdrawal(CombatSide side)
        {
            TacticalCombatSide state = GetSide(side);
            state.Withdrawal = new TacticalCombatWithdrawal(
                state.FactionIndex,
                state.InitialWithdrawalMilliseconds,
                () =>
                    (
                        _synchronizationAuthority ? GetSynchronizedSelection(side) : Selection
                    ).ProcessStationDeparture(),
                () => QueueCommand(new Command(CommandKind.Withdraw, BattleStation.Unit))
            );
            if (side == PlayerSide || _synchronizationAuthority)
                EnqueueFactionNotification(side, state.FactionIndex == 0 ? 143 : 269);
            PrepareWithdrawalGroups(side);
            state.Withdrawal.SelectInitialDelay(BattleStation?.IsOperational == true);
        }

        /// <summary>
        /// Changes retained group labels before requesting a separate departure group for each deployed capital.
        /// </summary>
        /// <param name="side">The side whose admitted withdrawal is being prepared.</param>
        private void PrepareWithdrawalGroups(CombatSide side)
        {
            List<TacticalCombatGroup> groups = new();
            foreach (TacticalCombatGroup group in _groups)
                if (group.Side == side)
                    groups.Add(group);
            groups.Sort(CompareGroupRegistration);
            foreach (TacticalCombatGroup group in groups)
            {
                if (!group.IsFighterGroup)
                {
                    SetOrder(group, TacticalCombatOrder.Withdraw);
                    continue;
                }
                if (side == PlayerSide || _synchronizationAuthority)
                    ClearGroupCategories(group);
                if (group.Members.Count == 0)
                    continue;
                if (
                    group.ActiveOrder == TacticalCombatOrder.AttackBattleStation
                    && group.AttackRun != null
                )
                    RequestAttackRunState(group.AttackRun, TacticalCombatAttackRunState.Cancelled);
                SetOrder(
                    group,
                    group.Members[0].HasHyperdrive
                        ? TacticalCombatOrder.Withdraw
                        : TacticalCombatOrder.Recover
                );
            }
            foreach (WithdrawalMember member in _orderedWithdrawalMembers)
            {
                CombatUnit unit = member.Unit;
                if (unit.Side != side || unit.IsFighter || !IsDisplayedMember(unit))
                    continue;
                TacticalCombatGroup applied = GetGroup(unit);
                if (applied != null)
                {
                    if (
                        (applied.Side == PlayerSide || _synchronizationAuthority)
                        && !HasUntrackedRequestedOwner(unit)
                    )
                        ClearGroupCategories(applied);
                    RemoveGroupMember(applied, unit);
                }
                TacticalCombatGroup departure = CreateGroup(new[] { unit });
                SetOrder(departure, TacticalCombatOrder.Withdraw);
            }
        }

        /// <summary>
        /// Checks whether requested ownership lies outside the player's retained object categories.
        /// </summary>
        /// <param name="unit">The deployed capital whose secondary ownership is inspected.</param>
        /// <returns>Whether a nonempty requested owner is not retained in an object category.</returns>
        private bool HasUntrackedRequestedOwner(CombatUnit unit)
        {
            TacticalCombatGroup requested = GetRequestedGroup(unit);
            if (requested == null)
                return false;
            for (int category = 0; category < 8; category++)
                if (
                    ReferenceEquals(
                        _objectGroups[
                            (int)(_synchronizationAuthority ? unit.Side : PlayerSide),
                            category
                        ],
                        requested
                    )
                )
                    return false;
            return true;
        }

        /// <summary>
        /// Compares retained group labels by unsigned battle registration identity.
        /// </summary>
        /// <param name="first">The first retained group.</param>
        /// <param name="second">The second retained group.</param>
        /// <returns>The ordered comparison of their registration keys.</returns>
        private static int CompareGroupRegistration(
            TacticalCombatGroup first,
            TacticalCombatGroup second
        ) => unchecked((uint)first.ID).CompareTo(unchecked((uint)second.ID));

        /// <summary>
        /// Changes one group's behavior while releasing recovery reservations owned by its previous order.
        /// </summary>
        /// <param name="group">The group receiving the command.</param>
        /// <param name="order">The requested behavior.</param>
        public void SetOrder(TacticalCombatGroup group, TacticalCombatOrder order)
        {
            RequireSimulationAuthority();
            if (
                !_groupsByID.TryGetValue(group.ID, out TacticalCombatGroup existing)
                || !ReferenceEquals(existing, group)
            )
                throw new ArgumentException("The group is outside this encounter.", nameof(group));
            if (
                group.Side != PlayerSide
                && GetSide(group.Side).IsAutomated
                && order != TacticalCombatOrder.None
                && (
                    group.Order
                        is TacticalCombatOrder.Recover
                            or TacticalCombatOrder.AttackBattleStation
                    || group.ActiveOrder
                        is TacticalCombatOrder.Recover
                            or TacticalCombatOrder.AttackBattleStation
                )
            )
                return;
            if (
                order == TacticalCombatOrder.AttackBattleStation
                && (!group.IsFighterGroup || !CanAttackBattleStation(group.Side))
            )
                throw new InvalidOperationException(
                    "A battle-station attack is unavailable for this group."
                );
            group.SetOrder(order);
        }

        /// <summary>
        /// Applies a delayed group behavior and its recovery or attack-run lifecycle changes.
        /// </summary>
        /// <param name="group">The registered group.</param>
        /// <param name="order">The due behavior.</param>
        private void ApplyOrder(TacticalCombatGroup group, TacticalCombatOrder order)
        {
            if (
                group.AttackRun?.IsInProgress == true
                && order != TacticalCombatOrder.AttackBattleStation
            )
                group.AttackRun.ApplyState(TacticalCombatAttackRunState.Cancelled);
            if (group.ActiveOrder == TacticalCombatOrder.Recover)
                foreach (CombatUnit unit in group.Members)
                    ReleaseRecovery(unit);
            group.ApplyOrder(order);
            if (
                group.IsFighterGroup
                && order is TacticalCombatOrder.Recover or TacticalCombatOrder.AttackBattleStation
            )
                ClearGroupCategories(group);
            _pendingManeuvers.Remove(group);
            _pendingAttackOrders.Remove(group);
            if (
                order
                is TacticalCombatOrder.AttackFighters
                    or TacticalCombatOrder.AttackCapitalShips
            )
                _pendingAttackOrders.Add(group);
            if (
                order
                is TacticalCombatOrder.LeftHook
                    or TacticalCombatOrder.RightHook
                    or TacticalCombatOrder.Hammer
                    or TacticalCombatOrder.Anvil
            )
                _pendingManeuvers.Add(group);
            if (order == TacticalCombatOrder.AttackBattleStation)
            {
                DetachAttackRun(group);
                TacticalCombatAttackRun run = new(
                    group,
                    _random,
                    () => GetSide(group.Side).GetCommandBudget(true),
                    GetSide(PlayerSide).FactionIndex
                );
                run.StateChangeRequested += RequestAttackRunState;
                run.MovieRequested += RequestAttackRunMovie;
                run.OrderRequested += SetOrder;
                run.UnitDestroyed += Destroy;
                run.UnitParticipationChanged += _spatial.Update;
                run.NotificationRequested += RequestAttackRunNotification;
                if (_synchronizationAuthority)
                    run.ConfigureSynchronizedNotifications(RouteSynchronizedAttackNotification);
                group.AttackRun = run;
                ActiveAttackRun = run;
                AttackRunChanged?.Invoke(run);
            }
        }

        /// <summary>
        /// Queues the next phase of a fighter group's staged station attack.
        /// </summary>
        /// <param name="run">The group's active attack sequence.</param>
        /// <param name="state">The requested phase.</param>
        private void RequestAttackRunState(
            TacticalCombatAttackRun run,
            TacticalCombatAttackRunState state
        ) => QueueCommand(new Command(CommandKind.AttackRun, null, run: run, runState: state));

        /// <summary>
        /// Routes one owned attack-run cue through the selected faction's shared notification queue.
        /// </summary>
        /// <param name="messageID">The authored command message.</param>
        private void RequestAttackRunNotification(int messageID) =>
            EnqueueFactionNotification(PlayerSide, messageID);

        /// <summary>
        /// Releases every callback owned by a removed or replaced attack helper.
        /// </summary>
        /// <param name="group">The group whose previous helper is being detached.</param>
        private void DetachAttackRun(TacticalCombatGroup group)
        {
            TacticalCombatAttackRun run = group.AttackRun;
            if (run == null)
                return;
            run.StateChangeRequested -= RequestAttackRunState;
            run.MovieRequested -= RequestAttackRunMovie;
            run.OrderRequested -= SetOrder;
            run.UnitDestroyed -= Destroy;
            run.UnitParticipationChanged -= _spatial.Update;
            run.NotificationRequested -= RequestAttackRunNotification;
            run.ConfigureSynchronizedNotifications(null);
        }

        /// <summary>
        /// Applies the station outcome before beginning its modal movie pause.
        /// </summary>
        /// <param name="run">The attack whose conclusion is launching a movie.</param>
        private void RequestAttackRunMovie(TacticalCombatAttackRun run)
        {
            if (run.State == TacticalCombatAttackRunState.Succeeded && BattleStation != null)
                Destroy(BattleStation.Unit);
            _moviePauses++;
            TacticalCombatMovieRequest request = new(run, CompleteAttackRunMovie);
            PendingAttackRunMovie = request;
            if (_synchronizationAuthority)
            {
                _synchronizedMovieID++;
                RecordSynchronizedEvent(
                    new() { Kind = TacticalCombatEventKind.Movie, MovieID = _synchronizedMovieID }
                );
            }
            bool notificationsEnabled = Audio.Notifications.Enabled;
            Audio.Notifications.Enabled = false;
            try
            {
                AttackRunMovieRequested?.Invoke(request);
            }
            finally
            {
                Audio.Notifications.Enabled = notificationsEnabled;
            }
        }

        /// <summary>
        /// Releases only the pause owned by the movie that completed or was cancelled.
        /// </summary>
        /// <param name="request">The completed movie's ownership token.</param>
        private void CompleteAttackRunMovie(TacticalCombatMovieRequest request)
        {
            _moviePauses = Math.Max(0, _moviePauses - 1);
            if (ReferenceEquals(PendingAttackRunMovie, request))
                PendingAttackRunMovie = null;
        }
    }
}
