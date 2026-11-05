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
        public void FullyObservedAtTick_DistinctLatestReport_SurvivesSerializationRoundTrip()
        {
            PlanetSnapshot snapshot = new PlanetSnapshot
            {
                TickCaptured = 100,
                FullyObservedAtTick = 10,
            };

            string xml = SerializationHelper.Serialize(snapshot);
            PlanetSnapshot restored = SerializationHelper.Deserialize<PlanetSnapshot>(xml);

            Assert.AreEqual(100, restored.TickCaptured);
            Assert.AreEqual(10, restored.FullyObservedAtTick);
        }

        [Test]
        public void FullyObservedAtTick_LegacyXmlWithoutField_FallsBackToCapturedTick()
        {
            PlanetSnapshot snapshot = new PlanetSnapshot
            {
                TickCaptured = 42,
                FullyObservedAtTick = 10,
            };
            XDocument document = XDocument.Parse(SerializationHelper.Serialize(snapshot));
            document.Descendants(nameof(PlanetSnapshot.FullyObservedAtTick)).Remove();

            PlanetSnapshot restored = SerializationHelper.Deserialize<PlanetSnapshot>(
                document.ToString()
            );

            Assert.AreEqual(42, restored.TickCaptured);
            Assert.AreEqual(42, restored.FullyObservedAtTick);
        }

        [TestCase("FullObservationTick", -1)]
        [TestCase("FullObservationTick", 0)]
        [TestCase("FullObservationTick", 10)]
        [TestCase(nameof(PlanetSnapshot.FullyObservedAtTick), -1)]
        [TestCase(nameof(PlanetSnapshot.FullyObservedAtTick), 0)]
        [TestCase(nameof(PlanetSnapshot.FullyObservedAtTick), 10)]
        public void FullyObservedAtTick_SavedXmlName_LoadsObservationTick(string name, int tick)
        {
            PlanetSnapshot snapshot = SerializationHelper.Deserialize<PlanetSnapshot>(
                $"<PlanetSnapshot><TickCaptured>100</TickCaptured><{name}>{tick}</{name}></PlanetSnapshot>"
            );

            Assert.AreEqual(tick, snapshot.FullyObservedAtTick);
        }

        [TestCase("OrbitalTickCaptured", -1)]
        [TestCase("OrbitalTickCaptured", 0)]
        [TestCase("OrbitalTickCaptured", 25)]
        [TestCase(nameof(PlanetSnapshot.FleetsObservedAtTick), -1)]
        [TestCase(nameof(PlanetSnapshot.FleetsObservedAtTick), 0)]
        [TestCase(nameof(PlanetSnapshot.FleetsObservedAtTick), 25)]
        public void FleetsObservedAtTick_SavedXmlName_LoadsObservationTick(string name, int tick)
        {
            PlanetSnapshot snapshot = SerializationHelper.Deserialize<PlanetSnapshot>(
                $"<PlanetSnapshot><{name}>{tick}</{name}></PlanetSnapshot>"
            );

            Assert.AreEqual(tick, snapshot.FleetsObservedAtTick);
        }

        [Test]
        public void FleetsObservedAtTick_MissingXmlField_RemainsUnknown()
        {
            PlanetSnapshot snapshot = SerializationHelper.Deserialize<PlanetSnapshot>(
                "<PlanetSnapshot><TickCaptured>100</TickCaptured></PlanetSnapshot>"
            );

            Assert.AreEqual(-1, snapshot.FleetsObservedAtTick);
        }

        [Test]
        public void PlanetSnapshot_DeprecatedObservationNames_WritesCanonicalNames()
        {
            PlanetSnapshot snapshot = SerializationHelper.Deserialize<PlanetSnapshot>(
                "<PlanetSnapshot><TickCaptured>100</TickCaptured><FullObservationTick>10</FullObservationTick><OrbitalTickCaptured>25</OrbitalTickCaptured></PlanetSnapshot>"
            );

            XDocument document = XDocument.Parse(SerializationHelper.Serialize(snapshot));

            Assert.AreEqual(
                "10",
                document.Root.Element(nameof(PlanetSnapshot.FullyObservedAtTick))?.Value
            );
            Assert.AreEqual(
                "25",
                document.Root.Element(nameof(PlanetSnapshot.FleetsObservedAtTick))?.Value
            );
            Assert.IsNull(document.Root.Element("FullObservationTick"));
            Assert.IsNull(document.Root.Element("OrbitalTickCaptured"));
        }
    }
}
