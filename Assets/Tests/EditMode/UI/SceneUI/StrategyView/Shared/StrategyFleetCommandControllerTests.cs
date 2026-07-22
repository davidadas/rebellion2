using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using GameFleet = Rebellion.Game.Units.Fleet;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Shared
{
    [TestFixture]
    public class StrategyFleetCommandControllerTests
    {
        private const string _playerFactionId = "player";

        private GameRoot _game;
        private Planet _planet;

        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());
            _game.Factions.Add(new Faction { InstanceID = _playerFactionId });
            _game.Summary.PlayerFactionID = _playerFactionId;
            GamePlanetSystem system = new GamePlanetSystem { InstanceID = "system" };
            _game.AttachNode(system, _game.Galaxy);
            _planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = _playerFactionId,
                IsColonized = true,
                EnergyCapacity = 10,
                NumRawResourceNodes = 10,
            };
            _game.AttachNode(_planet, system);
        }

        [Test]
        public void Constructor_NullDependencies_ThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyFleetCommandController(
                    null,
                    () => new FleetSystem(_game),
                    (_, _, _) => false,
                    (_, _, _) => null,
                    (_, _) => false,
                    (_, _) => null
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyFleetCommandController(
                    () => _game,
                    null,
                    (_, _, _) => false,
                    (_, _, _) => null,
                    (_, _) => false,
                    (_, _) => null
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyFleetCommandController(
                    () => _game,
                    () => new FleetSystem(_game),
                    null,
                    (_, _, _) => null,
                    (_, _) => false,
                    (_, _) => null
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyFleetCommandController(
                    () => _game,
                    () => new FleetSystem(_game),
                    (_, _, _) => false,
                    null,
                    (_, _) => false,
                    (_, _) => null
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyFleetCommandController(
                    () => _game,
                    () => new FleetSystem(_game),
                    (_, _, _) => false,
                    (_, _, _) => null,
                    null,
                    (_, _) => null
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyFleetCommandController(
                    () => _game,
                    () => new FleetSystem(_game),
                    (_, _, _) => false,
                    (_, _, _) => null,
                    (_, _) => false,
                    null
                )
            );
        }

        [Test]
        public void TryCreateFleetFromCapitalShips_OwnedShip_CreatesFleet()
        {
            CapitalShip ship = CreateShip("ship", _playerFactionId);
            GameFleet sourceFleet = new GameFleet(_playerFactionId, "source-fleet");
            _game.AttachNode(sourceFleet, _planet);
            _game.AttachNode(ship, sourceFleet);
            StrategyFleetCommandController controller = CreateController();

            bool created = controller.TryCreateFleetFromCapitalShips(new ISceneNode[] { ship });

            Assert.IsTrue(created);
            Assert.AreEqual(1, _planet.Fleets.Count);
            Assert.AreNotSame(sourceFleet, _planet.Fleets[0]);
            Assert.AreSame(_planet.Fleets[0], ship.GetParent());
        }

        [Test]
        public void TryCreateFleetFromCapitalShips_InvalidSelection_ReturnsFalse()
        {
            CapitalShip enemyShip = CreateShip("enemy-ship", "enemy");
            Officer officer = new Officer { OwnerInstanceID = _playerFactionId };
            StrategyFleetCommandController controller = CreateController();

            bool nullResult = controller.TryCreateFleetFromCapitalShips(null);
            bool emptyResult = controller.TryCreateFleetFromCapitalShips(Array.Empty<ISceneNode>());
            bool enemyResult = controller.TryCreateFleetFromCapitalShips(
                new ISceneNode[] { enemyShip }
            );
            bool mixedResult = controller.TryCreateFleetFromCapitalShips(
                new ISceneNode[] { enemyShip, officer }
            );

            Assert.IsFalse(nullResult);
            Assert.IsFalse(emptyResult);
            Assert.IsFalse(enemyResult);
            Assert.IsFalse(mixedResult);
        }

        [Test]
        public void TryCreateFleetFromCapitalShips_NoActiveGame_ReturnsFalse()
        {
            StrategyFleetCommandController controller = new StrategyFleetCommandController(
                () => null,
                () => new FleetSystem(_game),
                (_, _, _) => false,
                (_, _, _) => null,
                (_, _) => false,
                (_, _) => null
            );

            bool created = controller.TryCreateFleetFromCapitalShips(
                new ISceneNode[] { CreateShip("ship", _playerFactionId) }
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void ExecutePlanetaryBombardment_SnapshotTargetAndFleet_UsesLiveGraphObjects()
        {
            GameFleet fleet = new GameFleet(_playerFactionId, "fleet");
            _game.AttachNode(fleet, _planet);
            GameFleet requestedFleet = new GameFleet(_playerFactionId, "fleet snapshot")
            {
                InstanceID = fleet.InstanceID,
            };
            Planet requestedTarget = new Planet { InstanceID = _planet.InstanceID };
            Planet receivedTarget = null;
            IReadOnlyList<GameFleet> receivedFleets = null;
            BombardmentType? receivedType = null;
            BombardmentResult expected = new BombardmentResult();
            StrategyFleetCommandController controller = new StrategyFleetCommandController(
                () => _game,
                () => new FleetSystem(_game),
                (_, _, _) => true,
                (target, fleets, type) =>
                {
                    receivedTarget = target;
                    receivedFleets = fleets;
                    receivedType = type;
                    return expected;
                },
                (_, _) => false,
                (_, _) => null
            );

            BombardmentResult result = controller.ExecutePlanetaryBombardment(
                new ISceneNode[] { requestedFleet },
                requestedTarget,
                BombardmentType.Civilian
            );

            Assert.AreSame(expected, result);
            Assert.AreSame(_planet, receivedTarget);
            Assert.AreEqual(1, receivedFleets.Count);
            Assert.AreSame(fleet, receivedFleets[0]);
            Assert.AreEqual(BombardmentType.Civilian, receivedType);
        }

        [Test]
        public void ExecutePlanetaryBombardment_InvalidInputOrNullResult_ReturnsNull()
        {
            GameFleet fleet = new GameFleet(_playerFactionId, "fleet");
            StrategyFleetCommandController controller = CreateController();

            BombardmentResult noFleetResult = controller.ExecutePlanetaryBombardment(
                Array.Empty<ISceneNode>(),
                _planet,
                BombardmentType.Military
            );
            BombardmentResult missingTargetResult = controller.ExecutePlanetaryBombardment(
                new ISceneNode[] { fleet },
                new Planet { InstanceID = "missing" },
                BombardmentType.Military
            );
            BombardmentResult nullCommandResult = controller.ExecutePlanetaryBombardment(
                new ISceneNode[] { fleet },
                _planet,
                BombardmentType.Military
            );

            Assert.IsNull(noFleetResult);
            Assert.IsNull(missingTargetResult);
            Assert.IsNull(nullCommandResult);
        }

        [Test]
        public void ExecutePlanetaryAssault_SnapshotTargetAndFleet_UsesLiveGraphObjects()
        {
            GameFleet fleet = new GameFleet(_playerFactionId, "fleet");
            _game.AttachNode(fleet, _planet);
            GameFleet requestedFleet = new GameFleet(_playerFactionId, "fleet snapshot")
            {
                InstanceID = fleet.InstanceID,
            };
            Planet requestedTarget = new Planet { InstanceID = _planet.InstanceID };
            Planet receivedTarget = null;
            IReadOnlyList<GameFleet> receivedFleets = null;
            PlanetaryAssaultResult expected = new PlanetaryAssaultResult();
            StrategyFleetCommandController controller = new StrategyFleetCommandController(
                () => _game,
                () => new FleetSystem(_game),
                (_, _, _) => false,
                (_, _, _) => null,
                (_, _) => true,
                (target, fleets) =>
                {
                    receivedTarget = target;
                    receivedFleets = fleets;
                    return expected;
                }
            );

            PlanetaryAssaultResult result = controller.ExecutePlanetaryAssault(
                new ISceneNode[] { requestedFleet },
                requestedTarget
            );

            Assert.AreSame(expected, result);
            Assert.AreSame(_planet, receivedTarget);
            Assert.AreEqual(1, receivedFleets.Count);
            Assert.AreSame(fleet, receivedFleets[0]);
        }

        [Test]
        public void ExecutePlanetaryAssault_MultipleFleets_UsesFullLiveSelection()
        {
            GameFleet eligibleFleet = new GameFleet(_playerFactionId, "eligible");
            GameFleet ineligibleFleet = new GameFleet(_playerFactionId, "ineligible");
            _game.AttachNode(eligibleFleet, _planet);
            _game.AttachNode(ineligibleFleet, _planet);
            GameFleet requestedEligibleFleet = new GameFleet(_playerFactionId, "eligible snapshot")
            {
                InstanceID = eligibleFleet.InstanceID,
            };
            GameFleet requestedIneligibleFleet = new GameFleet(
                _playerFactionId,
                "ineligible snapshot"
            )
            {
                InstanceID = ineligibleFleet.InstanceID,
            };
            Planet requestedTarget = new Planet { InstanceID = _planet.InstanceID };
            IReadOnlyList<GameFleet> receivedFleets = null;
            IReadOnlyList<GameFleet> validatedFleets = null;
            PlanetaryAssaultResult expected = new PlanetaryAssaultResult();
            StrategyFleetCommandController controller = new StrategyFleetCommandController(
                () => _game,
                () => new FleetSystem(_game),
                (_, _, _) => false,
                (_, _, _) => null,
                (_, fleets) =>
                {
                    validatedFleets = fleets;
                    return true;
                },
                (_, fleets) =>
                {
                    receivedFleets = fleets;
                    return expected;
                }
            );

            bool canExecute = controller.CanExecutePlanetaryAssault(
                new ISceneNode[] { requestedEligibleFleet, requestedIneligibleFleet },
                requestedTarget
            );
            PlanetaryAssaultResult result = controller.ExecutePlanetaryAssault(
                new ISceneNode[] { requestedEligibleFleet, requestedIneligibleFleet },
                requestedTarget
            );

            Assert.IsTrue(canExecute);
            Assert.AreSame(expected, result);
            Assert.AreEqual(2, validatedFleets.Count);
            Assert.AreSame(eligibleFleet, validatedFleets[0]);
            Assert.AreSame(ineligibleFleet, validatedFleets[1]);
            Assert.AreEqual(2, receivedFleets.Count);
            Assert.AreSame(eligibleFleet, receivedFleets[0]);
            Assert.AreSame(ineligibleFleet, receivedFleets[1]);
        }

        [Test]
        public void CanExecutePlanetaryCommands_NeutralSnapshotTarget_UsesLiveGraphObjects()
        {
            _planet.OwnerInstanceID = null;
            GameFleet fleet = new GameFleet(_playerFactionId, "fleet");
            _game.AttachNode(fleet, _planet);
            GameFleet requestedFleet = new GameFleet(_playerFactionId, "fleet snapshot")
            {
                InstanceID = fleet.InstanceID,
            };
            Planet requestedTarget = new Planet { InstanceID = _planet.InstanceID };
            StrategyFleetCommandController controller = new StrategyFleetCommandController(
                () => _game,
                () => new FleetSystem(_game),
                (target, fleets, _) =>
                    ReferenceEquals(target, _planet)
                    && fleets.Count == 1
                    && ReferenceEquals(fleets[0], fleet),
                (_, _, _) => null,
                (target, fleets) =>
                    ReferenceEquals(target, _planet)
                    && fleets.Count == 1
                    && ReferenceEquals(fleets[0], fleet),
                (_, _) => null
            );

            bool canBombard = controller.CanExecutePlanetaryBombardment(
                new ISceneNode[] { requestedFleet },
                requestedTarget,
                BombardmentType.General
            );
            bool canAssault = controller.CanExecutePlanetaryAssault(
                new ISceneNode[] { requestedFleet },
                requestedTarget
            );

            Assert.IsTrue(canBombard);
            Assert.IsTrue(canAssault);
        }

        [Test]
        public void ExecutePlanetaryAssault_InvalidInputOrNullResult_ReturnsNull()
        {
            GameFleet fleet = new GameFleet(_playerFactionId, "fleet");
            StrategyFleetCommandController controller = CreateController();

            PlanetaryAssaultResult noFleetResult = controller.ExecutePlanetaryAssault(
                Array.Empty<ISceneNode>(),
                _planet
            );
            PlanetaryAssaultResult missingTargetResult = controller.ExecutePlanetaryAssault(
                new ISceneNode[] { fleet },
                new Planet { InstanceID = "missing" }
            );
            PlanetaryAssaultResult nullCommandResult = controller.ExecutePlanetaryAssault(
                new ISceneNode[] { fleet },
                _planet
            );

            Assert.IsNull(noFleetResult);
            Assert.IsNull(missingTargetResult);
            Assert.IsNull(nullCommandResult);
        }

        [Test]
        public void ResolvePlanet_SnapshotIdentity_ReturnsLiveGraphPlanet()
        {
            StrategyFleetCommandController controller = CreateController();

            Planet resolved = controller.ResolvePlanet(
                new Planet { InstanceID = _planet.InstanceID }
            );
            Planet missing = controller.ResolvePlanet(new Planet { InstanceID = "missing" });
            Planet empty = controller.ResolvePlanet(new Planet());
            Planet nullPlanet = controller.ResolvePlanet(null);

            Assert.AreSame(_planet, resolved);
            Assert.IsNull(missing);
            Assert.IsNull(empty);
            Assert.IsNull(nullPlanet);
        }

        private StrategyFleetCommandController CreateController()
        {
            return new StrategyFleetCommandController(
                () => _game,
                () => new FleetSystem(_game),
                (_, _, _) => false,
                (_, _, _) => null,
                (_, _) => false,
                (_, _) => null
            );
        }

        private static CapitalShip CreateShip(string instanceId, string ownerId)
        {
            return new CapitalShip
            {
                InstanceID = instanceId,
                OwnerInstanceID = ownerId,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }
    }
}
