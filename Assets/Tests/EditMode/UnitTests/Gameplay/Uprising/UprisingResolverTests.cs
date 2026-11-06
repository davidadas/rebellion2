using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class UprisingResolverTests
    {
        [Test]
        public void Constructor_OwnedPlanet_DoesNotConsumeRandomValues()
        {
            Assert.DoesNotThrow(() => BuildScene(rng: new ThrowingRNG()));
        }

        [Test]
        public void ProcessTick_DestroyedPlanetWithSavedUprising_EndsUprisingWithoutResolvingIncidents()
        {
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
                ownerSupport: 0,
                troopCount: 0,
                rng: new ThrowingRNG()
            );
            planet.BeginUprising();
            planet.NextUprisingIncidentTick = 1;
            planet.IsDestroyed = true;
            game.CurrentTick = 10;

            IReadOnlyList<GameResult> results = new UprisingTickProcessor(system).ProcessTick(game);

            Assert.IsEmpty(results);
            Assert.IsFalse(planet.IsInUprising);
            Assert.Zero(planet.NextUprisingIncidentTick);
        }

        [Test]
        public void ProcessTick_UnscheduledActiveUprising_PreservesTimerInitializationOrder()
        {
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
                ownerSupport: 50,
                troopCount: 2,
                rng: new QueueRNG(0.2, 0.4, 0.6)
            );
            game.CurrentTick = 10;
            game.Config.Uprising.ClearUprisingMinTicks = 1;
            game.Config.Uprising.ClearUprisingMaxTicks = 10;
            game.Config.Uprising.ActiveSupportDriftMinTicks = 1;
            game.Config.Uprising.ActiveSupportDriftMaxTicks = 10;
            game.Config.Uprising.IncidentPulseMinTicks = 1;
            game.Config.Uprising.IncidentPulseMaxTicks = 10;
            planet.BeginUprising();

            new UprisingTickProcessor(system).ProcessTick(game);

            CollectionAssert.AreEqual(
                new[] { 13, 15, 17 },
                new[]
                {
                    planet.NextUprisingClearTick,
                    planet.NextUprisingSupportDriftTick,
                    planet.NextUprisingIncidentTick,
                }
            );
            CollectionAssert.AreEqual(
                new[] { 1, 2, 3 },
                new[]
                {
                    planet.UprisingClearTimerOrder,
                    planet.UprisingSupportDriftTimerOrder,
                    planet.UprisingIncidentTimerOrder,
                }
            );
        }

        [Test]
        public void ProcessTick_ClearTimerWinsTie_EndsUprisingWithoutFurtherRolls()
        {
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
                ownerSupport: 50,
                troopCount: 2,
                rng: new ThrowingRNG()
            );
            game.CurrentTick = 10;
            planet.BeginUprising();
            planet.NextUprisingClearTick = 10;
            planet.UprisingClearTimerOrder = 1;
            planet.NextUprisingSupportDriftTick = 10;
            planet.UprisingSupportDriftTimerOrder = 2;
            planet.NextUprisingIncidentTick = 10;
            planet.UprisingIncidentTimerOrder = 3;
            planet.NextUprisingTimerOrder = 3;

            IReadOnlyList<GameResult> results = new UprisingTickProcessor(system).ProcessTick(game);

            Assert.IsFalse(planet.IsInUprising);
            Assert.IsInstanceOf<PlanetUprisingEndedResult>(results.Single());
        }

        [Test]
        public void ProcessTick_OverdueSupportTimer_ProcessesEveryDuePulse()
        {
            CountingRNG rng = new CountingRNG();
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
                ownerSupport: 50,
                troopCount: 2,
                rng: rng
            );
            game.CurrentTick = 10;
            game.Config.Uprising.ControllerSupportShift = 0;
            game.Config.Uprising.ActiveSupportDriftMinTicks = 3;
            game.Config.Uprising.ActiveSupportDriftMaxTicks = 3;
            planet.BeginUprising();
            planet.NextUprisingSupportDriftTick = 2;
            planet.UprisingSupportDriftTimerOrder = 1;
            planet.NextUprisingClearTick = 100;
            planet.UprisingClearTimerOrder = 2;
            planet.NextUprisingIncidentTick = 100;
            planet.UprisingIncidentTimerOrder = 3;
            planet.NextUprisingTimerOrder = 3;

            new UprisingTickProcessor(system).ProcessTick(game);

            Assert.AreEqual(11, planet.NextUprisingSupportDriftTick);
            Assert.AreEqual(3, rng.IntCallCount);
        }

        [Test]
        public void ProcessTick_SufficientGarrison_NoUprising()
        {
            // Garrison requirement is 5 at support 10. Five troops meets it exactly.
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
                ownerSupport: 10,
                troopCount: 5
            );

            new UprisingTickProcessor(system).ProcessTick(game);

            Assert.IsFalse(planet.IsInUprising, "Sufficient garrison should prevent uprising");
        }

        [Test]
        public void ProcessTick_NoGarrison_UprisingStarts()
        {
            // Garrison requirement is 5 at support 10. Zero troops means a deficit, so uprising starts.
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
                ownerSupport: 10,
                troopCount: 0
            );

            IReadOnlyList<GameResult> results = new UprisingTickProcessor(system).ProcessTick(game);

            Assert.IsTrue(planet.IsInUprising, "Garrison deficit should trigger uprising");
            Assert.AreEqual(
                "empire",
                planet.OwnerInstanceID,
                "UprisingResolver must not change ownership"
            );
            Assert.IsTrue(results.OfType<PlanetUprisingStartedResult>().Any());
        }

        [Test]
        public void ProcessTick_ExactGarrison_NoUprising()
        {
            // Garrison requirement is 5 at support 10. Five troops exactly meets it, no uprising.
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
                ownerSupport: 10,
                troopCount: 5
            );

            new UprisingTickProcessor(system).ProcessTick(game);

            Assert.IsFalse(
                planet.IsInUprising,
                "Exactly sufficient garrison should not trigger uprising"
            );
        }

        [Test]
        public void ProcessTick_GarrisonFallsToRequirement_ReportsNearUprisingOnce()
        {
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
                ownerSupport: 10,
                troopCount: 6
            );
            game.DetachNode(planet.GetChildren<Regiment>()[0]);

            IReadOnlyList<GameResult> firstResults = new UprisingTickProcessor(system).ProcessTick(
                game
            );
            IReadOnlyList<GameResult> secondResults = new UprisingTickProcessor(system).ProcessTick(
                game
            );

            Assert.AreEqual(1, firstResults.OfType<PlanetNearUprisingResult>().Count());
            Assert.IsEmpty(secondResults.OfType<PlanetNearUprisingResult>());
            Assert.IsFalse(planet.IsInUprising);
        }

        [Test]
        public void ProcessTick_ActiveUprisingWithFacility_DestroysFacility()
        {
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
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

            new UprisingTickProcessor(system).ProcessTick(game);
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Building>("b1"));

            game.CurrentTick = 1;
            new UprisingTickProcessor(system).ProcessTick(game);

            Assert.IsNull(
                game.GetSceneNodeByInstanceID<Building>(facility.InstanceID),
                "Facility should be destroyed by uprising case 1"
            );
            Assert.IsTrue(planet.IsInUprising, "Uprising should remain active after consequence");
        }

        [Test]
        public void ProcessTick_ActiveUprisingLastBuildingDestroyed_DoesNotChangeControl()
        {
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
                ownerSupport: 10,
                troopCount: 1
            );
            planet.BeginUprising();
            planet.EnergyCapacity = 1;
            Building facility = EntityFactory.CreateBuilding("b1", "empire");
            facility.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(facility, planet);

            new UprisingTickProcessor(system).ProcessTick(game);
            game.CurrentTick = 1;
            IReadOnlyList<GameResult> results = new UprisingTickProcessor(system).ProcessTick(game);

            Assert.AreEqual("empire", planet.GetOwnerInstanceID());
            Assert.IsTrue(planet.IsInUprising);
            Assert.IsEmpty(results.OfType<PlanetOwnershipChangedResult>());
        }

        [TestCase(80, 19, 16)]
        [TestCase(80, 20, 0)]
        [TestCase(120, 4, 11)]
        [TestCase(120, 5, 0)]
        public void ProcessTick_OfficerIncident_UsesCombatBasedInjuryChance(
            int combat,
            int injuryRoll,
            int expectedInjury
        )
        {
            var (game, planet, system, officer) = BuildOfficerIncident(
                combat + 3,
                new SequenceRNG(intValues: new[] { 0, 0, 0, injuryRoll, 10, 4 })
            );
            officer.InjuryPoints = 3;

            IReadOnlyList<GameResult> results = new UprisingTickProcessor(system).ProcessTick(game);

            Assert.AreEqual(3 + expectedInjury, officer.InjuryPoints);
            Assert.IsFalse(officer.IsCaptured);
            Assert.IsNull(officer.CaptorInstanceID);
            Assert.IsEmpty(results.OfType<OfficerCaptureStateResult>());
            if (expectedInjury == 0)
                Assert.IsEmpty(results.OfType<OfficerInjuredResult>());
            else
            {
                OfficerInjuredResult injured = results.OfType<OfficerInjuredResult>().Single();
                Assert.AreSame(officer, injured.Officer);
                Assert.AreEqual(expectedInjury, injured.Severity);
                Assert.AreEqual(game.CurrentTick, injured.Tick);
            }
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void ProcessTick_OfficerIncident_OnlyMinorOfficersCanDie(
            bool isMain,
            bool expectedDeath
        )
        {
            var (game, planet, system, officer) = BuildOfficerIncident(80, new StubRNG());
            officer.IsMain = isMain;
            game.Config.Assassination.KillProbability = 100;

            IReadOnlyList<GameResult> results = new UprisingTickProcessor(system).ProcessTick(game);

            Assert.IsNotEmpty(results.OfType<OfficerInjuredResult>());
            Assert.AreEqual(expectedDeath, officer.IsKilled);
            Assert.AreEqual(!expectedDeath, officer.IsEnabled);
            Assert.IsFalse(officer.IsCaptured);
            if (expectedDeath)
            {
                OfficerKilledResult killed = results.OfType<OfficerKilledResult>().Single();
                Assert.AreSame(officer, killed.TargetOfficer);
                Assert.AreSame(planet, killed.ParentAtDeath);
                Assert.AreSame(planet, killed.Context);
                Assert.IsNull(killed.Assassin);
                Assert.AreEqual(game.CurrentTick, killed.Tick);
            }
            else
                Assert.IsEmpty(results.OfType<OfficerKilledResult>());
        }

        [Test]
        public void ProcessTick_ZeroSeverityOfficerIncident_DoesNotInjureOrKill()
        {
            var (game, _, system, officer) = BuildOfficerIncident(80, new StubRNG());
            officer.IsMain = false;
            game.Config.DuelResolution.InjuryBase = 0;
            game.Config.DuelResolution.InjurySecondaryRollMaximum = 0;
            game.Config.Assassination.KillProbability = 100;

            IReadOnlyList<GameResult> results = new UprisingTickProcessor(system).ProcessTick(game);

            Assert.Zero(officer.InjuryPoints);
            Assert.IsFalse(officer.IsKilled);
            Assert.IsEmpty(results.OfType<OfficerInjuredResult>());
            Assert.IsEmpty(results.OfType<OfficerKilledResult>());
        }

        [Test]
        public void ProcessTick_OfficerIncidentWithoutEligibleOfficers_DoesNotInjureAnyone()
        {
            var (game, planet, system, officer) = BuildOfficerIncident(80, new StubRNG());
            officer.IsCaptured = true;
            Officer travelling = EntityFactory.CreateOfficer("travelling", "empire");
            travelling.Movement = new MovementState();
            game.AttachNode(travelling, planet);
            Officer killed = EntityFactory.CreateOfficer("killed", "empire");
            killed.IsKilled = true;
            game.AttachNode(killed, planet);

            IReadOnlyList<GameResult> results = new UprisingTickProcessor(system).ProcessTick(game);

            Assert.IsEmpty(results.OfType<OfficerInjuredResult>());
            Assert.IsEmpty(results.OfType<OfficerKilledResult>());
        }

        [Test]
        public void ProcessTick_ActiveUprising_CapturedOfficerFreed()
        {
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
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
                CaptorInstanceID = "rebels",
                CanEscape = true,
                NextEscapeAttemptTick = 10,
            };
            game.AttachNode(captive, planet);

            IReadOnlyList<GameResult> results = new UprisingTickProcessor(system).ProcessTick(game);

            Assert.IsFalse(
                captive.IsCaptured,
                "Captured officer should be freed by uprising case 4"
            );
            Assert.IsNull(captive.CaptorInstanceID);
            Assert.IsFalse(captive.CanEscape);
            Assert.Zero(captive.NextEscapeAttemptTick);
            Assert.IsTrue(
                results.OfType<OfficerCaptureStateResult>().Any(r => !r.IsCaptured),
                "Should emit OfficerCaptureStateResult with IsCaptured=false"
            );
        }

        [Test]
        public void ProcessTick_Incident_ExcludesIncompleteFacility()
        {
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
                ownerSupport: 10,
                troopCount: 1
            );
            ScheduleIncident(planet, 1);
            game.CurrentTick = 1;
            planet.EnergyCapacity = 1;
            Building facility = EntityFactory.CreateBuilding("b1", "empire");
            game.AttachNode(facility, planet);

            new UprisingTickProcessor(system).ProcessTick(game);

            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Building>("b1"));
        }

        [Test]
        public void ProcessTick_Incident_ExcludesEnrouteRegiment()
        {
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
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

            new UprisingTickProcessor(system).ProcessTick(game);

            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("enroute"));
            Assert.AreEqual(
                1,
                planet.GetChildren<Regiment>().Count(regiment => regiment.Movement == null)
            );
        }

        [Test]
        public void ProcessTick_IncidentDestroysLastGarrison_ReportsGarrisonRemoval()
        {
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
                ownerSupport: 10,
                opposingSupport: 50,
                troopCount: 1,
                rng: new SequenceRNG(intValues: new[] { 3, 3, 0 })
            );
            ScheduleIncident(planet, 1);
            game.CurrentTick = 1;

            IReadOnlyList<GameResult> results = new UprisingTickProcessor(system).ProcessTick(game);

            PlanetOwnershipChangedResult ownershipChange = results
                .OfType<PlanetOwnershipChangedResult>()
                .Single(change => change.Planet == planet);
            Assert.AreEqual(PlanetOwnershipChangeReason.GarrisonRemoved, ownershipChange.Reason);
            Assert.IsNull(planet.GetOwnerInstanceID());
        }

        [Test]
        public void ProcessTick_Incident_InjuresOnlyUsableOfficer()
        {
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
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

            new UprisingTickProcessor(system).ProcessTick(game);

            Assert.Zero(enroute.InjuryPoints);
            Assert.Zero(killed.InjuryPoints);
            Assert.Greater(usable.InjuryPoints, 0);
            Assert.IsFalse(usable.IsCaptured);
        }

        [Test]
        public void ProcessTick_HighSupport_NoUprising()
        {
            // At support 80, garrison requirement is 0. No uprising regardless of troops.
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
                ownerSupport: 80,
                opposingSupport: 20,
                troopCount: 0,
                rng: new SequenceRNG(intValues: new[] { 8, 8 })
            );

            new UprisingTickProcessor(system).ProcessTick(game);

            Assert.IsFalse(planet.IsInUprising, "High support should prevent uprising");
        }

        [Test]
        public void ProcessTick_ActiveUprising_ZeroTroops_PlanetGoesNeutral()
        {
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
                ownerSupport: 10,
                troopCount: 0
            );
            planet.BeginUprising();

            IReadOnlyList<GameResult> results = new UprisingTickProcessor(system).ProcessTick(game);

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
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
                ownerSupport: 0,
                opposingSupport: 100,
                troopCount: 0
            );
            planet.BeginUprising();

            IReadOnlyList<GameResult> results = new UprisingTickProcessor(system).ProcessTick(game);

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
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
                ownerSupport: 50,
                opposingSupport: 50,
                troopCount: 2
            );
            planet.BeginUprising();

            IReadOnlyList<GameResult> beforeTimer = new UprisingTickProcessor(system).ProcessTick(
                game
            );
            game.CurrentTick = 1;
            IReadOnlyList<GameResult> atTimer = new UprisingTickProcessor(system).ProcessTick(game);

            Assert.IsEmpty(beforeTimer.OfType<PlanetUprisingEndedResult>());
            Assert.IsFalse(planet.IsInUprising);
            Assert.AreEqual(1, atTimer.OfType<PlanetUprisingEndedResult>().Count());
        }

        [Test]
        public void ProcessTick_Incident_IgnoresHostileFleetPresence()
        {
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
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

            new UprisingTickProcessor(system).ProcessTick(game);

            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Building>("b1"));
        }

        [Test]
        public void ProcessTick_Incident_AppliesInciteAndSubdueLeadershipAdjustments()
        {
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
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

            new UprisingTickProcessor(system).ProcessTick(game);

            Assert.IsNull(game.GetSceneNodeByInstanceID<Building>(facility.InstanceID));
            Assert.AreEqual(8, planet.GetPopularSupport("empire"));
            Assert.AreEqual(92, planet.GetPopularSupport("rebels"));
        }

        [TestCase(InciteUprisingMission.MissionTypeID, 80, 8, true)]
        [TestCase(InciteUprisingMission.MissionTypeID, 0, 6, false)]
        [TestCase(SubdueUprisingMission.MissionTypeID, 80, 0, false)]
        [TestCase(SubdueUprisingMission.MissionTypeID, 0, 3, true)]
        public void ProcessTick_IncidentWithDecoy_AveragesBothMissionRoles(
            string missionTypeId,
            int decoyLeadership,
            int destructionThreshold,
            bool expectedDestruction
        )
        {
            var (game, planet, system) = BuildScene(troopCount: 1);
            game.Config.Uprising.DiceRange = 0;
            game.Config.Uprising.DiceAddend = 0;
            game.Config.Uprising.PrimaryConsequenceTable = new Dictionary<int, int>
            {
                { destructionThreshold, 1 },
            };
            game.Config.Uprising.SecondaryConsequenceTable.Clear();
            string owner =
                missionTypeId == InciteUprisingMission.MissionTypeID ? "rebels" : "empire";
            if (missionTypeId == SubdueUprisingMission.MissionTypeID)
                ScheduleIncident(planet, 1);
            AttachActiveMission(game, planet, missionTypeId, owner, 20, decoyLeadership);
            ScheduleIncident(planet, 1);
            game.CurrentTick = 1;
            planet.EnergyCapacity = 1;
            Building facility = EntityFactory.CreateBuilding("facility", "empire");
            facility.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(facility, planet);

            new UprisingTickProcessor(system).ProcessTick(game);

            Assert.AreEqual(
                expectedDestruction,
                game.GetSceneNodeByInstanceID<Building>(facility.InstanceID) == null
            );
        }

        [Test]
        public void ProcessTick_Incident_ExcludesMissionParticipantsInTransit()
        {
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
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

            new UprisingTickProcessor(system).ProcessTick(game);

            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Building>("b1"));
            Assert.AreEqual(10, planet.GetPopularSupport("empire"));
        }

        [Test]
        public void ProcessTick_NeutralPlanet_Skipped()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(planetSector, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = null,
                PopularSupport = new Dictionary<string, int>(),
            };
            game.AttachNode(planet, planetSector);

            MovementCommands movementSystem = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            PlanetaryControlCommands planetaryControl = new PlanetaryControlCommands(
                game,
                new PlanetaryControlQueries(game),
                new FogOfWarQueries(game)
            );
            UprisingResolver uprisingSystem = new UprisingResolver(
                game,
                new StubRNG(),
                planetaryControl,
                new JediCommands(game, new StubRNG())
            );
            new UprisingTickProcessor(uprisingSystem).ProcessTick(game);

            Assert.IsFalse(planet.IsInUprising, "Neutral planet should not revolt");
        }

        /// <summary>
        /// Verifies that a sector suppression source reduces the garrison needed to prevent uprising.
        /// </summary>
        /// <param name="region">The sector's galactic region.</param>
        [TestCase(PlanetSectorType.Core)]
        [TestCase(PlanetSectorType.OuterRim)]
        public void ProcessTick_EmpireGarrisonWithSuppressionSource_HalvesRequirement(
            PlanetSectorType region
        )
        {
            // Sector suppression divides the base requirement of three by two before truncation.
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
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
                SectorType = region,
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

            Fleet suppressionFleet = new Fleet
            {
                InstanceID = "suppression-fleet",
                OwnerInstanceID = "empire",
            };
            game.AttachNode(suppressionFleet, planet);
            game.AttachNode(
                new CapitalShip
                {
                    InstanceID = "suppression-source",
                    OwnerInstanceID = "empire",
                    SectorSuppression = 2,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                suppressionFleet
            );

            MovementCommands movementSystem = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            PlanetaryControlCommands planetaryControl = new PlanetaryControlCommands(
                game,
                new PlanetaryControlQueries(game),
                new FogOfWarQueries(game)
            );
            UprisingResolver uprisingSystem = new UprisingResolver(
                game,
                new StubRNG(),
                planetaryControl,
                new JediCommands(game, new StubRNG())
            );
            new UprisingTickProcessor(uprisingSystem).ProcessTick(game);

            Assert.IsFalse(
                planet.IsInUprising,
                "Sector suppression should halve the garrison requirement"
            );
        }

        [Test]
        public void ProcessTick_EmpireGarrisonOnOuterRim_NoBonus()
        {
            // Without a sector suppression source, the requirement stays at three with one troop.
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
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

            MovementCommands movementSystem = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            PlanetaryControlCommands planetaryControl = new PlanetaryControlCommands(
                game,
                new PlanetaryControlQueries(game),
                new FogOfWarQueries(game)
            );
            UprisingResolver uprisingSystem = new UprisingResolver(
                game,
                new StubRNG(),
                planetaryControl,
                new JediCommands(game, new StubRNG())
            );
            new UprisingTickProcessor(uprisingSystem).ProcessTick(game);

            Assert.IsTrue(
                planet.IsInUprising,
                "Without sector suppression, the garrison deficit should trigger an uprising"
            );
        }

        [Test]
        public void ReconcileGarrison_CapturedPlanetAtRequirement_ReportsNearUprising()
        {
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
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
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(
                ownerSupport: 50,
                opposingSupport: 50,
                troopCount: 2
            );
            game.Config.Uprising.ActiveSupportDriftMinTicks = 100;
            game.Config.Uprising.ActiveSupportDriftMaxTicks = 100;
            game.Config.Uprising.IncidentPulseMinTicks = 100;
            game.Config.Uprising.IncidentPulseMaxTicks = 100;
            planet.BeginUprising();
            new UprisingTickProcessor(system).ProcessTick(game);

            game.DetachNode(planet.GetChildren<Regiment>()[0]);
            system.ReconcileGarrison(planet);
            game.CurrentTick = 1;
            IReadOnlyList<GameResult> results = new UprisingTickProcessor(system).ProcessTick(game);

            Assert.IsTrue(planet.IsInUprising);
            Assert.AreEqual(0, planet.NextUprisingClearTick);
            Assert.IsEmpty(results.OfType<PlanetUprisingEndedResult>());
        }

        [Test]
        public void TryExecuteMission_OfficersOutOfScoreOrder_AttemptsLowestScoreFirst()
        {
            CountingRNG rng = new CountingRNG();
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(rng: rng);
            Officer highProbabilityOfficer = EntityFactory.CreateOfficer("high", "rebels");
            highProbabilityOfficer.SetBaseRating(SkillRating.Leadership, 100);
            Officer lowProbabilityOfficer = EntityFactory.CreateOfficer("low", "rebels");
            lowProbabilityOfficer.SetBaseRating(SkillRating.Leadership, 0);
            game.Config.ProbabilityTables.Mission.InciteUprising = new Dictionary<int, int>
            {
                { -10, 0 },
                { 90, 100 },
            };
            Mission mission = MissionTestFactory.TryCreate(
                InciteUprisingMission.MissionTypeID,
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

        [Test]
        public void TryExecuteMission_FirstOfficerSucceeds_DoesNotAttemptRemainingOfficers()
        {
            CountingRNG rng = new CountingRNG();
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(rng: rng);
            Officer firstOfficer = EntityFactory.CreateOfficer("first", "rebels");
            firstOfficer.SetBaseRating(SkillRating.Leadership, 100);
            Officer secondOfficer = EntityFactory.CreateOfficer("second", "rebels");
            secondOfficer.SetBaseRating(SkillRating.Leadership, 100);
            game.Config.ProbabilityTables.Mission.InciteUprising = new Dictionary<int, int>
            {
                { 90, 100 },
            };
            Mission mission = MissionTestFactory.TryCreate(
                InciteUprisingMission.MissionTypeID,
                game,
                "rebels",
                planet,
                new List<IMissionParticipant> { firstOfficer, secondOfficer }
            );
            game.AttachNode(mission, planet);

            bool handled = system.TryExecuteMission(mission, out _);

            Assert.IsTrue(handled);
            Assert.AreEqual(1, rng.DoubleCallCount);
        }

        [Test]
        public void TryExecuteMission_FirstSpecialForcesSucceeds_DoesNotAttemptRemainingSpecialForces()
        {
            CountingRNG rng = new CountingRNG();
            (GameRoot game, Planet planet, UprisingResolver system) = BuildScene(rng: rng);
            SpecialForces firstSpecialForces = new SpecialForces
            {
                InstanceID = "first",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            firstSpecialForces.SetBaseRating(SkillRating.Leadership, 100);
            SpecialForces secondSpecialForces = new SpecialForces
            {
                InstanceID = "second",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            secondSpecialForces.SetBaseRating(SkillRating.Leadership, 100);
            game.Config.ProbabilityTables.Mission.InciteUprising = new Dictionary<int, int>
            {
                { 90, 100 },
            };
            Mission mission = MissionTestFactory.TryCreate(
                InciteUprisingMission.MissionTypeID,
                game,
                "rebels",
                planet,
                new List<IMissionParticipant> { firstSpecialForces, secondSpecialForces }
            );
            game.AttachNode(mission, planet);

            bool handled = system.TryExecuteMission(mission, out _);

            Assert.IsTrue(handled);
            Assert.AreEqual(1, rng.DoubleCallCount);
        }

        /// <summary>
        /// Builds a scheduled officer incident with controlled injury rules.
        /// </summary>
        /// <param name="combat">The target officer's combat rating.</param>
        /// <param name="rng">The incident and injury rolls.</param>
        /// <returns>The game, planet, resolver, and eligible officer.</returns>
        private (
            GameRoot game,
            Planet planet,
            UprisingResolver system,
            Officer officer
        ) BuildOfficerIncident(int combat, IRandomNumberProvider rng)
        {
            var (game, planet, system) = BuildScene(troopCount: 1, rng: rng);
            game.Config.Uprising.PrimaryConsequenceTable = new Dictionary<int, int>
            {
                { -1000, 3 },
            };
            game.Config.Uprising.SecondaryConsequenceTable.Clear();
            game.Config.DuelResolution.MinimumInjuryChance = 5;
            game.Config.DuelResolution.CaptureEvasionInjuryBaseChance = 100;
            game.Config.DuelResolution.InjuryBase = 2;
            game.Config.DuelResolution.InjurySecondaryRollMaximum = 10;
            game.Config.Recovery.MaxInjuryPoints = 100;
            ScheduleIncident(planet, 1);
            game.CurrentTick = 1;
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            officer.IsMain = true;
            officer.SetBaseRating(SkillRating.Combat, combat);
            game.AttachNode(officer, planet);
            return (game, planet, system, officer);
        }

        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <param name="ownerSupport">The owner support.</param>
        /// <param name="opposingSupport">The opposing support.</param>
        /// <param name="troopCount">The troop count.</param>
        /// <param name="isCoreSector">Whether is core sector.</param>
        /// <param name="rng">The rng.</param>
        /// <returns>The constructed scene.</returns>
        private (GameRoot game, Planet planet, UprisingResolver system) BuildScene(
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
            GameRoot game = TestGame.Create(config);
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

            MovementCommands movementSystem = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            PlanetaryControlCommands planetaryControl = new PlanetaryControlCommands(
                game,
                new PlanetaryControlQueries(game),
                new FogOfWarQueries(game)
            );
            UprisingResolver uprisingSystem = new UprisingResolver(
                game,
                rng ?? new StubRNG(),
                planetaryControl,
                new JediCommands(game, rng ?? new StubRNG())
            );
            return (game, planet, uprisingSystem);
        }

        /// <summary>
        /// Executes schedule incident.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <param name="tick">The tick.</param>
        private static void ScheduleIncident(Planet planet, int tick)
        {
            planet.BeginUprising();
            planet.NextUprisingSupportDriftTick = tick + 1;
            planet.UprisingSupportDriftTimerOrder = 1;
            planet.NextUprisingIncidentTick = tick;
            planet.UprisingIncidentTimerOrder = 2;
            planet.NextUprisingTimerOrder = 2;
        }

        /// <summary>
        /// Attaches active mission.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="missionTypeId">The mission type id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="leadership">The leadership.</param>
        /// <param name="decoyLeadership">Optional leadership for a stationary decoy.</param>
        /// <returns>The result of attach active mission.</returns>
        private static Mission AttachActiveMission(
            GameRoot game,
            Planet planet,
            string missionTypeId,
            string ownerInstanceId,
            int leadership,
            int? decoyLeadership = null
        )
        {
            Officer officer = EntityFactory.CreateOfficer(
                $"{missionTypeId}-officer",
                ownerInstanceId
            );
            officer.SetBaseRating(SkillRating.Leadership, leadership);
            Officer decoy = null;
            if (decoyLeadership.HasValue)
            {
                decoy = EntityFactory.CreateOfficer($"{missionTypeId}-decoy", ownerInstanceId);
                decoy.SetBaseRating(SkillRating.Leadership, decoyLeadership.Value);
            }
            Mission mission = MissionTestFactory.TryCreate(
                missionTypeId,
                game,
                ownerInstanceId,
                planet,
                new List<IMissionParticipant> { officer },
                decoy == null ? null : new List<IMissionParticipant> { decoy }
            );
            Assert.IsNotNull(mission);
            mission.InstanceID = $"{missionTypeId}-mission";
            game.AttachNode(mission, planet);
            mission.Initiate(100);
            game.AttachNode(officer, mission);
            if (decoy != null)
                game.AttachNode(decoy, mission);
            return mission;
        }

        private sealed class CountingRNG : IRandomNumberProvider
        {
            public int DoubleCallCount { get; private set; }
            public int IntCallCount { get; private set; }

            /// <summary>
            /// Executes next double.
            /// </summary>
            /// <returns>The result of next double.</returns>
            public double NextDouble()
            {
                DoubleCallCount++;
                return 0.5;
            }

            /// <summary>
            /// Executes next int.
            /// </summary>
            /// <param name="min">The min.</param>
            /// <param name="max">The max.</param>
            /// <returns>The result of next int.</returns>
            public int NextInt(int min, int max)
            {
                IntCallCount++;
                return min;
            }
        }
    }
}
