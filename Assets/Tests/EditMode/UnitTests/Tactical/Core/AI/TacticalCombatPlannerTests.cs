using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Rebellion.AI.Planners;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Tactical;
using Rebellion.Tactical.AI;
using Rebellion.Tactical.Integration.Campaign;
using Rebellion.Tactical.Runtime.Audio;
using Rebellion.Util.Random;
using static Rebellion.Tests.Tactical.TacticalCombatAssessmentScenario;
using BitConverter = global::System.BitConverter;

namespace Rebellion.Tests.Tactical.AI
{
    [TestFixture]
    public sealed class TacticalCombatPlannerTests
    {
        [Test]
        public void Plan_OperationalStationWithUnorderedCharge_QueuesSuperlaser()
        {
            TacticalCombatUnit target = CreateUnit("target", CombatSide.Attacker, 1, 20, false);
            TacticalCombatUnit station = new(
                TacticalCombatUnitCapture.Capture(
                    new CapitalShip
                    {
                        InstanceID = "station",
                        CanDestroyPlanets = true,
                        MaxHullStrength = 100000,
                        CurrentHullStrength = 100000,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    }
                ),
                CombatSide.Defender,
                1000001
            );
            TacticalCombatSession session = CreateAssessmentSession(new[] { target, station });
            session.BattleStation.Advance(float.NaN);
            TacticalCombatTargeting targeting = new(session);
            TacticalCombatPlanner automation = new(session, targeting);

            AdvanceAssessment(automation, targeting, session.GetSide(CombatSide.Defender));

            Assert.AreEqual(0, session.BattleStation.Charge);
        }

        [Test]
        public void Plan_AutomatedSide_AssignsOpposingCapitalTarget()
        {
            TacticalCombatSession session = CreateSession(20, 20, false, 1);
            Advance(session);
            TacticalCombatUnit defender = session.GetUnit("defender-0");
            Assert.AreSame(session.GetUnit("attacker"), session.GetGroup(defender).PrimaryTarget);
        }

        [Test]
        public void Plan_PlayerSide_LeavesDesignatedTargetListUnchanged()
        {
            TacticalCombatSession session = CreateSession(20, 20, false, 1);
            Advance(session);
            Assert.IsEmpty(session.GetGroup(session.GetUnit("attacker")).RequestedTargets);
        }

        [Test]
        public void Plan_UnarmedAutomatedSide_StartsAvailableWithdrawal()
        {
            TacticalCombatSession session = CreateSession(20, 0, false, 1);
            Advance(session);
            Assert.IsTrue(session.GetSide(CombatSide.Defender).IsWithdrawing);
        }

        [Test]
        public void Plan_OpposingGravityWell_PreventsAutomatedWithdrawal()
        {
            TacticalCombatSession session = CreateSession(20, 0, true, 1);
            Advance(session);
            Assert.IsFalse(session.GetSide(CombatSide.Defender).IsWithdrawing);
        }

        [Test]
        public void Plan_FourEquivalentCapitalShips_CreatesTwoBalancedGroups()
        {
            TacticalCombatSession session = CreateSession(20, 20, false, 4);
            Advance(session);
            TacticalCombatGroup[] groups = session
                .Groups.Where(group => group.Side == CombatSide.Defender)
                .ToArray();
            Assert.AreEqual(2, groups.Length);
            Assert.IsTrue(groups.All(group => group.Members.Count == 2));
        }

        [Test]
        public void Plan_LargeCapitalPriority_OrdersAppendedFighterByStoredScoreTie()
        {
            TacticalCombatUnit own = CreateUnit("own", CombatSide.Defender, 1000001, 20, false);
            TacticalCombatUnit capital = CreateUnit(
                "priority-capital",
                CombatSide.Attacker,
                2,
                100000000,
                false
            );
            TacticalCombatUnit fighter = new(
                TacticalCombatUnitCapture.Capture(
                    new Starfighter
                    {
                        InstanceID = "priority-fighter",
                        MaxSquadronSize = 12,
                        CurrentSquadronSize = 12,
                        Hyperdrive = 1,
                        SublightSpeed = 9,
                        Agility = 5,
                        LaserCannon = 5,
                        LaserRange = 12,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    }
                ),
                CombatSide.Attacker,
                1
            );
            TacticalCombatSession session = CreateAssessmentSession(
                new[] { own, capital, fighter }
            );
            TacticalCombatGroup group = session.GetGroup(own);
            TacticalCombatTargeting targeting = new(session);
            TacticalCombatPlanner automation = new(session, targeting);

            AdvanceAssessment(automation, targeting, session.GetSide(CombatSide.Defender));

            Assert.AreSame(fighter, group.RequestedTargets.Single());
        }

        [Test]
        public void Plan_EngineDamageAfterEntry_PreservesCapturedCapitalPartitionOrder()
        {
            TacticalCombatSession session = CreateSession(20, 20, false, 4);
            TacticalCombatUnit damaged = session.GetUnit("defender-0");
            for (int damage = 0; damage < 4; damage++)
                damaged.Systems.ApplyComponentEvent(TacticalShipSystem.Engines, false);

            Advance(session);

            CollectionAssert.AreEqual(
                new[] { "defender-0", "defender-1" },
                session.GetGroup(damaged).Members.Select(unit => unit.InstanceID)
            );
        }

        [Test]
        public void Plan_SixUpdateCadence_ReassignsOnlyOnNextSixthUpdate()
        {
            TacticalCombatUnit own = CreateUnit("own", CombatSide.Defender, 1000001, 20, false);
            TacticalCombatUnit preferred = CreateUnit(
                "preferred",
                CombatSide.Attacker,
                1,
                40,
                false
            );
            TacticalCombatUnit secondary = CreateUnit(
                "secondary",
                CombatSide.Attacker,
                2,
                20,
                false
            );
            TacticalCombatSession session = CreateAssessmentSession(
                new[] { own, preferred, secondary }
            );
            TacticalCombatGroup group = session.GetGroup(own);
            TacticalCombatTargeting targeting = new(session);
            TacticalCombatPlanner automation = new(session, targeting);
            AdvanceAssessment(automation, targeting, session.GetSide(CombatSide.Defender));
            session.SetTarget(group, secondary);

            for (int update = 0; update < 5; update++)
                AdvanceAssessment(automation, targeting, session.GetSide(CombatSide.Defender));
            Assert.AreSame(secondary, group.RequestedTargets.Single());
            AdvanceAssessment(automation, targeting, session.GetSide(CombatSide.Defender));

            Assert.AreSame(preferred, group.RequestedTargets.Single());
        }

        [Test]
        public void Plan_PlayerAutomation_PublishesRequestedCommandGroupCategory()
        {
            TacticalCombatUnit player = CreateUnit("player", CombatSide.Attacker, 1, 20, false);
            TacticalCombatUnit opposing = CreateUnit(
                "opposing",
                CombatSide.Defender,
                1000001,
                20,
                false
            );
            TacticalCombatSession session = CreateAssessmentSession(new[] { player, opposing });
            session.SetAutomated(CombatSide.Attacker, true);
            TacticalCombatGroup previous = session.GetGroup(player);
            TacticalCombatTargeting targeting = new(session);
            TacticalCombatPlanner automation = new(session, targeting);

            AdvanceAssessment(automation, targeting, session.GetSide(CombatSide.Attacker));

            TacticalCombatGroup requested = session.GetRequestedGroup(player);
            Assert.AreNotSame(previous, requested);
            Assert.AreSame(requested, session.GetObjectGroup(CombatSide.Attacker, 0));
            Assert.AreSame(previous, session.GetGroup(player));
        }

        [Test]
        public void Plan_PartiallyDamagedUnarmedFighters_PrioritizesRetainedRechargeRatio()
        {
            TacticalCombatUnit own = CreateUnit("own", CombatSide.Defender, 1000001, 20, false);
            TacticalCombatUnit earlier = CreateUnarmedFighter("earlier", 1, 3.8450623f);
            TacticalCombatUnit preferred = CreateUnarmedFighter("preferred", 2, 3.8450603f);
            TacticalCombatSession session = CreateAssessmentSession(
                new[] { own, earlier, preferred }
            );
            TacticalCombatGroup group = session.GetGroup(own);
            TacticalCombatTargeting targeting = new(session);
            TacticalCombatPlanner automation = new(session, targeting);

            AdvanceAssessment(automation, targeting, session.GetSide(CombatSide.Defender));

            Assert.AreSame(preferred, group.RequestedTargets.Single());
        }
    }
}
