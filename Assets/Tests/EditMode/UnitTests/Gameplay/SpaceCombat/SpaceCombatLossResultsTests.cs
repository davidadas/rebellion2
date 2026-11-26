using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class SpaceCombatLossResultsTests
    {
        private static void RecordDestruction(List<GameResult> events, ISceneNode unit) =>
            events.Add(SpaceCombatCommands.CreateCombatDestructionResult(unit, null, 0));

        private static int GetLostValue(List<GameResult> events, string owner) =>
            SpaceCombatObserver.CaptureLostMilitaryValues(events, owner, null).FirstSide;

        [Test]
        public void GetLostValue_ChangedShipOwnerAndValue_ReadsRetainedUnitAtQueryTime()
        {
            CapitalShip ship = new CapitalShip { OwnerInstanceID = "first", UprisingDefense = 7 };
            List<GameResult> events = new();
            RecordDestruction(events, ship);

            ship.OwnerInstanceID = "second";
            ship.UprisingDefense = 19;

            Assert.AreEqual(0, GetLostValue(events, "first"));
            Assert.AreEqual(19, GetLostValue(events, "second"));
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
            List<GameResult> events = new();

            RecordDestruction(events, fighter);

            Assert.AreEqual(13, GetLostValue(events, "first"));
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
            List<GameResult> events = new();

            RecordDestruction(events, station);

            Assert.AreEqual(250, GetLostValue(events, "first"));
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
            List<GameResult> events = new();

            RecordDestruction(events, regiment);

            Assert.AreEqual(expectedValue, GetLostValue(events, "first"));
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
            List<GameResult> events = new();
            RecordDestruction(events, regiment);

            regiment.ManufacturingStatus = ManufacturingStatus.Delivering;
            regiment.Movement = new MovementState();
            regiment.UprisingDefense = 23;

            Assert.AreEqual(23, GetLostValue(events, "first"));
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
            List<GameResult> events = new();
            RecordDestruction(events, regiment);

            regiment.ManufacturingStatus = ManufacturingStatus.Complete;

            Assert.AreEqual(0, GetLostValue(events, "first"));
        }

        [Test]
        public void RecordDestroyedUnit_SpecialForces_ExcludesTheUnsupportedFamily()
        {
            SpecialForces unit = new SpecialForces
            {
                OwnerInstanceID = "first",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            List<GameResult> events = new();

            RecordDestruction(events, unit);

            Assert.AreEqual(0, GetLostValue(events, "first"));
        }

        [Test]
        public void GetLostValue_NeutralLoss_DoesNotEnterConfiguredFactionTotals()
        {
            List<GameResult> events = new();
            RecordDestruction(events, new CapitalShip { UprisingDefense = 17 });

            Assert.AreEqual(0, GetLostValue(events, "first"));
            Assert.AreEqual(0, GetLostValue(events, "second"));
            Assert.AreEqual(17, GetLostValue(events, null));
        }

        [Test]
        public void GetLostValue_TotalExceedsSignedRange_WrapsToLow32Bits()
        {
            List<GameResult> events = new();
            RecordDestruction(
                events,
                new CapitalShip { OwnerInstanceID = "first", UprisingDefense = int.MaxValue }
            );
            RecordDestruction(
                events,
                new Starfighter { OwnerInstanceID = "first", UprisingDefense = 1 }
            );

            Assert.AreEqual(int.MinValue, GetLostValue(events, "first"));
        }
    }
}
