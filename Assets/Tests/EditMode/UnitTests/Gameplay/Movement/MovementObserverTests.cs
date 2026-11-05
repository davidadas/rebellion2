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
using Rebellion.Gameplay;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class MovementObserverTests
    {
        [Test]
        public void HandleResults_RegimentAndSpecialForcesInbound_RerouteFromCurrentPosition()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                Planet blockade,
                MovementCommands movement,
                GameResultBus resultBus
            ) scene = BuildBlockadeRetargetingScene();
            Regiment regiment = EntityFactory.CreateRegiment("regiment", "empire");
            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "special-forces",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(regiment, scene.origin);
            scene.game.AttachNode(specialForces, scene.origin);

            scene.movement.RequestMove(regiment, scene.blockadedDestination);
            scene.movement.RequestMove(specialForces, scene.blockadedDestination);
            new MovementTickProcessor(scene.movement).ProcessTick(scene.game);

            IMovable[] units = { regiment, specialForces };
            foreach (IMovable unit in units)
            {
                unit.Movement.SetPosition(new Point(10, 0));
                MovementState movement = unit.Movement;
                unit.Movement = TestMovement.Create(
                    transitTicks: movement.TransitTicks,
                    ticksElapsed: movement.TicksElapsed,
                    originPosition: movement.OriginPosition,
                    currentPosition: movement.CurrentPosition,
                    movementGroupID: movement.MovementGroupID,
                    resolveEncounterOnArrival: true,
                    previousLocationInstanceID: movement.PreviousLocationInstanceID,
                    destinationPlanetInstanceID: movement.DestinationPlanetInstanceID
                );
            }

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
                Assert.AreSame(scene.origin, unit.GetParent());
                Assert.AreEqual(currentPositions[unit], unit.Movement.OriginPosition);
                Assert.AreEqual(currentPositions[unit], unit.Movement.CurrentPosition);
                Assert.AreEqual(movementGroupIDs[unit], unit.Movement.MovementGroupID);
                Assert.IsTrue(unit.Movement.ResolveEncounterOnArrival);
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

        [TestCase(0)]
        [TestCase(1)]
        public void HandleResults_DefenderOwnedInboundStarfighter_PrefersPreviousLocation(
            int hyperdrive
        )
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                Planet blockade,
                MovementCommands movement,
                GameResultBus resultBus
            ) scene = BuildBlockadeRetargetingScene();
            Starfighter starfighter = EntityFactory.CreateStarfighter("fighter", "empire");
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            starfighter.Hyperdrive = hyperdrive;
            scene.game.AttachNode(starfighter, scene.origin);
            scene.movement.RequestMove(starfighter, scene.blockadedDestination);
            MovementState movement = starfighter.Movement;
            movement.SetPosition(new Point(90, 0));
            movement = TestMovement.Create(
                transitTicks: movement.TransitTicks,
                originPosition: movement.OriginPosition,
                currentPosition: movement.CurrentPosition,
                movementGroupID: movement.MovementGroupID,
                sourceEventInstanceID: "movement-source",
                previousLocationInstanceID: movement.PreviousLocationInstanceID,
                destinationPlanetInstanceID: movement.DestinationPlanetInstanceID
            );
            starfighter.Movement = movement;

            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            List<GameResult> results = ProcessBlockadeStart(
                scene.game,
                scene.blockade,
                scene.resultBus
            );

            Assert.AreSame(scene.origin, starfighter.GetParent());
            Assert.IsNotNull(starfighter.Movement);
            Assert.AreSame(movement, starfighter.Movement);
            Assert.AreEqual(scene.origin.GetPosition(), starfighter.Movement.OriginPosition);
            Assert.AreEqual(movement.CurrentPosition, starfighter.Movement.CurrentPosition);
            Assert.AreEqual(movement.MovementGroupID, starfighter.Movement.MovementGroupID);
            Assert.AreEqual("movement-source", starfighter.Movement.SourceEventInstanceID);
            Assert.AreEqual(0, starfighter.Movement.TicksElapsed);
            Assert.IsTrue(
                results
                    .OfType<GameObjectEnrouteResult>()
                    .Any(result => ReferenceEquals(result.GameObject, starfighter))
            );
            Assert.IsFalse(
                results
                    .OfType<GameObjectDestroyedResult>()
                    .Any(result => ReferenceEquals(result.DestroyedObject, starfighter))
            );
        }

        [TestCase(0)]
        [TestCase(1)]
        public void HandleResults_InboundStarfighter_KeepsOutboundTimeBeforeReturnTrip(
            int hyperdrive
        )
        {
            var scene = BuildBlockadeRetargetingScene();
            Starfighter fighter = EntityFactory.CreateStarfighter("fighter", "empire");
            fighter.ManufacturingStatus = ManufacturingStatus.Complete;
            fighter.Hyperdrive = hyperdrive;
            scene.game.AttachNode(fighter, scene.origin);
            scene.movement.RequestMove(fighter, scene.blockadedDestination);
            int outboundTicks = fighter.Movement.TransitTicks;
            TestMovement.AdvanceTo(fighter.Movement, 1);
            fighter.Movement.SetPosition(new Point(10, 0));
            int returnTicks = new MovementQueries(scene.game).CalculateTransitTicks(
                fighter,
                scene.blockadedDestination,
                scene.origin
            );

            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            ProcessBlockadeStart(scene.game, scene.blockade, scene.resultBus);

            Assert.AreEqual(outboundTicks - 1 + returnTicks, fighter.Movement.TicksRemaining());
            MovementTickProcessor processor = new(scene.movement);
            for (int tick = 0; tick < outboundTicks - 1; tick++)
                processor.ProcessTick(scene.game);

            Assert.IsNotNull(fighter.Movement);
            Assert.AreEqual(
                scene.blockadedDestination.GetPosition(),
                fighter.Movement.CurrentPosition
            );
            Assert.AreEqual(returnTicks, fighter.Movement.TicksRemaining());
            for (int tick = 0; tick < returnTicks; tick++)
                processor.ProcessTick(scene.game);

            Assert.IsNull(fighter.Movement);
            Assert.AreSame(scene.origin, fighter.GetParent());
        }

        [Test]
        public void HandleResults_RetargetedStarfighter_ReturnsToVisitedOrigin()
        {
            var scene = BuildBlockadeRetargetingScene();
            Starfighter fighter = EntityFactory.CreateStarfighter("fighter", "empire");
            fighter.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(fighter, scene.origin);
            scene.movement.RequestMove(fighter, scene.blockadedDestination);
            fighter.Movement.SetPosition(new Point(90, 0));
            var originBlockade = AddBlockadingFleet(scene.game, scene.origin);
            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            ProcessBlockadeStart(scene.game, scene.blockade, scene.resultBus);
            Assert.AreSame(scene.nearestSafeDestination, fighter.GetParent());
            Assert.AreEqual(scene.blockadedDestination.InstanceID, fighter.LastParentInstanceID);
            fighter.Movement = SerializationHelper.Deserialize<MovementState>(
                SerializationHelper.Serialize(fighter.Movement)
            );
            fighter.LastParentNode = null;

            scene.game.DeleteNode(originBlockade.fleet);
            AddBlockadingFleet(scene.game, scene.nearestSafeDestination);
            ProcessBlockadeStart(scene.game, scene.nearestSafeDestination, scene.resultBus);

            Assert.AreSame(scene.origin, fighter.GetParent());
        }

        [Test]
        public void HandleResults_UnavailableOrigin_OrdersFallbacksFromBlockedDestination()
        {
            var scene = BuildBlockadeRetargetingScene();
            Starfighter fighter = EntityFactory.CreateStarfighter("fighter", "empire");
            fighter.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(fighter, scene.origin);
            scene.movement.RequestMove(fighter, scene.blockadedDestination);
            fighter.Movement.SetPosition(new Point(195, 0));
            scene.origin.OwnerInstanceID = "rebels";

            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            ProcessBlockadeStart(scene.game, scene.blockade, scene.resultBus);

            Assert.AreSame(scene.nearestSafeDestination, fighter.GetParent());
        }

        [Test]
        public void HandleResults_NearerCarrier_PrefersFriendlyWorldBeforeRemoteFleet()
        {
            var scene = BuildBlockadeRetargetingScene();
            Planet carrierPlanet = new Planet
            {
                InstanceID = "carrier-planet",
                PositionX = 101,
                IsColonized = true,
            };
            scene.game.AttachNode(carrierPlanet, scene.origin.GetParentOfType<PlanetSector>());
            AddFriendlyCarrier(scene.game, carrierPlanet, "carrier", 2);
            Starfighter fighter = EntityFactory.CreateStarfighter("fighter", "empire");
            fighter.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(fighter, scene.origin);
            scene.movement.RequestMove(fighter, scene.blockadedDestination);
            fighter.Movement.SetPosition(new Point(100, 0));
            scene.origin.OwnerInstanceID = "rebels";

            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            ProcessBlockadeStart(scene.game, scene.blockade, scene.resultBus);

            Assert.AreSame(scene.nearestSafeDestination, fighter.GetParent());
        }

        [Test]
        public void HandleResults_PreviousCarrierFull_ReturnsToItsPlanet()
        {
            var scene = BuildBlockadeRetargetingScene();
            CapitalShip carrier = AddFriendlyCarrier(scene.game, scene.origin, "carrier", 1);
            Starfighter fighter = EntityFactory.CreateStarfighter("fighter", "empire");
            fighter.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(fighter, carrier);
            scene.movement.RequestMove(fighter, scene.blockadedDestination);
            scene.game.AttachNode(EntityFactory.CreateStarfighter("occupant", "empire"), carrier);
            fighter.Movement.SetPosition(new Point(90, 0));

            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            ProcessBlockadeStart(scene.game, scene.blockade, scene.resultBus);

            Assert.AreSame(scene.origin, fighter.GetParent());
        }

        [Test]
        public void HandleResults_InboundCarrierStillValid_RetainsItsDestinationAndTiming()
        {
            var scene = BuildBlockadeRetargetingScene();
            CapitalShip carrier = AddFriendlyCarrier(
                scene.game,
                scene.blockadedDestination,
                "carrier",
                1
            );
            Starfighter fighter = EntityFactory.CreateStarfighter("fighter", "empire");
            fighter.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(fighter, scene.origin);
            Assert.IsTrue(scene.movement.TryRequestMove(fighter, carrier));
            MovementState movement = fighter.Movement;
            scene.blockadedDestination.OwnerInstanceID = "third-faction";
            AddBlockadingFleet(scene.game, scene.blockadedDestination);

            List<GameResult> results = ProcessBlockadeStart(
                scene.game,
                scene.blockade,
                scene.resultBus
            );

            Assert.AreSame(carrier, fighter.GetParent());
            Assert.AreSame(movement, fighter.Movement);
            Assert.IsNull(movement.NextLeg);
            Assert.IsEmpty(results.OfType<GameObjectEnrouteResult>());
        }

        [Test]
        public void HandleResults_CurrentSystemHasCarriers_PrefersMostAvailableCapacity()
        {
            var scene = BuildBlockadeRetargetingScene();
            AddFriendlyCarrier(scene.game, scene.blockadedDestination, "small-carrier", 1);
            CapitalShip largeCarrier = AddFriendlyCarrier(
                scene.game,
                scene.blockadedDestination,
                "large-carrier",
                5
            );
            Starfighter fighter = EntityFactory.CreateStarfighter("fighter", "empire");
            fighter.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(fighter, scene.origin);
            scene.movement.RequestMove(fighter, scene.blockadedDestination);
            scene.blockadedDestination.OwnerInstanceID = "third-faction";
            AddBlockadingFleet(scene.game, scene.blockadedDestination);

            Assert.IsTrue(scene.blockadedDestination.IsBlockaded());

            ProcessBlockadeStart(scene.game, scene.blockade, scene.resultBus);

            Assert.AreSame(largeCarrier, fighter.GetParent());
        }

        [Test]
        public void HandleResults_DeliveringStarfighter_PrefersItsProductionPlanet()
        {
            var scene = BuildBlockadeRetargetingScene();
            Starfighter starfighter = EntityFactory.CreateStarfighter("delivery", "empire");
            starfighter.ManufacturingStatus = ManufacturingStatus.Delivering;
            starfighter.ProducerPlanetID = scene.origin.InstanceID;
            scene.game.AttachNode(starfighter, scene.blockadedDestination);
            scene.movement.RequestMove(starfighter, scene.blockadedDestination, scene.origin);
            int outboundTicks = starfighter.Movement.TransitTicks;
            starfighter.Movement.SetPosition(new Point(90, 0));
            Assert.IsNull(starfighter.GetLastParent());

            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            ProcessBlockadeStart(scene.game, scene.blockade, scene.resultBus);

            Assert.AreSame(scene.origin, starfighter.GetParent());
            Assert.IsNotNull(starfighter.Movement);
            Assert.AreEqual(ManufacturingStatus.Delivering, starfighter.ManufacturingStatus);
            int remainingTicks =
                outboundTicks
                + new MovementQueries(scene.game).CalculateTransitTicks(
                    starfighter,
                    scene.blockadedDestination,
                    scene.origin
                );
            Assert.AreEqual(remainingTicks, starfighter.Movement.TicksRemaining());
            List<GameResult> arrivalResults = new();
            MovementTickProcessor processor = new(scene.movement);
            for (int tick = 0; tick < remainingTicks; tick++)
                arrivalResults.AddRange(processor.ProcessTick(scene.game));

            Assert.IsNull(starfighter.Movement);
            Assert.AreEqual(ManufacturingStatus.Complete, starfighter.ManufacturingStatus);
            Assert.AreSame(
                scene.origin,
                arrivalResults.OfType<UnitArrivedResult>().Single().Destination
            );
        }

        [Test]
        public void HandleResults_PreviousParentReferenceMissing_UsesItsSavedIdentity()
        {
            var scene = BuildBlockadeRetargetingScene();
            Starfighter starfighter = EntityFactory.CreateStarfighter("fighter", "empire");
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(starfighter, scene.origin);
            scene.movement.RequestMove(starfighter, scene.blockadedDestination);
            starfighter.Movement.SetPosition(new Point(90, 0));
            starfighter.LastParentNode = null;

            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            ProcessBlockadeStart(scene.game, scene.blockade, scene.resultBus);

            Assert.AreSame(scene.origin, starfighter.GetParent());
            Assert.IsNotNull(starfighter.Movement);
        }

        [TestCase("captured")]
        [TestCase("blockaded")]
        [TestCase("removed")]
        public void HandleResults_PreviousLocationUnavailable_UsesAnotherSafeDestination(
            string unavailableReason
        )
        {
            var scene = BuildBlockadeRetargetingScene();
            Starfighter starfighter = EntityFactory.CreateStarfighter("fighter", "empire");
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(starfighter, scene.origin);
            scene.movement.RequestMove(starfighter, scene.blockadedDestination);
            starfighter.Movement.SetPosition(new Point(90, 0));
            if (unavailableReason == "captured")
                scene.origin.OwnerInstanceID = "rebels";
            else if (unavailableReason == "blockaded")
                AddBlockadingFleet(scene.game, scene.origin);
            else
                scene.game.DeleteNode(scene.origin);

            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            ProcessBlockadeStart(scene.game, scene.blockade, scene.resultBus);

            Assert.AreSame(scene.nearestSafeDestination, starfighter.GetParent());
            Assert.IsNotNull(starfighter.Movement);
        }

        [Test]
        public void HandleResults_InboundStarfighterWithoutSafeDestination_IsDestroyed()
        {
            var scene = BuildBlockadeRetargetingScene();
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
            Assert.AreSame(
                starfighter,
                results.OfType<GameObjectDestroyedResult>().Single().DestroyedObject
            );
        }

        [Test]
        public void HandleResults_FighterArrivesBeforeBlockadeReaction_RemainsAvailableForCombat()
        {
            var scene = BuildBlockadeRetargetingScene();
            Starfighter starfighter = EntityFactory.CreateStarfighter("fighter", "empire");
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            starfighter.MaxSquadronSize = 12;
            starfighter.CurrentSquadronSize = 12;
            scene.game.AttachNode(starfighter, scene.origin);
            scene.movement.RequestMove(starfighter, scene.blockadedDestination);
            TestMovement.AdvanceTo(starfighter.Movement, starfighter.Movement.TransitTicks - 1);
            CapitalShip blockader = AddBlockadingFleet(scene.game, scene.blockadedDestination).ship;
            blockader.MaxHullStrength = 100;
            blockader.CurrentHullStrength = 100;

            new MovementTickProcessor(scene.movement).ProcessTick(scene.game);
            ProcessBlockadeStart(scene.game, scene.blockade, scene.resultBus);

            Assert.AreSame(scene.blockadedDestination, starfighter.GetParent());
            Assert.IsNull(starfighter.Movement);
            SpaceCombatQueries combat = new(scene.game, new MovementQueries(scene.game));
            Assert.IsTrue(
                combat.TryFindContestedForces(
                    new HashSet<string>(),
                    out Planet contestedPlanet,
                    out _,
                    out _,
                    out _,
                    out _
                )
            );
            Assert.AreSame(scene.blockadedDestination, contestedPlanet);
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
                Planet blockade,
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
                Planet blockade,
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
                Planet blockade,
                MovementCommands movement,
                GameResultBus resultBus
            ) scene = BuildBlockadeRetargetingScene();
            Regiment regiment = EntityFactory.CreateRegiment("regiment", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(regiment, scene.origin);
            scene.movement.RequestMove(regiment, scene.blockadedDestination);
            scene.origin.OwnerInstanceID = "rebels";
            scene.nearestSafeDestination.OwnerInstanceID = "rebels";
            scene.fartherSafeDestination.OwnerInstanceID = "rebels";

            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            List<GameResult> results = ProcessBlockadeStart(
                scene.game,
                scene.blockade,
                scene.resultBus
            );

            Assert.IsNull(scene.game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
            GameObjectDestroyedResult destroyed = results
                .OfType<GameObjectDestroyedResult>()
                .Single();
            Assert.AreSame(regiment, destroyed.DestroyedObject);
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
                Planet blockade,
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
        public void HandleResults_PreviousLocationUnavailable_PrefersNearestFriendlyCarrier()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                Planet blockade,
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
            starfighter.Movement.SetPosition(new Point(100, 0));
            scene.origin.OwnerInstanceID = "rebels";
            scene.nearestSafeDestination.OwnerInstanceID = "rebels";
            scene.fartherSafeDestination.OwnerInstanceID = "rebels";
            scene.blockadedDestination.OwnerInstanceID = "third-faction";

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
                Planet blockade,
                MovementCommands movement,
                GameResultBus resultBus
            ) scene = BuildBlockadeRetargetingScene();
            Starfighter starfighter = EntityFactory.CreateStarfighter("fighter", "empire");
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(starfighter, scene.origin);
            scene.movement.RequestMove(starfighter, scene.blockadedDestination);
            starfighter.Movement.SetPosition(new Point(140, 0));
            scene.origin.OwnerInstanceID = "rebels";
            scene.blockadedDestination.OwnerInstanceID = "third-faction";
            AddBlockadingFleet(scene.game, scene.nearestSafeDestination);
            AddBlockadingFleet(scene.game, scene.blockadedDestination);

            ProcessBlockadeStart(scene.game, scene.blockade, scene.resultBus);

            Assert.AreSame(scene.fartherSafeDestination, starfighter.GetParent());
            Assert.IsNotNull(starfighter.Movement);
        }

        [Test]
        public void HandleResults_DestroyedCarrierWithInactiveOfficer_RelocatesWithoutActivating()
        {
            var scene = BuildRemovedCarrierScene();
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            officer.IsEnabled = false;
            scene.game.AttachNode(officer, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            List<GameResult> settled = scene.resultBus.Publish(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = scene.carrier,
                    Context = scene.origin,
                    Reason = UnitDestructionReason.Combat,
                }
            );

            Assert.AreSame(
                officer,
                scene.game.GetSceneNodeByInstanceID<Officer>(
                    officer.InstanceID,
                    includeDisabled: true
                )
            );
            Assert.AreSame(scene.fallback, officer.GetParent());
            Assert.IsNotNull(officer.Movement);
            Assert.AreEqual(scene.origin.GetPosition(), officer.Movement.OriginPosition);
            Assert.IsFalse(officer.IsEnabled);
            Assert.IsTrue(
                settled
                    .OfType<GameObjectEnrouteResult>()
                    .Any(result => ReferenceEquals(result.GameObject, officer))
            );
        }

        [Test]
        public void HandleResults_DestroyedCarrierWithMultipleSafeDestinations_UsesNearestLocation()
        {
            var scene = BuildRemovedCarrierScene();
            Planet fartherDestination = new Planet
            {
                InstanceID = "farther-destination",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 175,
            };
            scene.game.AttachNode(fartherDestination, scene.origin.GetParentOfType<PlanetSector>());
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            scene.game.AttachNode(officer, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            scene.resultBus.Publish(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = scene.carrier,
                    Context = scene.origin,
                    Reason = UnitDestructionReason.Combat,
                }
            );

            Assert.AreSame(scene.fallback, officer.GetParent());
            Assert.AreNotSame(fartherDestination, officer.GetParent());
            Assert.IsNotNull(officer.Movement);
        }

        [Test]
        public void HandleResults_DestroyedCarrierWithSurvivingCarrier_MovesOfficerWithinFleet()
        {
            var scene = BuildRemovedCarrierScene();
            CapitalShip survivor = new CapitalShip
            {
                InstanceID = "survivor",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                CurrentHullStrength = 100,
            };
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            scene.game.AttachNode(survivor, scene.fleet);
            scene.game.AttachNode(officer, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            scene.resultBus.Publish(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = scene.carrier,
                    Context = scene.origin,
                }
            );

            Assert.AreSame(survivor, officer.GetParent());
            Assert.IsNull(officer.Movement);
        }

        [Test]
        public void HandleResults_DestroyedCarrierWithCarrierInOtherFleet_DoesNotMoveOfficerAcrossFleets()
        {
            var scene = BuildRemovedCarrierScene();
            Fleet otherFleet = EntityFactory.CreateFleet("other-fleet", "empire");
            CapitalShip otherCarrier = new CapitalShip
            {
                InstanceID = "other-carrier",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                CurrentHullStrength = 100,
            };
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            scene.game.AttachNode(otherFleet, scene.fallback);
            scene.game.AttachNode(otherCarrier, otherFleet);
            otherFleet.Movement = TestMovement.Create(
                originPosition: scene.origin.GetPosition(),
                currentPosition: scene.origin.GetPosition()
            );
            scene.game.AttachNode(officer, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            scene.resultBus.Publish(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = scene.carrier,
                    Context = scene.origin,
                    Reason = UnitDestructionReason.Combat,
                }
            );

            Assert.AreSame(scene.fallback, officer.GetParent());
            Assert.AreNotSame(otherCarrier, officer.GetParent());
            Assert.IsNotNull(officer.Movement);
        }

        [Test]
        public void HandleResults_DestroyedCarrierOverBlockadedFriendlyPlanet_RestoresOfficerLocally()
        {
            var scene = BuildRemovedCarrierScene();
            scene.origin.OwnerInstanceID = "empire";
            scene.fallback.OwnerInstanceID = "rebels";
            AddBlockadingFleet(scene.game, scene.origin);
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            scene.game.AttachNode(officer, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            List<GameResult> results = scene.resultBus.Publish(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = scene.carrier,
                    Context = scene.origin,
                    Reason = UnitDestructionReason.Combat,
                }
            );

            Assert.IsTrue(scene.origin.IsBlockadedFor("empire"));
            Assert.AreSame(scene.origin, officer.GetParent());
            Assert.AreSame(
                officer,
                scene.game.GetSceneNodeByInstanceID<Officer>(officer.InstanceID)
            );
            Assert.IsNull(officer.Movement);
            Assert.IsFalse(officer.IsCaptured);
            Assert.IsEmpty(results.OfType<OfficerCaptureStateResult>());
        }

        [Test]
        public void HandleResults_ScrappedCarrierOverBlockadedFriendlyPlanet_RestoresOfficerLocally()
        {
            var scene = BuildRemovedCarrierScene();
            scene.origin.OwnerInstanceID = "empire";
            scene.fallback.OwnerInstanceID = "rebels";
            AddBlockadingFleet(scene.game, scene.origin);
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            scene.game.AttachNode(officer, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            scene.resultBus.Publish(
                new GameObjectScrappedResult
                {
                    ScrappedObject = scene.carrier,
                    Context = scene.origin,
                }
            );

            Assert.AreSame(scene.origin, officer.GetParent());
            Assert.AreSame(
                officer,
                scene.game.GetSceneNodeByInstanceID<Officer>(officer.InstanceID)
            );
            Assert.IsNull(officer.Movement);
        }

        [Test]
        public void HandleResults_DestroyedCaptorsCarrierOverOfficersPlanet_ReleasesOfficer()
        {
            var scene = BuildRemovedCarrierScene();
            scene.fallback.OwnerInstanceID = "rebels";
            scene.game.CurrentTick = 25;
            Officer officer = EntityFactory.CreateOfficer("officer", "rebels");
            officer.IsCaptured = true;
            officer.CaptorInstanceID = "empire";
            officer.CanEscape = true;
            officer.NextEscapeAttemptTick = 40;
            scene.game.AttachNode(officer, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            List<GameResult> results = scene.resultBus.Publish(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = scene.carrier,
                    Context = scene.origin,
                    Reason = UnitDestructionReason.Combat,
                }
            );

            Assert.AreSame(scene.origin, officer.GetParent());
            Assert.AreSame(
                officer,
                scene.game.GetSceneNodeByInstanceID<Officer>(officer.InstanceID)
            );
            Assert.IsFalse(officer.IsCaptured);
            Assert.IsNull(officer.CaptorInstanceID);
            Assert.IsFalse(officer.CanEscape);
            Assert.AreEqual(0, officer.NextEscapeAttemptTick);
            OfficerCaptureStateResult release = results
                .OfType<OfficerCaptureStateResult>()
                .Single();
            Assert.AreSame(officer, release.TargetOfficer);
            Assert.IsFalse(release.IsCaptured);
            Assert.AreEqual("empire", release.CaptorInstanceID);
            Assert.AreSame(scene.origin, release.Context);
            Assert.AreEqual(25, release.Tick);
        }

        [Test]
        public void HandleResults_DestroyedCaptorsCarrierWithSafeDestination_PreservesCustody()
        {
            var scene = BuildRemovedCarrierScene();
            Officer officer = EntityFactory.CreateOfficer("officer", "rebels");
            officer.IsCaptured = true;
            officer.CaptorInstanceID = "empire";
            scene.game.AttachNode(officer, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            List<GameResult> results = scene.resultBus.Publish(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = scene.carrier,
                    Context = scene.origin,
                    Reason = UnitDestructionReason.Combat,
                }
            );

            Assert.AreSame(scene.fallback, officer.GetParent());
            Assert.IsTrue(officer.IsCaptured);
            Assert.AreEqual("empire", officer.CaptorInstanceID);
            Assert.IsEmpty(results.OfType<OfficerCaptureStateResult>());
        }

        [Test]
        public void HandleResults_DestroyedCarrierOverFriendlyPlanet_RestoresOfficerLocally()
        {
            var scene = BuildRemovedCarrierScene();
            scene.origin.OwnerInstanceID = "empire";
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            scene.game.AttachNode(officer, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            scene.resultBus.Publish(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = scene.carrier,
                    Context = scene.origin,
                    Reason = UnitDestructionReason.Combat,
                }
            );

            Assert.AreSame(scene.origin, officer.GetParent());
            Assert.IsNull(officer.Movement);
        }

        [Test]
        public void HandleResults_DestroyedCarrierWithoutFriendlyDestination_CapturesOfficerLocally()
        {
            var scene = BuildRemovedCarrierScene();
            scene.fallback.OwnerInstanceID = "rebels";
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            scene.game.AttachNode(officer, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            List<GameResult> results = scene.resultBus.Publish(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = scene.carrier,
                    Context = scene.origin,
                    Reason = UnitDestructionReason.Combat,
                }
            );

            Assert.AreSame(scene.origin, officer.GetParent());
            Assert.IsTrue(officer.IsCaptured);
            Assert.AreEqual("rebels", officer.CaptorInstanceID);
            Assert.IsNull(officer.Movement);
            Assert.IsTrue(
                results
                    .OfType<OfficerCaptureStateResult>()
                    .Any(result => ReferenceEquals(result.TargetOfficer, officer))
            );
        }

        [Test]
        public void HandleResults_DestroyedCarrierOverNeutralPlanetWithoutDestination_RetainsOfficerLocally()
        {
            var scene = BuildRemovedCarrierScene();
            scene.origin.OwnerInstanceID = null;
            scene.fallback.OwnerInstanceID = "rebels";
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            scene.game.AttachNode(officer, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            List<GameResult> results = scene.resultBus.Publish(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = scene.carrier,
                    Context = scene.origin,
                    Reason = UnitDestructionReason.Combat,
                }
            );

            Assert.AreSame(
                officer,
                scene.game.GetSceneNodeByInstanceID<Officer>(
                    officer.InstanceID,
                    includeDisabled: true
                )
            );
            Assert.AreSame(scene.origin, officer.GetParent());
            Assert.IsTrue(officer.IsCaptured);
            Assert.IsNull(officer.CaptorInstanceID);
            Assert.IsNull(officer.Movement);
            Assert.IsTrue(
                results
                    .OfType<OfficerCaptureStateResult>()
                    .Any(result => ReferenceEquals(result.TargetOfficer, officer))
            );
        }

        [Test]
        public void HandleResults_CombatDestroyedCarrierWithoutFriendlyDestination_LeavesFighterDestroyed()
        {
            var scene = BuildRemovedCarrierScene();
            scene.fallback.OwnerInstanceID = "rebels";
            scene.carrier.StarfighterCapacity = 1;
            Starfighter fighter = EntityFactory.CreateStarfighter("fighter", "empire");
            fighter.ManufacturingStatus = ManufacturingStatus.Complete;
            fighter.Hyperdrive = 1;
            scene.game.AttachNode(fighter, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            scene.resultBus.Publish(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = scene.carrier,
                    Context = scene.origin,
                    Reason = UnitDestructionReason.Combat,
                }
            );

            Assert.IsNull(
                scene.game.GetSceneNodeByInstanceID<Starfighter>(
                    fighter.InstanceID,
                    includeDisabled: true
                )
            );
            Assert.AreSame(scene.carrier, fighter.GetParent());
        }

        [Test]
        public void HandleResults_DestroyedCarrierAndOfficer_LeavesOfficerDestroyed()
        {
            var scene = BuildRemovedCarrierScene();
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            scene.game.AttachNode(officer, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            scene.resultBus.Publish(
                new GameResult[]
                {
                    new GameObjectDestroyedResult
                    {
                        DestroyedObject = scene.carrier,
                        Context = scene.origin,
                    },
                    new GameObjectDestroyedResult
                    {
                        DestroyedObject = officer,
                        Context = scene.origin,
                    },
                }
            );

            Assert.IsNull(
                scene.game.GetSceneNodeByInstanceID<Officer>(
                    officer.InstanceID,
                    includeDisabled: true
                )
            );
            Assert.AreSame(scene.carrier, officer.GetParent());
        }

        [Test]
        public void HandleResults_ScrappedCarrierWithOfficer_RelocatesOfficer()
        {
            var scene = BuildRemovedCarrierScene();
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            scene.game.AttachNode(officer, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            scene.resultBus.Publish(
                new GameObjectScrappedResult
                {
                    ScrappedObject = scene.carrier,
                    Context = scene.origin,
                }
            );

            Assert.AreSame(scene.fallback, officer.GetParent());
            Assert.IsNotNull(officer.Movement);
        }

        [Test]
        public void HandleResults_ScrappedCarrierWithCarrierInOtherFleet_DoesNotMoveOfficerAcrossFleets()
        {
            var scene = BuildRemovedCarrierScene();
            Fleet otherFleet = EntityFactory.CreateFleet("other-fleet", "empire");
            CapitalShip otherCarrier = new CapitalShip
            {
                InstanceID = "other-carrier",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                CurrentHullStrength = 100,
            };
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            scene.game.AttachNode(otherFleet, scene.fallback);
            scene.game.AttachNode(otherCarrier, otherFleet);
            otherFleet.Movement = TestMovement.Create(
                originPosition: scene.origin.GetPosition(),
                currentPosition: scene.origin.GetPosition()
            );
            scene.game.AttachNode(officer, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            scene.resultBus.Publish(
                new GameObjectScrappedResult
                {
                    ScrappedObject = scene.carrier,
                    Context = scene.origin,
                }
            );

            Assert.AreSame(scene.fallback, officer.GetParent());
            Assert.AreNotSame(otherCarrier, officer.GetParent());
            Assert.IsNotNull(officer.Movement);
        }

        [Test]
        public void HandleResults_AutoscrappedCarrierWithOfficer_RelocatesOfficer()
        {
            var scene = BuildRemovedCarrierScene();
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            scene.game.AttachNode(officer, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            scene.resultBus.Publish(
                new GameObjectAutoscrappedResult
                {
                    DestroyedObject = scene.carrier,
                    Context = scene.origin,
                }
            );

            Assert.AreSame(scene.fallback, officer.GetParent());
            Assert.IsNotNull(officer.Movement);
        }

        [Test]
        public void HandleResults_CombatDestroyedCarrierWithCompletedFighter_RelocatesFighter()
        {
            var scene = BuildRemovedCarrierScene();
            scene.carrier.StarfighterCapacity = 1;
            Starfighter fighter = EntityFactory.CreateStarfighter("fighter", "empire");
            fighter.ManufacturingStatus = ManufacturingStatus.Complete;
            fighter.Hyperdrive = 1;
            scene.game.AttachNode(fighter, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            scene.resultBus.Publish(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = scene.carrier,
                    Context = scene.origin,
                    Reason = UnitDestructionReason.Combat,
                }
            );

            Assert.AreSame(
                fighter,
                scene.game.GetSceneNodeByInstanceID<Starfighter>(
                    fighter.InstanceID,
                    includeDisabled: true
                )
            );
            Assert.AreSame(scene.fallback, fighter.GetParent());
            Assert.IsNotNull(fighter.Movement);
        }

        [Test]
        public void HandleResults_SabotagedCarrierWithCompletedFighter_LeavesFighterDestroyed()
        {
            var scene = BuildRemovedCarrierScene();
            scene.carrier.StarfighterCapacity = 1;
            Starfighter fighter = EntityFactory.CreateStarfighter("fighter", "empire");
            fighter.ManufacturingStatus = ManufacturingStatus.Complete;
            fighter.Hyperdrive = 1;
            scene.game.AttachNode(fighter, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            scene.resultBus.Publish(
                new GameObjectSabotagedResult
                {
                    DestroyedObject = scene.carrier,
                    Context = scene.origin,
                }
            );

            Assert.IsNull(
                scene.game.GetSceneNodeByInstanceID<Starfighter>(
                    fighter.InstanceID,
                    includeDisabled: true
                )
            );
            Assert.AreSame(scene.carrier, fighter.GetParent());
        }

        [Test]
        public void HandleResults_ScrappedCarrierWithCompletedFighter_LeavesFighterDestroyed()
        {
            var scene = BuildRemovedCarrierScene();
            scene.carrier.StarfighterCapacity = 1;
            Starfighter fighter = EntityFactory.CreateStarfighter("fighter", "empire");
            fighter.ManufacturingStatus = ManufacturingStatus.Complete;
            fighter.Hyperdrive = 1;
            scene.game.AttachNode(fighter, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            scene.resultBus.Publish(
                new GameObjectScrappedResult
                {
                    ScrappedObject = scene.carrier,
                    Context = scene.origin,
                }
            );

            Assert.IsNull(
                scene.game.GetSceneNodeByInstanceID<Starfighter>(
                    fighter.InstanceID,
                    includeDisabled: true
                )
            );
            Assert.AreSame(scene.carrier, fighter.GetParent());
        }

        [Test]
        public void HandleResults_AutoscrappedCarrierWithCompletedFighter_LeavesFighterDestroyed()
        {
            var scene = BuildRemovedCarrierScene();
            scene.carrier.StarfighterCapacity = 1;
            Starfighter fighter = EntityFactory.CreateStarfighter("fighter", "empire");
            fighter.ManufacturingStatus = ManufacturingStatus.Complete;
            fighter.Hyperdrive = 1;
            scene.game.AttachNode(fighter, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            scene.resultBus.Publish(
                new GameObjectAutoscrappedResult
                {
                    DestroyedObject = scene.carrier,
                    Context = scene.origin,
                }
            );

            Assert.IsNull(
                scene.game.GetSceneNodeByInstanceID<Starfighter>(
                    fighter.InstanceID,
                    includeDisabled: true
                )
            );
            Assert.AreSame(scene.carrier, fighter.GetParent());
        }

        [Test]
        public void HandleResults_DestroyedCarrierWithUnfinishedFighter_LeavesFighterDestroyed()
        {
            var scene = BuildRemovedCarrierScene();
            Starfighter fighter = EntityFactory.CreateStarfighter("fighter", "empire");
            fighter.ManufacturingStatus = ManufacturingStatus.Building;
            scene.carrier.StarfighterCapacity = 1;
            scene.game.AttachNode(fighter, scene.carrier);
            scene.game.DeleteNode(scene.carrier);

            scene.resultBus.Publish(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = scene.carrier,
                    Context = scene.origin,
                }
            );

            Assert.IsNull(
                scene.game.GetSceneNodeByInstanceID<Starfighter>(
                    fighter.InstanceID,
                    includeDisabled: true
                )
            );
            Assert.AreSame(scene.carrier, fighter.GetParent());
        }

        /// <summary>
        /// Builds a carrier-removal scene with one owned evacuation planet.
        /// </summary>
        /// <returns>The constructed carrier-removal scene.</returns>
        private static (
            GameRoot game,
            Planet origin,
            Planet fallback,
            Fleet fleet,
            CapitalShip carrier,
            MovementCommands movement,
            GameResultBus resultBus
        ) BuildRemovedCarrierScene()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet origin = new Planet
            {
                InstanceID = "origin",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
            };
            Planet fallback = new Planet
            {
                InstanceID = "fallback",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 125,
            };
            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "carrier",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                CurrentHullStrength = 100,
            };
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(origin, sector);
            game.AttachNode(fallback, sector);
            game.AttachNode(fleet, origin);
            game.AttachNode(carrier, fleet);
            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            GameResultBus resultBus = new GameResultBus();
            new MovementObserver(game, movement, new MovementQueries(game)).Connect(resultBus);

            return (game, origin, fallback, fleet, carrier, movement, resultBus);
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
            Planet blockade,
            MovementCommands movement,
            GameResultBus resultBus
        ) BuildBlockadeRetargetingScene()
        {
            GameConfig config = TestConfig.Create();
            config.Blockade.EvacuationLossPercent = 100;
            GameRoot game = TestGame.Create(config);
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

            Planet blockade = blockadedDestination;
            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game),
                new FixedRNG()
            );
            GameResultBus resultBus = new GameResultBus();
            new MovementObserver(game, movement, new MovementQueries(game)).Connect(resultBus);

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
        /// Adds a stationary friendly carrier with the requested capacity.
        /// </summary>
        /// <param name="game">The scene containing the carrier.</param>
        /// <param name="planet">The carrier's planetary system.</param>
        /// <param name="id">The carrier identifier.</param>
        /// <param name="capacity">The fighter capacity.</param>
        /// <returns>The registered carrier.</returns>
        private static CapitalShip AddFriendlyCarrier(
            GameRoot game,
            Planet planet,
            string id,
            int capacity
        )
        {
            Fleet fleet = EntityFactory.CreateFleet(id + "-fleet", "empire");
            CapitalShip ship = new CapitalShip
            {
                InstanceID = id,
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                StarfighterCapacity = capacity,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            return ship;
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
        /// <param name="blockade">The blockaded planet.</param>
        /// <param name="resultBus">The bus that delivers blockade reactions.</param>
        /// <returns>The result of process blockade start.</returns>
        private static List<GameResult> ProcessBlockadeStart(
            GameRoot game,
            Planet blockade,
            GameResultBus resultBus
        )
        {
            Fleet blockadingFleet = blockade
                .GetChildren<Fleet>()
                .First(fleet =>
                    fleet.Movement == null
                    && fleet.OwnerInstanceID == "rebels"
                    && fleet.OwnerInstanceID != blockade.OwnerInstanceID
                    && fleet.HasOperationalCapitalShips()
                );
            return resultBus.Publish(
                new BlockadeChangedResult
                {
                    Planet = blockade,
                    BlockadingFleet = blockadingFleet,
                    Blockaded = true,
                    Tick = game.CurrentTick,
                }
            );
        }
    }
}
