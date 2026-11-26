using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Content;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Tactical;
using Rebellion.Tactical.Synchronization;
using Rebellion.Util.Random;
using JsonUtility = global::UnityEngine.JsonUtility;
using TacticalCombatPlanner = global::Rebellion.AI.Tactical.TacticalCombatPlanner;

namespace Rebellion.Tests.Tactical
{
    [TestFixture]
    public sealed partial class TacticalCombatSynchronizationTests
    {
        [Test]
        public void ApplySynchronizedCommand_OpposingHuman_SelectsAndTargetsOnlyItsOwnForce()
        {
            TacticalCombatSession authority = CreateAuthority();

            Assert.IsTrue(
                authority.ApplySynchronizedCommand(CombatSide.Defender, Object("alliance"))
            );
            Assert.IsTrue(
                authority.ApplySynchronizedCommand(CombatSide.Defender, Object("empire"))
            );

            TacticalCombatGroup commanded = authority.GetRequestedGroup(
                authority.GetUnit("alliance")
            );
            Assert.AreSame(authority.GetUnit("empire"), commanded.RequestedTargets.Single());
            Assert.IsEmpty(
                authority.GetRequestedGroup(authority.GetUnit("empire")).RequestedTargets
            );
            Assert.AreEqual(TacticalCombatSelectionKind.Empty, authority.Selection.Kind);
        }

        [Test]
        public void ApplySynchronizedCommand_EnemyInspection_CannotIssueEnemyOrders()
        {
            TacticalCombatSession authority = CreateAuthority();
            authority.ApplySynchronizedCommand(CombatSide.Attacker, Object("alliance"));

            bool accepted = authority.ApplySynchronizedCommand(
                CombatSide.Attacker,
                new()
                {
                    Kind = TacticalCombatCommandKind.Order,
                    Order = TacticalCombatOrder.HoldFire,
                }
            );

            Assert.IsFalse(accepted);
            Assert.AreEqual(
                TacticalCombatOrder.None,
                authority.GetGroup(authority.GetUnit("alliance")).Order
            );
        }

        [Test]
        public void ApplySynchronizedCommand_InvalidIdentityAndSide_DoNotReplaceSelection()
        {
            TacticalCombatSession authority = CreateAuthority();

            Assert.IsFalse(authority.ApplySynchronizedCommand((CombatSide)2, Object("empire")));
            Assert.IsFalse(
                authority.ApplySynchronizedCommand(CombatSide.Attacker, Object("missing"))
            );
            Assert.IsFalse(
                authority.ApplySynchronizedCommand(
                    CombatSide.Attacker,
                    new() { Kind = TacticalCombatCommandKind.ObjectCategory, Category = 8 }
                )
            );

            Assert.AreEqual(TacticalCombatSelectionKind.Empty, authority.Selection.Kind);
        }

        [Test]
        public void QueueObject_Replica_RequestsAuthorityWithoutApplyingLocalSelection()
        {
            TacticalCombatSession replica = CreateReplica();
            List<TacticalCombatCommand> inputs = new();
            replica.SynchronizedCommandRequested += inputs.Add;

            replica.Selection.QueueObject(replica.GetUnit("alliance"));
            replica.AdvanceLocalSelection();

            Assert.AreEqual(TacticalCombatCommandKind.Object, inputs.Single().Kind);
            Assert.AreEqual("alliance", inputs[0].UnitID);
            Assert.AreEqual(TacticalCombatSelectionKind.Empty, replica.Selection.Kind);
            Assert.AreEqual(3, replica.SystemTime);
        }

        [Test]
        public void ApplySnapshot_RemoteSelection_RestoresLocalFactionContextWithoutAdvancingRandom()
        {
            TacticalCombatSession authority = CreateAuthority();
            SystemRandomProvider random = new(12);
            TacticalCombatSession replica = CreateReplica(random);
            authority.ApplySynchronizedCommand(CombatSide.Defender, Object("alliance"));
            long draws = random.CallCount;

            ApplyCapturedProjection(replica, authority.CaptureSnapshot(1));

            Assert.AreEqual(CombatSide.Defender, replica.PlayerSide);
            Assert.AreEqual(TacticalCombatSelectionKind.SingleObject, replica.Selection.Kind);
            Assert.AreSame(replica.GetUnit("alliance"), replica.Selection.PrimaryUnit);
            Assert.AreEqual(draws, random.CallCount);
            Assert.AreEqual(3, replica.SystemTime);
        }

        [Test]
        public void ApplySnapshot_AuthoritativeComponentRepair_UpdatesOpposingReplicaWithoutLocalRepair()
        {
            TacticalCombatSession authority = CreateSession(
                CombatSide.Attacker,
                new SystemRandomProvider(12),
                defenderDamageControl: 100
            );
            authority.ConfigureSynchronizedAuthority();
            SystemRandomProvider replicaRandom = new(12);
            TacticalCombatSession replica = CreateSession(
                CombatSide.Defender,
                replicaRandom,
                defenderDamageControl: 100
            );
            replica.ConfigureSynchronizedReplica();
            CombatUnit source = authority.GetUnit("alliance");
            CombatUnit projected = replica.GetUnit("alliance");
            int requests = 0;
            int applications = 0;
            int localRequests = 0;
            int localApplications = 0;
            authority.Damage.SystemChangeRequested += (unit, change) =>
            {
                if (ReferenceEquals(unit, source) && change.IsRepair)
                    requests++;
            };
            authority.SystemChanged += (unit, change) =>
            {
                if (ReferenceEquals(unit, source) && change.IsRepair)
                    applications++;
            };
            replica.Damage.SystemChangeRequested += (_, _) => localRequests++;
            replica.SystemChanged += (_, _) => localApplications++;
            source.ApplyComponentEvent(TacticalShipSystem.Engines, false);
            source.ApplyComponentEvent(TacticalShipSystem.Engines, false);
            TacticalCombatSnapshot damaged = authority.CaptureSnapshot(1);
            ApplyCapturedProjection(replica, damaged);
            long localDraws = replicaRandom.CallCount;
            Assert.AreEqual(2, projected.GetDamage(TacticalShipSystem.Engines));

            authority.Damage.AdvanceSystems(source, 1, new SystemRandomProvider(12));

            Assert.AreEqual(1, requests);
            Assert.AreEqual(0, applications);
            Assert.AreEqual(2, source.GetDamage(TacticalShipSystem.Engines));
            for (int frame = 0; frame < 6 * 60 && !authority.IsComplete; frame++)
                authority.Advance(1000f / 60);
            Assert.AreEqual(1, applications);
            Assert.AreEqual(1, source.GetDamage(TacticalShipSystem.Engines));
            ApplyCapturedProjection(replica, authority.CaptureSnapshot(2));

            Assert.AreEqual(1, projected.GetDamage(TacticalShipSystem.Engines));
            Assert.AreEqual(localDraws, replicaRandom.CallCount);
            Assert.AreEqual(0, localRequests);
            Assert.AreEqual(0, localApplications);
            Assert.Throws<ArgumentException>(() => replica.ApplySnapshot(damaged));
            Assert.AreEqual(1, projected.GetDamage(TacticalShipSystem.Engines));
            ApplyCapturedProjection(replica, authority.CaptureSnapshot(3));
            Assert.AreEqual(1, projected.GetDamage(TacticalShipSystem.Engines));
            Assert.AreEqual(localDraws, replicaRandom.CallCount);
            Assert.AreEqual(0, localRequests);
            Assert.AreEqual(0, localApplications);
        }

        [Test]
        public void ApplySnapshot_JsonRoundTrip_NormalizesAbsentOptionalIdentitiesAndRejectsInvalidReferences()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            TacticalCombatSnapshot captured = authority.CaptureSnapshot(1);
            Assert.IsNull(captured.Units[0].CarrierID);
            string json = JsonUtility.ToJson(captured);
            TacticalCombatSnapshot restored = JsonUtility.FromJson<TacticalCombatSnapshot>(json);
            Assert.AreEqual(string.Empty, restored.Units[0].CarrierID);

            ApplyCapturedProjection(replica, restored);

            Assert.IsNull(replica.GetUnit(restored.Units[0].UnitID).CarrierID);
            TacticalCombatSnapshot next = JsonUtility.FromJson<TacticalCombatSnapshot>(
                JsonUtility.ToJson(authority.CaptureSnapshot(2))
            );
            next.Groups[0].Members[0] = string.Empty;
            Assert.Throws<ArgumentException>(() => replica.ApplySnapshot(next));
            Assert.AreEqual(captured.SystemTime, replica.SystemTime);
        }

        [Test]
        public void ApplySnapshot_SameKindDifferentSelection_NotifiesContextReplacementOnce()
        {
            TacticalCombatSession authority = CreateAuthority(additionalDefender: true);
            TacticalCombatSession replica = CreateReplica(additionalDefender: true);
            int replacements = 0;
            replica.Selection.ContextReplaced += _ => replacements++;
            authority.ApplySynchronizedCommand(CombatSide.Defender, Object("alliance"));
            ApplyCapturedProjection(replica, authority.CaptureSnapshot(1));
            ApplyCapturedProjection(replica, authority.CaptureSnapshot(2));
            Assert.AreEqual(1, replacements);
            authority.ApplySynchronizedCommand(
                CombatSide.Defender,
                new() { Kind = TacticalCombatCommandKind.Clear }
            );
            authority.ApplySynchronizedCommand(CombatSide.Defender, Object("alliance-wing"));

            ApplyCapturedProjection(replica, authority.CaptureSnapshot(3));

            Assert.AreEqual(2, replacements);
            Assert.AreEqual(TacticalCombatSelectionKind.SingleObject, replica.Selection.Kind);
            Assert.AreSame(replica.GetUnit("alliance-wing"), replica.Selection.PrimaryUnit);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ApplySnapshot_SelectionHighlights_FollowVisibleRequestedMembers(
            bool selectCategory
        )
        {
            TacticalCombatSession authority = CreateAuthority(additionalDefender: true);
            TacticalCombatSession replica = CreateReplica(additionalDefender: true);
            CombatUnit primary = authority.GetUnit("alliance");
            CombatUnit joining = authority.GetUnit("alliance-wing");
            TacticalCombatGroup group = authority.GetRequestedGroup(primary);
            authority.SetObjectGroup(CombatSide.Defender, 0, group);
            authority.AddGroupMember(group, joining);
            Assert.AreNotSame(group, authority.GetGroup(joining));

            TacticalCombatCommand selection = selectCategory
                ? new() { Kind = TacticalCombatCommandKind.ObjectCategory, Category = 0 }
                : Object(primary.InstanceID);
            Assert.IsTrue(authority.ApplySynchronizedCommand(CombatSide.Defender, selection));
            ApplyCapturedProjection(replica, authority.CaptureSnapshot(1));
            CollectionAssert.AreEqual(
                new[] { "alliance", "alliance-wing" },
                replica.Selection.Highlights.Select(unit => unit.InstanceID)
            );
            Assert.AreEqual(selectCategory ? 2 : 1, replica.Selection.Members.Count);

            joining.Status = CombatUnitStatus.EnteringHyperspace;
            ApplyCapturedProjection(replica, authority.CaptureSnapshot(2));
            CollectionAssert.AreEqual(
                new[] { "alliance" },
                replica.Selection.Highlights.Select(unit => unit.InstanceID)
            );
            Assert.AreEqual(selectCategory ? 2 : 1, replica.Selection.Members.Count);

            joining.Status = CombatUnitStatus.Active;
            authority.SetObjectGroup(CombatSide.Defender, 0, null);
            ApplyCapturedProjection(replica, authority.CaptureSnapshot(3));
            CollectionAssert.AreEqual(
                selectCategory ? new[] { "alliance", "alliance-wing" } : new[] { "alliance" },
                replica.Selection.Highlights.Select(unit => unit.InstanceID)
            );
        }

        [Test]
        public void ApplySnapshot_QueuedGroupCreation_PreservesRequestedAndAppliedOwnership()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            authority.ApplySynchronizedCommand(CombatSide.Defender, Object("alliance"));
            authority.ApplySynchronizedCommand(CombatSide.Defender, Object("empire"));

            ApplyCapturedProjection(replica, authority.CaptureSnapshot(1));

            CombatUnit source = replica.GetUnit("alliance");
            Assert.AreNotSame(replica.GetGroup(source), replica.GetRequestedGroup(source));
            Assert.AreSame(
                replica.GetUnit("empire"),
                replica.GetRequestedGroup(source).RequestedTargets.Single()
            );
            Assert.IsFalse(replica.Groups.Contains(replica.GetRequestedGroup(source)));
        }

        [Test]
        public void ApplySnapshot_ReplacedPendingGroup_RetainsEmptyIdentityUntilAuthoritativeDispatch()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            authority.ApplySynchronizedCommand(CombatSide.Defender, Object("alliance"));
            authority.ApplySynchronizedCommand(CombatSide.Defender, Object("empire"));
            authority.ApplySynchronizedCommand(
                CombatSide.Defender,
                new()
                {
                    Kind = TacticalCombatCommandKind.Order,
                    Order = TacticalCombatOrder.HoldFire,
                }
            );
            TacticalCombatSnapshot state = authority.CaptureSnapshot(1);

            ApplyCapturedProjection(replica, state);

            Assert.IsTrue(
                state.Groups.Any(group =>
                    !group.Applied
                    && group.Members.Length == 0
                    && group.RequestedMembers.Length == 0
                )
            );
            Assert.AreEqual(
                TacticalCombatOrder.HoldFire,
                replica.GetRequestedGroup(replica.GetUnit("alliance")).Order
            );
        }

        [Test]
        public void Advance_Replica_RejectsLocalSimulationAndDirectMutations()
        {
            TacticalCombatSession replica = CreateReplica();
            Vector3 initial = replica.GetUnit("alliance").Position;

            Assert.Throws<InvalidOperationException>(() => replica.Advance(50));
            Assert.Throws<InvalidOperationException>(() =>
                replica.ConfigureClockedAdvance(() => 1)
            );
            Assert.Throws<InvalidOperationException>(() =>
                replica.CreateGroup(new[] { replica.GetUnit("alliance") })
            );
            Assert.Throws<InvalidOperationException>(() =>
                replica.SetTarget(
                    replica.GetGroup(replica.GetUnit("alliance")),
                    replica.GetUnit("empire")
                )
            );
            Assert.Throws<InvalidOperationException>(() =>
                replica.SetObjectGroup(CombatSide.Defender, 0, null)
            );

            Assert.AreEqual(initial, replica.GetUnit("alliance").Position);
            Assert.AreEqual(3, replica.SystemTime);
        }

        [Test]
        public void ApplySynchronizedEvents_NaturalFire_ReplaysCapturedEffectsAndRestoresState()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            int authoritativeShots = 0;
            int projectedShots = 0;
            authority.ShotFired += _ => authoritativeShots++;
            replica.ShotFired += shot =>
            {
                projectedShots++;
                Assert.AreSame(replica.GetUnit(shot.Source.InstanceID), shot.Source);
                Assert.AreSame(replica.GetUnit(shot.Target.InstanceID), shot.Target);
            };
            authority.ApplySynchronizedCommand(CombatSide.Attacker, Object("empire"));
            authority.ApplySynchronizedCommand(CombatSide.Attacker, Object("alliance"));
            authority.ApplySynchronizedCommand(CombatSide.Defender, Object("alliance"));
            authority.ApplySynchronizedCommand(CombatSide.Defender, Object("empire"));
            for (int frame = 0; frame < 600; frame++)
                authority.Advance(50);
            TacticalCombatSnapshot state = authority.CaptureSnapshot(1);

            ApplyCapturedProjection(replica, state);

            Assert.Greater(authoritativeShots, 0);
            Assert.AreEqual(authoritativeShots, projectedShots);
            foreach (CombatUnit unit in authority.Units)
            {
                CombatUnit projected = replica.GetUnit(unit.InstanceID);
                Assert.AreEqual(unit.Position, projected.Position);
                Assert.AreEqual(unit.Hull, projected.Hull);
                Assert.AreEqual(unit.Shields, projected.Shields);
                Assert.AreEqual(
                    unit.GetBatteryCharge(TacticalWeaponArc.Forward),
                    projected.GetBatteryCharge(TacticalWeaponArc.Forward)
                );
            }
            Assert.Throws<ArgumentException>(() => replica.ApplySnapshot(state));
            Assert.AreEqual(authoritativeShots, projectedShots);
            ApplyCapturedProjection(replica, authority.CaptureSnapshot(2));
            Assert.AreEqual(authoritativeShots, projectedShots);
        }

        [Test]
        public void ApplySnapshot_UnknownGroupParticipant_RejectsBeforeMutatingPose()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            TacticalCombatSnapshot state = authority.CaptureSnapshot(1);
            Vector3 initial = replica.GetUnit(state.Units[0].UnitID).Position;
            state.Units[0].Position = new(new Vector3(40, 40, 40));
            state.Groups[0].Members[0] = "missing";

            Assert.Throws<ArgumentException>(() => replica.ApplySnapshot(state));

            Assert.AreEqual(initial, replica.GetUnit(state.Units[0].UnitID).Position);
            Assert.AreEqual(3, replica.SystemTime);
        }

        [Test]
        public void ApplySynchronizedCommand_Withdrawal_SuspendsOnlyItsAuthenticatedFaction()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            int guestWithdrawals = 0;
            replica.Selection.WithdrawalDispatched += () => guestWithdrawals++;

            Assert.IsTrue(
                authority.ApplySynchronizedCommand(
                    CombatSide.Defender,
                    new() { Kind = TacticalCombatCommandKind.Withdrawal }
                )
            );
            ApplyCapturedProjection(replica, authority.CaptureSnapshot(1));

            Assert.IsTrue(authority.GetSide(CombatSide.Defender).IsWithdrawing);
            Assert.IsFalse(authority.GetSide(CombatSide.Attacker).IsWithdrawing);
            Assert.IsTrue(authority.IsDirectControlEnabled);
            Assert.IsFalse(replica.IsDirectControlEnabled);
            Assert.AreEqual(1, guestWithdrawals);
        }

        [Test]
        public void IsPaused_Replica_RequestsSharedPauseWithoutChangingItsLocalProjection()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            TacticalCombatCommand submitted = null;
            replica.SynchronizedCommandRequested += command => submitted = command;

            replica.IsPaused = true;

            Assert.IsFalse(replica.IsPaused);
            Assert.AreEqual(TacticalCombatCommandKind.Pause, submitted.Kind);
            Assert.IsTrue(authority.ApplySynchronizedCommand(CombatSide.Defender, submitted));
            ApplyCapturedProjection(replica, authority.CaptureSnapshot(1));
            Assert.IsTrue(replica.IsPaused);
            Assert.IsTrue(authority.IsPaused);
        }

        [Test]
        public void RequestImmediateResult_NetworkCombat_KeepsBattleRunning()
        {
            TacticalCombatSession authority = CreateAuthority();

            Assert.IsFalse(authority.CanRequestImmediateResult);
            Assert.IsFalse(authority.RequestImmediateResult());
            Assert.IsFalse(
                authority.ApplySynchronizedCommand(
                    CombatSide.Defender,
                    new() { Kind = TacticalCombatCommandKind.ImmediateResult }
                )
            );

            Assert.IsFalse(authority.IsComplete);
            Assert.IsFalse(authority.ImmediateResultRequested);
        }

        [Test]
        public void CanAttackBattleStation_Withdrawal_BlocksOrdersAndRetainsGlobalAvailability()
        {
            TacticalCombatSession session = CreateStationSession(CombatSide.Attacker);
            Assert.IsTrue(session.CanAttackBattleStation(CombatSide.Attacker));
            session.GetSide(CombatSide.Attacker).BeginWithdrawal();

            Assert.IsTrue(session.CanStartBattleStationAttack);
            Assert.IsFalse(session.CanAttackBattleStation(CombatSide.Attacker));
        }

        [Test]
        public void CanAttackBattleStation_PendingRun_BlocksGlobalAvailabilityAndNewOrders()
        {
            TacticalCombatSession session = CreateStationSession(CombatSide.Attacker);
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("fighter"));
            Assert.IsTrue(session.CanStartBattleStationAttack);
            session.SetOrder(group, TacticalCombatOrder.AttackBattleStation);

            Assert.AreEqual(TacticalCombatOrder.None, group.ActiveOrder);
            Assert.IsFalse(session.CanStartBattleStationAttack);
            Assert.IsFalse(session.CanAttackBattleStation(CombatSide.Attacker));
        }

        [Test]
        public void CaptureSnapshot_Briefing_AddressesEachFactionsExistingCue()
        {
            TacticalCombatSession authority = CreateAuthority();

            authority.Advance(50);
            TacticalCombatEvent[] cues = authority.CaptureSnapshot(1).Effects;

            Assert.IsTrue(
                cues.Any(cue =>
                    cue.Kind == TacticalCombatEventKind.Notification
                    && cue.HasRecipient
                    && cue.Recipient == CombatSide.Attacker
                    && cue.MessageID == 154
                )
            );
            Assert.IsTrue(
                cues.Any(cue =>
                    cue.Kind == TacticalCombatEventKind.Notification
                    && cue.HasRecipient
                    && cue.Recipient == CombatSide.Defender
                    && cue.MessageID == 32
                )
            );
        }

        [Test]
        public void ApplySynchronizedEvents_AddressedBriefing_QueuesOnlyTheLocalFactionsCue()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            List<int> requested = new();
            replica.Audio.NotificationRequested += (message, _) => requested.Add(message);
            authority.Advance(50);

            ApplyCapturedProjection(replica, authority.CaptureSnapshot(1));

            CollectionAssert.Contains(requested, 32);
            CollectionAssert.DoesNotContain(requested, 154);
        }

        [Test]
        public void ApplySnapshot_TractorRestraint_RestoresLiveInspectionMovement()
        {
            TacticalCombatSession authority = CreateAuthority();
            TacticalCombatSession replica = CreateReplica();
            TacticalCombatSnapshot state = authority.CaptureSnapshot(1);
            state.Units.Single(unit => unit.UnitID == "alliance").TractorRestraint = 100;
            state.Units.Single(unit => unit.UnitID == "alliance").HasTractorSources = true;

            ApplyCapturedProjection(replica, state);

            Assert.AreEqual(0, replica.GetMovementBudget(replica.GetUnit("alliance")));
            Assert.AreEqual(0, replica.GetMovementRatio(replica.GetUnit("alliance")));
            Assert.IsFalse(replica.CanWithdraw(CombatSide.Defender));
        }

        [Test]
        public void ApplySynchronizedCommand_CapitalRecovery_RejectsWithoutCreatingARequestedGroup()
        {
            TacticalCombatSession authority = CreateAuthority();
            authority.ApplySynchronizedCommand(CombatSide.Defender, Object("alliance"));
            TacticalCombatGroup owner = authority.GetRequestedGroup(authority.GetUnit("alliance"));

            bool accepted = authority.ApplySynchronizedCommand(
                CombatSide.Defender,
                new()
                {
                    Kind = TacticalCombatCommandKind.Order,
                    Order = TacticalCombatOrder.Recover,
                }
            );

            Assert.IsFalse(accepted);
            Assert.AreSame(owner, authority.GetRequestedGroup(authority.GetUnit("alliance")));
        }

        [Test]
        public void ConfigureSynchronizedAuthority_CapturedAutomationPreference_PreservesBothSidesAndGroups()
        {
            TacticalCombatSession authority = CreateSession(
                CombatSide.Attacker,
                new SystemRandomProvider(12)
            );
            authority.SetAutomated(CombatSide.Defender, true);

            authority.ConfigureSynchronizedAuthority();

            Assert.IsFalse(authority.GetSide(CombatSide.Attacker).IsAutomated);
            Assert.IsTrue(authority.GetSide(CombatSide.Defender).IsAutomated);
            Assert.IsTrue(authority.GetGroup(authority.GetUnit("alliance")).IsAutomated);
            Assert.IsFalse(authority.IsSideDirectControlEnabled(CombatSide.Defender));
            Assert.IsTrue(
                authority.ApplySynchronizedCommand(
                    CombatSide.Defender,
                    new() { Kind = TacticalCombatCommandKind.ToggleAutomation }
                )
            );
            Assert.IsTrue(authority.IsSideDirectControlEnabled(CombatSide.Defender));
        }

        [Test]
        public void ApplySynchronizedEvents_RemovedAttackGroup_RetainsMovieWithoutRestoringLiveOwnership()
        {
            TacticalCombatSession authority = CreateStationSession(CombatSide.Attacker);
            TacticalCombatSession replica = CreateStationSession(CombatSide.Defender);
            authority.ConfigureSynchronizedAuthority();
            replica.ConfigureSynchronizedReplica();
            authority.SetOrder(
                authority.GetGroup(authority.GetUnit("fighter")),
                TacticalCombatOrder.AttackBattleStation
            );
            for (int frame = 0; frame < 100 && authority.ActiveAttackRun == null; frame++)
                authority.Advance(50);
            Assert.IsNotNull(authority.ActiveAttackRun);
            authority.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Failed);
            TacticalCombatSnapshot state = authority.CaptureSnapshot(1);
            int movieRequests = 0;
            replica.AttackRunMovieRequested += _ => movieRequests++;

            ApplyCapturedProjection(replica, state);

            Assert.IsEmpty(state.Groups);
            Assert.AreEqual(
                TacticalCombatAttackRunState.Failed,
                replica.PendingAttackRunMovie.AttackRun.State
            );
            Assert.IsNull(replica.GetGroup(replica.GetUnit("fighter")));
            Assert.IsNull(replica.GetRequestedGroup(replica.GetUnit("fighter")));
            Assert.AreEqual(1, movieRequests);
            Assert.IsTrue(replica.IsPaused);
            ApplyCapturedProjection(replica, authority.CaptureSnapshot(2));
            Assert.AreEqual(1, movieRequests);
            Assert.IsTrue(authority.CompleteSynchronizedMovie(state.MovieID));
            ApplyCapturedProjection(replica, authority.CaptureSnapshot(3));
            Assert.IsNull(replica.PendingAttackRunMovie);
            Assert.IsFalse(replica.IsPaused);
        }

        [Test]
        public void CaptureSnapshot_RemoteSelectedGroupDestroyed_ClearsStaleSelectionBeforeProjection()
        {
            TacticalCombatSession authority = CreateStationSession(CombatSide.Defender);
            TacticalCombatSession replica = CreateStationSession(CombatSide.Attacker);
            authority.ConfigureSynchronizedAuthority();
            replica.ConfigureSynchronizedReplica();
            authority.ApplySynchronizedCommand(
                CombatSide.Attacker,
                new() { Kind = TacticalCombatCommandKind.FighterCategory, Category = 0 }
            );
            authority.SetOrder(
                authority.GetGroup(authority.GetUnit("fighter")),
                TacticalCombatOrder.AttackBattleStation
            );
            for (int frame = 0; frame < 100 && authority.ActiveAttackRun == null; frame++)
                authority.Advance(50);
            authority.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Failed);

            ApplyCapturedProjection(replica, authority.CaptureSnapshot(1));

            Assert.AreEqual(TacticalCombatSelectionKind.Empty, replica.Selection.Kind);
            Assert.IsEmpty(replica.Selection.Groups);
        }

        [Test]
        public void CaptureSnapshot_RemovedAttackHelper_EmitsNoFurtherNotificationsOrMovies()
        {
            TacticalCombatSession authority = CreateStationSession(CombatSide.Attacker);
            authority.ConfigureSynchronizedAuthority();
            authority.SetOrder(
                authority.GetGroup(authority.GetUnit("fighter")),
                TacticalCombatOrder.AttackBattleStation
            );
            for (int frame = 0; frame < 100 && authority.ActiveAttackRun == null; frame++)
                authority.Advance(50);
            TacticalCombatAttackRun removed = authority.ActiveAttackRun;
            removed.ApplyState(TacticalCombatAttackRunState.Failed);
            TacticalCombatMovieRequest movie = authority.PendingAttackRunMovie;
            TacticalCombatSnapshot first = authority.CaptureSnapshot(1);

            removed.ApplyState(TacticalCombatAttackRunState.Failed);
            TacticalCombatSnapshot second = authority.CaptureSnapshot(2);

            Assert.IsTrue(
                first.Effects.Any(effect =>
                    effect.Kind == TacticalCombatEventKind.Notification && effect.MessageID == 315
                )
            );
            Assert.IsEmpty(second.Effects);
            Assert.AreSame(movie, authority.PendingAttackRunMovie);
            Assert.AreEqual(first.MovieID, second.MovieID);
        }

        [Test]
        public void ApplySynchronizedEvents_FailedAttack_PreservesCueMovieAndDestructionOrderingOnce()
        {
            TacticalCombatSession authority = CreateStationSession(CombatSide.Defender);
            TacticalCombatSession replica = CreateStationSession(CombatSide.Attacker);
            authority.ConfigureSynchronizedAuthority();
            replica.ConfigureSynchronizedReplica();
            authority.SetOrder(
                authority.GetGroup(authority.GetUnit("fighter")),
                TacticalCombatOrder.AttackBattleStation
            );
            for (int frame = 0; frame < 100 && authority.ActiveAttackRun == null; frame++)
                authority.Advance(50);
            List<string> observed = new();
            replica.Audio.NotificationRequested += (message, _) =>
            {
                if (message == 315)
                    observed.Add("cue");
            };
            replica.AttackRunMovieRequested += _ => observed.Add("movie");
            replica.UnitDestroyed += unit =>
            {
                if (unit.InstanceID == "fighter")
                    observed.Add("destroyed");
            };
            authority.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Failed);

            ApplyCapturedProjection(replica, authority.CaptureSnapshot(1));
            ApplyCapturedProjection(replica, authority.CaptureSnapshot(2));

            CollectionAssert.AreEqual(new[] { "cue", "movie", "destroyed" }, observed);
        }

        [TestCase(TacticalCombatCommandKind.Pause)]
        [TestCase(TacticalCombatCommandKind.Clear)]
        [TestCase(TacticalCombatCommandKind.FighterCategory)]
        [TestCase(TacticalCombatCommandKind.Waypoint)]
        public void ApplySynchronizedCommand_PendingMovie_RejectsInflightInputWithoutMutatingSelectionOrPause(
            TacticalCombatCommandKind kind
        )
        {
            TacticalCombatSession authority = CreateStationSession(
                CombatSide.Attacker,
                reserveFighter: true
            );
            authority.ConfigureSynchronizedAuthority();
            authority.ApplySynchronizedCommand(
                CombatSide.Attacker,
                new() { Kind = TacticalCombatCommandKind.FighterCategory, Category = 1 }
            );
            TacticalCombatGroup selected = authority.Selection.Group;
            authority.SetOrder(
                authority.GetGroup(authority.GetUnit("fighter")),
                TacticalCombatOrder.AttackBattleStation
            );
            for (int frame = 0; frame < 100 && authority.ActiveAttackRun == null; frame++)
                authority.Advance(50);
            authority.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Failed);
            int movieID = authority.PendingSynchronizedMovieID;

            bool accepted = authority.ApplySynchronizedCommand(
                CombatSide.Attacker,
                new()
                {
                    Kind = kind,
                    Paused = true,
                    Category = 1,
                    Waypoint = new(authority.Navigation.Waypoints[0]),
                }
            );

            Assert.IsFalse(accepted);
            Assert.AreSame(selected, authority.Selection.Group);
            Assert.AreEqual(TacticalCombatSelectionKind.FighterGroup, authority.Selection.Kind);
            Assert.IsEmpty(selected.RequestedWaypoints);
            Assert.AreEqual(movieID, authority.PendingSynchronizedMovieID);
            Assert.IsTrue(authority.CompleteSynchronizedMovie(movieID));
            Assert.IsFalse(authority.IsPaused);
        }

        /// <summary>
        /// Applies a captured projection through the same separated state and reliable-effect boundaries as live playback.
        /// </summary>
        /// <param name="session">The passive encounter receiving the projection.</param>
        /// <param name="frame">The captured state and its separately delivered effects.</param>
        private static void ApplyCapturedProjection(
            TacticalCombatSession session,
            TacticalCombatSnapshot frame
        )
        {
            TacticalCombatEvent[] effects = frame.Effects;
            frame.Effects = Array.Empty<TacticalCombatEvent>();
            session.ApplySnapshot(frame);
            session.ApplySynchronizedEvents(effects);
        }

        /// <summary>
        /// Creates a battle station and an eligible opposite-faction fighter group before any playback.
        /// </summary>
        /// <param name="player">The faction presented by this local view.</param>
        /// <param name="reserveFighter">Whether an independent category remains selectable after an attack loss.</param>
        /// <returns>The unadvanced encounter.</returns>
        private static TacticalCombatSession CreateStationSession(
            CombatSide player,
            bool reserveFighter = false
        )
        {
            CombatUnit fighter = new(
                new Starfighter
                {
                    InstanceID = "fighter",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                    Agility = 20,
                    Torpedoes = 100,
                },
                CombatSide.Attacker,
                1
            );
            CombatUnit station = new(
                new CapitalShip
                {
                    InstanceID = "station",
                    CanDestroyPlanets = true,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    MaxHullStrength = 1000,
                    CurrentHullStrength = 1000,
                },
                CombatSide.Defender,
                1000001
            );
            TacticalCombatGroup group = new(2, new[] { fighter }, Vector3.UnitZ)
            {
                FighterCategory = 0,
            };
            List<CombatUnit> units = new() { fighter, station };
            List<TacticalCombatGroup> groups = new() { group };
            if (reserveFighter)
            {
                CombatUnit reserve = new(
                    new Starfighter
                    {
                        InstanceID = "reserve",
                        MaxSquadronSize = 12,
                        CurrentSquadronSize = 12,
                        Agility = 20,
                        Torpedoes = 100,
                    },
                    CombatSide.Attacker,
                    2
                );
                units.Add(reserve);
                groups.Add(
                    new TacticalCombatGroup(3, new[] { reserve }, Vector3.UnitZ)
                    {
                        FighterCategory = 1,
                    }
                );
            }
            return new(
                units,
                groups,
                new TacticalCombatSide(
                    CombatSide.Attacker,
                    0,
                    Vector3.UnitZ,
                    true,
                    0,
                    0,
                    null,
                    null,
                    0,
                    0,
                    false
                ),
                new TacticalCombatSide(
                    CombatSide.Defender,
                    1,
                    -Vector3.UnitZ,
                    true,
                    0,
                    0,
                    null,
                    null,
                    0,
                    0,
                    false
                ),
                player,
                100,
                new SystemRandomProvider(42),
                TacticalCombatPlanner.Attach
            );
        }

        /// <summary>
        /// Creates a battle authority with two manually controlled opposing factions.
        /// </summary>
        /// <param name="additionalDefender">Whether to include another own-faction inspection choice.</param>
        /// <returns>The unadvanced shared simulation.</returns>
        private static TacticalCombatSession CreateAuthority(bool additionalDefender = false)
        {
            TacticalCombatSession session = CreateSession(
                CombatSide.Attacker,
                new SystemRandomProvider(12),
                additionalDefender
            );
            session.ConfigureSynchronizedAuthority();
            return session;
        }

        /// <summary>
        /// Creates the opposite faction's passive view with its own local presentation random stream.
        /// </summary>
        /// <param name="random">The optional observable local presentation stream.</param>
        /// <param name="additionalDefender">Whether to include another own-faction inspection choice.</param>
        /// <returns>The unadvanced passive view.</returns>
        private static TacticalCombatSession CreateReplica(
            SystemRandomProvider random = null,
            bool additionalDefender = false
        )
        {
            TacticalCombatSession session = CreateSession(
                CombatSide.Defender,
                random ?? new SystemRandomProvider(12),
                additionalDefender
            );
            session.ConfigureSynchronizedReplica();
            return session;
        }

        /// <summary>
        /// Captures two complete armed capital ships without changing campaign state or priming batteries.
        /// </summary>
        /// <param name="player">The faction whose selection is presented locally.</param>
        /// <param name="random">The encounter's local random stream.</param>
        /// <param name="additionalDefender">Whether to include another own-faction inspection choice.</param>
        /// <param name="defenderDamageControl">The defender's component repair chance out of 100.</param>
        /// <returns>The captured encounter.</returns>
        private static TacticalCombatSession CreateSession(
            CombatSide player,
            IRandomNumberProvider random,
            bool additionalDefender = false,
            int defenderDamageControl = 0
        )
        {
            CombatUnit empire = CreateUnit("empire", CombatSide.Attacker, 1000001, -Vector3.UnitZ);
            CombatUnit alliance = CreateUnit(
                "alliance",
                CombatSide.Defender,
                1,
                Vector3.UnitZ,
                defenderDamageControl
            );
            empire.Position = new Vector3(0, 0, 10);
            alliance.Position = new Vector3(0, 0, -10);
            List<CombatUnit> units = new() { alliance, empire };
            List<TacticalCombatGroup> groups = new()
            {
                new TacticalCombatGroup(10, new[] { alliance }, Vector3.UnitZ),
                new TacticalCombatGroup(1000010, new[] { empire }, -Vector3.UnitZ),
            };
            if (additionalDefender)
            {
                CombatUnit wing = CreateUnit(
                    "alliance-wing",
                    CombatSide.Defender,
                    2,
                    Vector3.UnitZ
                );
                wing.Position = new Vector3(12, 0, -10);
                units.Add(wing);
                groups.Add(new TacticalCombatGroup(11, new[] { wing }, Vector3.UnitZ));
            }
            return new TacticalCombatSession(
                units,
                groups,
                new TacticalCombatSide(
                    CombatSide.Attacker,
                    1,
                    -Vector3.UnitZ,
                    true,
                    0,
                    0,
                    null,
                    null,
                    0,
                    0,
                    false
                ),
                new TacticalCombatSide(
                    CombatSide.Defender,
                    0,
                    Vector3.UnitZ,
                    true,
                    0,
                    0,
                    null,
                    null,
                    0,
                    0,
                    false
                ),
                player,
                100,
                random,
                TacticalCombatPlanner.Attach
            );
        }

        /// <summary>
        /// Creates one complete directional combat participant from runtime ship data.
        /// </summary>
        /// <param name="id">The campaign participant identity.</param>
        /// <param name="side">The encounter faction.</param>
        /// <param name="index">The stable tactical registration identity.</param>
        /// <param name="heading">The deployment heading.</param>
        /// <param name="damageControl">The participant's component repair chance out of 100.</param>
        /// <returns>The captured armed participant.</returns>
        private static CombatUnit CreateUnit(
            string id,
            CombatSide side,
            int index,
            Vector3 heading,
            int damageControl = 0
        )
        {
            CapitalShip ship = new()
            {
                InstanceID = id,
                OwnerInstanceID = side == CombatSide.Attacker ? "FNEMP1" : "FNALL1",
                MaxHullStrength = 1000,
                CurrentHullStrength = 1000,
                Hyperdrive = 1,
                SublightSpeed = 4,
                WeaponRecharge = 10,
                DamageControl = damageControl,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.LaserCannon] = new[] { 20, 20, 20, 20, 100 };
            return new(ship, side, index)
            {
                PresentationIndex = 1,
                Heading = heading,
                DesiredHeading = heading,
            };
        }

        /// <summary>
        /// Addresses a registered participant through semantic selection input.
        /// </summary>
        /// <param name="unitID">The registered participant identity.</param>
        /// <returns>The semantic object command.</returns>
        private static TacticalCombatCommand Object(string unitID) =>
            new() { Kind = TacticalCombatCommandKind.Object, UnitID = unitID };
    }
}
