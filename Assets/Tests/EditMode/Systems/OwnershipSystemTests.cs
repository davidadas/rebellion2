using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Game;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class OwnershipSystemTests
    {
        private class UncancelableMission : StubMission
        {
            public UncancelableMission(string ownerInstanceId, string targetInstanceId)
                : base(ownerInstanceId, targetInstanceId) { }

            public override bool CanceledOnOwnershipChange => false;
        }

        private GameRoot game;
        private Faction rebels;
        private Faction empire;
        private Planet targetPlanet;
        private Planet empirePlanet;
        private MovementSystem movementSystem;
        private OwnershipSystem ownershipSystem;

        [SetUp]
        public void SetUp()
        {
            game = new GameRoot(new GameConfig());

            rebels = new Faction { InstanceID = "rebels", DisplayName = "Rebels" };
            empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            game.Factions.Add(rebels);
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            // Planet being transferred — starts neutral
            targetPlanet = new Planet
            {
                InstanceID = "target",
                DisplayName = "Target",
                OwnerInstanceID = null,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(targetPlanet, system);

            // Empire's home planet — fallback destination for evicted units
            empirePlanet = new Planet
            {
                InstanceID = "empire-home",
                DisplayName = "Empire Home",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(empirePlanet, system);

            movementSystem = new MovementSystem(game, new FogOfWarSystem(game));
            ownershipSystem = new OwnershipSystem(game, movementSystem);
        }

        [Test]
        public void TransferPlanet_ChangesPlanetOwner()
        {
            ownershipSystem.TransferPlanet(targetPlanet, rebels);

            Assert.AreEqual("rebels", targetPlanet.GetOwnerInstanceID());
        }

        [Test]
        public void TransferPlanet_TransfersBuildings_ToNewOwner()
        {
            game.ChangeUnitOwnership(targetPlanet, "empire");
            targetPlanet.GroundSlots = 1;

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
            };
            game.AttachNode(building, targetPlanet);

            ownershipSystem.TransferPlanet(targetPlanet, rebels);

            Assert.AreEqual("rebels", building.GetOwnerInstanceID());
        }

        [Test]
        public void TransferPlanet_EvictsEnemyFleets()
        {
            Fleet empireFleet = new Fleet("empire", "Empire Fleet");
            game.AttachNode(empireFleet, targetPlanet);

            ownershipSystem.TransferPlanet(targetPlanet, rebels);

            Assert.IsNotNull(empireFleet.Movement, "Evicted fleet should be in transit");
            Assert.AreEqual(empirePlanet.InstanceID, empireFleet.Movement.DestinationInstanceID);
        }

        [Test]
        public void TransferPlanet_DoesNotEvictNewOwnerFleets()
        {
            Fleet rebelFleet = new Fleet("rebels", "Rebel Fleet");
            game.AttachNode(rebelFleet, targetPlanet);

            ownershipSystem.TransferPlanet(targetPlanet, rebels);

            Assert.IsNull(rebelFleet.Movement, "New owner fleet should not be evicted");
        }

        [Test]
        public void TransferPlanet_CancelsCompetingMissions()
        {
            StubMission empireMission = EntityFactory.CreateMission(
                "m1",
                "empire",
                targetPlanet.InstanceID
            );
            game.AttachNode(empireMission, targetPlanet);

            ownershipSystem.TransferPlanet(targetPlanet, rebels);

            Assert.IsNull(empireMission.GetParent(), "Competing mission should be detached");
        }

        [Test]
        public void TransferPlanet_ReturnsParticipantsOfCanceledMission()
        {
            game.ChangeUnitOwnership(targetPlanet, "empire");

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, targetPlanet);

            StubMission empireMission = EntityFactory.CreateMission(
                "m1",
                "empire",
                targetPlanet.InstanceID
            );
            game.AttachNode(empireMission, targetPlanet);
            empireMission.MainParticipants.Add(officer);

            ownershipSystem.TransferPlanet(targetPlanet, rebels);

            Assert.IsNotNull(
                officer.Movement,
                "Participant should be in transit after mission canceled"
            );
            Assert.AreEqual(empirePlanet.InstanceID, officer.Movement.DestinationInstanceID);
        }

        [Test]
        public void TransferPlanet_DoesNotCancelNewOwnerMissions()
        {
            StubMission rebelMission = EntityFactory.CreateMission(
                "m1",
                "rebels",
                targetPlanet.InstanceID
            );
            game.AttachNode(rebelMission, targetPlanet);

            ownershipSystem.TransferPlanet(targetPlanet, rebels);

            Assert.IsNotNull(
                rebelMission.GetParent(),
                "Winning faction's mission should not be canceled"
            );
        }

        [Test]
        public void TransferPlanet_PreservesUncancelableMissions()
        {
            UncancelableMission mission = new UncancelableMission(
                "empire",
                targetPlanet.InstanceID
            );
            mission.InstanceID = "m1";
            game.AttachNode(mission, targetPlanet);

            ownershipSystem.TransferPlanet(targetPlanet, rebels);

            Assert.IsNotNull(
                mission.GetParent(),
                "Mission with CanceledOnOwnershipChange=false should survive"
            );
        }
    }
}
