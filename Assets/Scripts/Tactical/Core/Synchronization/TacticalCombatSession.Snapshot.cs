using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Tactical.Synchronization;

namespace Rebellion.Tactical
{
    public sealed partial class TacticalCombatSession
    {
        private TacticalCombatMovieRequest _unannouncedSynchronizedMovie;

        /// <summary>
        /// Announces a validated replica projection before participant state changes.
        /// </summary>
        internal event Action<TacticalCombatSnapshot> SnapshotApplying;

        /// <summary>
        /// Restores replaceable authority state while retaining movie announcements for reliable effects.
        /// </summary>
        /// <param name="snapshot">The complete state without presentation effects.</param>
        public void ApplySnapshot(TacticalCombatSnapshot snapshot)
        {
            if (snapshot?.Effects?.Length != 0)
                throw new ArgumentException(
                    "Replaceable state cannot contain presentation effects.",
                    nameof(snapshot)
                );
            if (!_synchronizationReplica)
                throw new InvalidOperationException(
                    "Only a combat replica can receive an authoritative projection."
                );
            ValidateSnapshot(snapshot);
            SnapshotApplying?.Invoke(snapshot);
            bool completed = IsComplete;
            bool withdrew = _synchronizedCommandsSuspended[(int)PlayerSide];
            int?[] oldCategories = _objectGroups
                .Cast<TacticalCombatGroup>()
                .Concat(_fighterGroups.Cast<TacticalCombatGroup>())
                .Select(group => group?.ID)
                .ToArray();
            foreach (TacticalCombatSideSnapshot state in snapshot.Sides)
            {
                _sides[(int)state.Side].ApplySnapshot(state);
                _synchronizedCommandsSuspended[(int)state.Side] = state.CommandsSuspended;
            }
            HashSet<int> retained = snapshot.Groups.Select(state => state.GroupID).ToHashSet();
            foreach (int id in _groupsByID.Keys.Where(id => !retained.Contains(id)).ToArray())
                _groupsByID.Remove(id);
            _groups.Clear();
            Array.Clear(_objectGroups, 0, _objectGroups.Length);
            Array.Clear(_fighterGroups, 0, _fighterGroups.Length);
            foreach (TacticalCombatGroupSnapshot state in snapshot.Groups)
            {
                if (!_groupsByID.TryGetValue(state.GroupID, out TacticalCombatGroup group))
                {
                    group = TacticalCombatGroup.CreateSnapshotGroup(state, this);
                    _groupsByID.Add(state.GroupID, group);
                }
                group.ApplySnapshot(state, this);
                if (state.Applied)
                    _groups.Add(group);
                if (state.ObjectCategory >= 0)
                    _objectGroups[(int)state.Side, state.ObjectCategory] = group;
                if (state.FighterSlot >= 0)
                    _fighterGroups[(int)state.Side, state.FighterSlot] = group;
                if (state.HasAttackRun)
                {
                    group.AttackRun ??= new TacticalCombatAttackRun(
                        group,
                        _random,
                        () => GetSide(group.Side).GetCommandBudget(true),
                        GetSide(PlayerSide).FactionIndex
                    );
                    group.AttackRun.ApplySnapshot(
                        state.AttackRunState,
                        state.AttackRunMilliseconds
                    );
                }
                else
                    group.AttackRun = null;
            }
            foreach (TacticalCombatUnitSnapshot state in snapshot.Units)
            {
                CombatUnit unit = GetUnit(state.UnitID);
                unit.GroupID = null;
                unit.RequestedGroupID = null;
                ApplySnapshotUnit(unit, state);
            }
            foreach (TacticalCombatGroupSnapshot state in snapshot.Groups)
            {
                TacticalCombatGroup group = _groupsByID[state.GroupID];
                foreach (CombatUnit unit in group.Members)
                    unit.GroupID = group.ID;
                foreach (CombatUnit unit in group.RequestedMembers)
                    unit.RequestedGroupID = group.ID;
            }
            _synchronizedSystemTime = snapshot.SystemTime;
            _userPaused = snapshot.Paused;
            IsComplete = snapshot.Complete;
            Winner = snapshot.Winner;
            _outcomes[0] = snapshot.AttackerOutcome;
            _outcomes[1] = snapshot.DefenderOutcome;
            _ending.ApplySnapshotFlags(snapshot.TerminalFlags);
            BattleStation?.ApplySnapshot(snapshot.StationCharge, snapshot.StationDeparted);
            foreach (TacticalCombatSelectionSnapshot state in snapshot.Selections)
                GetSynchronizedSelection(state.Side).ApplySnapshot(state);
            TacticalCombatAttackRun previousRun = ActiveAttackRun;
            ActiveAttackRun = _groups
                .Select(group => group.AttackRun)
                .FirstOrDefault(run => run?.IsInProgress == true);
            if (!ReferenceEquals(previousRun, ActiveAttackRun))
                AttackRunChanged?.Invoke(ActiveAttackRun);
            if (snapshot.MovieID != 0 && snapshot.MovieID != _synchronizedMovieID)
            {
                _synchronizedMovieID = snapshot.MovieID;
                TacticalCombatGroup group =
                    GetSynchronizedGroup(snapshot.MovieGroupID)
                    ?? TacticalCombatGroup.CreateSynchronizedMovieGroup(
                        snapshot.MovieGroupID,
                        GetUnit(snapshot.MovieUnitID),
                        this
                    );
                group.FighterCategory = snapshot.MovieCategory;
                TacticalCombatAttackRun run =
                    group.AttackRun
                    ?? new TacticalCombatAttackRun(
                        group,
                        _random,
                        () => GetSide(group.Side).GetCommandBudget(true),
                        GetSide(PlayerSide).FactionIndex
                    );
                run.ApplySnapshot(snapshot.MovieState, 0);
                PendingAttackRunMovie = new TacticalCombatMovieRequest(run, _ => { });
                _unannouncedSynchronizedMovie = PendingAttackRunMovie;
            }
            else if (snapshot.MovieID == 0 && PendingAttackRunMovie != null)
            {
                PendingAttackRunMovie.Complete();
                PendingAttackRunMovie = null;
                _unannouncedSynchronizedMovie = null;
            }
            _appliedSnapshotSequence = snapshot.Sequence;
            if (!withdrew && _synchronizedCommandsSuspended[(int)PlayerSide])
                Selection.ApplySynchronizedWithdrawal();
            if (
                !oldCategories.SequenceEqual(
                    _objectGroups
                        .Cast<TacticalCombatGroup>()
                        .Concat(_fighterGroups.Cast<TacticalCombatGroup>())
                        .Select(group => group?.ID)
                )
            )
                GroupCategoryChanged?.Invoke();
            if (!completed && IsComplete)
                Completed?.Invoke();
        }

        /// <summary>
        /// Replays one ordered reliable batch independently of the replaceable state cadence.
        /// The connection owns batch sequencing and duplicate suppression.
        /// </summary>
        /// <param name="effects">The ordered presentation edges whose participant state is already available.</param>
        public void ApplySynchronizedEvents(TacticalCombatEvent[] effects)
        {
            if (!_synchronizationReplica)
                throw new InvalidOperationException(
                    "Only a combat replica can receive presentation effects."
                );
            ValidateSynchronizedEvents(effects);
            foreach (TacticalCombatEvent effect in effects)
                if (effect.Kind == TacticalCombatEventKind.Movie)
                {
                    if (
                        effect.MovieID == _synchronizedMovieID
                        && _unannouncedSynchronizedMovie != null
                    )
                        AnnounceSynchronizedMovie();
                }
                else
                    ApplySynchronizedEvent(effect);
        }

        /// <summary>
        /// Transfers one retained movie announcement to its local presentation exactly once.
        /// </summary>
        private void AnnounceSynchronizedMovie()
        {
            TacticalCombatMovieRequest request = _unannouncedSynchronizedMovie;
            _unannouncedSynchronizedMovie = null;
            AttackRunMovieRequested?.Invoke(request);
        }

        /// <summary>
        /// Restores one participant after all snapshot identities have passed validation.
        /// </summary>
        /// <param name="unit">The local registered participant.</param>
        /// <param name="state">The authoritative participant state.</param>
        private static void ApplySnapshotUnit(CombatUnit unit, TacticalCombatUnitSnapshot state)
        {
            unit.Position = state.Position.ToVector();
            unit.Heading = state.Heading.ToVector();
            unit.Velocity = state.Velocity.ToVector();
            unit.DesiredHeading = state.DesiredHeading.ToVector();
            unit.DesiredMovement = state.DesiredMovement.ToVector();
            unit.Status = state.Status;
            unit.CarrierID = string.IsNullOrEmpty(state.CarrierID) ? null : state.CarrierID;
            unit.ApplySystemProjection(
                state.Hull,
                state.Shields,
                state.ShieldLimit,
                state.AttackDelay,
                state.MovementDelay,
                state.WeaponRechargeSample,
                state.ShieldRechargeSample,
                state.ComponentDamage
            );
            unit.ApplyBatteryProjection(state.Charge, state.Ready);
            unit.HyperspaceMilliseconds = state.HyperspaceMilliseconds;
            unit.TractorRestraint = state.TractorRestraint;
            unit.HasTractorSources = state.HasTractorSources;
            unit.CommandBudget = state.CommandBudget;
            unit.TurnRate = state.TurnRate;
            unit.HasCollision = state.HasCollision;
        }

        /// <summary>
        /// Replays an authoritative presentation edge without reproducing its gameplay operation.
        /// </summary>
        /// <param name="effect">The validated ordered edge.</param>
        private void ApplySynchronizedEvent(TacticalCombatEvent effect)
        {
            CombatUnit source = GetUnit(effect.SourceID);
            CombatUnit target = GetUnit(effect.TargetID);
            switch (effect.Kind)
            {
                case TacticalCombatEventKind.Shot:
                    ShotFired?.Invoke(
                        new TacticalCombatShot(source, target, effect.Weapon, effect.Strength)
                    );
                    break;
                case TacticalCombatEventKind.Impact:
                    ImpactApplied?.Invoke(target, effect.Weapon, effect.Impact);
                    break;
                case TacticalCombatEventKind.Destruction:
                    DestructionEffectRequested?.Invoke(target);
                    break;
                case TacticalCombatEventKind.Destroyed:
                    UnitDestroyed?.Invoke(target);
                    break;
                case TacticalCombatEventKind.Withdrawn:
                    UnitWithdrawn?.Invoke(target);
                    break;
                case TacticalCombatEventKind.Recovered:
                    FighterRecovered?.Invoke(target);
                    break;
                case TacticalCombatEventKind.Superlaser:
                    SuperlaserFired?.Invoke(source, target);
                    break;
                case TacticalCombatEventKind.TractorSources:
                    SetTractorSources(target, effect.Enabled);
                    break;
                case TacticalCombatEventKind.GravityWell:
                    GravityWellPresentationRequested?.Invoke(target);
                    break;
                case TacticalCombatEventKind.Notification:
                    if (!effect.HasRecipient || effect.Recipient == PlayerSide)
                        Audio.EnqueueNotification(effect.MessageID, effect.Enabled);
                    break;
            }
        }

        /// <summary>
        /// Rejects stale or malformed projections before any local presentation state changes.
        /// </summary>
        /// <param name="snapshot">The proposed complete projection.</param>
        private void ValidateSnapshot(TacticalCombatSnapshot snapshot)
        {
            if (
                snapshot == null
                || snapshot.Sequence <= _appliedSnapshotSequence
                || snapshot.SystemTime < 3
                || snapshot.Units == null
                || snapshot.Units.Length != _units.Count
                || snapshot.Groups == null
                || snapshot.Sides?.Length != 2
                || snapshot.Selections?.Length != 2
                || snapshot.TerminalFlags?.Length != 7
                || snapshot.Effects == null
                || !Enum.IsDefined(typeof(SpaceCombatResult.BattleOutcome), snapshot.Winner)
                || !Enum.IsDefined(typeof(SpaceCombatResult.SideOutcome), snapshot.AttackerOutcome)
                || !Enum.IsDefined(typeof(SpaceCombatResult.SideOutcome), snapshot.DefenderOutcome)
            )
                throw new ArgumentException(
                    "The combat projection is stale or incomplete.",
                    nameof(snapshot)
                );
            HashSet<string> participants = new(StringComparer.Ordinal);
            HashSet<int> groups = new();
            HashSet<string> activeMembers = new(StringComparer.Ordinal);
            HashSet<string> requestedMembers = new(StringComparer.Ordinal);
            foreach (TacticalCombatGroupSnapshot state in snapshot.Groups)
            {
                if (
                    state == null
                    || !groups.Add(state.GroupID)
                    || !IsSynchronizedSide(state.Side)
                    || !_units.Any(unit => unit.Side == state.Side)
                    || state.Members == null
                    || state.RequestedMembers == null
                    || state.Targets == null
                    || state.RequestedTargets == null
                    || state.ComputedTargets == null
                    || state.Waypoints == null
                    || state.RequestedWaypoints == null
                    || state.ObjectCategory is < -1 or > 7
                    || state.FighterSlot is < -1 or > 3
                    || !Enum.IsDefined(typeof(TacticalCombatGroupKind), state.Kind)
                    || !Enum.IsDefined(typeof(TacticalCombatOrder), state.Order)
                    || !Enum.IsDefined(typeof(TacticalCombatOrder), state.ActiveOrder)
                    || !Enum.IsDefined(typeof(TacticalCombatFormationKind), state.Formation)
                    || !Enum.IsDefined(
                        typeof(TacticalCombatFormationKind),
                        state.RequestedFormation
                    )
                    || state.HasAttackRun
                        && (
                            state.Kind != TacticalCombatGroupKind.Fighter
                            || !Enum.IsDefined(
                                typeof(TacticalCombatAttackRunState),
                                state.AttackRunState
                            )
                        )
                )
                    throw new ArgumentException(
                        "The combat group projection is invalid.",
                        nameof(snapshot)
                    );
                TacticalCombatGroup existing = GetSynchronizedGroup(state.GroupID);
                if (
                    existing != null
                    && (existing.Side != state.Side || existing.Kind != state.Kind)
                )
                    throw new ArgumentException(
                        "The combat group identity has changed ownership.",
                        nameof(snapshot)
                    );
                RequireSnapshotUnits(state.Members, state.Side);
                RequireSnapshotUnits(state.RequestedMembers, state.Side);
                if (
                    state.Members.Any(member => !activeMembers.Add(member))
                    || state.RequestedMembers.Any(member => !requestedMembers.Add(member))
                )
                    throw new ArgumentException(
                        "The combat projection assigns a participant to multiple groups.",
                        nameof(snapshot)
                    );
                RequireSnapshotUnits(state.Targets);
                RequireSnapshotUnits(state.RequestedTargets);
                RequireSnapshotUnits(state.ComputedTargets);
                RequireSnapshotUnit(state.PrimaryMemberID);
                RequireSnapshotUnit(state.RequestedLeaderID);
            }
            foreach (TacticalCombatUnitSnapshot state in snapshot.Units)
            {
                if (
                    state == null
                    || !participants.Add(state.UnitID)
                    || GetUnit(state.UnitID) == null
                    || state.ComponentDamage?.Length != 5
                    || state.Charge?.Length != 4
                    || state.Ready?.Length != 4
                    || !Enum.IsDefined(typeof(CombatUnitStatus), state.Status)
                )
                    throw new ArgumentException(
                        "The combat participant projection is invalid.",
                        nameof(snapshot)
                    );
                RequireSnapshotUnit(state.CarrierID);
            }
            HashSet<CombatSide> sides = new();
            foreach (TacticalCombatSideSnapshot state in snapshot.Sides)
                if (
                    state == null
                    || !IsSynchronizedSide(state.Side)
                    || !sides.Add(state.Side)
                    || state.HasWithdrawalPhase
                        && !Enum.IsDefined(
                            typeof(TacticalCombatSide.DeparturePhase),
                            state.WithdrawalPhase
                        )
                )
                    throw new ArgumentException(
                        "The combat faction projection is invalid.",
                        nameof(snapshot)
                    );
            sides.Clear();
            foreach (TacticalCombatSelectionSnapshot state in snapshot.Selections)
            {
                if (
                    state == null
                    || !IsSynchronizedSide(state.Side)
                    || !sides.Add(state.Side)
                    || !Enum.IsDefined(typeof(TacticalCombatSelectionKind), state.Kind)
                    || state.HasGroup && !groups.Contains(state.GroupID)
                    || state.Members == null
                    || state.Targets == null
                    || state.Waypoints == null
                    || state.Groups?.Any(id => !groups.Contains(id)) != false
                )
                    throw new ArgumentException(
                        "The combat selection projection is invalid.",
                        nameof(snapshot)
                    );
                RequireSnapshotUnits(state.Members);
                RequireSnapshotUnits(state.Targets);
                RequireSnapshotUnit(state.PrimaryID);
                RequireSnapshotUnit(state.InspectedID);
            }
            if (
                snapshot.MovieID < 0
                || snapshot.MovieID != 0
                    && (
                        GetUnit(snapshot.MovieUnitID) is not CombatUnit movieUnit
                        || !movieUnit.IsFighter
                        || movieUnit.Side != snapshot.MovieSide
                        || snapshot.MovieState
                            is not TacticalCombatAttackRunState.Succeeded
                                and not TacticalCombatAttackRunState.Failed
                    )
            )
                throw new ArgumentException(
                    "The combat movie projection is invalid.",
                    nameof(snapshot)
                );
        }

        /// <summary>
        /// Validates every reliable edge before any edge in its batch can change presentation.
        /// </summary>
        /// <param name="effects">The proposed ordered batch.</param>
        private void ValidateSynchronizedEvents(TacticalCombatEvent[] effects)
        {
            if (effects == null)
                throw new ArgumentNullException(nameof(effects));
            foreach (TacticalCombatEvent effect in effects)
            {
                if (
                    effect == null
                    || !Enum.IsDefined(typeof(TacticalCombatEventKind), effect.Kind)
                    || effect.HasRecipient && !IsSynchronizedSide(effect.Recipient)
                    || effect.Kind is TacticalCombatEventKind.Shot or TacticalCombatEventKind.Impact
                        && !Enum.IsDefined(typeof(TacticalWeaponType), effect.Weapon)
                    || effect.Kind == TacticalCombatEventKind.Impact
                        && !Enum.IsDefined(typeof(TacticalCombatImpactKind), effect.Impact)
                    || effect.Kind == TacticalCombatEventKind.Notification
                        && (uint)effect.MessageID >= 317
                    || effect.Kind == TacticalCombatEventKind.Movie && effect.MovieID <= 0
                )
                    throw new ArgumentException(
                        "The combat effect projection is invalid.",
                        nameof(effects)
                    );
                RequireSnapshotUnit(effect.SourceID);
                RequireSnapshotUnit(effect.TargetID);
                if (
                    effect.Kind
                        is not TacticalCombatEventKind.Notification
                            and not TacticalCombatEventKind.Movie
                        && GetUnit(effect.TargetID) == null
                    || effect.Kind
                        is TacticalCombatEventKind.Shot
                            or TacticalCombatEventKind.Superlaser
                        && GetUnit(effect.SourceID) == null
                )
                    throw new ArgumentException(
                        "The combat effect participant is invalid.",
                        nameof(effects)
                    );
            }
        }

        /// <summary>
        /// Validates a projection's optional participant identity.
        /// </summary>
        /// <param name="unitID">The optional registered participant identity.</param>
        private void RequireSnapshotUnit(string unitID)
        {
            if (!string.IsNullOrEmpty(unitID) && GetUnit(unitID) == null)
                throw new ArgumentException(
                    "The combat projection references an unknown participant."
                );
        }

        /// <summary>
        /// Validates an ordered projection roster and its optional owning faction.
        /// </summary>
        /// <param name="unitIDs">The ordered participant identities.</param>
        /// <param name="side">The required owning faction, when applicable.</param>
        private void RequireSnapshotUnits(string[] unitIDs, CombatSide? side = null)
        {
            foreach (string id in unitIDs)
                if (GetUnit(id) is not CombatUnit unit || side.HasValue && unit.Side != side.Value)
                    throw new ArgumentException(
                        "The combat projection references an invalid roster."
                    );
        }

        /// <summary>
        /// Checks the two admitted encounter faction identities.
        /// </summary>
        /// <param name="side">The proposed faction.</param>
        /// <returns>Whether the faction belongs to the encounter domain.</returns>
        private static bool IsSynchronizedSide(CombatSide side) =>
            side is CombatSide.Attacker or CombatSide.Defender;
    }
}
