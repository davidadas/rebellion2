using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
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
                new StrategyFleetCommandController(null, (_, _) => null)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyFleetCommandController(() => _game, null)
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
                (_, _) => null
            );

            bool created = controller.TryCreateFleetFromCapitalShips(
                new ISceneNode[] { CreateShip("ship", _playerFactionId) }
            );

            Assert.IsFalse(created);
        }

        [Test]
        public void TryExecutePlanetaryBombardment_LiveTargetAndFleet_ForwardsCommand()
        {
            GameFleet fleet = new GameFleet(_playerFactionId, "fleet");
            _game.AttachNode(fleet, _planet);
            Planet requestedTarget = new Planet { InstanceID = _planet.InstanceID };
            Planet receivedTarget = null;
            IReadOnlyList<GameFleet> receivedFleets = null;
            StrategyFleetCommandController controller = new StrategyFleetCommandController(
                () => _game,
                (target, fleets) =>
                {
                    receivedTarget = target;
                    receivedFleets = fleets;
                    return new BombardmentResult();
                }
            );

            bool executed = controller.TryExecutePlanetaryBombardment(
                new ISceneNode[] { fleet },
                requestedTarget
            );

            Assert.IsTrue(executed);
            Assert.AreSame(_planet, receivedTarget);
            Assert.AreEqual(1, receivedFleets.Count);
            Assert.AreSame(fleet, receivedFleets[0]);
        }

        [Test]
        public void TryExecutePlanetaryBombardment_InvalidInputOrNullResult_ReturnsFalse()
        {
            GameFleet fleet = new GameFleet(_playerFactionId, "fleet");
            StrategyFleetCommandController controller = CreateController();

            bool noFleetResult = controller.TryExecutePlanetaryBombardment(
                Array.Empty<ISceneNode>(),
                _planet
            );
            bool missingTargetResult = controller.TryExecutePlanetaryBombardment(
                new ISceneNode[] { fleet },
                new Planet { InstanceID = "missing" }
            );
            bool nullCommandResult = controller.TryExecutePlanetaryBombardment(
                new ISceneNode[] { fleet },
                _planet
            );

            Assert.IsFalse(noFleetResult);
            Assert.IsFalse(missingTargetResult);
            Assert.IsFalse(nullCommandResult);
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
            return new StrategyFleetCommandController(() => _game, (_, _) => null);
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
