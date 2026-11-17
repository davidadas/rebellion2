using System;
using NUnit.Framework;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.SpaceCombat
{
    [TestFixture]
    public class SpaceCombatMilitaryRosterTests
    {
        [Test]
        public void CaptureValues_EligibleFamilies_UsesFullValuesForBothOwners()
        {
            SpaceCombatMilitaryRoster roster = new();
            roster.Register(
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
            roster.Register(
                new CapitalShip
                {
                    InstanceID = "station",
                    OwnerInstanceID = "first",
                    CanDestroyPlanets = true,
                    UprisingDefense = 250,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            roster.Register(
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
            roster.Register(
                new Regiment
                {
                    InstanceID = "regiment",
                    OwnerInstanceID = "second",
                    UprisingDefense = 13,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    Movement = new MovementState(),
                }
            );

            Assert.AreEqual((268, 13), roster.CaptureValues("first", "second"));
        }

        [Test]
        public void CaptureValues_BuildingUnits_ExcludesAllMilitaryFamilies()
        {
            SpaceCombatMilitaryRoster roster = new();
            roster.Register(
                new CapitalShip
                {
                    InstanceID = "ship",
                    OwnerInstanceID = "first",
                    UprisingDefense = 7,
                    ManufacturingStatus = ManufacturingStatus.Building,
                }
            );
            roster.Register(
                new Starfighter
                {
                    InstanceID = "fighter",
                    OwnerInstanceID = "second",
                    UprisingDefense = 11,
                    ManufacturingStatus = ManufacturingStatus.Building,
                }
            );
            roster.Register(
                new Regiment
                {
                    InstanceID = "regiment",
                    OwnerInstanceID = "first",
                    UprisingDefense = 13,
                    ManufacturingStatus = ManufacturingStatus.Building,
                }
            );

            Assert.AreEqual((0, 0), roster.CaptureValues("first", "second"));
        }

        [TestCase(ManufacturingStatus.Building, 0)]
        [TestCase(ManufacturingStatus.Delivering, 17)]
        [TestCase(ManufacturingStatus.Complete, 17)]
        public void CaptureValues_RegimentStatusChangesInPlace_ReadsCurrentAdmission(
            ManufacturingStatus status,
            int expectedValue
        )
        {
            SpaceCombatMilitaryRoster roster = new();
            Regiment regiment = new Regiment
            {
                InstanceID = "regiment",
                OwnerInstanceID = "first",
                UprisingDefense = 17,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            roster.Register(regiment);

            regiment.ManufacturingStatus = status;

            Assert.AreEqual((expectedValue, 0), roster.CaptureValues("first", "second"));
        }

        [Test]
        public void CaptureValues_OwnerAndValueChangeInPlace_ReadsCurrentFacts()
        {
            SpaceCombatMilitaryRoster roster = new();
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "first",
                UprisingDefense = 7,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            roster.Register(ship);
            Assert.AreEqual((7, 0), roster.CaptureValues("first", "second"));

            ship.OwnerInstanceID = "second";
            ship.UprisingDefense = 19;

            Assert.AreEqual((0, 19), roster.CaptureValues("first", "second"));
        }

        [Test]
        public void Register_NonMilitaryFamilies_DoNotEnterTotals()
        {
            SpaceCombatMilitaryRoster roster = new();
            roster.Register(new Fleet { InstanceID = "fleet", OwnerInstanceID = "first" });
            roster.Register(
                new SpecialForces { InstanceID = "special-forces", OwnerInstanceID = "first" }
            );
            roster.Register(new Officer { InstanceID = "officer", OwnerInstanceID = "second" });
            roster.Register(new Building { InstanceID = "building", OwnerInstanceID = "second" });

            Assert.AreEqual((0, 0), roster.CaptureValues("first", "second"));
        }

        [Test]
        public void Register_DuplicateRegistryKey_LeavesExistingContributionIntact()
        {
            SpaceCombatMilitaryRoster roster = new();
            roster.Register(
                new Regiment
                {
                    InstanceID = "unit",
                    OwnerInstanceID = "first",
                    UprisingDefense = 7,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );

            Assert.Throws<ArgumentException>(() =>
                roster.Register(new CapitalShip { InstanceID = "unit", UprisingDefense = 100 })
            );

            Assert.AreEqual((7, 0), roster.CaptureValues("first", "second"));
        }

        [Test]
        public void Unregister_UnitIdentifierChanged_RemovesOriginalRegistryKey()
        {
            SpaceCombatMilitaryRoster roster = new();
            Regiment regiment = new Regiment
            {
                InstanceID = "registered-key",
                OwnerInstanceID = "first",
                UprisingDefense = 7,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            roster.Register(regiment);
            regiment.InstanceID = "changed-identifier";

            roster.Unregister("registered-key");

            Assert.AreEqual((0, 0), roster.CaptureValues("first", "second"));
        }

        [Test]
        public void Clear_ReplacementUsesSameRegistryKey_DropsOutgoingReference()
        {
            SpaceCombatMilitaryRoster roster = new();
            roster.Register(
                new Regiment
                {
                    InstanceID = "unit",
                    OwnerInstanceID = "first",
                    UprisingDefense = 7,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            roster.Clear();

            roster.Register(
                new Regiment
                {
                    InstanceID = "unit",
                    OwnerInstanceID = "second",
                    UprisingDefense = 19,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );

            Assert.AreEqual((0, 19), roster.CaptureValues("first", "second"));
        }

        [Test]
        public void CaptureValues_TotalExceedsSignedRange_WrapsToLow32Bits()
        {
            SpaceCombatMilitaryRoster roster = new();
            roster.Register(
                new Regiment
                {
                    InstanceID = "first-unit",
                    OwnerInstanceID = "first",
                    UprisingDefense = int.MaxValue,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            roster.Register(
                new Regiment
                {
                    InstanceID = "second-unit",
                    OwnerInstanceID = "first",
                    UprisingDefense = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );

            Assert.AreEqual((int.MinValue, 0), roster.CaptureValues("first", "second"));
        }

        [Test]
        public void CaptureValues_NeutralOwner_DoesNotEnterConfiguredFactionTotals()
        {
            SpaceCombatMilitaryRoster roster = new();
            roster.Register(
                new Regiment
                {
                    InstanceID = "unit",
                    UprisingDefense = 17,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );

            Assert.AreEqual((0, 0), roster.CaptureValues("first", "second"));
            Assert.AreEqual((17, 0), roster.CaptureValues(null, "first"));
        }

        [Test]
        public void CaptureValues_RepeatedQueries_AllocateNoManagedMemory()
        {
            SpaceCombatMilitaryRoster roster = new();
            roster.Register(
                new Regiment
                {
                    InstanceID = "unit",
                    OwnerInstanceID = "first",
                    UprisingDefense = 17,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            for (int index = 0; index < 32; index++)
                roster.CaptureValues("first", "second");
            long before = GC.GetAllocatedBytesForCurrentThread();
            int total = 0;

            for (int index = 0; index < 1024; index++)
                total += roster.CaptureValues("first", "second").FirstSide;

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(17408, total);
            Assert.AreEqual(0, allocated);
        }
    }
}
