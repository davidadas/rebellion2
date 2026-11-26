using System.Collections.Generic;
using System.Numerics;
using Rebellion.Content;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.ShipComponents;

namespace Rebellion.Tactical
{
    public sealed partial class TacticalCombatSession
    {
        /// <summary>
        /// Applies due commands in their stable insertion order, including commands generated during dispatch.
        /// </summary>
        /// <param name="dueTime">The inclusive command time.</param>
        private void DispatchCommands(int dueTime)
        {
            while (_commands.Count > 0)
            {
                using SortedDictionary<int, Queue<Command>>.Enumerator entries =
                    _commands.GetEnumerator();
                entries.MoveNext();
                int time = entries.Current.Key;
                if (time > dueTime)
                    break;
                Queue<Command> bucket = entries.Current.Value;
                _commands.Remove(time);
                while (bucket.Count > 0)
                    ApplyCommand(bucket.Dequeue());
            }
        }

        /// <summary>
        /// Enqueues a typed battle mutation at the requested whole-second command time.
        /// </summary>
        /// <param name="command">The requested mutation.</param>
        /// <param name="delay">The additional command delay.</param>
        private void QueueCommand(Command command, int delay = 0)
        {
            int time = SystemTime + delay;
            if (!_commands.TryGetValue(time, out Queue<Command> bucket))
            {
                bucket = new();
                _commands.Add(time, bucket);
            }
            bucket.Enqueue(command);
        }

        /// <summary>
        /// Queues a discharge animation and its separately delayed damage command.
        /// </summary>
        /// <param name="shot">The selected battery discharge.</param>
        private void RequestShot(TacticalCombatShot shot)
        {
            QueueCommand(
                new Command(CommandKind.Fire, shot.Source, shot.Target, shot),
                shot.FiringDelay
            );
            QueueCommand(
                new Command(CommandKind.Hit, shot.Source, shot.Target, shot),
                shot.ImpactDelay
            );
        }

        /// <summary>
        /// Queues a component mutation raised by hull damage, ion damage, or damage control.
        /// </summary>
        /// <param name="unit">The affected unit.</param>
        /// <param name="change">The requested mutation.</param>
        private void QueueSystemChange(CombatUnit unit, TacticalSystemEvent change) =>
            QueueCommand(new Command(CommandKind.System, unit, null, default, change));

        /// <summary>
        /// Queues a launch request without changing a docked fighter during carrier enumeration.
        /// </summary>
        /// <param name="carrier">The launching carrier.</param>
        /// <param name="fighter">The next docked squadron.</param>
        private void RequestLaunch(TacticalCombatCarrier carrier, CombatUnit fighter) =>
            QueueCommand(new Command(CommandKind.Launch, carrier.Unit, fighter));

        /// <summary>
        /// Queues the transition from departure preparation into the hyperspace animation.
        /// </summary>
        /// <param name="unit">The departing unit.</param>
        private void RequestHyperspace(CombatUnit unit) =>
            QueueCommand(new Command(CommandKind.Hyperspace, unit));

        /// <summary>
        /// Requests a tractor lock or unlock while allowing only one unresolved tractor command per ship.
        /// </summary>
        /// <param name="unit">The tractor source.</param>
        /// <param name="target">The designated primary target.</param>
        private void RequestTractorTarget(CombatUnit unit, CombatUnit target)
        {
            if (_tractorCommandsPending.Contains(unit) || unit.TractorRange == 0)
                return;
            bool valid =
                target?.IsTargetable == true
                && unit.GetTractorBudget() > 0
                && TacticalCombatGeometry.GetTractorDistance(unit.Position, target.Position)
                    <= unit.TractorRange;
            if (unit.TractorTargetID != null)
            {
                if (valid && target.InstanceID == unit.TractorTargetID)
                    return;
                _tractorCommandsPending.Add(unit);
                QueueCommand(
                    new Command(CommandKind.TractorUnlock, unit, GetUnit(unit.TractorTargetID))
                );
            }
            else if (valid)
            {
                _tractorCommandsPending.Add(unit);
                QueueCommand(new Command(CommandKind.TractorLock, unit, target));
            }
        }

        /// <summary>
        /// Advances side countdowns, carrier operations, formation assessments, and unit systems once per second.
        /// </summary>
        private void AdvanceSystems()
        {
            TacticalCombatSide first = GetSide(PlayerSide);
            TacticalCombatSide second = GetSide(
                PlayerSide == CombatSide.Attacker ? CombatSide.Defender : CombatSide.Attacker
            );
            AdvanceSide(first);
            AdvanceSide(second);
            foreach (TacticalCombatGroup group in _groups)
            {
                group.Refresh(_units, SystemTime);
                if (group.ActiveOrder == TacticalCombatOrder.Recover)
                    AdvanceRecovery(group);
                group.AttackRun?.Advance(1000);
            }
            foreach (CombatUnit unit in _units)
            {
                unit.CommandBudget = GetSide(unit.Side).GetCommandBudget(unit.IsFighter);
                if (unit.IsBattleStation)
                {
                    _carriers[unit.InstanceID].Advance(SystemTime, _random);
                    continue;
                }
                if (!unit.IsTargetable)
                    continue;
                Damage.AdvanceSystems(unit, 1000, _random);
                unit.RechargeBatteries(1, unit.GetWeaponRecharge());
                if (_carriers.TryGetValue(unit.InstanceID, out TacticalCombatCarrier carrier))
                    carrier.Advance(SystemTime, _random);
                TacticalCombatGroup group = GetGroup(unit);
                if (group != null)
                    _fireControl.AcquireTargets(unit, group, _spatial, _random);
            }
            UpdateTractorRestraint();
        }

        /// <summary>
        /// Prepares candidates, advances the Empire station, and checks withdrawal members before side planning.
        /// </summary>
        /// <param name="side">The side occupying this whole-tick update slot.</param>
        private void AdvanceSide(TacticalCombatSide side)
        {
            _targeting.AssessStrength(side);
            AdvanceWithdrawalCountdown(side);
            _targeting.PrepareCandidates(side);
            if (side.FactionIndex == 1)
                BattleStation?.Advance(1000);
            AdvanceWithdrawalMembers(side);
            AutomationRequested?.Invoke(side);
        }

        /// <summary>
        /// Queues one preferred category target when an explicit attack behavior first finds an eligible candidate.
        /// </summary>
        private void AdvanceAttackOrders()
        {
            _completedAttackOrders.Clear();
            foreach (TacticalCombatGroup group in _pendingAttackOrders)
            {
                CombatUnit target = _targeting.SelectBehaviorTarget(group);
                if (target == null)
                    continue;
                SetTarget(group, target);
                _completedAttackOrders.Add(group);
            }
            foreach (TacticalCombatGroup group in _completedAttackOrders)
                _pendingAttackOrders.Remove(group);
        }

        /// <summary>
        /// Orders tactical object roots by unsigned registration identity.
        /// </summary>
        /// <param name="first">The first registered unit.</param>
        /// <param name="second">The second registered unit.</param>
        /// <returns>The unsigned registration comparison.</returns>
        private static int CompareUnits(CombatUnit first, CombatUnit second) =>
            unchecked((uint)first.Index).CompareTo(unchecked((uint)second.Index));

        /// <summary>
        /// Resolves each newly activated maneuver once using the preceding frame's opposing center.
        /// </summary>
        private void AdvanceManeuvers()
        {
            foreach (TacticalCombatGroup group in _pendingManeuvers)
                if (
                    Navigation.TryGetManeuver(
                        group,
                        _units,
                        _centers[1 - (int)group.Side],
                        out Vector3 point
                    )
                )
                    SetWaypoint(group, point);
                else
                    SetOrder(group, TacticalCombatOrder.None);
            _pendingManeuvers.Clear();
        }

        /// <summary>
        /// Captures active side centers in battle registration order for the following frame's maneuvers.
        /// </summary>
        private void UpdateCenters()
        {
            _centers[0] = _centers[1] = Vector3.Zero;
            int firstCount = 0;
            int secondCount = 0;
            foreach (CombatUnit unit in _units)
            {
                if (!unit.IsEngaged || unit.IsBattleStation)
                    continue;
                if (unit.Side == CombatSide.Attacker)
                    firstCount++;
                else
                    secondCount++;
                if (unit.IsTargetable)
                    _centers[(int)unit.Side] += unit.Position;
            }
            _centers[0] *= firstCount == 0 ? 0 : 1f / firstCount;
            _centers[1] *= secondCount == 0 ? 0 : 1f / secondCount;
        }

        /// <summary>
        /// Advances formations, unit movement, and four-second hyperspace animations.
        /// </summary>
        /// <param name="elapsedMilliseconds">The elapsed movement interval.</param>
        private void AdvanceMovement(float elapsedMilliseconds)
        {
            AdvanceAttackOrders();
            AdvanceManeuvers();
            foreach (TacticalCombatGroup group in _groups)
                group.Advance(
                    elapsedMilliseconds,
                    BattleDepth,
                    GetSide(group.Side).WithdrawalMilliseconds,
                    _groupsByID
                );
            foreach (CombatUnit unit in _units)
            {
                if (unit.Status == CombatUnitStatus.EnteringHyperspace)
                {
                    unit.Heading = -GetSide(unit.Side).Forward;
                    if (
                        unit.HyperspaceMilliseconds == 0
                        || float.IsNaN(unit.HyperspaceMilliseconds)
                    )
                        Audio.EnqueueNotification(unit.IsFighter ? 29 : 31);
                    unit.HyperspaceMilliseconds += elapsedMilliseconds;
                    if (unit.HyperspaceMilliseconds >= 4000)
                    {
                        unit.HyperspaceMilliseconds = 0;
                        unit.Status = CombatUnitStatus.AwaitingWithdrawal;
                        QueueCommand(new Command(CommandKind.Withdraw, unit));
                    }
                    continue;
                }
                TacticalCombatGroup group = GetGroup(unit);
                if (group != null)
                    _motion.Advance(
                        unit,
                        group,
                        GetRecoveryCarrier(unit)?.Unit,
                        BattleDepth,
                        _spatial,
                        elapsedMilliseconds
                    );
                if (unit.Status == CombatUnitStatus.Active && unit.HasGravityWell)
                    unit.RequestGravityWellPresentation();
            }
            CheckCompletion();
            UpdateCenters();
        }

        /// <summary>
        /// Counts down an admitted withdrawal before the side's candidate-maintenance phase.
        /// </summary>
        /// <param name="side">The withdrawing side.</param>
        private void AdvanceWithdrawalCountdown(TacticalCombatSide side)
        {
            if (
                !side.AdvanceWithdrawal(1000)
                || side.FactionIndex != 1
                || BattleStation?.IsOperational != true
            )
                return;

            if (side.Side == PlayerSide || _synchronizationAuthority)
                (
                    _synchronizationAuthority ? GetSynchronizedSelection(side.Side) : Selection
                ).ProcessStationDeparture();
            QueueCommand(new Command(CommandKind.Withdraw, BattleStation.Unit));
        }

        private void AdvanceWithdrawalMembers(TacticalCombatSide side)
        {
            if (side.WithdrawalPhase != TacticalCombatSide.DeparturePhase.Departing)
                return;

            // Capital ships are checked before fighters on both passes.
            for (int pass = 0; pass < 2; pass++)
                foreach (CombatUnit unit in _registeredMobileUnits)
                    if (
                        unit.Side == side.Side
                        && unit.IsFighter == (pass == 1)
                        && IsDisplayedMember(unit)
                        && CheckWithdrawalReadiness(unit)
                        && (
                            unit.IsActiveDisplayObject
                            || unit.Status
                                is CombatUnitStatus.EnteringHyperspace
                                    or CombatUnitStatus.AwaitingWithdrawal
                        )
                    )
                        return;

            side.StartScuttling();
            for (int pass = 0; pass < 2; pass++)
                foreach (CombatUnit unit in _registeredMobileUnits)
                    if (
                        unit.Side == side.Side
                        && unit.IsFighter == (pass == 1)
                        && IsDisplayedMember(unit)
                        && !CheckWithdrawalReadiness(unit)
                    )
                        QueueCommand(new Command(CommandKind.Scuttle, unit));
        }

        /// <summary>
        /// Checks the deployed capital or fighter chain's retained participation domain independently of hull.
        /// </summary>
        /// <param name="unit">The registered non-station participant.</param>
        /// <returns>Whether the member remains in its side's deployed display chain.</returns>
        private static bool IsDisplayedMember(CombatUnit unit) =>
            unit.Status
                is CombatUnitStatus.Active
                    or CombatUnitStatus.PreparingHyperspace
                    or CombatUnitStatus.EnteringHyperspace
                    or CombatUnitStatus.AwaitingWithdrawal
                    or CombatUnitStatus.AttackingBattleStation;

        /// <summary>
        /// Reads a participant's movement using current command support and retained beam sources.
        /// </summary>
        /// <param name="unit">The participant being inspected.</param>
        /// <returns>The available movement before a single-precision speed store.</returns>
        public double GetMovementBudget(CombatUnit unit) =>
            unit.GetAvailableMovement(
                GetSide(unit.Side).GetMovementSupport(unit.IsFighter),
                GetTractorRestraint(unit)
            );

        /// <summary>
        /// Reads a participant's live movement relative to its complete engine budget.
        /// </summary>
        /// <param name="unit">The participant being inspected.</param>
        /// <returns>The available movement ratio using current command support and retained beams.</returns>
        public double GetMovementRatio(CombatUnit unit) =>
            unit.GetMovementRatio(
                GetSide(unit.Side).GetMovementSupport(unit.IsFighter),
                GetTractorRestraint(unit)
            );

        /// <summary>
        /// Refreshes departure readiness from live command support and retained tractor contributors.
        /// </summary>
        /// <param name="unit">The participating capital ship or fighter.</param>
        /// <returns>Whether its hyperdrive and movement budgets are ordered and nonzero.</returns>
        private bool CheckWithdrawalReadiness(CombatUnit unit) =>
            unit.HasHyperdrive
            && unit.CanEnterHyperspace(
                GetSide(unit.Side).GetMovementSupport(unit.IsFighter),
                GetTractorRestraint(unit)
            );

        /// <summary>
        /// Reserves carrier berths and queues recovery for squadrons within their hangar entrance.
        /// </summary>
        /// <param name="group">The recovering fighter group.</param>
        private void AdvanceRecovery(TacticalCombatGroup group)
        {
            bool warned = false;
            foreach (CombatUnit fighter in group.Members)
            {
                if (!fighter.IsTargetable)
                    continue;
                if (!_recoveries.TryGetValue(fighter, out TacticalCombatCarrier.Recovery recovery))
                {
                    if (
                        fighter.CarrierID != null
                        && _carriers.TryGetValue(
                            fighter.CarrierID,
                            out TacticalCombatCarrier linked
                        )
                    )
                        recovery = linked.ReserveRecovery(fighter);
                    bool full = true;
                    if (recovery == null)
                        foreach (CombatUnit candidate in _units)
                            if (
                                !candidate.IsFighter
                                && !candidate.IsBattleStation
                                && candidate.Side == fighter.Side
                                && candidate.IsTargetable
                            )
                            {
                                TacticalCombatCarrier candidateCarrier = _carriers[
                                    candidate.InstanceID
                                ];
                                recovery = candidateCarrier.ReserveRecovery(fighter);
                                if (recovery != null)
                                    break;
                                full &= candidateCarrier.IsFullAndLaunched;
                            }
                    if (
                        recovery == null
                        && GetSide(fighter.Side).FactionIndex == 1
                        && BattleStation?.IsOperational == true
                        && BattleStation.Unit.Side == fighter.Side
                    )
                    {
                        TacticalCombatCarrier station = _carriers[BattleStation.Unit.InstanceID];
                        recovery = station.ReserveRecovery(fighter);
                        full &= station.IsFullAndLaunched;
                    }
                    if (recovery == null)
                    {
                        if (!warned)
                        {
                            RecoveryUnavailable?.Invoke(group);
                            warned = true;
                        }
                        if (full && GetSide(group.Side).IsWithdrawing)
                            QueueCommand(new Command(CommandKind.Scuttle, fighter));
                        else if (full)
                            SetOrder(group, TacticalCombatOrder.None);
                        continue;
                    }
                    _recoveries.Add(fighter, recovery);
                    QueueCommand(
                        new Command(CommandKind.SetRecovery, fighter, recovery.Carrier.Unit)
                    );
                }
                if (recovery.TryQueueLanding())
                    QueueCommand(new Command(CommandKind.Recover, recovery.Carrier.Unit, fighter));
            }
        }

        /// <summary>
        /// Gets the live carrier assigned by an applied recovery command.
        /// </summary>
        /// <param name="fighter">The approaching fighter squadron.</param>
        /// <returns>The assigned carrier, or null before assignment.</returns>
        private TacticalCombatCarrier GetRecoveryCarrier(CombatUnit fighter) =>
            _recoveries.TryGetValue(fighter, out TacticalCombatCarrier.Recovery recovery)
            && recovery.IsAssigned
                ? recovery.Carrier
                : null;

        /// <summary>
        /// Releases a fighter's pending recovery reservation.
        /// </summary>
        /// <param name="fighter">The reassigned or destroyed squadron.</param>
        private void ReleaseRecovery(CombatUnit fighter)
        {
            if (!_recoveries.Remove(fighter, out TacticalCombatCarrier.Recovery recovery))
                return;
            recovery.Carrier.Detach(fighter);
            fighter.CarrierID = null;
        }

        /// <summary>
        /// Announces a station charge crossing to the faction whose tactical advisor reports it.
        /// The station calls this before storing the completed charge.
        /// </summary>
        private void NotifyStationCharged()
        {
            if (_synchronizationAuthority)
                foreach (TacticalCombatSide side in _sides)
                    if (side.FactionIndex == 1)
                        EnqueueFactionNotification(side.Side, 275);
            if (!_synchronizationAuthority && GetSide(PlayerSide).FactionIndex == 1)
                EnqueueFactionNotification(PlayerSide, 275);
        }

        /// <summary>
        /// Selects the firing sound from the firing unit's weapon and authored faction.
        /// </summary>
        /// <param name="shot">The queued discharge.</param>
        /// <returns>The sound requested before drawing the weapon effect.</returns>
        private TacticalCombatSound GetFiringSound(TacticalCombatShot shot) =>
            shot.Weapon switch
            {
                TacticalWeaponType.LaserCannon when shot.Source.IsFighter => GetSide(
                    shot.Source.Side
                ).FactionIndex == 0
                    ? TacticalCombatSound.AllianceFighterLaser
                    : TacticalCombatSound.EmpireFighterLaser,
                TacticalWeaponType.LaserCannon => TacticalCombatSound.CapitalLaser,
                TacticalWeaponType.IonCannon => shot.Source.IsFighter
                    ? TacticalCombatSound.FighterIon
                    : TacticalCombatSound.CapitalIon,
                _ => TacticalCombatSound.HeavyWeapon,
            };

        /// <summary>
        /// Applies one queued mutation after resolving its current participation guards.
        /// </summary>
        /// <param name="command">The due command.</param>
        private void ApplyCommand(Command command)
        {
            CombatUnit source = command.Source;
            CombatUnit target = command.Target;
            switch (command.Kind)
            {
                case CommandKind.DeleteGroup:
                    if (
                        _groupsByID.TryGetValue(command.Group.ID, out TacticalCombatGroup deleted)
                        && ReferenceEquals(deleted, command.Group)
                    )
                    {
                        while (deleted.Members.Count != 0)
                            deleted.RemoveMember(deleted.Members[0]);
                        DeregisterGroup(deleted);
                    }
                    break;
                case CommandKind.CreateGroup:
                case CommandKind.AddGroupMember:
                case CommandKind.RemoveGroupMember:
                    if (
                        _groupsByID.TryGetValue(command.Group.ID, out TacticalCombatGroup owner)
                        && ReferenceEquals(owner, command.Group)
                    )
                    {
                        if (command.Kind == CommandKind.RemoveGroupMember)
                            RemoveAppliedMember(owner, source);
                        else
                        {
                            owner.AddMember(source);
                            if (command.Kind == CommandKind.CreateGroup)
                            {
                                owner.Formation.SetPosition(source.Position);
                                _groups.Add(owner);
                                _groups.Sort((first, second) => first.ID.CompareTo(second.ID));
                            }
                        }
                    }
                    break;
                case CommandKind.Order:
                case CommandKind.AttackMode:
                case CommandKind.Formation:
                case CommandKind.Waypoint:
                case CommandKind.RemoveWaypoint:
                    if (
                        _groupsByID.TryGetValue(command.Group.ID, out TacticalCombatGroup commanded)
                        && ReferenceEquals(commanded, command.Group)
                    )
                    {
                        if (command.Kind == CommandKind.Order)
                            ApplyOrder(commanded, (TacticalCombatOrder)command.Value);
                        else if (command.Kind == CommandKind.AttackMode)
                            commanded.ApplyAttackMode(command.Value != 0);
                        else if (command.Kind == CommandKind.Formation)
                            commanded.ApplyFormation((TacticalCombatFormationKind)command.Value);
                        else if (command.Kind == CommandKind.Waypoint)
                            commanded.ApplyWaypoint(command.Position, command.Append);
                        else
                            commanded.RemoveWaypoint(command.Position, false);
                    }
                    break;
                case CommandKind.Superlaser:
                    if (target.IsTargetable)
                    {
                        Audio.Enqueue(TacticalCombatSound.Superlaser);
                        if (_synchronizationAuthority)
                            foreach (TacticalCombatSide side in _sides)
                                if (side.FactionIndex == 0)
                                    EnqueueFactionNotification(side.Side, 145);
                        if (!_synchronizationAuthority && GetSide(PlayerSide).FactionIndex == 0)
                            EnqueueFactionNotification(PlayerSide, 145);
                    }
                    SuperlaserFired?.Invoke(source, target);
                    break;
                case CommandKind.AttackRun:
                    if (
                        _groupsByID.TryGetValue(
                            command.Run.Group.ID,
                            out TacticalCombatGroup attacking
                        )
                        && ReferenceEquals(attacking, command.Run.Group)
                        && ReferenceEquals(attacking.AttackRun, command.Run)
                        && attacking.ActiveOrder == TacticalCombatOrder.AttackBattleStation
                    )
                    {
                        command.Run.ApplyState(command.RunState);
                        AttackRunChanged?.Invoke(command.Run);
                    }
                    break;
                case CommandKind.Target:
                case CommandKind.RemoveTarget:
                    if (
                        _groupsByID.TryGetValue(
                            command.Group.ID,
                            out TacticalCombatGroup registered
                        ) && ReferenceEquals(registered, command.Group)
                    )
                    {
                        if (command.Kind == CommandKind.Target)
                            command.Group.ApplyTarget(target, command.Append);
                        else
                            command.Group.RemoveTarget(target, false);
                    }
                    break;
                case CommandKind.Fire:
                    if (IsActiveDisplayObject(source) && IsActiveDisplayObject(target))
                    {
                        Audio.Enqueue(GetFiringSound(command.Shot));
                        ShotFired?.Invoke(command.Shot);
                    }
                    break;
                case CommandKind.Hit:
                    if (IsActiveDisplayObject(target))
                    {
                        TacticalCombatImpactKind impact = Damage.ApplyHit(
                            target,
                            command.Shot.Weapon,
                            command.Shot.Strength,
                            target.GetManeuverability(),
                            source.GetManeuverability(),
                            _random,
                            source.InstanceID,
                            _queueCombatSound
                        );
                        RefreshTractorContribution(target);
                        if (impact == TacticalCombatImpactKind.Destroyed)
                            Destroy(target);
                    }
                    break;
                case CommandKind.System:
                    TacticalSystemEvent change = command.Change;
                    if (change.Kind == TacticalSystemEventKind.Disruption)
                    {
                        if (change.AttackDelay.HasValue && change.MovementDelay.HasValue)
                            source.ApplyDisruption(
                                change.AttackDelay.Value,
                                change.MovementDelay.Value
                            );
                        for (int arc = 0; arc < 4; arc++)
                            if ((change.DischargedArcMask & (1 << arc)) != 0)
                                source.ClearBattery((TacticalWeaponArc)arc);
                    }
                    else
                    {
                        source.ApplyComponentEvent(change.System, change.IsRepair);
                        if (
                            !change.IsRepair
                            && change.System == TacticalShipSystem.TractorBeam
                            && source.TractorTargetID != null
                            && source.GetTractorBudget() == 0
                        )
                        {
                            _tractorCommandsPending.Add(source);
                            QueueCommand(
                                new Command(
                                    CommandKind.TractorUnlock,
                                    source,
                                    GetUnit(source.TractorTargetID)
                                )
                            );
                        }
                    }
                    RefreshTractorContribution(source);
                    SystemChanged?.Invoke(source, change);
                    break;
                case CommandKind.Launch:
                    if (
                        target.IsAlive
                        && GetGroup(target)?.ActiveOrder != TacticalCombatOrder.Recover
                        && _carriers[source.InstanceID].Launch(target)
                    )
                    {
                        _initiallyDockedFighters.Remove(target);
                        Audio.Enqueue(TacticalCombatSound.FighterLaunch);
                        _spatial.Update(target);
                    }
                    break;
                case CommandKind.SetRecovery:
                    if (target == null)
                        ReleaseRecovery(source);
                    else if (
                        source.IsTargetable
                        && _recoveries.TryGetValue(
                            source,
                            out TacticalCombatCarrier.Recovery recovery
                        )
                        && ReferenceEquals(recovery.Carrier.Unit, target)
                    )
                        recovery.BeginApproach();
                    break;
                case CommandKind.Recover:
                    if (target.IsTargetable && _carriers[source.InstanceID].Recover(target))
                    {
                        RemoveFromGroup(target);
                        _spatial.Update(target);
                        FighterRecovered?.Invoke(target);
                    }
                    break;
                case CommandKind.TractorLock:
                    _tractorCommandsPending.Remove(source);
                    if (IsActiveDisplayObject(source) && IsActiveDisplayObject(target))
                        ApplyTractorLock(source, target);
                    break;
                case CommandKind.TractorUnlock:
                    _tractorCommandsPending.Remove(source);
                    source.TractorTargetID = null;
                    if (target != null)
                        ApplyTractorUnlock(source, target);
                    break;
                case CommandKind.Hyperspace:
                    if (!IsActiveDisplayObject(source))
                        break;
                    if (CheckWithdrawalReadiness(source))
                    {
                        source.Status = CombatUnitStatus.EnteringHyperspace;
                        _spatial.Update(source);
                    }
                    else
                        Scuttle(source);
                    break;
                case CommandKind.Withdraw:
                    if (source.IsBattleStation)
                    {
                        if (!BattleStation.TryWithdraw())
                            break;
                    }
                    else if (
                        source.Status
                        is not (
                            CombatUnitStatus.Active
                            or CombatUnitStatus.PreparingHyperspace
                            or CombatUnitStatus.EnteringHyperspace
                            or CombatUnitStatus.AwaitingWithdrawal
                        )
                    )
                        break;

                    source.Status = CombatUnitStatus.Withdrawn;
                    _departed.Add(source);
                    RemoveFromGroup(source);
                    _spatial.Update(source);
                    UnitWithdrawn?.Invoke(source);
                    break;
                case CommandKind.Destroy:
                    if (
                        IsActiveDisplayObject(source)
                        && (
                            (BattleStation != null && ReferenceEquals(target, BattleStation.Unit))
                            || source.Hull < 0
                            || float.IsNaN(source.Hull)
                        )
                    )
                    {
                        Audio.Enqueue(TacticalCombatAudio.GetExplosionSound(source));
                        DestructionEffectRequested?.Invoke(source);
                        if (BattleStation == null || !ReferenceEquals(target, BattleStation.Unit))
                            ReportDestruction(source, target?.InstanceID);
                        Destroy(source);
                    }
                    break;
                case CommandKind.Scuttle:
                    Scuttle(source);
                    break;
            }
        }
    }
}
