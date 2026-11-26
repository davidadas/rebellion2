using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Rebellion.AI.Planners;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Tactical;
using Rebellion.Util.Random;
using static Rebellion.Tests.Tactical.TacticalCombatAssessmentScenario;
using BitConverter = global::System.BitConverter;

namespace Rebellion.Tests.Tactical
{
    [TestFixture]
    public sealed class TacticalCombatTargetingTests
    {
        [TestCase(1000f)]
        [TestCase(999.9990234375f)]
        public void GetTargetMetric_DistantTarget_StoresTravelArithmeticBeforeAddingPenalty(float x)
        {
            CombatUnit attacker = CreateUnit("metric-attacker", CombatSide.Attacker, 1, 20, false);
            CombatUnit target = CreateUnit(
                "metric-target",
                CombatSide.Defender,
                1000001,
                20,
                false
            );
            target.ApplyHullLoss(99999);
            TacticalCombatGroup group = new(2, new[] { attacker }, Vector3.UnitZ);
            group.Formation.Refresh(0);
            target.Position = group.Formation.Position + new Vector3(x, 1234, 5678);
            float attack = 0;

            float metric = TacticalCombatTargeting.GetTargetMetric(group, target, ref attack);

            Assert.AreEqual(0x4043b8ae, BitConverter.SingleToInt32Bits(metric));
        }

        [Test]
        public void GetTargetMetric_HullAndShields_StoresWeightedHullBeforeAddingShields()
        {
            CombatUnit attacker = CreateUnit("attacker", CombatSide.Attacker, 1, 0, false);
            TacticalCombatGroup group = new(10, new[] { attacker }, Vector3.UnitZ);
            CombatUnit target = new(
                new CapitalShip
                {
                    InstanceID = "target",
                    MaxHullStrength = 7,
                    CurrentHullStrength = 7,
                    MaxShieldStrength = 4,
                },
                CombatSide.Defender,
                1000001
            )
            {
                Position = group.Formation.Position,
            };
            float attack = 1;

            float metric = TacticalCombatTargeting.GetTargetMetric(group, target, ref attack);

            Assert.AreEqual(0x4108cccc, BitConverter.SingleToInt32Bits(metric));
            Assert.AreEqual(1, attack);
        }

        [Test]
        public void GetTargetMetric_RechargeRoundsToAccumulatedAttack_RejectsExhaustedAttack()
        {
            CombatUnit attacker = CreateUnit("attacker", CombatSide.Attacker, 1, 0, false);
            TacticalCombatGroup group = new(10, new[] { attacker }, Vector3.UnitZ);
            CombatUnit target = new(
                new Starfighter
                {
                    InstanceID = "target",
                    MaxSquadronSize = 1,
                    CurrentSquadronSize = 1,
                    Hyperdrive = 1,
                },
                CombatSide.Defender,
                1000001
            );
            target.ApplyHullLoss(5);
            float attack = (float)target.GetShieldRecharge();

            float metric = TacticalCombatTargeting.GetTargetMetric(group, target, ref attack);

            Assert.AreEqual(-1, metric);
        }

        [TestCase(float.NaN, 0f, 0f)]
        [TestCase(float.NaN, float.NaN, float.NaN)]
        public void GetTargetMetric_OnlyZeroOrUnorderedSeparationComponents_OmitsTravelPenalty(
            float x,
            float y,
            float z
        )
        {
            CombatUnit attacker = CreateUnit("attacker", CombatSide.Attacker, 1, 0, false);
            TacticalCombatGroup group = new(10, new[] { attacker }, Vector3.UnitZ);
            CombatUnit target = CreateUnit("target", CombatSide.Defender, 1000001, 0, false);
            target.Position = group.Formation.Position + new Vector3(x, y, z);
            float attack = 1;

            float metric = TacticalCombatTargeting.GetTargetMetric(group, target, ref attack);

            Assert.AreEqual(64999.99609375f, metric);
        }

        [Test]
        public void GetTargetMetric_LargeFiniteSeparation_StoresOverflowedSquaredDistance()
        {
            CombatUnit attacker = CreateUnit("attacker", CombatSide.Attacker, 1, 0, false);
            TacticalCombatGroup group = new(10, new[] { attacker }, Vector3.UnitZ);
            group.Formation.Refresh(0);
            CombatUnit target = CreateUnit("target", CombatSide.Defender, 1000001, 0, false);
            target.Position = group.Formation.Position + Vector3.UnitX * float.MaxValue;
            float attack = 1;

            float metric = TacticalCombatTargeting.GetTargetMetric(group, target, ref attack);

            Assert.AreEqual(float.PositiveInfinity, metric);
        }

        [Test]
        public void PrepareCandidates_AutomatedSide_DoesNotIssuePlanningCommands()
        {
            TacticalCombatSession session = CreateSession(20, 20, false, 1);
            CombatUnit unit = session.GetUnit("defender-0");
            TacticalCombatGroup group = session.GetGroup(unit);
            TacticalCombatTargeting assessment = new(session);

            assessment.PrepareCandidates(session.GetSide(CombatSide.Defender));

            Assert.IsEmpty(group.RequestedTargets);
        }

        [Test]
        public void AssessStrength_DepartingCapital_RetainsItsDisplayedAttackBudget()
        {
            CombatUnit unit = CreateUnit("departing", CombatSide.Defender, 1000001, 20, false);
            unit.Status = CombatUnitStatus.EnteringHyperspace;
            TacticalCombatSession session = CreateAssessmentSession(new[] { unit });
            TacticalCombatTargeting assessment = new(session);

            assessment.AssessStrength(session.GetSide(CombatSide.Defender));

            Assert.AreEqual(20, session.GetSide(CombatSide.Defender).AttackBudget);
        }

        [Test]
        public void AssessStrength_RecoveryCompletedFighter_ExcludesItsAttackBudget()
        {
            CombatUnit unit = new(
                new Starfighter
                {
                    InstanceID = "recovered",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                    LaserCannon = 5,
                    Hyperdrive = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                CombatSide.Attacker,
                1
            );
            unit.Status = CombatUnitStatus.Recovered;
            TacticalCombatSession session = CreateAssessmentSession(new[] { unit });
            TacticalCombatTargeting assessment = new(session);

            assessment.AssessStrength(session.GetSide(CombatSide.Attacker));

            Assert.AreEqual(0, session.GetSide(CombatSide.Attacker).AttackBudget);
        }

        [Test]
        public void AssessStrength_GlobalInitialHangars_AddsOpposingDockedFighterOnlyToEmpire()
        {
            TacticalCombatSession session = CreateHangarSession(out CombatUnit fighter);
            TacticalCombatTargeting assessment = new(session);

            assessment.AssessStrength(session.GetSide(CombatSide.Attacker));
            assessment.AssessStrength(session.GetSide(CombatSide.Defender));

            Assert.AreEqual(0, session.GetSide(CombatSide.Attacker).AttackBudget);
            Assert.That(session.GetSide(CombatSide.Defender).AttackBudget, Is.GreaterThan(0));
            Assert.AreEqual(CombatUnitStatus.Docked, fighter.Status);
        }

        [Test]
        public void AssessStrength_InitialFighterLaunch_RemovesItFromGlobalHangarAssessment()
        {
            TacticalCombatSession session = CreateHangarSession(out CombatUnit fighter);
            TacticalCombatTargeting assessment = new(session);
            for (int frame = 0; frame < 600 && fighter.Status == CombatUnitStatus.Docked; frame++)
                session.Advance(50);
            Assert.AreEqual(CombatUnitStatus.Active, fighter.Status);

            assessment.AssessStrength(session.GetSide(CombatSide.Defender));

            Assert.AreEqual(0, session.GetSide(CombatSide.Defender).AttackBudget);
        }

        [Test]
        public void AssessStrength_AppliedPrimaryTargetWithNoShieldRecharge_SuppressesCapital()
        {
            CombatUnit own = CreateUnit("own", CombatSide.Defender, 1000001, 20, false);
            CombatUnit opposing = CreateUnit("opposing", CombatSide.Attacker, 1, 20, false);
            TacticalCombatSession session = CreateAssessmentSession(new[] { own, opposing });
            session.GetGroup(opposing).SetTarget(own);
            TacticalCombatTargeting assessment = new(session);

            assessment.AssessStrength(session.GetSide(CombatSide.Defender));

            Assert.AreEqual(0, session.GetSide(CombatSide.Defender).AttackBudget);
        }

        [Test]
        public void AssessStrength_RequestedTargetBeforeApplication_DoesNotSuppressCapital()
        {
            CombatUnit own = CreateUnit("own", CombatSide.Defender, 1000001, 20, false);
            CombatUnit opposing = CreateUnit("opposing", CombatSide.Attacker, 1, 20, false);
            TacticalCombatSession session = CreateAssessmentSession(new[] { own, opposing });
            session.SetTarget(session.GetGroup(opposing), own);
            TacticalCombatTargeting assessment = new(session);

            assessment.AssessStrength(session.GetSide(CombatSide.Defender));

            Assert.AreEqual(20, session.GetSide(CombatSide.Defender).AttackBudget);
        }

        [Test]
        public void AssessStrength_OperationalEmpireStation_AddsItsFixedAttackBudget()
        {
            CombatUnit station = new(
                new CapitalShip
                {
                    InstanceID = "station",
                    CanDestroyPlanets = true,
                    MaxHullStrength = 1000,
                    CurrentHullStrength = 1000,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                CombatSide.Defender,
                1000001
            );
            TacticalCombatSession session = CreateAssessmentSession(new[] { station });
            TacticalCombatTargeting assessment = new(session);

            assessment.AssessStrength(session.GetSide(CombatSide.Defender));

            Assert.AreEqual(20000000, session.GetSide(CombatSide.Defender).AttackBudget);
        }
    }
}
