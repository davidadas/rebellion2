using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatSessionTests
    {
        [Test]
        public void Advance_WeaponImpacts_LeaveCampaignHullUnchanged()
        {
            CapitalShip attacker = CreateShip("attacker", 1000, 100);
            CapitalShip defender = CreateShip("defender", 1000, 0);
            TacticalCombatSession session = CreateSession(attacker, defender);
            AdvanceSeconds(session, 20);

            Assert.Less(session.GetUnit("defender").Systems.Hull, 1000);
            Assert.AreEqual(1000, defender.CurrentHullStrength);
        }

        [Test]
        public void Advance_SameSeedAndCommands_ProducesIdenticalBattleResults()
        {
            TacticalCombatSession first = CreateSession(
                CreateShip("a", 100, 100),
                CreateShip("b", 100, 100)
            );
            TacticalCombatSession second = CreateSession(
                CreateShip("a", 100, 100),
                CreateShip("b", 100, 100)
            );
            AdvanceSeconds(first, 60);
            AdvanceSeconds(second, 60);

            Assert.AreEqual(first.IsComplete, second.IsComplete);
            for (int index = 0; index < first.Units.Count; index++)
            {
                Assert.AreEqual(first.Units[index].Systems.Hull, second.Units[index].Systems.Hull);
                Assert.AreEqual(first.Units[index].Status, second.Units[index].Status);
                Assert.AreEqual(first.Units[index].Position, second.Units[index].Position);
            }
        }

        [Test]
        public void Advance_DischargeBeforeImpact_DelaysHullDamage()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 100),
                CreateShip("b", 1000, 0)
            );
            bool observedDischargeBeforeImpact = false;
            session.ShotFired += shot =>
                observedDischargeBeforeImpact |= shot.Target.Systems.Hull == 1000;
            AdvanceSeconds(session, 20);

            Assert.IsTrue(observedDischargeBeforeImpact);
        }

        [Test]
        public void Advance_DestroyedSide_CompletesEncounter()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 1000),
                CreateShip("b", 10, 0)
            );
            AdvanceSeconds(session, 30);

            Assert.IsTrue(session.IsComplete);
            Assert.AreEqual(TacticalCombatUnitStatus.Destroyed, session.GetUnit("b").Status);
            Assert.IsNull(session.GetGroup(session.GetUnit("b")));
        }

        [Test]
        public void Advance_PausedEncounter_DoesNotAdvanceSystems()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 10),
                CreateShip("b", 100, 10)
            );
            session.IsPaused = true;
            AdvanceSeconds(session, 20);

            Assert.AreEqual(3, session.SystemTime);
            Assert.AreEqual(100, session.GetUnit("b").Systems.Hull);
        }

        [Test]
        public void CanWithdraw_OpposingGravityWell_PreventsDeparture()
        {
            CapitalShip interdictor = CreateShip("b", 100, 0);
            interdictor.HasGravityWell = true;
            TacticalCombatSession session = CreateSession(CreateShip("a", 100, 0), interdictor);

            Assert.IsFalse(session.CanWithdraw(CombatSide.Attacker));
        }

        [Test]
        public void CanWithdraw_DisabledHyperdrive_PreventsDeparture()
        {
            CapitalShip ship = CreateShip("a", 100, 0);
            ship.SystemDamage.PrimaryHyperdrive = 1;
            TacticalCombatSession session = CreateSession(ship, CreateShip("b", 100, 0));

            Assert.IsFalse(session.CanWithdraw(CombatSide.Attacker));
        }

        [Test]
        public void BeginWithdrawal_EligibleSide_StartsThirtySecondMinimumCountdown()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );

            Assert.IsTrue(session.BeginWithdrawal(CombatSide.Attacker));
            Assert.AreEqual(30000, session.GetSide(CombatSide.Attacker).WithdrawalMilliseconds);
            Assert.AreEqual(TacticalCombatOrder.Withdraw, session.Groups[0].Order);
        }

        [Test]
        public void Advance_WithdrawnShip_CompletesWithoutCampaignMutation()
        {
            CapitalShip ship = CreateShip("a", 100, 0);
            TacticalCombatSession session = CreateSession(ship, CreateShip("b", 100, 0));
            session.BeginWithdrawal(CombatSide.Attacker);
            AdvanceSeconds(session, 100);

            Assert.IsTrue(session.IsComplete);
            Assert.AreEqual(TacticalCombatUnitStatus.Withdrawn, session.GetUnit("a").Status);
            Assert.IsNull(session.GetGroup(session.GetUnit("a")));
            Assert.AreEqual(100, ship.CurrentHullStrength);
        }

        [Test]
        public void SetOrder_BeforeCommandDispatch_PreservesActiveBehavior()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("a"));
            session.SetOrder(group, TacticalCombatOrder.HoldFire);
            Assert.AreEqual(TacticalCombatOrder.HoldFire, group.Order);
            Assert.AreEqual(TacticalCombatOrder.None, group.ActiveOrder);
        }

        [Test]
        public void SetOrder_AfterCommandDispatch_AppliesActiveBehavior()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("a"));
            session.SetOrder(group, TacticalCombatOrder.HoldFire);
            AdvanceSeconds(session, 8);
            Assert.AreEqual(TacticalCombatOrder.HoldFire, group.ActiveOrder);
        }

        [Test]
        public void CreateGroup_DuplicateMember_LeavesPreviousFormationIntact()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatUnit unit = session.GetUnit("a");
            TacticalCombatGroup group = session.GetGroup(unit);
            Assert.Throws<ArgumentException>(() => session.CreateGroup(new[] { unit, unit }));
            Assert.AreSame(group, session.GetGroup(unit));
            Assert.AreEqual(1, group.Members.Count);
        }

        [Test]
        public void CreateGroup_QueuedOrderForReplacedGroup_DoesNotAffectReplacement()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatUnit member = session.GetUnit("a");
            TacticalCombatGroup previous = session.GetGroup(member);
            session.SetOrder(previous, TacticalCombatOrder.Hammer);

            TacticalCombatGroup replacement = session.CreateGroup(new[] { member });
            AdvanceSeconds(session, 8);

            CollectionAssert.DoesNotContain(session.Groups, previous);
            Assert.AreSame(replacement, session.GetGroup(member));
            Assert.AreEqual(TacticalCombatOrder.None, replacement.ActiveOrder);
            Assert.IsEmpty(replacement.RequestedWaypoints);
        }

        [Test]
        public void CreateGroup_BeforeDispatch_PreservesAppliedOwnership()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatUnit first = session.GetUnit("first");
            TacticalCombatGroup previous = session.GetGroup(first);

            TacticalCombatGroup requested = session.CreateGroup(new[] { first });

            Assert.AreSame(previous, session.GetGroup(first));
            Assert.AreSame(requested, session.GetRequestedGroup(first));
            CollectionAssert.AreEqual(new[] { first }, previous.Members);
            Assert.IsEmpty(previous.RequestedMembers);
            Assert.IsEmpty(requested.Members);
            CollectionAssert.DoesNotContain(session.Groups, requested);
        }

        [Test]
        public void CreateGroup_PausedCommands_PreservesAppliedRosterUntilResume()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatUnit first = session.GetUnit("first");
            TacticalCombatGroup previous = session.GetGroup(first);
            session.IsPaused = true;
            TacticalCombatGroup requested = session.CreateGroup(new[] { first });

            AdvanceSeconds(session, 8);

            Assert.AreSame(previous, session.GetGroup(first));
            Assert.AreSame(requested, session.GetRequestedGroup(first));
            session.IsPaused = false;
            AdvanceSeconds(session, 8);
            Assert.AreSame(requested, session.GetGroup(first));
        }

        [Test]
        public void CreateGroup_OrderedMixedSelection_PreservesLeaderAndCommandGroupKind()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatUnit[] ordered =
            {
                session.GetUnit("fighter"),
                session.GetUnit("first"),
                session.GetUnit("second"),
            };

            TacticalCombatGroup requested = session.CreateGroup(ordered);
            TacticalCombatUnit[] registryOrder = { ordered[2], ordered[0], ordered[1] };
            CollectionAssert.AreEqual(registryOrder, requested.RequestedMembers);
            Assert.AreSame(ordered[0], requested.RequestedLeader);
            AdvanceSeconds(session, 8);

            CollectionAssert.AreEqual(registryOrder, requested.Members);
            Assert.AreSame(ordered[0], requested.PrimaryMember);
            Assert.AreEqual(TacticalCombatGroupKind.Command, requested.Kind);
            Assert.AreEqual(TacticalCombatFormationKind.Line, requested.Formation.Kind);
        }

        [Test]
        public void CreateGroup_TwoReassignmentsBeforeDispatch_AppliesMembershipCommandsInOrder()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatUnit first = session.GetUnit("first");
            TacticalCombatGroup transient = session.CreateGroup(new[] { first });
            TacticalCombatGroup final = session.CreateGroup(new[] { first });

            AdvanceSeconds(session, 8);

            Assert.AreSame(final, session.GetGroup(first));
            Assert.AreSame(final, session.GetRequestedGroup(first));
            CollectionAssert.DoesNotContain(session.Groups, transient);
            Assert.IsEmpty(transient.Members);
            Assert.IsEmpty(transient.RequestedMembers);
        }

        [Test]
        public void AddGroupMember_ExistingDestination_AppendsAfterDelayedRemoval()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatUnit first = session.GetUnit("first");
            TacticalCombatUnit second = session.GetUnit("second");
            TacticalCombatGroup previous = session.GetGroup(second);
            TacticalCombatGroup destination = session.GetGroup(first);

            session.AddGroupMember(destination, second);
            Assert.AreSame(previous, session.GetGroup(second));
            CollectionAssert.AreEqual(new[] { second, first }, destination.RequestedMembers);
            AdvanceSeconds(session, 8);

            CollectionAssert.AreEqual(new[] { second, first }, destination.Members);
            Assert.AreSame(destination, session.GetGroup(second));
            CollectionAssert.DoesNotContain(session.Groups, previous);
        }

        [Test]
        public void SetTarget_FriendlyDesignation_PreservesRequestedAndAppliedLanes()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("first"));
            TacticalCombatUnit friendly = session.GetUnit("second");
            session.SetTarget(group, friendly);
            session.SetOrder(group, TacticalCombatOrder.HoldFire);
            CollectionAssert.AreEqual(new[] { friendly }, group.RequestedTargets);
            Assert.IsNull(group.PrimaryTarget);

            AdvanceSeconds(session, 8);

            Assert.AreSame(friendly, group.PrimaryTarget);
            Assert.AreEqual(TacticalCombatOrder.HoldFire, group.ActiveOrder);
        }

        [Test]
        public void RemoveGroupMember_DesignatedLeader_PromotesFirstRemainingRegistryMember()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatUnit first = session.GetUnit("first");
            TacticalCombatUnit second = session.GetUnit("second");
            TacticalCombatUnit fighter = session.GetUnit("fighter");
            TacticalCombatGroup group = session.CreateGroup(new[] { first, fighter, second });
            AdvanceSeconds(session, 8);

            session.RemoveGroupMember(group, first);
            Assert.AreSame(second, group.RequestedLeader);
            Assert.AreSame(first, group.PrimaryMember);
            AdvanceSeconds(session, 8);

            Assert.AreSame(second, group.PrimaryMember);
        }

        [Test]
        public void SetObjectGroup_PreviousOwnerRemoved_KeepsReplacementCategory()
        {
            TacticalCombatSession session = CreateMembershipSession();
            TacticalCombatUnit first = session.GetUnit("first");
            TacticalCombatGroup replacement = session.CreateGroup(new[] { first });
            Assert.IsNull(session.GetObjectGroup(CombatSide.Attacker, 0));
            session.SetObjectGroup(CombatSide.Attacker, 0, replacement);

            AdvanceSeconds(session, 8);

            Assert.AreSame(replacement, session.GetObjectGroup(CombatSide.Attacker, 0));
        }

        [Test]
        public void CreateResult_UnfinishedEncounter_RejectsPrematureCommit()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            Assert.Throws<InvalidOperationException>(() => session.CreateResult());
        }

        [Test]
        public void CreateResult_CompletedEncounter_CapturesLossesWithoutChangingCampaignShips()
        {
            CapitalShip defender = CreateShip("b", 10, 0);
            TacticalCombatSession session = CreateSession(CreateShip("a", 1000, 1000), defender);
            AdvanceSeconds(session, 30);
            SpaceCombatResult result = session.CreateResult();
            Assert.AreEqual(0, result.ShipDamage.Find(damage => damage.Ship == defender).HullAfter);
            Assert.AreEqual(10, defender.CurrentHullStrength);
        }

        [Test]
        public void SetAttackMode_QueuedMode_LeavesActiveEngagementUntilCommandPhase()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 0),
                CreateShip("b", 1000, 0)
            );
            TacticalCombatGroup group = session.Groups[0];
            group.SetAttackMode(true);
            Assert.IsTrue(group.CloseAttack);
            Assert.IsFalse(group.ActiveCloseAttack);
            AdvanceSeconds(session, 5);
            Assert.IsTrue(group.ActiveCloseAttack);
        }

        [Test]
        public void SetFormation_QueuedFormation_ChangesOnlyDuringCommandPhase()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 0),
                CreateShip("b", 1000, 0)
            );
            TacticalCombatGroup group = session.Groups[0];
            session.SetFormation(group, TacticalCombatFormationKind.Surround);
            Assert.AreEqual(TacticalCombatFormationKind.Line, group.Formation.Kind);
            AdvanceSeconds(session, 5);
            Assert.AreEqual(TacticalCombatFormationKind.Surround, group.Formation.Kind);
        }

        [Test]
        public void RemoveTarget_BeforeDispatch_ChangesRequestedListOnly()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("a"));
            session.RemoveTarget(group, session.GetUnit("b"));
            Assert.IsEmpty(group.RequestedTargets);
            Assert.AreSame(session.GetUnit("b"), group.PrimaryTarget);
        }

        [Test]
        public void RemoveTarget_AddThenRemoveBeforeDispatch_LeavesNoActiveTarget()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("a"));
            session.SetTarget(group, session.GetUnit("b"), true);
            session.RemoveTarget(group, session.GetUnit("b"));
            AdvanceSeconds(session, 5);
            Assert.IsEmpty(group.RequestedTargets);
            Assert.IsEmpty(group.Targets);
        }

        [Test]
        public void RemoveTarget_RemoveThenAddBeforeDispatch_PreservesLatestRequest()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("a"));
            TacticalCombatUnit target = session.GetUnit("b");
            session.RemoveTarget(group, target);
            session.SetTarget(group, target, true);
            AdvanceSeconds(session, 5);
            CollectionAssert.AreEqual(new[] { target }, group.RequestedTargets);
            CollectionAssert.AreEqual(new[] { target }, group.Targets);
        }

        [Test]
        public void RemoveWaypoint_AddThenRemoveBeforeDispatch_CancelsRoute()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("a"));
            Vector3 waypoint = new(10000, 0, 0);
            session.SetWaypoint(group, waypoint, true);
            session.RemoveWaypoint(group, waypoint);
            AdvanceSeconds(session, 5);
            Assert.IsEmpty(group.RequestedWaypoints);
            Assert.IsFalse(group.HasWaypoints);
        }

        [Test]
        public void SetWaypoint_RepeatedAppend_StoresOneNavigationPoint()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("a"));
            Vector3 waypoint = new(10000, 0, 0);
            session.SetWaypoint(group, waypoint, true);
            session.SetWaypoint(group, waypoint, true);
            AdvanceSeconds(session, 5);
            CollectionAssert.AreEqual(new[] { waypoint }, group.RequestedWaypoints);
        }

        [Test]
        public void SetOrder_HoldFire_PreservesDesignatedTarget()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("a"));
            session.SetOrder(group, TacticalCombatOrder.HoldFire);
            AdvanceSeconds(session, 5);
            CollectionAssert.AreEqual(new[] { session.GetUnit("b") }, group.RequestedTargets);
            Assert.AreSame(session.GetUnit("b"), group.PrimaryTarget);
        }

        [Test]
        public void SetOrder_None_ClearsRequestedTargetsBeforeDispatch()
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 100, 0),
                CreateShip("b", 100, 0)
            );
            TacticalCombatGroup group = session.GetGroup(session.GetUnit("a"));
            session.SetOrder(group, TacticalCombatOrder.None);
            Assert.IsEmpty(group.RequestedTargets);
            Assert.AreSame(session.GetUnit("b"), group.PrimaryTarget);
        }

        [Test]
        public void ActiveAttackRun_SuccessMovie_RemovesStationBeforeLaunchAndResetsOrderAfterward()
        {
            TacticalCombatSession session = CreateAttackSession();
            TacticalCombatOrder? orderAtLaunch = null;
            TacticalCombatUnitStatus? stationAtLaunch = null;
            session.AttackRunMovieRequested += request =>
            {
                orderAtLaunch = request.AttackRun.Group.Order;
                stationAtLaunch = session.BattleStation.Unit.Status;
            };

            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);

            Assert.AreEqual(TacticalCombatUnitStatus.Destroyed, stationAtLaunch);
            Assert.AreEqual(TacticalCombatOrder.AttackBattleStation, orderAtLaunch);
            Assert.AreEqual(TacticalCombatOrder.None, session.ActiveAttackRun.Group.Order);
        }

        [Test]
        public void ActiveAttackRun_FailureMovie_DestroysSquadronAfterLaunch()
        {
            TacticalCombatSession session = CreateAttackSession();
            TacticalCombatUnitStatus? atLaunch = null;
            session.AttackRunMovieRequested += _ => atLaunch = session.GetUnit("fighter").Status;

            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Failed);

            Assert.AreEqual(TacticalCombatUnitStatus.AttackingBattleStation, atLaunch);
            Assert.AreEqual(TacticalCombatUnitStatus.Destroyed, session.GetUnit("fighter").Status);
        }

        [Test]
        public void SetOrder_FailedAttackGroupRemoved_AllowsNextEligibleAttack()
        {
            TacticalCombatSession session = CreateAttackSession(reserveFighter: true);
            TacticalCombatAttackRun failed = session.ActiveAttackRun;
            TacticalCombatGroup reserve = session.GetGroup(session.GetUnit("reserve"));

            failed.ApplyState(TacticalCombatAttackRunState.Failed);
            session.PendingAttackRunMovie.Complete();

            CollectionAssert.DoesNotContain(session.Groups, failed.Group);
            Assert.IsNull(session.GetGroup(session.GetUnit("fighter")));
            Assert.IsNull(session.GetUnit("fighter").GroupID);
            Assert.IsTrue(session.CanAttackBattleStation(CombatSide.Attacker));
            session.SetOrder(reserve, TacticalCombatOrder.AttackBattleStation);
            AdvanceSeconds(session, 5);

            Assert.AreNotSame(failed, session.ActiveAttackRun);
            Assert.AreSame(reserve, session.ActiveAttackRun.Group);
        }

        [Test]
        public void Advance_AttackConclusionQueuedAfterRegrouping_IgnoresRemovedRun()
        {
            TacticalCombatSession session = CreateAttackSession();
            TacticalCombatAttackRun removed = session.ActiveAttackRun;
            TacticalCombatGroup replacement = session.CreateGroup(
                new[] { session.GetUnit("fighter") }
            );
            removed.Advance(120000);

            AdvanceSeconds(session, 5);

            Assert.IsNull(session.PendingAttackRunMovie);
            Assert.IsTrue(session.BattleStation.IsOperational);
            Assert.AreSame(replacement, session.GetGroup(session.GetUnit("fighter")));
            Assert.AreEqual(TacticalCombatOrder.None, replacement.ActiveOrder);
        }

        [Test]
        public void Advance_MovieWithoutPresentationListener_RemainsPausedUntilAcknowledged()
        {
            TacticalCombatSession session = CreateAttackSession();
            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);
            int time = session.SystemTime;

            session.Advance(10000);

            Assert.AreEqual(time, session.SystemTime);
            Assert.IsFalse(session.PendingAttackRunMovie.IsComplete);
        }

        [Test]
        public void Advance_PausedElapsed_ResumesOneModuloReducedTick()
        {
            TacticalCombatSession session = CreateAttackSession();
            session.IsPaused = true;
            int before = session.SystemTime;
            session.Advance(20500);
            session.IsPaused = false;

            session.Advance(1);
            session.Advance(1);

            Assert.AreEqual(before + 1, session.SystemTime);
        }

        [Test]
        public void ActiveAttackRun_EnabledNotifications_SuspendsSchedulingOnlyDuringMovieLaunch()
        {
            TacticalCombatSession session = CreateAttackSession();
            bool? enabledDuringLaunch = null;
            session.AttackRunMovieRequested += _ =>
                enabledDuringLaunch = session.Audio.Notifications.Enabled;

            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);

            Assert.AreEqual(false, enabledDuringLaunch);
            Assert.IsTrue(session.Audio.Notifications.Enabled);
        }

        [Test]
        public void ActiveAttackRun_DisabledNotifications_PreservesDisabledSchedulingAfterLaunch()
        {
            TacticalCombatSession session = CreateAttackSession();
            session.Audio.Notifications.Enabled = false;

            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Succeeded);

            Assert.IsFalse(session.Audio.Notifications.Enabled);
        }

        [TestCase(CombatSide.Attacker, true)]
        [TestCase(CombatSide.Defender, false)]
        public void FireSuperlaser_LiveTarget_ReportsSoundAndPlayerAdvisorBeforeVisual(
            CombatSide player,
            bool allianceAdvisor
        )
        {
            CapitalShip station = CreateShip("station", 1000, 0);
            station.CanDestroyPlanets = true;
            TacticalCombatSession session = CreateSession(
                CreateShip("target", 1000, 0),
                station,
                NotificationBank(20, 145),
                player
            );
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };
            session.SuperlaserFired += (_, _) => output.Add("visual");
            Assert.IsTrue(session.FireSuperlaser(session.GetUnit("target")));
            AdvanceSeconds(session, 5);
            CollectionAssert.AreEqual(
                allianceAdvisor ? new[] { "20", "145", "visual" } : new[] { "20", "visual" },
                output
            );
        }

        [Test]
        public void FireSuperlaser_TargetDestroyedBeforeDispatch_StillDispatchesVisualWithoutSound()
        {
            CapitalShip station = CreateShip("station", 1000, 0);
            station.CanDestroyPlanets = true;
            TacticalCombatSession session = CreateSession(
                CreateShip("target", 1000, 0),
                station,
                NotificationBank(20, 145)
            );
            TacticalCombatUnit target = session.GetUnit("target");
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };
            session.SuperlaserFired += (_, _) => output.Add("visual");
            Assert.IsTrue(session.FireSuperlaser(target));
            target.Systems.Destroy();
            AdvanceSeconds(session, 5);
            CollectionAssert.AreEqual(new[] { "visual" }, output);
        }

        [TestCase(CombatSide.Attacker, false)]
        [TestCase(CombatSide.Defender, true)]
        public void Advance_StationChargeCrossing_AnnouncesOnlyToEmpireBeforeChargeStore(
            CombatSide player,
            bool shouldAnnounce
        )
        {
            CapitalShip station = CreateShip("station", 1000, 0);
            station.CanDestroyPlanets = true;
            TacticalCombatSession session = CreateSession(
                CreateShip("target", 1000, 0),
                station,
                NotificationBank(275),
                player
            );
            float announcedCharge = -1;
            session.Audio.Playback = (clip, _) =>
            {
                announcedCharge = session.BattleStation.Charge;
                return null;
            };
            session.BattleStation.TryFire();
            session.BattleStation.Advance(300000);
            Assert.AreEqual(shouldAnnounce ? 0 : -1, announcedCharge);
            Assert.AreEqual(100, session.BattleStation.Charge);
        }

        [TestCase(0f, true)]
        [TestCase(float.NaN, true)]
        [TestCase(1f, false)]
        public void Advance_EnteringHyperspaceTimer_AnnouncesOnlyAtZeroOrUnorderedValue(
            float elapsed,
            bool shouldAnnounce
        )
        {
            TacticalCombatSession session = CreateSession(
                CreateShip("a", 1000, 0),
                CreateShip("b", 1000, 0),
                NotificationBank(31)
            );
            AdvanceSeconds(session, 3);
            TacticalCombatUnit unit = session.GetUnit("a");
            unit.Status = TacticalCombatUnitStatus.EnteringHyperspace;
            unit.HyperspaceMilliseconds = elapsed;
            List<float> observedTimers = new();
            session.Audio.Playback = (clip, _) =>
            {
                observedTimers.Add(unit.HyperspaceMilliseconds);
                return null;
            };
            session.Advance(16);
            Assert.AreEqual(shouldAnnounce ? 1 : 0, observedTimers.Count);
            if (shouldAnnounce)
                Assert.AreEqual(elapsed, observedTimers[0]);
        }

        [Test]
        public void Advance_EnteringHyperspaceFighter_UsesFighterAnimationSound()
        {
            TacticalCombatUnit fighter = new(
                new Starfighter
                {
                    InstanceID = "fighter",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                },
                CombatSide.Attacker,
                0
            );
            TacticalCombatUnit enemy = new(
                CreateShip("enemy", 1000, 0),
                CombatSide.Defender,
                1000000
            );
            TacticalCombatSession session = CreateEncounter(
                fighter,
                enemy,
                NotificationBank(29),
                CombatSide.Attacker
            );
            AdvanceSeconds(session, 3);
            fighter.Status = TacticalCombatUnitStatus.EnteringHyperspace;
            List<string> output = new();
            session.Audio.Playback = (clip, _) =>
            {
                output.Add(clip.Path);
                return null;
            };
            session.Advance(16);
            CollectionAssert.AreEqual(new[] { "29" }, output);
        }

        /// <summary>
        /// Creates explicit shared notification records for observing a public encounter transition.
        /// </summary>
        /// <param name="ids">The notification message IDs to author.</param>
        /// <returns>The addressed media bank.</returns>
        private static TacticalCombatDefinition NotificationBank(params int[] ids)
        {
            TacticalCombatDefinition definition = new();
            foreach (int id in ids)
                definition.Notifications.Add(
                    new()
                    {
                        MessageID = id,
                        Variants = new() { new() { Path = id.ToString() } },
                    }
                );
            return definition;
        }

        /// <summary>
        /// Creates a completed armed or unarmed mobile capital ship.
        /// </summary>
        /// <param name="id">The campaign instance identifier.</param>
        /// <param name="hull">The initial hull strength.</param>
        /// <param name="strength">The forward turbolaser strength.</param>
        /// <returns>The campaign ship.</returns>
        private static CapitalShip CreateShip(string id, int hull, int strength)
        {
            CapitalShip ship = new()
            {
                InstanceID = id,
                MaxHullStrength = hull,
                CurrentHullStrength = hull,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Hyperdrive = 1,
                SublightSpeed = 4,
                Maneuverability = 1,
                WeaponRecharge = strength,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[]
            {
                strength,
                strength,
                strength,
                strength,
                100,
            };
            return ship;
        }

        /// <summary>
        /// Creates two opposing groups that target each other without tactical automation.
        /// </summary>
        /// <param name="attacker">The attacking campaign ship.</param>
        /// <param name="defender">The defending campaign ship.</param>
        /// <param name="definition">The optional authored notification bank.</param>
        /// <param name="player">The player-controlled side.</param>
        /// <returns>The isolated tactical encounter.</returns>
        private static TacticalCombatSession CreateSession(
            CapitalShip attacker,
            CapitalShip defender,
            TacticalCombatDefinition definition = null,
            CombatSide player = CombatSide.Attacker
        )
        {
            TacticalCombatUnit first = new(attacker, CombatSide.Attacker, 0)
            {
                Position = new Vector3(0, 0, -40),
            };
            TacticalCombatUnit second = new(defender, CombatSide.Defender, 1000000)
            {
                Position = new Vector3(0, 0, 40),
                Heading = -Vector3.UnitZ,
                DesiredHeading = -Vector3.UnitZ,
            };
            return CreateEncounter(first, second, definition, player);
        }

        /// <summary>
        /// Creates two opposing groups around captured units and a controlled replay clock.
        /// </summary>
        /// <param name="first">The attacking captured unit.</param>
        /// <param name="second">The defending captured unit.</param>
        /// <param name="definition">The optional authored notification bank.</param>
        /// <param name="player">The player-controlled side.</param>
        /// <returns>The isolated tactical encounter.</returns>
        private static TacticalCombatSession CreateEncounter(
            TacticalCombatUnit first,
            TacticalCombatUnit second,
            TacticalCombatDefinition definition,
            CombatSide player
        )
        {
            TacticalCombatGroup firstGroup = new(1, new[] { first }, Vector3.UnitZ);
            TacticalCombatGroup secondGroup = new(2, new[] { second }, -Vector3.UnitZ);
            firstGroup.SetTarget(second);
            secondGroup.SetTarget(first);
            return new TacticalCombatSession(
                new[] { first, second },
                new[] { firstGroup, secondGroup },
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
                definition,
                presentationClock: () => 0
            );
        }

        /// <summary>
        /// Creates ordered friendly capital and fighter members with an unarmed opposing group.
        /// </summary>
        /// <returns>The encounter used to observe membership command transitions.</returns>
        private static TacticalCombatSession CreateMembershipSession()
        {
            TacticalCombatUnit first = new(CreateShip("first", 100, 0), CombatSide.Attacker, 8);
            TacticalCombatUnit second = new(CreateShip("second", 100, 0), CombatSide.Attacker, 2);
            TacticalCombatUnit fighter = new(
                new Starfighter
                {
                    InstanceID = "fighter",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                },
                CombatSide.Attacker,
                5
            );
            TacticalCombatUnit enemy = new(
                CreateShip("enemy", 100, 0),
                CombatSide.Defender,
                1000001
            );
            return new TacticalCombatSession(
                new[] { first, second, fighter, enemy },
                new[]
                {
                    new TacticalCombatGroup(10, new[] { first }, Vector3.UnitZ),
                    new TacticalCombatGroup(11, new[] { second }, Vector3.UnitZ),
                    new TacticalCombatGroup(12, new[] { fighter }, Vector3.UnitZ),
                    new TacticalCombatGroup(1000010, new[] { enemy }, -Vector3.UnitZ),
                },
                CreateSide(CombatSide.Attacker, 0, Vector3.UnitZ),
                CreateSide(CombatSide.Defender, 1, -Vector3.UnitZ),
                CombatSide.Attacker,
                100,
                new SystemRandomProvider(42)
            );
        }

        /// <summary>
        /// Advances at sixty presentation frames per second for a bounded battle duration.
        /// </summary>
        /// <param name="session">The encounter to advance.</param>
        /// <param name="seconds">The elapsed battle duration.</param>
        private static void AdvanceSeconds(TacticalCombatSession session, int seconds)
        {
            for (int frame = 0; frame < seconds * 60 && !session.IsComplete; frame++)
                session.Advance(1000f / 60);
        }

        /// <summary>
        /// Creates a registered station attack with one surviving fighter squadron.
        /// </summary>
        /// <param name="reserveFighter">Whether to deploy another eligible squadron in a separate group.</param>
        /// <returns>The battle after its attack run has entered the two-minute sequence.</returns>
        private static TacticalCombatSession CreateAttackSession(bool reserveFighter = false)
        {
            TacticalCombatUnit fighter = new(
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
            TacticalCombatUnit station = new(
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
            TacticalCombatGroup group = new(2, new[] { fighter }, Vector3.UnitZ);
            List<TacticalCombatUnit> units = new() { fighter, station };
            List<TacticalCombatGroup> groups = new() { group };
            if (reserveFighter)
            {
                TacticalCombatUnit reserve = new(
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
                groups.Add(new TacticalCombatGroup(3, new[] { reserve }, Vector3.UnitZ));
            }
            TacticalCombatSession session = new(
                units,
                groups,
                CreateSide(CombatSide.Attacker, 0, Vector3.UnitZ),
                CreateSide(CombatSide.Defender, 1, -Vector3.UnitZ),
                CombatSide.Attacker,
                100,
                new SystemRandomProvider(42),
                new TacticalCombatDefinition
                {
                    AttackRunSuccessMoviePath = "Pack/Videos/success",
                    AttackRunFailureMoviePath = "Pack/Videos/failure",
                }
            );
            session.SetOrder(group, TacticalCombatOrder.AttackBattleStation);
            for (int index = 0; index < 100 && session.ActiveAttackRun == null; index++)
                session.Advance(50);
            Assert.IsNotNull(session.ActiveAttackRun);
            session.ActiveAttackRun.ApplyState(TacticalCombatAttackRunState.Starting);
            return session;
        }

        /// <summary>
        /// Captures an unautomated side without command officers or retreat destinations.
        /// </summary>
        /// <param name="side">The deployment side.</param>
        /// <param name="faction">The authored faction index.</param>
        /// <param name="forward">The deployment heading.</param>
        /// <returns>The side's command state.</returns>
        private static TacticalCombatSide CreateSide(CombatSide side, int faction, Vector3 forward)
        {
            return new TacticalCombatSide(
                side,
                faction,
                forward,
                false,
                0,
                0,
                null,
                null,
                0,
                0,
                false
            );
        }
    }
}
