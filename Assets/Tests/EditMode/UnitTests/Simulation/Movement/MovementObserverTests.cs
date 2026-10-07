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
                BlockadeTracker blockade,
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
            foreach (IMovable unit in units)
                unit.Movement.CurrentPosition = new Point(10, 0);

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
                BlockadeTracker blockade,
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
                BlockadeTracker blockade,
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
                BlockadeTracker blockade,
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
                BlockadeTracker blockade,
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
                BlockadeTracker blockade,
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
            starfighter.Movement.CurrentPosition = new Point(100, 0);

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
                BlockadeTracker blockade,
                MovementCommands movement,
                GameResultBus resultBus
            ) scene = BuildBlockadeRetargetingScene();
            Starfighter starfighter = EntityFactory.CreateStarfighter("fighter", "empire");
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(starfighter, scene.origin);
            scene.movement.RequestMove(starfighter, scene.blockadedDestination);
            starfighter.Movement.CurrentPosition = new Point(140, 0);
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
            otherFleet.Movement = new MovementState
            {
                OriginPosition = scene.origin.GetPosition(),
                CurrentPosition = scene.origin.GetPosition(),
            };
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
            otherFleet.Movement = new MovementState
            {
                OriginPosition = scene.origin.GetPosition(),
                CurrentPosition = scene.origin.GetPosition(),
            };
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
            BlockadeTracker blockade,
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

            BlockadeTracker blockade = new BlockadeTracker(game);
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
            BlockadeTracker blockade,
            GameResultBus resultBus
        )
        {
            return resultBus.Publish(new BlockadeTickProcessor(blockade).ProcessTick(game));
        }
    }
}
