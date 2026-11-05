using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Tests.Helpers;

namespace Rebellion.Tests.Game.FogOfWar
{
    [TestFixture]
    public class PlanetSnapshotTests : FogOfWarTestBase
    {
        [Test]
        public void PlanetSnapshot_Default_MissionParticipantIntelligenceSurvivesSerializationRoundTrip()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            vader.DisplayName = "Darth Vader";
            vader.DisplayImagePath = "officers/vader";
            _game.AttachNode(vader, _coruscant);

            Mission empireMission = CreateMission("M1", _empire, _coruscant);
            _game.AttachNode(empireMission, _coruscant);
            _game.MoveNode(vader, empireMission);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots[_coreSector.InstanceID].Planets[
                _coruscant.InstanceID
            ];
            string xml = SerializationHelper.Serialize(snapshot);
            PlanetSnapshot restored = SerializationHelper.Deserialize<PlanetSnapshot>(xml);
            Officer restoredParticipant =
                restored.Missions.Single().GetMainParticipants(includeDisabled: true).Single()
                as Officer;

            Assert.IsNotNull(restoredParticipant);
            Assert.AreEqual(vader.InstanceID, restoredParticipant.InstanceID);
            Assert.AreEqual("Darth Vader", restoredParticipant.DisplayName);
            Assert.AreEqual("officers/vader", restoredParticipant.DisplayImagePath);
        }

        [Test]
        public void RevealedCategories_CompositeValue_SurvivesSerializationRoundTrip()
        {
            PlanetSnapshot snapshot = new PlanetSnapshot
            {
                RevealedCategories = PlanetIntelligenceCategory.PlanetMilitaryProduction,
            };

            string xml = SerializationHelper.Serialize(snapshot);
            PlanetSnapshot restored = SerializationHelper.Deserialize<PlanetSnapshot>(xml);

            Assert.AreEqual(
                PlanetIntelligenceCategory.PlanetMilitaryProduction,
                restored.RevealedCategories
            );
            Assert.IsTrue(
                restored.RevealedCategories.HasFlag(PlanetIntelligenceCategory.CapitalShips)
            );
            Assert.IsTrue(
                restored.RevealedCategories.HasFlag(PlanetIntelligenceCategory.Manufacturing)
            );
            Assert.IsFalse(
                restored.RevealedCategories.HasFlag(PlanetIntelligenceCategory.Officers)
            );
        }

        [TestCase(
            PlanetIntelligenceCategory.GroundForces,
            PlanetIntelligenceCategory.Regiments,
            true
        )]
        [TestCase(
            PlanetIntelligenceCategory.GroundForces,
            PlanetIntelligenceCategory.SpecialForces,
            true
        )]
        [TestCase(
            PlanetIntelligenceCategory.Regiments | PlanetIntelligenceCategory.SpecialForces,
            PlanetIntelligenceCategory.GroundForces,
            true
        )]
        [TestCase(
            PlanetIntelligenceCategory.Regiments,
            PlanetIntelligenceCategory.GroundForces,
            false
        )]
        [TestCase(
            PlanetIntelligenceCategory.SpecialForces,
            PlanetIntelligenceCategory.GroundForces,
            false
        )]
        [TestCase(
            PlanetIntelligenceCategory.Buildings,
            PlanetIntelligenceCategory.PlanetaryDefenses,
            true
        )]
        [TestCase(
            PlanetIntelligenceCategory.Buildings,
            PlanetIntelligenceCategory.ProductionFacilities,
            true
        )]
        [TestCase(
            PlanetIntelligenceCategory.PlanetaryDefenses
                | PlanetIntelligenceCategory.ProductionFacilities,
            PlanetIntelligenceCategory.Buildings,
            false
        )]
        [TestCase(
            PlanetIntelligenceCategory.Planet,
            PlanetIntelligenceCategory.PlanetControl,
            true
        )]
        [TestCase(
            PlanetIntelligenceCategory.Planet,
            PlanetIntelligenceCategory.PlanetResources,
            true
        )]
        [TestCase(
            PlanetIntelligenceCategory.PlanetControl | PlanetIntelligenceCategory.PlanetResources,
            PlanetIntelligenceCategory.Planet,
            false
        )]
        [TestCase(
            PlanetIntelligenceCategory.PlanetMilitaryProduction,
            PlanetIntelligenceCategory.Regiments,
            true
        )]
        [TestCase(
            PlanetIntelligenceCategory.PlanetMilitaryProduction,
            PlanetIntelligenceCategory.SpecialForces,
            false
        )]
        [TestCase(
            PlanetIntelligenceCategory.GroundForces,
            PlanetIntelligenceCategory.Regiments | PlanetIntelligenceCategory.Officers,
            false
        )]
        public void HasIntelligence_CategoryCoverage_RecognizesOnlyCoveredInformation(
            PlanetIntelligenceCategory known,
            PlanetIntelligenceCategory requested,
            bool expected
        )
        {
            PlanetSnapshot snapshot = new PlanetSnapshot { RevealedCategories = known };
            string before = SerializationHelper.Serialize(snapshot);

            bool actual = snapshot.HasIntelligence(requested);

            Assert.AreEqual(expected, actual);
            Assert.AreEqual(before, SerializationHelper.Serialize(snapshot));
        }

        [TestCase("GroundForces", 8, PlanetIntelligenceCategory.Regiments)]
        [TestCase("8", 8, PlanetIntelligenceCategory.SpecialForces)]
        [TestCase("Regiments, SpecialForces", 3072, PlanetIntelligenceCategory.GroundForces)]
        [TestCase("3072", 3072, PlanetIntelligenceCategory.GroundForces)]
        [TestCase("Buildings", 16, PlanetIntelligenceCategory.ProductionFacilities)]
        [TestCase("Planet", 1, PlanetIntelligenceCategory.PlanetResources)]
        [TestCase("PlanetMilitaryProduction", 1175, PlanetIntelligenceCategory.PlanetControl)]
        public void HasIntelligence_SavedNamedOrNumericMask_PreservesStoredValue(
            string saved,
            int expectedMask,
            PlanetIntelligenceCategory requested
        )
        {
            PlanetSnapshot snapshot = SerializationHelper.Deserialize<PlanetSnapshot>(
                $"<PlanetSnapshot><RevealedCategories>{saved}</RevealedCategories></PlanetSnapshot>"
            );

            Assert.IsTrue(snapshot.HasIntelligence(requested));

            Assert.AreEqual(expectedMask, (int)snapshot.RevealedCategories);
            PlanetSnapshot restored = SerializationHelper.Deserialize<PlanetSnapshot>(
                SerializationHelper.Serialize(snapshot)
            );
            Assert.AreEqual(expectedMask, (int)restored.RevealedCategories);
        }

        [Test]
        public void FullObservationTick_DistinctLatestReport_SurvivesSerializationRoundTrip()
        {
            PlanetSnapshot snapshot = new PlanetSnapshot
            {
                TickCaptured = 100,
                FullObservationTick = 10,
            };

            string xml = SerializationHelper.Serialize(snapshot);
            PlanetSnapshot restored = SerializationHelper.Deserialize<PlanetSnapshot>(xml);

            Assert.AreEqual(100, restored.TickCaptured);
            Assert.AreEqual(10, restored.FullObservationTick);
        }

        [Test]
        public void FullObservationTick_LegacyXmlWithoutField_FallsBackToCapturedTick()
        {
            PlanetSnapshot snapshot = new PlanetSnapshot
            {
                TickCaptured = 42,
                FullObservationTick = 10,
            };
            XDocument document = XDocument.Parse(SerializationHelper.Serialize(snapshot));
            document.Descendants(nameof(PlanetSnapshot.FullObservationTick)).Remove();

            PlanetSnapshot restored = SerializationHelper.Deserialize<PlanetSnapshot>(
                document.ToString()
            );

            Assert.AreEqual(42, restored.TickCaptured);
            Assert.AreEqual(42, restored.FullObservationTick);
        }
    }
}
