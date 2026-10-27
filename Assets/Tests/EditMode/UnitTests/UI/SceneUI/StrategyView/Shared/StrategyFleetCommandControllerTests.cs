using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Gameplay.Runtime;
using Rebellion.SceneGraph;
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;
using GameFleet = Rebellion.Game.Units.Fleet;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Shared
{
    [TestFixture]
    public class StrategyFleetCommandControllerTests
    {
        private const string _opposingFactionId = "opponent";
        private const string _playerFactionId = "player";

        private GameRoot _game;
        private GameSession _session;
        private Planet _planet;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestConfig.Create());
            _game.GetFactions().Add(new Faction { InstanceID = _playerFactionId });
            _game.GetFactions().Add(new Faction { InstanceID = _opposingFactionId });
            _game.Summary.PlayerFactionID = _playerFactionId;
            _game.SetFactionController(_playerFactionId, "PLAYER1", PlayerControllerType.Human);
            GalaxyPlanetSector planetSector = new GalaxyPlanetSector { InstanceID = "sector" };
            _game.AttachNode(planetSector, _game.Galaxy);
            _planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = _playerFactionId,
                IsColonized = true,
                EnergyCapacity = 10,
                NumRawResourceNodes = 10,
            };
            _game.AttachNode(_planet, planetSector);
            _session = TestContent.CreateGameSession(_game);
        }

        [Test]
        public void Constructor_NullServices_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyFleetCommandController(null, () => _playerFactionId)
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
            Assert.AreEqual(1, _planet.GetChildren<GameFleet>().Count);
            Assert.AreNotSame(sourceFleet, _planet.GetChildren<GameFleet>()[0]);
            Assert.AreSame(_planet.GetChildren<GameFleet>()[0], ship.GetParent());
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

            CombatReport report = controller.ExecutePlanetaryCombat(
                new ISceneNode[] { requestedFleet },
                requestedTarget,
                StrategyMenuAction.BombardCivilianFacilities
            );

            Assert.IsNotNull(report);
            Assert.AreEqual(CombatReportType.Bombardment, report.CombatType);
            Assert.AreEqual(_planet.InstanceID, report.PlanetInstanceID);
            Assert.AreEqual(BombardmentType.Civilian, report.BombardmentType);
            Assert.AreEqual(1, report.AttackingUnits.Count);
            Assert.AreEqual(
                fleet.GetChildren<CapitalShip>()[0].InstanceID,
                report.AttackingUnits[0].InstanceID
            );
        }

        [Test]
        public void ExecutePlanetaryCombat_InvalidBombardmentInputOrNullResult_ReturnsNull()
        {
            GameFleet fleet = new GameFleet(_playerFactionId, "fleet");
            StrategyFleetCommandController controller = CreateController();

            CombatReport noFleetResult = controller.ExecutePlanetaryCombat(
                Array.Empty<ISceneNode>(),
                _planet,
                StrategyMenuAction.BombardMilitaryFacilities
            );
            CombatReport missingTargetResult = controller.ExecutePlanetaryCombat(
                new ISceneNode[] { fleet },
                new Planet { InstanceID = "missing" },
                StrategyMenuAction.BombardMilitaryFacilities
            );
            CombatReport nullCommandResult = controller.ExecutePlanetaryCombat(
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

            CombatReport report = controller.ExecutePlanetaryCombat(
                new ISceneNode[] { requestedFleet },
                requestedTarget,
                StrategyMenuAction.PlanetaryAssault
            );

            Assert.IsNotNull(report);
            Assert.AreEqual(CombatReportType.PlanetaryAssault, report.CombatType);
            Assert.AreEqual(_planet.InstanceID, report.PlanetInstanceID);
            Assert.AreEqual(1, report.InitialAttackerRegimentCount);
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
            CombatReport report = controller.ExecutePlanetaryCombat(
                new ISceneNode[] { requestedEligibleFleet, requestedIneligibleFleet },
                requestedTarget,
                StrategyMenuAction.PlanetaryAssault
            );

            Assert.IsTrue(canExecute);
            Assert.IsNotNull(report);
            Assert.AreEqual(CombatReportType.PlanetaryAssault, report.CombatType);
            Assert.AreEqual(2, report.InitialAttackerRegimentCount);
        }

        [Test]
        public void ExecutePlanetaryCombat_InvalidAssaultInputOrNullResult_ReturnsNull()
        {
            GameFleet fleet = new GameFleet(_playerFactionId, "fleet");
            StrategyFleetCommandController controller = CreateController();

            CombatReport noFleetResult = controller.ExecutePlanetaryCombat(
                Array.Empty<ISceneNode>(),
                _planet,
                StrategyMenuAction.PlanetaryAssault
            );
            CombatReport missingTargetResult = controller.ExecutePlanetaryCombat(
                new ISceneNode[] { fleet },
                new Planet { InstanceID = "missing" },
                StrategyMenuAction.PlanetaryAssault
            );
            CombatReport nullCommandResult = controller.ExecutePlanetaryCombat(
                new ISceneNode[] { fleet },
                _planet,
                StrategyMenuAction.PlanetaryAssault
            );

            Assert.IsNull(noFleetResult);
            Assert.IsNull(missingTargetResult);
            Assert.IsNull(nullCommandResult);
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

        /// <summary>
        /// Creates controller.
        /// </summary>
        /// <returns>The created controller.</returns>
        private StrategyFleetCommandController CreateController()
        {
            return new StrategyFleetCommandController(_session, () => _playerFactionId);
        }

        /// <summary>
        /// Adds combat fleet.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="bombardment">The bombardment.</param>
        /// <param name="includeRegiment">Whether include regiment.</param>
        /// <returns>The result of add combat fleet.</returns>
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

        /// <summary>
        /// Creates ship.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerId">The owner id.</param>
        /// <returns>The created ship.</returns>
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
