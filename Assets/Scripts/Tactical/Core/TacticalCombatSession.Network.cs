using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;

namespace Rebellion.Tactical
{
    public sealed partial class TacticalCombatSession
    {
        private bool _networkAuthority;
        private bool _networkReplica;
        private bool _applyingNetworkCommand;
        private readonly bool[] _networkCommandsSuspended = new bool[2];
        private readonly TacticalCombatSelection[] _networkSelections = new TacticalCombatSelection[
            2
        ];
        private readonly List<TacticalCombatNetworkEffect> _networkEffects = new();
        private long _capturedNetworkSequence = -1;
        private long _appliedNetworkSequence = -1;
        private int _networkSystemTime = 3;
        private int _networkMovieID;
        private TacticalCombatBriefingSequence _remoteNotificationBriefing;
        private TacticalCombatEnding _remoteNotificationEnding;
        private CombatSide? _networkNotificationRecipient;
        private bool _networkEntryPresentation;
        internal bool IsRefreshingNetworkPresentation { get; private set; }
        public bool IsReplica => _networkReplica;
        public bool IsNetworked => _networkAuthority || _networkReplica;
        public int PendingNetworkMovieID => PendingAttackRunMovie == null ? 0 : _networkMovieID;
        public event Action<TacticalCombatNetworkCommand> NetworkCommandRequested;

        /// <summary>
        /// Admits one simulation owner while retaining independent command contexts for both factions.
        /// </summary>
        public void ConfigureNetworkAuthority()
        {
            ConfigureNetworkMode(false);
            ShotFired += shot =>
                RecordNetworkEffect(
                    new()
                    {
                        Kind = TacticalCombatNetworkEffectKind.Shot,
                        SourceID = shot.Source.InstanceID,
                        TargetID = shot.Target.InstanceID,
                        Weapon = shot.Weapon,
                        Strength = shot.Strength,
                    }
                );
            ImpactApplied += (unit, weapon, impact) =>
                RecordNetworkEffect(
                    new()
                    {
                        Kind = TacticalCombatNetworkEffectKind.Impact,
                        TargetID = unit.InstanceID,
                        Weapon = weapon,
                        Impact = impact,
                    }
                );
            DestructionEffectRequested += unit =>
                RecordNetworkEffect(
                    new()
                    {
                        Kind = TacticalCombatNetworkEffectKind.Destruction,
                        TargetID = unit.InstanceID,
                    }
                );
            UnitDestroyed += unit =>
                RecordNetworkEffect(
                    new()
                    {
                        Kind = TacticalCombatNetworkEffectKind.Destroyed,
                        TargetID = unit.InstanceID,
                    }
                );
            UnitWithdrawn += unit =>
                RecordNetworkEffect(
                    new()
                    {
                        Kind = TacticalCombatNetworkEffectKind.Withdrawn,
                        TargetID = unit.InstanceID,
                    }
                );
            FighterRecovered += unit =>
                RecordNetworkEffect(
                    new()
                    {
                        Kind = TacticalCombatNetworkEffectKind.Recovered,
                        TargetID = unit.InstanceID,
                    }
                );
            SuperlaserFired += (source, target) =>
                RecordNetworkEffect(
                    new()
                    {
                        Kind = TacticalCombatNetworkEffectKind.Superlaser,
                        SourceID = source.InstanceID,
                        TargetID = target.InstanceID,
                    }
                );
            Audio.NotificationRequested += CaptureNetworkNotification;
            CombatSide remote =
                PlayerSide == CombatSide.Attacker ? CombatSide.Defender : CombatSide.Attacker;
            _remoteNotificationBriefing = new TacticalCombatBriefingSequence(
                GetSide(remote).FactionIndex,
                message => EnqueueFactionNotification(remote, message)
            );
            _remoteNotificationEnding = new TacticalCombatEnding(
                GetSide(remote).FactionIndex,
                message => EnqueueFactionNotification(remote, message),
                () => { },
                () => { }
            );
            foreach (TacticalCombatUnit unit in _units)
            {
                unit.TractorSourcesChanged += populated =>
                    RecordNetworkEffect(
                        new()
                        {
                            Kind = TacticalCombatNetworkEffectKind.TractorSources,
                            TargetID = unit.InstanceID,
                            Enabled = populated,
                        }
                    );
                unit.GravityWellPresentationRequested += () =>
                    RecordNetworkEffect(
                        new()
                        {
                            Kind = TacticalCombatNetworkEffectKind.GravityWell,
                            TargetID = unit.InstanceID,
                        }
                    );
            }
        }

        /// <summary>
        /// Admits a passive view that accepts authoritative frames and submits semantic local input.
        /// </summary>
        public void ConfigureNetworkReplica() => ConfigureNetworkMode(true);

        /// <summary>
        /// Selects the network ownership model before either combat clock advances.
        /// </summary>
        /// <param name="replica">Whether this encounter is a passive view.</param>
        private void ConfigureNetworkMode(bool replica)
        {
            if (IsNetworked || _legacyTimeAdvanced || _scheduler != null || SystemTime != 3)
                throw new InvalidOperationException(
                    "Network combat ownership must be configured before playback starts."
                );
            _networkReplica = replica;
            _networkAuthority = !replica;
            _networkSelections[(int)PlayerSide] = Selection;
            foreach (TacticalCombatSide side in _sides)
            {
                _networkSelections[(int)side.Side] ??= new TacticalCombatSelection(this, side.Side);
            }
        }

        /// <summary>
        /// Routes local semantic input without applying it to a projected simulation.
        /// </summary>
        /// <param name="command">The semantic input being submitted.</param>
        /// <returns>Whether the network boundary owns this operation.</returns>
        internal bool RequestNetworkCommand(TacticalCombatNetworkCommand command)
        {
            if (!IsNetworked || _applyingNetworkCommand)
                return false;
            NetworkCommandRequested?.Invoke(command);
            return true;
        }

        /// <summary>
        /// Checks direct command authority for one authenticated encounter faction.
        /// </summary>
        /// <param name="side">The faction being inspected.</param>
        /// <returns>Whether this faction retains direct command authority.</returns>
        public bool IsSideDirectControlEnabled(CombatSide side) =>
            side is CombatSide.Attacker or CombatSide.Defender
            && !(IsNetworked ? _networkCommandsSuspended[(int)side] : _directCommandsSuspended)
            && !IsArriving
            && !GetSide(side).IsAutomated;

        /// <summary>
        /// Suspends only the faction whose withdrawal was admitted by the command owner.
        /// </summary>
        /// <param name="side">The admitted withdrawing faction.</param>
        internal void SuspendDirectCommands(CombatSide side)
        {
            if (IsNetworked)
                _networkCommandsSuspended[(int)side] = true;
            else
                SuspendDirectCommands();
        }

        /// <summary>
        /// Resolves the authoritative command context belonging to one faction.
        /// </summary>
        /// <param name="side">The authenticated encounter faction.</param>
        /// <returns>The faction's independent command context.</returns>
        private TacticalCombatSelection GetNetworkSelection(CombatSide side) =>
            _networkSelections[(int)side];

        /// <summary>
        /// Validates and applies authenticated semantic input through the existing selection interpreter.
        /// </summary>
        /// <param name="authenticatedSide">The faction established by the authenticated transport.</param>
        /// <param name="command">The semantic input without an actor override.</param>
        /// <returns>Whether the command passed admission and was applied.</returns>
        public bool ApplyNetworkCommand(
            CombatSide authenticatedSide,
            TacticalCombatNetworkCommand command
        )
        {
            if (
                !_networkAuthority
                || IsComplete
                || PendingAttackRunMovie != null
                || command == null
                || authenticatedSide is not CombatSide.Attacker and not CombatSide.Defender
                || !Enum.IsDefined(typeof(TacticalCombatNetworkCommandKind), command.Kind)
            )
                return false;
            TacticalCombatSelection selection = GetNetworkSelection(authenticatedSide);
            if (
                command.Kind == TacticalCombatNetworkCommandKind.Object
                    && GetUnit(command.UnitID) == null
                || command.Kind == TacticalCombatNetworkCommandKind.Waypoint
                    && !Navigation.Waypoints.Contains(command.Waypoint.ToVector())
                || command.Kind == TacticalCombatNetworkCommandKind.ObjectCategory
                    && (uint)command.Category >= 8
                || command.Kind == TacticalCombatNetworkCommandKind.FighterCategory
                    && (uint)command.Category >= 4
                || command.Kind == TacticalCombatNetworkCommandKind.Order
                    && !Enum.IsDefined(typeof(TacticalCombatOrder), command.Order)
                || command.Kind == TacticalCombatNetworkCommandKind.Formation
                    && !Enum.IsDefined(typeof(TacticalCombatFormationKind), command.Formation)
                || command.Kind == TacticalCombatNetworkCommandKind.StationTarget
                    && BattleStation?.Unit.Side != authenticatedSide
            )
                return false;
            _applyingNetworkCommand = true;
            try
            {
                switch (command.Kind)
                {
                    case TacticalCombatNetworkCommandKind.Object:
                        selection.QueueObject(GetUnit(command.UnitID), command.Extend);
                        break;
                    case TacticalCombatNetworkCommandKind.Waypoint:
                        selection.QueueWaypoint(command.Waypoint.ToVector(), command.Extend);
                        break;
                    case TacticalCombatNetworkCommandKind.Clear:
                        selection.QueueClear();
                        break;
                    case TacticalCombatNetworkCommandKind.DisplayMode:
                        selection.QueueDisplayMode(command.Next);
                        break;
                    case TacticalCombatNetworkCommandKind.ObjectCategory:
                        selection.QueueObjectCategory(command.Category);
                        break;
                    case TacticalCombatNetworkCommandKind.FighterCategory:
                        selection.QueueFighterCategory(command.Category);
                        break;
                    case TacticalCombatNetworkCommandKind.NearestTarget:
                        selection.QueueNearestTarget();
                        break;
                    case TacticalCombatNetworkCommandKind.Order:
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
                    case TacticalCombatNetworkCommandKind.AttackMode:
                        if (!selection.CanIssueCommands)
                            return false;
                        selection.RequestAttackMode(command.CloseAttack);
                        break;
                    case TacticalCombatNetworkCommandKind.Formation:
                        if (
                            !selection.CanIssueCommands
                            || selection.Kind == TacticalCombatSelectionKind.SingleObject
                        )
                            return false;
                        selection.RequestFormation(command.Formation);
                        break;
                    case TacticalCombatNetworkCommandKind.ToggleAutomation:
                        if (GetSide(authenticatedSide).IsWithdrawing)
                            return false;
                        selection.QueueToggleAutomation();
                        break;
                    case TacticalCombatNetworkCommandKind.Withdrawal:
                        if (!CanWithdraw(authenticatedSide))
                            return false;
                        selection.QueueWithdrawal();
                        break;
                    case TacticalCombatNetworkCommandKind.StationTarget:
                        selection.QueueStationTarget();
                        break;
                    case TacticalCombatNetworkCommandKind.Pause:
                        if (IsArriving)
                            return false;
                        _userPaused = command.Paused;
                        break;
                    case TacticalCombatNetworkCommandKind.ImmediateResult:
                        return false;
                }
                selection.ProcessPendingCommands();
                return true;
            }
            finally
            {
                _applyingNetworkCommand = false;
            }
        }

        /// <summary>
        /// Advances local entry transforms without advancing combat clocks, damage, movement, or planning.
        /// </summary>
        /// <param name="elapsedMilliseconds">The unscaled local presentation interval.</param>
        /// <returns>Whether local entry has completed.</returns>
        public bool AdvanceNetworkArrival(float elapsedMilliseconds)
        {
            if (!IsNetworked)
                throw new InvalidOperationException(
                    "Network entry requires configured combat ownership."
                );
            _networkEntryPresentation = true;
            try
            {
                return AdvanceArrival(elapsedMilliseconds);
            }
            finally
            {
                _networkEntryPresentation = false;
            }
        }

        /// <summary>
        /// Refreshes the local selection and notification view without advancing combat state.
        /// </summary>
        public void AdvanceLocalSelection()
        {
            IsRefreshingNetworkPresentation = true;
            try
            {
                Selection.ProcessPendingCommands();
            }
            finally
            {
                IsRefreshingNetworkPresentation = false;
            }
        }

        /// <summary>
        /// Releases an authoritative movie pause only for the currently admitted request.
        /// </summary>
        /// <param name="movieID">The acknowledged movie identity.</param>
        /// <returns>Whether this acknowledgement completed the current movie.</returns>
        public bool CompleteNetworkMovie(int movieID)
        {
            if (!_networkAuthority || movieID <= 0 || movieID != PendingNetworkMovieID)
                return false;
            PendingAttackRunMovie.Complete();
            return true;
        }

        /// <summary>
        /// Captures the final authoritative outcome for the shared campaign commit boundary.
        /// </summary>
        /// <returns>The participant outcomes and terminal flags.</returns>
        public TacticalCombatOutcome CaptureNetworkOutcome()
        {
            if (!_networkAuthority)
                throw new InvalidOperationException(
                    "Only the combat authority can capture final outcomes."
                );
            return CaptureOutcome();
        }

        /// <summary>
        /// Captures a complete view update and consumes only its already emitted presentation edges.
        /// </summary>
        /// <param name="sequence">The strictly increasing authoritative frame identity.</param>
        /// <returns>The detached serializable combat projection.</returns>
        public TacticalCombatNetworkFrame CaptureNetworkFrame(long sequence)
        {
            if (!_networkAuthority || sequence <= _capturedNetworkSequence)
                throw new InvalidOperationException(
                    "Combat frames require authority and increasing sequence identities."
                );
            foreach (TacticalCombatSelection selection in _networkSelections)
                selection.RefreshNetworkContext();
            TacticalCombatNetworkFrame frame = new()
            {
                Sequence = sequence,
                SystemTime = SystemTime,
                Paused = IsPaused,
                Complete = IsComplete,
                Winner = Winner,
                AttackerOutcome = _outcomes[0],
                DefenderOutcome = _outcomes[1],
                TerminalFlags = _ending.Flags.ToArray(),
                StationCharge = BattleStation?.Charge ?? 0,
                StationDeparted = BattleStation?.HasDeparted ?? false,
                MovieID = PendingNetworkMovieID,
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
                Units = _units.Select(CaptureNetworkUnit).ToArray(),
                Groups = _groupsByID
                    .Values.OrderBy(group => unchecked((uint)group.ID))
                    .Select(CaptureNetworkGroup)
                    .ToArray(),
                Sides = _sides
                    .Select(side => new TacticalCombatNetworkSideState
                    {
                        Side = side.Side,
                        Automated = side.IsAutomated,
                        CommandsSuspended = _networkCommandsSuspended[(int)side.Side],
                        Withdrawing = side.IsWithdrawing,
                        WithdrawalMilliseconds = side.WithdrawalMilliseconds,
                        HasWithdrawalPhase = side.WithdrawalPhase.HasValue,
                        WithdrawalPhase = side.WithdrawalPhase ?? default,
                        AttackBudget = side.AttackBudget,
                    })
                    .ToArray(),
                Selections = _sides
                    .Select(side => GetNetworkSelection(side.Side).CaptureNetworkState())
                    .ToArray(),
                Effects = _networkEffects.ToArray(),
            };
            _capturedNetworkSequence = sequence;
            _networkEffects.Clear();
            return frame;
        }

        /// <summary>
        /// Captures one participant's visible systems and transform without campaign references.
        /// </summary>
        /// <param name="unit">The registered participant.</param>
        /// <returns>The participant projection.</returns>
        private static TacticalCombatNetworkUnitState CaptureNetworkUnit(TacticalCombatUnit unit)
        {
            TacticalCombatNetworkUnitState state = new()
            {
                UnitID = unit.InstanceID,
                Position = new(unit.Position),
                Heading = new(unit.Heading),
                Velocity = new(unit.Velocity),
                DesiredHeading = new(unit.DesiredHeading),
                DesiredMovement = new(unit.DesiredMovement),
                Status = unit.Status,
                CarrierID = unit.CarrierID,
                TargetID = unit.TargetID,
                Hull = unit.Systems.Hull,
                Shields = unit.Systems.Shields,
                ShieldLimit = unit.Systems.MaximumShields,
                AttackDelay = unit.Systems.AttackDelay,
                MovementDelay = unit.Systems.MovementDelay,
                ComponentDamage = Enumerable
                    .Range(0, 5)
                    .Select(index => unit.Systems.GetDamage((TacticalShipSystem)index))
                    .ToArray(),
                Charge = Enumerable
                    .Range(0, 4)
                    .Select(index => unit.Weapons.GetCharge((TacticalWeaponArc)index))
                    .ToArray(),
                Ready = Enumerable
                    .Range(0, 4)
                    .Select(index => unit.Weapons.IsReady((TacticalWeaponArc)index))
                    .ToArray(),
                HyperspaceMilliseconds = unit.HyperspaceMilliseconds,
                TractorRestraint = unit.TractorRestraint,
                HasTractorSources = unit.HasTractorSources,
                CommandBudget = unit.CommandBudget,
                TurnRate = unit.TurnRate,
                HasCollision = unit.HasCollision,
                HasGroup = unit.GroupID.HasValue,
                GroupID = unit.GroupID ?? 0,
                HasRequestedGroup = unit.RequestedGroupID.HasValue,
                RequestedGroupID = unit.RequestedGroupID ?? 0,
            };
            unit.Systems.CaptureNetworkSamples(state);
            return state;
        }

        /// <summary>
        /// Captures a group together with its retained category ownership.
        /// </summary>
        /// <param name="group">The registered requested or applied group.</param>
        /// <returns>The group projection.</returns>
        private TacticalCombatNetworkGroupState CaptureNetworkGroup(TacticalCombatGroup group)
        {
            TacticalCombatNetworkGroupState state = group.CaptureNetworkState();
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
        internal TacticalCombatGroup GetNetworkGroup(int groupID) =>
            _groupsByID.TryGetValue(groupID, out TacticalCombatGroup group) ? group : null;

        /// <summary>
        /// Retains a presentation edge in its authoritative order until the next captured frame.
        /// </summary>
        /// <param name="effect">The detached presentation edge.</param>
        private void RecordNetworkEffect(TacticalCombatNetworkEffect effect) =>
            _networkEffects.Add(effect);

        /// <summary>
        /// Captures shared sounds and addressed notifications while keeping selection loops local to each view.
        /// </summary>
        /// <param name="message">The authored message identity.</param>
        /// <param name="loop">Whether this is a local selection playback loop.</param>
        private void CaptureNetworkNotification(int message, bool loop)
        {
            if (loop || _networkEntryPresentation || !Audio.Notifications.Enabled)
                return;
            RecordNetworkEffect(
                new()
                {
                    Kind = TacticalCombatNetworkEffectKind.Notification,
                    MessageID = message,
                    HasRecipient = _networkNotificationRecipient.HasValue,
                    Recipient = _networkNotificationRecipient ?? default,
                }
            );
        }

        /// <summary>
        /// Routes a faction-specific authored cue to its owning presentation without using the other peer's random stream.
        /// </summary>
        /// <param name="side">The faction receiving this cue.</param>
        /// <param name="message">The authored notification identity.</param>
        internal void EnqueueFactionNotification(CombatSide side, int message)
        {
            if (!_networkAuthority || side == PlayerSide)
            {
                CombatSide? previous = _networkNotificationRecipient;
                _networkNotificationRecipient = IsNetworked ? side : null;
                try
                {
                    Audio.EnqueueNotification(message);
                }
                finally
                {
                    _networkNotificationRecipient = previous;
                }
            }
            else
                RecordNetworkEffect(
                    new()
                    {
                        Kind = TacticalCombatNetworkEffectKind.Notification,
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
        private void AdvanceRemoteNotifications(float elapsedMilliseconds) =>
            _remoteNotificationBriefing?.Advance(elapsedMilliseconds, CaptureBriefingState());

        /// <summary>
        /// Resolves an attack cue's authored recipient index to its authenticated encounter side.
        /// </summary>
        /// <param name="faction">The authored recipient faction index.</param>
        /// <param name="message">The authored attack notification.</param>
        private void RouteNetworkAttackNotification(int faction, int message)
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
            if (_networkReplica)
                throw new InvalidOperationException(
                    "A combat replica cannot advance or mutate the authoritative simulation."
                );
        }
    }
}
