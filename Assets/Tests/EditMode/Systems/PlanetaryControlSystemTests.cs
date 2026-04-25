using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class PlanetaryControlSystemTests
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
        private PlanetaryControlSystem _ownershipSystem;

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
            _ownershipSystem = new PlanetaryControlSystem(
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
        public void TransferPlanet_FleetAtPlanet_FleetNotEvicted()
        {
            Fleet empireFleet = new Fleet("empire", "Empire Fleet");
            _game.AttachNode(empireFleet, _targetPlanet);

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNull(empireFleet.Movement, "Fleet should not be evicted on planet transfer");
            Assert.AreEqual(
                _targetPlanet,
                empireFleet.GetParent(),
                "Fleet should remain at the captured planet"
            );
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
        public void TransferPlanet_InTransitFleetAtPlanet_FleetNotRedirected()
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

            Assert.AreEqual(
                _targetPlanet,
                empireFleet.GetParent(),
                "In-transit fleet should not be redirected on planet transfer"
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
        public void TransferPlanet_FleetAtPlanet_FleetOwnershipUnchanged()
        {
            Fleet empireFleet = new Fleet("empire", "Empire Fleet");
            _game.AttachNode(empireFleet, _targetPlanet);

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Assert.AreEqual(
                "empire",
                empireFleet.GetOwnerInstanceID(),
                "Fleet must retain its original owner after planet transfer"
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
        public void TransferPlanet_BuildingAtPlanet_BuildingNotEvicted()
        {
            _game.ChangeUnitOwnership(_targetPlanet, "empire");
            _targetPlanet.EnergyCapacity = 1;

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(building, _targetPlanet);

            _ownershipSystem.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNull(
                building.Movement,
                "Building must not be given a movement state on transfer"
            );
            Assert.AreEqual(
                _targetPlanet,
                building.GetParent(),
                "Building must remain at the planet after transfer"
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

        /// <summary>
        /// Builds a regiment-aboard-fleet at an uncolonized planet, in a state that
        /// passes <see cref="Planet.CanAcceptRegiment"/>: complete, not-in-transit, parent
        /// is the planet (via fleet → ship → regiment), and the planet has the faction
        /// as a visitor.
        /// </summary>
        private (Planet planet, Regiment regiment) StageUncolonizedPlanetWithFleet(
            string planetId,
            string ownerInstanceId,
            int positionX = 50
        )
        {
            PlanetSystem system = _targetPlanet.GetParentOfType<PlanetSystem>();

            Planet planet = new Planet
            {
                InstanceID = planetId,
                DisplayName = planetId,
                OwnerInstanceID = null,
                IsColonized = false,
                PositionX = positionX,
                PositionY = 0,
            };
            _game.AttachNode(planet, system);
            planet.AddVisitor(ownerInstanceId);

            Fleet fleet = new Fleet(ownerInstanceId, $"{ownerInstanceId}-fleet");
            _game.AttachNode(fleet, planet);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = $"{planetId}-ship",
                OwnerInstanceID = ownerInstanceId,
                AllowedOwnerInstanceIDs = new List<string> { ownerInstanceId },
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 4,
            };
            _game.AttachNode(ship, fleet);

            Regiment regiment = new Regiment
            {
                InstanceID = $"{planetId}-reg",
                OwnerInstanceID = ownerInstanceId,
                AllowedOwnerInstanceIDs = new List<string> { ownerInstanceId },
                ManufacturingStatus = ManufacturingStatus.Complete,
                Movement = null,
            };
            _game.AttachNode(regiment, ship);

            return (planet, regiment);
        }

        [Test]
        public void ProcessTick_UncolonizedNeutralPlanetWithRegiment_ClaimsForRegimentFaction()
        {
            (Planet planet, Regiment regiment) = StageUncolonizedPlanetWithFleet("wild1", "empire");
            _game.MoveNode(regiment, planet);

            List<GameResult> results = _ownershipSystem.ProcessTick();

            Assert.AreEqual("empire", planet.GetOwnerInstanceID());
            Assert.AreEqual(
                _game.Config.Planet.MaxPopularSupport,
                planet.GetPopularSupport("empire")
            );
            Assert.AreEqual(0, planet.GetPopularSupport("rebels"));
            Assert.IsTrue(
                results
                    .OfType<PlanetOwnershipChangedResult>()
                    .Any(r =>
                        r.Planet == planet && r.NewOwner == _empire && r.PreviousOwner == null
                    )
            );
        }

        [Test]
        public void ProcessTick_UncolonizedOwnedPlanetWithoutRegiments_ReleasesToNeutral()
        {
            (Planet planet, Regiment regiment) = StageUncolonizedPlanetWithFleet("wild2", "empire");
            _game.MoveNode(regiment, planet);
            _ownershipSystem.ProcessTick();
            Assert.AreEqual("empire", planet.GetOwnerInstanceID(), "Setup: claim should succeed");

            _game.DetachNode(regiment);

            List<GameResult> results = _ownershipSystem.ProcessTick();

            Assert.IsNull(planet.GetOwnerInstanceID());
            Assert.AreEqual(0, planet.GetPopularSupport("empire"));
            Assert.AreEqual(0, planet.GetPopularSupport("rebels"));
            Assert.IsTrue(
                results
                    .OfType<PlanetOwnershipChangedResult>()
                    .Any(r =>
                        r.Planet == planet && r.NewOwner == null && r.PreviousOwner == _empire
                    )
            );
        }

        [Test]
        public void ProcessTick_ColonizedPlanetLosesLastRegiment_OwnershipPersists()
        {
            (Planet planet, Regiment regiment) = StageUncolonizedPlanetWithFleet("wild3", "empire");
            _game.MoveNode(regiment, planet);
            _ownershipSystem.ProcessTick();

            // Once colonized, regiment count no longer drives ownership.
            planet.IsColonized = true;
            _game.DetachNode(regiment);

            _ownershipSystem.ProcessTick();

            Assert.AreEqual("empire", planet.GetOwnerInstanceID());
        }

        [Test]
        public void ProcessTick_UncolonizedOwnedPlanetReleased_EvictsPreviousOwnerOfficers()
        {
            (Planet planet, Regiment regiment) = StageUncolonizedPlanetWithFleet("wild4", "empire");
            _game.MoveNode(regiment, planet);
            _ownershipSystem.ProcessTick();

            Officer officer = EntityFactory.CreateOfficer("o-evict", "empire");
            _game.AttachNode(officer, planet);

            _game.DetachNode(regiment);
            _ownershipSystem.ProcessTick();

            Assert.AreNotEqual(
                planet,
                officer.GetParentOfType<Planet>(),
                "Officer should be evacuated when the planet flips back to neutral"
            );
        }

        [Test]
        public void ReconcilePlanet_NeutralUncolonizedPlanetWithRegiment_ClaimsImmediately()
        {
            (Planet planet, Regiment regiment) = StageUncolonizedPlanetWithFleet("wild5", "empire");
            _game.MoveNode(regiment, planet);

            // No ProcessTick — single-planet reconcile should flip ownership in this call alone.
            List<GameResult> results = _ownershipSystem.ReconcilePlanet(planet);

            Assert.AreEqual("empire", planet.GetOwnerInstanceID());
            Assert.AreEqual(
                _game.Config.Planet.MaxPopularSupport,
                planet.GetPopularSupport("empire")
            );
            Assert.IsTrue(
                results
                    .OfType<PlanetOwnershipChangedResult>()
                    .Any(r => r.Planet == planet && r.NewOwner == _empire)
            );
        }

        [Test]
        public void ReconcilePlanet_ColonizedPlanet_NoOp()
        {
            (Planet planet, Regiment regiment) = StageUncolonizedPlanetWithFleet("wild6", "empire");
            planet.IsColonized = true;
            string priorOwner = planet.GetOwnerInstanceID();

            List<GameResult> results = _ownershipSystem.ReconcilePlanet(planet);

            Assert.AreEqual(priorOwner, planet.GetOwnerInstanceID());
            Assert.IsEmpty(results);
        }
    }
}
