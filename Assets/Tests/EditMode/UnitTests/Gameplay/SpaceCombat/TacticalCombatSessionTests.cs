using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
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

        /// <summary>
        /// Creates an armed or unarmed mobile capital ship.
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
        /// <returns>The isolated tactical encounter.</returns>
        private static TacticalCombatSession CreateSession(
            CapitalShip attacker,
            CapitalShip defender
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
    }
}
