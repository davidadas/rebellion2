using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class SpaceCombatMilitaryValuesTests
    {
        private static void Register(GameRoot game, ISceneNode unit) =>
            game.NodesByInstanceID.Add(unit.InstanceID, unit);

        [Test]
        public void CaptureValues_EligibleFamilies_UsesFullValuesForBothOwners()
        {
            GameRoot game = new();
            Register(
                game,
                new CapitalShip
                {
                    InstanceID = "ship",
                    OwnerInstanceID = "first",
                    UprisingDefense = 7,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    IsEnabled = false,
                    MaxHullStrength = 100,
                    CurrentHullStrength = 1,
                }
            );
            Register(
                game,
                new CapitalShip
                {
                    InstanceID = "station",
                    OwnerInstanceID = "first",
                    CanDestroyPlanets = true,
                    UprisingDefense = 250,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            Register(
                game,
                new Starfighter
                {
                    InstanceID = "fighter",
                    OwnerInstanceID = "first",
                    UprisingDefense = 11,
                    ManufacturingStatus = ManufacturingStatus.Delivering,
                    Movement = new MovementState(),
                    MaxSquadronSize = 24,
                    CurrentSquadronSize = 1,
                }
            );
            Register(
                game,
                new Regiment
                {
                    InstanceID = "regiment",
                    OwnerInstanceID = "second",
                    UprisingDefense = 13,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    Movement = new MovementState(),
                }
            );

            Assert.AreEqual(
                (268, 13),
                SpaceCombatAftermath.CaptureMilitaryValues(game, "first", "second")
            );
        }

        [Test]
        public void CaptureValues_BuildingUnits_ExcludesAllMilitaryFamilies()
        {
            GameRoot game = new();
            Register(
                game,
                new CapitalShip
                {
                    InstanceID = "ship",
                    OwnerInstanceID = "first",
                    UprisingDefense = 7,
                    ManufacturingStatus = ManufacturingStatus.Building,
                }
            );
            Register(
                game,
                new Starfighter
                {
                    InstanceID = "fighter",
                    OwnerInstanceID = "second",
                    UprisingDefense = 11,
                    ManufacturingStatus = ManufacturingStatus.Building,
                }
            );
            Register(
                game,
                new Regiment
                {
                    InstanceID = "regiment",
                    OwnerInstanceID = "first",
                    UprisingDefense = 13,
                    ManufacturingStatus = ManufacturingStatus.Building,
                }
            );

            Assert.AreEqual(
                (0, 0),
                SpaceCombatAftermath.CaptureMilitaryValues(game, "first", "second")
            );
        }

        [TestCase(ManufacturingStatus.Building, 0)]
        [TestCase(ManufacturingStatus.Delivering, 17)]
        [TestCase(ManufacturingStatus.Complete, 17)]
        public void CaptureValues_RegimentStatusChangesInPlace_ReadsCurrentAdmission(
            ManufacturingStatus status,
            int expectedValue
        )
        {
            GameRoot game = new();
            Regiment regiment = new Regiment
            {
                InstanceID = "regiment",
                OwnerInstanceID = "first",
                UprisingDefense = 17,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            Register(game, regiment);

            regiment.ManufacturingStatus = status;

            Assert.AreEqual(
                (expectedValue, 0),
                SpaceCombatAftermath.CaptureMilitaryValues(game, "first", "second")
            );
        }

        [Test]
        public void CaptureValues_OwnerAndValueChangeInPlace_ReadsCurrentFacts()
        {
            GameRoot game = new();
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "first",
                UprisingDefense = 7,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Register(game, ship);
            Assert.AreEqual(
                (7, 0),
                SpaceCombatAftermath.CaptureMilitaryValues(game, "first", "second")
            );

            ship.OwnerInstanceID = "second";
            ship.UprisingDefense = 19;

            Assert.AreEqual(
                (0, 19),
                SpaceCombatAftermath.CaptureMilitaryValues(game, "first", "second")
            );
        }

        [Test]
        public void Register_NonMilitaryFamilies_DoNotEnterTotals()
        {
            GameRoot game = new();
            Register(game, new Fleet { InstanceID = "fleet", OwnerInstanceID = "first" });
            Register(
                game,
                new SpecialForces { InstanceID = "special-forces", OwnerInstanceID = "first" }
            );
            Register(game, new Officer { InstanceID = "officer", OwnerInstanceID = "second" });
            Register(game, new Building { InstanceID = "building", OwnerInstanceID = "second" });

            Assert.AreEqual(
                (0, 0),
                SpaceCombatAftermath.CaptureMilitaryValues(game, "first", "second")
            );
        }

        [Test]
        public void Register_DuplicateRegistryKey_LeavesExistingContributionIntact()
        {
            GameRoot game = new();
            Register(
                game,
                new Regiment
                {
                    InstanceID = "unit",
                    OwnerInstanceID = "first",
                    UprisingDefense = 7,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );

            Assert.Throws<ArgumentException>(() =>
                Register(game, new CapitalShip { InstanceID = "unit", UprisingDefense = 100 })
            );

            Assert.AreEqual(
                (7, 0),
                SpaceCombatAftermath.CaptureMilitaryValues(game, "first", "second")
            );
        }

        [Test]
        public void Unregister_UnitIdentifierChanged_RemovesOriginalRegistryKey()
        {
            GameRoot game = new();
            Regiment regiment = new Regiment
            {
                InstanceID = "registered-key",
                OwnerInstanceID = "first",
                UprisingDefense = 7,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Register(game, regiment);
            regiment.InstanceID = "changed-identifier";

            game.NodesByInstanceID.Remove("registered-key");

            Assert.AreEqual(
                (0, 0),
                SpaceCombatAftermath.CaptureMilitaryValues(game, "first", "second")
            );
        }

        [Test]
        public void Clear_ReplacementUsesSameRegistryKey_DropsOutgoingReference()
        {
            GameRoot game = new();
            Register(
                game,
                new Regiment
                {
                    InstanceID = "unit",
                    OwnerInstanceID = "first",
                    UprisingDefense = 7,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            game.NodesByInstanceID.Clear();

            Register(
                game,
                new Regiment
                {
                    InstanceID = "unit",
                    OwnerInstanceID = "second",
                    UprisingDefense = 19,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );

            Assert.AreEqual(
                (0, 19),
                SpaceCombatAftermath.CaptureMilitaryValues(game, "first", "second")
            );
        }

        [Test]
        public void CaptureValues_TotalExceedsSignedRange_WrapsToLow32Bits()
        {
            GameRoot game = new();
            Register(
                game,
                new Regiment
                {
                    InstanceID = "first-unit",
                    OwnerInstanceID = "first",
                    UprisingDefense = int.MaxValue,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            Register(
                game,
                new Regiment
                {
                    InstanceID = "second-unit",
                    OwnerInstanceID = "first",
                    UprisingDefense = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );

            Assert.AreEqual(
                (int.MinValue, 0),
                SpaceCombatAftermath.CaptureMilitaryValues(game, "first", "second")
            );
        }

        [Test]
        public void CaptureValues_NeutralOwner_DoesNotEnterConfiguredFactionTotals()
        {
            GameRoot game = new();
            Register(
                game,
                new Regiment
                {
                    InstanceID = "unit",
                    UprisingDefense = 17,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );

            Assert.AreEqual(
                (0, 0),
                SpaceCombatAftermath.CaptureMilitaryValues(game, "first", "second")
            );
            Assert.AreEqual(
                (17, 0),
                SpaceCombatAftermath.CaptureMilitaryValues(game, null, "first")
            );
        }

        [Test]
        public void CaptureValues_RepeatedQueries_AllocateNoManagedMemory()
        {
            GameRoot game = new();
            Register(
                game,
                new Regiment
                {
                    InstanceID = "unit",
                    OwnerInstanceID = "first",
                    UprisingDefense = 17,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            for (int index = 0; index < 32; index++)
                SpaceCombatAftermath.CaptureMilitaryValues(game, "first", "second");
            long before = GC.GetAllocatedBytesForCurrentThread();
            int total = 0;

            for (int index = 0; index < 1024; index++)
                total += SpaceCombatAftermath
                    .CaptureMilitaryValues(game, "first", "second")
                    .FirstSide;

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(17408, total);
            Assert.AreEqual(0, allocated);
        }
    }
}
