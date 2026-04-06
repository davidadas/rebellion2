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

        private GameRoot _game;
        private Faction _rebels;
        private Faction _empire;
        private Planet _targetPlanet;
        private Planet _empirePlanet;
        private MovementSystem _movementSystem;
        private OwnershipSystem _ownershipSystem;

        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());

            _rebels = new Faction { InstanceID = "rebels", DisplayName = "Rebels" };
            _empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            _game.Factions.Add(_rebels);
            _game.Factions.Add(_empire);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(system, _game.Galaxy);

            // Planet being transferred — starts neutral
            _targetPlanet = new Planet
            {
                InstanceID = "target",
                DisplayName = "Target",
                OwnerInstanceID = null,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(_targetPlanet, system);

            // Empire's home planet — fallback destination for evicted units
            _empirePlanet = new Planet
            {
                InstanceID = "empire-home",
                DisplayName = "Empire Home",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            _game.AttachNode(_empirePlanet, system);

            _movementSystem = new MovementSystem(_game, new FogOfWarSystem(_game));
            _ownershipSystem = new OwnershipSystem(
                _game,
                _movementSystem,
                new ManufacturingSystem(_game)
            );
        }

        [Test]
        public void TransferPlanet_ValidTransfer_ChangesPlanetOwner()
        {
            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Assert.AreEqual("rebels", _targetPlanet.GetOwnerInstanceID());
        }

        [Test]
        public void TransferPlanet_TransfersBuildings_ToNewOwner()
        {
            _game.ChangeUnitOwnership(_targetPlanet, "empire");
            _targetPlanet.EnergyCapacity = 1;

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
            };
            _game.AttachNode(building, _targetPlanet);

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Assert.AreEqual("rebels", building.GetOwnerInstanceID());
        }

        [Test]
        public void TransferPlanet_PlanetWithEnemyFleets_EvictsEnemyFleets()
        {
            Fleet empireFleet = new Fleet("empire", "Empire Fleet");
            _game.AttachNode(empireFleet, _targetPlanet);

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNotNull(empireFleet.Movement, "Evicted fleet should be in transit");
            Assert.AreEqual(_empirePlanet, empireFleet.GetParentOfType<Planet>());
        }

        [Test]
        public void TransferPlanet_PlanetWithNewOwnerFleets_DoesNotEvictNewOwnerFleets()
        {
            Fleet rebelFleet = new Fleet("rebels", "Rebel Fleet");
            _game.AttachNode(rebelFleet, _targetPlanet);

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNull(rebelFleet.Movement, "New owner fleet should not be evicted");
        }

        [Test]
        public void TransferPlanet_PlanetWithActiveMissions_CancelsCompetingMissions()
        {
            StubMission empireMission = EntityFactory.CreateMission(
                "m1",
                "empire",
                _targetPlanet.InstanceID
            );
            _game.AttachNode(empireMission, _targetPlanet);

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNull(empireMission.GetParent(), "Competing mission should be detached");
        }

        [Test]
        public void TransferPlanet_PlanetWithCanceledMission_ReturnsParticipants()
        {
            _game.ChangeUnitOwnership(_targetPlanet, "empire");

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            _game.AttachNode(officer, _targetPlanet);

            StubMission empireMission = EntityFactory.CreateMission(
                "m1",
                "empire",
                _targetPlanet.InstanceID
            );
            _game.AttachNode(empireMission, _targetPlanet);
            empireMission.MainParticipants.Add(officer);

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNotNull(
                officer.Movement,
                "Participant should be in transit after mission canceled"
            );
            Assert.AreEqual(_empirePlanet, officer.GetParentOfType<Planet>());
        }

        [Test]
        public void TransferPlanet_PlanetWithNewOwnerMissions_DoesNotCancelThem()
        {
            StubMission rebelMission = EntityFactory.CreateMission(
                "m1",
                "rebels",
                _targetPlanet.InstanceID
            );
            _game.AttachNode(rebelMission, _targetPlanet);

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNotNull(
                rebelMission.GetParent(),
                "Winning faction's mission should not be canceled"
            );
        }

        [Test]
        public void TransferPlanet_PlanetWithUncancelableMissions_PreservesThem()
        {
            UncancelableMission mission = new UncancelableMission(
                "empire",
                _targetPlanet.InstanceID
            );
            mission.InstanceID = "m1";
            _game.AttachNode(mission, _targetPlanet);

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNotNull(
                mission.GetParent(),
                "Mission with CanceledOnOwnershipChange=false should survive"
            );
        }

        [Test]
        public void TransferPlanet_PlanetWithEnemyOfficers_EvictsEnemyOfficers()
        {
            _game.ChangeUnitOwnership(_targetPlanet, "empire");
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            _game.AttachNode(officer, _targetPlanet);

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNotNull(officer.Movement, "Evicted officer should be in transit");
            Assert.AreEqual(_empirePlanet, officer.GetParentOfType<Planet>());
        }

        [Test]
        public void TransferPlanet_PlanetWithEnemyRegiments_EvictsEnemyRegiments()
        {
            _game.ChangeUnitOwnership(_targetPlanet, "empire");
            _targetPlanet.EnergyCapacity = 1;
            Regiment regiment = EntityFactory.CreateRegiment("reg1", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            _game.AttachNode(regiment, _targetPlanet);

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNotNull(regiment.Movement, "Evicted regiment should be in transit");
            Assert.AreEqual(_empirePlanet, regiment.GetParentOfType<Planet>());
        }

        [Test]
        public void TransferPlanet_InTransitFleetDestinedForPlanet_EvictsFleet()
        {
            // Fleet already reparented to target (our immediate-reparent model) but mid-flight.
            Fleet empireFleet = new Fleet("empire", "Empire Fleet");
            _game.AttachNode(empireFleet, _targetPlanet);
            empireFleet.Movement = new MovementState
            {
                TransitTicks = 5,
                TicksElapsed = 2,
                OriginPosition = _empirePlanet.GetPosition(),
                CurrentPosition = _empirePlanet.GetPosition(),
            };

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNotNull(empireFleet.Movement, "Evicted in-transit fleet should be redirected");
            Assert.AreEqual(
                _empirePlanet,
                empireFleet.GetParentOfType<Planet>(),
                "In-transit fleet should be redirected to nearest friendly planet"
            );
        }

        [Test]
        public void TransferPlanet_InTransitOfficerDestinedForPlanet_EvictsOfficer()
        {
            _game.ChangeUnitOwnership(_targetPlanet, "empire");
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            _game.AttachNode(officer, _targetPlanet);
            officer.Movement = new MovementState
            {
                TransitTicks = 5,
                TicksElapsed = 2,
                OriginPosition = _empirePlanet.GetPosition(),
                CurrentPosition = _empirePlanet.GetPosition(),
            };

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNotNull(officer.Movement, "Evicted in-transit officer should be redirected");
            Assert.AreEqual(
                _empirePlanet,
                officer.GetParentOfType<Planet>(),
                "In-transit officer should be redirected to nearest friendly planet"
            );
        }

        [Test]
        public void TransferPlanet_RedirectsInTransitOfficer_OriginIsCurrentPosition()
        {
            // Officer is mid-flight to _targetPlanet. After the planet changes sides the officer
            // should be redirected to the nearest empire planet, and the new journey must begin
            // from the officer's current visual position — not from targetPlanet's coordinates.
            _game.ChangeUnitOwnership(_targetPlanet, "empire");
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            _game.AttachNode(officer, _targetPlanet);

            Point midPoint = new Point(50, 0);
            officer.Movement = new MovementState
            {
                TransitTicks = 10,
                TicksElapsed = 5,
                OriginPosition = _empirePlanet.GetPosition(),
                CurrentPosition = midPoint,
            };

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNotNull(officer.Movement, "Officer should be in transit after redirect");
            Assert.AreEqual(
                _empirePlanet,
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
        public void TransferPlanet_EvictedFleet_DoesNotChangeFleetOwner()
        {
            Fleet empireFleet = new Fleet("empire", "Empire Fleet");
            _game.AttachNode(empireFleet, _targetPlanet);

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Assert.AreEqual(
                "empire",
                empireFleet.GetOwnerInstanceID(),
                "Evicted fleet must retain its original owner"
            );
        }

        [Test]
        public void TransferPlanet_EvictedOfficer_DoesNotChangeOfficerOwner()
        {
            _game.ChangeUnitOwnership(_targetPlanet, "empire");
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            _game.AttachNode(officer, _targetPlanet);

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Assert.AreEqual(
                "empire",
                officer.GetOwnerInstanceID(),
                "Evicted officer must retain its original owner"
            );
        }

        [Test]
        public void TransferPlanet_PlanetWithManufacturingQueues_ClearsQueues()
        {
            _game.ChangeUnitOwnership(_targetPlanet, "empire");
            _targetPlanet.EnergyCapacity = 1;

            ManufacturingSystem manufacturing = new ManufacturingSystem(_game);
            Regiment regiment = EntityFactory.CreateRegiment("reg1", "empire");
            bool enqueued = manufacturing.Enqueue(
                _targetPlanet,
                regiment,
                _targetPlanet,
                ignoreCost: true
            );
            Assert.IsTrue(enqueued, "Setup: regiment should enqueue successfully");

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _targetPlanet.GetManufacturingQueue();
            bool anyItems = queue.Values.Any(list => list.Count > 0);
            Assert.IsFalse(anyItems, "Manufacturing queue must be empty after ownership transfer");
        }

        [Test]
        public void TransferPlanet_PlanetWithInProgressBuilding_ClearsInProgressBuilding()
        {
            _game.ChangeUnitOwnership(_targetPlanet, "empire");
            _targetPlanet.EnergyCapacity = 1;

            ManufacturingSystem manufacturing = new ManufacturingSystem(_game);
            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
            };
            bool enqueued = manufacturing.Enqueue(
                _targetPlanet,
                mine,
                _targetPlanet,
                ignoreCost: true
            );
            Assert.IsTrue(enqueued, "Setup: building should enqueue successfully");
            Assert.IsNotNull(mine.GetParent(), "Setup: building should be attached to planet");

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _targetPlanet.GetManufacturingQueue();
            bool anyItems = queue.Values.Any(list => list.Count > 0);
            Assert.IsFalse(anyItems, "In-progress building must be cleared from queue on transfer");
            Assert.IsNull(
                mine.GetParent(),
                "In-progress building must be detached from planet on transfer"
            );
        }
    }
}
