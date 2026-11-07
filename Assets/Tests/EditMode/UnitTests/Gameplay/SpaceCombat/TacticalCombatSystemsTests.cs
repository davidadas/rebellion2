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
        [Test]
        public void ApplyHit_ConventionalDamage_ConsumesShieldsBeforeHull()
        {
            TacticalCombatSystems systems = CreateSystems();
            systems.ApplyHit(TacticalWeaponType.Turbolaser, 125, 1, 1, new SequenceRandom(0));

            Assert.AreEqual(0, systems.Shields);
            Assert.AreEqual(975, systems.Hull);
        }

        [Test]
        public void ApplyHit_IonDamagePastShields_DoesNotDamageHull()
        {
            TacticalCombatSystems systems = CreateSystems();
            systems.ApplyHit(TacticalWeaponType.IonCannon, 101, 1, 1, new SequenceRandom(3));

            Assert.AreEqual(1000, systems.Hull);
            Assert.AreEqual(0, systems.Shields);
        }

        [Test]
        public void ApplyHit_IonDamagePastShields_RequestsBatteryDisruption()
        {
            TacticalCombatSystems systems = CreateSystems();
            List<TacticalSystemEvent> events = new();
            systems.SystemEventRequested += events.Add;
            systems.ApplyHit(TacticalWeaponType.IonCannon, 101, 1, 1, new SequenceRandom(3));

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(1, events[0].DischargedArcMask);
        }

        [Test]
        public void GetWeaponRecharge_HullAndWeaponDamage_ReducesBaseRecharge()
        {
            TacticalCombatSystems systems = CreateSystems(currentHull: 500);
            systems.ApplyComponentEvent(TacticalShipSystem.Weapons, false);

            Assert.AreEqual(18.75f, systems.GetWeaponRecharge());
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
            systems.ApplyHit(TacticalWeaponType.LaserCannon, 8, 8, 4, new SequenceRandom());

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

        [Test]
        public void ApplyHit_HullImpact_RequestsSoundBeforeComponentEvent()
        {
            TacticalCombatSystems systems = new(false, 100, 100, 0, 0, 0, 0, 0, 0, 1, 0);
            System.Collections.Generic.List<string> events = new();
            systems.SoundRequested += _ => events.Add("sound");
            systems.SystemEventRequested += _ => events.Add("component");
            systems.ApplyHit(
                TacticalWeaponType.LaserCannon,
                1,
                1,
                1,
                new FixedRandomProvider(new[] { .99 })
            );
            CollectionAssert.AreEqual(new[] { "sound", "component" }, events);
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
                int value = values.Dequeue();
                Assert.That(value, Is.GreaterThanOrEqualTo(min).And.LessThan(max));
                return value;
            }
        }
    }
}
