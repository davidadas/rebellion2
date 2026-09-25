using System.Linq;
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
    }
}
