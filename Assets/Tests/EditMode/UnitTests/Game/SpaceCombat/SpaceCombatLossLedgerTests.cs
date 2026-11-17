using NUnit.Framework;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.SpaceCombat
{
    [TestFixture]
    public class SpaceCombatLossLedgerTests
    {
        [Test]
        public void GetLostValue_ChangedShipOwnerAndValue_ReadsRetainedUnitAtQueryTime()
        {
            CapitalShip ship = new CapitalShip { OwnerInstanceID = "first", UprisingDefense = 7 };
            SpaceCombatLossLedger ledger = new();
            ledger.RecordDestroyedUnit(ship);

            ship.OwnerInstanceID = "second";
            ship.UprisingDefense = 19;

            Assert.AreEqual(0, ledger.GetLostValue("first"));
            Assert.AreEqual(19, ledger.GetLostValue("second"));
        }

        [Test]
        public void GetLostValue_AcceptedFighterLoss_UsesWholeAuthoredSquadronValue()
        {
            Starfighter fighter = new Starfighter
            {
                OwnerInstanceID = "first",
                UprisingDefense = 13,
                MaxSquadronSize = 24,
                CurrentSquadronSize = 0,
            };
            SpaceCombatLossLedger ledger = new();

            ledger.RecordDestroyedUnit(fighter);

            Assert.AreEqual(13, ledger.GetLostValue("first"));
        }

        [Test]
        public void RecordDestroyedUnit_AcceptedConstructionStation_RetainsDirectLoss()
        {
            CapitalShip station = new CapitalShip
            {
                OwnerInstanceID = "first",
                CanDestroyPlanets = true,
                ManufacturingStatus = ManufacturingStatus.Building,
                UprisingDefense = 250,
            };
            SpaceCombatLossLedger ledger = new();

            ledger.RecordDestroyedUnit(station);

            Assert.AreEqual(250, ledger.GetLostValue("first"));
        }

        [TestCase(ManufacturingStatus.Building, false, 0)]
        [TestCase(ManufacturingStatus.Delivering, false, 0)]
        [TestCase(ManufacturingStatus.Delivering, true, 0)]
        [TestCase(ManufacturingStatus.Complete, true, 0)]
        [TestCase(ManufacturingStatus.Complete, false, 17)]
        public void RecordDestroyedUnit_CarriedRegiment_RequiresCompletedStationaryAdmission(
            ManufacturingStatus status,
            bool moving,
            int expectedValue
        )
        {
            Regiment regiment = new Regiment
            {
                OwnerInstanceID = "first",
                UprisingDefense = 17,
                ManufacturingStatus = status,
                Movement = moving ? new MovementState() : null,
            };
            SpaceCombatLossLedger ledger = new();

            ledger.RecordDestroyedUnit(regiment);

            Assert.AreEqual(expectedValue, ledger.GetLostValue("first"));
        }

        [Test]
        public void GetLostValue_CarriedRegimentChangesAfterAdmission_RetainsAdmissionAndReadsCurrentValue()
        {
            Regiment regiment = new Regiment
            {
                OwnerInstanceID = "first",
                UprisingDefense = 17,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            SpaceCombatLossLedger ledger = new();
            ledger.RecordDestroyedUnit(regiment);

            regiment.ManufacturingStatus = ManufacturingStatus.Delivering;
            regiment.Movement = new MovementState();
            regiment.UprisingDefense = 23;

            Assert.AreEqual(23, ledger.GetLostValue("first"));
        }

        [Test]
        public void RecordDestroyedUnit_IncompleteRegimentFinishesAfterCarrierLoss_DoesNotAddALoss()
        {
            Regiment regiment = new Regiment
            {
                OwnerInstanceID = "first",
                UprisingDefense = 17,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            SpaceCombatLossLedger ledger = new();
            ledger.RecordDestroyedUnit(regiment);

            regiment.ManufacturingStatus = ManufacturingStatus.Complete;

            Assert.AreEqual(0, ledger.GetLostValue("first"));
        }

        [Test]
        public void RecordDestroyedUnit_SpecialForces_ExcludesTheUnsupportedFamily()
        {
            SpecialForces unit = new SpecialForces
            {
                OwnerInstanceID = "first",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            SpaceCombatLossLedger ledger = new();

            ledger.RecordDestroyedUnit(unit);

            Assert.AreEqual(0, ledger.GetLostValue("first"));
        }

        [Test]
        public void GetLostValue_NeutralLoss_DoesNotEnterConfiguredFactionTotals()
        {
            SpaceCombatLossLedger ledger = new();
            ledger.RecordDestroyedUnit(new CapitalShip { UprisingDefense = 17 });

            Assert.AreEqual(0, ledger.GetLostValue("first"));
            Assert.AreEqual(0, ledger.GetLostValue("second"));
            Assert.AreEqual(17, ledger.GetLostValue(null));
        }

        [Test]
        public void GetLostValue_TotalExceedsSignedRange_WrapsToLow32Bits()
        {
            SpaceCombatLossLedger ledger = new();
            ledger.RecordDestroyedUnit(
                new CapitalShip { OwnerInstanceID = "first", UprisingDefense = int.MaxValue }
            );
            ledger.RecordDestroyedUnit(
                new Starfighter { OwnerInstanceID = "first", UprisingDefense = 1 }
            );

            Assert.AreEqual(int.MinValue, ledger.GetLostValue("first"));
        }
    }
}
