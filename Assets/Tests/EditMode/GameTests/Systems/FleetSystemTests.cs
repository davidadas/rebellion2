using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class FleetSystemTests
    {
        private const string _ownerId = "owner";

        private FleetSystem _fleetSystem;
        private GameRoot _game;
        private Planet _planet;

        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());
            _game.Factions.Add(new Faction { InstanceID = _ownerId });
            PlanetSystem system = new PlanetSystem { InstanceID = "system" };
            _game.AttachNode(system, _game.Galaxy);
            _planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = _ownerId,
                IsColonized = true,
            };
            _game.AttachNode(_planet, system);
            _fleetSystem = new FleetSystem(_game);
        }

        [Test]
        public void Constructor_WithNullGame_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new FleetSystem(null)
            );

            Assert.AreEqual("game", exception.ParamName);
        }

        [Test]
        public void CreateAtPlanet_SnapshotDestination_CreatesFleetOnLivePlanet()
        {
            Fleet fleet = _fleetSystem.CreateAtPlanet(
                new Planet { InstanceID = _planet.InstanceID },
                _ownerId
            );

            Assert.IsNotNull(fleet);
            Assert.AreSame(_planet, fleet.GetParent());
            Assert.AreEqual(_ownerId, fleet.GetOwnerInstanceID());
        }

        [Test]
        public void CreateAtPlanet_InvalidOwner_DoesNotCreateFleet()
        {
            Fleet fleet = _fleetSystem.CreateAtPlanet(_planet, string.Empty);

            Assert.IsNull(fleet);
            Assert.IsEmpty(_planet.GetFleets());
        }

        [Test]
        public void CreateFromCapitalShips_PartialSourceSelection_PreservesSourceFleet()
        {
            Fleet sourceFleet = CreateFleet("source", out CapitalShip selectedShip);
            CapitalShip remainingShip = CreateShip("remaining");
            _game.AttachNode(remainingShip, sourceFleet);

            Fleet createdFleet = _fleetSystem.CreateFromCapitalShips(
                new List<CapitalShip> { selectedShip },
                _ownerId
            );

            Assert.IsNotNull(createdFleet);
            Assert.AreSame(createdFleet, selectedShip.GetParent());
            Assert.AreSame(sourceFleet, remainingShip.GetParent());
            CollectionAssert.AreEquivalent(
                new[] { sourceFleet, createdFleet },
                _planet.GetFleets()
            );
        }

        [Test]
        public void CreateFromCapitalShips_CompleteSourceSelection_RemovesSourceFleet()
        {
            Fleet sourceFleet = CreateFleet("source", out CapitalShip ship);

            Fleet createdFleet = _fleetSystem.CreateFromCapitalShips(
                new List<CapitalShip> { ship },
                _ownerId
            );

            Assert.IsNotNull(createdFleet);
            Assert.AreSame(createdFleet, ship.GetParent());
            Assert.IsNull(sourceFleet.GetParent());
            CollectionAssert.AreEqual(new[] { createdFleet }, _planet.GetFleets());
        }

        [Test]
        public void CreateFromCapitalShips_SnapshotSelection_UsesLiveShip()
        {
            Fleet sourceFleet = CreateFleet("source", out CapitalShip ship);
            CapitalShip snapshot = new CapitalShip { InstanceID = ship.InstanceID };

            Fleet createdFleet = _fleetSystem.CreateFromCapitalShips(
                new List<CapitalShip> { snapshot },
                _ownerId
            );

            Assert.IsNotNull(createdFleet);
            Assert.AreSame(createdFleet, ship.GetParent());
            Assert.IsNull(sourceFleet.GetParent());
            Assert.IsNull(snapshot.GetParent());
        }

        [Test]
        public void CreateFromCapitalShips_UnauthorizedOwner_PreservesSourceGraph()
        {
            Fleet sourceFleet = CreateFleet("source", out CapitalShip ship);

            Fleet createdFleet = _fleetSystem.CreateFromCapitalShips(
                new List<CapitalShip> { ship },
                "other"
            );

            Assert.IsNull(createdFleet);
            Assert.AreSame(sourceFleet, ship.GetParent());
            CollectionAssert.AreEqual(new[] { sourceFleet }, _planet.GetFleets());
        }

        [Test]
        public void CreateFromCapitalShips_CompletedShipInTransit_PreservesSourceGraph()
        {
            Fleet sourceFleet = CreateFleet("source", out CapitalShip ship);
            sourceFleet.Movement = new MovementState();

            Fleet createdFleet = _fleetSystem.CreateFromCapitalShips(
                new List<CapitalShip> { ship },
                _ownerId
            );

            Assert.IsNull(createdFleet);
            Assert.AreSame(sourceFleet, ship.GetParent());
            CollectionAssert.AreEqual(new[] { sourceFleet }, _planet.GetFleets());
        }

        [Test]
        public void CreateFromCapitalShips_ShipUnderConstruction_ChangesDeliveryFleet()
        {
            Fleet sourceFleet = CreateFleet("source", out CapitalShip ship);
            sourceFleet.Movement = new MovementState();
            ship.ManufacturingStatus = ManufacturingStatus.Building;

            Fleet createdFleet = _fleetSystem.CreateFromCapitalShips(
                new List<CapitalShip> { ship },
                _ownerId
            );

            Assert.IsNotNull(createdFleet);
            Assert.AreSame(createdFleet, ship.GetParent());
            Assert.IsNull(sourceFleet.GetParent());
            Assert.AreEqual(ManufacturingStatus.Building, ship.ManufacturingStatus);
        }

        [Test]
        public void RemoveIfEmpty_PopulatedFleet_PreservesFleet()
        {
            Fleet fleet = CreateFleet("fleet", out _);

            bool removed = _fleetSystem.RemoveIfEmpty(fleet);

            Assert.IsFalse(removed);
            Assert.AreSame(_planet, fleet.GetParent());
        }

        [Test]
        public void RemoveIfEmpty_EmptyFleet_RemovesFleet()
        {
            Fleet fleet = new Fleet(_ownerId, "fleet") { InstanceID = "fleet" };
            _game.AttachNode(fleet, _planet);

            bool removed = _fleetSystem.RemoveIfEmpty(fleet);

            Assert.IsTrue(removed);
            Assert.IsNull(fleet.GetParent());
            Assert.IsNull(_game.GetSceneNodeByInstanceID<Fleet>(fleet.InstanceID));
        }

        private Fleet CreateFleet(string instanceId, out CapitalShip ship)
        {
            Fleet fleet = new Fleet(_ownerId, instanceId) { InstanceID = instanceId };
            _game.AttachNode(fleet, _planet);
            ship = CreateShip($"{instanceId}-ship");
            _game.AttachNode(ship, fleet);
            return fleet;
        }

        private static CapitalShip CreateShip(string instanceId)
        {
            return new CapitalShip
            {
                InstanceID = instanceId,
                OwnerInstanceID = _ownerId,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }
    }
}
