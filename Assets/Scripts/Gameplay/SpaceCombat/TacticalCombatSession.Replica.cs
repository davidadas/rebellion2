using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay
{
    public sealed partial class TacticalCombatSession
    {
        private TacticalCombatMovieRequest _unannouncedNetworkMovie;

        /// <summary>
        /// Announces a validated replica projection before participant state changes.
        /// </summary>
        internal event Action<TacticalCombatNetworkFrame> NetworkStateApplying;

        /// <summary>
        /// Restores replaceable authority state while retaining movie announcements for reliable effects.
        /// </summary>
        /// <param name="frame">The complete state without presentation effects.</param>
        public void ApplyNetworkState(TacticalCombatNetworkFrame frame)
        {
            if (frame?.Effects?.Length != 0)
                throw new ArgumentException(
                    "Replaceable state cannot contain presentation effects.",
                    nameof(frame)
                );
            if (!_networkReplica)
                throw new InvalidOperationException(
                    "Only a combat replica can receive an authoritative projection."
                );
            ValidateNetworkFrame(frame);
            NetworkStateApplying?.Invoke(frame);
            bool completed = IsComplete;
            bool withdrew = _networkCommandsSuspended[(int)PlayerSide];
            int?[] oldCategories = _objectGroups
                .Cast<TacticalCombatGroup>()
                .Concat(_fighterGroups.Cast<TacticalCombatGroup>())
                .Select(group => group?.ID)
                .ToArray();
            foreach (TacticalCombatNetworkSideState state in frame.Sides)
            {
                _sides[(int)state.Side].ApplyNetworkState(state);
                _networkCommandsSuspended[(int)state.Side] = state.CommandsSuspended;
            }
            HashSet<int> retained = frame.Groups.Select(state => state.GroupID).ToHashSet();
            foreach (int id in _groupsByID.Keys.Where(id => !retained.Contains(id)).ToArray())
                _groupsByID.Remove(id);
            _groups.Clear();
            Array.Clear(_objectGroups, 0, _objectGroups.Length);
            Array.Clear(_fighterGroups, 0, _fighterGroups.Length);
            foreach (TacticalCombatNetworkGroupState state in frame.Groups)
            {
                if (!_groupsByID.TryGetValue(state.GroupID, out TacticalCombatGroup group))
                {
                    group = TacticalCombatGroup.CreateNetworkGroup(state, this);
                    _groupsByID.Add(state.GroupID, group);
                }
                group.ApplyNetworkState(state, this);
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
                    group.AttackRun.ApplyNetworkState(
                        state.AttackRunState,
                        state.AttackRunMilliseconds
                    );
                }
                else
                    group.AttackRun = null;
            }
            foreach (TacticalCombatNetworkUnitState state in frame.Units)
                ApplyNetworkUnit(GetUnit(state.UnitID), state);
            _networkSystemTime = frame.SystemTime;
            _userPaused = frame.Paused;
            IsComplete = frame.Complete;
            Winner = frame.Winner;
            _outcomes[0] = frame.AttackerOutcome;
            _outcomes[1] = frame.DefenderOutcome;
            _ending.ApplyNetworkFlags(frame.TerminalFlags);
            BattleStation?.ApplyNetworkState(frame.StationCharge, frame.StationDeparted);
            foreach (TacticalCombatNetworkSelectionState state in frame.Selections)
                GetNetworkSelection(state.Side).ApplyNetworkState(state);
            TacticalCombatAttackRun previousRun = ActiveAttackRun;
            ActiveAttackRun = _groups
                .Select(group => group.AttackRun)
                .FirstOrDefault(run => run?.IsInProgress == true);
            if (!ReferenceEquals(previousRun, ActiveAttackRun))
                AttackRunChanged?.Invoke(ActiveAttackRun);
            if (frame.MovieID != 0 && frame.MovieID != _networkMovieID)
            {
                _networkMovieID = frame.MovieID;
                TacticalCombatGroup group =
                    GetNetworkGroup(frame.MovieGroupID)
                    ?? TacticalCombatGroup.CreateNetworkMovieGroup(
                        frame.MovieGroupID,
                        GetUnit(frame.MovieUnitID),
                        this
                    );
                group.FighterCategory = frame.MovieCategory;
                TacticalCombatAttackRun run =
                    group.AttackRun
                    ?? new TacticalCombatAttackRun(
                        group,
                        _random,
                        () => GetSide(group.Side).GetCommandBudget(true),
                        GetSide(PlayerSide).FactionIndex
                    );
                run.ApplyNetworkState(frame.MovieState, 0);
                PendingAttackRunMovie = new TacticalCombatMovieRequest(run, _ => { });
                _unannouncedNetworkMovie = PendingAttackRunMovie;
            }
            else if (frame.MovieID == 0 && PendingAttackRunMovie != null)
            {
                PendingAttackRunMovie.Complete();
                PendingAttackRunMovie = null;
                _unannouncedNetworkMovie = null;
            }
            _appliedNetworkSequence = frame.Sequence;
            if (!withdrew && _networkCommandsSuspended[(int)PlayerSide])
                Selection.ApplyNetworkWithdrawal();
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
        public void ApplyNetworkEffects(TacticalCombatNetworkEffect[] effects)
        {
            if (!_networkReplica)
                throw new InvalidOperationException(
                    "Only a combat replica can receive presentation effects."
                );
            ValidateNetworkEffects(effects);
            foreach (TacticalCombatNetworkEffect effect in effects)
                if (effect.Kind == TacticalCombatNetworkEffectKind.Movie)
                {
                    if (effect.MovieID == _networkMovieID && _unannouncedNetworkMovie != null)
                        AnnounceNetworkMovie();
                }
                else
                    ApplyNetworkEffect(effect);
        }

        /// <summary>
        /// Transfers one retained movie announcement to its local presentation exactly once.
        /// </summary>
        private void AnnounceNetworkMovie()
        {
            TacticalCombatMovieRequest request = _unannouncedNetworkMovie;
            _unannouncedNetworkMovie = null;
            AttackRunMovieRequested?.Invoke(request);
        }

        /// <summary>
        /// Restores one participant after all frame identities have passed validation.
        /// </summary>
        /// <param name="unit">The local registered participant.</param>
        /// <param name="state">The authoritative participant state.</param>
        private static void ApplyNetworkUnit(
            TacticalCombatUnit unit,
            TacticalCombatNetworkUnitState state
        )
        {
            unit.Position = state.Position.ToVector();
            unit.Heading = state.Heading.ToVector();
            unit.Velocity = state.Velocity.ToVector();
            unit.DesiredHeading = state.DesiredHeading.ToVector();
            unit.DesiredMovement = state.DesiredMovement.ToVector();
            unit.Status = state.Status;
            unit.CarrierID = string.IsNullOrEmpty(state.CarrierID) ? null : state.CarrierID;
            unit.TargetID = string.IsNullOrEmpty(state.TargetID) ? null : state.TargetID;
            unit.Systems.ApplyNetworkState(state);
            unit.Weapons.ApplyNetworkState(state);
            unit.HyperspaceMilliseconds = state.HyperspaceMilliseconds;
            unit.TractorRestraint = state.TractorRestraint;
            unit.ApplyNetworkTractorMembership(state.HasTractorSources);
            unit.CommandBudget = state.CommandBudget;
            unit.TurnRate = state.TurnRate;
            unit.HasCollision = state.HasCollision;
            unit.GroupID = state.HasGroup ? state.GroupID : null;
            unit.RequestedGroupID = state.HasRequestedGroup ? state.RequestedGroupID : null;
        }

        /// <summary>
        /// Replays an authoritative presentation edge without reproducing its gameplay operation.
        /// </summary>
        /// <param name="effect">The validated ordered edge.</param>
        private void ApplyNetworkEffect(TacticalCombatNetworkEffect effect)
        {
            TacticalCombatUnit source = GetUnit(effect.SourceID);
            TacticalCombatUnit target = GetUnit(effect.TargetID);
            switch (effect.Kind)
            {
                case TacticalCombatNetworkEffectKind.Shot:
                    ShotFired?.Invoke(
                        new TacticalCombatShot(source, target, effect.Weapon, effect.Strength)
                    );
                    break;
                case TacticalCombatNetworkEffectKind.Impact:
                    ImpactApplied?.Invoke(target, effect.Weapon, effect.Impact);
                    break;
                case TacticalCombatNetworkEffectKind.Destruction:
                    DestructionEffectRequested?.Invoke(target);
                    break;
                case TacticalCombatNetworkEffectKind.Destroyed:
                    UnitDestroyed?.Invoke(target);
                    break;
                case TacticalCombatNetworkEffectKind.Withdrawn:
                    UnitWithdrawn?.Invoke(target);
                    break;
                case TacticalCombatNetworkEffectKind.Recovered:
                    FighterRecovered?.Invoke(target);
                    break;
                case TacticalCombatNetworkEffectKind.Superlaser:
                    SuperlaserFired?.Invoke(source, target);
                    break;
                case TacticalCombatNetworkEffectKind.TractorSources:
                    target.SetTractorSources(effect.Enabled);
                    break;
                case TacticalCombatNetworkEffectKind.GravityWell:
                    target.RequestGravityWellPresentation();
                    break;
                case TacticalCombatNetworkEffectKind.Notification:
                    if (!effect.HasRecipient || effect.Recipient == PlayerSide)
                        Audio.EnqueueNotification(effect.MessageID, effect.Enabled);
                    break;
            }
        }

        /// <summary>
        /// Rejects stale or malformed projections before any local presentation state changes.
        /// </summary>
        /// <param name="frame">The proposed complete projection.</param>
        private void ValidateNetworkFrame(TacticalCombatNetworkFrame frame)
        {
            if (
                frame == null
                || frame.Sequence <= _appliedNetworkSequence
                || frame.SystemTime < 3
                || frame.Units == null
                || frame.Units.Length != _units.Count
                || frame.Groups == null
                || frame.Sides?.Length != 2
                || frame.Selections?.Length != 2
                || frame.TerminalFlags?.Length != 7
                || frame.Effects == null
                || !Enum.IsDefined(typeof(CombatSide), frame.Winner)
                || !Enum.IsDefined(typeof(SpaceCombatSideOutcome), frame.AttackerOutcome)
                || !Enum.IsDefined(typeof(SpaceCombatSideOutcome), frame.DefenderOutcome)
            )
                throw new ArgumentException(
                    "The combat projection is stale or incomplete.",
                    nameof(frame)
                );
            HashSet<string> participants = new(StringComparer.Ordinal);
            HashSet<int> groups = new();
            foreach (TacticalCombatNetworkGroupState state in frame.Groups)
            {
                if (
                    state == null
                    || !groups.Add(state.GroupID)
                    || !IsNetworkSide(state.Side)
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
                        nameof(frame)
                    );
                TacticalCombatGroup existing = GetNetworkGroup(state.GroupID);
                if (
                    existing != null
                    && (existing.Side != state.Side || existing.Kind != state.Kind)
                )
                    throw new ArgumentException(
                        "The combat group identity has changed ownership.",
                        nameof(frame)
                    );
                RequireNetworkUnits(state.Members, state.Side);
                RequireNetworkUnits(state.RequestedMembers, state.Side);
                RequireNetworkUnits(state.Targets);
                RequireNetworkUnits(state.RequestedTargets);
                RequireNetworkUnits(state.ComputedTargets);
                RequireNetworkUnit(state.PrimaryMemberID);
                RequireNetworkUnit(state.RequestedLeaderID);
            }
            foreach (TacticalCombatNetworkUnitState state in frame.Units)
            {
                if (
                    state == null
                    || !participants.Add(state.UnitID)
                    || GetUnit(state.UnitID) == null
                    || state.ComponentDamage?.Length != 5
                    || state.Charge?.Length != 4
                    || state.Ready?.Length != 4
                    || !Enum.IsDefined(typeof(TacticalCombatUnitStatus), state.Status)
                    || state.HasGroup && !groups.Contains(state.GroupID)
                    || state.HasRequestedGroup && !groups.Contains(state.RequestedGroupID)
                )
                    throw new ArgumentException(
                        "The combat participant projection is invalid.",
                        nameof(frame)
                    );
                RequireNetworkUnit(state.CarrierID);
                RequireNetworkUnit(state.TargetID);
            }
            HashSet<CombatSide> sides = new();
            foreach (TacticalCombatNetworkSideState state in frame.Sides)
                if (
                    state == null
                    || !IsNetworkSide(state.Side)
                    || !sides.Add(state.Side)
                    || state.HasWithdrawalPhase
                        && !Enum.IsDefined(
                            typeof(TacticalCombatWithdrawalPhase),
                            state.WithdrawalPhase
                        )
                )
                    throw new ArgumentException(
                        "The combat faction projection is invalid.",
                        nameof(frame)
                    );
            sides.Clear();
            foreach (TacticalCombatNetworkSelectionState state in frame.Selections)
            {
                if (
                    state == null
                    || !IsNetworkSide(state.Side)
                    || !sides.Add(state.Side)
                    || !Enum.IsDefined(typeof(TacticalCombatSelectionKind), state.Kind)
                    || state.HasGroup && !groups.Contains(state.GroupID)
                    || state.Members == null
                    || state.Highlights == null
                    || state.Targets == null
                    || state.Waypoints == null
                    || state.Groups?.Any(id => !groups.Contains(id)) != false
                )
                    throw new ArgumentException(
                        "The combat selection projection is invalid.",
                        nameof(frame)
                    );
                RequireNetworkUnits(state.Members);
                RequireNetworkUnits(state.Highlights);
                RequireNetworkUnits(state.Targets);
                RequireNetworkUnit(state.PrimaryID);
                RequireNetworkUnit(state.InspectedID);
            }
            if (
                frame.MovieID < 0
                || frame.MovieID != 0
                    && (
                        GetUnit(frame.MovieUnitID) is not TacticalCombatUnit movieUnit
                        || !movieUnit.IsFighter
                        || movieUnit.Side != frame.MovieSide
                        || frame.MovieState
                            is not TacticalCombatAttackRunState.Succeeded
                                and not TacticalCombatAttackRunState.Failed
                    )
            )
                throw new ArgumentException(
                    "The combat movie projection is invalid.",
                    nameof(frame)
                );
        }

        /// <summary>
        /// Validates every reliable edge before any edge in its batch can change presentation.
        /// </summary>
        /// <param name="effects">The proposed ordered batch.</param>
        private void ValidateNetworkEffects(TacticalCombatNetworkEffect[] effects)
        {
            if (effects == null)
                throw new ArgumentNullException(nameof(effects));
            foreach (TacticalCombatNetworkEffect effect in effects)
            {
                if (
                    effect == null
                    || !Enum.IsDefined(typeof(TacticalCombatNetworkEffectKind), effect.Kind)
                    || effect.HasRecipient && !IsNetworkSide(effect.Recipient)
                    || effect.Kind
                        is TacticalCombatNetworkEffectKind.Shot
                            or TacticalCombatNetworkEffectKind.Impact
                        && !Enum.IsDefined(typeof(TacticalWeaponType), effect.Weapon)
                    || effect.Kind == TacticalCombatNetworkEffectKind.Impact
                        && !Enum.IsDefined(typeof(TacticalCombatImpactKind), effect.Impact)
                    || effect.Kind == TacticalCombatNetworkEffectKind.Notification
                        && (uint)effect.MessageID >= 317
                    || effect.Kind == TacticalCombatNetworkEffectKind.Movie && effect.MovieID <= 0
                )
                    throw new ArgumentException(
                        "The combat effect projection is invalid.",
                        nameof(effects)
                    );
                RequireNetworkUnit(effect.SourceID);
                RequireNetworkUnit(effect.TargetID);
                if (
                    effect.Kind
                        is not TacticalCombatNetworkEffectKind.Notification
                            and not TacticalCombatNetworkEffectKind.Movie
                        && GetUnit(effect.TargetID) == null
                    || effect.Kind
                        is TacticalCombatNetworkEffectKind.Shot
                            or TacticalCombatNetworkEffectKind.Superlaser
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
        private void RequireNetworkUnit(string unitID)
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
        private void RequireNetworkUnits(string[] unitIDs, CombatSide? side = null)
        {
            foreach (string id in unitIDs)
                if (
                    GetUnit(id) is not TacticalCombatUnit unit
                    || side.HasValue && unit.Side != side.Value
                )
                    throw new ArgumentException(
                        "The combat projection references an invalid roster."
                    );
        }

        /// <summary>
        /// Checks the two admitted encounter faction identities.
        /// </summary>
        /// <param name="side">The proposed faction.</param>
        /// <returns>Whether the faction belongs to the encounter domain.</returns>
        private static bool IsNetworkSide(CombatSide side) =>
            side is CombatSide.Attacker or CombatSide.Defender;
    }
}
