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
using Rebellion.Util.Common;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class UprisingSystemTests
    {
        [Test]
        public void ProcessTick_SufficientGarrison_NoUprising()
        {
            // Garrison requirement is 5 at support 10. Five troops meets it exactly.
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 5
            );

            system.ProcessTick();

            Assert.IsFalse(planet.IsInUprising, "Sufficient garrison should prevent uprising");
        }

        [Test]
        public void ProcessTick_NoGarrison_UprisingStarts()
        {
            // Garrison requirement is 5 at support 10. Zero troops means a deficit, so uprising starts.
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 0
            );

            List<GameResult> results = system.ProcessTick();

            Assert.IsTrue(planet.IsInUprising, "Garrison deficit should trigger uprising");
            Assert.AreEqual(
                "empire",
                planet.OwnerInstanceID,
                "UprisingSystem must not change ownership"
            );
            Assert.IsTrue(results.OfType<PlanetUprisingStartedResult>().Any());
        }

        [Test]
        public void ProcessTick_ExactGarrison_NoUprising()
        {
            // Garrison requirement is 5 at support 10. Five troops exactly meets it, no uprising.
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 5
            );

            system.ProcessTick();

            Assert.IsFalse(
                planet.IsInUprising,
                "Exactly sufficient garrison should not trigger uprising"
            );
        }

        [Test]
        public void ProcessTick_GarrisonFallsToRequirement_ReportsNearUprisingOnce()
        {
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 6
            );
            game.DetachNode(planet.GetChildren<Regiment>()[0]);

            List<GameResult> firstResults = system.ProcessTick();
            List<GameResult> secondResults = system.ProcessTick();

            Assert.AreEqual(1, firstResults.OfType<PlanetNearUprisingResult>().Count());
            Assert.IsEmpty(secondResults.OfType<PlanetNearUprisingResult>());
            Assert.IsFalse(planet.IsInUprising);
        }

        [Test]
        public void ProcessTick_ActiveUprisingWithFacility_DestroysFacility()
        {
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 1
            );
            planet.BeginUprising();
            planet.EnergyCapacity = 2;
            Building facility = EntityFactory.CreateBuilding("b1", "empire");
            Building facility2 = EntityFactory.CreateBuilding("b2", "empire");
            facility.ManufacturingStatus = ManufacturingStatus.Complete;
            facility2.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(facility, planet);
            game.AttachNode(facility2, planet);

            system.ProcessTick();
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Building>("b1"));

            game.CurrentTick = 1;
            system.ProcessTick();

            Assert.IsNull(
                game.GetSceneNodeByInstanceID<Building>(facility.InstanceID),
                "Facility should be destroyed by uprising case 1"
            );
            Assert.IsTrue(planet.IsInUprising, "Uprising should remain active after consequence");
        }

        [Test]
        public void ProcessTick_ActiveUprisingLastBuildingDestroyed_DoesNotChangeControl()
        {
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 1
            );
            planet.BeginUprising();
            planet.EnergyCapacity = 1;
            Building facility = EntityFactory.CreateBuilding("b1", "empire");
            facility.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(facility, planet);

            system.ProcessTick();
            game.CurrentTick = 1;
            List<GameResult> results = system.ProcessTick();

            Assert.AreEqual("empire", planet.GetOwnerInstanceID());
            Assert.IsTrue(planet.IsInUprising);
            Assert.IsEmpty(results.OfType<PlanetOwnershipChangedResult>());
        }

        [Test]
        public void ProcessTick_ActiveUprising_OfficerCaptured()
        {
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 1,
                rng: new SequenceRNG(intValues: new[] { 2, 1 })
            );
            ScheduleIncident(planet, 1);
            game.CurrentTick = 1;
            Officer officer = new Officer { InstanceID = "o1", OwnerInstanceID = "empire" };
            game.AttachNode(officer, planet);

            List<GameResult> results = system.ProcessTick();

            Assert.IsTrue(officer.IsCaptured, "Officer should be captured by uprising case 3");
            Assert.IsNotNull(
                officer.CaptorInstanceID,
                "CaptorInstanceID should be set to the opposing faction"
            );
            Assert.IsTrue(officer.CanEscape, "Uprising-captured officer should be able to escape");
            Assert.IsTrue(results.OfType<OfficerCaptureStateResult>().Any(r => r.IsCaptured));
        }

        [Test]
        public void ProcessTick_ActiveUprising_CapturedOfficerFreed()
        {
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 1,
                rng: new SequenceRNG(intValues: new[] { 3, 2 })
            );
            ScheduleIncident(planet, 1);
            game.CurrentTick = 1;
            Officer captive = new Officer
            {
                InstanceID = "o1",
                OwnerInstanceID = "empire",
                IsCaptured = true,
            };
            game.AttachNode(captive, planet);

            List<GameResult> results = system.ProcessTick();

            Assert.IsFalse(
                captive.IsCaptured,
                "Captured officer should be freed by uprising case 4"
            );
            Assert.IsTrue(
                results.OfType<OfficerCaptureStateResult>().Any(r => !r.IsCaptured),
                "Should emit OfficerCaptureStateResult with IsCaptured=false"
            );
        }

        [Test]
        public void ProcessTick_IncidentExcludesIncompleteFacility()
        {
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 1
            );
            ScheduleIncident(planet, 1);
            game.CurrentTick = 1;
            planet.EnergyCapacity = 1;
            Building facility = EntityFactory.CreateBuilding("b1", "empire");
            game.AttachNode(facility, planet);

            system.ProcessTick();

            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Building>("b1"));
        }

        [Test]
        public void ProcessTick_IncidentExcludesEnrouteRegiment()
        {
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 2,
                rng: new SequenceRNG(intValues: new[] { 3, 3, 2 })
            );
            Regiment enroute = EntityFactory.CreateRegiment("enroute", "empire");
            enroute.ManufacturingStatus = ManufacturingStatus.Complete;
            enroute.Movement = new MovementState();
            game.AttachNode(enroute, planet);
            ScheduleIncident(planet, 1);
            game.CurrentTick = 1;

            system.ProcessTick();

            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("enroute"));
            Assert.AreEqual(
                1,
                planet.GetChildren<Regiment>().Count(regiment => regiment.Movement == null)
            );
        }

        [Test]
        public void ProcessTick_IncidentCapturesOnlyUsableOfficer()
        {
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 1,
                rng: new SequenceRNG(intValues: new[] { 2, 1, 0 })
            );
            Officer enroute = new Officer
            {
                InstanceID = "enroute",
                OwnerInstanceID = "empire",
                Movement = new MovementState(),
            };
            Officer killed = new Officer
            {
                InstanceID = "killed",
                OwnerInstanceID = "empire",
                IsKilled = true,
            };
            Officer usable = new Officer { InstanceID = "usable", OwnerInstanceID = "empire" };
            game.AttachNode(enroute, planet);
            game.AttachNode(killed, planet);
            game.AttachNode(usable, planet);
            ScheduleIncident(planet, 1);
            game.CurrentTick = 1;

            system.ProcessTick();

            Assert.IsFalse(enroute.IsCaptured);
            Assert.IsFalse(killed.IsCaptured);
            Assert.IsTrue(usable.IsCaptured);
        }

        [Test]
        public void ProcessTick_HighSupport_NoUprising()
        {
            // At support 80, garrison requirement is 0. No uprising regardless of troops.
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 80,
                opposingSupport: 20,
                troopCount: 0,
                rng: new SequenceRNG(intValues: new[] { 8, 8 })
            );

            system.ProcessTick();

            Assert.IsFalse(planet.IsInUprising, "High support should prevent uprising");
        }

        [Test]
        public void ProcessTick_ActiveUprising_ZeroTroops_PlanetGoesNeutral()
        {
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 0
            );
            planet.BeginUprising();

            List<GameResult> results = system.ProcessTick();

            Assert.IsFalse(planet.IsInUprising, "Uprising should end when controller loses planet");
            Assert.IsNull(planet.OwnerInstanceID, "Planet should become neutral");
            PlanetOwnershipChangedResult flip = results
                .OfType<PlanetOwnershipChangedResult>()
                .SingleOrDefault();
            Assert.IsNotNull(flip, "PlanetOwnershipChangedResult should be emitted");
            Assert.AreEqual("empire", flip.PreviousOwner?.InstanceID);
            Assert.IsNull(flip.NewOwner);
        }

        [Test]
        public void ProcessTick_ActiveUprisingZeroTroopsWithOpposingSupport_TransfersControl()
        {
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 0,
                opposingSupport: 100,
                troopCount: 0
            );
            planet.BeginUprising();

            List<GameResult> results = system.ProcessTick();

            Assert.IsFalse(planet.IsInUprising);
            Assert.AreEqual("rebels", planet.OwnerInstanceID);
            PlanetOwnershipChangedResult result = results
                .OfType<PlanetOwnershipChangedResult>()
                .Single();
            Assert.AreEqual("empire", result.PreviousOwner?.InstanceID);
            Assert.AreEqual("rebels", result.NewOwner?.InstanceID);
        }

        [Test]
        public void ProcessTick_SufficientUprisingGarrison_ClearsOnlyWhenTimerExpires()
        {
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 50,
                opposingSupport: 50,
                troopCount: 2
            );
            planet.BeginUprising();

            List<GameResult> beforeTimer = system.ProcessTick();
            game.CurrentTick = 1;
            List<GameResult> atTimer = system.ProcessTick();

            Assert.IsEmpty(beforeTimer.OfType<PlanetUprisingEndedResult>());
            Assert.IsFalse(planet.IsInUprising);
            Assert.AreEqual(1, atTimer.OfType<PlanetUprisingEndedResult>().Count());
        }

        [Test]
        public void ProcessTick_IncidentIgnoresHostileFleetPresence()
        {
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 2
            );
            ScheduleIncident(planet, 1);
            game.CurrentTick = 1;
            planet.EnergyCapacity = 1;
            Building facility = EntityFactory.CreateBuilding("b1", "empire");
            facility.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(facility, planet);
            game.AttachNode(EntityFactory.CreateFleet("enemy-fleet", "rebels"), planet);

            system.ProcessTick();

            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Building>("b1"));
        }

        [Test]
        public void ProcessTick_IncidentSubtractsResistanceRegiment()
        {
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 1
            );
            planet.GetChildren<Regiment>()[0].TypeID = game.Config
                .Uprising
                .ResistanceRegimentTypeID;
            ScheduleIncident(planet, 1);
            game.CurrentTick = 1;
            planet.EnergyCapacity = 1;
            Building facility = EntityFactory.CreateBuilding("b1", "empire");
            facility.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(facility, planet);

            system.ProcessTick();

            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Building>("b1"));
        }

        [Test]
        public void ProcessTick_IncidentAppliesInciteAndSubdueLeadershipAdjustments()
        {
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 2
            );
            AttachActiveMission(game, planet, InciteUprisingMission.MissionTypeID, "rebels", 20);
            ScheduleIncident(planet, 1);
            AttachActiveMission(game, planet, SubdueUprisingMission.MissionTypeID, "empire", 10);
            game.CurrentTick = 1;
            planet.EnergyCapacity = 1;
            Building facility = EntityFactory.CreateBuilding("b1", "empire");
            facility.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(facility, planet);

            system.ProcessTick();

            Assert.IsNull(game.GetSceneNodeByInstanceID<Building>(facility.InstanceID));
            Assert.AreEqual(8, planet.GetPopularSupport("empire"));
            Assert.AreEqual(92, planet.GetPopularSupport("rebels"));
        }

        [Test]
        public void ProcessTick_IncidentExcludesMissionParticipantsInTransit()
        {
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 2
            );
            Mission mission = AttachActiveMission(
                game,
                planet,
                InciteUprisingMission.MissionTypeID,
                "rebels",
                50
            );
            mission.GetMainParticipants()[0].Movement = new MovementState();
            ScheduleIncident(planet, 1);
            game.CurrentTick = 1;
            planet.EnergyCapacity = 1;
            Building facility = EntityFactory.CreateBuilding("b1", "empire");
            facility.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(facility, planet);

            system.ProcessTick();

            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Building>("b1"));
            Assert.AreEqual(10, planet.GetPopularSupport("empire"));
        }

        [Test]
        public void ProcessTick_NeutralPlanet_Skipped()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(planetSector, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = null,
                PopularSupport = new Dictionary<string, int>(),
            };
            game.AttachNode(planet, planetSector);

            MovementSystem movementSystem = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
            PlanetaryControlSystem planetaryControl = new PlanetaryControlSystem(
                game,
                movementSystem,
                new ManufacturingSystem(game, new FleetSystem(game)),
                new FogOfWarSystem(game)
            );
            UprisingSystem uprisingSystem = new UprisingSystem(
                game,
                new StubRNG(),
                planetaryControl
            );
            uprisingSystem.ProcessTick();

            Assert.IsFalse(planet.IsInUprising, "Neutral planet should not revolt");
        }

        [Test]
        public void ProcessTick_EmpireGarrisonOnCoreSector_HalvesRequirement()
        {
            // In a core sector with GarrisonEfficiency=2, the base garrison requirement of 3
            // is halved to 1. One troop meets it, so no uprising.
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction
            {
                InstanceID = "empire",
                Settings = new FactionSettings { GarrisonEfficiency = 2 },
            };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                SectorType = PlanetSectorType.Core,
            };
            game.AttachNode(planetSector, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "empire", 30 }, { "rebels", 50 } },
            };
            game.AttachNode(planet, planetSector);
            Regiment regiment = EntityFactory.CreateRegiment("r1", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, planet);

            MovementSystem movementSystem = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
            PlanetaryControlSystem planetaryControl = new PlanetaryControlSystem(
                game,
                movementSystem,
                new ManufacturingSystem(game, new FleetSystem(game)),
                new FogOfWarSystem(game)
            );
            UprisingSystem uprisingSystem = new UprisingSystem(
                game,
                new StubRNG(),
                planetaryControl
            );
            uprisingSystem.ProcessTick();

            Assert.IsFalse(
                planet.IsInUprising,
                "GarrisonEfficiency=2 in a core sector should halve the garrison requirement"
            );
        }

        [Test]
        public void ProcessTick_EmpireGarrisonOnOuterRim_NoBonus()
        {
            // On an outer rim planet, GarrisonEfficiency does not apply. Garrison requirement
            // stays at 3 with one troop, so the deficit triggers an uprising.
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction
            {
                InstanceID = "empire",
                Settings = new FactionSettings { GarrisonEfficiency = 2 },
            };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                SectorType = PlanetSectorType.OuterRim,
            };
            game.AttachNode(planetSector, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "empire", 30 }, { "rebels", 50 } },
            };
            game.AttachNode(planet, planetSector);
            Regiment regiment = EntityFactory.CreateRegiment("r1", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, planet);

            MovementSystem movementSystem = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
            PlanetaryControlSystem planetaryControl = new PlanetaryControlSystem(
                game,
                movementSystem,
                new ManufacturingSystem(game, new FleetSystem(game)),
                new FogOfWarSystem(game)
            );
            UprisingSystem uprisingSystem = new UprisingSystem(
                game,
                new StubRNG(),
                planetaryControl
            );
            uprisingSystem.ProcessTick();

            Assert.IsTrue(
                planet.IsInUprising,
                "Outer rim should NOT apply GarrisonEfficiency — garrison deficit triggers uprising"
            );
        }

        [Test]
        public void HandleResults_GarrisonDeficit_StartsUprising()
        {
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                troopCount: 5
            );
            Regiment departingRegiment = planet.GetChildren<Regiment>()[0];
            game.DetachNode(departingRegiment);

            List<GameResult> results = system.HandleResults(
                new PlanetGarrisonChangedResult[]
                {
                    new PlanetGarrisonChangedResult { Planet = planet },
                }
            );

            Assert.IsTrue(planet.IsInUprising);
            Assert.AreEqual(1, results.OfType<PlanetUprisingStartedResult>().Count());
        }

        [Test]
        public void ReconcileGarrison_CapturedPlanetAtRequirement_ReportsNearUprising()
        {
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 10,
                opposingSupport: 50,
                troopCount: 4
            );
            foreach (Regiment regiment in planet.GetChildren<Regiment>().ToList())
                game.DetachNode(regiment);
            game.ChangeOwnership(planet, "rebels");
            Regiment occupyingRegiment = EntityFactory.CreateRegiment("occupier", "rebels");
            occupyingRegiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(occupyingRegiment, planet);

            List<GameResult> results = system.ReconcileGarrison(planet);

            Assert.AreEqual(1, results.OfType<PlanetNearUprisingResult>().Count());
            Assert.IsEmpty(results.OfType<PlanetUprisingStartedResult>());
            Assert.IsFalse(planet.IsInUprising);
        }

        [Test]
        public void ReconcileGarrison_DeficitReturnsBeforeClearPulse_CancelsClearTimer()
        {
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(
                ownerSupport: 50,
                opposingSupport: 50,
                troopCount: 2
            );
            game.Config.Uprising.ActiveSupportDriftMinTicks = 100;
            game.Config.Uprising.ActiveSupportDriftMaxTicks = 100;
            game.Config.Uprising.IncidentPulseMinTicks = 100;
            game.Config.Uprising.IncidentPulseMaxTicks = 100;
            planet.BeginUprising();
            system.ProcessTick();

            game.DetachNode(planet.GetChildren<Regiment>()[0]);
            system.ReconcileGarrison(planet);
            game.CurrentTick = 1;
            List<GameResult> results = system.ProcessTick();

            Assert.IsTrue(planet.IsInUprising);
            Assert.AreEqual(0, planet.NextUprisingClearTick);
            Assert.IsEmpty(results.OfType<PlanetUprisingEndedResult>());
        }

        [Test]
        public void TryExecuteMission_OfficersOutOfScoreOrder_AttemptsLowestScoreFirst()
        {
            CountingRNG rng = new CountingRNG();
            (GameRoot game, Planet planet, UprisingSystem system) = BuildScene(rng: rng);
            Officer highProbabilityOfficer = EntityFactory.CreateOfficer("high", "rebels");
            highProbabilityOfficer.SetBaseRating(OfficerRating.Leadership, 100);
            Officer lowProbabilityOfficer = EntityFactory.CreateOfficer("low", "rebels");
            lowProbabilityOfficer.SetBaseRating(OfficerRating.Leadership, 0);
            game.Config.ProbabilityTables.Mission.InciteUprising = new Dictionary<int, int>
            {
                { -10, 0 },
                { 90, 100 },
            };
            Mission mission = MissionTestFactory.TryCreate(
                MissionTypeIDs.InciteUprising,
                game,
                "rebels",
                planet,
                new List<IMissionParticipant> { highProbabilityOfficer, lowProbabilityOfficer }
            );
            game.AttachNode(mission, planet);

            bool handled = system.TryExecuteMission(mission, out List<GameResult> results);

            Assert.IsTrue(handled);
            Assert.AreEqual(2, rng.DoubleCallCount);
            Assert.IsNotEmpty(results.OfType<MissionCompletedResult>());
        }

        private (GameRoot game, Planet planet, UprisingSystem system) BuildScene(
            int ownerSupport = 10,
            int opposingSupport = 50,
            int troopCount = 0,
            bool isCoreSector = false,
            IRandomNumberProvider rng = null
        )
        {
            GameConfig config = TestConfig.Create();
            config.Uprising.ActiveSupportDriftMinTicks = 1;
            config.Uprising.ActiveSupportDriftMaxTicks = 1;
            config.Uprising.IncidentPulseMinTicks = 1;
            config.Uprising.IncidentPulseMaxTicks = 1;
            config.Uprising.ClearUprisingMinTicks = 1;
            config.Uprising.ClearUprisingMaxTicks = 1;
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                SectorType = isCoreSector ? PlanetSectorType.Core : PlanetSectorType.OuterRim,
            };
            game.AttachNode(planetSector, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int>
                {
                    { "empire", ownerSupport },
                    { "rebels", opposingSupport },
                },
            };
            game.AttachNode(planet, planetSector);

            // Add garrison troops
            for (int i = 0; i < troopCount; i++)
            {
                Regiment regiment = EntityFactory.CreateRegiment($"r{i}", "empire");
                regiment.ManufacturingStatus = ManufacturingStatus.Complete;
                game.AttachNode(regiment, planet);
            }

            MovementSystem movementSystem = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
            PlanetaryControlSystem planetaryControl = new PlanetaryControlSystem(
                game,
                movementSystem,
                new ManufacturingSystem(game, new FleetSystem(game)),
                new FogOfWarSystem(game)
            );
            UprisingSystem uprisingSystem = new UprisingSystem(
                game,
                rng ?? new StubRNG(),
                planetaryControl
            );
            return (game, planet, uprisingSystem);
        }

        private static void ScheduleIncident(Planet planet, int tick)
        {
            planet.BeginUprising();
            planet.NextUprisingSupportDriftTick = tick + 1;
            planet.UprisingSupportDriftTimerOrder = 1;
            planet.NextUprisingIncidentTick = tick;
            planet.UprisingIncidentTimerOrder = 2;
            planet.NextUprisingTimerOrder = 2;
        }

        private static Mission AttachActiveMission(
            GameRoot game,
            Planet planet,
            string missionTypeId,
            string ownerInstanceId,
            int leadership
        )
        {
            Officer officer = EntityFactory.CreateOfficer(
                $"{missionTypeId}-officer",
                ownerInstanceId
            );
            officer.SetBaseRating(OfficerRating.Leadership, leadership);
            Mission mission = MissionTestFactory.TryCreate(
                missionTypeId,
                game,
                ownerInstanceId,
                planet,
                new List<IMissionParticipant> { officer }
            );
            Assert.IsNotNull(mission);
            mission.InstanceID = $"{missionTypeId}-mission";
            game.AttachNode(mission, planet);
            mission.Initiate(100);
            game.AttachNode(officer, mission);
            return mission;
        }

        private sealed class CountingRNG : IRandomNumberProvider
        {
            public int DoubleCallCount { get; private set; }

            public double NextDouble()
            {
                DoubleCallCount++;
                return 0.5;
            }

            public int NextInt(int min, int max) => min;
        }
    }

    [TestFixture]
    public class GarrisonRequirementTests
    {
        [TestCase(80, 0)]
        [TestCase(60, 0)]
        [TestCase(55, 1)]
        [TestCase(50, 1)]
        [TestCase(40, 2)]
        [TestCase(30, 3)]
        [TestCase(20, 4)]
        [TestCase(10, 5)]
        [TestCase(0, 6)]
        public void CalculateGarrisonRequirement_StandardPlanet_MatchesOriginalFormula(
            int support,
            int expectedGarrison
        )
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction faction = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(faction);

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                SectorType = PlanetSectorType.OuterRim,
            };
            game.AttachNode(planetSector, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "empire", support } },
            };
            game.AttachNode(planet, planetSector);

            int garrison = UprisingSystem.CalculateGarrisonRequirement(
                planet,
                faction,
                config.AI.Garrison
            );

            Assert.AreEqual(expectedGarrison, garrison, $"Garrison for support={support}");
        }

        [Test]
        public void CalculateGarrisonRequirement_CoreWorldEmpire_Halved()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction
            {
                InstanceID = "empire",
                Settings = new FactionSettings { GarrisonEfficiency = 2 },
            };
            game.GetFactions().Add(empire);

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                SectorType = PlanetSectorType.Core,
            };
            game.AttachNode(planetSector, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "empire", 20 } },
            };
            game.AttachNode(planet, planetSector);

            // Base: ceil((60-20)/10) = 4. Halved: 4/2 = 2.
            int garrison = UprisingSystem.CalculateGarrisonRequirement(
                planet,
                empire,
                config.AI.Garrison
            );

            Assert.AreEqual(2, garrison, "Empire core world should halve garrison");
        }

        [Test]
        public void CalculateGarrisonRequirement_CoreWorldAlliance_NotHalved()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction alliance = new Faction
            {
                InstanceID = "alliance",
                Settings = new FactionSettings { GarrisonEfficiency = 1 },
            };
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                SectorType = PlanetSectorType.Core,
            };
            game.AttachNode(planetSector, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "alliance",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "alliance", 20 } },
            };
            game.AttachNode(planet, planetSector);

            // Base: ceil((60-20)/10) = 4. Alliance: no halving.
            int garrison = UprisingSystem.CalculateGarrisonRequirement(
                planet,
                alliance,
                config.AI.Garrison
            );

            Assert.AreEqual(4, garrison, "Alliance core world should NOT halve garrison");
        }

        [Test]
        public void CalculateGarrisonRequirement_CoreWorldEmpire_CanBeZero()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction
            {
                InstanceID = "empire",
                Settings = new FactionSettings { GarrisonEfficiency = 2 },
            };
            game.GetFactions().Add(empire);

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                SectorType = PlanetSectorType.Core,
            };
            game.AttachNode(planetSector, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "empire", 55 } },
            };
            game.AttachNode(planet, planetSector);

            // Base: ceil((60-55)/10) = 1. Halved: 1/2 = 0 (integer division).
            int garrison = UprisingSystem.CalculateGarrisonRequirement(
                planet,
                empire,
                config.AI.Garrison
            );

            Assert.AreEqual(
                0,
                garrison,
                "Empire core world garrison can be 0 via integer division (no min-1 floor)"
            );
        }
    }
}
