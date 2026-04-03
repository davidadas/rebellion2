using System.Collections.Generic;
using System.Drawing;
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
            ownershipSystem = new OwnershipSystem(
                game,
                movementSystem,
                new ManufacturingSystem(game, movementSystem)
            );
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
            Assert.AreEqual(empirePlanet, empireFleet.GetParentOfType<Planet>());
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
            Assert.AreEqual(empirePlanet, officer.GetParentOfType<Planet>());
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

        [Test]
        public void TransferPlanet_EvictsEnemyOfficers()
        {
            game.ChangeUnitOwnership(targetPlanet, "empire");
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, targetPlanet);

            ownershipSystem.TransferPlanet(targetPlanet, rebels);

            Assert.IsNotNull(officer.Movement, "Evicted officer should be in transit");
            Assert.AreEqual(empirePlanet, officer.GetParentOfType<Planet>());
        }

        [Test]
        public void TransferPlanet_EvictsEnemyRegiments()
        {
            game.ChangeUnitOwnership(targetPlanet, "empire");
            targetPlanet.GroundSlots = 1;
            Regiment regiment = EntityFactory.CreateRegiment("reg1", "empire");
            game.AttachNode(regiment, targetPlanet);

            ownershipSystem.TransferPlanet(targetPlanet, rebels);

            Assert.IsNotNull(regiment.Movement, "Evicted regiment should be in transit");
            Assert.AreEqual(empirePlanet, regiment.GetParentOfType<Planet>());
        }

        [Test]
        public void TransferPlanet_EvictsInTransitFleet()
        {
            // Fleet already reparented to target (our immediate-reparent model) but mid-flight.
            Fleet empireFleet = new Fleet("empire", "Empire Fleet");
            game.AttachNode(empireFleet, targetPlanet);
            empireFleet.Movement = new MovementState
            {
                TransitTicks = 5,
                TicksElapsed = 2,
                OriginPosition = empirePlanet.GetPosition(),
                CurrentPosition = empirePlanet.GetPosition(),
            };

            ownershipSystem.TransferPlanet(targetPlanet, rebels);

            Assert.IsNotNull(empireFleet.Movement, "Evicted in-transit fleet should be redirected");
            Assert.AreEqual(
                empirePlanet,
                empireFleet.GetParentOfType<Planet>(),
                "In-transit fleet should be redirected to nearest friendly planet"
            );
        }

        [Test]
        public void TransferPlanet_EvictsInTransitOfficer()
        {
            game.ChangeUnitOwnership(targetPlanet, "empire");
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, targetPlanet);
            officer.Movement = new MovementState
            {
                TransitTicks = 5,
                TicksElapsed = 2,
                OriginPosition = empirePlanet.GetPosition(),
                CurrentPosition = empirePlanet.GetPosition(),
            };

            ownershipSystem.TransferPlanet(targetPlanet, rebels);

            Assert.IsNotNull(officer.Movement, "Evicted in-transit officer should be redirected");
            Assert.AreEqual(
                empirePlanet,
                officer.GetParentOfType<Planet>(),
                "In-transit officer should be redirected to nearest friendly planet"
            );
        }

        [Test]
        public void TransferPlanet_RedirectsInTransitOfficer_OriginIsCurrentPosition()
        {
            // Officer is mid-flight to targetPlanet. After the planet changes sides the officer
            // should be redirected to the nearest empire planet, and the new journey must begin
            // from the officer's current visual position — not from targetPlanet's coordinates.
            game.ChangeUnitOwnership(targetPlanet, "empire");
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, targetPlanet);

            Point midPoint = new Point(50, 0);
            officer.Movement = new MovementState
            {
                TransitTicks = 10,
                TicksElapsed = 5,
                OriginPosition = empirePlanet.GetPosition(),
                CurrentPosition = midPoint,
            };

            ownershipSystem.TransferPlanet(targetPlanet, rebels);

            Assert.IsNotNull(officer.Movement, "Officer should be in transit after redirect");
            Assert.AreEqual(
                empirePlanet,
                officer.GetParentOfType<Planet>(),
                "Officer should head to nearest friendly planet"
            );
            Assert.AreEqual(
                midPoint,
                officer.Movement.OriginPosition,
                "New journey must start from the officer's current visual position, not from the planet"
            );
        }

        [Test]
        public void TransferPlanet_DoesNotChangeOwnerOfEvictedFleet()
        {
            Fleet empireFleet = new Fleet("empire", "Empire Fleet");
            game.AttachNode(empireFleet, targetPlanet);

            ownershipSystem.TransferPlanet(targetPlanet, rebels);

            Assert.AreEqual(
                "empire",
                empireFleet.GetOwnerInstanceID(),
                "Evicted fleet must retain its original owner"
            );
        }

        [Test]
        public void TransferPlanet_DoesNotChangeOwnerOfEvictedOfficer()
        {
            game.ChangeUnitOwnership(targetPlanet, "empire");
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, targetPlanet);

            ownershipSystem.TransferPlanet(targetPlanet, rebels);

            Assert.AreEqual(
                "empire",
                officer.GetOwnerInstanceID(),
                "Evicted officer must retain its original owner"
            );
        }

        [Test]
        public void TransferPlanet_ClearsManufacturingQueues()
        {
            game.ChangeUnitOwnership(targetPlanet, "empire");
            targetPlanet.GroundSlots = 1;

            ManufacturingSystem manufacturing = new ManufacturingSystem(game, movementSystem);
            Regiment regiment = EntityFactory.CreateRegiment("reg1", "empire");
            bool enqueued = manufacturing.Enqueue(
                targetPlanet,
                regiment,
                targetPlanet,
                ignoreCost: true
            );
            Assert.IsTrue(enqueued, "Setup: regiment should enqueue successfully");

            ownershipSystem.TransferPlanet(targetPlanet, rebels);

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                targetPlanet.GetManufacturingQueue();
            bool anyItems = queue.Values.Any(list => list.Count > 0);
            Assert.IsFalse(anyItems, "Manufacturing queue must be empty after ownership transfer");
        }

        [Test]
        public void TransferPlanet_ClearsInProgressBuilding()
        {
            game.ChangeUnitOwnership(targetPlanet, "empire");
            targetPlanet.GroundSlots = 1;

            ManufacturingSystem manufacturing = new ManufacturingSystem(game, movementSystem);
            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
                ConstructionCost = 100,
            };
            bool enqueued = manufacturing.Enqueue(
                targetPlanet,
                mine,
                targetPlanet,
                ignoreCost: true
            );
            Assert.IsTrue(enqueued, "Setup: building should enqueue successfully");
            Assert.IsNotNull(mine.GetParent(), "Setup: building should be attached to planet");

            ownershipSystem.TransferPlanet(targetPlanet, rebels);

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                targetPlanet.GetManufacturingQueue();
            bool anyItems = queue.Values.Any(list => list.Count > 0);
            Assert.IsFalse(anyItems, "In-progress building must be cleared from queue on transfer");
            Assert.IsNull(
                mine.GetParent(),
                "In-progress building must be detached from planet on transfer"
            );
        }
    }
}
