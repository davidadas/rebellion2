using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Tactical;

namespace Rebellion.Tests.Tactical
{
    [TestFixture]
    public sealed class TacticalCombatUnitTests
    {
        [Test]
        public void Constructor_ReusedCombatant_CreatesIndependentSimulationState()
        {
            CombatUnit combatant = CombatUnit.Create(
                new CapitalShip
                {
                    InstanceID = "ship",
                    MaxHullStrength = 100,
                    CurrentHullStrength = 100,
                }
            );
            TacticalCombatUnit first = new(combatant, CombatSide.Attacker, 1);
            TacticalCombatUnit second = new(combatant, CombatSide.Attacker, 1);

            first.Systems.ApplyHullLoss(20);
            first.Systems.ApplyComponentEvent(TacticalShipSystem.Engines, false);

            Assert.AreEqual(100, second.Systems.Hull);
            Assert.AreEqual(0, second.Systems.GetDamage(TacticalShipSystem.Engines));
            Assert.AreEqual(0, ((CapitalShip)combatant.Unit).SystemDamage.Engines);
            Assert.AreNotSame(first.Weapons, second.Weapons);
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
            TacticalCombatUnit unit = new(
                CombatUnit.Create(fighter),
                CombatSide.Attacker,
                1,
                new TacticalCombatUnitDefinition { BackupHyperdrive = backup }
            );
            for (int index = 0; index < damaged; index++)
                unit.Systems.ApplyComponentEvent(TacticalShipSystem.Hyperdrive, false);

            Assert.AreEqual(expected, unit.Systems.HasHyperdrive);
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

            TacticalCombatUnit unit = new(
                CombatUnit.Create(CreateShip()),
                CombatSide.Attacker,
                1,
                definition
            );

            Assert.AreEqual(3, unit.HalfHeight);
            Assert.AreEqual(expectedRadius, unit.CollisionRadius);
            Assert.AreEqual(new Vector3(40, 50, 60), unit.HullDimensions);
        }

        [Test]
        public void Constructor_OrdinaryCapitalWithAuthoredTurnCap_UsesUncappedManeuverRate()
        {
            TacticalCombatUnit unit = new(
                CombatUnit.Create(CreateShip()),
                CombatSide.Attacker,
                1,
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
            TacticalCombatUnit unit = new(
                CombatUnit.Create(CreateShip()),
                CombatSide.Attacker,
                1,
                new TacticalCombatUnitDefinition { PresentationIndex = 21 }
            );

            Assert.AreEqual(0.0523598827f, unit.MaximumTurnRate);
            Assert.AreEqual(0.0523598827f, unit.TurnRate);
        }

        [Test]
        public void Constructor_IncompleteOrdinaryCapital_RemainsActive()
        {
            CapitalShip ship = CreateShip();
            ship.ManufacturingStatus = ManufacturingStatus.Building;

            TacticalCombatUnit unit = new(CombatUnit.Create(ship), CombatSide.Attacker, 1);

            Assert.AreEqual(TacticalCombatUnitStatus.Active, unit.Status);
        }

        [Test]
        public void Proximity_MovementMagnitudeNearFloatMidpoint_RetainsMovementArithmeticPrecision()
        {
            TacticalCombatUnit unit = new(CombatUnit.Create(CreateShip()), CombatSide.Attacker, 1)
            {
                CollisionRadius = 0,
                Velocity = new Vector3(0.9999999403953552f, 0.00048828125f, 9.313225746154785e-10f),
            };

            Assert.AreEqual(0.5f, unit.Proximity);
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
