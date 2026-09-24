using System.Linq;
using NUnit.Framework;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Tests.Helpers;

namespace Rebellion.Tests.Game.FogOfWar
{
    [TestFixture]
    public class FogOfWarRecorderTests : FogOfWarTestBase
    {
        [Test]
        public void RecordIntelligenceSnapshot_CapitalShips_DoesNotLeakShipCargo()
        {
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "STAR_DESTROYER");
            ship.StarfighterCapacity = 1;
            _game.AttachNode(CreateOfficer("VADER", _empire), ship);
            _game.AttachNode(
                new Starfighter
                {
                    InstanceID = "TIE_SQUADRON",
                    OwnerInstanceID = _empire.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                ship
            );

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.CapitalShips
            );

            CapitalShip knownShip = _alliance
                .Fog.Snapshots["CORE_SECTOR"]
                .Planets["CORUSCANT"]
                .Fleets.Single()
                .GetChildren<CapitalShip>()
                .Single();
            Assert.AreEqual("STAR_DESTROYER", knownShip.InstanceID);
            Assert.IsEmpty(knownShip.GetChildren<Officer>());
            Assert.IsEmpty(knownShip.GetChildren<Starfighter>());

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                43,
                PlanetIntelligenceCategory.Starfighters
            );
            PlanetSnapshot updatedSnapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets[
                "CORUSCANT"
            ];
            Assert.AreEqual("TIE_SQUADRON", updatedSnapshot.Starfighters.Single().InstanceID);
            Assert.AreEqual(
                "TIE_SQUADRON",
                updatedSnapshot
                    .Fleets.Single()
                    .GetChildren<CapitalShip>()
                    .Single()
                    .GetChildren<Starfighter>()
                    .Single()
                    .InstanceID
            );
        }

        [Test]
        public void RecordIntelligenceSnapshot_EnemyFleet_DoesNotRetainWaypoints()
        {
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            fleet.Waypoints.Add(_tatooine.InstanceID);
            _game.AttachNode(fleet, _coruscant);
            AddCapitalShip(fleet, _empire, "STAR_DESTROYER");

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.CapitalShips
            );

            Fleet knownFleet = _alliance
                .Fog.Snapshots["CORE_SECTOR"]
                .Planets["CORUSCANT"]
                .Fleets.Single();
            Assert.IsEmpty(knownFleet.Waypoints);
        }

        [Test]
        public void RecordEspionageSnapshot_MissionCompletion_PreservesParticipantIntelligence()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            vader.DisplayName = "Darth Vader";
            vader.DisplayImagePath = "officers/vader";
            _game.AttachNode(vader, _coruscant);
            Officer tarkin = CreateOfficer("TARKIN", _empire);
            tarkin.DisplayName = "Grand Moff Tarkin";
            _game.AttachNode(tarkin, _coruscant);

            Mission empireMission = CreateMission("M1", _empire, _coruscant);
            _game.AttachNode(empireMission, _coruscant);
            _game.MoveNode(vader, empireMission);
            empireMission.AddDecoyParticipant(tarkin);
            _game.MoveNode(tarkin, empireMission);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            _game.MoveNode(vader, _coruscant);
            _game.MoveNode(tarkin, _coruscant);
            vader.DisplayName = "Changed live officer";
            vader.DisplayImagePath = "officers/changed";
            tarkin.DisplayName = "Changed live decoy";

            Mission recordedMission = _alliance
                .Fog.Snapshots[_coreSector.InstanceID]
                .Planets[_coruscant.InstanceID]
                .Missions.Single();
            Officer recordedParticipant =
                recordedMission.GetMainParticipants(includeDisabled: true).Single() as Officer;
            Officer recordedDecoy =
                recordedMission.GetDecoyParticipants(includeDisabled: true).Single() as Officer;

            Assert.IsNotNull(recordedParticipant);
            Assert.AreNotSame(vader, recordedParticipant);
            Assert.AreEqual(vader.InstanceID, recordedParticipant.InstanceID);
            Assert.AreEqual("Darth Vader", recordedParticipant.DisplayName);
            Assert.AreEqual("officers/vader", recordedParticipant.DisplayImagePath);
            Assert.IsNotNull(recordedDecoy);
            Assert.AreNotSame(tarkin, recordedDecoy);
            Assert.AreEqual(tarkin.InstanceID, recordedDecoy.InstanceID);
            Assert.AreEqual("Grand Moff Tarkin", recordedDecoy.DisplayName);
        }

        [Test]
        public void RecordEspionageSnapshot_DisabledMissionParticipant_PreservesParticipant()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);
            Mission empireMission = CreateMission("M1", _empire, _coruscant);
            _game.AttachNode(empireMission, _coruscant);
            _game.MoveNode(vader, empireMission);
            vader.IsEnabled = false;

            new FogOfWarRecorder().RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            Mission recordedMission = _alliance
                .Fog.Snapshots[_coreSector.InstanceID]
                .Planets[_coruscant.InstanceID]
                .Missions.Single();
            Officer recordedParticipant =
                recordedMission.GetMainParticipants(includeDisabled: true).Single() as Officer;

            Assert.IsNotNull(recordedParticipant);
            Assert.AreEqual(vader.InstanceID, recordedParticipant.InstanceID);
            Assert.IsFalse(recordedParticipant.IsEnabled);
        }
    }
}
