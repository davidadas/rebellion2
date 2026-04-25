using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
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
        public void RequestMove_WhenUnitAlreadyInTransit_RedirectsFromCurrentPosition()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            movement.RequestMove(officer, destination);

            Point midPoint = new Point(50, 0);
            officer.Movement.CurrentPosition = midPoint;

            movement.RequestMove(officer, origin);

            Assert.AreEqual(
                midPoint,
                officer.Movement.OriginPosition,
                "Redirected unit must start its new journey from its current visual position"
            );
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
            Assert.AreEqual(officer, arrived.Officer);
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
        public void RequestGroupMove_NonCapturedUnits_AllMove()
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

            movement.RequestGroupMove(
                new System.Collections.Generic.List<IMovable> { officer, officer2 },
                destination
            );

            Assert.AreEqual(destination, officer.GetParent());
            Assert.AreEqual(destination, officer2.GetParent());
        }

        [Test]
        public void RequestGroupMove_CapturedOfficerWithEscortFromCaptor_BothMove()
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

            movement.RequestGroupMove(
                new System.Collections.Generic.List<IMovable> { escort, captive },
                destination
            );

            Assert.AreEqual(destination, escort.GetParent(), "Escort should move to destination");
            Assert.AreEqual(destination, captive.GetParent(), "Captive should move with escort");
        }

        [Test]
        public void RequestGroupMove_CapturedOfficerWithoutEscort_NotMoved()
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

            movement.RequestGroupMove(
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
        public void RequestGroupMove_CapturedOfficerEscortFromWrongFaction_NotMoved()
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

            movement.RequestGroupMove(
                new System.Collections.Generic.List<IMovable> { escort, captive },
                destination
            );

            Assert.AreEqual(
                origin,
                captive.GetParent(),
                "Captive must not move when the escort is from a different faction than the captor"
            );
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
        public void RequestMove_UnitUnderConstruction_IsNotMoved()
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
            mfg.Enqueue(planet, fighter, destFleet, ignoreCost: true);

            Assert.AreEqual(ManufacturingStatus.Building, fighter.ManufacturingStatus);
            ISceneNode originalParent = fighter.GetParent();

            // Attempting to move a unit under construction should be rejected.
            movement.RequestMove(fighter, otherPlanet);

            Assert.AreEqual(
                originalParent,
                fighter.GetParent(),
                "Fighter should not move while under construction."
            );
            Assert.IsNull(
                fighter.Movement,
                "Fighter should have no movement state while under construction."
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
        public void RequestMove_CapitalShipInFleetDestinationCaptured_MovementCleared()
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

            Assert.IsNull(
                ship.Movement,
                "Capital ship in a fleet should have movement cleared when destination planet is captured"
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
            // Stage the building as if it had just left origin: its scene parent is the
            // destination (the arrival pipeline expects this), with one tick remaining.
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
    }
}
