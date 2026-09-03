using System.Collections.Generic;
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
using Rebellion.Util.Extensions;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class CaptiveSystemTests
    {
        [Test]
        public void HandleResults_CaptureAtCaptorPlanet_RecordsImmediateCustody()
        {
            (GameRoot game, Planet planet, Officer captive, MovementSystem movement) = BuildScene();
            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.0), movement);

            system.HandleResults(new[] { CaptureResult(captive, planet) });

            Assert.AreSame(planet, captive.GetParent());
            Assert.IsNull(captive.Movement);
            PlanetSnapshot snapshot = GetOfficerOwnerSnapshot(game, captive, planet);
            Officer observed = snapshot.Officers.Single(officer =>
                officer.InstanceID == captive.InstanceID
            );
            Assert.AreNotSame(captive, observed);
            Assert.IsTrue(observed.IsCaptured);
            Assert.IsNull(observed.Movement);
        }

        [Test]
        public void HandleResults_CaptureInsideForeignContainerAtCaptorPlanet_MovesToPlanet()
        {
            (GameRoot game, Planet planet, Officer captive, MovementSystem movement) = BuildScene();
            StubMission mission = new StubMission
            {
                InstanceID = "mission",
                OwnerInstanceID = captive.OwnerInstanceID,
            };
            game.AttachNode(mission, planet);
            game.MoveNode(captive, mission);
            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.0), movement);

            system.HandleResults(new[] { CaptureResult(captive, planet) });

            Assert.AreSame(planet, captive.GetParent());
            Assert.IsNull(captive.Movement);
        }

        [Test]
        public void HandleResults_CaptureByShipAwayFromCaptorPlanet_BoardsCapturingShip()
        {
            (
                GameRoot game,
                Planet capturePlanet,
                Officer captive,
                Fleet fleet,
                CapitalShip ship,
                MovementSystem movement
            ) = BuildFleetCustodyScene();
            game.MoveNode(captive, capturePlanet);
            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.0), movement);

            system.HandleResults(
                new[] { CaptureResult(captive, capturePlanet, capturingUnit: ship) }
            );

            Assert.AreSame(ship, captive.GetParent());
            Assert.IsNull(captive.Movement);
            PlanetSnapshot snapshot = GetOfficerOwnerSnapshot(game, captive, capturePlanet);
            Officer observed = snapshot
                .Fleets.Single(candidate => candidate.InstanceID == fleet.InstanceID)
                .GetChildren<CapitalShip>()
                .Single(candidate => candidate.InstanceID == ship.InstanceID)
                .GetChildren<Officer>()
                .Single(candidate => candidate.InstanceID == captive.InstanceID);
            Assert.IsTrue(observed.IsCaptured);

            Planet destination = new Planet
            {
                InstanceID = "captor-destination",
                OwnerInstanceID = captive.CaptorInstanceID,
                IsColonized = true,
                PositionX = 200,
                PositionY = 0,
            };
            game.AttachNode(destination, capturePlanet.GetParent());

            movement.RequestMove(fleet, destination);

            Assert.AreSame(destination, fleet.GetParent());
            Assert.AreSame(ship, captive.GetParent());
            Assert.AreSame(fleet.Movement, captive.GetTransitMovement());
            Assert.AreEqual(
                capturePlanet.InstanceID,
                game.GetFactionByOwnerInstanceID(captive.OwnerInstanceID).Fog.EntityLastSeenAt[
                    captive.InstanceID
                ]
            );
        }

        [Test]
        public void HandleResults_CaptureWithoutPhysicalCaptor_PlacesAtCustodyDestination()
        {
            (GameRoot game, Planet destination, Officer captive, MovementSystem movement) =
                BuildScene();
            Planet capturePlanet = game.GetSceneNodeByInstanceID<Planet>("emp_planet");
            game.MoveNode(captive, capturePlanet);
            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.0), movement);

            system.HandleResults(new[] { CaptureResult(captive, capturePlanet) });

            Assert.AreSame(destination, captive.GetParent());
            Assert.AreEqual(captive.CaptorInstanceID, destination.OwnerInstanceID);
            Assert.IsNull(captive.Movement);
            PlanetSnapshot snapshot = GetOfficerOwnerSnapshot(game, captive, destination);
            Officer observed = snapshot.Officers.Single(officer =>
                officer.InstanceID == captive.InstanceID
            );
            Assert.IsTrue(observed.IsCaptured);
            Assert.IsNull(observed.Movement);
            Assert.AreEqual(
                destination.InstanceID,
                game.GetFactionByOwnerInstanceID(captive.OwnerInstanceID).Fog.EntityLastSeenAt[
                    captive.InstanceID
                ]
            );
        }

        [Test]
        public void HandleResults_CaptureAtUncolonizedCaptorPlanet_UsesFallbackDestination()
        {
            (GameRoot game, Planet destination, Officer captive, MovementSystem movement) =
                BuildScene();
            Planet capturePlanet = game.GetSceneNodeByInstanceID<Planet>("emp_planet");
            game.MoveNode(captive, capturePlanet);
            capturePlanet.OwnerInstanceID = captive.CaptorInstanceID;
            capturePlanet.IsColonized = false;
            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.0), movement);

            system.HandleResults(new[] { CaptureResult(captive, capturePlanet) });

            Assert.AreSame(destination, captive.GetParent());
            Assert.IsNull(captive.Movement);
        }

        [Test]
        public void HandleResults_CaptureByOfficerAwayFromCaptorPlanet_MovesWithEscort()
        {
            (GameRoot game, Planet destination, Officer captive, MovementSystem movement) =
                BuildScene();
            Planet capturePlanet = game.GetSceneNodeByInstanceID<Planet>("emp_planet");
            Officer escort = EntityFactory.CreateOfficer("captor", captive.CaptorInstanceID);
            StubMission mission = new StubMission
            {
                InstanceID = "captor-mission",
                OwnerInstanceID = captive.CaptorInstanceID,
            };
            game.AttachNode(mission, capturePlanet);
            game.AttachNode(escort, mission);
            game.MoveNode(captive, capturePlanet);
            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.0), movement);

            List<GameResult> results = system.HandleResults(
                new[] { CaptureResult(captive, capturePlanet, capturingUnit: escort) }
            );

            Assert.AreSame(destination, escort.GetParent());
            Assert.AreSame(destination, captive.GetParent());
            Assert.IsNotNull(escort.Movement);
            Assert.IsNotNull(captive.Movement);
            Assert.AreEqual(escort.Movement.MovementGroupID, captive.Movement.MovementGroupID);
            Assert.IsTrue(
                results
                    .OfType<GameObjectEnrouteResult>()
                    .Any(result => ReferenceEquals(result.GameObject, escort))
            );
            Assert.IsTrue(
                results
                    .OfType<GameObjectEnrouteResult>()
                    .Any(result => ReferenceEquals(result.GameObject, captive))
            );
        }

        [Test]
        public void HandleResults_CaptureWithEstablishedTransfer_PreservesTransfer()
        {
            (GameRoot game, Planet destination, Officer captive, MovementSystem movement) =
                BuildScene();
            Planet capturePlanet = game.GetSceneNodeByInstanceID<Planet>("emp_planet");
            MovementState establishedMovement = new MovementState
            {
                TransitTicks = 5,
                MovementGroupID = "return-group",
                OriginPosition = capturePlanet.GetPosition(),
                CurrentPosition = capturePlanet.GetPosition(),
            };
            game.MoveNode(captive, destination);
            captive.Movement = establishedMovement;
            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.0), movement);

            system.HandleResults(new[] { CaptureResult(captive, capturePlanet) });

            Assert.AreSame(destination, captive.GetParent());
            Assert.AreSame(establishedMovement, captive.Movement);
            PlanetSnapshot snapshot = GetOfficerOwnerSnapshot(game, captive, destination);
            Officer observed = snapshot.Officers.Single(officer =>
                officer.InstanceID == captive.InstanceID
            );
            Assert.AreEqual("return-group", observed.Movement.MovementGroupID);
        }

        [Test]
        public void HandleResults_InactiveCaptureAwayFromCaptorPlanet_PlacesAtCustodyDestination()
        {
            (GameRoot game, Planet destination, Officer captive, MovementSystem movement) =
                BuildScene();
            Planet capturePlanet = game.GetSceneNodeByInstanceID<Planet>("emp_planet");
            game.MoveNode(captive, capturePlanet);
            captive.IsEnabled = false;
            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.0), movement);

            system.HandleResults(new[] { CaptureResult(captive, capturePlanet) });

            Assert.AreSame(destination, captive.GetParent());
            Assert.AreEqual(captive.CaptorInstanceID, destination.OwnerInstanceID);
            Assert.IsNull(captive.Movement);
            PlanetSnapshot snapshot = GetOfficerOwnerSnapshot(game, captive, destination);
            Officer observed = snapshot.Officers.Single(officer =>
                officer.InstanceID == captive.InstanceID
            );
            Assert.IsNull(observed.Movement);
        }

        [Test]
        public void HandleResults_CustodyTransferArrives_DoesNotRefreshCaptureSnapshot()
        {
            (GameRoot game, Planet destination, Officer captive, MovementSystem movement) =
                BuildScene();
            Planet capturePlanet = game.GetSceneNodeByInstanceID<Planet>("emp_planet");
            game.MoveNode(captive, capturePlanet);
            Officer escort = EntityFactory.CreateOfficer("captor", captive.CaptorInstanceID);
            StubMission mission = new StubMission
            {
                InstanceID = "captor-mission",
                OwnerInstanceID = captive.CaptorInstanceID,
            };
            game.AttachNode(mission, capturePlanet);
            game.AttachNode(escort, mission);
            game.CurrentTick = 11;
            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.0), movement);
            system.HandleResults(
                new[] { CaptureResult(captive, capturePlanet, 10, capturingUnit: escort) }
            );
            PlanetSnapshot snapshot = GetOfficerOwnerSnapshot(game, captive, destination);
            Officer observed = snapshot.Officers.Single(officer =>
                officer.InstanceID == captive.InstanceID
            );
            captive.Movement.TransitTicks = 1;

            game.CurrentTick = 12;
            movement.ProcessTick();

            Assert.IsNull(captive.Movement);
            Assert.AreEqual(10, snapshot.TickCaptured);
            Assert.IsNotNull(observed.Movement);
            Assert.AreEqual(0, observed.Movement.TicksElapsed);
        }

        [Test]
        public void HandleResults_ReleasedOfficer_RemovesCaptureSnapshot()
        {
            (GameRoot game, Planet planet, Officer captive, MovementSystem movement) = BuildScene();
            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.0), movement);
            system.HandleResults(new[] { CaptureResult(captive, planet) });
            captive.IsCaptured = false;
            captive.CaptorInstanceID = null;

            system.HandleResults(
                new[]
                {
                    new OfficerCaptureStateResult { TargetOfficer = captive, IsCaptured = false },
                }
            );

            Faction owner = game.GetFactionByOwnerInstanceID(captive.OwnerInstanceID);
            Assert.IsFalse(owner.Fog.EntityLastSeenAt.ContainsKey(captive.InstanceID));
            Assert.IsFalse(
                owner
                    .Fog.Snapshots.Values.SelectMany(snapshot => snapshot.Planets.Values)
                    .SelectMany(snapshot => snapshot.Officers)
                    .Any(officer => officer.InstanceID == captive.InstanceID)
            );
        }

        [Test]
        public void ProcessTick_EscapeRollSucceeds_FreesOfficer()
        {
            (GameRoot game, Planet planet, Officer captive, MovementSystem movement) = BuildScene();

            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.0), movement);

            system.ProcessTick();

            Assert.IsFalse(captive.IsCaptured, "Officer should be freed on successful escape");
            Assert.IsNull(captive.CaptorInstanceID, "CaptorInstanceID should be cleared");
            Assert.IsFalse(captive.CanEscape, "CanEscape should be cleared after escape");
        }

        [Test]
        public void ProcessTick_EscapeRollFails_StaysCaptured()
        {
            (GameRoot game, Planet planet, Officer captive, MovementSystem movement) = BuildScene();

            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.99), movement);

            system.ProcessTick();

            Assert.IsTrue(captive.IsCaptured, "Officer should remain captured when escape fails");
        }

        [Test]
        public void ProcessTick_EscapeSucceeds_ShiftsLoyalty()
        {
            (GameRoot game, Planet planet, Officer captive, MovementSystem movement) = BuildScene();

            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.0), movement);

            system.ProcessTick();

            Assert.AreEqual(70, captive.Loyalty, "Loyalty should decrease by EscapeLoyaltyShift");
        }

        [Test]
        public void ProcessTick_EscapeSucceeds_EmitsCaptureStateResult()
        {
            (GameRoot game, Planet planet, Officer captive, MovementSystem movement) = BuildScene();

            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.0), movement);

            List<GameResult> results = system.ProcessTick();

            OfficerCaptureStateResult result = results
                .OfType<OfficerCaptureStateResult>()
                .FirstOrDefault();
            Assert.IsNotNull(result, "Should emit OfficerCaptureStateResult");
            Assert.IsFalse(
                result.IsCaptured,
                "Result should indicate officer is no longer captured"
            );
        }

        [Test]
        public void ProcessTick_CanEscapeFalse_SkipsEscapeAttempt()
        {
            (GameRoot game, Planet planet, Officer captive, MovementSystem movement) = BuildScene();
            captive.CanEscape = false;

            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.0), movement);

            system.ProcessTick();

            Assert.IsTrue(captive.IsCaptured, "Officer with CanEscape=false should not escape");
        }

        [Test]
        public void ProcessTick_KilledOfficer_SkipsEscapeAttempt()
        {
            (GameRoot game, Planet planet, Officer captive, MovementSystem movement) = BuildScene();
            captive.IsKilled = true;

            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.0), movement);

            system.ProcessTick();

            Assert.IsTrue(captive.IsCaptured, "Killed officer should not attempt escape");
        }

        [Test]
        public void ProcessTick_StrongGarrison_LowerEscapeChance()
        {
            (GameRoot game, Planet planet, Officer captive, MovementSystem movement) = BuildScene();

            Officer guard = EntityFactory.CreateOfficer("guard", "rebels");
            guard.SetBaseRating(OfficerRating.Combat, 100);
            game.AttachNode(guard, planet);

            for (int i = 0; i < 10; i++)
            {
                Regiment regiment = new Regiment
                {
                    InstanceID = $"r{i}",
                    OwnerInstanceID = "rebels",
                    DefenseRating = 10,
                };
                game.AttachNode(regiment, planet);
            }

            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.5), movement);

            system.ProcessTick();

            Assert.IsTrue(
                captive.IsCaptured,
                "Officer should not escape with strong garrison and moderate roll"
            );
        }

        [Test]
        public void ProcessTick_NoGarrison_HigherEscapeChance()
        {
            (GameRoot game, Planet planet, Officer captive, MovementSystem movement) = BuildScene();
            captive.SetBaseRating(OfficerRating.Espionage, 80);
            captive.SetBaseRating(OfficerRating.Combat, 80);

            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.2), movement);

            system.ProcessTick();

            Assert.IsFalse(
                captive.IsCaptured,
                "High-skill officer on ungarrisoned planet should escape with moderate roll"
            );
        }

        [Test]
        public void ProcessTick_LoyaltyClampsToZero_DoesNotGoNegative()
        {
            (GameRoot game, Planet planet, Officer captive, MovementSystem movement) = BuildScene();
            captive.Loyalty = 5;

            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.0), movement);

            system.ProcessTick();

            Assert.AreEqual(0, captive.Loyalty, "Loyalty should clamp to 0, not go negative");
        }

        [Test]
        public void ProcessTick_CaptiveAboardFleet_UsesCaptorFleetGuards()
        {
            (
                GameRoot game,
                Planet _,
                Officer captive,
                Fleet _,
                CapitalShip ship,
                MovementSystem movement
            ) = BuildFleetCustodyScene();
            captive.SetBaseRating(OfficerRating.Espionage, 40);
            captive.SetBaseRating(OfficerRating.Combat, 40);
            Officer guard = EntityFactory.CreateOfficer("guard", "rebels");
            guard.SetBaseRating(OfficerRating.Combat, 100);
            game.AttachNode(guard, ship);
            for (int index = 0; index < 10; index++)
            {
                Regiment regiment = EntityFactory.CreateRegiment($"guard-{index}", "rebels");
                regiment.ManufacturingStatus = ManufacturingStatus.Complete;
                game.AttachNode(regiment, ship);
            }
            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.2), movement);

            system.ProcessTick();

            Assert.IsTrue(captive.IsCaptured);
            Assert.AreSame(ship, captive.GetParent());
        }

        [Test]
        public void ProcessTick_CaptiveAboardFleet_IgnoresPlanetGarrison()
        {
            (
                GameRoot game,
                Planet planet,
                Officer captive,
                Fleet _,
                CapitalShip ship,
                MovementSystem movement
            ) = BuildFleetCustodyScene();
            captive.SetBaseRating(OfficerRating.Espionage, 40);
            captive.SetBaseRating(OfficerRating.Combat, 40);
            Officer planetGuard = EntityFactory.CreateOfficer("planet-guard", "empire");
            planetGuard.SetBaseRating(OfficerRating.Combat, 100);
            game.AttachNode(planetGuard, planet);
            for (int index = 0; index < 10; index++)
            {
                Regiment regiment = EntityFactory.CreateRegiment(
                    $"planet-regiment-{index}",
                    "empire"
                );
                regiment.ManufacturingStatus = ManufacturingStatus.Complete;
                game.AttachNode(regiment, planet);
            }
            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.2), movement);

            system.ProcessTick();

            Assert.IsFalse(captive.IsCaptured);
            Assert.AreSame(planet, captive.GetParent());
        }

        [Test]
        public void ProcessTick_CaptiveInTransit_SkipsEscapeAttempt()
        {
            (
                GameRoot game,
                Planet _,
                Officer captive,
                Fleet fleet,
                CapitalShip ship,
                MovementSystem movement
            ) = BuildFleetCustodyScene();
            fleet.Movement = new MovementState { TransitTicks = 2 };
            CaptiveSystem system = CreateSystem(game, new FixedRNG(0.0), movement);

            system.ProcessTick();

            Assert.IsTrue(captive.IsCaptured);
            Assert.AreSame(ship, captive.GetParent());
        }

        private static CaptiveSystem CreateSystem(
            GameRoot game,
            FixedRNG provider,
            MovementSystem movement
        )
        {
            return new CaptiveSystem(game, provider, movement, new FogOfWarSystem(game));
        }

        private static OfficerCaptureStateResult CaptureResult(
            Officer officer,
            Planet context,
            int tick = 0,
            ISceneNode capturingUnit = null
        )
        {
            return new OfficerCaptureStateResult
            {
                TargetOfficer = officer,
                IsCaptured = true,
                CapturingUnit = capturingUnit,
                Context = context,
                Tick = tick,
            };
        }

        private static PlanetSnapshot GetOfficerOwnerSnapshot(
            GameRoot game,
            Officer officer,
            Planet planet
        )
        {
            Faction owner = game.GetFactionByOwnerInstanceID(officer.OwnerInstanceID);
            PlanetSector sector = planet.GetParentOfType<PlanetSector>();
            return owner.Fog.Snapshots[sector.InstanceID].Planets[planet.InstanceID];
        }

        private (
            GameRoot game,
            Planet planet,
            Officer captive,
            MovementSystem movement
        ) BuildScene()
        {
            GameConfig config = new GameConfig();
            config.Captive = new GameConfig.CaptiveConfig
            {
                EscapeTable = new Dictionary<int, int>
                {
                    { -50, 1 },
                    { -49, 2 },
                    { -31, 3 },
                    { -11, 5 },
                    { 10, 10 },
                    { 20, 15 },
                    { 30, 20 },
                    { 40, 25 },
                    { 50, 30 },
                },
                EscapeLoyaltyShift = -10,
            };
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planetSector, game.Galaxy);

            Planet empirePlanet = new Planet
            {
                InstanceID = "emp_planet",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(empirePlanet, planetSector);

            Planet rebelPlanet = new Planet
            {
                InstanceID = "reb_planet",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(rebelPlanet, planetSector);

            Officer captive = EntityFactory.CreateOfficer("captive", "empire");
            captive.IsCaptured = true;
            captive.CaptorInstanceID = "rebels";
            captive.CanEscape = true;
            captive.Loyalty = 80;
            game.AttachNode(captive, rebelPlanet);

            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
            return (game, rebelPlanet, captive, movement);
        }

        private (
            GameRoot game,
            Planet planet,
            Officer captive,
            Fleet fleet,
            CapitalShip ship,
            MovementSystem movement
        ) BuildFleetCustodyScene()
        {
            (GameRoot game, Planet planet, Officer captive, MovementSystem movement) = BuildScene();
            game.ChangeOwnership(planet, "empire");
            Fleet fleet = EntityFactory.CreateFleet("fleet", "rebels");
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 10,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.MoveNode(captive, ship);
            return (game, planet, captive, fleet, ship, movement);
        }
    }
}
