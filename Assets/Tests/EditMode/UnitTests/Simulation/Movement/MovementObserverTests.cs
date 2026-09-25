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
using Rebellion.Util.Random;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class MovementObserverTests
    {
        [Test]
        public void HandleResults_IndependentInboundUnits_RerouteFromCurrentPosition()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                BlockadeCommands blockade,
                MovementCommands movement,
                GameResultBus resultBus
            ) scene = BuildBlockadeRetargetingScene();
            Starfighter starfighter = EntityFactory.CreateStarfighter("fighter", "empire");
            Regiment regiment = EntityFactory.CreateRegiment("regiment", "empire");
            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "special-forces",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(starfighter, scene.origin);
            scene.game.AttachNode(regiment, scene.origin);
            scene.game.AttachNode(specialForces, scene.origin);

            scene.movement.RequestMove(starfighter, scene.blockadedDestination);
            scene.movement.RequestMove(regiment, scene.blockadedDestination);
            scene.movement.RequestMove(specialForces, scene.blockadedDestination);
            new MovementTickProcessor(scene.movement).ProcessTick(scene.game);

            IMovable[] units = { starfighter, regiment, specialForces };
            Dictionary<IMovable, Point> currentPositions = units.ToDictionary(
                unit => unit,
                unit => unit.Movement.CurrentPosition
            );
            Dictionary<IMovable, string> movementGroupIDs = units.ToDictionary(
                unit => unit,
                unit => unit.Movement.MovementGroupID
            );

            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            List<GameResult> results = ProcessBlockadeStart(
                scene.game,
                scene.blockade,
                scene.resultBus
            );

            foreach (IMovable unit in units)
            {
                Assert.AreSame(scene.nearestSafeDestination, unit.GetParent());
                Assert.AreEqual(currentPositions[unit], unit.Movement.OriginPosition);
                Assert.AreEqual(currentPositions[unit], unit.Movement.CurrentPosition);
                Assert.AreEqual(movementGroupIDs[unit], unit.Movement.MovementGroupID);
                Assert.AreEqual(0, unit.Movement.TicksElapsed);
            }

            CollectionAssert.AreEquivalent(
                units,
                results
                    .OfType<GameObjectEnrouteResult>()
                    .Select(result => result.GameObject)
                    .ToArray()
            );
            Assert.IsEmpty(results.OfType<EvacuationLossesResult>());
        }

        [Test]
        public void HandleResults_ExcludedInboundUnits_ContinueToDestination()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                BlockadeCommands blockade,
                MovementCommands movement,
                GameResultBus resultBus
            ) scene = BuildBlockadeRetargetingScene();
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            SpecialForces missionForces = new SpecialForces
            {
                InstanceID = "mission-forces",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            StubMission mission = new StubMission("empire", scene.blockadedDestination.InstanceID)
            {
                InstanceID = "mission",
            };
            Fleet fleet = EntityFactory.CreateFleet("inbound-fleet", "empire");
            CapitalShip fleetShip = new CapitalShip
            {
                InstanceID = "inbound-fleet-ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                StarfighterCapacity = 1,
            };
            Starfighter carriedStarfighter = EntityFactory.CreateStarfighter(
                "carried-fighter",
                "empire"
            );
            carriedStarfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            Fleet capitalShipSource = EntityFactory.CreateFleet("capital-ship-source", "empire");
            CapitalShip sourceAnchor = new CapitalShip
            {
                InstanceID = "source-anchor",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            CapitalShip independentCapitalShip = new CapitalShip
            {
                InstanceID = "independent-capital-ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Fleet capitalShipDestination = EntityFactory.CreateFleet(
                "capital-ship-destination",
                "empire"
            );

            scene.game.AttachNode(officer, scene.origin);
            scene.game.AttachNode(missionForces, scene.origin);
            scene.game.AttachNode(mission, scene.blockadedDestination);
            scene.game.AttachNode(fleet, scene.origin);
            scene.game.AttachNode(fleetShip, fleet);
            scene.game.AttachNode(carriedStarfighter, fleetShip);
            scene.game.AttachNode(capitalShipSource, scene.origin);
            scene.game.AttachNode(sourceAnchor, capitalShipSource);
            scene.game.AttachNode(independentCapitalShip, capitalShipSource);
            scene.game.AttachNode(capitalShipDestination, scene.blockadedDestination);

            scene.movement.RequestMove(officer, scene.blockadedDestination);
            scene.movement.SendToMission(missionForces, mission);
            scene.movement.RequestMove(fleet, scene.blockadedDestination);
            scene.movement.RequestMove(independentCapitalShip, capitalShipDestination);

            Dictionary<IMovable, MovementState> movements = new IMovable[]
            {
                officer,
                missionForces,
                fleet,
                independentCapitalShip,
            }.ToDictionary(unit => unit, unit => unit.Movement);

            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            List<GameResult> results = ProcessBlockadeStart(
                scene.game,
                scene.blockade,
                scene.resultBus
            );

            Assert.AreSame(scene.blockadedDestination, officer.GetParent());
            Assert.AreSame(mission, missionForces.GetParent());
            Assert.AreSame(scene.blockadedDestination, fleet.GetParent());
            Assert.AreSame(capitalShipDestination, independentCapitalShip.GetParent());
            Assert.AreSame(fleetShip, carriedStarfighter.GetParent());
            Assert.IsNull(carriedStarfighter.Movement);
            foreach (KeyValuePair<IMovable, MovementState> movement in movements)
                Assert.AreSame(movement.Value, movement.Key.Movement);
            Assert.IsEmpty(results.OfType<GameObjectEnrouteResult>());
            Assert.IsEmpty(results.OfType<GameObjectDestroyedResult>());
        }

        [Test]
        public void HandleResults_InTransitBuilding_IsDestroyed()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                BlockadeCommands blockade,
                MovementCommands movement,
                GameResultBus resultBus
            ) scene = BuildBlockadeRetargetingScene();
            Building building = EntityFactory.CreateBuilding("building", "empire");
            building.ManufacturingStatus = ManufacturingStatus.Delivering;
            scene.game.AttachNode(building, scene.blockadedDestination);
            scene.movement.RequestMove(building, scene.blockadedDestination, scene.origin);

            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            List<GameResult> results = ProcessBlockadeStart(
                scene.game,
                scene.blockade,
                scene.resultBus
            );

            Assert.IsNull(scene.game.GetSceneNodeByInstanceID<Building>(building.InstanceID));
            GameObjectDestroyedResult destroyed = results
                .OfType<GameObjectDestroyedResult>()
                .Single();
            Assert.AreSame(building, destroyed.DestroyedObject);
            Assert.AreSame(scene.blockadedDestination, destroyed.Context);
        }

        [Test]
        public void HandleResults_NoValidFallback_DestroysAutoroutedUnit()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                BlockadeCommands blockade,
                MovementCommands movement,
                GameResultBus resultBus
            ) scene = BuildBlockadeRetargetingScene();
            Starfighter starfighter = EntityFactory.CreateStarfighter("fighter", "empire");
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(starfighter, scene.origin);
            scene.movement.RequestMove(starfighter, scene.blockadedDestination);
            scene.origin.OwnerInstanceID = "rebels";
            scene.nearestSafeDestination.OwnerInstanceID = "rebels";
            scene.fartherSafeDestination.OwnerInstanceID = "rebels";

            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            List<GameResult> results = ProcessBlockadeStart(
                scene.game,
                scene.blockade,
                scene.resultBus
            );

            Assert.IsNull(scene.game.GetSceneNodeByInstanceID<Starfighter>(starfighter.InstanceID));
            GameObjectDestroyedResult destroyed = results
                .OfType<GameObjectDestroyedResult>()
                .Single();
            Assert.AreSame(starfighter, destroyed.DestroyedObject);
            Assert.AreSame(scene.blockadedDestination, destroyed.Context);
        }

        [Test]
        public void HandleResults_BlockaderOwnedInboundUnit_Continues()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                BlockadeCommands blockade,
                MovementCommands movement,
                GameResultBus resultBus
            ) scene = BuildBlockadeRetargetingScene();
            scene.fartherSafeDestination.OwnerInstanceID = "rebels";
            (Fleet blockadingFleet, CapitalShip blockadingShip) = AddBlockadingFleet(
                scene.game,
                scene.blockadedDestination,
                starfighterCapacity: 1
            );
            Starfighter starfighter = EntityFactory.CreateStarfighter("fighter", "rebels");
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(starfighter, scene.fartherSafeDestination);
            scene.movement.RequestMove(starfighter, blockadingFleet);
            MovementState movement = starfighter.Movement;

            List<GameResult> results = ProcessBlockadeStart(
                scene.game,
                scene.blockade,
                scene.resultBus
            );

            Assert.AreSame(blockadingShip, starfighter.GetParent());
            Assert.AreSame(movement, starfighter.Movement);
            Assert.IsFalse(
                results
                    .OfType<GameObjectDestroyedResult>()
                    .Any(result => ReferenceEquals(result.DestroyedObject, starfighter))
            );
            Assert.IsFalse(
                results
                    .OfType<GameObjectEnrouteResult>()
                    .Any(result => ReferenceEquals(result.GameObject, starfighter))
            );
        }

        [Test]
        public void HandleResults_NearerFriendlyCarrier_IsPreferredOverOwnedPlanet()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                BlockadeCommands blockade,
                MovementCommands movement,
                GameResultBus resultBus
            ) scene = BuildBlockadeRetargetingScene();
            Planet carrierLocation = new Planet
            {
                InstanceID = "carrier-location",
                IsColonized = false,
                PositionX = 105,
                PositionY = 0,
            };
            PlanetSector sector = scene.blockadedDestination.GetParentOfType<PlanetSector>();
            scene.game.AttachNode(carrierLocation, sector);
            Fleet carrierFleet = EntityFactory.CreateFleet("carrier-fleet", "empire");
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "carrier",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                StarfighterCapacity = 1,
            };
            scene.game.AttachNode(carrierFleet, carrierLocation);
            scene.game.AttachNode(carrier, carrierFleet);
            Starfighter starfighter = EntityFactory.CreateStarfighter("fighter", "empire");
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(starfighter, scene.origin);
            scene.movement.RequestMove(starfighter, scene.blockadedDestination);

            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            ProcessBlockadeStart(scene.game, scene.blockade, scene.resultBus);

            Assert.AreSame(carrier, starfighter.GetParent());
            Assert.IsNotNull(starfighter.Movement);
        }

        [Test]
        public void HandleResults_BlockadedFallback_IsSkipped()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                BlockadeCommands blockade,
                MovementCommands movement,
                GameResultBus resultBus
            ) scene = BuildBlockadeRetargetingScene();
            Starfighter starfighter = EntityFactory.CreateStarfighter("fighter", "empire");
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(starfighter, scene.origin);
            scene.movement.RequestMove(starfighter, scene.blockadedDestination);
            AddBlockadingFleet(scene.game, scene.nearestSafeDestination);
            AddBlockadingFleet(scene.game, scene.blockadedDestination);

            ProcessBlockadeStart(scene.game, scene.blockade, scene.resultBus);

            Assert.AreSame(scene.fartherSafeDestination, starfighter.GetParent());
            Assert.IsNotNull(starfighter.Movement);
        }

        /// <summary>
        /// Builds blockade retargeting scene.
        /// </summary>
        /// <returns>The constructed blockade retargeting scene.</returns>
        private (
            GameRoot game,
            Planet origin,
            Planet blockadedDestination,
            Planet nearestSafeDestination,
            Planet fartherSafeDestination,
            BlockadeCommands blockade,
            MovementCommands movement,
            GameResultBus resultBus
        ) BuildBlockadeRetargetingScene()
        {
            GameConfig config = TestConfig.Create();
            config.Blockade.EvacuationLossPercent = 100;
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet origin = new Planet
            {
                InstanceID = "origin",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
                PositionX = 0,
                PositionY = 0,
            };
            Planet blockadedDestination = new Planet
            {
                InstanceID = "blockaded",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
                PositionX = 100,
                PositionY = 0,
            };
            Planet nearestSafeDestination = new Planet
            {
                InstanceID = "nearest-safe",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
                PositionX = 120,
                PositionY = 0,
            };
            Planet fartherSafeDestination = new Planet
            {
                InstanceID = "farther-safe",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
                PositionX = 160,
                PositionY = 0,
            };
            game.AttachNode(origin, sector);
            game.AttachNode(blockadedDestination, sector);
            game.AttachNode(nearestSafeDestination, sector);
            game.AttachNode(fartherSafeDestination, sector);

            BlockadeCommands blockade = new BlockadeCommands(game, new FixedRNG());
            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game),
                blockade
            );
            GameResultBus resultBus = new GameResultBus();
            new MovementObserver(movement).Connect(resultBus);

            return (
                game,
                origin,
                blockadedDestination,
                nearestSafeDestination,
                fartherSafeDestination,
                blockade,
                movement,
                resultBus
            );
        }

        /// <summary>
        /// Adds blockading fleet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="starfighterCapacity">The starfighter capacity.</param>
        /// <returns>The result of add blockading fleet.</returns>
        private static (Fleet fleet, CapitalShip ship) AddBlockadingFleet(
            GameRoot game,
            Planet planet,
            int starfighterCapacity = 0
        )
        {
            Fleet fleet = EntityFactory.CreateFleet($"blockader-{planet.InstanceID}", "rebels");
            CapitalShip ship = new CapitalShip
            {
                InstanceID = $"blockader-ship-{planet.InstanceID}",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
                StarfighterCapacity = starfighterCapacity,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            return (fleet, ship);
        }

        /// <summary>
        /// Processes blockade start.
        /// </summary>
        /// <param name="game">The game being processed.</param>
        /// <param name="blockade">The blockade.</param>
        /// <param name="resultBus">The bus that delivers blockade reactions.</param>
        /// <returns>The result of process blockade start.</returns>
        private static List<GameResult> ProcessBlockadeStart(
            GameRoot game,
            BlockadeCommands blockade,
            GameResultBus resultBus
        )
        {
            return resultBus.Publish(new BlockadeTickProcessor(blockade).ProcessTick(game));
        }
    }
}
