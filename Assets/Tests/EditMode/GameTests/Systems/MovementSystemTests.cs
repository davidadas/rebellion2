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
using Rebellion.Game.Requests;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Sectors
{
    [TestFixture]
    public class MovementSystemTests
    {
        [Test]
        public void RelocateUnits_NoCompatibleShip_MovesStarfighterToFriendlyPlanet()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(faction);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            Planet combatPlanet = new Planet { InstanceID = "combat" };
            Planet friendlyPlanet = new Planet
            {
                InstanceID = "friendly",
                OwnerInstanceID = faction.InstanceID,
                IsColonized = true,
            };
            game.AttachNode(combatPlanet, sector);
            game.AttachNode(friendlyPlanet, sector);
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = faction.InstanceID };
            CapitalShip destroyedShip = new CapitalShip
            {
                InstanceID = "destroyed",
                OwnerInstanceID = faction.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                StarfighterCapacity = 1,
            };
            CapitalShip unfinishedCarrier = new CapitalShip
            {
                InstanceID = "unfinished",
                OwnerInstanceID = faction.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Building,
                CurrentHullStrength = 100,
                StarfighterCapacity = 1,
            };
            Starfighter starfighter = new Starfighter
            {
                InstanceID = "fighter",
                OwnerInstanceID = faction.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, combatPlanet);
            game.AttachNode(destroyedShip, fleet);
            game.AttachNode(unfinishedCarrier, fleet);
            game.AttachNode(starfighter, destroyedShip);
            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );

            movement.RelocateUnits(new[] { starfighter });

            Assert.AreSame(friendlyPlanet, starfighter.GetParent());
        }

        [Test]
        public void Constructor_WithNullGame_ThrowsArgumentNullException()
        {
            GameRoot dependencyGame = new GameRoot(TestConfig.Create());

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new MovementSystem(
                    null,
                    new FogOfWarSystem(dependencyGame),
                    new FleetSystem(dependencyGame)
                )
            );

            Assert.AreEqual("game", exception.ParamName);
        }

        [Test]
        public void Constructor_WithNullFogOfWar_ThrowsArgumentNullException()
        {
            GameRoot game = new GameRoot(TestConfig.Create());

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new MovementSystem(game, null, new FleetSystem(game))
            );

            Assert.AreEqual("fogOfWar", exception.ParamName);
        }

        [Test]
        public void Constructor_WithNullFleetSystem_ThrowsArgumentNullException()
        {
            GameRoot game = new GameRoot(TestConfig.Create());

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new MovementSystem(game, new FogOfWarSystem(game), null)
            );

            Assert.AreEqual("fleetSystem", exception.ParamName);
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
        public void RequestMove_SameSectorDestination_CanUseLocalTransitMinimum()
        {
            GameConfig config = TestContent.Data.GameConfig;
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });

            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet origin = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(origin, sector);

            Planet destination = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 10,
                PositionY = 0,
            };
            game.AttachNode(destination, sector);

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, origin);

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );

            movement.RequestMove(officer, destination);

            Assert.Less(officer.Movement.TransitTicks, config.Movement.MinTransitTicks);
            Assert.GreaterOrEqual(
                officer.Movement.TransitTicks,
                config.Movement.SameSectorMinTransitTicks
            );
        }

        [Test]
        public void RequestMove_DifferentSystemDestination_UsesGlobalTransitMinimum()
        {
            GameConfig config = TestContent.Data.GameConfig;
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });

            PlanetSector originSector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(originSector, game.GetGalaxyMap());

            Planet origin = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(origin, originSector);

            PlanetSector destinationSector = new PlanetSector { InstanceID = "sector2" };
            game.AttachNode(destinationSector, game.GetGalaxyMap());

            Planet destination = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 10,
                PositionY = 0,
            };
            game.AttachNode(destination, destinationSector);

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, origin);

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );

            movement.RequestMove(officer, destination);

            Assert.AreEqual(config.Movement.MinTransitTicks, officer.Movement.TransitTicks);
        }

        [Test]
        public void RequestMove_ValidDestination_SetsMovementGroupID()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            movement.RequestMove(officer, destination);

            Assert.IsFalse(string.IsNullOrEmpty(officer.Movement.MovementGroupID));
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
        public void RequestMove_CapturedOfficerAtSamePlanet_ReparentsWithoutMovement()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            Mission mission = new SabotageMission
            {
                InstanceID = "m1",
                OwnerInstanceID = "empire",
                LocationInstanceID = origin.InstanceID,
                HasInitiated = true,
            };
            game.AttachNode(mission, origin);
            mission.AddChild(officer);
            game.MoveNode(officer, mission);
            officer.IsCaptured = true;
            officer.CaptorInstanceID = "rebels";

            movement.RequestMove(officer, origin);

            Assert.AreEqual(origin, officer.GetParent());
            Assert.IsNull(officer.Movement);
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

            movement.RequestMove(new List<IMovable> { officer, officer2 }, destination);

            Assert.AreEqual(destination, officer.GetParent());
            Assert.AreEqual(destination, officer2.GetParent());
        }

        [Test]
        public void RequestMove_GroupNonCapturedUnits_SetsSharedMovementGroupID()
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

            movement.RequestMove(new List<IMovable> { officer, officer2 }, destination);

            Assert.IsFalse(string.IsNullOrEmpty(officer.Movement.MovementGroupID));
            Assert.AreEqual(officer.Movement.MovementGroupID, officer2.Movement.MovementGroupID);
        }

        [Test]
        public void RequestMove_GroupToFactionViewFleet_BoardsLiveFleet()
        {
            (GameRoot game, Planet origin, Planet _, Officer officer, MovementSystem movement) =
                BuildScene();

            Officer officer2 = EntityFactory.CreateOfficer("o2", "empire");
            game.AttachNode(officer2, origin);

            Fleet liveFleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(liveFleet, origin);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(ship, liveFleet);

            Fleet viewFleet = EntityFactory.CreateFleet(liveFleet.InstanceID, "empire");

            movement.RequestMove(new List<IMovable> { officer, officer2 }, viewFleet);

            Assert.AreEqual(ship, officer.GetParent());
            Assert.AreEqual(ship, officer2.GetParent());
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
        public void RequestMove_GroupUnitUnderConstruction_RetargetsDelivery()
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

            Assert.AreEqual(destination, officer.GetParent());
            Assert.AreEqual(destination, starfighter.GetParent());
            Assert.IsNotNull(officer.Movement);
            Assert.IsNull(starfighter.Movement);
            Assert.AreEqual(ManufacturingStatus.Building, starfighter.ManufacturingStatus);
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
        public void RequestMove_GroupCapturedOfficerWithCapturingSpecialForcesEscort_BothMove()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            SpecialForces escort = new SpecialForces
            {
                InstanceID = "escort",
                DisplayName = "escort",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(escort, origin);

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
        public void RequestMove_GroupCapturedOfficerWithCapturingRegimentEscort_DoesNotMove()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();

            Regiment escort = new Regiment
            {
                InstanceID = "escort",
                DisplayName = "escort",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(escort, origin);

            Officer captive = new Officer
            {
                InstanceID = "captive",
                DisplayName = "captive",
                OwnerInstanceID = "rebels",
                IsCaptured = true,
                CaptorInstanceID = "empire",
            };
            game.AttachNode(captive, origin);

            movement.RequestMove(new List<IMovable> { escort, captive }, destination);

            Assert.AreEqual(origin, escort.GetParent());
            Assert.AreEqual(origin, captive.GetParent());
            Assert.IsNull(escort.Movement);
            Assert.IsNull(captive.Movement);
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
        public void RequestMove_FleetWithInboundUnits_RetargetsInboundUnits()
        {
            GameConfig config = TestContent.Data.GameConfig;
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });

            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet planetA = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planetA, sector);

            Planet planetB = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(planetB, sector);

            Planet planetC = new Planet
            {
                InstanceID = "pC",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 10000,
                PositionY = 0,
            };
            game.AttachNode(planetC, sector);

            Fleet destinationFleet = EntityFactory.CreateFleet("destination", "empire");
            game.AttachNode(destinationFleet, planetB);

            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "carrier",
                OwnerInstanceID = "empire",
                Hyperdrive = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
                StarfighterCapacity = 2,
                RegimentCapacity = 2,
            };
            game.AttachNode(carrier, destinationFleet);

            Fleet sourceFleet = EntityFactory.CreateFleet("source", "empire");
            game.AttachNode(sourceFleet, planetA);

            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "empire",
                Hyperdrive = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(capitalShip, sourceFleet);

            Starfighter starfighter = new Starfighter
            {
                InstanceID = "fighter",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(starfighter, planetA);

            Regiment regiment = new Regiment
            {
                InstanceID = "regiment",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, planetA);

            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            game.AttachNode(officer, planetA);

            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "special",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(specialForces, planetA);

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
            IMovable[] inboundUnits =
            {
                capitalShip,
                starfighter,
                regiment,
                officer,
                specialForces,
            };

            foreach (IMovable inboundUnit in inboundUnits)
                movement.RequestMove(inboundUnit, destinationFleet);

            movement.ProcessTick();

            Dictionary<IMovable, MovementState> previousMovements = inboundUnits.ToDictionary(
                unit => unit,
                unit => unit.Movement
            );

            movement.RequestMove(destinationFleet, planetC);

            foreach (IMovable inboundUnit in inboundUnits)
            {
                MovementState previousMovement = previousMovements[inboundUnit];
                Assert.IsNotNull(inboundUnit.Movement);
                Assert.AreNotSame(previousMovement, inboundUnit.Movement);
                Assert.AreEqual(
                    previousMovement.CurrentPosition,
                    inboundUnit.Movement.OriginPosition
                );
                Assert.AreEqual(
                    previousMovement.CurrentPosition,
                    inboundUnit.Movement.CurrentPosition
                );
                Assert.AreEqual(
                    previousMovement.MovementGroupID,
                    inboundUnit.Movement.MovementGroupID
                );
                Assert.AreEqual(0, inboundUnit.Movement.TicksElapsed);
                Assert.Greater(
                    inboundUnit.Movement.TransitTicks,
                    previousMovement.TicksRemaining()
                );
            }
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
            game.GetFactions().Add(empire);

            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planet, sector);

            Planet otherPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(otherPlanet, sector);

            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(destFleet, planet);
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 2,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(carrier, destFleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
            ManufacturingSystem mfg = new ManufacturingSystem(game, new FleetSystem(game));
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
            game.GetFactions().Add(new Faction { InstanceID = "empire" });

            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet planetA = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planetA, sector);

            Planet planetB = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(planetB, sector);

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

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
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
        public void RequestMove_CapitalShipInFriendlyFleetOverHostilePlanet_StartsTransit()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet productionPlanet = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(productionPlanet, sector);

            Planet capturedPlanet = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(capturedPlanet, sector);

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

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
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
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet productionPlanet = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(productionPlanet, sector);

            Planet capturedPlanet = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(capturedPlanet, sector);

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

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
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
            GameConfig config = TestContent.Data.GameConfig;
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });

            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet origin = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(origin, sector);

            Planet missionPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(missionPlanet, sector);

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

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );

            Assert.DoesNotThrow(
                () => movement.RequestMove(officer, mission),
                "Officer on a CapitalShip should be movable to a mission"
            );
        }

        [Test]
        public void RequestMove_CapitalShipFromFleetToFleetAtSamePlanet_ReparentsWithoutTransit()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });

            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            game.AttachNode(planet, sector);

            Fleet sourceFleet = EntityFactory.CreateFleet("source", "empire");
            Fleet destinationFleet = EntityFactory.CreateFleet("destination", "empire");
            game.AttachNode(sourceFleet, planet);
            game.AttachNode(destinationFleet, planet);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(ship, sourceFleet);

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
            movement.RequestMove(ship, destinationFleet);

            Assert.AreEqual(destinationFleet, ship.GetParent());
            Assert.IsNull(ship.Movement);
        }

        [Test]
        public void RequestMove_GroupFromDifferentShipsAtSamePlanet_MovesAllToDestinationFleet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });

            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            game.AttachNode(planet, sector);

            Fleet sourceFleet = EntityFactory.CreateFleet("source", "empire");
            Fleet destinationFleet = EntityFactory.CreateFleet("destination", "empire");
            game.AttachNode(sourceFleet, planet);
            game.AttachNode(destinationFleet, planet);

            CapitalShip sourceShip1 = new CapitalShip
            {
                InstanceID = "source1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            CapitalShip sourceShip2 = new CapitalShip
            {
                InstanceID = "source2",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            CapitalShip destinationShip = new CapitalShip
            {
                InstanceID = "destination1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(sourceShip1, sourceFleet);
            game.AttachNode(sourceShip2, sourceFleet);
            game.AttachNode(destinationShip, destinationFleet);

            Officer officer1 = EntityFactory.CreateOfficer("o1", "empire");
            Officer officer2 = EntityFactory.CreateOfficer("o2", "empire");
            game.AttachNode(officer1, sourceShip1);
            game.AttachNode(officer2, sourceShip2);

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
            movement.RequestMove(new List<IMovable> { officer1, officer2 }, destinationFleet);

            Assert.AreEqual(destinationShip, officer1.GetParent());
            Assert.AreEqual(destinationShip, officer2.GetParent());
            Assert.IsNull(officer1.Movement);
            Assert.IsNull(officer2.Movement);
        }

        [Test]
        public void RequestMove_SpecialForcesToFleetAtSamePlanet_BoardsFirstShip()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });

            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            game.AttachNode(planet, sector);

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);

            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(specialForces, planet);

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
            movement.RequestMove(specialForces, fleet);

            Assert.AreEqual(ship, specialForces.GetParent());
            Assert.IsNull(specialForces.Movement);
        }

        [Test]
        public void RequestMove_ManufacturedBuildingCompletedAtBlockadedProductionPlanet_RemainsLocal()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                BlockadeSystem blockade,
                MovementSystem movement,
                GameResultProcessor resultProcessor
            ) scene = BuildBlockadeRetargetingScene();
            Building building = new Building
            {
                InstanceID = "building",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            scene.game.AttachNode(building, scene.blockadedDestination);
            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            ProcessBlockadeStart(scene.blockade, scene.resultProcessor);

            building.ManufacturingStatus = ManufacturingStatus.Delivering;
            scene.movement.RequestMove(
                building,
                scene.blockadedDestination,
                scene.blockadedDestination
            );

            Assert.AreSame(building, scene.game.GetSceneNodeByInstanceID<Building>("building"));
            Assert.AreSame(scene.blockadedDestination, building.GetParent());
            Assert.IsNull(building.Movement);
            Assert.AreEqual(ManufacturingStatus.Complete, building.ManufacturingStatus);
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
                game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID),
                "Regiment should be destroyed running the blockade"
            );
        }

        [Test]
        public void RequestMove_RegimentFromBlockadedPlanet_HighRoll_RegimentSurvives()
        {
            // MaximumRNG returns 99 -> 99 >= 50 -> survives
            (GameRoot game, Planet origin, Planet destination, MovementSystem movement) =
                BuildBlockadeScene(new MaximumRNG());

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

            game.GetFactions().Add(new Faction { InstanceID = "empire" });

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sector, game.GetGalaxyMap());

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
            game.AttachNode(origin, sector);
            game.AttachNode(destination, sector);

            Regiment regiment = EntityFactory.CreateRegiment("r1", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, origin);

            Assert.IsFalse(origin.IsBlockaded());

            BlockadeSystem blockade = new BlockadeSystem(game, new FixedRNG());
            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game),
                blockade
            );

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

        [Test]
        public void RequestMove_BuildingUnderConstructionToUncolonizedPlanet_IsRejected()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();

            origin.EnergyCapacity = 5;
            destination.EnergyCapacity = 5;
            destination.OwnerInstanceID = null;
            destination.IsColonized = false;

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            game.AttachNode(building, origin);

            movement.RequestMove(building, destination);

            Assert.AreEqual(origin, building.GetParent());
            Assert.IsFalse(destination.IsColonized);
        }

        [Test]
        public void RequestMove_RegimentFromFleetAtNeutralUncolonizedPlanet_ClaimsImmediately()
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
        public void RequestMove_RegimentFromFleetAtNeutralUncolonizedPlanet_HiddenObserverSnapshot_NotRefreshed()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            Faction observer = AddFaction(game, "observer");
            destination.OwnerInstanceID = null;
            destination.IsColonized = false;
            destination.EnergyCapacity = 1;

            CapturePlanetSnapshot(game, observer, destination, 5);
            (Fleet _, Regiment regiment) = StageFleetWithRegimentAt(game, destination, "empire");

            game.CurrentTick = 20;
            movement.RequestMove(regiment, destination);

            PlanetSnapshot snapshot = GetPlanetSnapshot(observer, destination);
            Assert.AreEqual(5, snapshot.TickCaptured);
            Assert.IsNull(snapshot.OwnerInstanceID);
        }

        [Test]
        public void RequestMove_RegimentFromFleetAtEnemyUncolonizedPlanet_IsRejected()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            destination.OwnerInstanceID = "rebels";
            destination.IsColonized = false;

            (Fleet _, Regiment regiment) = StageFleetWithRegimentAt(game, destination, "empire");

            movement.RequestMove(regiment, destination);

            Assert.AreNotEqual(destination, regiment.GetParent());
            Assert.AreEqual(destination, regiment.GetParentOfType<Planet>());
            Assert.AreEqual("rebels", destination.GetOwnerInstanceID());
        }

        [Test]
        public void RequestMove_RegimentFromOtherPlanetToNeutralUncolonizedPlanet_IsRejected()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            destination.OwnerInstanceID = null;
            destination.IsColonized = false;

            Regiment regiment = new Regiment
            {
                InstanceID = "reg-from-planet",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, origin);

            movement.RequestMove(regiment, destination);

            Assert.AreEqual(origin, regiment.GetParent());
            Assert.IsNull(destination.GetOwnerInstanceID());
        }

        [Test]
        public void RequestMove_RegimentFromFleetAtOtherPlanetToNeutralUncolonizedPlanet_IsRejected()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            destination.OwnerInstanceID = null;
            destination.IsColonized = false;

            (Fleet _, Regiment regiment) = StageFleetWithRegimentAt(game, origin, "empire");

            movement.RequestMove(regiment, destination);

            Assert.AreNotEqual(destination, regiment.GetParent());
            Assert.AreEqual(origin, regiment.GetParentOfType<Planet>());
            Assert.IsNull(destination.GetOwnerInstanceID());
        }

        [Test]
        public void RequestMove_StarfighterToNeutralUncolonizedPlanet_IsRejected()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            destination.OwnerInstanceID = null;
            destination.IsColonized = false;

            Starfighter starfighter = new Starfighter
            {
                InstanceID = "fighter-to-uncolonized",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(starfighter, origin);

            movement.RequestMove(starfighter, destination);

            Assert.AreEqual(origin, starfighter.GetParent());
            Assert.IsNull(destination.GetOwnerInstanceID());
        }

        [Test]
        public void RequestMove_FleetToNeutralUncolonizedPlanet_IsAllowed()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            destination.OwnerInstanceID = null;
            destination.IsColonized = false;

            Fleet fleet = EntityFactory.CreateFleet("fleet-to-uncolonized", "empire");
            game.AttachNode(fleet, origin);
            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = "fleet-to-uncolonized-ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(capitalShip, fleet);

            movement.RequestMove(fleet, destination);

            Assert.AreEqual(destination, fleet.GetParent());
            Assert.IsNotNull(fleet.Movement);
            Assert.IsNull(destination.GetOwnerInstanceID());
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
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, destination);

            Fleet fleet = new Fleet("empire", "pickup-fleet");
            game.AttachNode(fleet, destination);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "pickup-ship",
                OwnerInstanceID = "empire",
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
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, destination);

            Fleet fleet = new Fleet("empire", "pickup-fleet");
            game.AttachNode(fleet, destination);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "pickup-ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 4,
            };
            game.AttachNode(ship, fleet);

            movement.RequestMove(regiment, ship);

            Assert.AreEqual("empire", destination.GetOwnerInstanceID());
        }

        [Test]
        public void HandleMovementRequest_ValidRequest_RoutesThroughAuthoritativeMovePath()
        {
            (GameRoot game, _, Planet destination, Officer officer, MovementSystem movement) =
                BuildScene();
            IGameRequestHandler<UnitMovementRequest> handler = movement;

            handler.HandleRequests(
                new[]
                {
                    new UnitMovementRequest
                    {
                        Units = new List<IMovable> { officer },
                        Destinations = new List<ContainerNode> { destination },
                    },
                }
            );

            Assert.AreEqual(destination, officer.GetParent());
            Assert.IsNotNull(officer.Movement);
        }

        [Test]
        public void HandleMovementRequest_EventOriginatedRequest_PropagatesSourceToArrival()
        {
            (_, _, Planet destination, Officer officer, MovementSystem movement) = BuildScene();
            IGameRequestHandler<UnitMovementRequest> handler = movement;
            handler.HandleRequests(
                new[]
                {
                    new UnitMovementRequest
                    {
                        Units = new List<IMovable> { officer },
                        Destinations = new List<ContainerNode> { destination },
                        SourceEventInstanceID = "SEND_OFFICER",
                    },
                }
            );
            int transitTicks = officer.Movement.TransitTicks;

            List<GameResult> results = new List<GameResult>();
            for (int tick = 0; tick < transitTicks; tick++)
                results.AddRange(movement.ProcessTick());

            UnitArrivedResult arrival = results.OfType<UnitArrivedResult>().Single();
            Assert.AreEqual("SEND_OFFICER", arrival.SourceEventInstanceID);
        }

        [Test]
        public void HandleMovementRequest_EventOriginatedRequestAlreadyAtDestination_EmitsArrival()
        {
            (_, Planet origin, _, Officer officer, MovementSystem movement) = BuildScene();
            IGameRequestHandler<UnitMovementRequest> handler = movement;

            List<GameResult> results = handler.HandleRequests(
                new[]
                {
                    new UnitMovementRequest
                    {
                        Units = new List<IMovable> { officer },
                        Destinations = new List<ContainerNode> { origin },
                        SourceEventInstanceID = "SEND_OFFICER",
                    },
                }
            );
            UnitArrivedResult arrival = results.OfType<UnitArrivedResult>().Single();

            Assert.AreSame(officer, arrival.Unit);
            Assert.AreSame(origin, arrival.Destination);
            Assert.AreEqual("SEND_OFFICER", arrival.SourceEventInstanceID);
        }

        [Test]
        public void HandleMovementRequest_FirstCandidateRejectsGroup_UsesNextCandidate()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            Planet rejected = new Planet
            {
                InstanceID = "rejected",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(rejected, origin.GetParent());
            IGameRequestHandler<UnitMovementRequest> handler = movement;

            handler.HandleRequests(
                new[]
                {
                    new UnitMovementRequest
                    {
                        Units = new List<IMovable> { officer },
                        Destinations = new List<ContainerNode> { rejected, destination },
                    },
                }
            );

            Assert.AreSame(destination, officer.GetParent());
            Assert.IsNotNull(officer.Movement);
        }

        public void HandlePlacementRequest_NewDetachedUnit_AttachesAndRegistersUnit()
        {
            (GameRoot game, _, Planet destination, _, MovementSystem movement) = BuildScene();
            Regiment regiment = new Regiment
            {
                InstanceID = "created-regiment",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            IGameRequestHandler<UnitPlacementRequest> handler = movement;

            handler.HandleRequests(
                new[]
                {
                    new UnitPlacementRequest
                    {
                        Units = new List<IMovable> { regiment },
                        Destinations = new List<ContainerNode> { destination },
                    },
                }
            );

            Assert.AreSame(destination, regiment.GetParent());
            Assert.AreSame(regiment, game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
            CollectionAssert.Contains(
                game.GetFactionByOwnerInstanceID("empire").GetOwnedUnitsByType<Regiment>(),
                regiment
            );
        }

        [Test]
        public void HandlePlacementRequest_GroupExceedsCapacity_LeavesEveryUnitUnchanged()
        {
            (GameRoot game, Planet origin, Planet destination, _, MovementSystem movement) =
                BuildScene();
            Fleet sourceFleet = EntityFactory.CreateFleet("source-fleet", "empire");
            CapitalShip sourceShip = new CapitalShip
            {
                InstanceID = "source-ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                StarfighterCapacity = 2,
            };
            Fleet destinationFleet = EntityFactory.CreateFleet("destination-fleet", "empire");
            CapitalShip destinationShip = new CapitalShip
            {
                InstanceID = "destination-ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                StarfighterCapacity = 1,
            };
            Starfighter first = new Starfighter
            {
                InstanceID = "first",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Starfighter second = new Starfighter
            {
                InstanceID = "second",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(sourceFleet, origin);
            game.AttachNode(sourceShip, sourceFleet);
            game.AttachNode(first, sourceShip);
            game.AttachNode(second, sourceShip);
            game.AttachNode(destinationFleet, destination);
            game.AttachNode(destinationShip, destinationFleet);
            IGameRequestHandler<UnitPlacementRequest> handler = movement;

            handler.HandleRequests(
                new[]
                {
                    new UnitPlacementRequest
                    {
                        Units = new List<IMovable> { first, second },
                        Destinations = new List<ContainerNode> { destinationFleet },
                    },
                }
            );

            Assert.AreSame(sourceShip, first.GetParent());
            Assert.AreSame(sourceShip, second.GetParent());
        }

        [Test]
        public void SendToMission_OfficerAboardShip_RecordsShipAndPlanet()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, origin);
            game.AttachNode(ship, fleet);
            game.MoveNode(officer, ship);
            StubMission mission = new StubMission("empire", destination.InstanceID);
            game.AttachNode(mission, destination);

            movement.SendToMission(officer, mission);

            Assert.AreEqual(ship.InstanceID, officer.MissionReturnParentInstanceID);
            Assert.AreEqual(origin.InstanceID, officer.MissionReturnLocationInstanceID);
            Assert.AreEqual(mission, officer.GetParent());
        }

        [Test]
        public void ReturnFromMission_RecordedShipMoved_ReturnsToRecordedShip()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, origin);
            game.AttachNode(ship, fleet);
            game.MoveNode(officer, ship);
            StubMission mission = new StubMission("empire", destination.InstanceID);
            game.AttachNode(mission, destination);
            movement.SendToMission(officer, mission);
            officer.Movement = null;
            game.MoveNode(fleet, destination);

            List<IMovable> stranded = movement.ReturnFromMission(
                new IMissionParticipant[] { officer },
                new IMovable[0]
            );

            Assert.IsEmpty(stranded);
            Assert.AreSame(ship, officer.GetParent());
        }

        [Test]
        public void ReturnFromMission_MissingRecordedLocation_ReturnsParticipantAsStranded()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            StubMission mission = new StubMission("empire", destination.InstanceID);
            game.AttachNode(mission, destination);
            movement.SendToMission(officer, mission);
            officer.Movement = null;
            officer.MissionReturnParentInstanceID = "missing-parent";
            officer.MissionReturnLocationInstanceID = "missing-location";

            List<IMovable> stranded = movement.ReturnFromMission(
                new IMissionParticipant[] { officer },
                new IMovable[0]
            );

            CollectionAssert.AreEqual(new IMovable[] { officer }, stranded);
            Assert.AreEqual(mission, officer.GetParent());
        }

        [Test]
        public void ReturnFromMission_RecordedPlanetCaptured_ReturnsParticipantAsStranded()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            StubMission mission = new StubMission("empire", destination.InstanceID);
            game.AttachNode(mission, destination);
            movement.SendToMission(officer, mission);
            officer.Movement = null;
            origin.OwnerInstanceID = "rebels";

            List<IMovable> stranded = movement.ReturnFromMission(
                new IMissionParticipant[] { officer },
                new IMovable[0]
            );

            CollectionAssert.AreEqual(new IMovable[] { officer }, stranded);
            Assert.AreSame(mission, officer.GetParent());
        }

        [Test]
        public void ReturnFromMission_MissingRecordedLocation_DoesNotChooseAnotherFleet()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            destination.OwnerInstanceID = "rebels";
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, destination);
            game.AttachNode(ship, fleet);
            StubMission mission = new StubMission("empire", destination.InstanceID);
            game.AttachNode(mission, destination);
            movement.SendToMission(officer, mission);
            officer.Movement = null;
            officer.MissionReturnParentInstanceID = "missing-parent";
            officer.MissionReturnLocationInstanceID = "missing-location";

            List<IMovable> stranded = movement.ReturnFromMission(
                new IMissionParticipant[] { officer },
                new IMovable[0]
            );

            CollectionAssert.AreEqual(new IMovable[] { officer }, stranded);
            Assert.AreSame(mission, officer.GetParent());
        }

        [Test]
        public void ReturnFromMission_NoFriendlyDestination_ReturnsParticipantAsStranded()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            origin.OwnerInstanceID = "rebels";
            destination.OwnerInstanceID = "rebels";
            StubMission mission = new StubMission("empire", destination.InstanceID);
            game.AttachNode(mission, destination);
            movement.SendToMission(officer, mission);
            officer.Movement = null;
            officer.MissionReturnParentInstanceID = "missing-parent";
            officer.MissionReturnLocationInstanceID = "missing-location";

            List<IMovable> stranded = movement.ReturnFromMission(
                new IMissionParticipant[] { officer },
                new IMovable[0]
            );

            CollectionAssert.AreEqual(new IMovable[] { officer }, stranded);
            Assert.AreSame(mission, officer.GetParent());
            Assert.IsNull(officer.Movement);
        }

        [Test]
        public void ReturnFromMission_CapturedPassenger_ReturnsWithEscortGroup()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer escort,
                MovementSystem movement
            ) = BuildScene();
            StubMission mission = new StubMission("empire", destination.InstanceID);
            game.AttachNode(mission, destination);
            movement.SendToMission(escort, mission);
            escort.Movement = null;
            Officer passenger = EntityFactory.CreateOfficer("passenger", "rebels");
            passenger.IsCaptured = true;
            passenger.CaptorInstanceID = "empire";
            game.AttachNode(passenger, destination);

            List<IMovable> stranded = movement.ReturnFromMission(
                new IMissionParticipant[] { escort },
                new IMovable[] { passenger }
            );

            Assert.IsEmpty(stranded);
            Assert.AreSame(origin, escort.GetParent());
            Assert.AreSame(origin, passenger.GetParent());
            Assert.AreEqual(escort.Movement.MovementGroupID, passenger.Movement.MovementGroupID);
        }

        [Test]
        public void ReturnFromMission_PassengerWithoutParticipant_ReturnsPassengerAsStranded()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            Officer passenger = EntityFactory.CreateOfficer("passenger", "rebels");
            passenger.IsCaptured = true;
            passenger.CaptorInstanceID = "empire";
            game.AttachNode(passenger, destination);

            List<IMovable> stranded = movement.ReturnFromMission(
                new IMissionParticipant[0],
                new IMovable[] { passenger }
            );

            CollectionAssert.AreEqual(new IMovable[] { passenger }, stranded);
            Assert.AreSame(destination, passenger.GetParent());
            Assert.IsNull(passenger.Movement);
        }

        [Test]
        public void ReturnFromMission_ParticipantsWithDifferentOrigins_ReturnToTheirOwnLocations()
        {
            (
                GameRoot game,
                Planet firstOrigin,
                Planet destination,
                Officer firstOfficer,
                MovementSystem movement
            ) = BuildScene();
            PlanetSector secondSector = new PlanetSector
            {
                InstanceID = "sector2",
                PositionX = 200,
                PositionY = 0,
            };
            Planet secondOrigin = new Planet
            {
                InstanceID = "p3",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 200,
                PositionY = 0,
            };
            Officer secondOfficer = EntityFactory.CreateOfficer("o2", "empire");
            game.AttachNode(secondSector, game.GetGalaxyMap());
            game.AttachNode(secondOrigin, secondSector);
            game.AttachNode(secondOfficer, secondOrigin);
            StubMission mission = new StubMission("empire", destination.InstanceID);
            game.AttachNode(mission, destination);
            movement.SendToMission(firstOfficer, mission);
            movement.SendToMission(secondOfficer, mission);
            firstOfficer.Movement = null;
            secondOfficer.Movement = null;

            List<IMovable> stranded = movement.ReturnFromMission(
                new IMissionParticipant[] { firstOfficer, secondOfficer },
                new IMovable[0]
            );

            Assert.IsEmpty(stranded);
            Assert.AreEqual(firstOrigin, firstOfficer.GetParent());
            Assert.AreEqual(secondOrigin, secondOfficer.GetParent());
            Assert.AreNotEqual(
                firstOfficer.Movement.MovementGroupID,
                secondOfficer.Movement.MovementGroupID
            );
        }

        [Test]
        public void TryGetTransitTicks_ValidDestination_DoesNotMoveUnit()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            bool result = movement.TryGetTransitTicks(
                new List<IMovable> { officer },
                destination,
                out int transitTicks
            );

            Assert.IsTrue(result);
            Assert.Greater(transitTicks, 0);
            Assert.AreEqual(origin, officer.GetParent());
            Assert.IsNull(officer.Movement);
        }

        [Test]
        public void TryGetTransitTicks_FleetWithUnfinishedSlowerShip_IgnoresUnfinishedShip()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            Fleet fleet = EntityFactory.CreateFleet("mixed-fleet", "empire");
            game.AttachNode(fleet, origin);
            CapitalShip completedShip = CreateMovableCapitalShip("completed-ship");
            completedShip.Hyperdrive = 10;
            game.AttachNode(completedShip, fleet);
            Assert.IsTrue(
                movement.TryGetTransitTicks(
                    new List<IMovable> { fleet },
                    destination,
                    out int completedOnlyTicks
                )
            );
            CapitalShip unfinishedShip = new CapitalShip
            {
                InstanceID = "unfinished-ship",
                OwnerInstanceID = "empire",
                Hyperdrive = 1,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            game.AttachNode(unfinishedShip, fleet);

            bool estimated = movement.TryGetTransitTicks(
                new List<IMovable> { fleet },
                destination,
                out int mixedFleetTicks
            );

            Assert.IsTrue(estimated);
            Assert.AreEqual(completedOnlyTicks, mixedFleetTicks);
            Assert.IsNull(fleet.Movement);
        }

        [Test]
        public void TryEstimateManufacturedTransitTicks_ValidDestination_DoesNotAssignMovement()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            bool result = movement.TryEstimateManufacturedTransitTicks(
                officer,
                origin,
                destination,
                out int transitTicks
            );

            Assert.IsTrue(result);
            Assert.Greater(transitTicks, 0);
            Assert.IsNull(officer.Movement);
        }

        [Test]
        public void TryEstimateManufacturedTransitTicks_ViewFleetDestination_UsesLiveFleetLocation()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();

            Fleet liveFleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(liveFleet, destination);

            Fleet viewFleet = EntityFactory.CreateFleet(liveFleet.InstanceID, "empire");

            bool result = movement.TryEstimateManufacturedTransitTicks(
                officer,
                origin,
                viewFleet,
                out int transitTicks
            );

            Assert.IsTrue(result);
            Assert.Greater(transitTicks, 0);
            Assert.IsNull(officer.Movement);
        }

        [Test]
        public void TryEstimateManufacturedTransitTicks_HostilePlanetDestination_ReturnsFalse()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            destination.OwnerInstanceID = "rebels";

            bool result = movement.TryEstimateManufacturedTransitTicks(
                officer,
                origin,
                destination,
                out int transitTicks
            );

            Assert.IsFalse(result);
            Assert.AreEqual(0, transitTicks);
            Assert.IsNull(officer.Movement);
        }

        [Test]
        public void TryEstimateManufacturedTransitTicks_StarfighterToEnemyBlockadedPlanet_ReturnsFalse()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            Starfighter starfighter = EntityFactory.CreateStarfighter("fighter", "empire");
            starfighter.ManufacturingStatus = ManufacturingStatus.Building;
            AddBlockadingFleet(game, destination);

            bool estimated = movement.TryEstimateManufacturedTransitTicks(
                starfighter,
                origin,
                destination,
                out _
            );

            Assert.IsFalse(estimated);
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
        public void UpdateMovement_OnArrival_PreservesMovementGroupIDInArrivalResult()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            movement.RequestMove(officer, destination);
            string movementGroupId = officer.Movement.MovementGroupID;
            officer.Movement.TicksElapsed = officer.Movement.TransitTicks;

            UnitArrivedResult arrival = movement.ProcessTick().OfType<UnitArrivedResult>().Single();

            Assert.AreEqual(movementGroupId, arrival.MovementGroupID);
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
            Mission mission = new SabotageMission
            {
                InstanceID = "m1",
                OwnerInstanceID = "empire",
                LocationInstanceID = destination.InstanceID,
                HasInitiated = true,
            };
            game.AttachNode(mission, destination);
            mission.AddChild(officer);

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

            Mission mission = new SabotageMission
            {
                InstanceID = "m1",
                OwnerInstanceID = "empire",
                LocationInstanceID = destination.InstanceID,
                HasInitiated = true,
            };
            game.AttachNode(mission, destination);
            mission.AddChild(specialForces);

            movement.RequestMove(specialForces, mission);
            specialForces.Movement.TicksElapsed = specialForces.Movement.TransitTicks;

            List<GameResult> results = movement.ProcessTick();

            Assert.IsNull(specialForces.Movement);
            Assert.AreEqual(mission, specialForces.GetParent());
        }

        [Test]
        public void UpdateMovement_GroupNonCapturedUnits_PreservesMovementGroupIDInArrivalResults()
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

            movement.RequestMove(new List<IMovable> { officer, officer2 }, destination);
            string movementGroupId = officer.Movement.MovementGroupID;
            officer.Movement.TicksElapsed = officer.Movement.TransitTicks;
            officer2.Movement.TicksElapsed = officer2.Movement.TransitTicks;

            List<UnitArrivedResult> arrivals = movement
                .ProcessTick()
                .OfType<UnitArrivedResult>()
                .Where(result => result.Unit == officer || result.Unit == officer2)
                .ToList();

            Assert.AreEqual(2, arrivals.Count);
            Assert.IsFalse(string.IsNullOrEmpty(movementGroupId));
            Assert.IsTrue(arrivals.All(result => result.MovementGroupID == movementGroupId));
        }

        [Test]
        public void UpdateMovement_FleetMovesBeforeUnitArrives_UnitStillEnRoute()
        {
            GameConfig config = TestContent.Data.GameConfig;
            GameRoot game = new GameRoot(config);

            game.GetFactions().Add(new Faction { InstanceID = "empire" });

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet planetA = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planetA, sector);

            Planet planetB = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 100,
            };
            game.AttachNode(planetB, sector);

            Planet planetC = new Planet
            {
                InstanceID = "pC",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 500,
                PositionY = 500,
            };
            game.AttachNode(planetC, sector);

            // Fleet starts at planet B
            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(fleet, planetB);
            CapitalShip fleetShip = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleetShip, fleet);

            // Officer at planet A moves toward the fleet
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, planetA);

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
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
        public void UpdateMovement_BuildingInTransitDestinationChangedSides_BuildingDestroyed()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet originPlanet = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(originPlanet, sector);

            Planet destPlanet = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(destPlanet, sector);

            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            game.AttachNode(mine, destPlanet);

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
            movement.RequestMove(mine, destPlanet, originPlanet);

            // Destination captured while building is in transit.
            destPlanet.OwnerInstanceID = "rebels";

            int transit = mine.Movement.TransitTicks;
            List<GameResult> allResults = new List<GameResult>();
            for (int i = 0; i < transit; i++)
                allResults.AddRange(movement.ProcessTick());

            Assert.IsNull(
                game.GetSceneNodeByInstanceID<Building>(mine.InstanceID),
                "Building should be destroyed when destination changes sides during transit."
            );
            Assert.IsTrue(allResults.OfType<GameObjectDestroyedOnArrivalResult>().Any());
        }

        [Test]
        public void UpdateMovement_NonBuildingInTransitDestinationChangedSides_UnitRerouted()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet originPlanet = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(originPlanet, sector);

            Planet destPlanet = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(destPlanet, sector);

            Regiment regiment = new Regiment
            {
                InstanceID = "rg1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, destPlanet);

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
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
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet originPlanet = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(originPlanet, sector);

            Planet hostilePlanet = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(hostilePlanet, sector);

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

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
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
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet originPlanet = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(originPlanet, sector);

            Planet hostilePlanet = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(hostilePlanet, sector);

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

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
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
        public void UpdateMovement_ManufacturedBuildingDispatchedAfterBlockadeStarted_DestroysOnArrival()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                BlockadeSystem blockade,
                MovementSystem movement,
                GameResultProcessor resultProcessor
            ) scene = BuildBlockadeRetargetingScene();
            Building building = new Building
            {
                InstanceID = "building",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            scene.game.AttachNode(building, scene.blockadedDestination);
            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            ProcessBlockadeStart(scene.blockade, scene.resultProcessor);

            building.ManufacturingStatus = ManufacturingStatus.Delivering;
            scene.movement.RequestMove(building, scene.blockadedDestination, scene.origin);

            int transitTicks = building.Movement.TransitTicks;
            List<GameResult> results = new List<GameResult>();
            for (int tick = 0; tick < transitTicks; tick++)
                results.AddRange(scene.movement.ProcessTick());

            Assert.IsNull(scene.game.GetSceneNodeByInstanceID<Building>(building.InstanceID));
            GameObjectDestroyedOnArrivalResult destroyed = results
                .OfType<GameObjectDestroyedOnArrivalResult>()
                .Single();
            Assert.AreSame(building, destroyed.DestroyedObject);
            Assert.AreSame(scene.blockadedDestination, destroyed.Context);
            Assert.IsFalse(
                results
                    .OfType<UnitArrivedResult>()
                    .Any(result => ReferenceEquals(result.Unit, building))
            );
        }

        [Test]
        public void UpdateMovement_ManufacturedRegimentDispatchedAfterBlockadeStarted_DestroysOnArrival()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                BlockadeSystem blockade,
                MovementSystem movement,
                GameResultProcessor resultProcessor
            ) scene = BuildBlockadeRetargetingScene();
            Regiment regiment = new Regiment
            {
                InstanceID = "regiment",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            scene.game.AttachNode(regiment, scene.blockadedDestination);
            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            ProcessBlockadeStart(scene.blockade, scene.resultProcessor);

            regiment.ManufacturingStatus = ManufacturingStatus.Delivering;
            scene.movement.RequestMove(regiment, scene.blockadedDestination, scene.origin);

            int transitTicks = regiment.Movement.TransitTicks;
            List<GameResult> results = new List<GameResult>();
            for (int tick = 0; tick < transitTicks; tick++)
                results.AddRange(scene.movement.ProcessTick());

            Assert.IsNull(scene.game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
            GameObjectDestroyedOnArrivalResult destroyed = results
                .OfType<GameObjectDestroyedOnArrivalResult>()
                .Single();
            Assert.AreSame(regiment, destroyed.DestroyedObject);
            Assert.AreSame(scene.blockadedDestination, destroyed.Context);
            Assert.IsFalse(
                results
                    .OfType<UnitArrivedResult>()
                    .Any(result => ReferenceEquals(result.Unit, regiment))
            );
        }

        [Test]
        public void UpdateMovement_BlockadeEndsBeforeManufacturedBuildingArrival_CompletesArrival()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                BlockadeSystem blockade,
                MovementSystem movement,
                GameResultProcessor resultProcessor
            ) scene = BuildBlockadeRetargetingScene();
            Building building = new Building
            {
                InstanceID = "building",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            scene.game.AttachNode(building, scene.blockadedDestination);
            (Fleet blockadingFleet, _) = AddBlockadingFleet(scene.game, scene.blockadedDestination);
            ProcessBlockadeStart(scene.blockade, scene.resultProcessor);

            building.ManufacturingStatus = ManufacturingStatus.Delivering;
            scene.movement.RequestMove(building, scene.blockadedDestination, scene.origin);
            scene.game.DetachNode(blockadingFleet);
            scene.resultProcessor.Process(scene.blockade.ProcessTick());

            int transitTicks = building.Movement.TransitTicks;
            List<GameResult> results = new List<GameResult>();
            for (int tick = 0; tick < transitTicks; tick++)
                results.AddRange(scene.movement.ProcessTick());

            Assert.AreSame(
                building,
                scene.game.GetSceneNodeByInstanceID<Building>(building.InstanceID)
            );
            Assert.AreSame(scene.blockadedDestination, building.GetParent());
            Assert.IsNull(building.Movement);
            Assert.IsFalse(results.OfType<GameObjectDestroyedOnArrivalResult>().Any());
            UnitArrivedResult arrival = results
                .OfType<UnitArrivedResult>()
                .Single(result => ReferenceEquals(result.Unit, building));
            Assert.AreEqual(ManufacturingStatus.Complete, building.ManufacturingStatus);
            Assert.IsTrue(
                results
                    .OfType<GameObjectDeployedResult>()
                    .Any(result => ReferenceEquals(result.GameObject, building))
            );
        }

        [Test]
        public void TrySetFleetWaypointRoute_MultipleDestinations_ContinuesRouteAfterArrival()
        {
            (
                _,
                _,
                Planet firstDestination,
                Planet secondDestination,
                Fleet fleet,
                MovementSystem movement
            ) = BuildWaypointScene();

            bool routeSet = movement.TrySetFleetWaypointRoute(
                new ISceneNode[] { fleet },
                new[] { firstDestination.InstanceID, secondDestination.InstanceID },
                "empire"
            );

            Assert.IsTrue(routeSet);
            Assert.AreSame(firstDestination, fleet.GetParent());
            Assert.IsNotNull(fleet.Movement);
            CollectionAssert.AreEqual(
                new[] { firstDestination.InstanceID, secondDestination.InstanceID },
                fleet.Waypoints
            );

            fleet.Movement.TicksElapsed = fleet.Movement.TransitTicks - 1;
            List<GameResult> firstArrivalResults = movement.ProcessTick();

            Assert.IsNull(fleet.Movement);
            CollectionAssert.AreEqual(new[] { secondDestination.InstanceID }, fleet.Waypoints);
            Assert.IsFalse(firstArrivalResults.OfType<FleetWaypointsCompletedResult>().Any());

            movement.ContinueFleetWaypointRoutes();

            Assert.AreSame(secondDestination, fleet.GetParent());
            Assert.IsNotNull(fleet.Movement);
            CollectionAssert.AreEqual(new[] { secondDestination.InstanceID }, fleet.Waypoints);

            fleet.Movement.TicksElapsed = fleet.Movement.TransitTicks - 1;
            List<GameResult> finalArrivalResults = movement.ProcessTick();

            FleetWaypointsCompletedResult completed = finalArrivalResults
                .OfType<FleetWaypointsCompletedResult>()
                .Single();
            Assert.AreSame(fleet, completed.Fleet);
            Assert.AreSame(secondDestination, completed.Destination);
            Assert.IsEmpty(fleet.Waypoints);
        }

        [Test]
        public void TrySetFleetWaypointRoute_FleetAlreadyMoving_QueuesContinuation()
        {
            (
                _,
                _,
                Planet firstDestination,
                Planet secondDestination,
                Fleet fleet,
                MovementSystem movement
            ) = BuildWaypointScene();
            bool moved = movement.TryRequestMove(
                new ISceneNode[] { fleet },
                firstDestination,
                "empire"
            );

            bool routeSet = movement.TrySetFleetWaypointRoute(
                new ISceneNode[] { fleet },
                new[] { secondDestination.InstanceID },
                "empire"
            );

            Assert.IsTrue(moved);
            Assert.IsTrue(routeSet);
            CollectionAssert.AreEqual(
                new[] { firstDestination.InstanceID, secondDestination.InstanceID },
                fleet.Waypoints
            );
        }

        [Test]
        public void TrySetFleetWaypointRoute_OpposingFleet_ReturnsFalse()
        {
            (_, Planet origin, Planet firstDestination, _, Fleet fleet, MovementSystem movement) =
                BuildWaypointScene();

            bool routeSet = movement.TrySetFleetWaypointRoute(
                new ISceneNode[] { fleet },
                new[] { firstDestination.InstanceID },
                "rebels"
            );

            Assert.IsFalse(routeSet);
            Assert.IsNull(fleet.Movement);
            Assert.IsEmpty(fleet.Waypoints);
            Assert.AreSame(origin, fleet.GetParent());
        }

        [Test]
        public void CanSetFleetWaypointRoute_ValidRoute_DoesNotMutateFleet()
        {
            (
                _,
                Planet origin,
                Planet firstDestination,
                Planet secondDestination,
                Fleet fleet,
                MovementSystem movement
            ) = BuildWaypointScene();

            bool canSetRoute = movement.CanSetFleetWaypointRoute(
                new ISceneNode[] { fleet },
                new[] { firstDestination.InstanceID, secondDestination.InstanceID },
                "empire"
            );

            Assert.IsTrue(canSetRoute);
            Assert.AreSame(origin, fleet.GetParent());
            Assert.IsNull(fleet.Movement);
            Assert.IsEmpty(fleet.Waypoints);
        }

        [Test]
        public void ClearFleetWaypoints_ActiveRoute_PreservesCurrentMovementAndStopsContinuation()
        {
            (
                _,
                _,
                Planet firstDestination,
                Planet secondDestination,
                Fleet fleet,
                MovementSystem movement
            ) = BuildWaypointScene();
            movement.TrySetFleetWaypointRoute(
                new ISceneNode[] { fleet },
                new[] { firstDestination.InstanceID, secondDestination.InstanceID },
                "empire"
            );
            MovementState activeMovement = fleet.Movement;

            bool cleared = movement.ClearFleetWaypoints(new ISceneNode[] { fleet }, "empire");

            Assert.IsTrue(cleared);
            Assert.AreSame(activeMovement, fleet.Movement);
            Assert.AreSame(firstDestination, fleet.GetParent());
            Assert.IsEmpty(fleet.Waypoints);

            fleet.Movement.TicksElapsed = fleet.Movement.TransitTicks - 1;
            movement.ProcessTick();
            movement.ContinueFleetWaypointRoutes();

            Assert.IsNull(fleet.Movement);
            Assert.AreSame(firstDestination, fleet.GetParent());
        }

        [Test]
        public void TryRequestMove_FleetWithQueuedWaypoints_ReplacesRoute()
        {
            (
                _,
                Planet origin,
                Planet firstDestination,
                Planet secondDestination,
                Fleet fleet,
                MovementSystem movement
            ) = BuildWaypointScene();
            movement.TrySetFleetWaypointRoute(
                new ISceneNode[] { fleet },
                new[] { firstDestination.InstanceID, secondDestination.InstanceID },
                "empire"
            );
            fleet.Movement.TicksElapsed = fleet.Movement.TransitTicks - 1;
            movement.ProcessTick();

            bool moved = movement.TryRequestMove(new ISceneNode[] { fleet }, origin, "empire");

            Assert.IsTrue(moved);
            Assert.IsNotNull(fleet.Movement);
            Assert.IsEmpty(fleet.Waypoints);
            Assert.AreSame(origin, fleet.GetParent());
        }

        [Test]
        public void TryRequestMove_GroupUnderConstructionExceedsCapacity_NoneRetarget()
        {
            (
                GameRoot game,
                Planet origin,
                Planet destination,
                Officer officer,
                MovementSystem movement
            ) = BuildScene();
            Fleet destinationFleet = EntityFactory.CreateFleet("destination-fleet", "empire");
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "carrier",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                StarfighterCapacity = 1,
            };
            Starfighter firstStarfighter = new Starfighter
            {
                InstanceID = "first-starfighter",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            Starfighter secondStarfighter = new Starfighter
            {
                InstanceID = "second-starfighter",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            game.AttachNode(destinationFleet, destination);
            game.AttachNode(carrier, destinationFleet);
            game.AttachNode(firstStarfighter, origin);
            game.AttachNode(secondStarfighter, origin);
            IReadOnlyList<GameResult> results = null;
            movement.ResultsProduced += producedResults => results = producedResults;

            bool moved = movement.TryRequestMove(
                new ISceneNode[] { firstStarfighter, secondStarfighter },
                destinationFleet,
                "empire"
            );

            Assert.IsFalse(moved);
            Assert.AreSame(origin, firstStarfighter.GetParent());
            Assert.AreSame(origin, secondStarfighter.GetParent());
            Assert.IsEmpty(carrier.GetChildren<Starfighter>());
            Assert.IsNull(results);
        }

        [Test]
        public void TryRequestMove_StarfighterToEnemyBlockadedPlanet_ReturnsFalse()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            Starfighter starfighter = EntityFactory.CreateStarfighter("fighter", "empire");
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(starfighter, origin);
            AddBlockadingFleet(game, destination);

            bool moved = movement.TryRequestMove(
                new ISceneNode[] { starfighter },
                destination,
                "empire"
            );

            Assert.IsFalse(moved);
            Assert.AreSame(origin, starfighter.GetParent());
            Assert.IsNull(starfighter.Movement);
        }

        [Test]
        public void TryRequestMove_RegimentToEnemyBlockadedPlanet_ReturnsFalse()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            Regiment regiment = EntityFactory.CreateRegiment("regiment", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, origin);
            AddBlockadingFleet(game, destination);

            bool moved = movement.TryRequestMove(
                new ISceneNode[] { regiment },
                destination,
                "empire"
            );

            Assert.IsFalse(moved);
            Assert.AreSame(origin, regiment.GetParent());
            Assert.IsNull(regiment.Movement);
        }

        [Test]
        public void TryRequestMove_RegimentToShip_ReturnsGarrisonChange()
        {
            (GameRoot game, Planet origin, Planet _, Officer _, MovementSystem movement) =
                BuildScene();
            Regiment regiment = new Regiment
            {
                InstanceID = "garrison-reg",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, origin);

            Fleet fleet = new Fleet("empire", "pickup-fleet");
            game.AttachNode(fleet, origin);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "pickup-ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 1,
            };
            game.AttachNode(ship, fleet);
            IReadOnlyList<GameResult> results = null;
            movement.ResultsProduced += producedResults => results = producedResults;

            bool moved = movement.TryRequestMove(new ISceneNode[] { regiment }, ship, "empire");

            Assert.IsTrue(moved);
            PlanetGarrisonChangedResult result = results
                .OfType<PlanetGarrisonChangedResult>()
                .Single();
            Assert.AreSame(origin, result.Planet);
            Assert.IsEmpty(movement.ProcessTick().OfType<PlanetGarrisonChangedResult>());
        }

        [Test]
        public void TryRequestMove_FleetToFleet_MovesShipsAndRemovesSourceFleet()
        {
            (GameRoot game, Planet origin, Planet _, Officer _, MovementSystem movement) =
                BuildScene();
            Fleet sourceFleet = EntityFactory.CreateFleet("source-fleet", "empire");
            Fleet destinationFleet = EntityFactory.CreateFleet("destination-fleet", "empire");
            game.AttachNode(sourceFleet, origin);
            game.AttachNode(destinationFleet, origin);
            CapitalShip firstShip = CreateMovableCapitalShip("first-ship");
            CapitalShip secondShip = CreateMovableCapitalShip("second-ship");
            game.AttachNode(firstShip, sourceFleet);
            game.AttachNode(secondShip, sourceFleet);

            bool moved = movement.TryRequestMove(
                new ISceneNode[] { sourceFleet },
                destinationFleet,
                "empire"
            );

            Assert.IsTrue(moved);
            Assert.AreSame(destinationFleet, firstShip.GetParent());
            Assert.AreSame(destinationFleet, secondShip.GetParent());
            Assert.IsNull(sourceFleet.GetParent());
        }

        [Test]
        public void TryRequestMove_FleetWithOnlyShipsUnderConstruction_RetargetsDelivery()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            Fleet fleet = EntityFactory.CreateFleet("unfinished-fleet", "empire");
            game.AttachNode(fleet, origin);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "unfinished-ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            game.AttachNode(ship, fleet);

            bool moved = movement.TryRequestMove(new ISceneNode[] { fleet }, destination, "empire");

            Assert.IsTrue(moved);
            Assert.AreSame(destination, fleet.GetParent());
            Assert.IsNull(fleet.Movement);
            Assert.AreSame(fleet, ship.GetParent());
        }

        [Test]
        public void TryRequestMove_CapitalShipToFleet_RemovesEmptySourceFleet()
        {
            (GameRoot game, Planet origin, Planet _, Officer _, MovementSystem movement) =
                BuildScene();
            Fleet sourceFleet = EntityFactory.CreateFleet("source-fleet", "empire");
            Fleet destinationFleet = EntityFactory.CreateFleet("destination-fleet", "empire");
            game.AttachNode(sourceFleet, origin);
            game.AttachNode(destinationFleet, origin);
            CapitalShip ship = CreateMovableCapitalShip("ship");
            game.AttachNode(ship, sourceFleet);

            bool moved = movement.TryRequestMove(
                new ISceneNode[] { ship },
                destinationFleet,
                "empire"
            );

            Assert.IsTrue(moved);
            Assert.AreSame(destinationFleet, ship.GetParent());
            Assert.IsNull(sourceFleet.GetParent());
            Assert.IsNull(ship.Movement);
        }

        [Test]
        public void TryRequestMove_CapitalShipUnderConstruction_RetargetsDelivery()
        {
            (GameRoot game, Planet origin, Planet _, Officer _, MovementSystem movement) =
                BuildScene();
            Fleet sourceFleet = EntityFactory.CreateFleet("source-fleet", "empire");
            Fleet destinationFleet = EntityFactory.CreateFleet("destination-fleet", "empire");
            game.AttachNode(sourceFleet, origin);
            game.AttachNode(destinationFleet, origin);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            game.AttachNode(ship, sourceFleet);
            IReadOnlyList<GameResult> results = null;
            movement.ResultsProduced += producedResults => results = producedResults;

            bool moved = movement.TryRequestMove(
                new ISceneNode[] { ship },
                destinationFleet,
                "empire"
            );

            Assert.IsTrue(moved);
            Assert.AreSame(destinationFleet, ship.GetParent());
            Assert.IsNull(ship.Movement);
            Assert.IsNull(sourceFleet.GetParent());
            Assert.IsEmpty(results);
        }

        [Test]
        public void TryRequestMove_CapitalShipInMovingFleet_PreservesSourceGraph()
        {
            (GameRoot game, Planet origin, Planet _, Officer _, MovementSystem movement) =
                BuildScene();
            Fleet sourceFleet = EntityFactory.CreateFleet("source-fleet", "empire");
            Fleet destinationFleet = EntityFactory.CreateFleet("destination-fleet", "empire");
            game.AttachNode(sourceFleet, origin);
            game.AttachNode(destinationFleet, origin);
            CapitalShip ship = CreateMovableCapitalShip("ship");
            game.AttachNode(ship, sourceFleet);
            sourceFleet.Movement = new MovementState();

            bool moved = movement.TryRequestMove(
                new ISceneNode[] { ship },
                destinationFleet,
                "empire"
            );

            Assert.IsFalse(moved);
            Assert.AreSame(sourceFleet, ship.GetParent());
            CollectionAssert.AreEquivalent(
                new[] { sourceFleet, destinationFleet },
                origin.GetChildren<Fleet>().ToList()
            );
        }

        [Test]
        public void TryRequestMove_CapitalShipToPlanet_CreatesDestinationFleet()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            Fleet sourceFleet = EntityFactory.CreateFleet("source-fleet", "empire");
            game.AttachNode(sourceFleet, origin);
            CapitalShip ship = CreateMovableCapitalShip("ship");
            game.AttachNode(ship, sourceFleet);

            bool moved = movement.TryRequestMove(new ISceneNode[] { ship }, destination, "empire");

            Assert.IsTrue(moved);
            Assert.AreEqual(1, destination.GetChildren<Fleet>().Count);
            Assert.AreSame(destination.GetChildren<Fleet>()[0], ship.GetParent());
            Assert.IsNotNull(ship.Movement);
            Assert.IsNull(sourceFleet.GetParent());
        }

        [Test]
        public void TryRequestMove_SnapshotPlanet_CreatesFleetOnLiveDestination()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            Fleet sourceFleet = EntityFactory.CreateFleet("source-fleet", "empire");
            game.AttachNode(sourceFleet, origin);
            CapitalShip ship = CreateMovableCapitalShip("ship");
            game.AttachNode(ship, sourceFleet);
            Planet snapshot = new Planet { InstanceID = destination.InstanceID };

            bool moved = movement.TryRequestMove(new ISceneNode[] { ship }, snapshot, "empire");

            Assert.IsTrue(moved);
            Assert.AreSame(destination, ship.GetParentOfType<Planet>());
            Assert.AreEqual(0, snapshot.GetChildren<Fleet>().Count);
        }

        [Test]
        public void TryRequestMove_MultipleCapitalShipsToPlanet_CreatesOneDestinationFleet()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            Fleet sourceFleet = EntityFactory.CreateFleet("source-fleet", "empire");
            game.AttachNode(sourceFleet, origin);
            CapitalShip firstShip = CreateMovableCapitalShip("first-ship");
            CapitalShip secondShip = CreateMovableCapitalShip("second-ship");
            game.AttachNode(firstShip, sourceFleet);
            game.AttachNode(secondShip, sourceFleet);

            bool moved = movement.TryRequestMove(
                new ISceneNode[] { firstShip, secondShip },
                destination,
                "empire"
            );

            Assert.IsTrue(moved);
            Assert.AreEqual(1, destination.GetChildren<Fleet>().Count);
            Assert.AreSame(destination.GetChildren<Fleet>()[0], firstShip.GetParent());
            Assert.AreSame(destination.GetChildren<Fleet>()[0], secondShip.GetParent());
            Assert.AreEqual(
                2,
                destination.GetChildren<Fleet>()[0].GetChildren<CapitalShip>().Count
            );
        }

        [Test]
        public void TryRequestMove_CapitalShipsAtDifferentPlanets_PreservesSourceFleets()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            Fleet originFleet = EntityFactory.CreateFleet("origin-fleet", "empire");
            Fleet destinationFleet = EntityFactory.CreateFleet("destination-fleet", "empire");
            game.AttachNode(originFleet, origin);
            game.AttachNode(destinationFleet, destination);
            CapitalShip originShip = CreateMovableCapitalShip("origin-ship");
            CapitalShip destinationShip = CreateMovableCapitalShip("destination-ship");
            game.AttachNode(originShip, originFleet);
            game.AttachNode(destinationShip, destinationFleet);

            bool moved = movement.TryRequestMove(
                new ISceneNode[] { originShip, destinationShip },
                origin,
                "empire"
            );

            Assert.IsFalse(moved);
            CollectionAssert.AreEqual(new[] { originFleet }, origin.GetChildren<Fleet>().ToList());
            CollectionAssert.AreEqual(
                new[] { destinationFleet },
                destination.GetChildren<Fleet>().ToList()
            );
            Assert.AreSame(originFleet, originShip.GetParent());
            Assert.AreSame(destinationFleet, destinationShip.GetParent());
        }

        [Test]
        public void ProcessTick_GroupCapturedOfficerArrivesAtCaptorPlanet_CompletesMovement()
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
            escort.Movement.TicksElapsed = escort.Movement.TransitTicks;
            captive.Movement.TicksElapsed = captive.Movement.TransitTicks;

            List<GameResult> results = movement.ProcessTick();

            Assert.AreEqual(destination, captive.GetParent());
            Assert.IsNull(captive.Movement);
            Assert.IsTrue(
                results
                    .OfType<UnitArrivedResult>()
                    .Any(result => ReferenceEquals(result.Unit, captive))
            );
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

        [Test]
        public void TryGetSelectionTransitTicks_StarfighterToEnemyBlockadedPlanet_ReturnsFalse()
        {
            (GameRoot game, Planet origin, Planet destination, Officer _, MovementSystem movement) =
                BuildScene();
            Starfighter starfighter = EntityFactory.CreateStarfighter("fighter", "empire");
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(starfighter, origin);
            AddBlockadingFleet(game, destination);

            bool estimated = movement.TryGetSelectionTransitTicks(
                new ISceneNode[] { starfighter },
                destination,
                "empire",
                out _
            );

            Assert.IsFalse(estimated);
        }

        [Test]
        public void BlockadeStarted_IndependentInboundUnits_RerouteFromCurrentPosition()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                BlockadeSystem blockade,
                MovementSystem movement,
                GameResultProcessor resultProcessor
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
            scene.movement.ProcessTick();

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
            List<GameResult> results = ProcessBlockadeStart(scene.blockade, scene.resultProcessor);

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
        public void BlockadeStarted_ExcludedInboundUnits_ContinueToDestination()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                BlockadeSystem blockade,
                MovementSystem movement,
                GameResultProcessor resultProcessor
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
            List<GameResult> results = ProcessBlockadeStart(scene.blockade, scene.resultProcessor);

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
        public void BlockadeStarted_InTransitBuilding_IsDestroyed()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                BlockadeSystem blockade,
                MovementSystem movement,
                GameResultProcessor resultProcessor
            ) scene = BuildBlockadeRetargetingScene();
            Building building = EntityFactory.CreateBuilding("building", "empire");
            building.ManufacturingStatus = ManufacturingStatus.Delivering;
            scene.game.AttachNode(building, scene.blockadedDestination);
            scene.movement.RequestMove(building, scene.blockadedDestination, scene.origin);

            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            List<GameResult> results = ProcessBlockadeStart(scene.blockade, scene.resultProcessor);

            Assert.IsNull(scene.game.GetSceneNodeByInstanceID<Building>(building.InstanceID));
            GameObjectDestroyedResult destroyed = results
                .OfType<GameObjectDestroyedResult>()
                .Single();
            Assert.AreSame(building, destroyed.DestroyedObject);
            Assert.AreSame(scene.blockadedDestination, destroyed.Context);
        }

        [Test]
        public void BlockadeStarted_NoValidFallback_DestroysAutoroutedUnit()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                BlockadeSystem blockade,
                MovementSystem movement,
                GameResultProcessor resultProcessor
            ) scene = BuildBlockadeRetargetingScene();
            Starfighter starfighter = EntityFactory.CreateStarfighter("fighter", "empire");
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(starfighter, scene.origin);
            scene.movement.RequestMove(starfighter, scene.blockadedDestination);
            scene.origin.OwnerInstanceID = "rebels";
            scene.nearestSafeDestination.OwnerInstanceID = "rebels";
            scene.fartherSafeDestination.OwnerInstanceID = "rebels";

            AddBlockadingFleet(scene.game, scene.blockadedDestination);
            List<GameResult> results = ProcessBlockadeStart(scene.blockade, scene.resultProcessor);

            Assert.IsNull(scene.game.GetSceneNodeByInstanceID<Starfighter>(starfighter.InstanceID));
            GameObjectDestroyedResult destroyed = results
                .OfType<GameObjectDestroyedResult>()
                .Single();
            Assert.AreSame(starfighter, destroyed.DestroyedObject);
            Assert.AreSame(scene.blockadedDestination, destroyed.Context);
        }

        [Test]
        public void BlockadeStarted_BlockaderOwnedInboundUnit_Continues()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                BlockadeSystem blockade,
                MovementSystem movement,
                GameResultProcessor resultProcessor
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

            List<GameResult> results = ProcessBlockadeStart(scene.blockade, scene.resultProcessor);

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
        public void BlockadeStarted_NearerFriendlyCarrier_IsPreferredOverOwnedPlanet()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                BlockadeSystem blockade,
                MovementSystem movement,
                GameResultProcessor resultProcessor
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
            ProcessBlockadeStart(scene.blockade, scene.resultProcessor);

            Assert.AreSame(carrier, starfighter.GetParent());
            Assert.IsNotNull(starfighter.Movement);
        }

        [Test]
        public void BlockadeStarted_BlockadedFallback_IsSkipped()
        {
            (
                GameRoot game,
                Planet origin,
                Planet blockadedDestination,
                Planet nearestSafeDestination,
                Planet fartherSafeDestination,
                BlockadeSystem blockade,
                MovementSystem movement,
                GameResultProcessor resultProcessor
            ) scene = BuildBlockadeRetargetingScene();
            Starfighter starfighter = EntityFactory.CreateStarfighter("fighter", "empire");
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(starfighter, scene.origin);
            scene.movement.RequestMove(starfighter, scene.blockadedDestination);
            AddBlockadingFleet(scene.game, scene.nearestSafeDestination);
            AddBlockadingFleet(scene.game, scene.blockadedDestination);

            ProcessBlockadeStart(scene.blockade, scene.resultProcessor);

            Assert.AreSame(scene.fartherSafeDestination, starfighter.GetParent());
            Assert.IsNotNull(starfighter.Movement);
        }

        // Builds a minimal scene: two planets in the same sector, an officer parented to
        // the origin planet, and a MovementSystem ready to use.
        private (
            GameRoot game,
            Planet origin,
            Planet destination,
            Officer officer,
            MovementSystem movement
        ) BuildScene()
        {
            GameConfig config = TestContent.Data.GameConfig;
            GameRoot game = new GameRoot(config);

            Faction empire = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet origin = new Planet
            {
                InstanceID = "p1",
                TypeID = "origin-type",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(origin, sector);

            Planet destination = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 100,
            };
            game.AttachNode(destination, sector);

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, origin);

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );

            return (game, origin, destination, officer, movement);
        }

        private static (
            GameRoot game,
            Planet origin,
            Planet firstDestination,
            Planet secondDestination,
            Fleet fleet,
            MovementSystem movement
        ) BuildWaypointScene()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.GetGalaxyMap());
            Planet origin = new Planet
            {
                InstanceID = "origin",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            Planet firstDestination = new Planet
            {
                InstanceID = "first-destination",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 50,
                PositionY = 25,
            };
            Planet secondDestination = new Planet
            {
                InstanceID = "second-destination",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 50,
            };
            game.AttachNode(origin, sector);
            game.AttachNode(firstDestination, sector);
            game.AttachNode(secondDestination, sector);

            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            CapitalShip ship = CreateMovableCapitalShip("ship");
            game.AttachNode(fleet, origin);
            game.AttachNode(ship, fleet);
            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
            return (game, origin, firstDestination, secondDestination, fleet, movement);
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
            GameConfig config = TestContent.Data.GameConfig;
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });

            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet planetA = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planetA, sector);

            Planet planetB = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 1,
                PositionY = 0,
            };
            game.AttachNode(planetB, sector);

            Planet planetC = new Planet
            {
                InstanceID = "pC",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 10,
                PositionY = 0,
            };
            game.AttachNode(planetC, sector);

            // Fleet at A with capitalShip1 carrying a starfighter, regiment, and officer.
            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(fleet, planetA);

            CapitalShip capitalShip1 = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                Hyperdrive = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
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

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );

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

        private (
            GameRoot game,
            Planet origin,
            Planet blockadedDestination,
            Planet nearestSafeDestination,
            Planet fartherSafeDestination,
            BlockadeSystem blockade,
            MovementSystem movement,
            GameResultProcessor resultProcessor
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

            BlockadeSystem blockade = new BlockadeSystem(game, new FixedRNG());
            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game),
                blockade
            );
            GameResultProcessor resultProcessor = new GameResultProcessor();
            resultProcessor.Subscribe<BlockadeChangedResult>(movement);

            return (
                game,
                origin,
                blockadedDestination,
                nearestSafeDestination,
                fartherSafeDestination,
                blockade,
                movement,
                resultProcessor
            );
        }

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

        private static List<GameResult> ProcessBlockadeStart(
            BlockadeSystem blockade,
            GameResultProcessor resultProcessor
        )
        {
            return resultProcessor.Process(blockade.ProcessTick());
        }

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

            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sector, game.GetGalaxyMap());

            Planet origin = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(origin, sector);

            Planet destination = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 100,
            };
            game.AttachNode(destination, sector);

            // Hostile fleet creates the blockade
            Fleet hostile = EntityFactory.CreateFleet("hostile", "rebels");
            game.AttachNode(hostile, origin);
            CapitalShip hostileShip = new CapitalShip
            {
                InstanceID = "hostile-ship",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(hostileShip, hostile);

            Assert.IsTrue(origin.IsBlockaded());

            BlockadeSystem blockade = new BlockadeSystem(game, rng);
            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game),
                blockade
            );

            return (game, origin, destination, movement);
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
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 4,
            };
            game.AttachNode(ship, fleet);

            Regiment regiment = new Regiment
            {
                InstanceID = $"{factionId}-reg-{planet.InstanceID}",
                OwnerInstanceID = factionId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Movement = null,
            };
            game.AttachNode(regiment, ship);

            return (fleet, regiment);
        }

        private static CapitalShip CreateMovableCapitalShip(string instanceId)
        {
            return new CapitalShip
            {
                InstanceID = instanceId,
                OwnerInstanceID = "empire",
                Hyperdrive = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        private static Faction AddFaction(GameRoot game, string instanceId)
        {
            Faction faction = new Faction { InstanceID = instanceId, DisplayName = instanceId };
            game.GetFactions().Add(faction);
            return faction;
        }

        private static void CapturePlanetSnapshot(
            GameRoot game,
            Faction faction,
            Planet planet,
            int tick
        )
        {
            PlanetSector sector = planet.GetParentOfType<PlanetSector>();
            new FogOfWarSystem(game).CaptureSnapshot(faction, planet, sector, tick);
        }

        private static PlanetSnapshot GetPlanetSnapshot(Faction faction, Planet planet)
        {
            PlanetSector sector = planet.GetParentOfType<PlanetSector>();
            return faction.Fog.Snapshots[sector.InstanceID].Planets[planet.InstanceID];
        }
    }
}
