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
using Rebellion.Game.Movement;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class MovementSystemTests
    {
        // Builds a minimal scene: two planets in the same system, an officer parented to
        // the origin planet, and a MovementSystem ready to use.
        private (
            GameRoot game,
            Planet origin,
            Planet destination,
            Officer officer,
            MovementSystem movement
        ) BuildScene()
        {
            GameConfig config = ResourceManager.GetConfig<GameConfig>();
            GameRoot game = new GameRoot(config);

            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet origin = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(origin, system);

            Planet destination = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 100,
            };
            game.AttachNode(destination, system);

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, origin);

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));

            return (game, origin, destination, officer, movement);
        }

        [Test]
        public void Constructor_WithNullGame_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new MovementSystem(null, new FogOfWarSystem(new GameRoot(TestConfig.Create())))
            );

            Assert.AreEqual("game", exception.ParamName);
        }

        [Test]
        public void Constructor_WithNullFogOfWar_ThrowsArgumentNullException()
        {
            GameRoot game = new GameRoot(TestConfig.Create());

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new MovementSystem(game, null)
            );

            Assert.AreEqual("fogOfWar", exception.ParamName);
        }

        [Test]
        public void RequestMove_ValidDestination_ImmediatelyReparentsUnit()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            movement.RequestMove(officer, destination);

            Assert.AreEqual(destination, officer.GetParent());
        }

        [Test]
        public void RequestMove_ValidDestination_UnitIsNoLongerAtOrigin()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            movement.RequestMove(officer, destination);

            Assert.AreNotEqual(origin, officer.GetParent());
        }

        [Test]
        public void RequestMove_ValidDestination_SetsMovementStateWithDestination()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            movement.RequestMove(officer, destination);

            Assert.IsNotNull(officer.Movement);
            Assert.AreEqual(destination, officer.GetParent());
        }

        [Test]
        public void RequestMove_ValidDestination_SetsOriginPositionFromDeparturePlanet()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            Point expectedOrigin = origin.GetPosition();

            movement.RequestMove(officer, destination);

            Assert.AreEqual(expectedOrigin, officer.Movement.OriginPosition);
        }

        [Test]
        public void RequestMove_ValidDestination_SetsTransitTicksGreaterThanZero()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            movement.RequestMove(officer, destination);

            Assert.Greater(officer.Movement.TransitTicks, 0);
        }

        [Test]
        public void RequestMove_WhenUnitAlreadyInTransit_DoesNotRedirect()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            movement.RequestMove(officer, destination);

            MovementState originalMovement = officer.Movement;
            Point originalOrigin = officer.Movement.OriginPosition;

            movement.RequestMove(officer, origin);

            Assert.AreSame(originalMovement, officer.Movement);
            Assert.AreEqual(originalOrigin, officer.Movement.OriginPosition);
            Assert.AreEqual(destination, officer.GetParent());
        }

        [Test]
        public void RequestMove_WhenUnitNotAtAnyPlanet_IsIgnored()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            game.DetachNode(officer);

            Assert.DoesNotThrow(() => movement.RequestMove(officer, destination));
            Assert.IsNull(officer.Movement, "Orphaned unit must not be given a movement state");
        }

        [Test]
        public void RequestMove_WhenDestinationRejectsUnit_DoesNotThrow()
        {
            // Destination ownership changes after scene setup (e.g. enemy captures the planet).
            // RequestMove must not propagate the SceneAccessException — the unit stays at origin.
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            destination.OwnerInstanceID = "rebels";

            Assert.DoesNotThrow(
                () => movement.RequestMove(officer, destination),
                "RequestMove must not throw when the destination rejects the unit"
            );
        }

        [Test]
        public void RequestMove_WhenDestinationRejectsUnit_UnitStaysAtOrigin()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            destination.OwnerInstanceID = "rebels";

            try
            {
                movement.RequestMove(officer, destination);
            }
            catch
            { /* ignored for this assertion */
            }

            Assert.AreEqual(
                origin,
                officer.GetParent(),
                "Unit must remain at origin when destination rejects it"
            );
        }

        [Test]
        public void RequestMove_WhenDestinationRejectsUnit_MovementStateNotSet()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            destination.OwnerInstanceID = "rebels";

            try
            {
                movement.RequestMove(officer, destination);
            }
            catch
            { /* ignored for this assertion */
            }

            Assert.IsNull(
                officer.Movement,
                "Movement state must not be set when the destination rejected the unit"
            );
        }

        [Test]
        public void UpdateMovement_WhenNotInTransit_DoesNothing()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            Assert.DoesNotThrow(() => movement.ProcessTick());
            Assert.IsNull(officer.Movement);
        }

        [Test]
        public void UpdateMovement_InTransit_IncrementsElapsedTicks()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            movement.RequestMove(officer, destination);

            movement.ProcessTick();

            Assert.AreEqual(1, officer.Movement.TicksElapsed);
        }

        [Test]
        public void UpdateMovement_OnArrival_ClearsMovementState()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            movement.RequestMove(officer, destination);
            officer.Movement.TicksElapsed = officer.Movement.TransitTicks;

            List<GameResult> results = movement.ProcessTick();

            Assert.IsNull(officer.Movement);
            Assert.IsTrue(results.OfType<UnitArrivedResult>().Any());
            GameObjectEnrouteResult enroute = results
                .OfType<GameObjectEnrouteResult>()
                .FirstOrDefault();
            Assert.IsNotNull(enroute);
            Assert.AreEqual(officer, enroute.GameObject);
            Assert.IsTrue(results.OfType<GameObjectEnrouteActiveResult>().Any(r => r.IsActive));
            Assert.IsTrue(results.OfType<GameObjectEnrouteActiveResult>().Any(r => !r.IsActive));
        }

        [Test]
        public void UpdateMovement_OnArrival_UnitRemainsAtDestination()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            movement.RequestMove(officer, destination);
            officer.Movement.TicksElapsed = officer.Movement.TransitTicks;

            movement.ProcessTick();

            Assert.AreEqual(destination, officer.GetParent());
        }

        [Test]
        public void UpdateMovement_OfficerArrivesAtMission_ClearsMovementState()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            // Attach a mission to the destination planet so the officer can be sent to it
            AbductionMission mission = new AbductionMission
            {
                InstanceID = "m1",
                OwnerInstanceID = "empire",
                TargetInstanceID = destination.InstanceID,
                HasInitiated = true,
            };
            game.AttachNode(mission, destination);
            mission.MainParticipants.Add(officer);

            movement.RequestMove(officer, mission);
            officer.Movement.TicksElapsed = officer.Movement.TransitTicks;

            List<GameResult> results = movement.ProcessTick();

            Assert.IsNull(officer.Movement, "Movement should be cleared on arrival at a mission");
            Assert.AreEqual(
                mission,
                officer.GetParent(),
                "Officer should remain parented to the mission node, not be rerouted"
            );
            Assert.IsTrue(results.OfType<GameObjectEnrouteResult>().Any());
            Assert.IsTrue(results.OfType<RoleEnrouteActiveResult>().Any(r => r.IsActive));
            RoleEnrouteActiveResult arrived = results
                .OfType<RoleEnrouteActiveResult>()
                .FirstOrDefault(r => !r.IsActive);
            Assert.IsNotNull(arrived);
            Assert.AreEqual(officer, arrived.Participant);
        }

        [Test]
        public void UpdateMovement_SpecialForcesArrivesAtMission_ClearsRoleEnrouteState()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(specialForces, origin);

            AbductionMission mission = new AbductionMission
            {
                InstanceID = "m1",
                OwnerInstanceID = "empire",
                TargetInstanceID = destination.InstanceID,
                HasInitiated = true,
            };
            game.AttachNode(mission, destination);
            mission.MainParticipants.Add(specialForces);

            movement.RequestMove(specialForces, mission);
            specialForces.Movement.TicksElapsed = specialForces.Movement.TransitTicks;

            List<GameResult> results = movement.ProcessTick();

            Assert.IsNull(specialForces.Movement);
            Assert.AreEqual(mission, specialForces.GetParent());
            Assert.IsTrue(
                results
                    .OfType<RoleEnrouteActiveResult>()
                    .Any(r => r.IsActive && r.Participant == specialForces)
            );
            Assert.IsTrue(
                results
                    .OfType<RoleEnrouteActiveResult>()
                    .Any(r => !r.IsActive && r.Participant == specialForces)
            );
        }

        [Test]
        public void RequestMove_CapturedOfficer_IsNotMoved()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            officer.IsCaptured = true;

            movement.RequestMove(officer, destination);

            Assert.AreEqual(
                origin,
                officer.GetParent(),
                "Captured officer must not be moved by its owning faction"
            );
        }

        [Test]
        public void RequestMove_CompletedBuilding_DoesNotMove()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            origin.EnergyCapacity = 5;
            destination.EnergyCapacity = 5;

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, origin);

            movement.RequestMove(building, destination);

            Assert.AreEqual(origin, building.GetParent());
            Assert.IsNull(building.Movement);
        }

        [Test]
        public void RequestMove_GroupNonCapturedUnits_AllMove()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            Officer officer2 = EntityFactory.CreateOfficer("o2", "empire");
            game.AttachNode(officer2, origin);

            movement.RequestMove(
                new System.Collections.Generic.List<IMovable> { officer, officer2 },
                destination
            );

            Assert.AreEqual(destination, officer.GetParent());
            Assert.AreEqual(destination, officer2.GetParent());
        }

        [Test]
        public void RequestMove_GroupUnitsAtDifferentLocations_NoneMove()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            Officer officer2 = EntityFactory.CreateOfficer("o2", "empire");
            game.AttachNode(officer2, destination);

            movement.RequestMove(new List<IMovable> { officer, officer2 }, destination);

            Assert.AreEqual(origin, officer.GetParent());
            Assert.AreEqual(destination, officer2.GetParent());
            Assert.IsNull(officer.Movement);
            Assert.IsNull(officer2.Movement);
        }

        [Test]
        public void RequestMove_GroupUnitAlreadyInTransit_NoneMove()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            Officer movingOfficer = EntityFactory.CreateOfficer("o2", "empire");
            game.AttachNode(movingOfficer, origin);
            movement.RequestMove(movingOfficer, destination);
            MovementState originalMovement = movingOfficer.Movement;

            movement.RequestMove(new List<IMovable> { officer, movingOfficer }, destination);

            Assert.AreEqual(origin, officer.GetParent());
            Assert.AreEqual(destination, movingOfficer.GetParent());
            Assert.AreSame(originalMovement, movingOfficer.Movement);
        }

        [Test]
        public void RequestMove_GroupUnitUnderConstruction_NoneMove()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            Starfighter starfighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            game.AttachNode(starfighter, origin);

            movement.RequestMove(new List<IMovable> { officer, starfighter }, destination);

            Assert.AreEqual(origin, officer.GetParent());
            Assert.AreEqual(origin, starfighter.GetParent());
            Assert.IsNull(officer.Movement);
            Assert.IsNull(starfighter.Movement);
        }

        [Test]
        public void RequestMove_GroupCompletedBuilding_NoneMove()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            origin.EnergyCapacity = 5;
            destination.EnergyCapacity = 5;

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(building, origin);

            movement.RequestMove(new List<IMovable> { officer, building }, destination);

            Assert.AreEqual(origin, officer.GetParent());
            Assert.AreEqual(origin, building.GetParent());
            Assert.IsNull(officer.Movement);
            Assert.IsNull(building.Movement);
        }

        [Test]
        public void RequestMove_GroupCapturedOfficerWithCapturingOfficerEscort_BothMove()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer escort,
                MovementSystem movement
            ) = BuildScene();

            Officer captive = new Officer
            {
                InstanceID = "captive",
                DisplayName = "captive",
                OwnerInstanceID = "rebels",
                IsCaptured = true,
                CaptorInstanceID = "empire",
            };
            game.AttachNode(captive, origin);

            movement.RequestMove(
                new System.Collections.Generic.List<IMovable> { escort, captive },
                destination
            );

            Assert.AreEqual(destination, escort.GetParent(), "Escort should move to destination");
            Assert.AreEqual(destination, captive.GetParent(), "Captive should move with escort");
        }

        [Test]
        public void RequestMove_GroupCapturedOfficerWithoutEscort_NotMoved()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            Officer captive = new Officer
            {
                InstanceID = "captive",
                DisplayName = "captive",
                OwnerInstanceID = "rebels",
                IsCaptured = true,
                CaptorInstanceID = "empire",
            };
            game.AttachNode(captive, origin);

            movement.RequestMove(
                new System.Collections.Generic.List<IMovable> { captive },
                destination
            );

            Assert.AreEqual(
                origin,
                captive.GetParent(),
                "Captive must not move without an escort from the capturing faction"
            );
        }

        [Test]
        public void RequestMove_GroupCapturedOfficerEscortFromWrongFaction_NoneMove()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer escort,
                MovementSystem movement
            ) = BuildScene();

            Officer captive = new Officer
            {
                InstanceID = "captive",
                DisplayName = "captive",
                OwnerInstanceID = "rebels",
                IsCaptured = true,
                CaptorInstanceID = "other",
            };
            game.AttachNode(captive, origin);

            movement.RequestMove(
                new System.Collections.Generic.List<IMovable> { escort, captive },
                destination
            );

            Assert.AreEqual(
                origin,
                escort.GetParent(),
                "Group movement must fail atomically when a captive has no valid escort"
            );
            Assert.AreEqual(
                origin,
                captive.GetParent(),
                "Captive must not move when the escort is from a different faction than the captor"
            );
        }

        [Test]
        public void RequestMove_GroupCapturedOfficerEscortAtDifferentLocation_NoneMove()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer escort,
                MovementSystem movement
            ) = BuildScene();

            Officer captive = new Officer
            {
                InstanceID = "captive",
                DisplayName = "captive",
                OwnerInstanceID = "rebels",
                IsCaptured = true,
                CaptorInstanceID = "empire",
            };
            game.AttachNode(captive, destination);

            movement.RequestMove(
                new System.Collections.Generic.List<IMovable> { escort, captive },
                destination
            );

            Assert.AreEqual(origin, escort.GetParent());
            Assert.AreEqual(destination, captive.GetParent());
        }

        [Test]
        public void UpdateMovement_FleetMovesBeforeUnitArrives_UnitStillEnRoute()
        {
            GameConfig config = ResourceManager.GetConfig<GameConfig>();
            GameRoot game = new GameRoot(config);

            game.Factions.Add(new Faction { InstanceID = "empire" });

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet planetA = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planetA, system);

            Planet planetB = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 100,
            };
            game.AttachNode(planetB, system);

            Planet planetC = new Planet
            {
                InstanceID = "pC",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 500,
                PositionY = 500,
            };
            game.AttachNode(planetC, system);

            // Fleet starts at planet B
            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(fleet, planetB);
            CapitalShip fleetShip = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
            };
            game.AttachNode(fleetShip, fleet);

            // Officer at planet A moves toward the fleet
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, planetA);

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));
            movement.RequestMove(officer, fleet);

            int transitTicks = officer.Movement.TransitTicks;

            // Tick until one tick before arrival
            for (int i = 0; i < transitTicks - 1; i++)
                movement.ProcessTick();

            Assert.IsNotNull(officer.Movement, "Officer should still be in transit.");

            // Fleet moves to planet C the tick before officer would arrive
            movement.RequestMove(fleet, planetC);

            // Tick once more — officer would have arrived at old position
            movement.ProcessTick();

            // Officer should still be en route because the fleet moved
            Assert.IsNotNull(
                officer.Movement,
                "Officer should still be en route after fleet moved away."
            );
        }

        private (
            GameRoot game,
            MovementSystem movement,
            Fleet fleet,
            CapitalShip capitalShip1,
            CapitalShip capitalShip2,
            Starfighter starfighter,
            Regiment regiment,
            Officer officer,
            Planet planetA,
            Planet planetB,
            Planet planetC,
            int fleetTransit,
            int capitalShip2Transit
        ) BuildFleetWithInTransitChildrenScene()
        {
            GameConfig config = ResourceManager.GetConfig<GameConfig>();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet planetA = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planetA, system);

            Planet planetB = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 1,
                PositionY = 0,
            };
            game.AttachNode(planetB, system);

            Planet planetC = new Planet
            {
                InstanceID = "pC",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 10,
                PositionY = 0,
            };
            game.AttachNode(planetC, system);

            // Fleet at A with capitalShip1 carrying a starfighter, regiment, and officer.
            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(fleet, planetA);

            CapitalShip capitalShip1 = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                Hyperdrive = 1,
                StarfighterCapacity = 2,
                RegimentCapacity = 2,
            };
            game.AttachNode(capitalShip1, fleet);

            Starfighter starfighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
            };
            game.AttachNode(starfighter, capitalShip1);

            Regiment regiment = new Regiment { InstanceID = "reg1", OwnerInstanceID = "empire" };
            game.AttachNode(regiment, capitalShip1);

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, capitalShip1);

            // capitalShip2 at planet C will move to the fleet.
            Fleet sourceFleet = EntityFactory.CreateFleet("f2", "empire");
            game.AttachNode(sourceFleet, planetC);
            CapitalShip capitalShip2 = new CapitalShip
            {
                InstanceID = "cs2",
                OwnerInstanceID = "empire",
                Hyperdrive = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(capitalShip2, sourceFleet);

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));

            // Fleet moves A -> B (MinTransitTicks, since A and B are very close).
            movement.RequestMove(fleet, planetB);
            int fleetTransit = fleet.Movement.TransitTicks;

            // capitalShip2 moves from C toward fleet (now at B; C is farther from B than A is, so transit > fleetTransit).
            movement.RequestMove(capitalShip2, fleet);
            int capitalShip2Transit = capitalShip2.Movement.TransitTicks;

            return (
                game,
                movement,
                fleet,
                capitalShip1,
                capitalShip2,
                starfighter,
                regiment,
                officer,
                planetA,
                planetB,
                planetC,
                fleetTransit,
                capitalShip2Transit
            );
        }

        [Test]
        public void UpdateMovement_InTransitFleetWithInTransitChildren_FleetArrivesBeforeChildren()
        {
            (
                GameRoot game,
                MovementSystem movement,
                Fleet fleet,
                CapitalShip capitalShip1,
                CapitalShip capitalShip2,
                Starfighter starfighter,
                Regiment regiment,
                Officer officer,
                Planet planetA,
                Planet planetB,
                Planet planetC,
                int fleetTransit,
                int capitalShip2Transit
            ) scene = BuildFleetWithInTransitChildrenScene();

            Assert.Greater(
                scene.capitalShip2Transit,
                scene.fleetTransit,
                "CS2 must have a longer transit than the fleet for this test to be meaningful."
            );

            // Advance until the fleet arrives.
            for (int i = 0; i < scene.fleetTransit; i++)
                scene.movement.ProcessTick();

            Assert.IsNull(scene.fleet.Movement, "Fleet should have arrived at planet B.");
            Assert.AreEqual(scene.planetB, scene.fleet.GetParent(), "Fleet should be at planet B.");
            Assert.IsNotNull(
                scene.capitalShip2.Movement,
                "CS2 should still be in transit while fleet has arrived."
            );

            // Verify fleet children are intact.
            Assert.AreEqual(
                scene.fleet,
                scene.capitalShip1.GetParent(),
                "CS1 should still be in the fleet."
            );
            Assert.AreEqual(scene.capitalShip1, scene.starfighter.GetParentOfType<CapitalShip>());
            Assert.AreEqual(scene.capitalShip1, scene.regiment.GetParentOfType<CapitalShip>());
            Assert.AreEqual(scene.capitalShip1, scene.officer.GetParent());
        }

        [Test]
        public void UpdateMovement_InTransitFleetWithInTransitChildren_ChildrenArriveAfterFleet()
        {
            (
                GameRoot game,
                MovementSystem movement,
                Fleet fleet,
                CapitalShip capitalShip1,
                CapitalShip capitalShip2,
                Starfighter starfighter,
                Regiment regiment,
                Officer officer,
                Planet planetA,
                Planet planetB,
                Planet planetC,
                int fleetTransit,
                int capitalShip2Transit
            ) scene = BuildFleetWithInTransitChildrenScene();

            Assert.Greater(
                scene.capitalShip2Transit,
                scene.fleetTransit,
                "CS2 must have a longer transit than the fleet for this test to be meaningful."
            );

            // Advance until CS2 also arrives (covers fleet arrival + remaining ticks).
            for (int i = 0; i < scene.capitalShip2Transit; i++)
                scene.movement.ProcessTick();

            Assert.IsNull(scene.fleet.Movement, "Fleet should have arrived at planet B.");
            Assert.IsNull(scene.capitalShip2.Movement, "CS2 should have arrived.");
            Assert.AreEqual(
                scene.fleet,
                scene.capitalShip2.GetParent(),
                "CS2 should be in the fleet."
            );
            Assert.AreEqual(
                scene.planetB,
                scene.fleet.GetParent(),
                "Fleet should still be at planet B."
            );
        }

        [Test]
        public void RequestMove_BuildingUnderConstruction_RetargetsDestination()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            origin.EnergyCapacity = 5;
            destination.EnergyCapacity = 5;

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            game.AttachNode(building, origin);

            movement.RequestMove(building, destination);

            Assert.AreEqual(
                destination,
                building.GetParent(),
                "Building destination should change while it is still under construction."
            );
            Assert.IsNull(
                building.Movement,
                "Building should have no movement state while its manufacturing destination changes."
            );
        }

        [Test]
        public void RequestMove_StarfighterUnderConstruction_RetargetsDestination()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planet, system);

            Planet otherPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(otherPlanet, system);

            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(destFleet, planet);
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 2,
            };
            game.AttachNode(carrier, destFleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));
            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, fighter, destFleet);

            Assert.AreEqual(ManufacturingStatus.Building, fighter.ManufacturingStatus);
            movement.RequestMove(fighter, otherPlanet);

            Assert.AreEqual(
                otherPlanet,
                fighter.GetParent(),
                "Fighter destination should change while it is still under construction."
            );
            Assert.IsNull(
                fighter.Movement,
                "Fighter should have no movement state while its manufacturing destination changes."
            );
        }

        [Test]
        public void RequestMove_CapitalShipToFleet_LandsAtFleet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet planetA = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planetA, system);

            Planet planetB = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(planetB, system);

            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                Hyperdrive = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Fleet sourceFleet = EntityFactory.CreateFleet("f0", "empire");
            game.AttachNode(sourceFleet, planetA);
            game.AttachNode(capitalShip, sourceFleet);

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(fleet, planetB);

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));
            movement.RequestMove(capitalShip, fleet);

            // Tick until transit completes.
            int transit = capitalShip.Movement.TransitTicks;
            for (int i = 0; i < transit; i++)
                movement.ProcessTick();

            Assert.IsNull(
                capitalShip.Movement,
                "Capital ship should have no movement state after arrival."
            );
            Assert.AreEqual(
                fleet,
                capitalShip.GetParent(),
                "Capital ship should be in the destination fleet."
            );
            Assert.AreEqual(planetB, fleet.GetParent(), "Fleet should still be at planet B.");
        }

        [Test]
        public void UpdateMovement_BuildingInTransitDestinationChangedSides_BuildingDestroyed()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet originPlanet = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(originPlanet, system);

            Planet destPlanet = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(destPlanet, system);

            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            game.AttachNode(mine, destPlanet);

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));
            movement.RequestMove(mine, destPlanet, originPlanet);

            // Destination captured while building is in transit.
            destPlanet.OwnerInstanceID = "rebels";

            int transit = mine.Movement.TransitTicks;
            List<GameResult> allResults = new List<GameResult>();
            for (int i = 0; i < transit; i++)
                allResults.AddRange(movement.ProcessTick());

            Assert.IsNull(
                game.GetSceneNodeByInstanceID<Building>("mine1"),
                "Building should be destroyed when destination changes sides during transit."
            );
            Assert.IsTrue(allResults.OfType<GameObjectDestroyedOnArrivalResult>().Any());
        }

        [Test]
        public void UpdateMovement_NonBuildingInTransitDestinationChangedSides_UnitRerouted()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet originPlanet = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(originPlanet, system);

            Planet destPlanet = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(destPlanet, system);

            Regiment regiment = new Regiment
            {
                InstanceID = "rg1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, destPlanet);

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));
            movement.RequestMove(regiment, destPlanet, originPlanet);

            // Destination captured while regiment is in transit.
            destPlanet.OwnerInstanceID = "rebels";

            int transit = regiment.Movement.TransitTicks;
            for (int i = 0; i < transit; i++)
                movement.ProcessTick();

            // Regiment should be rerouted to nearest friendly planet (originPlanet).
            Assert.AreEqual(
                originPlanet,
                regiment.GetParent(),
                "Regiment should reroute to nearest friendly planet."
            );
        }

        [Test]
        public void UpdateMovement_FleetInTransitToHostilePlanet_FleetArrivesAtHostilePlanet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet originPlanet = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(originPlanet, system);

            Planet hostilePlanet = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(hostilePlanet, system);

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                Hyperdrive = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, originPlanet);
            game.AttachNode(capitalShip, fleet);

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));
            movement.RequestMove(fleet, hostilePlanet);

            int transit = fleet.Movement.TransitTicks;
            List<GameResult> allResults = new List<GameResult>();
            for (int i = 0; i < transit; i++)
                allResults.AddRange(movement.ProcessTick());

            Assert.IsNull(fleet.Movement, "Fleet should complete arrival at the hostile planet.");
            Assert.AreEqual(hostilePlanet, fleet.GetParent());
            Assert.IsTrue(
                allResults.OfType<UnitArrivedResult>().Any(result => result.Unit == fleet)
            );
        }

        [Test]
        public void UpdateMovement_RegimentInTransitToFriendlyFleetAtHostilePlanet_ArrivesInFleet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet originPlanet = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(originPlanet, system);

            Planet hostilePlanet = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(hostilePlanet, system);

            Fleet hostileOrbitFleet = EntityFactory.CreateFleet("f1", "empire");
            CapitalShip receivingShip = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                Hyperdrive = 1,
                RegimentCapacity = 4,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(hostileOrbitFleet, hostilePlanet);
            game.AttachNode(receivingShip, hostileOrbitFleet);

            Fleet sourceFleet = EntityFactory.CreateFleet("f0", "empire");
            CapitalShip sourceShip = new CapitalShip
            {
                InstanceID = "cs0",
                OwnerInstanceID = "empire",
                Hyperdrive = 1,
                RegimentCapacity = 4,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Regiment regiment = EntityFactory.CreateRegiment("r1", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(sourceFleet, originPlanet);
            game.AttachNode(sourceShip, sourceFleet);
            game.AttachNode(regiment, sourceShip);

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));
            movement.RequestMove(regiment, hostileOrbitFleet);

            int transit = regiment.Movement.TransitTicks;
            List<GameResult> allResults = new List<GameResult>();
            for (int i = 0; i < transit; i++)
                allResults.AddRange(movement.ProcessTick());

            Assert.IsNull(
                regiment.Movement,
                "Regiment should complete arrival into the friendly fleet at the hostile planet."
            );
            Assert.AreEqual(receivingShip, regiment.GetParent());
            Assert.IsTrue(
                allResults.OfType<UnitArrivedResult>().Any(result => result.Unit == regiment)
            );
        }

        [Test]
        public void RequestMove_CapitalShipInFriendlyFleetOverHostilePlanet_StartsTransit()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet productionPlanet = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(productionPlanet, system);

            Planet capturedPlanet = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(capturedPlanet, system);

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(fleet, capturedPlanet);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                Hyperdrive = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(ship, fleet);

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));
            movement.RequestMove(ship, fleet, productionPlanet);

            Assert.IsNotNull(
                ship.Movement,
                "Capital ship should travel to its assigned fleet over a hostile planet."
            );
        }

        [Test]
        public void RequestMove_CapitalShipInFleetDestinationCaptured_ShipRemainsInFleet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet productionPlanet = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(productionPlanet, system);

            Planet capturedPlanet = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(capturedPlanet, system);

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(fleet, capturedPlanet);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                Hyperdrive = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(ship, fleet);

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));
            movement.RequestMove(ship, fleet, productionPlanet);

            Assert.AreEqual(
                fleet,
                ship.GetParent(),
                "Capital ship should remain in its fleet when destination planet is captured"
            );
        }

        [Test]
        public void RequestMove_ManufacturedUnitDestinationWithoutPlanet_Throws()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(ship, fleet);

            Assert.Throws<System.InvalidOperationException>(() =>
                movement.RequestMove(ship, fleet, origin)
            );
            Assert.IsNull(ship.Movement);
        }

        [Test]
        public void RequestMove_OfficerOnCapitalShipInFleet_CanMoveToMission()
        {
            GameConfig config = ResourceManager.GetConfig<GameConfig>();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet origin = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(origin, system);

            Planet missionPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(missionPlanet, system);

            Fleet fleet = new Fleet { InstanceID = "fl1", OwnerInstanceID = "empire" };
            game.AttachNode(fleet, origin);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(ship, fleet);

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, ship);

            StubMission mission = EntityFactory.CreateMission("m1", "empire", "p2");
            game.AttachNode(mission, missionPlanet);

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));

            Assert.DoesNotThrow(
                () => movement.RequestMove(officer, mission),
                "Officer on a CapitalShip should be movable to a mission"
            );
        }

        #region Blockade Evacuation Losses

        private (
            GameRoot game,
            Planet origin,
            Planet destination,
            MovementSystem movement
        ) BuildBlockadeScene(IRandomNumberProvider rng)
        {
            GameConfig config = TestConfig.Create();
            config.Blockade.EvacuationLossPercent = 50;
            GameRoot game = new GameRoot(config);

            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet origin = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(origin, system);

            Planet destination = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 100,
            };
            game.AttachNode(destination, system);

            // Hostile fleet creates the blockade
            Fleet hostile = EntityFactory.CreateFleet("hostile", "rebels");
            game.AttachNode(hostile, origin);

            Assert.IsTrue(origin.IsBlockaded());

            BlockadeSystem blockade = new BlockadeSystem(game, rng);
            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game), blockade);

            return (game, origin, destination, movement);
        }

        [Test]
        public void RequestMove_RegimentFromBlockadedPlanet_LowRoll_DestroysRegiment()
        {
            // FixedRNG returns 0 -> 0 < 50 -> loss
            (GameRoot game, Planet origin, Planet destination, MovementSystem movement) =
                BuildBlockadeScene(new FixedRNG());

            Regiment regiment = EntityFactory.CreateRegiment("r1", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, origin);

            movement.RequestMove(regiment, destination);

            Assert.IsNull(
                game.GetSceneNodeByInstanceID<Regiment>("r1"),
                "Regiment should be destroyed running the blockade"
            );
        }

        [Test]
        public void RequestMove_RegimentFromBlockadedPlanet_HighRoll_RegimentSurvives()
        {
            // MaxRNG returns 99 -> 99 >= 50 -> survives
            (GameRoot game, Planet origin, Planet destination, MovementSystem movement) =
                BuildBlockadeScene(new MaxRNG());

            Regiment regiment = EntityFactory.CreateRegiment("r1", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, origin);

            movement.RequestMove(regiment, destination);

            Assert.IsNotNull(
                game.GetSceneNodeByInstanceID<Regiment>("r1"),
                "Regiment should survive the blockade"
            );
            Assert.IsNotNull(regiment.Movement, "Surviving regiment should be in transit");
        }

        [Test]
        public void RequestMove_RegimentFromBlockadedPlanet_EmitsEvacuationResult()
        {
            (GameRoot game, Planet origin, Planet destination, MovementSystem movement) =
                BuildBlockadeScene(new FixedRNG());

            Regiment regiment = EntityFactory.CreateRegiment("r1", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, origin);

            movement.RequestMove(regiment, destination);

            // Evacuation results are pending — flush via ProcessTick
            List<GameResult> results = movement.ProcessTick();

            EvacuationLossesResult evacResult = results
                .OfType<EvacuationLossesResult>()
                .FirstOrDefault();
            Assert.IsNotNull(evacResult, "Should emit EvacuationLossesResult");
            Assert.AreEqual(origin, evacResult.Location);
            Assert.AreEqual(1, evacResult.LostRegiments.Count);
        }

        [Test]
        public void RequestMove_RegimentFromUnblockedPlanet_NoEvacuationLoss()
        {
            // FixedRNG would cause loss, but planet isn't blockaded
            GameConfig config = TestConfig.Create();
            config.Blockade.EvacuationLossPercent = 100;
            GameRoot game = new GameRoot(config);

            game.Factions.Add(new Faction { InstanceID = "empire" });

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet origin = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                PositionX = 0,
                PositionY = 0,
            };
            Planet destination = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "empire",
                PositionX = 100,
                PositionY = 100,
            };
            game.AttachNode(origin, system);
            game.AttachNode(destination, system);

            Regiment regiment = EntityFactory.CreateRegiment("r1", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, origin);

            Assert.IsFalse(origin.IsBlockaded());

            BlockadeSystem blockade = new BlockadeSystem(game, new FixedRNG());
            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game), blockade);

            movement.RequestMove(regiment, destination);

            Assert.IsNotNull(
                game.GetSceneNodeByInstanceID<Regiment>("r1"),
                "Regiment on unblockaded planet should never be destroyed"
            );
        }

        [Test]
        public void RequestMove_OfficerFromBlockadedPlanet_NotAffected()
        {
            (GameRoot game, Planet origin, Planet destination, MovementSystem movement) =
                BuildBlockadeScene(new FixedRNG());

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, origin);

            movement.RequestMove(officer, destination);

            Assert.IsNotNull(
                game.GetSceneNodeByInstanceID<Officer>("o1"),
                "Officers should not be affected by evacuation losses"
            );
        }

        #endregion

        [Test]
        public void ProcessTick_BuildingArrivesAtUncolonizedPlanet_MarksItColonized()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();

            origin.EnergyCapacity = 5;
            destination.EnergyCapacity = 5;
            destination.IsColonized = false;

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            // Stage the building scene-parented to the destination (the arrival pipeline
            // expects this) and ready to complete on the next tick.
            game.AttachNode(building, destination);
            building.Movement = new MovementState
            {
                TransitTicks = 1,
                TicksElapsed = 1,
                OriginPosition = origin.GetPosition(),
                CurrentPosition = origin.GetPosition(),
            };

            movement.ProcessTick();

            Assert.IsTrue(
                destination.IsColonized,
                "Building arrival at an uncolonized planet should colonize it"
            );
            Assert.IsNull(building.Movement, "Building should have completed its transit");
        }

        [Test]
        public void ProcessTick_FleetArrivesAtPlanet_MarksFactionAsVisitor()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();

            // Precondition: destination has not yet been visited by anyone.
            Assert.IsFalse(destination.WasVisitedBy("empire"));

            Fleet fleet = new Fleet("empire", "Empire Fleet");
            game.AttachNode(fleet, destination);
            fleet.Movement = new MovementState
            {
                TransitTicks = 1,
                TicksElapsed = 1,
                OriginPosition = origin.GetPosition(),
                CurrentPosition = origin.GetPosition(),
            };

            movement.ProcessTick();

            Assert.IsTrue(
                destination.WasVisitedBy("empire"),
                "Fleet arrival should mark the faction as a visitor of the destination planet"
            );
        }

        [Test]
        public void ProcessTick_FleetArrivesAtNeutralPlanet_CompletesAndMarksVisitor()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();

            destination.SetOwnerInstanceID(null);
            destination.IsColonized = false;

            Fleet fleet = new Fleet("empire", "Empire Fleet");
            game.AttachNode(fleet, destination);
            fleet.Movement = new MovementState
            {
                TransitTicks = 1,
                TicksElapsed = 1,
                OriginPosition = origin.GetPosition(),
                CurrentPosition = origin.GetPosition(),
            };

            movement.ProcessTick();

            Assert.IsNull(
                fleet.Movement,
                "Fleet should complete arrival at a neutral planet, clearing its movement state"
            );
            Assert.IsTrue(
                destination.WasVisitedBy("empire"),
                "Arrival at a neutral planet must record the visitor for first-contact tracking"
            );
        }

        [Test]
        public void ProcessTick_OfficerArrivesAtPlanet_MarksFactionAsVisitor()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            game.DetachNode(officer);
            game.AttachNode(officer, destination);
            officer.Movement = new MovementState
            {
                TransitTicks = 1,
                TicksElapsed = 1,
                OriginPosition = origin.GetPosition(),
                CurrentPosition = origin.GetPosition(),
            };

            movement.ProcessTick();

            Assert.IsTrue(
                destination.WasVisitedBy("empire"),
                "Officer arrival should mark the faction as a visitor of the destination planet"
            );
        }

        [Test]
        public void ProcessTick_FleetArrivesAtAlreadyVisitedPlanet_DoesNotDuplicate()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            destination.AddVisitor("empire");
            int countBefore = destination.VisitingFactionIDs.Count;

            Fleet fleet = new Fleet("empire", "Empire Fleet");
            game.AttachNode(fleet, destination);
            fleet.Movement = new MovementState
            {
                TransitTicks = 1,
                TicksElapsed = 1,
                OriginPosition = origin.GetPosition(),
                CurrentPosition = origin.GetPosition(),
            };

            movement.ProcessTick();

            Assert.AreEqual(
                countBefore,
                destination.VisitingFactionIDs.Count,
                "Repeat arrivals must not duplicate visitor entries"
            );
        }

        /// <summary>
        /// Builds a fleet of the given faction parked over the given planet, holding one
        /// idle regiment ready to drop. The planet has the faction as a visitor.
        /// </summary>
        private (Fleet fleet, Regiment regiment) StageFleetWithRegimentAt(
            GameRoot game,
            Planet planet,
            string factionId
        )
        {
            planet.AddVisitor(factionId);

            Fleet fleet = new Fleet(factionId, $"{factionId}-fleet");
            game.AttachNode(fleet, planet);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = $"{factionId}-ship-{planet.InstanceID}",
                OwnerInstanceID = factionId,
                AllowedOwnerInstanceIDs = new List<string> { factionId },
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 4,
            };
            game.AttachNode(ship, fleet);

            Regiment regiment = new Regiment
            {
                InstanceID = $"{factionId}-reg-{planet.InstanceID}",
                OwnerInstanceID = factionId,
                AllowedOwnerInstanceIDs = new List<string> { factionId },
                ManufacturingStatus = ManufacturingStatus.Complete,
                Movement = null,
            };
            game.AttachNode(regiment, ship);

            return (fleet, regiment);
        }

        [Test]
        public void RequestMove_RegimentToNeutralUncolonizedPlanet_ClaimsImmediately()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            destination.OwnerInstanceID = null;
            destination.IsColonized = false;

            (Fleet _, Regiment regiment) = StageFleetWithRegimentAt(game, destination, "empire");

            movement.RequestMove(regiment, destination);

            Assert.AreEqual("empire", destination.GetOwnerInstanceID());
            Assert.AreEqual(100, destination.GetPopularSupport("empire"));
            Assert.AreEqual(destination, regiment.GetParent());
        }

        [Test]
        public void RequestMove_RegimentToNeutralUncolonizedPlanet_HiddenObserverSnapshot_NotRefreshed()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            Faction observer = AddFaction(game, "observer");
            destination.OwnerInstanceID = null;
            destination.IsColonized = false;
            destination.EnergyCapacity = 1;

            CapturePlanetSnapshot(game, observer, destination, 5);
            AddBuilding(game, destination, "hidden-regiment-claim-building", "rebels");
            (Fleet _, Regiment regiment) = StageFleetWithRegimentAt(game, destination, "empire");

            game.CurrentTick = 20;
            movement.RequestMove(regiment, destination);

            PlanetSnapshot snapshot = GetPlanetSnapshot(observer, destination);
            Assert.AreEqual(5, snapshot.TickCaptured);
            Assert.IsNull(snapshot.OwnerInstanceID);
            Assert.AreEqual(0, snapshot.Buildings.Count);
        }

        [Test]
        public void RequestMove_RegimentToNeutralColonizedPlanet_IsRejected()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            destination.OwnerInstanceID = null;
            destination.IsColonized = true;

            (Fleet _, Regiment regiment) = StageFleetWithRegimentAt(game, destination, "empire");

            movement.RequestMove(regiment, destination);

            Assert.IsNull(destination.GetOwnerInstanceID());
            Assert.AreNotEqual(destination, regiment.GetParent());
        }

        [Test]
        public void RequestMove_LastRegimentOffUncolonizedOwnedPlanet_DoesNotImmediatelyReleaseToNeutral()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            destination.OwnerInstanceID = "empire";
            destination.IsColonized = false;

            // Garrison the planet directly, then stage an empty fleet to receive the regiment.
            Regiment regiment = new Regiment
            {
                InstanceID = "garrison-reg",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, destination);

            Fleet fleet = new Fleet("empire", "pickup-fleet");
            game.AttachNode(fleet, destination);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "pickup-ship",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 4,
            };
            game.AttachNode(ship, fleet);

            movement.RequestMove(regiment, ship);

            Assert.AreEqual("empire", destination.GetOwnerInstanceID());
        }

        [Test]
        public void RequestMove_LastRegimentOffColonizedOwnedPlanet_OwnershipPersists()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            destination.OwnerInstanceID = "empire";
            destination.IsColonized = true;

            Regiment regiment = new Regiment
            {
                InstanceID = "garrison-reg",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, destination);

            Fleet fleet = new Fleet("empire", "pickup-fleet");
            game.AttachNode(fleet, destination);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "pickup-ship",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 4,
            };
            game.AttachNode(ship, fleet);

            movement.RequestMove(regiment, ship);

            Assert.AreEqual("empire", destination.GetOwnerInstanceID());
        }

        private static Faction AddFaction(GameRoot game, string instanceId)
        {
            Faction faction = new Faction { InstanceID = instanceId, DisplayName = instanceId };
            game.Factions.Add(faction);
            return faction;
        }

        private static Building AddBuilding(
            GameRoot game,
            Planet planet,
            string instanceId,
            string ownerInstanceId
        )
        {
            Building building = new Building
            {
                InstanceID = instanceId,
                OwnerInstanceID = planet.GetOwnerInstanceID(),
                AllowedOwnerInstanceIDs = new List<string> { ownerInstanceId },
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            game.AttachNode(building, planet);

            if (building.GetOwnerInstanceID() != ownerInstanceId)
                game.ChangeUnitOwnership(building, ownerInstanceId);

            return building;
        }

        private static void CapturePlanetSnapshot(
            GameRoot game,
            Faction faction,
            Planet planet,
            int tick
        )
        {
            PlanetSystem system = planet.GetParentOfType<PlanetSystem>();
            new FogOfWarSystem(game).CaptureSnapshot(faction, planet, system, tick);
        }

        private static PlanetSnapshot GetPlanetSnapshot(Faction faction, Planet planet)
        {
            PlanetSystem system = planet.GetParentOfType<PlanetSystem>();
            return faction.Fog.Snapshots[system.InstanceID].Planets[planet.InstanceID];
        }
    }
}
