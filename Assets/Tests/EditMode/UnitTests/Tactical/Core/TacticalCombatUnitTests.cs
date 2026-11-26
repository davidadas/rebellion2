using System.Numerics;
using NUnit.Framework;
using Rebellion.Content;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Tactical;

namespace Rebellion.Tests.Tactical
{
    [TestFixture]
    public sealed class TacticalCombatUnitTests
    {
        [Test]
        public void Constructor_ReusedCampaignShip_CreatesIndependentCombatants()
        {
            CapitalShip ship = new()
            {
                InstanceID = "ship",
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
            };
            CombatUnit first = new(ship, CombatSide.Attacker, 1);
            CombatUnit second = new(ship, CombatSide.Attacker, 1);

            first.ApplyHullLoss(20);
            first.ApplyComponentEvent(TacticalShipSystem.Engines, false);

            Assert.AreEqual(100, second.Hull);
            Assert.AreEqual(0, second.GetDamage(TacticalShipSystem.Engines));
            Assert.AreEqual(0, ship.SystemDamage.Engines);
            Assert.AreNotSame(first.Batteries[0], second.Batteries[0]);
        }

        [TestCase(0, 1, 0, true)]
        [TestCase(1, 1, 1, true)]
        [TestCase(1, 1, 2, false)]
        public void Constructor_FighterDriveDamage_RetainsBackupAvailability(
            int primary,
            int backup,
            int damaged,
            bool expected
        )
        {
            Starfighter fighter = new()
            {
                InstanceID = "fighter",
                MaxSquadronSize = 12,
                CurrentSquadronSize = 12,
                Hyperdrive = primary,
            };
            fighter.BackupHyperdrive = backup;
            CombatUnit unit = new(fighter, CombatSide.Attacker, 1);
            for (int index = 0; index < damaged; index++)
                unit.ApplyComponentEvent(TacticalShipSystem.Hyperdrive, false);

            Assert.AreEqual(expected, unit.HasHyperdrive);
        }

        [TestCase(0, 3.6f)]
        [TestCase(21, 19f)]
        [TestCase(37, 3.6f)]
        public void Constructor_CapitalPresentationType_SelectsFixedCollisionBounds(
            int presentationIndex,
            float expectedRadius
        )
        {
            TacticalCombatUnitDefinition definition = new()
            {
                PresentationIndex = presentationIndex,
                HullWidth = 40,
                HullHeight = 50,
                HullDepth = 60,
            };

            CombatUnit unit = CreateWithLegacyGeometry(definition);

            Assert.AreEqual(3, unit.HalfHeight);
            Assert.AreEqual(expectedRadius, unit.CollisionRadius);
            Assert.AreEqual(new Vector3(40, 50, 60), unit.HullDimensions);
        }

        [Test]
        public void Constructor_OrdinaryCapitalWithAuthoredTurnCap_UsesUncappedManeuverRate()
        {
            CombatUnit unit = CreateWithLegacyGeometry(
                new TacticalCombatUnitDefinition
                {
                    PresentationIndex = 20,
                    MaximumTurnRate = 0.001f,
                }
            );

            Assert.That(unit.TurnRate, Is.GreaterThan(0.001f));
            Assert.AreEqual(0, unit.MaximumTurnRate);
        }

        [Test]
        public void Constructor_LargeCapitalWithoutAuthoredTurnCap_UsesFixedTurnCap()
        {
            CombatUnit unit = CreateWithLegacyGeometry(
                new TacticalCombatUnitDefinition { PresentationIndex = 21 }
            );

            Assert.AreEqual(0.0523598827f, unit.MaximumTurnRate);
            Assert.AreEqual(0.0523598827f, unit.TurnRate);
        }

        private static CombatUnit CreateWithLegacyGeometry(TacticalCombatUnitDefinition definition)
        {
            CombatUnit unit = CombatUnit.Create(CreateShip());
            definition.InitializeCombatant(unit, CombatSide.Attacker, 1);
            return unit;
        }

        [Test]
        public void Constructor_IncompleteOrdinaryCapital_RemainsActive()
        {
            CapitalShip ship = CreateShip();
            ship.ManufacturingStatus = ManufacturingStatus.Building;

            CombatUnit unit = new(ship, CombatSide.Attacker, 1);

            Assert.AreEqual(CombatUnitStatus.Active, unit.Status);
        }

        [Test]
        public void Proximity_MovementMagnitudeNearFloatMidpoint_RetainsMovementArithmeticPrecision()
        {
            CombatUnit unit = new(CreateShip(), CombatSide.Attacker, 1)
            {
                CollisionRadius = 0,
                Velocity = new Vector3(0.9999999403953552f, 0.00048828125f, 9.313225746154785e-10f),
            };

            Assert.AreEqual(0.5f, TacticalCombatSpatialIndex.GetProximity(unit));
        }

        /// <summary>
        /// Creates a complete capital ship with a maneuver rating above its baseline turn rate.
        /// </summary>
        /// <returns>The campaign ship used to capture tactical initialization.</returns>
        private static CapitalShip CreateShip() =>
            new()
            {
                InstanceID = "ship",
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                Maneuverability = 5,
            };
    }
}
