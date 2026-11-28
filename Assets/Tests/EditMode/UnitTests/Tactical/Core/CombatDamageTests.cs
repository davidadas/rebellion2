using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;
using Rebellion.Tactical;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Tactical
{
    [TestFixture]
    public sealed class CombatDamageTests
    {
        private TacticalCombatDamage _damage;

        /// <summary>
        /// Creates a fresh damage processor for each simulation case.
        /// </summary>
        [SetUp]
        public void SetUp() => _damage = new TacticalCombatDamage();

        [TestCase(HardpointWeaponType.LaserCannon, 0f)]
        [TestCase(HardpointWeaponType.LaserCannon, -5f)]
        [TestCase(HardpointWeaponType.LaserCannon, float.NaN)]
        [TestCase(HardpointWeaponType.IonCannon, 0f)]
        [TestCase(HardpointWeaponType.IonCannon, -5f)]
        [TestCase(HardpointWeaponType.IonCannon, float.NaN)]
        [TestCase(HardpointWeaponType.Turbolaser, 0f)]
        [TestCase(HardpointWeaponType.Turbolaser, -5f)]
        [TestCase(HardpointWeaponType.Turbolaser, float.NaN)]
        [TestCase(HardpointWeaponType.TorpedoLauncher, 0f)]
        [TestCase(HardpointWeaponType.TorpedoLauncher, -5f)]
        [TestCase(HardpointWeaponType.TorpedoLauncher, float.NaN)]
        public void ApplyHit_NonpositiveEntryHull_ClampsHullWithoutImpactSideEffects(
            HardpointWeaponType weapon,
            float entryHull
        )
        {
            CombatUnit systems = CreateUnitWithPools(
                false,
                100,
                entryHull,
                37,
                0,
                0,
                0,
                0,
                0,
                1,
                0
            );
            SystemRandomProvider random = new(42);
            List<string> events = new();
            _damage.LethalHit += (_, _) => events.Add("report");
            _damage.ImpactApplied += (_, _, _) => events.Add("impact");
            _damage.SystemChangeRequested += (_, _) => events.Add("component");

            TacticalCombatImpactKind impact = _damage.ApplyHit(
                systems,
                weapon,
                100,
                1,
                1,
                random,
                "attacker",
                playSound: _ => events.Add("sound")
            );

            Assert.AreEqual(TacticalCombatImpactKind.None, impact);
            Assert.AreEqual(0, systems.Hull);
            Assert.AreEqual(37, systems.Shields);
            Assert.AreEqual(0, random.CallCount);
            Assert.IsEmpty(events);
        }

        [Test]
        public void Destroy_ChargedUnit_ClearsHullAndRetainsShieldPool()
        {
            CombatUnit systems = CreateSystems();
            float shields = systems.Shields;

            systems.Destroy();

            Assert.AreEqual(0, systems.Hull);
            Assert.AreEqual(shields, systems.Shields);
        }

        [Test]
        public void ApplyHit_ConventionalDamage_ConsumesShieldsBeforeHull()
        {
            CombatUnit systems = CreateSystems();
            _damage.ApplyHit(
                systems,
                HardpointWeaponType.Turbolaser,
                125,
                1,
                1,
                new SequenceRandom(0),
                null
            );

            Assert.AreEqual(0, systems.Shields);
            Assert.AreEqual(975, systems.Hull);
        }

        [Test]
        public void ApplyHit_IonDamagePastShields_DoesNotDamageHull()
        {
            CombatUnit systems = CreateSystems();
            _damage.ApplyHit(
                systems,
                HardpointWeaponType.IonCannon,
                101,
                1,
                1,
                new SequenceRandom(3),
                null
            );

            Assert.AreEqual(1000, systems.Hull);
            Assert.AreEqual(0, systems.Shields);
        }

        [TestCase(150f, -50f)]
        [TestCase(float.NaN, float.NaN)]
        public void ApplyHit_LethalConventionalImpact_ReportsAttackerBeforeHullClampAndExplosion(
            float damage,
            float reportedHull
        )
        {
            CombatUnit systems = CreateUnitWithPools(false, 100, 100, 0, 0, 0, 0, 0, 0, 1, 0);
            List<string> events = new();
            _damage.LethalHit += (_, attackerID) =>
            {
                Assert.AreEqual("attacker", attackerID);
                Assert.AreEqual(reportedHull, systems.Hull);
                events.Add("report");
            };
            _damage.ImpactApplied += (_, weapon, impact) =>
            {
                Assert.AreEqual(HardpointWeaponType.Turbolaser, weapon);
                Assert.AreEqual(TacticalCombatImpactKind.Destroyed, impact);
                Assert.AreEqual(0, systems.Hull);
                events.Add("effect");
            };

            TacticalCombatImpactKind result = _damage.ApplyHit(
                systems,
                HardpointWeaponType.Turbolaser,
                damage,
                1,
                1,
                new SequenceRandom(),
                "attacker",
                playSound: sound => events.Add(sound.ToString())
            );

            Assert.AreEqual(TacticalCombatImpactKind.Destroyed, result);
            Assert.AreEqual(0, systems.Hull);
            CollectionAssert.AreEqual(
                new[] { "HeavyHullHit", "report", "effect", "SmallExplosion" },
                events
            );
        }

        [Test]
        public void ApplyHit_IonImpact_DoesNotReportDestruction()
        {
            CombatUnit systems = CreateUnitWithPools(true, 100, 100, 0, 0, 0, 0, 0, 0, 1, 0);
            List<string> reports = new();
            _damage.LethalHit += (_, attackerID) => reports.Add(attackerID);

            _damage.ApplyHit(
                systems,
                HardpointWeaponType.IonCannon,
                150,
                1,
                1,
                new SequenceRandom(),
                "attacker"
            );

            Assert.IsEmpty(reports);
            Assert.AreEqual(100, systems.Hull);
        }

        [TestCase(1, 0, 35, 0)]
        [TestCase(2, 0, 0, 35)]
        [TestCase(3, 1, 0, 0)]
        [TestCase(4, 1, 0, 0)]
        [TestCase(5, 2, 0, 0)]
        [TestCase(6, 2, 0, 0)]
        [TestCase(7, 4, 0, 0)]
        [TestCase(8, 4, 0, 0)]
        [TestCase(9, 8, 0, 0)]
        [TestCase(10, 8, 0, 0)]
        public void ApplyHit_IonComponentSelection_RequestsSelectedTimersAndBatteries(
            int selector,
            int expectedMask,
            float expectedAttackDelay,
            float expectedMovementDelay
        )
        {
            CombatUnit systems = CreateSystems();
            List<TacticalSystemEvent> events = new();
            _damage.SystemChangeRequested += (_, change) => events.Add(change);
            SequenceRandom random =
                selector <= 2 ? new SequenceRandom(selector, 35) : new SequenceRandom(selector);

            _damage.ApplyHit(systems, HardpointWeaponType.IonCannon, 101, 1, 1, random, null);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(TacticalSystemEventKind.Disruption, events[0].Kind);
            Assert.AreEqual(expectedMask, events[0].DischargedArcMask);
            Assert.AreEqual(expectedAttackDelay, events[0].AttackDelay);
            Assert.AreEqual(expectedMovementDelay, events[0].MovementDelay);
            CollectionAssert.AreEqual(
                selector <= 2 ? new[] { (1, 11), (30, 51) } : new[] { (1, 11) },
                random.Requests
            );
        }

        [Test]
        public void ApplyHit_MixedIonComponentSelections_PreservesDrawOrderAndEverySelectedBattery()
        {
            CombatUnit systems = CreateSystems();
            List<TacticalSystemEvent> events = new();
            _damage.SystemChangeRequested += (_, change) => events.Add(change);
            SequenceRandom random = new(1, 30, 2, 50, 3, 5, 7, 10);

            _damage.ApplyHit(systems, HardpointWeaponType.IonCannon, 106, 1, 1, random, null);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(15, events[0].DischargedArcMask);
            Assert.AreEqual(30, events[0].AttackDelay);
            Assert.AreEqual(50, events[0].MovementDelay);
            CollectionAssert.AreEqual(
                new[] { (1, 11), (30, 51), (1, 11), (30, 51), (1, 11), (1, 11), (1, 11), (1, 11) },
                random.Requests
            );
        }

        [Test]
        public void ApplyHit_UnorderedIonExcess_RequestsEmptyDisruptionWithoutComponentDraws()
        {
            CombatUnit systems = CreateSystems();
            List<TacticalSystemEvent> events = new();
            _damage.SystemChangeRequested += (_, change) => events.Add(change);
            SequenceRandom random = new();

            _damage.ApplyHit(systems, HardpointWeaponType.IonCannon, float.NaN, 1, 1, random, null);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(0, events[0].DischargedArcMask);
            Assert.AreEqual(0, events[0].AttackDelay);
            Assert.AreEqual(0, events[0].MovementDelay);
            Assert.IsEmpty(random.Requests);
        }

        [Test]
        public void ApplyHit_RepeatedIonTimerSelections_AccumulatesEverySelectedDuration()
        {
            CombatUnit systems = CreateSystems();
            List<TacticalSystemEvent> events = new();
            _damage.SystemChangeRequested += (_, change) => events.Add(change);

            _damage.ApplyHit(
                systems,
                HardpointWeaponType.IonCannon,
                104,
                1,
                1,
                new SequenceRandom(1, 30, 1, 50, 2, 31, 2, 49),
                null
            );

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(80, events[0].AttackDelay);
            Assert.AreEqual(80, events[0].MovementDelay);
            Assert.AreEqual(0, events[0].DischargedArcMask);
        }

        [Test]
        public void GetWeaponRecharge_PartialFighterHull_RetainsLiveBudgetBeforeSampleStore()
        {
            CombatUnit systems = CreateUnitWithPools(true, 96, 73, 0, 0, 0, 0, 0, 0, 1, 0);

            double recharge = TacticalCombatFireControl.GetWeaponRecharge(systems);

            Assert.AreEqual(0x40411d2afc800000L, BitConverter.DoubleToInt64Bits(recharge));
        }

        [Test]
        public void GetWeaponRecharge_HullAndWeaponDamage_ReducesBaseRecharge()
        {
            CombatUnit systems = CreateSystems(currentHull: 500);
            TacticalCombatDamage.ApplyComponentEvent(systems, TacticalShipSystem.Weapons, false);

            Assert.AreEqual(18.75f, TacticalCombatFireControl.GetWeaponRecharge(systems));
        }

        [Test]
        public void HullFraction_PartialHull_RetainsDivisionUntilConsumerStore()
        {
            CombatUnit systems = CreateSystems(currentHull: 631);

            double fraction = systems.HullFraction;

            Assert.AreEqual(0x3fe43126e978d4feL, BitConverter.DoubleToInt64Bits(fraction));
        }

        [Test]
        public void GetWeaponRechargeRatio_FighterSample_UsesUpdatedStoredRateWithoutRoundingNumerator()
        {
            CombatUnit systems = CreateUnitWithPools(true, 96, 96, 0, 0, 0, 0, 0, 0, 1, 0);

            double ratio = TacticalCombatFireControl.GetWeaponRechargeRatio(systems);

            Assert.AreEqual(0x3fefffffe9402186L, BitConverter.DoubleToInt64Bits(ratio));
        }

        [Test]
        public void GetWeaponRechargeRatio_FighterLosesHalfHull_UsesUpdatedRechargeSample()
        {
            CombatUnit systems = CreateUnitWithPools(true, 96, 96, 0, 0, 0, 0, 0, 0, 1, 0);
            TacticalCombatFireControl.GetWeaponRecharge(systems);
            systems.ApplyHullLoss(48);

            double ratio = TacticalCombatFireControl.GetWeaponRechargeRatio(systems);

            Assert.AreEqual(0x3fefffffe9402186L, BitConverter.DoubleToInt64Bits(ratio));
        }

        [Test]
        public void GetWeaponRechargeRatio_DestroyedFighter_UpdatesSampleBeforeDivisionThenStopsAtZero()
        {
            CombatUnit systems = CreateUnitWithPools(true, 96, 96, 0, 0, 0, 0, 0, 0, 1, 0);
            TacticalCombatFireControl.GetWeaponRecharge(systems);
            systems.ApplyHullLoss(96);

            double first = TacticalCombatFireControl.GetWeaponRechargeRatio(systems);
            double second = TacticalCombatFireControl.GetWeaponRechargeRatio(systems);

            Assert.IsTrue(double.IsNaN(first));
            Assert.AreEqual(0, second);
        }

        [Test]
        public void GetWeaponRecharge_FighterZeroHull_StoresZeroForSubsequentRatioAdmission()
        {
            CombatUnit systems = CreateUnitWithPools(true, 96, 96, 0, 0, 0, 0, 0, 0, 1, 0);
            systems.Destroy();

            Assert.AreEqual(0, TacticalCombatFireControl.GetWeaponRecharge(systems));
            Assert.AreEqual(0, TacticalCombatFireControl.GetWeaponRechargeRatio(systems));
        }

        [Test]
        public void GetTractorRatio_HullAndSystemDamage_UsesCompleteTractorBudget()
        {
            CombatUnit systems = CreateUnitWithPools(false, 1000, 500, 0, 0, 0, 0, 0, 8, 1, 0);
            TacticalCombatDamage.ApplyComponentEvent(
                systems,
                TacticalShipSystem.TractorBeam,
                false
            );

            Assert.AreEqual(0.25d, TacticalCombatMotion.GetTractorRatio(systems));
        }

        [Test]
        public void GetMovementRatio_LiveSupportDamageAndRestraint_UsesStoredEngineDenominator()
        {
            CombatUnit systems = CreateUnitWithPools(true, 96, 96, 0, 0, 0, 20, 0, 0, 1, 0);
            TacticalCombatDamage.ApplyComponentEvent(systems, TacticalShipSystem.Engines, false);

            Assert.AreEqual(1d, TacticalCombatMotion.GetMovementRatio(systems, 8, 4));
            systems.ApplyDisruption(0, 1);
            Assert.AreEqual(0d, TacticalCombatMotion.GetMovementRatio(systems, 8, 4));
        }

        [Test]
        public void GetMovementRatio_ZeroStoredEngines_DoesNotDivideLiveCommandSupport()
        {
            CombatUnit systems = CreateUnitWithPools(false, 100, 100, 0, 0, 0, 0, 0, 0, 1, 0);

            Assert.AreEqual(0d, TacticalCombatMotion.GetMovementRatio(systems, 9, 0));
            Assert.AreEqual(9d, TacticalCombatMotion.GetMovementBudget(systems, 9, 0));
        }

        [TestCase(false, 5f)]
        [TestCase(true, 6f)]
        public void GetTurnBudget_PositiveMovementBelowStoredPrecision_RetainsManeuverability(
            bool isFighter,
            float expected
        )
        {
            CombatUnit systems = CreateUnitWithPools(isFighter, 100, 100, 0, 0, 0, 0, 5, 0, 1, 0);
            for (int index = 0; index < 3; index++)
                TacticalCombatDamage.ApplyComponentEvent(
                    systems,
                    TacticalShipSystem.Engines,
                    false
                );

            float turnBudget = TacticalCombatMotion.GetTurnBudget(systems, float.Epsilon, 0);

            Assert.AreEqual(0, TacticalCombatMotion.GetMovementBudget(systems, float.Epsilon, 0));
            Assert.AreEqual(expected, turnBudget);
        }

        [Test]
        public void GetHyperdriveRatio_DamagedPrimaryAndBackup_TracksRemainingInstalledDriveCount()
        {
            CombatUnit systems = CreateUnitWithPools(false, 100, 100, 0, 0, 0, 0, 0, 0, 2, 0);
            TacticalCombatDamage.ApplyComponentEvent(systems, TacticalShipSystem.Hyperdrive, false);

            Assert.AreEqual(0.5d, systems.GetHyperdriveRatio());
            TacticalCombatDamage.ApplyComponentEvent(systems, TacticalShipSystem.Hyperdrive, false);
            Assert.AreEqual(0d, systems.GetHyperdriveRatio());
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        public void GetShieldFraction_ZeroOrUnorderedLimit_ReturnsZero(float limit)
        {
            CombatUnit systems = CreateUnitWithPools(false, 100, 100, limit, 0, 0, 0, 0, 0, 1, 0);

            Assert.AreEqual(0d, systems.GetShieldFraction());
        }

        [Test]
        public void GetShieldFraction_OverLimitPool_RetainsUnclampedRatio()
        {
            CombatUnit systems = CreateUnitWithPools(false, 100, 100, 100, 0, 0, 0, 0, 0, 1, 0);
            systems.RestorePools(100, 125);

            Assert.AreEqual(1.25d, systems.GetShieldFraction());
        }

        [Test]
        public void GetShieldRecharge_FighterHull_ReturnsLiveBudgetBeforeStoredSampleRounding()
        {
            CombatUnit systems = CreateUnitWithPools(true, 96, 96, 0, 0, 0, 0, 0, 0, 1, 0);

            double recharge = TacticalCombatDamage.GetShieldRecharge(systems);

            Assert.AreEqual(0.30000000447034836d, recharge);
            Assert.AreEqual(
                0x3feffffff2aaaab4L,
                BitConverter.DoubleToInt64Bits(TacticalCombatDamage.GetShieldRechargeRatio(systems))
            );
        }

        [Test]
        public void GetShieldRechargeRatio_DestroyedFighter_UpdatesSampleBeforeDivisionThenStopsAtZero()
        {
            CombatUnit systems = CreateUnitWithPools(true, 96, 96, 0, 0, 0, 0, 0, 0, 1, 0);
            systems.ApplyHullLoss(96);

            double first = TacticalCombatDamage.GetShieldRechargeRatio(systems);
            double second = TacticalCombatDamage.GetShieldRechargeRatio(systems);

            Assert.IsTrue(double.IsNaN(first));
            Assert.AreEqual(0, second);
        }

        [Test]
        public void GetShieldRechargeRatio_ZeroSampleFromRechargeRead_ReturnsZeroWithoutAnotherDivision()
        {
            CombatUnit systems = CreateUnitWithPools(true, 96, 96, 0, 0, 0, 0, 0, 0, 1, 0);
            systems.ApplyHullLoss(96);
            Assert.AreEqual(0, TacticalCombatDamage.GetShieldRecharge(systems));

            double ratio = TacticalCombatDamage.GetShieldRechargeRatio(systems);

            Assert.AreEqual(0, ratio);
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        public void GetShieldRechargeRatio_UnavailableCapitalRate_ReturnsZero(float rechargeRate)
        {
            CombatUnit systems = CreateUnitWithPools(
                false,
                1000,
                1000,
                0,
                0,
                rechargeRate,
                0,
                0,
                0,
                1,
                0
            );

            double ratio = TacticalCombatDamage.GetShieldRechargeRatio(systems);

            Assert.AreEqual(0, ratio);
        }

        [Test]
        public void GetShieldRechargeRatio_CapitalShieldRepair_PreservesAuthoredRateAsDenominator()
        {
            CombatUnit systems = CreateUnitWithPools(
                false,
                1000,
                1000,
                0,
                0,
                8,
                0,
                0,
                0,
                1,
                0,
                new CapitalShipSystemDamage { Shields = 4 }
            );
            Assert.AreEqual(0, TacticalCombatDamage.GetShieldRechargeRatio(systems));

            TacticalCombatDamage.ApplyComponentEvent(systems, TacticalShipSystem.Shields, true);

            Assert.AreEqual(0.25d, TacticalCombatDamage.GetShieldRechargeRatio(systems));
            Assert.AreEqual(2d, TacticalCombatDamage.GetShieldRecharge(systems));
        }

        [Test]
        public void ScaleByHull_PartialHull_MultipliesBeforeDividing()
        {
            CombatUnit systems = CreateSystems(currentHull: 631);

            float scaled = systems.ScaleByHull(BitConverter.Int32BitsToSingle(0x42122223));

            Assert.AreEqual(0x41b86b98, BitConverter.SingleToInt32Bits(scaled));
        }

        [TestCase(8, 88)]
        [TestCase(0, 96)]
        [TestCase(-8, 104)]
        public void ApplyHullLoss_LivingUnit_ClearsShieldsAndAppliesLoss(
            float loss,
            float expectedHull
        )
        {
            CombatUnit systems = CreateUnitWithPools(false, 96, 96, 12, 0, 0, 0, 0, 0, 0, 0);

            systems.ApplyHullLoss(loss);

            Assert.AreEqual(expectedHull, systems.Hull);
            Assert.AreEqual(0, systems.Shields);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ApplyHullLoss_NonpositiveEntry_RemainsDeadWithoutChangingShields(
            float entryHull
        )
        {
            CombatUnit systems = CreateUnitWithPools(false, 96, entryHull, 12, 0, 0, 0, 0, 0, 0, 0);

            systems.ApplyHullLoss(-8);

            Assert.AreEqual(0, systems.Hull);
            Assert.AreEqual(12, systems.Shields);
        }

        [Test]
        public void ApplyHullLoss_FatalLoss_ClearsHullAndShields()
        {
            CombatUnit systems = CreateUnitWithPools(false, 96, 96, 12, 0, 0, 0, 0, 0, 0, 0);

            systems.ApplyHullLoss(1000);

            Assert.AreEqual(0, systems.Hull);
            Assert.AreEqual(0, systems.Shields);
        }

        [Test]
        public void ApplyComponentEvent_FourEngineHits_DisablesMovement()
        {
            CombatUnit systems = CreateSystems();
            for (int hit = 0; hit < 4; hit++)
                TacticalCombatDamage.ApplyComponentEvent(
                    systems,
                    TacticalShipSystem.Engines,
                    false
                );

            Assert.AreEqual(0, TacticalCombatMotion.GetMovementBudget(systems, 1, 0));
        }

        [Test]
        public void ApplyComponentEvent_LastHyperdriveHit_PreventsHyperspace()
        {
            CombatUnit systems = CreateSystems();
            TacticalCombatDamage.ApplyComponentEvent(systems, TacticalShipSystem.Hyperdrive, false);

            Assert.IsFalse(systems.HasHyperdrive);
        }

        [Test]
        public void Advance_TemporaryDisruption_RecoversAfterElapsedTime()
        {
            CombatUnit systems = CreateSystems();
            systems.ApplyDisruption(30, 50);
            _damage.AdvanceSystems(systems, 1000, new SequenceRandom());

            Assert.AreEqual(29, systems.AttackDelay);
            Assert.AreEqual(49, systems.MovementDelay);
        }

        [Test]
        public void ApplyDisruption_ExplicitEvent_UsesDefinedTimerIncrements()
        {
            CombatUnit systems = CreateSystems();
            TacticalSystemEvent change = new(11, 17, 4);

            systems.ApplyDisruption(change.AttackDelay.Value, change.MovementDelay.Value);

            Assert.AreEqual(11, systems.AttackDelay);
            Assert.AreEqual(17, systems.MovementDelay);
        }

        [Test]
        public void Advance_RepeatedDisruptionDecay_UsesStoredSinglePrecisionElapsedProduct()
        {
            CombatUnit systems = CreateSystems();
            systems.ApplyDisruption(30, 30);

            for (int index = 0; index < 29; index++)
                _damage.AdvanceSystems(systems, 1000, new SequenceRandom());

            Assert.AreEqual(1, systems.AttackDelay);
            Assert.AreEqual(1, systems.MovementDelay);
        }

        [Test]
        public void Advance_DisruptionSecondsProduct_RoundsBeforeSubtractingDelay()
        {
            CombatUnit systems = CreateSystems();
            systems.ApplyDisruption(2, 2);

            _damage.AdvanceSystems(systems, 1000, new SequenceRandom());

            Assert.AreEqual(1, systems.AttackDelay);
            Assert.AreEqual(1, systems.MovementDelay);
        }

        [TestCase(HardpointWeaponType.LaserCannon)]
        [TestCase(HardpointWeaponType.IonCannon)]
        [TestCase(HardpointWeaponType.Turbolaser)]
        [TestCase(HardpointWeaponType.TorpedoLauncher)]
        public void ApplyHit_UnorderedShieldDamageProgress_RequestsShieldDamage(
            HardpointWeaponType weapon
        )
        {
            CombatUnit systems = CreateUnitWithPools(
                false,
                100,
                100,
                float.PositiveInfinity,
                0,
                0,
                0,
                0,
                0,
                1,
                0
            );
            List<TacticalSystemEvent> events = new();
            _damage.SystemChangeRequested += (_, change) => events.Add(change);

            TacticalCombatImpactKind impact = _damage.ApplyHit(
                systems,
                weapon,
                1,
                1,
                1,
                new SequenceRandom(100),
                null
            );

            Assert.AreEqual(TacticalCombatImpactKind.Shield, impact);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(TacticalShipSystem.Shields, events[0].System);
            Assert.AreEqual(TacticalSystemEventKind.Damage, events[0].Kind);
            Assert.IsNull(events[0].AttackDelay);
            Assert.IsNull(events[0].MovementDelay);
        }

        [Test]
        public void ApplyHit_HullDamageScoreJustAboveThreshold_RequestsShieldDamage()
        {
            CombatUnit systems = CreateUnitWithPools(
                false,
                100.00001f,
                100.00001f,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                0
            );
            List<TacticalSystemEvent> events = new();
            _damage.SystemChangeRequested += (_, change) => events.Add(change);

            _damage.ApplyHit(
                systems,
                HardpointWeaponType.LaserCannon,
                1,
                1,
                1,
                new SequenceRandom(51),
                null
            );

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(TacticalShipSystem.Shields, events[0].System);
            Assert.AreEqual(TacticalSystemEventKind.Damage, events[0].Kind);
        }

        [Test]
        public void ApplyHit_UnorderedHullDamageScore_DoesNotRequestComponentDamage()
        {
            CombatUnit systems = CreateUnitWithPools(
                false,
                float.PositiveInfinity,
                float.PositiveInfinity,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                0
            );
            List<TacticalSystemEvent> events = new();
            _damage.SystemChangeRequested += (_, change) => events.Add(change);

            _damage.ApplyHit(
                systems,
                HardpointWeaponType.LaserCannon,
                1,
                1,
                1,
                new SequenceRandom(100),
                null
            );

            Assert.IsEmpty(events);
            Assert.AreEqual(float.PositiveInfinity, systems.Hull);
        }

        [Test]
        public void Advance_FighterWithoutShieldDeficit_UpdatesRechargeSample()
        {
            CombatUnit systems = CreateUnitWithPools(true, 96, 0, 0, 0, 0, 0, 0, 0, 1, 0);

            _damage.AdvanceSystems(systems, 1000, new SequenceRandom());

            Assert.AreEqual(0, TacticalCombatDamage.GetShieldRechargeRatio(systems));
        }

        [Test]
        public void Advance_UnorderedRepairRating_DoesNotConsumeRandomDraw()
        {
            CombatUnit systems = CreateUnitWithPools(
                false,
                100,
                100,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                float.NaN
            );
            TacticalCombatDamage.ApplyComponentEvent(systems, TacticalShipSystem.Engines, false);

            _damage.AdvanceSystems(systems, 1000, new SequenceRandom());

            Assert.AreEqual(1u, systems.GetDamage(TacticalShipSystem.Engines));
        }

        [Test]
        public void ApplyHit_UnorderedConventionalDamage_ClearsInvalidHull()
        {
            CombatUnit systems = CreateSystems();

            TacticalCombatImpactKind result = _damage.ApplyHit(
                systems,
                HardpointWeaponType.Turbolaser,
                float.NaN,
                1,
                1,
                new SequenceRandom(),
                null
            );

            Assert.AreEqual(TacticalCombatImpactKind.Destroyed, result);
            Assert.AreEqual(0, systems.Hull);
            Assert.AreEqual(0, systems.Shields);
        }

        [Test]
        public void Advance_DamagedSystem_RequestsRepairWithoutApplyingItEarly()
        {
            CombatUnit systems = CreateSystems();
            TacticalCombatDamage.ApplyComponentEvent(systems, TacticalShipSystem.Engines, false);
            List<TacticalSystemEvent> events = new();
            _damage.SystemChangeRequested += (_, change) => events.Add(change);
            _damage.AdvanceSystems(systems, 1000, new SequenceRandom(1));

            Assert.AreEqual(1u, systems.GetDamage(TacticalShipSystem.Engines));
            Assert.AreEqual(1, events.Count);
            Assert.IsTrue(events[0].IsRepair);
            Assert.AreEqual(TacticalShipSystem.Engines, events[0].System);
        }

        [Test]
        public void ApplyHit_FighterTarget_UsesRelativeManeuverRating()
        {
            CombatUnit systems = CreateUnitWithPools(true, 96, 96, 0, 0, 0, 8, 8, 0, 1, 0);
            _damage.ApplyHit(
                systems,
                HardpointWeaponType.LaserCannon,
                8,
                8,
                4,
                new SequenceRandom(),
                null
            );

            Assert.AreEqual(92, systems.Hull);
        }

        [Test]
        public void Advance_InitialWeaponOnlyDamage_DoesNotRequestRepairsOrConsumeRandomDraws()
        {
            CombatUnit systems = CreateSystems(
                initialDamage: new CapitalShipSystemDamage { Weapons = 4 }
            );
            List<TacticalSystemEvent> events = new();
            _damage.SystemChangeRequested += (_, change) => events.Add(change);
            SequenceRandom random = new(1, 1, 1, 1);

            _damage.AdvanceSystems(systems, 50, random);
            _damage.AdvanceSystems(systems, 50000, random);

            Assert.IsEmpty(events);
            Assert.IsEmpty(random.Requests);
            Assert.AreEqual(4, systems.GetDamage(TacticalShipSystem.Weapons));
        }

        [Test]
        public void Advance_InitialMixedDamage_StopsAfterCountedDamageIsRepaired()
        {
            CombatUnit systems = CreateSystems(
                initialDamage: new CapitalShipSystemDamage { Engines = 1, Weapons = 2 }
            );
            List<TacticalSystemEvent> events = new();
            _damage.SystemChangeRequested += (_, change) =>
            {
                events.Add(change);
                TacticalCombatDamage.ApplyComponentEvent(systems, change.System, change.IsRepair);
            };
            SequenceRandom random = new(1, 1, 1, 1);

            _damage.AdvanceSystems(systems, 50, random);
            _damage.AdvanceSystems(systems, 50000, random);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(TacticalShipSystem.Engines, events[0].System);
            Assert.AreEqual(0, systems.GetDamage(TacticalShipSystem.Engines));
            Assert.AreEqual(2, systems.GetDamage(TacticalShipSystem.Weapons));
            CollectionAssert.AreEqual(new[] { (1, 101) }, random.Requests);
        }

        [Test]
        public void Advance_SaturatedWeaponHit_AdmitsOneRepairForNewDamageEvent()
        {
            CombatUnit systems = CreateSystems(
                initialDamage: new CapitalShipSystemDamage { Weapons = 4 }
            );
            List<TacticalSystemEvent> events = new();
            _damage.SystemChangeRequested += (_, change) =>
            {
                events.Add(change);
                TacticalCombatDamage.ApplyComponentEvent(systems, change.System, change.IsRepair);
            };
            TacticalCombatDamage.ApplyComponentEvent(systems, TacticalShipSystem.Weapons, false);
            SequenceRandom random = new(1, 1, 1, 1);

            _damage.AdvanceSystems(systems, 50, random);
            _damage.AdvanceSystems(systems, 50000, random);

            Assert.AreEqual(1, events.Count);
            Assert.IsTrue(events[0].IsRepair);
            Assert.AreEqual(TacticalShipSystem.Weapons, events[0].System);
            Assert.AreEqual(3, systems.GetDamage(TacticalShipSystem.Weapons));
            CollectionAssert.AreEqual(new[] { (1, 101) }, random.Requests);
        }

        [Test]
        public void CaptureDamage_PreDamagedSecondDrive_PreservesTheDriveIndex()
        {
            CombatUnit unit = CreateUnitWithPools(
                false,
                100,
                100,
                0,
                0,
                0,
                0,
                0,
                0,
                3,
                0,
                new CapitalShipSystemDamage { HyperdriveDamage = new[] { 0, 1, 0 } }
            );

            CollectionAssert.AreEqual(new[] { 0, 1, 0 }, unit.CaptureDamage().HyperdriveDamage);
            TacticalCombatDamage.ApplyComponentEvent(unit, TacticalShipSystem.Hyperdrive, false);
            CollectionAssert.AreEqual(new[] { 1, 1, 0 }, unit.CaptureDamage().HyperdriveDamage);
            TacticalCombatDamage.ApplyComponentEvent(unit, TacticalShipSystem.Hyperdrive, true);
            CollectionAssert.AreEqual(new[] { 1, 0, 0 }, unit.CaptureDamage().HyperdriveDamage);
        }

        [Test]
        public void CaptureDamage_FullyRepairedComponents_ReturnsNoDamage()
        {
            CombatUnit systems = CreateUnitWithPools(
                false,
                100,
                100,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                0,
                new CapitalShipSystemDamage { Shields = 1 }
            );
            Assert.AreEqual(1, systems.CaptureDamage().Shields);
            TacticalCombatDamage.ApplyComponentEvent(systems, TacticalShipSystem.Shields, true);
            Assert.AreEqual(0, systems.CaptureDamage().Total);
        }

        [TestCase(HardpointWeaponType.LaserCannon)]
        [TestCase(HardpointWeaponType.Turbolaser)]
        [TestCase(HardpointWeaponType.TorpedoLauncher)]
        [TestCase(HardpointWeaponType.IonCannon)]
        public void ApplyHit_HullImpact_RequestsEffectBeforeComponentEvent(
            HardpointWeaponType weapon
        )
        {
            CombatUnit systems = CreateUnitWithPools(false, 100, 100, 0, 0, 0, 0, 0, 0, 1, 0);
            List<string> events = new();
            _damage.ImpactApplied += (_, kind, impact) =>
            {
                Assert.AreEqual(weapon, kind);
                Assert.AreEqual(TacticalCombatImpactKind.Hull, impact);
                events.Add("effect");
            };
            _damage.SystemChangeRequested += (_, _) => events.Add("component");
            _damage.ApplyHit(
                systems,
                weapon,
                1,
                1,
                1,
                new SequenceRandom(weapon == HardpointWeaponType.IonCannon ? 3 : 99),
                null,
                playSound: _ => events.Add("sound")
            );
            CollectionAssert.AreEqual(new[] { "sound", "effect", "component" }, events);
        }

        [TestCase(HardpointWeaponType.LaserCannon)]
        [TestCase(HardpointWeaponType.Turbolaser)]
        [TestCase(HardpointWeaponType.TorpedoLauncher)]
        [TestCase(HardpointWeaponType.IonCannon)]
        public void ApplyHit_ShieldImpact_RequestsComponentEventBeforeEffect(
            HardpointWeaponType weapon
        )
        {
            CombatUnit systems = CreateSystems();
            List<string> events = new();
            _damage.SystemChangeRequested += (_, _) => events.Add("component");
            _damage.ImpactApplied += (_, kind, impact) =>
            {
                Assert.AreEqual(weapon, kind);
                Assert.AreEqual(TacticalCombatImpactKind.Shield, impact);
                events.Add("effect");
            };

            _damage.ApplyHit(
                systems,
                weapon,
                1,
                1,
                1,
                new SequenceRandom(0),
                null,
                playSound: _ => events.Add("sound")
            );

            CollectionAssert.AreEqual(new[] { "sound", "component", "effect" }, events);
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        public void ApplyHit_InactiveHull_DoesNotRequestSoundReportOrEffect(float hull)
        {
            CombatUnit systems = CreateUnitWithPools(false, 100, hull, 0, 0, 0, 0, 0, 0, 1, 0);
            List<string> events = new();
            _damage.LethalHit += (_, _) => events.Add("report");
            _damage.ImpactApplied += (_, _, _) => events.Add("effect");

            TacticalCombatImpactKind impact = _damage.ApplyHit(
                systems,
                HardpointWeaponType.Turbolaser,
                100,
                1,
                1,
                new SequenceRandom(),
                "attacker",
                playSound: _ => events.Add("sound")
            );

            Assert.AreEqual(TacticalCombatImpactKind.None, impact);
            Assert.IsEmpty(events);
        }

        [Test]
        public void GetMovementBudget_UnorderedMovementDelay_EvaluatesMovement()
        {
            CombatUnit systems = CreateSystems();
            float budget = TacticalCombatMotion.GetMovementBudget(systems, 1, 0);
            systems.ApplyDisruption(0, float.NaN);

            Assert.AreEqual(budget, TacticalCombatMotion.GetMovementBudget(systems, 1, 0));
        }

        /// <summary>
        /// Creates a capital ship with an installed hyperdrive and repair crew.
        /// </summary>
        /// <param name="currentHull">The hull strength at entry.</param>
        /// <param name="initialDamage">The component damage captured before the encounter.</param>
        /// <returns>The captured tactical systems.</returns>
        private static CombatUnit CreateSystems(
            float currentHull = 1000,
            CapitalShipSystemDamage initialDamage = null
        )
        {
            return CreateUnitWithPools(
                false,
                1000,
                currentHull,
                100,
                20,
                10,
                3,
                2,
                2,
                1,
                100,
                initialDamage
            );
        }

        /// <summary>
        /// Builds a capital ship or fighter with explicit combat pools and ratings.
        /// </summary>
        /// <param name="isFighter">Whether the participant is a fighter squadron.</param>
        /// <param name="maximumHull">The undamaged hull capacity.</param>
        /// <param name="currentHull">The initial live hull strength.</param>
        /// <param name="maximumShields">The maximum shield pool.</param>
        /// <param name="weaponRechargeRate">The installed weapon recharge rating.</param>
        /// <param name="shieldRechargeRate">The installed shield recharge rating.</param>
        /// <param name="sublightSpeed">The ship's sublight speed rating.</param>
        /// <param name="maneuverability">The ship's maneuverability rating.</param>
        /// <param name="tractorPower">The installed tractor beam rating.</param>
        /// <param name="hyperdrives">The number of installed drive systems.</param>
        /// <param name="damageControlRating">The ship's repair probability rating.</param>
        /// <param name="initialDamage">The component damage already present at battle entry.</param>
        /// <returns>The tactical participant with the requested starting state.</returns>
        internal static CombatUnit CreateUnitWithPools(
            bool isFighter,
            float maximumHull,
            float currentHull,
            float maximumShields,
            float weaponRechargeRate,
            float shieldRechargeRate,
            float sublightSpeed,
            float maneuverability,
            float tractorPower,
            uint hyperdrives,
            float damageControlRating,
            CapitalShipSystemDamage initialDamage = null
        )
        {
            BaseSceneNode ship = isFighter
                ? new Starfighter
                {
                    Hyperdrive = hyperdrives > 0 ? 1 : 0,
                    BackupHyperdrive = hyperdrives > 1 ? 1 : 0,
                }
                : new CapitalShip
                {
                    Hyperdrives = Enumerable
                        .Range(0, (int)hyperdrives)
                        .Select(_ => new Hyperdrive { Rating = 1 })
                        .ToArray(),
                    SystemDamage = initialDamage,
                };
            CombatUnit unit = CombatUnit.Create(ship);
            unit.InitializeCombatPools(
                maximumHull,
                currentHull,
                maximumShields,
                isFighter ? 0.25f : (float)((double)weaponRechargeRate * 3.75f),
                isFighter ? 1 : shieldRechargeRate,
                isFighter
                    ? (float)((double)sublightSpeed * 0.4f)
                    : (float)((double)sublightSpeed * 0.4f * 0.6f),
                maneuverability,
                tractorPower,
                hyperdrives,
                damageControlRating
            );
            if (initialDamage != null && !isFighter)
                unit.DamageEventCount = (uint)(initialDamage.Total - initialDamage.Weapons);
            return unit;
        }

        /// <summary>
        /// Provides only the explicitly authored random draws expected by a test.
        /// </summary>
        private sealed class SequenceRandom : IRandomNumberProvider
        {
            private readonly Queue<int> values;
            private readonly List<(int Minimum, int Maximum)> _requests = new();

            /// <summary>
            /// Gets the ordered bounds requested for integer draws.
            /// </summary>
            public IReadOnlyList<(int Minimum, int Maximum)> Requests => _requests;

            /// <summary>
            /// Captures a test's ordered integer draws.
            /// </summary>
            /// <param name="draws">The integer results in call order.</param>
            public SequenceRandom(params int[] draws)
            {
                values = new Queue<int>(draws);
            }

            /// <summary>
            /// Rejects an unexpected floating-point draw.
            /// </summary>
            /// <returns>No value; this provider supports only integer draws.</returns>
            public double NextDouble()
            {
                throw new NotSupportedException();
            }

            /// <summary>
            /// Returns the next authored integer within the requested bounds.
            /// </summary>
            /// <param name="min">The inclusive lower bound.</param>
            /// <param name="max">The exclusive upper bound.</param>
            /// <returns>The next authored value.</returns>
            public int NextInt(int min, int max)
            {
                _requests.Add((min, max));
                int value = values.Dequeue();
                Assert.That(value, Is.GreaterThanOrEqualTo(min).And.LessThan(max));
                return value;
            }
        }
    }
}
