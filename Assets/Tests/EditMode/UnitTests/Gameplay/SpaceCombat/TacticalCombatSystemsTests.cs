using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Gameplay;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatSystemsTests
    {
        [TestCase(TacticalWeaponType.LaserCannon, 0f)]
        [TestCase(TacticalWeaponType.LaserCannon, -5f)]
        [TestCase(TacticalWeaponType.LaserCannon, float.NaN)]
        [TestCase(TacticalWeaponType.IonCannon, 0f)]
        [TestCase(TacticalWeaponType.IonCannon, -5f)]
        [TestCase(TacticalWeaponType.IonCannon, float.NaN)]
        [TestCase(TacticalWeaponType.Turbolaser, 0f)]
        [TestCase(TacticalWeaponType.Turbolaser, -5f)]
        [TestCase(TacticalWeaponType.Turbolaser, float.NaN)]
        [TestCase(TacticalWeaponType.Torpedo, 0f)]
        [TestCase(TacticalWeaponType.Torpedo, -5f)]
        [TestCase(TacticalWeaponType.Torpedo, float.NaN)]
        public void ApplyHit_NonpositiveEntryHull_ClampsHullWithoutImpactSideEffects(
            TacticalWeaponType weapon,
            float entryHull
        )
        {
            TacticalCombatSystems systems = new(false, 100, entryHull, 37, 0, 0, 0, 0, 0, 1, 0);
            SystemRandomProvider random = new(42);
            List<string> events = new();
            systems.SoundRequested += _ => events.Add("sound");
            systems.LethalHit += _ => events.Add("report");
            systems.ImpactRequested += (_, _) => events.Add("impact");
            systems.SystemEventRequested += _ => events.Add("component");

            TacticalCombatImpactKind impact = systems.ApplyHit(
                weapon,
                100,
                1,
                1,
                random,
                "attacker"
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
            TacticalCombatSystems systems = CreateSystems();
            float shields = systems.Shields;

            systems.Destroy();

            Assert.AreEqual(0, systems.Hull);
            Assert.AreEqual(shields, systems.Shields);
        }

        [Test]
        public void ApplyHit_ConventionalDamage_ConsumesShieldsBeforeHull()
        {
            TacticalCombatSystems systems = CreateSystems();
            systems.ApplyHit(TacticalWeaponType.Turbolaser, 125, 1, 1, new SequenceRandom(0), null);

            Assert.AreEqual(0, systems.Shields);
            Assert.AreEqual(975, systems.Hull);
        }

        [Test]
        public void ApplyHit_IonDamagePastShields_DoesNotDamageHull()
        {
            TacticalCombatSystems systems = CreateSystems();
            systems.ApplyHit(TacticalWeaponType.IonCannon, 101, 1, 1, new SequenceRandom(3), null);

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
            TacticalCombatSystems systems = new(false, 100, 100, 0, 0, 0, 0, 0, 0, 1, 0);
            List<string> events = new();
            systems.SoundRequested += sound => events.Add(sound.ToString());
            systems.LethalHit += attackerID =>
            {
                Assert.AreEqual("attacker", attackerID);
                Assert.AreEqual(reportedHull, systems.Hull);
                events.Add("report");
            };
            systems.ImpactRequested += (weapon, impact) =>
            {
                Assert.AreEqual(TacticalWeaponType.Turbolaser, weapon);
                Assert.AreEqual(TacticalCombatImpactKind.Destroyed, impact);
                Assert.AreEqual(0, systems.Hull);
                events.Add("effect");
            };

            TacticalCombatImpactKind result = systems.ApplyHit(
                TacticalWeaponType.Turbolaser,
                damage,
                1,
                1,
                new SequenceRandom(),
                "attacker"
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
            TacticalCombatSystems systems = new(true, 100, 100, 0, 0, 0, 0, 0, 0, 1, 0);
            List<string> reports = new();
            systems.LethalHit += reports.Add;

            systems.ApplyHit(
                TacticalWeaponType.IonCannon,
                150,
                1,
                1,
                new SequenceRandom(),
                "attacker"
            );

            Assert.IsEmpty(reports);
            Assert.AreEqual(100, systems.Hull);
        }

        [TestCase(1, 0)]
        [TestCase(2, 0)]
        [TestCase(3, 4)]
        [TestCase(4, 4)]
        [TestCase(5, 8)]
        [TestCase(6, 8)]
        [TestCase(7, 0)]
        [TestCase(8, 0)]
        [TestCase(9, 0)]
        [TestCase(10, 0)]
        public void ApplyHit_IonComponentSelection_RequestsKnownBatteriesWithoutTimerValues(
            int selector,
            int expectedMask
        )
        {
            TacticalCombatSystems systems = CreateSystems();
            List<TacticalSystemEvent> events = new();
            systems.SystemEventRequested += events.Add;
            SequenceRandom random =
                selector <= 2 ? new SequenceRandom(selector, 35) : new SequenceRandom(selector);

            systems.ApplyHit(TacticalWeaponType.IonCannon, 101, 1, 1, random, null);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(TacticalSystemEventKind.Disruption, events[0].Kind);
            Assert.AreEqual(expectedMask, events[0].DischargedArcMask);
            Assert.IsNull(events[0].AttackDelay);
            Assert.IsNull(events[0].MovementDelay);
            CollectionAssert.AreEqual(
                selector <= 2 ? new[] { (1, 11), (30, 51) } : new[] { (1, 11) },
                random.Requests
            );
        }

        [Test]
        public void ApplyHit_MixedIonComponentSelections_PreservesDrawOrderAndKnownBatteryMask()
        {
            TacticalCombatSystems systems = CreateSystems();
            List<TacticalSystemEvent> events = new();
            systems.SystemEventRequested += events.Add;
            SequenceRandom random = new(1, 30, 2, 50, 3, 5, 7, 10);

            systems.ApplyHit(TacticalWeaponType.IonCannon, 106, 1, 1, random, null);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(12, events[0].DischargedArcMask);
            Assert.IsNull(events[0].AttackDelay);
            Assert.IsNull(events[0].MovementDelay);
            CollectionAssert.AreEqual(
                new[] { (1, 11), (30, 51), (1, 11), (30, 51), (1, 11), (1, 11), (1, 11), (1, 11) },
                random.Requests
            );
        }

        [Test]
        public void ApplyHit_UnorderedIonExcess_RequestsUnresolvedDisruptionWithoutComponentDraws()
        {
            TacticalCombatSystems systems = CreateSystems();
            List<TacticalSystemEvent> events = new();
            systems.SystemEventRequested += events.Add;
            SequenceRandom random = new();

            systems.ApplyHit(TacticalWeaponType.IonCannon, float.NaN, 1, 1, random, null);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(0, events[0].DischargedArcMask);
            Assert.IsNull(events[0].AttackDelay);
            Assert.IsNull(events[0].MovementDelay);
            Assert.IsEmpty(random.Requests);
        }

        [Test]
        public void GetWeaponRecharge_PartialFighterHull_RetainsLiveBudgetBeforeSampleStore()
        {
            TacticalCombatSystems systems = new(true, 96, 73, 0, 0, 0, 0, 0, 0, 1, 0);

            double recharge = systems.GetWeaponRecharge();

            Assert.AreEqual(0x40411d2afc800000L, BitConverter.DoubleToInt64Bits(recharge));
        }

        [Test]
        public void GetWeaponRecharge_HullAndWeaponDamage_ReducesBaseRecharge()
        {
            TacticalCombatSystems systems = CreateSystems(currentHull: 500);
            systems.ApplyComponentEvent(TacticalShipSystem.Weapons, false);

            Assert.AreEqual(18.75f, systems.GetWeaponRecharge());
        }

        [Test]
        public void HullFraction_PartialHull_RetainsDivisionUntilConsumerStore()
        {
            TacticalCombatSystems systems = CreateSystems(currentHull: 631);

            double fraction = systems.HullFraction;

            Assert.AreEqual(0x3fe43126e978d4feL, BitConverter.DoubleToInt64Bits(fraction));
        }

        [Test]
        public void GetWeaponRechargeRatio_FighterSample_UsesUpdatedStoredRateWithoutRoundingNumerator()
        {
            TacticalCombatSystems systems = new(true, 96, 96, 0, 0, 0, 0, 0, 0, 1, 0);

            double ratio = systems.GetWeaponRechargeRatio();

            Assert.AreEqual(0x3fefffffe9402186L, BitConverter.DoubleToInt64Bits(ratio));
        }

        [Test]
        public void GetWeaponRecharge_FighterZeroHull_StoresZeroForSubsequentRatioAdmission()
        {
            TacticalCombatSystems systems = new(true, 96, 96, 0, 0, 0, 0, 0, 0, 1, 0);
            systems.Destroy();

            Assert.AreEqual(0, systems.GetWeaponRecharge());
            Assert.AreEqual(0, systems.GetWeaponRechargeRatio());
        }

        [Test]
        public void GetTractorRatio_HullAndSystemDamage_UsesCompleteTractorBudget()
        {
            TacticalCombatSystems systems = new(false, 1000, 500, 0, 0, 0, 0, 0, 8, 1, 0);
            systems.ApplyComponentEvent(TacticalShipSystem.TractorBeam, false);

            Assert.AreEqual(0.25d, systems.GetTractorRatio());
        }

        [Test]
        public void GetMovementRatio_LiveSupportDamageAndRestraint_UsesStoredEngineDenominator()
        {
            TacticalCombatSystems systems = new(true, 96, 96, 0, 0, 0, 20, 0, 0, 1, 0);
            systems.ApplyComponentEvent(TacticalShipSystem.Engines, false);

            Assert.AreEqual(1d, systems.GetMovementRatio(8, 4));
            systems.ApplyDisruption(0, 1);
            Assert.AreEqual(0d, systems.GetMovementRatio(8, 4));
        }

        [Test]
        public void GetMovementRatio_ZeroStoredEngines_DoesNotDivideLiveCommandSupport()
        {
            TacticalCombatSystems systems = new(false, 100, 100, 0, 0, 0, 0, 0, 0, 1, 0);

            Assert.AreEqual(0d, systems.GetMovementRatio(9, 0));
            Assert.AreEqual(9d, systems.GetMovementBudget(9, 0));
        }

        [TestCase(false, 5f)]
        [TestCase(true, 6f)]
        public void GetTurnBudget_PositiveMovementBelowStoredPrecision_RetainsManeuverability(
            bool isFighter,
            float expected
        )
        {
            TacticalCombatSystems systems = new(isFighter, 100, 100, 0, 0, 0, 0, 5, 0, 1, 0);
            for (int index = 0; index < 3; index++)
                systems.ApplyComponentEvent(TacticalShipSystem.Engines, false);

            float turnBudget = systems.GetTurnBudget(float.Epsilon, 0);

            Assert.AreEqual(0, systems.GetMovementBudget(float.Epsilon, 0));
            Assert.AreEqual(expected, turnBudget);
        }

        [Test]
        public void GetHyperdriveRatio_DamagedPrimaryAndBackup_TracksRemainingInstalledDriveCount()
        {
            TacticalCombatSystems systems = new(false, 100, 100, 0, 0, 0, 0, 0, 0, 2, 0);
            systems.ApplyComponentEvent(TacticalShipSystem.Hyperdrive, false);

            Assert.AreEqual(0.5d, systems.GetHyperdriveRatio());
            systems.ApplyComponentEvent(TacticalShipSystem.Hyperdrive, false);
            Assert.AreEqual(0d, systems.GetHyperdriveRatio());
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        public void GetShieldFraction_ZeroOrUnorderedLimit_ReturnsZero(float limit)
        {
            TacticalCombatSystems systems = new(false, 100, 100, limit, 0, 0, 0, 0, 0, 1, 0);

            Assert.AreEqual(0d, systems.GetShieldFraction());
        }

        [Test]
        public void GetShieldFraction_OverLimitPool_RetainsUnclampedRatio()
        {
            TacticalCombatSystems systems = new(false, 100, 100, 100, 0, 0, 0, 0, 0, 1, 0);
            systems.RestorePools(100, 125);

            Assert.AreEqual(1.25d, systems.GetShieldFraction());
        }

        [Test]
        public void GetShieldRecharge_FighterHull_ReturnsLiveBudgetBeforeStoredSampleRounding()
        {
            TacticalCombatSystems systems = new(true, 96, 96, 0, 0, 0, 0, 0, 0, 1, 0);

            double recharge = systems.GetShieldRecharge();

            Assert.AreEqual(0.30000000447034836d, recharge);
            Assert.AreEqual(
                0x3feffffff2aaaab4L,
                BitConverter.DoubleToInt64Bits(systems.GetShieldRechargeRatio())
            );
        }

        [Test]
        public void GetShieldRechargeRatio_DestroyedFighter_UpdatesSampleBeforeDivisionThenStopsAtZero()
        {
            TacticalCombatSystems systems = new(true, 96, 96, 0, 0, 0, 0, 0, 0, 1, 0);
            systems.ApplyHullLoss(96);

            double first = systems.GetShieldRechargeRatio();
            double second = systems.GetShieldRechargeRatio();

            Assert.IsTrue(double.IsNaN(first));
            Assert.AreEqual(0, second);
        }

        [Test]
        public void GetShieldRechargeRatio_ZeroSampleFromRechargeRead_ReturnsZeroWithoutAnotherDivision()
        {
            TacticalCombatSystems systems = new(true, 96, 96, 0, 0, 0, 0, 0, 0, 1, 0);
            systems.ApplyHullLoss(96);
            Assert.AreEqual(0, systems.GetShieldRecharge());

            double ratio = systems.GetShieldRechargeRatio();

            Assert.AreEqual(0, ratio);
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        public void GetShieldRechargeRatio_UnavailableCapitalRate_ReturnsZero(float rechargeRate)
        {
            TacticalCombatSystems systems = new(
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

            double ratio = systems.GetShieldRechargeRatio();

            Assert.AreEqual(0, ratio);
        }

        [Test]
        public void GetShieldRechargeRatio_CapitalShieldRepair_PreservesAuthoredRateAsDenominator()
        {
            TacticalCombatSystems systems = new(
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
                new Rebellion.Game.Units.CapitalShipSystemDamage { Shields = 4 }
            );
            Assert.AreEqual(0, systems.GetShieldRechargeRatio());

            systems.ApplyComponentEvent(TacticalShipSystem.Shields, true);

            Assert.AreEqual(0.25d, systems.GetShieldRechargeRatio());
            Assert.AreEqual(2d, systems.GetShieldRecharge());
        }

        [Test]
        public void ScaleByHull_PartialHull_MultipliesBeforeDividing()
        {
            TacticalCombatSystems systems = CreateSystems(currentHull: 631);

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
            TacticalCombatSystems systems = new(false, 96, 96, 12, 0, 0, 0, 0, 0, 0, 0);

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
            TacticalCombatSystems systems = new(false, 96, entryHull, 12, 0, 0, 0, 0, 0, 0, 0);

            systems.ApplyHullLoss(-8);

            Assert.AreEqual(0, systems.Hull);
            Assert.AreEqual(12, systems.Shields);
        }

        [Test]
        public void ApplyHullLoss_FatalLoss_ClearsHullAndShields()
        {
            TacticalCombatSystems systems = new(false, 96, 96, 12, 0, 0, 0, 0, 0, 0, 0);

            systems.ApplyHullLoss(1000);

            Assert.AreEqual(0, systems.Hull);
            Assert.AreEqual(0, systems.Shields);
        }

        [Test]
        public void ApplyComponentEvent_FourEngineHits_DisablesMovement()
        {
            TacticalCombatSystems systems = CreateSystems();
            for (int hit = 0; hit < 4; hit++)
                systems.ApplyComponentEvent(TacticalShipSystem.Engines, false);

            Assert.AreEqual(0, systems.GetMovementBudget(1, 0));
        }

        [Test]
        public void ApplyComponentEvent_LastHyperdriveHit_PreventsHyperspace()
        {
            TacticalCombatSystems systems = CreateSystems();
            systems.ApplyComponentEvent(TacticalShipSystem.Hyperdrive, false);

            Assert.IsFalse(systems.HasHyperdrive);
        }

        [Test]
        public void Advance_TemporaryDisruption_RecoversAfterElapsedTime()
        {
            TacticalCombatSystems systems = CreateSystems();
            systems.ApplyDisruption(30, 50);
            systems.Advance(1000, new SequenceRandom());

            Assert.AreEqual(29, systems.AttackDelay);
            Assert.AreEqual(49, systems.MovementDelay);
        }

        [Test]
        public void ApplyDisruption_ExplicitEvent_UsesDefinedTimerIncrements()
        {
            TacticalCombatSystems systems = CreateSystems();
            TacticalSystemEvent change = new(11, 17, 4);

            systems.ApplyDisruption(change.AttackDelay.Value, change.MovementDelay.Value);

            Assert.AreEqual(11, systems.AttackDelay);
            Assert.AreEqual(17, systems.MovementDelay);
        }

        [Test]
        public void Advance_RepeatedDisruptionDecay_UsesStoredSinglePrecisionElapsedProduct()
        {
            TacticalCombatSystems systems = CreateSystems();
            systems.ApplyDisruption(30, 30);

            for (int index = 0; index < 29; index++)
                systems.Advance(1000, new SequenceRandom());

            Assert.AreEqual(1, systems.AttackDelay);
            Assert.AreEqual(1, systems.MovementDelay);
        }

        [Test]
        public void Advance_DisruptionSecondsProduct_RoundsBeforeSubtractingDelay()
        {
            TacticalCombatSystems systems = CreateSystems();
            systems.ApplyDisruption(2, 2);

            systems.Advance(1000, new SequenceRandom());

            Assert.AreEqual(1, systems.AttackDelay);
            Assert.AreEqual(1, systems.MovementDelay);
        }

        [TestCase(TacticalWeaponType.LaserCannon)]
        [TestCase(TacticalWeaponType.IonCannon)]
        [TestCase(TacticalWeaponType.Turbolaser)]
        [TestCase(TacticalWeaponType.Torpedo)]
        public void ApplyHit_UnorderedShieldDamageProgress_RequestsShieldDamage(
            TacticalWeaponType weapon
        )
        {
            TacticalCombatSystems systems = new(
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
            systems.SystemEventRequested += events.Add;

            TacticalCombatImpactKind impact = systems.ApplyHit(
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
            TacticalCombatSystems systems = new(
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
            systems.SystemEventRequested += events.Add;

            systems.ApplyHit(TacticalWeaponType.LaserCannon, 1, 1, 1, new SequenceRandom(51), null);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(TacticalShipSystem.Shields, events[0].System);
            Assert.AreEqual(TacticalSystemEventKind.Damage, events[0].Kind);
        }

        [Test]
        public void ApplyHit_UnorderedHullDamageScore_DoesNotRequestComponentDamage()
        {
            TacticalCombatSystems systems = new(
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
            systems.SystemEventRequested += events.Add;

            systems.ApplyHit(
                TacticalWeaponType.LaserCannon,
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
            TacticalCombatSystems systems = new(true, 96, 0, 0, 0, 0, 0, 0, 0, 1, 0);

            systems.Advance(1000, new SequenceRandom());

            Assert.AreEqual(0, systems.GetShieldRechargeRatio());
        }

        [Test]
        public void Advance_UnorderedRepairRating_DoesNotConsumeRandomDraw()
        {
            TacticalCombatSystems systems = new(false, 100, 100, 0, 0, 0, 0, 0, 0, 1, float.NaN);
            systems.ApplyComponentEvent(TacticalShipSystem.Engines, false);

            systems.Advance(1000, new SequenceRandom());

            Assert.AreEqual(1u, systems.GetDamage(TacticalShipSystem.Engines));
        }

        [Test]
        public void ApplyHit_UnorderedConventionalDamage_ClearsInvalidHull()
        {
            TacticalCombatSystems systems = CreateSystems();

            TacticalCombatImpactKind result = systems.ApplyHit(
                TacticalWeaponType.Turbolaser,
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
            TacticalCombatSystems systems = CreateSystems();
            systems.ApplyComponentEvent(TacticalShipSystem.Engines, false);
            List<TacticalSystemEvent> events = new();
            systems.SystemEventRequested += events.Add;
            systems.Advance(1000, new SequenceRandom(1));

            Assert.AreEqual(1u, systems.GetDamage(TacticalShipSystem.Engines));
            Assert.AreEqual(1, events.Count);
            Assert.IsTrue(events[0].IsRepair);
            Assert.AreEqual(TacticalShipSystem.Engines, events[0].System);
        }

        [Test]
        public void ApplyHit_FighterTarget_UsesRelativeManeuverRating()
        {
            TacticalCombatSystems systems = new(true, 96, 96, 0, 0, 0, 8, 8, 0, 1, 0);
            systems.ApplyHit(TacticalWeaponType.LaserCannon, 8, 8, 4, new SequenceRandom(), null);

            Assert.AreEqual(92, systems.Hull);
        }

        [Test]
        public void CaptureDamage_ExistingRepairSchedule_PreservesCampaignTimer()
        {
            TacticalCombatSystems systems = new(
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
                new Rebellion.Game.Units.CapitalShipSystemDamage
                {
                    Shields = 1,
                    RepairDueTick = 42,
                    RepairInterval = 30,
                }
            );
            Rebellion.Game.Units.CapitalShipSystemDamage result = systems.CaptureDamage(
                true,
                false
            );
            Assert.AreEqual(42, result.RepairDueTick);
            Assert.AreEqual(30, result.RepairInterval);
        }

        [Test]
        public void CaptureDamage_FullyRepairedComponents_ClearsCampaignTimer()
        {
            TacticalCombatSystems systems = new(
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
                new Rebellion.Game.Units.CapitalShipSystemDamage
                {
                    Shields = 1,
                    RepairDueTick = 42,
                    RepairInterval = 30,
                }
            );
            systems.ApplyComponentEvent(TacticalShipSystem.Shields, true);
            Rebellion.Game.Units.CapitalShipSystemDamage result = systems.CaptureDamage(
                true,
                false
            );
            Assert.AreEqual(0, result.RepairDueTick);
            Assert.AreEqual(0, result.RepairInterval);
        }

        [TestCase(TacticalWeaponType.LaserCannon)]
        [TestCase(TacticalWeaponType.Turbolaser)]
        [TestCase(TacticalWeaponType.Torpedo)]
        [TestCase(TacticalWeaponType.IonCannon)]
        public void ApplyHit_HullImpact_RequestsEffectBeforeComponentEvent(
            TacticalWeaponType weapon
        )
        {
            TacticalCombatSystems systems = new(false, 100, 100, 0, 0, 0, 0, 0, 0, 1, 0);
            System.Collections.Generic.List<string> events = new();
            systems.SoundRequested += _ => events.Add("sound");
            systems.ImpactRequested += (kind, impact) =>
            {
                Assert.AreEqual(weapon, kind);
                Assert.AreEqual(TacticalCombatImpactKind.Hull, impact);
                events.Add("effect");
            };
            systems.SystemEventRequested += _ => events.Add("component");
            systems.ApplyHit(
                weapon,
                1,
                1,
                1,
                new SequenceRandom(weapon == TacticalWeaponType.IonCannon ? 3 : 99),
                null
            );
            CollectionAssert.AreEqual(new[] { "sound", "effect", "component" }, events);
        }

        [TestCase(TacticalWeaponType.LaserCannon)]
        [TestCase(TacticalWeaponType.Turbolaser)]
        [TestCase(TacticalWeaponType.Torpedo)]
        [TestCase(TacticalWeaponType.IonCannon)]
        public void ApplyHit_ShieldImpact_RequestsComponentEventBeforeEffect(
            TacticalWeaponType weapon
        )
        {
            TacticalCombatSystems systems = CreateSystems();
            List<string> events = new();
            systems.SoundRequested += _ => events.Add("sound");
            systems.SystemEventRequested += _ => events.Add("component");
            systems.ImpactRequested += (kind, impact) =>
            {
                Assert.AreEqual(weapon, kind);
                Assert.AreEqual(TacticalCombatImpactKind.Shield, impact);
                events.Add("effect");
            };

            systems.ApplyHit(weapon, 1, 1, 1, new SequenceRandom(0), null);

            CollectionAssert.AreEqual(new[] { "sound", "component", "effect" }, events);
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        public void ApplyHit_InactiveHull_DoesNotRequestSoundReportOrEffect(float hull)
        {
            TacticalCombatSystems systems = new(false, 100, hull, 0, 0, 0, 0, 0, 0, 1, 0);
            List<string> events = new();
            systems.SoundRequested += _ => events.Add("sound");
            systems.LethalHit += _ => events.Add("report");
            systems.ImpactRequested += (_, _) => events.Add("effect");

            TacticalCombatImpactKind impact = systems.ApplyHit(
                TacticalWeaponType.Turbolaser,
                100,
                1,
                1,
                new SequenceRandom(),
                "attacker"
            );

            Assert.AreEqual(TacticalCombatImpactKind.None, impact);
            Assert.IsEmpty(events);
        }

        [Test]
        public void GetMovementBudget_UnorderedMovementDelay_EvaluatesMovement()
        {
            TacticalCombatSystems systems = CreateSystems();
            float budget = systems.GetMovementBudget(1, 0);
            systems.ApplyDisruption(0, float.NaN);

            Assert.AreEqual(budget, systems.GetMovementBudget(1, 0));
        }

        /// <summary>
        /// Creates a capital ship with an installed hyperdrive and repair crew.
        /// </summary>
        /// <param name="currentHull">The hull strength at entry.</param>
        /// <returns>The captured tactical systems.</returns>
        private static TacticalCombatSystems CreateSystems(float currentHull = 1000)
        {
            return new TacticalCombatSystems(
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
                100
            );
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
