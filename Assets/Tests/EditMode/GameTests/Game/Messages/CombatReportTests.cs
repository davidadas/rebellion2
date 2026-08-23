using NUnit.Framework;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;

namespace Rebellion.Tests.Game.Messages
{
    [TestFixture]
    public class CombatReportTests
    {
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
