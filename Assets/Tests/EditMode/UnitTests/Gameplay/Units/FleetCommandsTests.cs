using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class FleetCommandsTests
    {
        private const string _ownerId = "owner";

        private FleetCommands _fleetSystem;
        private GameRoot _game;
        private Planet _planet;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestConfig.Create());
            _game.GetFactions().Add(new Faction { InstanceID = _ownerId });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            _game.AttachNode(sector, _game.Galaxy);
            _planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = _ownerId,
                IsColonized = true,
            };
            _game.AttachNode(_planet, sector);
            _fleetSystem = new FleetCommands(_game);
        }

        [Test]
        public void Constructor_WithNullGame_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new FleetCommands(null)
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
            Assert.IsEmpty(_planet.GetChildren<Fleet>());
        }

        [Test]
        public void CreateFleet_RegisteredShip_PreservesFactionOwnership()
        {
            CreateFleet("source", out CapitalShip ship);
            Faction faction = _game.GetFactionByOwnerInstanceID(_ownerId);
            Assert.Contains(ship, faction.GetOwnedUnitsByType<CapitalShip>());

            _fleetSystem.CreateFleet(new[] { ship }, _ownerId);

            Assert.Contains(ship, faction.GetOwnedUnitsByType<CapitalShip>());
        }

        [TestCase(ManufacturingStatus.Complete)]
        [TestCase(ManufacturingStatus.Building)]
        public void CreateFleet_CommittedShip_PreservesProjectedMaintenance(
            ManufacturingStatus status
        )
        {
            CreateFleet("source", out CapitalShip ship);
            ship.ManufacturingStatus = status;
            ship.MaintenanceCost = 12;
            Faction faction = _game.GetFactionByOwnerInstanceID(_ownerId);
            int before = faction.GetTotalProjectedMaintenanceCost();
            Assert.Greater(before, 0);

            _fleetSystem.CreateFleet(new[] { ship }, _ownerId);

            Assert.AreEqual(before, faction.GetTotalProjectedMaintenanceCost());
        }

        [Test]
        public void CreateFleet_PartialSourceSelection_PreservesSourceFleet()
        {
            Fleet sourceFleet = CreateFleet("source", out CapitalShip selectedShip);
            CapitalShip remainingShip = CreateShip("remaining");
            _game.AttachNode(remainingShip, sourceFleet);

            Fleet createdFleet = _fleetSystem.CreateFleet(
                new List<CapitalShip> { selectedShip },
                _ownerId
            );

            Assert.IsNotNull(createdFleet);
            Assert.AreSame(createdFleet, selectedShip.GetParent());
            Assert.AreSame(sourceFleet, remainingShip.GetParent());
            CollectionAssert.AreEquivalent(
                new[] { sourceFleet, createdFleet },
                _planet.GetChildren<Fleet>().ToList()
            );
        }

        [Test]
        public void CreateFleet_CompleteSourceSelection_RemovesSourceFleet()
        {
            Fleet sourceFleet = CreateFleet("source", out CapitalShip ship);

            Fleet createdFleet = _fleetSystem.CreateFleet(new List<CapitalShip> { ship }, _ownerId);

            Assert.IsNotNull(createdFleet);
            Assert.AreSame(createdFleet, ship.GetParent());
            Assert.IsNull(sourceFleet.GetParent());
            CollectionAssert.AreEqual(
                new[] { createdFleet },
                _planet.GetChildren<Fleet>().ToList()
            );
        }

        [Test]
        public void CreateFleet_OwnedShip_PreservesOwnershipAndMaintenance()
        {
            CreateFleet("source", out CapitalShip ship);
            ship.MaintenanceCost = 17;
            Faction faction = _game.GetFactionByOwnerInstanceID(_ownerId);
            Assert.AreEqual(17, faction.GetTotalMaintenanceCost());

            Fleet createdFleet = _fleetSystem.CreateFleet(new List<CapitalShip> { ship }, _ownerId);

            Assert.IsNotNull(createdFleet);
            CollectionAssert.Contains(faction.GetOwnedUnitsByType<CapitalShip>(), ship);
            Assert.AreEqual(17, faction.GetTotalMaintenanceCost());
        }

        [Test]
        public void CreateFleet_SnapshotSelection_UsesLiveShip()
        {
            Fleet sourceFleet = CreateFleet("source", out CapitalShip ship);
            CapitalShip snapshot = new CapitalShip { InstanceID = ship.InstanceID };

            Fleet createdFleet = _fleetSystem.CreateFleet(
                new List<CapitalShip> { snapshot },
                _ownerId
            );

            Assert.IsNotNull(createdFleet);
            Assert.AreSame(createdFleet, ship.GetParent());
            Assert.IsNull(sourceFleet.GetParent());
            Assert.IsNull(snapshot.GetParent());
        }

        [Test]
        public void CreateFleet_UnauthorizedOwner_PreservesSourceGraph()
        {
            Fleet sourceFleet = CreateFleet("source", out CapitalShip ship);

            Fleet createdFleet = _fleetSystem.CreateFleet(new List<CapitalShip> { ship }, "other");

            Assert.IsNull(createdFleet);
            Assert.AreSame(sourceFleet, ship.GetParent());
            CollectionAssert.AreEqual(new[] { sourceFleet }, _planet.GetChildren<Fleet>().ToList());
        }

        [Test]
        public void CreateFleet_CompletedShipInTransit_PreservesSourceGraph()
        {
            Fleet sourceFleet = CreateFleet("source", out CapitalShip ship);
            sourceFleet.Movement = new MovementState();

            Fleet createdFleet = _fleetSystem.CreateFleet(new List<CapitalShip> { ship }, _ownerId);

            Assert.IsNull(createdFleet);
            Assert.AreSame(sourceFleet, ship.GetParent());
            CollectionAssert.AreEqual(new[] { sourceFleet }, _planet.GetChildren<Fleet>().ToList());
        }

        [Test]
        public void CreateFleet_ShipUnderConstruction_ChangesDeliveryFleet()
        {
            Fleet sourceFleet = CreateFleet("source", out CapitalShip ship);
            sourceFleet.Movement = new MovementState();
            ship.ManufacturingStatus = ManufacturingStatus.Building;

            Fleet createdFleet = _fleetSystem.CreateFleet(new List<CapitalShip> { ship }, _ownerId);

            Assert.IsNotNull(createdFleet);
            Assert.AreSame(createdFleet, ship.GetParent());
            Assert.IsNull(sourceFleet.GetParent());
            Assert.AreEqual(ManufacturingStatus.Building, ship.ManufacturingStatus);
        }

        /// <summary>
        /// Creates fleet.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ship">Receives the ship.</param>
        /// <returns>The created fleet.</returns>
        private Fleet CreateFleet(string instanceId, out CapitalShip ship)
        {
            Fleet fleet = new Fleet(_ownerId, instanceId) { InstanceID = instanceId };
            _game.AttachNode(fleet, _planet);
            ship = CreateShip($"{instanceId}-ship");
            _game.AttachNode(ship, fleet);
            return fleet;
        }

        /// <summary>
        /// Creates ship.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <returns>The created ship.</returns>
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
