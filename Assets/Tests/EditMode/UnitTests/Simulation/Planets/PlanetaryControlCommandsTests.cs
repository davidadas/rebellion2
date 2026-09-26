using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class PlanetaryControlCommandsTests
    {
        private GameRoot _game;
        private Faction _rebels;
        private Faction _empire;
        private Planet _targetPlanet;
        private Planet _empirePlanet;
        private MovementCommands _movementSystem;
        private PlanetaryControlCommands _commands;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            GameConfig config = new GameConfig();
            config.SupportShift.OwnershipTransferThreshold = 60;
            _game = new GameRoot(config);

            _rebels = new Faction { InstanceID = "rebels", DisplayName = "Rebels" };
            _empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            _game.GetFactions().Add(_rebels);
            _game.GetFactions().Add(_empire);

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(planetSector, _game.Galaxy);

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
            _game.AttachNode(_targetPlanet, planetSector);

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
            _game.AttachNode(_empirePlanet, planetSector);

            _movementSystem = new MovementCommands(
                _game,
                new FogOfWarCommands(_game),
                new FleetCommands(_game),
                new FogOfWarQueries(_game),
                new MovementQueries(_game)
            );
            _commands = new PlanetaryControlCommands(
                _game,
                _movementSystem,
                new ManufacturingCommands(
                    _game,
                    new FleetCommands(_game),
                    new ManufacturingQueries(_game)
                ),
                new FogOfWarCommands(_game),
                new PlanetaryControlQueries(_game),
                new FogOfWarQueries(_game)
            );
        }

        [Test]
        public void ChangeOwnership_UnitTransfer_PreservesParentAndUpdatesOwner()
        {
            Officer officer = EntityFactory.CreateOfficer("transferred", _empire.InstanceID);
            _game.AttachNode(officer, _empirePlanet);

            List<GameResult> results = _commands.ChangeOwnership(
                _rebels,
                Array.Empty<Planet>(),
                new ISceneNode[] { officer }
            );

            Assert.AreSame(_empirePlanet, officer.GetParent());
            Assert.AreEqual(_rebels.InstanceID, officer.OwnerInstanceID);
            CollectionAssert.Contains(_rebels.GetOwnedUnitsByType<Officer>(), officer);
            CollectionAssert.DoesNotContain(_empire.GetOwnedUnitsByType<Officer>(), officer);
            UnitOwnershipChangedResult result = results
                .OfType<UnitOwnershipChangedResult>()
                .Single();
            Assert.AreSame(_empire, result.PreviousOwner);
            Assert.AreSame(_rebels, result.NewOwner);
        }

        [Test]
        public void ChangeOwnership_UnitAlreadyOwned_ReturnsNoResults()
        {
            Officer officer = EntityFactory.CreateOfficer("unchanged", _empire.InstanceID);
            _game.AttachNode(officer, _empirePlanet);

            List<GameResult> results = _commands.ChangeOwnership(
                _empire,
                Array.Empty<Planet>(),
                new ISceneNode[] { officer }
            );

            Assert.IsEmpty(results);
        }

        [Test]
        public void ChangeOwnership_LaterUnitIsNull_PreservesEarlierTransferAndThrows()
        {
            Officer officer = EntityFactory.CreateOfficer("transferred", _empire.InstanceID);
            _game.AttachNode(officer, _empirePlanet);

            Assert.Throws<NullReferenceException>(() =>
                _commands.ChangeOwnership(
                    _rebels,
                    Array.Empty<Planet>(),
                    new ISceneNode[] { officer, null }
                )
            );

            Assert.AreEqual(_rebels.InstanceID, officer.OwnerInstanceID);
        }

        [Test]
        public void ReconcilePlanet_UncolonizedPlanetWithOnlyInboundRegiment_ReroutesRegiment()
        {
            _targetPlanet.IsColonized = false;
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            Regiment stationedRegiment = new Regiment
            {
                InstanceID = "stationed-regiment",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(stationedRegiment, _targetPlanet);
            Point currentPosition = new Point(50, 0);
            Regiment inboundRegiment = new Regiment
            {
                InstanceID = "inbound-regiment",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Movement = new MovementState
                {
                    TransitTicks = 10,
                    TicksElapsed = 5,
                    OriginPosition = _empirePlanet.GetPosition(),
                    CurrentPosition = currentPosition,
                },
            };
            _game.AttachNode(inboundRegiment, _targetPlanet);
            _game.DetachNode(stationedRegiment);

            List<GameResult> results = _commands.ReconcilePlanet(_targetPlanet);

            Assert.IsNull(_targetPlanet.GetOwnerInstanceID());
            Assert.AreSame(_empirePlanet, inboundRegiment.GetParentOfType<Planet>());
            Assert.IsNotNull(inboundRegiment.Movement);
            Assert.AreEqual(currentPosition, inboundRegiment.Movement.OriginPosition);
            Assert.IsTrue(
                results
                    .OfType<PlanetOwnershipChangedResult>()
                    .Any(result =>
                        result.Planet == _targetPlanet
                        && result.PreviousOwner == _empire
                        && result.NewOwner == null
                    )
            );
        }

        [Test]
        public void TransferPlanet_ValidTransfer_ChangesPlanetOwner()
        {
            _commands.TransferPlanet(_targetPlanet, _rebels);

            Assert.AreEqual("rebels", _targetPlanet.GetOwnerInstanceID());
        }

        [Test]
        public void TransferPlanet_TransfersBuildings_ToNewOwner()
        {
            _game.ChangeOwnership(_targetPlanet, "empire");
            _targetPlanet.EnergyCapacity = 1;

            Building building = new Building { InstanceID = "b1", OwnerInstanceID = "empire" };
            _game.AttachNode(building, _targetPlanet);

            _commands.TransferPlanet(_targetPlanet, _rebels);

            Assert.AreEqual("rebels", building.GetOwnerInstanceID());
        }

        [Test]
        public void TransferPlanet_InactivePlanet_TransfersRetainedBuildingsToNewOwner()
        {
            _game.ChangeOwnership(_targetPlanet, "empire");
            _targetPlanet.EnergyCapacity = 1;
            Building building = new Building { InstanceID = "b1", OwnerInstanceID = "empire" };
            _game.AttachNode(building, _targetPlanet);
            _targetPlanet.IsEnabled = false;

            _commands.TransferPlanet(_targetPlanet, _rebels);

            Assert.AreEqual("rebels", building.GetOwnerInstanceID());
        }

        [Test]
        public void TransferPlanet_HiddenObserverSnapshot_NotRefreshed()
        {
            Faction observer = AddFaction("observer");
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.OuterRim;
            _game.ChangeOwnership(_targetPlanet, "empire");
            _targetPlanet.EnergyCapacity = 1;

            CapturePlanetSnapshot(observer, _targetPlanet, 5);
            AddBuilding(_targetPlanet, "hidden-transfer-building", "empire");

            _game.CurrentTick = 20;
            _commands.TransferPlanet(_targetPlanet, _rebels);

            PlanetSnapshot snapshot = GetPlanetSnapshot(observer, _targetPlanet);
            Assert.AreEqual(5, snapshot.TickCaptured);
            Assert.AreEqual("empire", snapshot.OwnerInstanceID);
            Assert.AreEqual(0, snapshot.Buildings.Count);
        }

        [Test]
        public void TransferPlanet_CoreObserverSnapshot_RefreshesOwnershipOnly()
        {
            Faction observer = AddFaction("observer");
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            _targetPlanet.EnergyCapacity = 1;
            CapturePlanetSnapshot(observer, _targetPlanet, 5);
            AddBuilding(_targetPlanet, "hidden-transfer-building", _empire.InstanceID);

            _game.CurrentTick = 20;
            PlanetOwnershipChangedResult result = _commands.TransferPlanet(_targetPlanet, _rebels);

            PlanetSnapshot snapshot = GetPlanetSnapshot(observer, _targetPlanet);
            Assert.AreEqual(5, snapshot.TickCaptured);
            Assert.AreEqual(_rebels.InstanceID, snapshot.OwnerInstanceID);
            Assert.AreEqual(0, snapshot.Buildings.Count);
            CollectionAssert.Contains(result.ObserverFactionInstanceIDs, observer.InstanceID);
        }

        [Test]
        public void TransferPlanet_OuterRimVisibleObserverSnapshot_RefreshesOwnershipOnly()
        {
            Faction observer = AddFaction("observer");
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.OuterRim;
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            _targetPlanet.EnergyCapacity = 1;
            CapturePlanetSnapshot(observer, _targetPlanet, 5);
            AddBuilding(_targetPlanet, "hidden-transfer-building", _empire.InstanceID);
            Fleet observerFleet = new Fleet(observer.InstanceID, "Observer Fleet");
            CapitalShip observerShip = new CapitalShip
            {
                InstanceID = "observer-ship",
                OwnerInstanceID = observer.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(observerFleet, _targetPlanet);
            _game.AttachNode(observerShip, observerFleet);

            _game.CurrentTick = 20;
            PlanetOwnershipChangedResult result = _commands.TransferPlanet(_targetPlanet, _rebels);

            PlanetSnapshot snapshot = GetPlanetSnapshot(observer, _targetPlanet);
            Assert.AreEqual(5, snapshot.TickCaptured);
            Assert.AreEqual(_rebels.InstanceID, snapshot.OwnerInstanceID);
            Assert.AreEqual(0, snapshot.Buildings.Count);
            CollectionAssert.Contains(result.ObserverFactionInstanceIDs, observer.InstanceID);
        }

        [Test]
        public void TransferPlanet_PreviousOwnerSnapshot_Refreshed()
        {
            _game.ChangeOwnership(_targetPlanet, "empire");

            _game.CurrentTick = 20;
            _commands.TransferPlanet(_targetPlanet, _rebels);

            PlanetSnapshot snapshot = GetPlanetSnapshot(_empire, _targetPlanet);
            Assert.AreEqual(20, snapshot.TickCaptured);
            Assert.AreEqual("rebels", snapshot.OwnerInstanceID);
        }

        [Test]
        public void TransferPlanet_FleetAtPlanet_FleetNotEvicted()
        {
            Fleet empireFleet = new Fleet("empire", "Empire Fleet");
            _game.AttachNode(empireFleet, _targetPlanet);

            _commands.TransferPlanet(_targetPlanet, _rebels);

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

            _commands.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNull(rebelFleet.Movement, "New owner fleet should not be evicted");
        }

        [Test]
        public void TransferPlanet_PlanetWithActiveMissions_PreservesMissionsForLifecycleValidation()
        {
            StubMission empireMission = EntityFactory.CreateMission(
                "m1",
                "empire",
                _targetPlanet.InstanceID
            );
            _game.AttachNode(empireMission, _targetPlanet);

            _commands.TransferPlanet(_targetPlanet, _rebels);

            Assert.AreEqual(_targetPlanet, empireMission.GetParent());
        }

        [Test]
        public void TransferPlanet_PlanetWithActiveMission_DoesNotMoveMissionParticipants()
        {
            _game.ChangeOwnership(_targetPlanet, "empire");

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            _game.AttachNode(officer, _targetPlanet);

            StubMission empireMission = EntityFactory.CreateMission(
                "m1",
                "empire",
                _targetPlanet.InstanceID
            );
            _game.AttachNode(empireMission, _targetPlanet);
            _game.MoveNode(officer, empireMission);

            _commands.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNull(officer.Movement);
            Assert.AreEqual(empireMission, officer.GetParent());
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

            _commands.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNotNull(
                rebelMission.GetParent(),
                "Winning faction's mission should not be canceled"
            );
        }

        [Test]
        public void TransferPlanet_PlanetWithNewOwnerDiplomacyMission_DoesNotCancelIt()
        {
            _targetPlanet.PopularSupport = new Dictionary<string, int> { { "rebels", 70 } };
            _targetPlanet.VisitingFactionIDs = new List<string> { "rebels" };
            Officer officer = EntityFactory.CreateOfficer("o1", "rebels");

            Mission diplomacyMission = MissionTestFactory.TryCreate(
                DiplomacyMission.MissionTypeID,
                _game,
                "rebels",
                _targetPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            _game.AttachNode(diplomacyMission, _targetPlanet);

            _commands.TransferPlanet(_targetPlanet, _rebels);

            Assert.AreEqual(_targetPlanet, diplomacyMission.GetParent());
            Assert.IsTrue(diplomacyMission.ShouldRepeatAfterCompletion(_game));
        }

        [Test]
        public void TransferPlanet_PlanetWithEnemyOfficers_EvictsEnemyOfficers()
        {
            _game.ChangeOwnership(_targetPlanet, "empire");
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            _game.AttachNode(officer, _targetPlanet);

            _commands.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNotNull(officer.Movement, "Evicted officer should be in transit");
            Assert.AreEqual(_empirePlanet, officer.GetParentOfType<Planet>());
        }

        [Test]
        public void TransferPlanet_PlanetWithEnemyRegiments_EvictsEnemyRegiments()
        {
            _game.ChangeOwnership(_targetPlanet, "empire");
            _targetPlanet.EnergyCapacity = 1;
            Regiment regiment = EntityFactory.CreateRegiment("reg1", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            _game.AttachNode(regiment, _targetPlanet);

            _commands.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNotNull(regiment.Movement, "Evicted regiment should be in transit");
            Assert.AreEqual(_empirePlanet, regiment.GetParentOfType<Planet>());
        }

        [Test]
        public void TransferPlanet_EnemyRegimentWithNoReachableDestination_DestroysRegiment()
        {
            _game.ChangeOwnership(_targetPlanet, "empire");
            _game.ChangeOwnership(_empirePlanet, "rebels");
            _targetPlanet.EnergyCapacity = 1;
            Regiment regiment = EntityFactory.CreateRegiment("reg-stranded", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            _game.AttachNode(regiment, _targetPlanet);

            _commands.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsNull(
                regiment.GetParentOfType<Planet>(),
                "Regiment with nowhere to evacuate should be destroyed"
            );
        }

        [Test]
        public void TransferPlanet_EnemyOfficerWithNoReachableDestination_OfficerCaptured()
        {
            _game.ChangeOwnership(_targetPlanet, "empire");
            _game.ChangeOwnership(_empirePlanet, "rebels");
            Officer officer = EntityFactory.CreateOfficer("o-stranded", "empire");
            _game.AttachNode(officer, _targetPlanet);

            _commands.TransferPlanet(_targetPlanet, _rebels);

            Assert.IsTrue(
                officer.IsCaptured,
                "Officer with nowhere to evacuate should be captured by the new owner"
            );
            Assert.AreEqual("rebels", officer.CaptorInstanceID);
            Assert.AreEqual(
                _targetPlanet,
                officer.GetParentOfType<Planet>(),
                "Captured officer should be held on the planet"
            );
        }

        [Test]
        public void TransferPlanet_StationedEnemyStarfighter_ReroutesStarfighter()
        {
            _game.ChangeOwnership(_targetPlanet, "empire");
            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 10,
                CurrentSquadronSize = 10,
                Hyperdrive = 0,
            };
            _game.AttachNode(fighter, _targetPlanet);

            _commands.TransferPlanet(_targetPlanet, _rebels);

            Assert.AreSame(
                _empirePlanet,
                fighter.GetParentOfType<Planet>(),
                "Stationed enemy starfighter should evacuate despite lacking its own hyperdrive"
            );
            Assert.IsNotNull(fighter.Movement);
            Assert.AreSame(
                fighter,
                _game.GetSceneNodeByInstanceID<Starfighter>(fighter.InstanceID)
            );
        }

        [Test]
        public void TransferPlanet_InTransitStarfighterDestinedForPlanet_ReroutesStarfighter()
        {
            _game.ChangeOwnership(_targetPlanet, "empire");
            Point currentPosition = new Point(50, 0);
            Starfighter fighter = new Starfighter
            {
                InstanceID = "inbound-starfighter",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                Movement = new MovementState
                {
                    TransitTicks = 10,
                    TicksElapsed = 5,
                    OriginPosition = _empirePlanet.GetPosition(),
                    CurrentPosition = currentPosition,
                },
            };
            _game.AttachNode(fighter, _targetPlanet);

            _commands.TransferPlanet(_targetPlanet, _rebels);

            Assert.AreSame(_empirePlanet, fighter.GetParentOfType<Planet>());
            Assert.IsNotNull(fighter.Movement);
            Assert.AreEqual(currentPosition, fighter.Movement.OriginPosition);
            Assert.AreSame(
                fighter,
                _game.GetSceneNodeByInstanceID<Starfighter>(fighter.InstanceID)
            );
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

            _commands.TransferPlanet(_targetPlanet, _rebels);

            Assert.AreEqual(
                _targetPlanet,
                empireFleet.GetParent(),
                "In-transit fleet should not be redirected on planet transfer"
            );
        }

        [Test]
        public void TransferPlanet_InTransitOfficerDestinedForPlanet_EvictsOfficer()
        {
            _game.ChangeOwnership(_targetPlanet, "empire");
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            _game.AttachNode(officer, _targetPlanet);
            officer.Movement = new MovementState
            {
                TransitTicks = 5,
                TicksElapsed = 2,
                OriginPosition = _empirePlanet.GetPosition(),
                CurrentPosition = _empirePlanet.GetPosition(),
            };

            _commands.TransferPlanet(_targetPlanet, _rebels);

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
            _game.ChangeOwnership(_targetPlanet, "empire");
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

            _commands.TransferPlanet(_targetPlanet, _rebels);

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

            _commands.TransferPlanet(_targetPlanet, _rebels);

            Assert.AreEqual(
                "empire",
                empireFleet.GetOwnerInstanceID(),
                "Fleet must retain its original owner after planet transfer"
            );
        }

        [Test]
        public void TransferPlanet_EvictedOfficer_DoesNotChangeOfficerOwner()
        {
            _game.ChangeOwnership(_targetPlanet, "empire");
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            _game.AttachNode(officer, _targetPlanet);

            _commands.TransferPlanet(_targetPlanet, _rebels);

            Assert.AreEqual(
                "empire",
                officer.GetOwnerInstanceID(),
                "Evicted officer must retain its original owner"
            );
        }

        [Test]
        public void TransferPlanet_BuildingAtPlanet_BuildingNotEvicted()
        {
            _game.ChangeOwnership(_targetPlanet, "empire");
            _targetPlanet.EnergyCapacity = 1;

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(building, _targetPlanet);

            _commands.TransferPlanet(_targetPlanet, _rebels);

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
            _game.ChangeOwnership(_targetPlanet, "empire");
            _targetPlanet.EnergyCapacity = 1;

            ManufacturingCommands manufacturing = new ManufacturingCommands(
                _game,
                new FleetCommands(_game),
                new ManufacturingQueries(_game)
            );
            Regiment regiment = EntityFactory.CreateRegiment("reg1", "empire");
            bool enqueued = manufacturing.Enqueue(_targetPlanet, regiment, _targetPlanet);
            Assert.IsTrue(enqueued, "Setup: regiment should enqueue successfully");

            _commands.TransferPlanet(_targetPlanet, _rebels);

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _targetPlanet.GetManufacturingQueue();
            bool anyItems = queue.Values.Any(list => list.Count > 0);
            Assert.IsFalse(anyItems, "Manufacturing queue must be empty after ownership transfer");
        }

        [Test]
        public void TransferPlanet_PlanetWithInProgressBuilding_ClearsInProgressBuilding()
        {
            _game.ChangeOwnership(_targetPlanet, "empire");
            _targetPlanet.EnergyCapacity = 1;

            ManufacturingCommands manufacturing = new ManufacturingCommands(
                _game,
                new FleetCommands(_game),
                new ManufacturingQueries(_game)
            );
            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
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

            _commands.TransferPlanet(_targetPlanet, _rebels);

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _targetPlanet.GetManufacturingQueue();
            bool anyItems = queue.Values.Any(list => list.Count > 0);
            Assert.IsFalse(anyItems, "In-progress building must be cleared from queue on transfer");
            Assert.IsNull(
                mine.GetParent(),
                "In-progress building must be detached from planet on transfer"
            );
        }

        [Test]
        public void TransferPlanet_MixedRemoteOrders_CancelsDestinationAndPreservesOthers()
        {
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            _targetPlanet.EnergyCapacity = 2;
            _empirePlanet.IsColonized = true;
            _empirePlanet.EnergyCapacity = 3;

            ManufacturingCommands manufacturing = new ManufacturingCommands(
                _game,
                new FleetCommands(_game),
                new ManufacturingQueries(_game)
            );
            Building remoteMine = new Building
            {
                InstanceID = "remote-mine",
                OwnerInstanceID = _empire.InstanceID,
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
            };
            Building localMine = new Building
            {
                InstanceID = "local-mine",
                OwnerInstanceID = _empire.InstanceID,
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
            };
            Assert.IsTrue(
                manufacturing.Enqueue(_empirePlanet, remoteMine, _targetPlanet, ignoreCost: true)
            );
            Assert.IsTrue(
                manufacturing.Enqueue(_empirePlanet, localMine, _empirePlanet, ignoreCost: true)
            );

            _commands.TransferPlanet(_targetPlanet, _rebels);

            List<IManufacturable> queue = _empirePlanet.GetManufacturingQueue()[
                ManufacturingType.Building
            ];
            Assert.AreEqual(1, queue.Count);
            Assert.AreSame(localMine, queue[0]);
            Assert.IsNull(remoteMine.GetParent());
            Assert.IsNull(_game.GetSceneNodeByInstanceID<Building>(remoteMine.InstanceID));
        }

        [Test]
        public void TransferPlanet_Default_PreservesRegimentOrderAssignedToFriendlyFleet()
        {
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            Fleet fleet = new Fleet(_empire.InstanceID, "Empire Fleet");
            CapitalShip transport = new CapitalShip
            {
                InstanceID = "transport",
                OwnerInstanceID = _empire.InstanceID,
                RegimentCapacity = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(fleet, _targetPlanet);
            _game.AttachNode(transport, fleet);

            ManufacturingCommands manufacturing = new ManufacturingCommands(
                _game,
                new FleetCommands(_game),
                new ManufacturingQueries(_game)
            );
            Regiment regiment = new Regiment
            {
                InstanceID = "fleet-regiment",
                OwnerInstanceID = _empire.InstanceID,
                ConstructionCost = 100,
            };
            Assert.IsTrue(manufacturing.Enqueue(_empirePlanet, regiment, fleet, ignoreCost: true));

            _commands.TransferPlanet(_targetPlanet, _rebels);

            CollectionAssert.Contains(
                _empirePlanet.GetManufacturingQueue()[ManufacturingType.Troop],
                regiment
            );
            Assert.AreSame(fleet, regiment.GetParentOfType<Fleet>());
            Assert.AreSame(regiment, _game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
        }

        [Test]
        public void ClearPlanetOwnership_ActiveDiplomacyMission_PreservesMission()
        {
            _game.ChangeOwnership(_targetPlanet, _rebels.InstanceID);
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 70 },
            };
            _targetPlanet.AddVisitor(_rebels.InstanceID);

            Officer officer = EntityFactory.CreateOfficer("diplomat", _rebels.InstanceID);
            Mission diplomacyMission = MissionTestFactory.TryCreate(
                DiplomacyMission.MissionTypeID,
                _game,
                _rebels.InstanceID,
                _targetPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            _game.AttachNode(diplomacyMission, _targetPlanet);
            _game.AttachNode(officer, diplomacyMission);

            _commands.ClearPlanetOwnership(_targetPlanet);

            Assert.AreSame(_targetPlanet, diplomacyMission.GetParent());
            Assert.IsNull(officer.Movement);
            Assert.AreSame(diplomacyMission, officer.GetParent());
        }

        [Test]
        public void ClearPlanetOwnership_PlanetWithManufacturingQueue_DestroysQueuedUnit()
        {
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);

            ManufacturingCommands manufacturing = new ManufacturingCommands(
                _game,
                new FleetCommands(_game),
                new ManufacturingQueries(_game)
            );
            Regiment regiment = EntityFactory.CreateRegiment("neutralized-regiment", "empire");
            bool enqueued = manufacturing.Enqueue(_targetPlanet, regiment, _targetPlanet);
            Assert.IsTrue(enqueued);

            _commands.ClearPlanetOwnership(_targetPlanet);

            Assert.IsEmpty(_targetPlanet.GetManufacturingQueue());
            Assert.IsNull(regiment.GetParent());
            Assert.IsNull(regiment.Movement);
        }

        [Test]
        public void ProcessTick_UncolonizedNeutralPlanetWithRegiment_DoesNotClaimWithoutFleetDrop()
        {
            (Planet planet, Regiment regiment) = StageUncolonizedPlanetWithFleet("wild1", "empire");
            _game.MoveNode(regiment, planet);

            IReadOnlyList<GameResult> results = new PlanetaryControlTickProcessor(
                _commands
            ).ProcessTick(_game);

            Assert.IsNull(planet.GetOwnerInstanceID());
            Assert.IsEmpty(
                results.OfType<PlanetOwnershipChangedResult>().Where(r => r.Planet == planet)
            );
        }

        [Test]
        public void ProcessTick_UncolonizedOwnedPlanetWithoutRegiments_ReleasesToNeutral()
        {
            (Planet planet, Regiment regiment) = StageUncolonizedPlanetWithFleet("wild2", "empire");
            _movementSystem.RequestMove(regiment, planet);
            Assert.AreEqual("empire", planet.GetOwnerInstanceID(), "Setup: claim should succeed");

            _game.DetachNode(regiment);

            IReadOnlyList<GameResult> results = new PlanetaryControlTickProcessor(
                _commands
            ).ProcessTick(_game);

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
        public void ProcessTick_ReleaseToNeutral_HiddenObserverSnapshot_NotRefreshed()
        {
            Faction observer = AddFaction("observer");
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.OuterRim;
            (Planet planet, Regiment regiment) = StageUncolonizedPlanetWithFleet(
                "wild-hidden-release",
                "empire"
            );
            _movementSystem.RequestMove(regiment, planet);

            CapturePlanetSnapshot(observer, planet, 5);

            _game.DetachNode(regiment);
            _game.CurrentTick = 20;
            new PlanetaryControlTickProcessor(_commands).ProcessTick(_game);

            PlanetSnapshot snapshot = GetPlanetSnapshot(observer, planet);
            Assert.AreEqual(5, snapshot.TickCaptured);
            Assert.AreEqual("empire", snapshot.OwnerInstanceID);
        }

        [Test]
        public void ProcessTick_ReleaseToNeutral_PreviousOwnerSnapshot_Refreshed()
        {
            (Planet planet, Regiment regiment) = StageUncolonizedPlanetWithFleet(
                "wild-release-owner",
                "empire"
            );
            _movementSystem.RequestMove(regiment, planet);

            _game.DetachNode(regiment);
            _game.CurrentTick = 20;
            new PlanetaryControlTickProcessor(_commands).ProcessTick(_game);

            PlanetSnapshot snapshot = GetPlanetSnapshot(_empire, planet);
            Assert.AreEqual(20, snapshot.TickCaptured);
            Assert.IsNull(snapshot.OwnerInstanceID);
        }

        [Test]
        public void ProcessTick_PopularSupportTransfer_SetsOwnershipChangeReason()
        {
            int threshold = _game.Config.SupportShift.OwnershipTransferThreshold;
            _targetPlanet.SetPopularSupport(_rebels.InstanceID, threshold + 1);

            IReadOnlyList<GameResult> results = new PlanetaryControlTickProcessor(
                _commands
            ).ProcessTick(_game);

            PlanetOwnershipChangedResult result = results
                .OfType<PlanetOwnershipChangedResult>()
                .Single(r => r.Planet == _targetPlanet);
            Assert.AreEqual(_rebels, result.NewOwner);
            Assert.AreEqual(PlanetOwnershipChangeReason.PopularSupport, result.Reason);
        }

        [Test]
        public void ProcessTick_OwnedPlanetWithSupport_DoesNotShiftPopularSupport()
        {
            (Planet planet, PlanetaryControlCommands system) = BuildSupportScene(support: 15);

            new PlanetaryControlTickProcessor(system).ProcessTick(_game);

            Assert.AreEqual(15, planet.GetPopularSupport("empire"));
        }

        [Test]
        public void ProcessTick_BlockadeFleetOpposesFavoredSide_ShiftsTowardFavoredSide()
        {
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.OuterRim;
            _game.Config.SupportShift.BlockadeOpposeShiftIntervalTicks = 30;
            _game.Config.SupportShift.BlockadeOpposeShift = -1;
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            _targetPlanet.SetPopularSupport(_empire.InstanceID, 60);
            _targetPlanet.SetPopularSupport(_rebels.InstanceID, 40);
            Fleet fleet = EntityFactory.CreateFleet("rebel-fleet", _rebels.InstanceID);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "rebel-ship",
                OwnerInstanceID = _rebels.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                CurrentHullStrength = 1,
                MaxHullStrength = 1,
            };
            _game.AttachNode(fleet, _targetPlanet);
            _game.AttachNode(ship, fleet);
            _game.CurrentTick = 30;

            new PlanetaryControlTickProcessor(_commands).ProcessTick(_game);
            _game.CurrentTick = 60;

            new PlanetaryControlTickProcessor(_commands).ProcessTick(_game);

            Assert.AreEqual(61, _targetPlanet.GetPopularSupport(_empire.InstanceID));
            Assert.AreEqual(39, _targetPlanet.GetPopularSupport(_rebels.InstanceID));
        }

        [Test]
        public void ProcessTick_BlockadeFleetMatchesFavoredSide_IncreasesFleetSupport()
        {
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.OuterRim;
            _game.Config.SupportShift.BlockadeMatchShiftIntervalTicks = 30;
            _game.Config.SupportShift.BlockadeMatchShift = 1;
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            _targetPlanet.SetPopularSupport(_empire.InstanceID, 40);
            _targetPlanet.SetPopularSupport(_rebels.InstanceID, 60);
            Fleet fleet = EntityFactory.CreateFleet("rebel-fleet", _rebels.InstanceID);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "rebel-ship",
                OwnerInstanceID = _rebels.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                CurrentHullStrength = 1,
                MaxHullStrength = 1,
            };
            _game.AttachNode(fleet, _targetPlanet);
            _game.AttachNode(ship, fleet);
            _game.CurrentTick = 30;

            new PlanetaryControlTickProcessor(_commands).ProcessTick(_game);
            _game.CurrentTick = 60;

            new PlanetaryControlTickProcessor(_commands).ProcessTick(_game);

            Assert.AreEqual(61, _targetPlanet.GetPopularSupport(_rebels.InstanceID));
            Assert.AreEqual(39, _targetPlanet.GetPopularSupport(_empire.InstanceID));
        }

        [Test]
        public void ProcessTick_BlockadeAtTiedSupport_AppliesOpposingShift()
        {
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.OuterRim;
            _game.Config.SupportShift.BlockadeOpposeShiftIntervalTicks = 30;
            _game.Config.SupportShift.BlockadeOpposeShift = -1;
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            _targetPlanet.SetPopularSupport(_empire.InstanceID, 50);
            _targetPlanet.SetPopularSupport(_rebels.InstanceID, 50);
            Fleet fleet = EntityFactory.CreateFleet("rebel-fleet", _rebels.InstanceID);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "rebel-ship",
                OwnerInstanceID = _rebels.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                CurrentHullStrength = 1,
                MaxHullStrength = 1,
            };
            _game.AttachNode(fleet, _targetPlanet);
            _game.AttachNode(ship, fleet);
            _game.CurrentTick = 30;

            new PlanetaryControlTickProcessor(_commands).ProcessTick(_game);
            _game.CurrentTick = 60;
            new PlanetaryControlTickProcessor(_commands).ProcessTick(_game);

            Assert.AreEqual(49, _targetPlanet.GetPopularSupport(_rebels.InstanceID));
            Assert.AreEqual(51, _targetPlanet.GetPopularSupport(_empire.InstanceID));
        }

        [Test]
        public void ProcessTick_BlockadeReinforcesWeakCoreAllianceSupport_DoesNotShift()
        {
            _game.Config.SupportShift.BlockadeMatchShiftIntervalTicks = 30;
            _game.Config.SupportShift.BlockadeMatchShift = 1;
            _game.Config.SupportShift.WeakSupportPenaltyDivisor = 2;
            _rebels.Settings.SupportResistance = SupportChange.Increase;
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.Core;
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            _targetPlanet.SetPopularSupport(_empire.InstanceID, 40);
            _targetPlanet.SetPopularSupport(_rebels.InstanceID, 60);
            Fleet fleet = EntityFactory.CreateFleet("rebel-fleet", _rebels.InstanceID);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "rebel-ship",
                OwnerInstanceID = _rebels.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                CurrentHullStrength = 1,
                MaxHullStrength = 1,
            };
            _game.AttachNode(fleet, _targetPlanet);
            _game.AttachNode(ship, fleet);
            _game.CurrentTick = 30;

            new PlanetaryControlTickProcessor(_commands).ProcessTick(_game);
            _game.CurrentTick = 60;
            new PlanetaryControlTickProcessor(_commands).ProcessTick(_game);

            Assert.AreEqual(60, _targetPlanet.GetPopularSupport(_rebels.InstanceID));
            Assert.AreEqual(40, _targetPlanet.GetPopularSupport(_empire.InstanceID));
        }

        [Test]
        public void ProcessTick_NeutralPlanetBelowThreshold_DoesNotTransferOwnership()
        {
            (Planet planet, PlanetaryControlCommands system) = BuildSupportScene(
                support: 59,
                ownerInstanceId: null
            );

            new PlanetaryControlTickProcessor(system).ProcessTick(_game);

            Assert.IsNull(planet.GetOwnerInstanceID());
        }

        [Test]
        public void ProcessTick_NeutralPlanetAboveThreshold_TransfersOwnership()
        {
            (Planet planet, PlanetaryControlCommands system) = BuildSupportScene(
                support: 61,
                ownerInstanceId: null
            );

            new PlanetaryControlTickProcessor(system).ProcessTick(_game);

            Assert.AreEqual("empire", planet.GetOwnerInstanceID());
        }

        [Test]
        public void ProcessTick_NeutralPlanetWithRegiments_DoesNotTransferOwnership()
        {
            (Planet planet, PlanetaryControlCommands system) = BuildSupportScene(
                support: 61,
                ownerInstanceId: null
            );
            planet.OwnerInstanceID = "empire";
            planet.AddChild(EntityFactory.CreateRegiment("reg1", "empire"));
            planet.OwnerInstanceID = null;

            new PlanetaryControlTickProcessor(system).ProcessTick(_game);

            Assert.IsNull(planet.GetOwnerInstanceID());
        }

        [Test]
        public void ProcessTick_UncolonizedPlanetAboveThreshold_DoesNotTransferOwnership()
        {
            (Planet planet, PlanetaryControlCommands system) = BuildSupportScene(
                support: 61,
                ownerInstanceId: null,
                isColonized: false
            );

            new PlanetaryControlTickProcessor(system).ProcessTick(_game);

            Assert.IsNull(planet.GetOwnerInstanceID());
        }

        [Test]
        public void ReconcilePlanet_ColonizedPlanetLosesLastRegiment_BecomesNeutralWithoutControllingSupport()
        {
            (Planet planet, Regiment regiment) = StageUncolonizedPlanetWithFleet("wild3", "empire");
            _game.ChangeOwnership(planet, "empire");

            planet.IsColonized = true;
            _game.DetachNode(regiment);

            List<GameResult> results = _commands.ReconcilePlanet(planet);

            Assert.IsNull(planet.GetOwnerInstanceID());
            Assert.IsTrue(
                results
                    .OfType<PlanetOwnershipChangedResult>()
                    .Any(result => result.Planet == planet && result.NewOwner == null)
            );
        }

        [Test]
        public void ReconcilePlanet_UncolonizedNeutralPlanetWithRegiment_DoesNotClaim()
        {
            (Planet planet, Regiment regiment) = StageUncolonizedPlanetWithFleet("wild5", "empire");
            _game.MoveNode(regiment, planet);

            List<GameResult> results = _commands.ReconcilePlanet(planet);

            Assert.IsNull(planet.GetOwnerInstanceID());
            Assert.IsEmpty(results);
        }

        [Test]
        public void ReconcilePlanet_ColonizedPlanetWithoutGarrison_TransfersToSupportController()
        {
            (Planet planet, Regiment _) = StageUncolonizedPlanetWithFleet("wild6", "empire");
            planet.IsColonized = true;
            _game.ChangeOwnership(planet, _empire.InstanceID);
            planet.SetPopularSupport(
                _rebels.InstanceID,
                _game.Config.SupportShift.OwnershipTransferThreshold
            );

            List<GameResult> results = _commands.ReconcilePlanet(planet);

            Assert.AreEqual(_rebels.InstanceID, planet.GetOwnerInstanceID());
            PlanetOwnershipChangedResult result = results
                .OfType<PlanetOwnershipChangedResult>()
                .Single(change => change.Planet == planet);
            Assert.AreEqual(_empire, result.PreviousOwner);
            Assert.AreEqual(_rebels, result.NewOwner);
            Assert.AreEqual(PlanetOwnershipChangeReason.PopularSupport, result.Reason);
        }

        [Test]
        public void ReconcilePlanet_ColonizedNeutralPlanetWithRegiment_TransfersToRegimentOwner()
        {
            (Planet planet, Regiment regiment) = StageUncolonizedPlanetWithFleet("wild7", "empire");
            planet.IsColonized = true;
            planet.OwnerInstanceID = "empire";
            _game.MoveNode(regiment, planet);
            planet.OwnerInstanceID = null;

            List<GameResult> results = _commands.ReconcilePlanet(planet);

            Assert.AreEqual(_empire.InstanceID, planet.GetOwnerInstanceID());
            PlanetOwnershipChangedResult result = results
                .OfType<PlanetOwnershipChangedResult>()
                .Single(change => change.Planet == planet);
            Assert.IsNull(result.PreviousOwner);
            Assert.AreEqual(_empire, result.NewOwner);
            Assert.AreEqual(PlanetOwnershipChangeReason.None, result.Reason);
        }

        /// <summary>
        /// Builds a regiment-aboard-fleet at an uncolonized planet, ready for the planet
        /// to accept it: complete, not-in-transit, present at the planet via fleet → ship,
        /// and the planet has the regiment's faction as a visitor.
        /// </summary>
        /// <param name="planetId">The planet id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="positionX">The position x.</param>
        /// <returns>The result of stage uncolonized planet with fleet.</returns>
        private (Planet planet, Regiment regiment) StageUncolonizedPlanetWithFleet(
            string planetId,
            string ownerInstanceId,
            int positionX = 50
        )
        {
            PlanetSector planetSector = _targetPlanet.GetParentOfType<PlanetSector>();

            Planet planet = new Planet
            {
                InstanceID = planetId,
                DisplayName = planetId,
                OwnerInstanceID = null,
                IsColonized = false,
                PositionX = positionX,
                PositionY = 0,
            };
            _game.AttachNode(planet, planetSector);
            planet.AddVisitor(ownerInstanceId);

            Fleet fleet = new Fleet(ownerInstanceId, $"{ownerInstanceId}-fleet");
            _game.AttachNode(fleet, planet);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = $"{planetId}-ship",
                OwnerInstanceID = ownerInstanceId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 4,
            };
            _game.AttachNode(ship, fleet);

            Regiment regiment = new Regiment
            {
                InstanceID = $"{planetId}-reg",
                OwnerInstanceID = ownerInstanceId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Movement = null,
            };
            _game.AttachNode(regiment, ship);

            return (planet, regiment);
        }

        /// <summary>
        /// Builds support scene.
        /// </summary>
        /// <param name="support">The support.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="isColonized">Whether is colonized.</param>
        /// <returns>The constructed support scene.</returns>
        private static (Planet planet, PlanetaryControlCommands system) BuildSupportScene(
            int support,
            string ownerInstanceId = "empire",
            bool isColonized = true
        )
        {
            GameConfig config = new GameConfig();
            config.SupportShift.OwnershipTransferThreshold = 60;
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(planetSector, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = ownerInstanceId,
                IsColonized = isColonized,
                PopularSupport = new Dictionary<string, int> { { "empire", support } },
            };
            game.AttachNode(planet, planetSector);

            FogOfWarCommands fogOfWarSystem = new FogOfWarCommands(game);
            FleetCommands fleetSystem = new FleetCommands(game);
            MovementCommands movementSystem = new MovementCommands(
                game,
                fogOfWarSystem,
                fleetSystem,
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            PlanetaryControlCommands controlSystem = new PlanetaryControlCommands(
                game,
                movementSystem,
                new ManufacturingCommands(game, fleetSystem, new ManufacturingQueries(game)),
                fogOfWarSystem,
                new PlanetaryControlQueries(game),
                new FogOfWarQueries(game)
            );
            return (planet, controlSystem);
        }

        /// <summary>
        /// Adds faction.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <returns>The result of add faction.</returns>
        private Faction AddFaction(string instanceId)
        {
            Faction faction = new Faction { InstanceID = instanceId, DisplayName = instanceId };
            _game.GetFactions().Add(faction);
            return faction;
        }

        /// <summary>
        /// Adds building.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <returns>The result of add building.</returns>
        private Building AddBuilding(Planet planet, string instanceId, string ownerInstanceId)
        {
            Building building = new Building
            {
                InstanceID = instanceId,
                OwnerInstanceID = planet.GetOwnerInstanceID(),
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            _game.AttachNode(building, planet);

            if (building.GetOwnerInstanceID() != ownerInstanceId)
                _game.ChangeOwnership(building, ownerInstanceId);

            return building;
        }

        /// <summary>
        /// Captures planet snapshot.
        /// </summary>
        /// <param name="faction">The faction.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="tick">The tick.</param>
        private void CapturePlanetSnapshot(Faction faction, Planet planet, int tick)
        {
            PlanetSector planetSector = planet.GetParentOfType<PlanetSector>();
            new FogOfWarCommands(_game).CaptureSnapshot(faction, planet, planetSector, tick);
        }

        /// <summary>
        /// Gets planet snapshot.
        /// </summary>
        /// <param name="faction">The faction.</param>
        /// <param name="planet">The planet.</param>
        /// <returns>The requested planet snapshot.</returns>
        private static PlanetSnapshot GetPlanetSnapshot(Faction faction, Planet planet)
        {
            PlanetSector planetSector = planet.GetParentOfType<PlanetSector>();
            return faction.Fog.Snapshots[planetSector.InstanceID].Planets[planet.InstanceID];
        }
    }
}
