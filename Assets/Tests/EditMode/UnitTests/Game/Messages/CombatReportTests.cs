using NUnit.Framework;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Messages
{
    [TestFixture]
    public class CombatReportTests
    {
        [Test]
        public void CaptureBombardment_DestroyedMine_AppearsInDefendingUnits()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                DisplayName = "Mining Facility",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            CombatUnitSnapshot snapshot = new CombatUnitSnapshot(mine);
            snapshot.Destroyed = true;

            BombardmentResult result = new BombardmentResult
            {
                DefendingUnits = { snapshot },
                DestroyedBuildings = { mine },
            };

            CombatReport report = CombatReport.Capture(result, "FNALL1", "Title", "Body");

            Assert.IsNotNull(report);
            Assert.AreEqual(1, report.DefendingUnits.Count);
            Assert.AreEqual(
                CombatReportUnitCategory.ManufacturingFacility,
                report.DefendingUnits[0].Category
            );
            Assert.IsTrue(report.DefendingUnits[0].Destroyed);
        }

        [Test]
        public void CaptureBombardment_DestroyedRefinery_AppearsInDefendingUnits()
        {
            Building refinery = new Building
            {
                InstanceID = "REF1",
                DisplayName = "Refinery",
                BuildingType = BuildingType.Refinery,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            CombatUnitSnapshot snapshot = new CombatUnitSnapshot(refinery);
            snapshot.Destroyed = true;

            BombardmentResult result = new BombardmentResult
            {
                DefendingUnits = { snapshot },
                DestroyedBuildings = { refinery },
            };

            CombatReport report = CombatReport.Capture(result, "FNALL1", "Title", "Body");

            Assert.IsNotNull(report);
            Assert.AreEqual(1, report.DefendingUnits.Count);
            Assert.AreEqual(
                CombatReportUnitCategory.ManufacturingFacility,
                report.DefendingUnits[0].Category
            );
            Assert.IsTrue(report.DefendingUnits[0].Destroyed);
        }

        [Test]
        public void SerializeAndDeserialize_CombatReport_MaintainsState()
        {
            CombatReport report = new CombatReport
            {
                InstanceID = "MSG1",
                Type = MessageType.Conflict,
                ResultType = MessageResultType.Bombardment,
                Title = "Support gained",
                Body = "Support gained",
                BackgroundImageKey = "mission_report",
                OverlayImagePath = "overlay-card",
                EventLocationInstanceID = "PLANET1",
                NavigationTargetInstanceID = "OFFICER1",
                NavigationSecondaryTargetInstanceID = "MISSION1",
                MissionInstanceID = "mission-1",
                CombatType = CombatReportType.SpaceBattle,
                PlanetInstanceID = "PLANET1",
                PlanetName = "Test System",
                Winner = CombatSide.Attacker,
                AttackerOutcome = SpaceCombatSideOutcome.Active,
                DefenderOutcome = SpaceCombatSideOutcome.Destroyed,
                AttackingUnits =
                {
                    new CombatReportUnit
                    {
                        InstanceID = "SHIP1",
                        DisplayName = "Test Cruiser",
                        Category = CombatReportUnitCategory.CapitalShip,
                        WasOperational = true,
                    },
                },
                CreatedTick = 42,
                Read = true,
            };

            string serialized = SerializationHelper.Serialize(report);
            Message deserialized = SerializationHelper.Deserialize<Message>(serialized);

            Assert.IsInstanceOf<CombatReport>(deserialized);
            CombatReport deserializedReport = (CombatReport)deserialized;
            Assert.AreEqual(report.InstanceID, deserializedReport.InstanceID);
            Assert.AreEqual(report.Type, deserializedReport.Type);
            Assert.AreEqual(report.ResultType, deserializedReport.ResultType);
            Assert.AreEqual(report.Title, deserializedReport.Title);
            Assert.AreEqual(report.Body, deserializedReport.Body);
            Assert.AreEqual(report.BackgroundImageKey, deserializedReport.BackgroundImageKey);
            Assert.AreEqual(report.OverlayImagePath, deserializedReport.OverlayImagePath);
            Assert.AreEqual(
                report.EventLocationInstanceID,
                deserializedReport.EventLocationInstanceID
            );
            Assert.AreEqual(
                report.NavigationTargetInstanceID,
                deserializedReport.NavigationTargetInstanceID
            );
            Assert.AreEqual(
                report.NavigationSecondaryTargetInstanceID,
                deserializedReport.NavigationSecondaryTargetInstanceID
            );
            Assert.AreEqual(report.MissionInstanceID, deserializedReport.MissionInstanceID);
            Assert.AreEqual(report.CreatedTick, deserializedReport.CreatedTick);
            Assert.AreEqual(report.Read, deserializedReport.Read);
            Assert.AreEqual(report.CombatType, deserializedReport.CombatType);
            Assert.AreEqual(report.PlanetName, deserializedReport.PlanetName);
            Assert.AreEqual(report.DefenderOutcome, deserializedReport.DefenderOutcome);
            Assert.AreEqual(
                report.AttackingUnits[0].DisplayName,
                deserializedReport.AttackingUnits[0].DisplayName
            );
        }
    }
}
