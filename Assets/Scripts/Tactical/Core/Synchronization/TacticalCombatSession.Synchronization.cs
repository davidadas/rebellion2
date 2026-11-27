using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.ShipComponents;
using Rebellion.Tactical.Synchronization;

namespace Rebellion.Tactical
{
    public sealed partial class TacticalCombatSession
    {
        private bool _synchronizationAuthority;
        private bool _synchronizationReplica;
        private bool _applyingSynchronizedCommand;
        private readonly bool[] _synchronizedCommandsSuspended = new bool[2];
        private readonly TacticalCombatSelection[] _synchronizedSelections =
            new TacticalCombatSelection[2];
        private readonly List<TacticalCombatEvent> _synchronizedEvents = new();
        private long _capturedSnapshotSequence = -1;
        private long _appliedSnapshotSequence = -1;
        private int _synchronizedSystemTime = 3;
        private int _synchronizedMovieID;
        private TacticalCombatBriefingSequence _remoteNotificationBriefing;
        private CombatSide? _synchronizedNotificationRecipient;
        private bool _synchronizedEntryPresentation;
        internal bool IsRefreshingSynchronizedPresentation { get; private set; }
        public bool IsReplica => _synchronizationReplica;
        public bool IsSynchronized => _synchronizationAuthority || _synchronizationReplica;
        public int PendingSynchronizedMovieID =>
            PendingAttackRunMovie == null ? 0 : _synchronizedMovieID;
        public event Action<TacticalCombatCommand> SynchronizedCommandRequested;

        /// <summary>
        /// Admits one simulation owner while retaining independent command contexts for both factions.
        /// </summary>
        public void ConfigureSynchronizedAuthority()
        {
            ConfigureSynchronization(false);
            ShotFired += shot =>
                RecordSynchronizedEvent(
                    new()
                    {
                        Kind = TacticalCombatEventKind.Shot,
                        SourceID = shot.Source.InstanceID,
                        TargetID = shot.Target.InstanceID,
                        Weapon = shot.Weapon,
                        Strength = shot.Strength,
                    }
                );
            ImpactApplied += (unit, weapon, impact) =>
                RecordSynchronizedEvent(
                    new()
                    {
                        Kind = TacticalCombatEventKind.Impact,
                        TargetID = unit.InstanceID,
                        Weapon = weapon,
                        Impact = impact,
                    }
                );
            DestructionEffectRequested += unit =>
                RecordSynchronizedEvent(
                    new() { Kind = TacticalCombatEventKind.Destruction, TargetID = unit.InstanceID }
                );
            UnitDestroyed += unit =>
                RecordSynchronizedEvent(
                    new() { Kind = TacticalCombatEventKind.Destroyed, TargetID = unit.InstanceID }
                );
            UnitWithdrawn += unit =>
                RecordSynchronizedEvent(
                    new() { Kind = TacticalCombatEventKind.Withdrawn, TargetID = unit.InstanceID }
                );
            FighterRecovered += unit =>
                RecordSynchronizedEvent(
                    new() { Kind = TacticalCombatEventKind.Recovered, TargetID = unit.InstanceID }
                );
            SuperlaserFired += (source, target) =>
                RecordSynchronizedEvent(
                    new()
                    {
                        Kind = TacticalCombatEventKind.Superlaser,
                        SourceID = source.InstanceID,
                        TargetID = target.InstanceID,
                    }
                );
            Audio.NotificationRequested += CaptureSynchronizedNotification;
            Audio.SoundRequested += CaptureSynchronizedFiringSound;
            CombatSide remote =
                PlayerSide == CombatSide.Attacker ? CombatSide.Defender : CombatSide.Attacker;
            _remoteNotificationBriefing = new TacticalCombatBriefingSequence(
                GetSide(remote).FactionIndex,
                message => EnqueueFactionNotification(remote, message)
            );
            TractorSourcesChanged += (unit, populated) =>
                RecordSynchronizedEvent(
                    new()
                    {
                        Kind = TacticalCombatEventKind.TractorSources,
                        TargetID = unit.InstanceID,
                        Enabled = populated,
                    }
                );
            GravityWellPresentationRequested += unit =>
                RecordSynchronizedEvent(
                    new() { Kind = TacticalCombatEventKind.GravityWell, TargetID = unit.InstanceID }
                );
        }

        /// <summary>
        /// Admits a passive view that accepts authoritative snapshots and submits semantic local input.
        /// </summary>
        public void ConfigureSynchronizedReplica() => ConfigureSynchronization(true);

        /// <summary>
        /// Selects the synchronization ownership model before either combat clock advances.
        /// </summary>
        /// <param name="replica">Whether this encounter is a passive view.</param>
        private void ConfigureSynchronization(bool replica)
        {
            if (IsSynchronized || _scheduler != null || SystemTime != 3)
                throw new InvalidOperationException(
                    "Synchronized combat ownership must be configured before playback starts."
                );
            _synchronizationReplica = replica;
            _synchronizationAuthority = !replica;
            _synchronizedSelections[(int)PlayerSide] = Selection;
            foreach (TacticalCombatSide side in _sides)
            {
                _synchronizedSelections[(int)side.Side] ??= new TacticalCombatSelection(
                    this,
                    side.Side
                );
            }
        }

        /// <summary>
        /// Routes local semantic input without applying it to a projected simulation.
        /// </summary>
        /// <param name="command">The semantic input being submitted.</param>
        /// <returns>Whether the synchronization boundary owns this operation.</returns>
        internal bool RequestSynchronizedCommand(TacticalCombatCommand command)
        {
            if (!IsSynchronized || _applyingSynchronizedCommand)
                return false;
            SynchronizedCommandRequested?.Invoke(command);
            return true;
        }

        /// <summary>
        /// Checks direct command authority for one authenticated encounter faction.
        /// </summary>
        /// <param name="side">The faction being inspected.</param>
        /// <returns>Whether this faction retains direct command authority.</returns>
        public bool IsSideDirectControlEnabled(CombatSide side) =>
            side is CombatSide.Attacker or CombatSide.Defender
            && !(
                IsSynchronized
                    ? _synchronizedCommandsSuspended[(int)side]
                    : _directCommandsSuspended
            )
            && !IsArriving
            && !GetSide(side).IsAutomated;

        /// <summary>
        /// Suspends only the faction whose withdrawal was admitted by the command owner.
        /// </summary>
        /// <param name="side">The admitted withdrawing faction.</param>
        internal void SuspendDirectCommands(CombatSide side)
        {
            if (IsSynchronized)
                _synchronizedCommandsSuspended[(int)side] = true;
            else
                SuspendDirectCommands();
        }

        /// <summary>
        /// Resolves the authoritative command context belonging to one faction.
        /// </summary>
        /// <param name="side">The authenticated encounter faction.</param>
        /// <returns>The faction's independent command context.</returns>
        private TacticalCombatSelection GetSynchronizedSelection(CombatSide side) =>
            _synchronizedSelections[(int)side];

        /// <summary>
        /// Validates and applies authenticated semantic input through the existing selection interpreter.
        /// </summary>
        /// <param name="authenticatedSide">The faction established by the authenticated transport.</param>
        /// <param name="command">The semantic input without an actor override.</param>
        /// <returns>Whether the command passed admission and was applied.</returns>
        public bool ApplySynchronizedCommand(
            CombatSide authenticatedSide,
            TacticalCombatCommand command
        )
        {
            if (
                !_synchronizationAuthority
                || IsComplete
                || PendingAttackRunMovie != null
                || command == null
                || authenticatedSide is not CombatSide.Attacker and not CombatSide.Defender
                || !Enum.IsDefined(typeof(TacticalCombatCommandKind), command.Kind)
            )
                return false;
            TacticalCombatSelection selection = GetSynchronizedSelection(authenticatedSide);
            if (
                command.Kind == TacticalCombatCommandKind.Object && GetUnit(command.UnitID) == null
                || command.Kind == TacticalCombatCommandKind.Waypoint
                    && !Navigation.Waypoints.Contains(command.Waypoint.ToVector())
                || command.Kind == TacticalCombatCommandKind.ObjectCategory
                    && (uint)command.Category >= 8
                || command.Kind == TacticalCombatCommandKind.FighterCategory
                    && (uint)command.Category >= 4
                || command.Kind == TacticalCombatCommandKind.Order
                    && !Enum.IsDefined(typeof(TacticalCombatOrder), command.Order)
                || command.Kind == TacticalCombatCommandKind.Formation
                    && !Enum.IsDefined(typeof(TacticalCombatFormationKind), command.Formation)
                || command.Kind == TacticalCombatCommandKind.StationTarget
                    && BattleStation?.Unit.Side != authenticatedSide
            )
                return false;
            _applyingSynchronizedCommand = true;
            try
            {
                switch (command.Kind)
                {
                    case TacticalCombatCommandKind.Object:
                        selection.QueueObject(GetUnit(command.UnitID), command.Extend);
                        break;
                    case TacticalCombatCommandKind.Waypoint:
                        selection.QueueWaypoint(command.Waypoint.ToVector(), command.Extend);
                        break;
                    case TacticalCombatCommandKind.Clear:
                        selection.QueueClear();
                        break;
                    case TacticalCombatCommandKind.DisplayMode:
                        selection.QueueDisplayMode(command.Next);
                        break;
                    case TacticalCombatCommandKind.ObjectCategory:
                        selection.QueueObjectCategory(command.Category);
                        break;
                    case TacticalCombatCommandKind.FighterCategory:
                        selection.QueueFighterCategory(command.Category);
                        break;
                    case TacticalCombatCommandKind.NearestTarget:
                        selection.QueueNearestTarget();
                        break;
                    case TacticalCombatCommandKind.Order:
                        if (
                            !selection.CanIssueCommands
                            || command.Order
                                is TacticalCombatOrder.Recover
                                    or TacticalCombatOrder.AttackBattleStation
                                && selection.Kind != TacticalCombatSelectionKind.FighterGroup
                            || command.Order == TacticalCombatOrder.AttackBattleStation
                                && !CanAttackBattleStation(authenticatedSide)
                        )
                            return false;
                        selection.RequestOrder(command.Order);
                        break;
                    case TacticalCombatCommandKind.AttackMode:
                        if (!selection.CanIssueCommands)
                            return false;
                        selection.RequestAttackMode(command.CloseAttack);
                        break;
                    case TacticalCombatCommandKind.Formation:
                        if (
                            !selection.CanIssueCommands
                            || selection.Kind == TacticalCombatSelectionKind.SingleObject
                        )
                            return false;
                        selection.RequestFormation(command.Formation);
                        break;
                    case TacticalCombatCommandKind.ToggleAutomation:
                        if (GetSide(authenticatedSide).IsWithdrawing)
                            return false;
                        selection.QueueToggleAutomation();
                        break;
                    case TacticalCombatCommandKind.Withdrawal:
                        if (!CanWithdraw(authenticatedSide))
                            return false;
                        selection.QueueWithdrawal();
                        break;
                    case TacticalCombatCommandKind.StationTarget:
                        selection.QueueStationTarget();
                        break;
                    case TacticalCombatCommandKind.Pause:
                        if (IsArriving)
                            return false;
                        _userPaused = command.Paused;
                        break;
                    case TacticalCombatCommandKind.ImmediateResult:
                        return false;
                }
                selection.ProcessPendingCommands();
                return true;
            }
            finally
            {
                _applyingSynchronizedCommand = false;
            }
        }

        /// <summary>
        /// Advances local entry transforms without advancing combat clocks, damage, movement, or planning.
        /// </summary>
        /// <param name="elapsedMilliseconds">The unscaled local presentation interval.</param>
        /// <returns>Whether local entry has completed.</returns>
        public bool AdvanceSynchronizedArrival(float elapsedMilliseconds)
        {
            if (!IsSynchronized)
                throw new InvalidOperationException(
                    "Synchronized entry requires configured combat ownership."
                );
            _synchronizedEntryPresentation = true;
            try
            {
                return AdvanceArrival(elapsedMilliseconds);
            }
            finally
            {
                _synchronizedEntryPresentation = false;
            }
        }

        /// <summary>
        /// Refreshes the local selection and notification view without advancing combat state.
        /// </summary>
        public void AdvanceLocalSelection()
        {
            IsRefreshingSynchronizedPresentation = true;
            try
            {
                Selection.ProcessPendingCommands();
            }
            finally
            {
                IsRefreshingSynchronizedPresentation = false;
            }
        }

        /// <summary>
        /// Releases an authoritative movie pause only for the currently admitted request.
        /// </summary>
        /// <param name="movieID">The acknowledged movie identity.</param>
        /// <returns>Whether this acknowledgement completed the current movie.</returns>
        public bool CompleteSynchronizedMovie(int movieID)
        {
            if (!_synchronizationAuthority || movieID <= 0 || movieID != PendingSynchronizedMovieID)
                return false;
            PendingAttackRunMovie.Complete();
            return true;
        }

        /// <summary>
        /// Captures the final authoritative outcome for the shared campaign commit boundary.
        /// </summary>
        /// <returns>The participant outcomes and terminal flags.</returns>
        public TacticalCombatOutcome CaptureSynchronizedOutcome()
        {
            if (!_synchronizationAuthority)
                throw new InvalidOperationException(
                    "Only the combat authority can capture final outcomes."
                );
            return CaptureOutcome();
        }

        /// <summary>
        /// Captures a complete view update and consumes only its already emitted presentation edges.
        /// </summary>
        /// <param name="sequence">The strictly increasing authoritative snapshot identity.</param>
        /// <returns>The detached serializable combat projection.</returns>
        public TacticalCombatSnapshot CaptureSnapshot(long sequence)
        {
            if (!_synchronizationAuthority || sequence <= _capturedSnapshotSequence)
                throw new InvalidOperationException(
                    "Combat snapshots require authority and increasing sequence identities."
                );
            foreach (TacticalCombatSelection selection in _synchronizedSelections)
                selection.RefreshSynchronizedContext();
            TacticalCombatSnapshot snapshot = new()
            {
                Sequence = sequence,
                SystemTime = SystemTime,
                Paused = IsPaused,
                Complete = IsComplete,
                Winner = Winner,
                AttackerOutcome = _outcomes[0],
                DefenderOutcome = _outcomes[1],
                StationCharge = BattleStation?.Charge ?? 0,
                StationDeparted = BattleStation?.HasDeparted ?? false,
                MovieID = PendingSynchronizedMovieID,
                MovieGroupID = PendingAttackRunMovie?.AttackRun.Group.ID ?? 0,
                MovieSide = PendingAttackRunMovie?.AttackRun.Group.Side ?? default,
                MovieUnitID =
                    PendingAttackRunMovie == null
                        ? null
                        : _units
                            .First(unit =>
                                unit.IsFighter
                                && unit.Side == PendingAttackRunMovie.AttackRun.Group.Side
                            )
                            .InstanceID,
                MovieCategory = PendingAttackRunMovie?.AttackRun.Group.FighterCategory ?? 0,
                MovieState = PendingAttackRunMovie?.AttackRun.State ?? default,
                Units = _units.Select(CaptureUnitSnapshot).ToArray(),
                Groups = _groupsByID
                    .Values.OrderBy(group => unchecked((uint)group.ID))
                    .Select(CaptureGroupSnapshot)
                    .ToArray(),
                Sides = _sides
                    .Select(side => new TacticalCombatSideSnapshot
                    {
                        Side = side.Side,
                        Automated = side.IsAutomated,
                        CommandsSuspended = _synchronizedCommandsSuspended[(int)side.Side],
                        Withdrawing = side.IsWithdrawing,
                        WithdrawalMilliseconds = side.WithdrawalMilliseconds,
                        HasWithdrawalPhase = side.WithdrawalPhase.HasValue,
                        WithdrawalPhase = side.WithdrawalPhase ?? default,
                        AttackBudget = side.AttackBudget,
                    })
                    .ToArray(),
                Selections = _sides
                    .Select(side => GetSynchronizedSelection(side.Side).CaptureSnapshot())
                    .ToArray(),
                Effects = _synchronizedEvents.ToArray(),
            };
            _capturedSnapshotSequence = sequence;
            _synchronizedEvents.Clear();
            return snapshot;
        }

        /// <summary>
        /// Captures one participant's visible systems and transform without campaign references.
        /// </summary>
        /// <param name="unit">The registered participant.</param>
        /// <returns>The participant projection.</returns>
        private static TacticalCombatUnitSnapshot CaptureUnitSnapshot(CombatUnit unit)
        {
            TacticalCombatUnitSnapshot state = new()
            {
                UnitID = unit.InstanceID,
                Position = new(unit.Position),
                Heading = new(unit.Heading),
                Velocity = new(unit.Velocity),
                DesiredHeading = new(unit.DesiredHeading),
                DesiredMovement = new(unit.DesiredMovement),
                Status = unit.Status,
                CarrierID = unit.CarrierID,
                Hull = unit.Hull,
                Shields = unit.Shields,
                ShieldLimit = unit.MaximumShields,
                AttackDelay = unit.AttackDelay,
                MovementDelay = unit.MovementDelay,
                ComponentDamage = Enumerable
                    .Range(0, 5)
                    .Select(index => unit.GetDamage((TacticalShipSystem)index))
                    .ToArray(),
                HyperdriveDamage = unit.CaptureDriveDamage(),
                Charge = Enumerable
                    .Range(0, 4)
                    .Select(index => unit.GetBatteryCharge((TacticalWeaponArc)index))
                    .ToArray(),
                Ready = Enumerable
                    .Range(0, 4)
                    .Select(index => unit.IsBatteryReady((TacticalWeaponArc)index))
                    .ToArray(),
                HyperspaceMilliseconds = unit.HyperspaceMilliseconds,
                TractorRestraint = unit.TractorRestraint,
                HasTractorSources = unit.HasTractorSources,
                CommandBudget = unit.CommandBudget,
                TurnRate = unit.TurnRate,
                HasCollision = unit.HasCollision,
            };
            state.WeaponRechargeSample = unit.WeaponRechargeSample;
            state.ShieldRechargeSample = unit.ShieldRechargeSample;
            return state;
        }

        /// <summary>
        /// Captures a group together with its retained category ownership.
        /// </summary>
        /// <param name="group">The registered requested or applied group.</param>
        /// <returns>The group projection.</returns>
        private TacticalCombatGroupSnapshot CaptureGroupSnapshot(TacticalCombatGroup group)
        {
            TacticalCombatGroupSnapshot state = group.CaptureSnapshot();
            state.Applied = _groups.Contains(group);
            for (int category = 0; category < 8; category++)
                if (ReferenceEquals(_objectGroups[(int)group.Side, category], group))
                    state.ObjectCategory = category;
            for (int category = 0; category < 4; category++)
                if (ReferenceEquals(_fighterGroups[(int)group.Side, category], group))
                    state.FighterSlot = category;
            return state;
        }

        /// <summary>
        /// Finds a requested or applied group by its stable battle identity.
        /// </summary>
        /// <param name="groupID">The stable battle group identity.</param>
        /// <returns>The local group projection, or null when absent.</returns>
        internal TacticalCombatGroup GetSynchronizedGroup(int groupID) =>
            _groupsByID.TryGetValue(groupID, out TacticalCombatGroup group) ? group : null;

        /// <summary>
        /// Retains a presentation edge in its authoritative order until the next captured snapshot.
        /// </summary>
        /// <param name="effect">The detached presentation edge.</param>
        private void RecordSynchronizedEvent(TacticalCombatEvent effect) =>
            _synchronizedEvents.Add(effect);

        /// <summary>
        /// Sends the hardpoint's sound name to the other combat view.
        /// </summary>
        /// <param name="name">The sound named by the firing hardpoint.</param>
        private void CaptureSynchronizedFiringSound(string name)
        {
            if (_synchronizedEntryPresentation || !Audio.Notifications.Enabled)
                return;
            RecordSynchronizedEvent(
                new() { Kind = TacticalCombatEventKind.FiringSound, SoundName = name }
            );
        }

        /// <summary>
        /// Captures shared sounds and addressed notifications while keeping selection loops local to each view.
        /// </summary>
        /// <param name="message">The authored message identity.</param>
        /// <param name="loop">Whether this is a local selection playback loop.</param>
        private void CaptureSynchronizedNotification(int message, bool loop)
        {
            if (loop || _synchronizedEntryPresentation || !Audio.Notifications.Enabled)
                return;
            RecordSynchronizedEvent(
                new()
                {
                    Kind = TacticalCombatEventKind.Notification,
                    MessageID = message,
                    HasRecipient = _synchronizedNotificationRecipient.HasValue,
                    Recipient = _synchronizedNotificationRecipient ?? default,
                }
            );
        }

        /// <summary>
        /// Routes a faction-specific authored cue to its owning presentation without using the other view's random stream.
        /// </summary>
        /// <param name="side">The faction receiving this cue.</param>
        /// <param name="message">The authored notification identity.</param>
        internal void EnqueueFactionNotification(CombatSide side, int message)
        {
            if (!_synchronizationAuthority || side == PlayerSide)
            {
                CombatSide? previous = _synchronizedNotificationRecipient;
                _synchronizedNotificationRecipient = IsSynchronized ? side : null;
                try
                {
                    Audio.EnqueueNotification(message);
                }
                finally
                {
                    _synchronizedNotificationRecipient = previous;
                }
            }
            else
                RecordSynchronizedEvent(
                    new()
                    {
                        Kind = TacticalCombatEventKind.Notification,
                        MessageID = message,
                        HasRecipient = true,
                        Recipient = side,
                    }
                );
        }

        /// <summary>
        /// Observes the same battle facts through the other faction's existing briefing sequence.
        /// </summary>
        /// <param name="elapsedMilliseconds">The authoritative unscaled interval.</param>
        private void AdvanceSynchronizedNotifications(float elapsedMilliseconds) =>
            _remoteNotificationBriefing?.Advance(
                elapsedMilliseconds,
                CaptureBattleStationAttackReadiness(),
                IsArriving
            );

        /// <summary>
        /// Resolves an attack cue's authored recipient index to its authenticated encounter side.
        /// </summary>
        /// <param name="faction">The authored recipient faction index.</param>
        /// <param name="message">The authored attack notification.</param>
        private void RouteSynchronizedAttackNotification(int faction, int message)
        {
            foreach (TacticalCombatSide side in _sides)
                if (side.FactionIndex == faction)
                    EnqueueFactionNotification(side.Side, message);
        }

        /// <summary>
        /// Rejects simulation mutation on a passive view.
        /// </summary>
        private void RequireSimulationAuthority()
        {
            if (_synchronizationReplica)
                throw new InvalidOperationException(
                    "A combat replica cannot advance or mutate the authoritative simulation."
                );
        }
    }
}
