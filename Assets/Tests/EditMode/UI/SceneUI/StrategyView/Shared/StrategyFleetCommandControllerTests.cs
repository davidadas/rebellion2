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
        private const string _opposingFactionId = "opponent";
        private const string _playerFactionId = "player";

        private GameRoot _game;
        private GameManager _gameManager;
        private Planet _planet;

        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());
            _game.Factions.Add(new Faction { InstanceID = _playerFactionId });
            _game.Factions.Add(new Faction { InstanceID = _opposingFactionId });
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
            _gameManager = new GameManager(_game);
        }

        [Test]
        public void Constructor_NullGameManager_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new StrategyFleetCommandController(null));
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
        public void ExecutePlanetaryCombat_BombardmentSnapshot_UsesLiveGraphObjects()
        {
            _planet.OwnerInstanceID = _opposingFactionId;
            GameFleet fleet = AddCombatFleet("fleet", bombardment: 1);
            GameFleet requestedFleet = new GameFleet(_playerFactionId, "fleet snapshot")
            {
                InstanceID = fleet.InstanceID,
            };
            Planet requestedTarget = new Planet { InstanceID = _planet.InstanceID };
            StrategyFleetCommandController controller = CreateController();

            GameResult result = controller.ExecutePlanetaryCombat(
                new ISceneNode[] { requestedFleet },
                requestedTarget,
                StrategyMenuAction.BombardCivilianFacilities
            );

            Assert.IsInstanceOf<BombardmentResult>(result);
            BombardmentResult bombardment = (BombardmentResult)result;
            Assert.AreSame(_planet, bombardment.Planet);
            Assert.AreEqual(BombardmentType.Civilian, bombardment.Type);
            Assert.AreEqual(1, bombardment.AttackingUnits.Count);
            Assert.AreEqual(
                fleet.CapitalShips[0].InstanceID,
                bombardment.AttackingUnits[0].Unit.InstanceID
            );
        }

        [Test]
        public void ExecutePlanetaryCombat_InvalidBombardmentInputOrNullResult_ReturnsNull()
        {
            GameFleet fleet = new GameFleet(_playerFactionId, "fleet");
            StrategyFleetCommandController controller = CreateController();

            GameResult noFleetResult = controller.ExecutePlanetaryCombat(
                Array.Empty<ISceneNode>(),
                _planet,
                StrategyMenuAction.BombardMilitaryFacilities
            );
            GameResult missingTargetResult = controller.ExecutePlanetaryCombat(
                new ISceneNode[] { fleet },
                new Planet { InstanceID = "missing" },
                StrategyMenuAction.BombardMilitaryFacilities
            );
            GameResult nullCommandResult = controller.ExecutePlanetaryCombat(
                new ISceneNode[] { fleet },
                _planet,
                StrategyMenuAction.BombardMilitaryFacilities
            );

            Assert.IsNull(noFleetResult);
            Assert.IsNull(missingTargetResult);
            Assert.IsNull(nullCommandResult);
        }

        [Test]
        public void ExecutePlanetaryCombat_AssaultSnapshot_UsesLiveGraphObjects()
        {
            _planet.OwnerInstanceID = _opposingFactionId;
            GameFleet fleet = AddCombatFleet("fleet", includeRegiment: true);
            GameFleet requestedFleet = new GameFleet(_playerFactionId, "fleet snapshot")
            {
                InstanceID = fleet.InstanceID,
            };
            Planet requestedTarget = new Planet { InstanceID = _planet.InstanceID };
            StrategyFleetCommandController controller = CreateController();

            GameResult result = controller.ExecutePlanetaryCombat(
                new ISceneNode[] { requestedFleet },
                requestedTarget,
                StrategyMenuAction.PlanetaryAssault
            );

            Assert.IsInstanceOf<PlanetaryAssaultResult>(result);
            PlanetaryAssaultResult assault = (PlanetaryAssaultResult)result;
            Assert.AreSame(_planet, assault.Planet);
            Assert.AreEqual(1, assault.InitialAttackerRegimentCount);
        }

        [Test]
        public void ExecutePlanetaryCombat_AssaultWithMultipleFleets_UsesFullLiveSelection()
        {
            _planet.OwnerInstanceID = _opposingFactionId;
            GameFleet firstFleet = AddCombatFleet("first", includeRegiment: true);
            GameFleet secondFleet = AddCombatFleet("second", includeRegiment: true);
            GameFleet requestedEligibleFleet = new GameFleet(_playerFactionId, "eligible snapshot")
            {
                InstanceID = firstFleet.InstanceID,
            };
            GameFleet requestedIneligibleFleet = new GameFleet(
                _playerFactionId,
                "ineligible snapshot"
            )
            {
                InstanceID = secondFleet.InstanceID,
            };
            Planet requestedTarget = new Planet { InstanceID = _planet.InstanceID };
            StrategyFleetCommandController controller = CreateController();

            bool canExecute = controller.CanExecutePlanetaryCombat(
                new ISceneNode[] { requestedEligibleFleet, requestedIneligibleFleet },
                requestedTarget,
                StrategyMenuAction.PlanetaryAssault
            );
            GameResult result = controller.ExecutePlanetaryCombat(
                new ISceneNode[] { requestedEligibleFleet, requestedIneligibleFleet },
                requestedTarget,
                StrategyMenuAction.PlanetaryAssault
            );

            Assert.IsTrue(canExecute);
            Assert.IsInstanceOf<PlanetaryAssaultResult>(result);
            Assert.AreEqual(2, ((PlanetaryAssaultResult)result).InitialAttackerRegimentCount);
        }

        [Test]
        public void CanExecutePlanetaryCommands_NeutralSnapshotTarget_UsesLiveGraphObjects()
        {
            _planet.OwnerInstanceID = null;
            GameFleet fleet = AddCombatFleet("fleet", bombardment: 1, includeRegiment: true);
            GameFleet requestedFleet = new GameFleet(_playerFactionId, "fleet snapshot")
            {
                InstanceID = fleet.InstanceID,
            };
            Planet requestedTarget = new Planet { InstanceID = _planet.InstanceID };
            StrategyFleetCommandController controller = CreateController();

            bool canBombard = controller.CanExecutePlanetaryCombat(
                new ISceneNode[] { requestedFleet },
                requestedTarget,
                StrategyMenuAction.GeneralBombardment
            );
            bool canAssault = controller.CanExecutePlanetaryCombat(
                new ISceneNode[] { requestedFleet },
                requestedTarget,
                StrategyMenuAction.PlanetaryAssault
            );

            Assert.IsTrue(canBombard);
            Assert.IsTrue(canAssault);
        }

        [Test]
        public void ExecutePlanetaryCombat_InvalidAssaultInputOrNullResult_ReturnsNull()
        {
            GameFleet fleet = new GameFleet(_playerFactionId, "fleet");
            StrategyFleetCommandController controller = CreateController();

            GameResult noFleetResult = controller.ExecutePlanetaryCombat(
                Array.Empty<ISceneNode>(),
                _planet,
                StrategyMenuAction.PlanetaryAssault
            );
            GameResult missingTargetResult = controller.ExecutePlanetaryCombat(
                new ISceneNode[] { fleet },
                new Planet { InstanceID = "missing" },
                StrategyMenuAction.PlanetaryAssault
            );
            GameResult nullCommandResult = controller.ExecutePlanetaryCombat(
                new ISceneNode[] { fleet },
                _planet,
                StrategyMenuAction.PlanetaryAssault
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
            return new StrategyFleetCommandController(_gameManager);
        }

        private GameFleet AddCombatFleet(
            string instanceId,
            int bombardment = 0,
            bool includeRegiment = false
        )
        {
            GameFleet fleet = new GameFleet
            {
                InstanceID = instanceId,
                OwnerInstanceID = _playerFactionId,
            };
            _game.AttachNode(fleet, _planet);
            CapitalShip ship = CreateShip($"{instanceId}-ship", _playerFactionId);
            ship.Bombardment = bombardment;
            ship.MaxHullStrength = 100;
            ship.CurrentHullStrength = 100;
            ship.RegimentCapacity = includeRegiment ? 1 : 0;
            _game.AttachNode(ship, fleet);
            if (includeRegiment)
            {
                _game.AttachNode(
                    new Regiment
                    {
                        InstanceID = $"{instanceId}-regiment",
                        OwnerInstanceID = _playerFactionId,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    ship
                );
            }

            return fleet;
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
