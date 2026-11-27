using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Combat;
using Rebellion.Util.Random;
using CommandKind = Rebellion.Game.Combat.ActiveBattle.CommandKind;

namespace Rebellion.Tactical
{
    public sealed partial class TacticalCombatSession
    {
        internal bool CanCaptureForSave =>
            _battle != null
            && _random is LinearCongruentialRandomProvider
            && !IsSynchronized
            && !IsArriving
            && !IsComplete
            && PendingAttackRunMovie == null;

        /// <summary>
        /// Captures work still pending at the save boundary using the existing saved combatants.
        /// </summary>
        internal void CaptureForSave()
        {
            if (!CanCaptureForSave)
                throw new InvalidOperationException("The encounter cannot be saved at this point.");

            Selection.ProcessPendingCommands();
            if (!CanCaptureForSave)
                throw new InvalidOperationException(
                    "The battle ended before the save was captured."
                );

            _scheduler?.CaptureTime();
            ActiveBattle.ResumeState state = new()
            {
                RandomState = ((LinearCongruentialRandomProvider)_random).State,
                PlayerSide = PlayerSide,
                BattleDepth = BattleDepth,
                ScenerySeed = (SceneryRandom as TacticalCombatSceneryRandom)?.InitialSeed,
                StationCharge = BattleStation?.Charge ?? 0,
                StationDeparted = BattleStation?.HasDeparted == true,
                DirectCommandsSuspended = _directCommandsSuspended,
                Winner = Winner,
                Ending = _ending.CaptureState(),
                Briefing = _briefing.CaptureState(),
                Selection = Selection.CaptureState(),
                Notifications = Audio.Notifications.CaptureState(),
                LatestAttackGroupID = ActiveAttackRun?.Group.ID,
                InitiallyDockedFighterIDs = _initiallyDockedFighters.ConvertAll(unit =>
                    unit.InstanceID
                ),
                DepartedUnitIDs = _departed.Select(unit => unit.InstanceID).ToList(),
                PendingManeuverGroupIDs = _pendingManeuvers.Select(group => group.ID).ToList(),
                PendingAttackGroupIDs = _pendingAttackOrders.Select(group => group.ID).ToList(),
                AttackerCenter = _centers[0],
                DefenderCenter = _centers[1],
            };

            foreach (var pair in _personnelHostCounts)
                for (int index = 0; index < pair.Value; index++)
                    state.PersonnelHostIDs.Add(pair.Key);

            for (int index = 0; index < _sides.Length; index++)
            {
                ActiveBattle.SideState side = _sides[index].CaptureState();
                side.NextGroupID = _nextGroupID[index];
                side.Outcome = _outcomes[index];
                for (int category = 0; category < _objectGroups.GetLength(1); category++)
                    side.ObjectCategoryGroupIDs.Add(_objectGroups[index, category]?.ID ?? 0);
                for (int category = 0; category < _fighterGroups.GetLength(1); category++)
                    side.FighterCategoryGroupIDs.Add(_fighterGroups[index, category]?.ID ?? 0);
                state.Sides.Add(side);
            }

            HashSet<TacticalCombatGroup> deployed = new(_groups);
            foreach (TacticalCombatGroup group in _groupsByID.Values)
            {
                ActiveBattle.GroupState savedGroup = group.CaptureState(deployed.Contains(group));
                savedGroup.AttackRun = group.AttackRun?.CaptureState();
                state.Groups.Add(savedGroup);
            }

            Dictionary<TacticalCombatCarrier, ActiveBattle.CarrierState> carrierStates = new();
            foreach (TacticalCombatCarrier carrier in _carriers.Values)
            {
                ActiveBattle.CarrierState savedCarrier = carrier.CaptureState();
                carrierStates.Add(carrier, savedCarrier);
                state.Carriers.Add(savedCarrier);
            }
            foreach (var pair in _recoveries)
            {
                ActiveBattle.CarrierState savedCarrier = carrierStates[pair.Value.Carrier];
                if (
                    !savedCarrier.Recoveries.Any(recovery =>
                        recovery.FighterID == pair.Key.InstanceID
                    )
                )
                    savedCarrier.Recoveries.Add(
                        new() { FighterID = pair.Key.InstanceID, Phase = pair.Value.Phase }
                    );
            }

            foreach (var bucket in _commands)
            foreach (Command command in bucket.Value)
                if (CaptureCommand(bucket.Key, command) is { } savedCommand)
                    state.Commands.Add(savedCommand);

            _battle.Resume = state;
        }

        /// <summary>
        /// Records one command and the identities of its live participants.
        /// </summary>
        /// <param name="iteration">The original dispatch time.</param>
        /// <param name="command">The queued mutation.</param>
        /// <returns>The saved mutation, or null if dispatch would discard an obsolete group reference.</returns>
        private ActiveBattle.CommandState CaptureCommand(int iteration, Command command)
        {
            TacticalCombatGroup group = command.Group ?? command.Run?.Group;
            if (
                group != null
                && (
                    !ReferenceEquals(_groupsByID.GetValueOrDefault(group.ID), group)
                    || command.Run != null && !ReferenceEquals(group.AttackRun, command.Run)
                )
            )
                return null;

            return new()
            {
                Kind = command.Kind,
                Iteration = iteration,
                SourceID = command.Source?.InstanceID,
                TargetID = command.Target?.InstanceID,
                GroupID = group?.ID,
                Append = command.Append,
                Value = command.Value,
                Position = command.Position,
                Weapon = command.Shot.Weapon,
                Strength = command.Shot.Strength,
                System = command.Change.System,
                IsRepair = command.Change.IsRepair,
                AttackDelay = command.Change.AttackDelay,
                MovementDelay = command.Change.MovementDelay,
                DischargedArcMask = command.Change.DischargedArcMask,
                AttackRunState = command.Run == null ? null : command.RunState,
            };
        }

        /// <summary>
        /// Restores references and queued work after the detached unit roster is registered.
        /// </summary>
        /// <param name="state">The saved encounter state.</param>
        private void RestoreSavedState(ActiveBattle.ResumeState state)
        {
            BattleStation?.ApplySnapshot(state.StationCharge, state.StationDeparted);
            _briefing.RestoreState(state.Briefing);
            _centers[0] = state.AttackerCenter;
            _centers[1] = state.DefenderCenter;
            _directCommandsSuspended = state.DirectCommandsSuspended;
            Winner = state.Winner;
            _initiallyDockedFighters.AddRange(state.InitiallyDockedFighterIDs.Select(GetUnit));
            _departed.UnionWith(state.DepartedUnitIDs.Select(GetUnit));
            if (state.Ending != null)
            {
                _ending.RestoreState(state.Ending);
                _completionStarted = true;
            }

            foreach (ActiveBattle.GroupState savedGroup in state.Groups)
            {
                TacticalCombatGroup restored = TacticalCombatGroup.RestoreState(savedGroup, this);
                RegisterGroup(restored);
                if (savedGroup.Deployed)
                    _groups.Add(restored);
                if (savedGroup.AttackRun != null)
                    CreateAttackRun(restored).RestoreState(savedGroup.AttackRun, GetUnit);
            }
            _groups.Sort((first, second) => first.ID.CompareTo(second.ID));
            if (state.LatestAttackGroupID is int attackGroupID)
                ActiveAttackRun = _groupsByID.GetValueOrDefault(attackGroupID)?.AttackRun;

            foreach (ActiveBattle.SideState side in state.Sides)
            {
                int index = (int)side.Side;
                _nextGroupID[index] = side.NextGroupID;
                _outcomes[index] = side.Outcome;
                for (int category = 0; category < side.ObjectCategoryGroupIDs.Count; category++)
                    _objectGroups[index, category] = _groupsByID.GetValueOrDefault(
                        side.ObjectCategoryGroupIDs[category]
                    );
                for (int category = 0; category < side.FighterCategoryGroupIDs.Count; category++)
                    _fighterGroups[index, category] = _groupsByID.GetValueOrDefault(
                        side.FighterCategoryGroupIDs[category]
                    );
            }

            _pendingManeuvers.UnionWith(
                state.PendingManeuverGroupIDs.Select(id => _groupsByID[id])
            );
            _pendingAttackOrders.UnionWith(
                state.PendingAttackGroupIDs.Select(id => _groupsByID[id])
            );
            foreach (ActiveBattle.CarrierState carrier in state.Carriers)
                _carriers[carrier.UnitID].RestoreState(carrier, GetUnit, _recoveries);
            foreach (CombatUnit unit in _units)
                if (GetUnit(unit.TractorTargetID) is { } target)
                {
                    if (
                        !_tractorSources.TryGetValue(
                            target,
                            out SortedDictionary<uint, CombatUnit> sources
                        )
                    )
                    {
                        sources = new();
                        _tractorSources.Add(target, sources);
                    }
                    sources.Add(unchecked((uint)unit.Index), unit);
                }

            foreach (ActiveBattle.CommandState command in state.Commands)
                RestoreCommand(command);

            Audio.Notifications.RestoreState(state.Notifications);
            Selection.RestoreState(state.Selection, id => _groupsByID.GetValueOrDefault(id));
            _userPaused = true;
            _resumePaused = true;
        }

        /// <summary>
        /// Rebinds a pending command and preserves the ordering of its original interval.
        /// </summary>
        /// <param name="state">The saved mutation.</param>
        private void RestoreCommand(ActiveBattle.CommandState state)
        {
            CommandKind kind = state.Kind;
            CombatUnit source = GetUnit(state.SourceID);
            CombatUnit target = GetUnit(state.TargetID);
            TacticalCombatGroup group = state.GroupID is int groupID ? _groupsByID[groupID] : null;
            TacticalCombatShot shot = kind is CommandKind.Fire or CommandKind.Hit
                ? new(source, target, state.Weapon, state.Strength)
                : default;
            TacticalSystemEvent change =
                state.AttackDelay is float attackDelay && state.MovementDelay is float movementDelay
                    ? new(attackDelay, movementDelay, state.DischargedArcMask)
                    : new(state.System, state.IsRepair);
            TacticalCombatAttackRunState runState = state.AttackRunState ?? default;
            if (!_commands.TryGetValue(state.Iteration, out Queue<Command> bucket))
            {
                bucket = new();
                _commands.Add(state.Iteration, bucket);
            }
            bucket.Enqueue(
                new(
                    kind,
                    source,
                    target,
                    shot,
                    change,
                    group,
                    state.Append,
                    kind == CommandKind.AttackRun ? group.AttackRun : null,
                    runState,
                    state.Value,
                    state.Position
                )
            );
            if (kind is CommandKind.TractorLock or CommandKind.TractorUnlock)
                _tractorCommandsPending.Add(source);
        }
    }
}
