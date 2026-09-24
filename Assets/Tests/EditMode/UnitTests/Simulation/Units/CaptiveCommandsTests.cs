using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;
using Rebellion.Util.Random;
using UnityEngine;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class CaptiveCommandsTests
    {
        [Test]
        public void ProcessTick_EscapeRollSucceeds_FreesOfficer()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();

            CaptiveCommands system = CreateCommands(game, new FixedRNG(0.0), movement);

            new CaptiveTickProcessor(system).ProcessTick(game);

            Assert.IsFalse(captive.IsCaptured, "Officer should be freed on successful escape");
            Assert.IsNull(captive.CaptorInstanceID, "CaptorInstanceID should be cleared");
            Assert.IsFalse(captive.CanEscape, "CanEscape should be cleared after escape");
        }

        /// <summary>
        /// Verifies a successful escape stops evaluating later destinations.
        /// </summary>
        [Test]
        public void ProcessTick_MultipleEscapeDestinations_StopsAfterAcceptedMove()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            PlanetSector sector = planet.GetParentOfType<PlanetSector>();
            Planet alternateDestination = new Planet
            {
                InstanceID = "alternate_empire_planet",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 200,
                PositionY = 0,
            };
            game.AttachNode(alternateDestination, sector);
            CaptiveCommands commands = CreateCommands(game, new FixedRNG(0.0), movement);
            int transitRejections = 0;
            Application.LogCallback handler = (condition, _, _) =>
            {
                if (condition.Contains("already in transit", StringComparison.Ordinal))
                    transitRejections++;
            };
            Application.logMessageReceived += handler;

            try
            {
                new CaptiveTickProcessor(commands).ProcessTick(game);
            }
            finally
            {
                Application.logMessageReceived -= handler;
            }

            Assert.AreEqual(0, transitRejections);
            Assert.AreEqual("emp_planet", captive.GetParent()?.InstanceID);
            Assert.IsNotNull(captive.Movement);
        }

        [Test]
        public void ProcessTick_UnscheduledCaptive_SchedulesEscapeAttempt()
        {
            (GameRoot game, Planet _, Officer captive, MovementCommands movement) = BuildScene();
            captive.NextEscapeAttemptTick = 0;
            CaptiveCommands system = CreateCommands(game, new FixedRNG(0.0), movement);

            IReadOnlyList<GameResult> results = new CaptiveTickProcessor(system).ProcessTick(game);

            Assert.IsTrue(captive.IsCaptured);
            Assert.AreEqual(101, captive.NextEscapeAttemptTick);
            Assert.IsEmpty(results);
        }

        [Test]
        public void ProcessTick_UnscheduledCaptiveUsesMaximumRoll_SchedulesMaximumInterval()
        {
            (GameRoot game, Planet _, Officer captive, MovementCommands movement) = BuildScene();
            captive.NextEscapeAttemptTick = 0;
            CaptiveCommands system = CreateCommands(game, new MaximumRNG(), movement);

            new CaptiveTickProcessor(system).ProcessTick(game);

            Assert.AreEqual(1101, captive.NextEscapeAttemptTick);
        }

        [Test]
        public void ProcessTick_EscapeAttemptNotDue_SkipsEscapeRoll()
        {
            (GameRoot game, Planet _, Officer captive, MovementCommands movement) = BuildScene();
            captive.NextEscapeAttemptTick = 100;
            CaptiveCommands system = CreateCommands(game, new ThrowingRNG(), movement);

            IReadOnlyList<GameResult> results = new CaptiveTickProcessor(system).ProcessTick(game);

            Assert.IsTrue(captive.IsCaptured);
            Assert.IsEmpty(results);
        }

        [Test]
        public void ProcessTick_CaptivesWithDifferentSchedules_EvaluatesOnlyDueCaptive()
        {
            (GameRoot game, Planet planet, Officer dueCaptive, MovementCommands movement) =
                BuildScene();
            Officer waitingCaptive = EntityFactory.CreateOfficer("waiting", "empire");
            waitingCaptive.IsCaptured = true;
            waitingCaptive.CaptorInstanceID = "rebels";
            waitingCaptive.CanEscape = true;
            waitingCaptive.NextEscapeAttemptTick = game.CurrentTick + 10;
            game.AttachNode(waitingCaptive, planet);
            dueCaptive.NextEscapeAttemptTick = game.CurrentTick;
            CaptiveCommands system = CreateCommands(game, new FixedRNG(0.0), movement);

            IReadOnlyList<GameResult> results = new CaptiveTickProcessor(system).ProcessTick(game);

            Assert.IsFalse(dueCaptive.IsCaptured);
            Assert.IsTrue(waitingCaptive.IsCaptured);
            Assert.AreEqual(game.CurrentTick + 10, waitingCaptive.NextEscapeAttemptTick);
            Assert.AreEqual(1, results.Count);
        }

        [Test]
        public void ProcessTick_EscapeRollFails_ReschedulesEscapeAttempt()
        {
            (GameRoot game, Planet _, Officer captive, MovementCommands movement) = BuildScene();
            captive.NextEscapeAttemptTick = game.CurrentTick;
            CaptiveCommands system = CreateCommands(game, new FixedRNG(0.99), movement);

            new CaptiveTickProcessor(system).ProcessTick(game);

            Assert.AreEqual(101, captive.NextEscapeAttemptTick);
        }

        [Test]
        public void ProcessTick_EscapeRollFails_StaysCaptured()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();

            CaptiveCommands system = CreateCommands(game, new FixedRNG(0.99), movement);

            new CaptiveTickProcessor(system).ProcessTick(game);

            Assert.IsTrue(captive.IsCaptured, "Officer should remain captured when escape fails");
        }

        [Test]
        public void ProcessTick_EscapeSucceeds_ShiftsLoyalty()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();

            CaptiveCommands system = CreateCommands(game, new FixedRNG(0.0), movement);

            new CaptiveTickProcessor(system).ProcessTick(game);

            Assert.AreEqual(70, captive.Loyalty, "Loyalty should decrease by EscapeLoyaltyShift");
        }

        [Test]
        public void ProcessTick_EscapeSucceeds_EmitsCaptureStateResult()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();

            CaptiveCommands system = CreateCommands(game, new FixedRNG(0.0), movement);

            IReadOnlyList<GameResult> results = new CaptiveTickProcessor(system).ProcessTick(game);

            OfficerCaptureStateResult result = results
                .OfType<OfficerCaptureStateResult>()
                .FirstOrDefault();
            Assert.IsNotNull(result, "Should emit OfficerCaptureStateResult");
            Assert.IsFalse(
                result.IsCaptured,
                "Result should indicate officer is no longer captured"
            );
            Assert.AreEqual(
                "rebels",
                result.CaptorInstanceID,
                "Result should retain the faction that lost custody"
            );
        }

        [Test]
        public void ProcessTick_EscapeSucceedsWithoutFriendlyDestination_RemainsCaptured()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            Planet friendlyPlanet = game.GetSceneNodeByInstanceID<Planet>("emp_planet");
            game.ChangeOwnership(friendlyPlanet, "rebels");
            CaptiveCommands system = CreateCommands(game, new FixedRNG(0.0), movement);

            IReadOnlyList<GameResult> results = new CaptiveTickProcessor(system).ProcessTick(game);

            Assert.IsTrue(captive.IsCaptured);
            Assert.AreEqual("rebels", captive.CaptorInstanceID);
            Assert.IsTrue(captive.CanEscape);
            Assert.AreSame(planet, captive.GetParent());
            Assert.IsNull(captive.Movement);
            Assert.IsEmpty(results.OfType<OfficerCaptureStateResult>());
        }

        [Test]
        public void ProcessTick_EscapeSucceedsWithFriendlyFleet_MovesOfficerToFleet()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            Planet friendlyPlanet = game.GetSceneNodeByInstanceID<Planet>("emp_planet");
            game.ChangeOwnership(friendlyPlanet, "rebels");
            Fleet fleet = EntityFactory.CreateFleet("friendly_fleet", "empire");
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "friendly_ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            CaptiveCommands system = CreateCommands(game, new FixedRNG(0.0), movement);

            IReadOnlyList<GameResult> results = new CaptiveTickProcessor(system).ProcessTick(game);

            Assert.IsFalse(captive.IsCaptured);
            Assert.IsNull(captive.CaptorInstanceID);
            Assert.IsFalse(captive.CanEscape);
            Assert.AreSame(ship, captive.GetParent());
            Assert.AreEqual(1, results.OfType<OfficerCaptureStateResult>().Count());
        }

        [Test]
        public void ProcessTick_FriendlyFleetFirstShipUnavailable_MovesOfficerToOperationalShip()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            Planet friendlyPlanet = game.GetSceneNodeByInstanceID<Planet>("emp_planet");
            game.ChangeOwnership(friendlyPlanet, "rebels");
            Fleet fleet = EntityFactory.CreateFleet("friendly_fleet", "empire");
            CapitalShip unavailableShip = new CapitalShip
            {
                InstanceID = "unavailable_ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            CapitalShip operationalShip = new CapitalShip
            {
                InstanceID = "operational_ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(unavailableShip, fleet);
            game.AttachNode(operationalShip, fleet);
            CaptiveCommands system = CreateCommands(game, new FixedRNG(0.0), movement);

            IReadOnlyList<GameResult> results = new CaptiveTickProcessor(system).ProcessTick(game);

            Assert.IsFalse(captive.IsCaptured);
            Assert.AreSame(operationalShip, captive.GetParent());
            Assert.AreEqual(1, results.OfType<OfficerCaptureStateResult>().Count());
        }

        [Test]
        public void ProcessTick_CanEscapeFalse_SkipsEscapeAttempt()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            captive.CanEscape = false;

            CaptiveCommands system = CreateCommands(game, new FixedRNG(0.0), movement);

            new CaptiveTickProcessor(system).ProcessTick(game);

            Assert.IsTrue(captive.IsCaptured, "Officer with CanEscape=false should not escape");
        }

        [Test]
        public void ProcessTick_KilledOfficer_SkipsEscapeAttempt()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            captive.IsKilled = true;

            CaptiveCommands system = CreateCommands(game, new FixedRNG(0.0), movement);

            new CaptiveTickProcessor(system).ProcessTick(game);

            Assert.IsTrue(captive.IsCaptured, "Killed officer should not attempt escape");
        }

        [Test]
        public void ProcessTick_StrongGarrison_LowerEscapeChance()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();

            Officer guard = EntityFactory.CreateOfficer("guard", "rebels");
            guard.SetBaseRating(SkillRating.Combat, 100);
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

            CaptiveCommands system = CreateCommands(game, new FixedRNG(0.5), movement);

            new CaptiveTickProcessor(system).ProcessTick(game);

            Assert.IsTrue(
                captive.IsCaptured,
                "Officer should not escape with strong garrison and moderate roll"
            );
        }

        [Test]
        public void ProcessTick_NoGarrison_HigherEscapeChance()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            captive.SetBaseRating(SkillRating.Espionage, 80);
            captive.SetBaseRating(SkillRating.Combat, 80);

            CaptiveCommands system = CreateCommands(game, new FixedRNG(0.2), movement);

            new CaptiveTickProcessor(system).ProcessTick(game);

            Assert.IsFalse(
                captive.IsCaptured,
                "High-skill officer on ungarrisoned planet should escape with moderate roll"
            );
        }

        [Test]
        public void ProcessTick_LoyaltyClampsToZero_DoesNotGoNegative()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            captive.Loyalty = 5;

            CaptiveCommands system = CreateCommands(game, new FixedRNG(0.0), movement);

            new CaptiveTickProcessor(system).ProcessTick(game);

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
                MovementCommands movement
            ) = BuildFleetCustodyScene();
            captive.SetBaseRating(SkillRating.Espionage, 40);
            captive.SetBaseRating(SkillRating.Combat, 40);
            Officer guard = EntityFactory.CreateOfficer("guard", "rebels");
            guard.SetBaseRating(SkillRating.Combat, 100);
            game.AttachNode(guard, ship);
            for (int index = 0; index < 10; index++)
            {
                Regiment regiment = EntityFactory.CreateRegiment($"guard-{index}", "rebels");
                regiment.ManufacturingStatus = ManufacturingStatus.Complete;
                game.AttachNode(regiment, ship);
            }
            CaptiveCommands system = CreateCommands(game, new FixedRNG(0.2), movement);

            new CaptiveTickProcessor(system).ProcessTick(game);

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
                MovementCommands movement
            ) = BuildFleetCustodyScene();
            captive.SetBaseRating(SkillRating.Espionage, 40);
            captive.SetBaseRating(SkillRating.Combat, 40);
            Officer planetGuard = EntityFactory.CreateOfficer("planet-guard", "empire");
            planetGuard.SetBaseRating(SkillRating.Combat, 100);
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
            CaptiveCommands system = CreateCommands(game, new FixedRNG(0.2), movement);

            new CaptiveTickProcessor(system).ProcessTick(game);

            Assert.IsFalse(captive.IsCaptured);
            Assert.AreEqual("empire", captive.GetParentOfType<Planet>()?.OwnerInstanceID);
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
                MovementCommands movement
            ) = BuildFleetCustodyScene();
            fleet.Movement = new MovementState { TransitTicks = 2 };
            CaptiveCommands system = CreateCommands(game, new FixedRNG(0.0), movement);

            new CaptiveTickProcessor(system).ProcessTick(game);

            Assert.IsTrue(captive.IsCaptured);
            Assert.AreSame(ship, captive.GetParent());
        }

        [Test]
        public void ReleaseOfficer_OfficerAlreadyMoving_PreservesMovement()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            MovementState transfer = new MovementState { TransitTicks = 5 };
            captive.Movement = transfer;
            CaptiveCommands commands = CreateCommands(game, new ThrowingRNG(), movement);

            commands.ReleaseOfficer(captive, planet, game.CurrentTick, captive.CaptorInstanceID);

            Assert.AreSame(transfer, captive.Movement);
        }

        [Test]
        public void ReleaseOfficer_CaptiveReleased_PreservesLoyalty()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
            CaptiveCommands commands = CreateCommands(game, new ThrowingRNG(), movement);

            commands.ReleaseOfficer(captive, planet, game.CurrentTick, captive.CaptorInstanceID);

            Assert.AreEqual(80, captive.Loyalty);
        }

        /// <summary>
        /// Creates custody operations with the supplied movement and random providers.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="provider">The provider.</param>
        /// <param name="movement">The movement.</param>
        /// <returns>The custody operations for the test game.</returns>
        private static CaptiveCommands CreateCommands(
            GameRoot game,
            IRandomNumberProvider provider,
            MovementCommands movement
        )
        {
            return new CaptiveCommands(game, provider, movement, new FogOfWarCommands(game));
        }

        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <returns>The constructed scene.</returns>
        private (
            GameRoot game,
            Planet planet,
            Officer captive,
            MovementCommands movement
        ) BuildScene()
        {
            GameConfig config = new GameConfig();
            config.Captive = new GameConfig.CaptiveConfig
            {
                EscapeAttemptInterval = new GameConfig.TickRangeConfig
                {
                    Minimum = 100,
                    Maximum = 1100,
                },
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
            game.CurrentTick = 1;
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
            captive.NextEscapeAttemptTick = game.CurrentTick;
            captive.Loyalty = 80;
            game.AttachNode(captive, rebelPlanet);

            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            return (game, rebelPlanet, captive, movement);
        }

        /// <summary>
        /// Builds fleet custody scene.
        /// </summary>
        /// <returns>The constructed fleet custody scene.</returns>
        private (
            GameRoot game,
            Planet planet,
            Officer captive,
            Fleet fleet,
            CapitalShip ship,
            MovementCommands movement
        ) BuildFleetCustodyScene()
        {
            (GameRoot game, Planet planet, Officer captive, MovementCommands movement) =
                BuildScene();
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
