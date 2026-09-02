using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class CaptiveSystemTests
    {
        [Test]
        public void ProcessTick_EscapeRollSucceeds_FreesOfficer()
        {
            (GameRoot game, Planet planet, Officer captive, MovementSystem movement) = BuildScene();

            CaptiveSystem system = new CaptiveSystem(game, new FixedRNG(0.0), movement);

            system.ProcessTick();

            Assert.IsFalse(captive.IsCaptured, "Officer should be freed on successful escape");
            Assert.IsNull(captive.CaptorInstanceID, "CaptorInstanceID should be cleared");
            Assert.IsFalse(captive.CanEscape, "CanEscape should be cleared after escape");
        }

        [Test]
        public void ProcessTick_EscapeRollFails_StaysCaptured()
        {
            (GameRoot game, Planet planet, Officer captive, MovementSystem movement) = BuildScene();

            CaptiveSystem system = new CaptiveSystem(game, new FixedRNG(0.99), movement);

            system.ProcessTick();

            Assert.IsTrue(captive.IsCaptured, "Officer should remain captured when escape fails");
        }

        [Test]
        public void ProcessTick_EscapeSucceeds_ShiftsLoyalty()
        {
            (GameRoot game, Planet planet, Officer captive, MovementSystem movement) = BuildScene();

            CaptiveSystem system = new CaptiveSystem(game, new FixedRNG(0.0), movement);

            system.ProcessTick();

            Assert.AreEqual(70, captive.Loyalty, "Loyalty should decrease by EscapeLoyaltyShift");
        }

        [Test]
        public void ProcessTick_EscapeSucceeds_EmitsCaptureStateResult()
        {
            (GameRoot game, Planet planet, Officer captive, MovementSystem movement) = BuildScene();

            CaptiveSystem system = new CaptiveSystem(game, new FixedRNG(0.0), movement);

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

            CaptiveSystem system = new CaptiveSystem(game, new FixedRNG(0.0), movement);

            system.ProcessTick();

            Assert.IsTrue(captive.IsCaptured, "Officer with CanEscape=false should not escape");
        }

        [Test]
        public void ProcessTick_KilledOfficer_SkipsEscapeAttempt()
        {
            (GameRoot game, Planet planet, Officer captive, MovementSystem movement) = BuildScene();
            captive.IsKilled = true;

            CaptiveSystem system = new CaptiveSystem(game, new FixedRNG(0.0), movement);

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

            CaptiveSystem system = new CaptiveSystem(game, new FixedRNG(0.5), movement);

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

            CaptiveSystem system = new CaptiveSystem(game, new FixedRNG(0.2), movement);

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

            CaptiveSystem system = new CaptiveSystem(game, new FixedRNG(0.0), movement);

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
            CaptiveSystem system = new CaptiveSystem(game, new FixedRNG(0.2), movement);

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
            CaptiveSystem system = new CaptiveSystem(game, new FixedRNG(0.2), movement);

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
            CaptiveSystem system = new CaptiveSystem(game, new FixedRNG(0.0), movement);

            system.ProcessTick();

            Assert.IsTrue(captive.IsCaptured);
            Assert.AreSame(ship, captive.GetParent());
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
