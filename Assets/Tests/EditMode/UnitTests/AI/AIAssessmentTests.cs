using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Scripting;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Tests.AI.Helpers;
using ArgumentOutOfRangeException = global::System.ArgumentOutOfRangeException;

namespace Rebellion.Tests.AI
{
    [TestFixture]
    public class AIAssessmentTests
    {
        [TestCase(nameof(AIAssessment.GetReadyFleetCombatValueAgainstCapitalShips))]
        [TestCase(nameof(AIAssessment.GetProjectedFleetCombatValueAgainstCapitalShips))]
        [TestCase(nameof(AIAssessment.GetReadyFleetRegimentCount))]
        [TestCase(nameof(AIAssessment.GetReadyFleetRegimentCapacity))]
        [TestCase(nameof(AIAssessment.GetReadyFleetRegimentAttackStrength))]
        [TestCase(nameof(AIAssessment.GetProjectedFleetRegimentAttackStrength))]
        public void BeginExecution_WithFleetReinforcements_ReturnsCurrentCapabilities(string query)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);
            Fleet fleet = new() { InstanceID = "fleet", OwnerInstanceID = empire.InstanceID };
            Fleet donor = new() { InstanceID = "donor", OwnerInstanceID = empire.InstanceID };
            CapitalShip first = AITestSceneBuilder.CreateCapitalShip("first", empire.InstanceID);
            CapitalShip second = AITestSceneBuilder.CreateCapitalShip("second", empire.InstanceID);
            game.AttachNode(fleet, planet);
            game.AttachNode(donor, planet);
            game.AttachNode(first, fleet);
            game.AttachNode(second, donor);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("first-army", empire.InstanceID),
                first
            );
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("second-army", empire.InstanceID),
                second
            );
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;
            assessment.BeginPlanning();
            int before = MeasureFleetCapability(assessment, fleet, query);
            Assert.Greater(before, 0);

            assessment.BeginExecution();
            game.MoveNode(second, fleet);

            Assert.AreEqual(before * 2, MeasureFleetCapability(assessment, fleet, query));
        }

        [TestCase(PlanetIntelligenceCategory.GroundForces, PlanetIntelligenceCategory.Regiments)]
        [TestCase(
            PlanetIntelligenceCategory.Regiments | PlanetIntelligenceCategory.SpecialForces,
            PlanetIntelligenceCategory.GroundForces
        )]
        public void HasPlanetIntelligence_OverlappingCategories_RecognizesCoverage(
            PlanetIntelligenceCategory known,
            PlanetIntelligenceCategory requested
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            AITestSceneBuilder.AddPlanet(game, sector, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, sector, "enemy", rebels.InstanceID);
            new FogOfWarRecorder().RecordIntelligenceSnapshot(empire, enemy, sector, 10, known);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.IsTrue(assessment.HasPlanetIntelligence(enemy, requested));
        }

        [Test]
        public void GetPlanetIntelAge_NarrowReportAfterFullObservation_UsesFullObservationAge()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            AITestSceneBuilder.AddPlanet(game, sector, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, sector, "enemy", rebels.InstanceID);
            game.CurrentTick = 10;
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            game.CurrentTick = 100;
            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                empire,
                enemy,
                sector,
                100,
                PlanetIntelligenceCategory.PlanetControl
            );

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(90, assessment.GetPlanetIntelAge(enemy));
        }

        [Test]
        public void GetPlanetIntelAge_NewPartialObservation_ReturnsUnknownAge()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            AITestSceneBuilder.AddPlanet(game, sector, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, sector, "enemy", rebels.InstanceID);
            game.CurrentTick = 100;
            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                empire,
                enemy,
                sector,
                100,
                PlanetIntelligenceCategory.PlanetControl
            );

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(int.MaxValue, assessment.GetPlanetIntelAge(enemy));
        }

        [Test]
        public void GetPlanetIntelAge_OrdinaryObservation_ReturnsElapsedAge()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            AITestSceneBuilder.AddPlanet(game, sector, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, sector, "enemy", rebels.InstanceID);
            new FogOfWarRecorder().RecordPlanetSnapshot(empire, enemy, sector, 100);
            game.CurrentTick = 101;

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(1, assessment.GetPlanetIntelAge(enemy));
        }

        [Test]
        public void GetPlanetIntelAge_OrdinaryObservationAfterEspionage_UsesLatestObservation()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            AITestSceneBuilder.AddPlanet(game, sector, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, sector, "enemy", rebels.InstanceID);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(empire, enemy, sector, 10);
            recorder.RecordPlanetSnapshot(empire, enemy, sector, 100);
            game.CurrentTick = 101;

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(1, assessment.GetPlanetIntelAge(enemy));
        }

        [Test]
        public void GetPlanetIntelAge_PlanetLossObservation_UsesLossTick()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            AITestSceneBuilder.AddPlanet(game, sector, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, sector, "enemy", empire.InstanceID);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(empire, enemy, sector, 10);
            enemy.OwnerInstanceID = rebels.InstanceID;
            recorder.RecordPlanetLossSnapshot(empire, enemy, sector, 100);
            game.CurrentTick = 101;

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(1, assessment.GetPlanetIntelAge(enemy));
        }

        [TestCase(ManufacturingStatus.Building, true)]
        [TestCase(ManufacturingStatus.Complete, false)]
        public void GetReplaceableManufacturingOrder_SingleOrder_UsesManufacturingStatus(
            ManufacturingStatus status,
            bool expected
        )
        {
            AITurnContext context = CreateProductionQueueContext(
                status,
                out Building order,
                out Planet producer,
                out Planet destination
            );

            Assert.AreSame(
                expected ? order : null,
                context.Assessment.GetReplaceableManufacturingOrder(
                    producer,
                    ManufacturingType.Building
                )
            );
        }

        [Test]
        public void GetReplaceableManufacturingOrder_MixedQueue_PreservesAllWork()
        {
            AITurnContext context = CreateProductionQueueContext(
                ManufacturingStatus.Building,
                out Building order,
                out Planet producer,
                out Planet destination
            );
            destination.EnergyCapacity = 2;
            Building second = AITestSceneBuilder.CreateBuildingTemplate(
                "required",
                BuildingType.Defense
            );
            second.OwnerInstanceID = context.Faction.InstanceID;
            second.ProducerPlanetID = producer.InstanceID;
            second.ManufacturingStatus = ManufacturingStatus.Building;
            context.Game.AttachNode(second, destination);
            producer.AddToManufacturingQueue(second);

            Assert.IsNull(
                context.Assessment.GetReplaceableManufacturingOrder(
                    producer,
                    ManufacturingType.Building
                )
            );
        }

        [TestCase(ManufacturingStatus.Building, 1)]
        [TestCase(ManufacturingStatus.Complete, 0)]
        public void GetAvailableBuildingSpace_FullDestination_CountsOnlyReplaceableConstruction(
            ManufacturingStatus status,
            int expected
        )
        {
            AITurnContext context = CreateProductionQueueContext(
                status,
                out Building order,
                out Planet producer,
                out Planet destination
            );

            Assert.AreEqual(0, destination.GetAvailableEnergy());
            Assert.AreEqual(expected, context.Assessment.GetAvailableBuildingSpace(destination));
        }

        [TestCase(0, true, false)]
        [TestCase(1, true, false)]
        [TestCase(5, true, true)]
        [TestCase(5, false, false)]
        public void CanBombardDefenders_AdditionalBombardment_ReportsCapabilityWithoutChangingFleet(
            int contribution,
            bool hasDefenders,
            bool expected
        )
        {
            AITurnContext context = AITestSceneBuilder.CreateBombardmentDonorContext(
                false,
                0,
                out Fleet receiver,
                out _
            );
            Planet target = context.Game.GetSceneNodeByInstanceID<Planet>(
                receiver.Order.TargetPlanetId
            );
            if (!hasDefenders)
            {
                foreach (Regiment regiment in target.GetAllRegiments().ToList())
                    context.Game.DeleteNode(regiment);
                AITestSceneBuilder.RevealPlanet(context.Game, context.Faction, target);
                context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            }

            bool actual = context.Assessment.CanBombardDefenders(
                receiver,
                context.Assessment.GetKnownPlanet(target.InstanceID),
                true,
                contribution
            );

            Assert.AreEqual(expected, actual);
            Assert.AreEqual(1, context.Assessment.GetProjectedFleetBombardmentStrength(receiver));
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void CanBombardDefenders_UnfinishedShip_UsesOnlyRequestedReadiness(
            bool projected,
            bool expected
        )
        {
            AITurnContext context = AITestSceneBuilder.CreateBombardmentDonorContext(
                false,
                5,
                out Fleet receiver,
                out CapitalShip ship
            );
            context.Game.MoveNode(ship, receiver);
            ship.ManufacturingStatus = ManufacturingStatus.Building;
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);

            bool actual = context.Assessment.CanBombardDefenders(
                receiver,
                context.Assessment.GetKnownPlanet(receiver.Order.TargetPlanetId),
                projected
            );

            Assert.AreEqual(expected, actual);
        }

        [TestCase(1, 90, true, "active", false, 90)]
        [TestCase(3, 90, true, "active", false, 270)]
        [TestCase(3, 30, true, "active", false, 90)]
        [TestCase(3, 0, true, "active", false, 0)]
        [TestCase(3, 90, false, "active", false, 0)]
        [TestCase(3, 90, true, "active", true, 0)]
        [TestCase(3, 90, true, "captured", false, 0)]
        [TestCase(3, 90, true, "killed", false, 0)]
        [TestCase(3, 90, true, "retired", false, 0)]
        [TestCase(3, 90, true, "disabled", false, 0)]
        public void GetEnemyDiplomacyStrengthInSystem_ObservedOfficers_SumsOnlyActiveOpponents(
            int count,
            int rating,
            bool revealed,
            string state,
            bool friendly,
            double expected
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction enemy);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "competition");
            sector.SectorType = PlanetSectorType.OuterRim;
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "enemy-base",
                friendly ? faction.InstanceID : enemy.InstanceID
            );
            for (int index = 0; index < count; index++)
            {
                Officer officer = EntityFactory.CreateOfficer(
                    "diplomat-" + index,
                    friendly ? faction.InstanceID : enemy.InstanceID
                );
                officer.SetBaseRating(SkillRating.Diplomacy, rating);
                officer.IsCaptured = state == "captured";
                officer.IsKilled = state == "killed";
                officer.IsRetired = state == "retired";
                officer.IsEnabled = state != "disabled";
                game.AttachNode(officer, planet);
            }
            if (revealed)
                AITestSceneBuilder.RevealPlanet(game, faction, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);

            double strength = context.Assessment.GetEnemyDiplomacyStrengthInSystem(
                sector.InstanceID
            );

            Assert.AreEqual(expected, strength);
            Assert.AreEqual(0, context.Assessment.GetEnemyDiplomacyStrengthInSystem("unknown"));
        }

        [TestCase(0, 100)]
        [TestCase(50, 100)]
        [TestCase(100, 100)]
        [TestCase(0, 67)]
        [TestCase(50, 67)]
        [TestCase(100, 67)]
        public void CanMeetOperationalMissionSuccess_FeasibleTeam_DoesNotReject(
            int age,
            int successPercent
        )
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(age);
            context.Game.Config.ProbabilityTables.Mission.DefaultSuccessProbability = 100;
            context.Game.Config.ProbabilityTables.Mission.Espionage = new Dictionary<int, int>
            {
                { -1000, successPercent },
            };
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant>
                {
                    context.Game.GetSceneNodeByInstanceID<Officer>("spy"),
                },
            };
            MissionOdds exact = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );

            bool possible = context.Assessment.CanMeetOperationalMissionSuccess(
                context.MissionQueries,
                request,
                exact.OverallSuccessProbability
            );

            Assert.IsTrue(possible);
        }

        [TestCase(0, 35d, 100)]
        [TestCase(50, 35d, 100)]
        [TestCase(100, 35d, 100)]
        [TestCase(0, 100d, 100)]
        [TestCase(50, 100d, 100)]
        [TestCase(100, 100d, 100)]
        [TestCase(0, null, 100)]
        [TestCase(50, null, 100)]
        [TestCase(100, null, 100)]
        [TestCase(0, null, 67)]
        [TestCase(50, null, 67)]
        [TestCase(100, null, 67)]
        public void TryGetOperationalMissionOdds_SuccessFloor_MatchesUnboundedDecision(
            int age,
            double? configuredFloor,
            int successPercent
        )
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(age);
            context.Game.Config.ProbabilityTables.Mission.DefaultSuccessProbability = 100;
            context.Game.Config.ProbabilityTables.Mission.Espionage = new Dictionary<int, int>
            {
                { -1000, successPercent },
            };
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant>
                {
                    context.Game.GetSceneNodeByInstanceID<Officer>("spy"),
                },
            };
            AIAssessment oracle = new(context.Game, context.Faction, context.FactionView);
            MissionOdds complete = oracle.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );
            double floor = configuredFloor ?? complete.OverallSuccessProbability;

            bool accepted = context.Assessment.TryGetOperationalMissionOdds(
                context.MissionQueries,
                request,
                floor,
                out MissionOdds bounded
            );
            MissionOdds unbounded = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );

            Assert.AreEqual(complete.OverallSuccessProbability >= floor, accepted);
            if (accepted)
                Assert.AreEqual(complete.FoilProbability, bounded.FoilProbability, 0.000000001);
            else
                Assert.IsNull(bounded);
            Assert.AreEqual(complete.FoilProbability, unbounded.FoilProbability, 0.000000001);
        }

        [Test]
        public void GetMissionOdds_EquivalentRequests_ReusesAssessmentResult()
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(100);
            Officer spy = context.Game.GetSceneNodeByInstanceID<Officer>("spy");
            Planet target = context.Assessment.GetKnownPlanet("target");

            MissionOdds first = context.Assessment.GetMissionOdds(
                context.MissionQueries,
                new MissionContext
                {
                    MissionTypeID = MissionTypeIDs.Espionage,
                    Location = target,
                    MainParticipants = new List<IMissionParticipant> { spy },
                }
            );
            MissionOdds second = context.Assessment.GetMissionOdds(
                context.MissionQueries,
                new MissionContext
                {
                    MissionTypeID = MissionTypeIDs.Espionage,
                    Location = target,
                    MainParticipants = new List<IMissionParticipant> { spy },
                }
            );

            Assert.IsNotNull(first);
            Assert.AreSame(first, second);
        }

        [Test]
        public void GetMissionOdds_NewAssessment_RecalculatesResult()
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(100);
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant>
                {
                    context.Game.GetSceneNodeByInstanceID<Officer>("spy"),
                },
            };
            MissionOdds first = context.Assessment.GetMissionOdds(context.MissionQueries, request);
            AIAssessment next = new(context.Game, context.Faction, context.FactionView);

            MissionOdds second = next.GetMissionOdds(context.MissionQueries, request);

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreNotSame(first, second);
            Assert.AreEqual(
                first.PersonnelLossProbability,
                second.PersonnelLossProbability,
                0.000000001
            );
        }

        [TestCase(MissionTypeIDs.Espionage)]
        [TestCase(MissionTypeIDs.Reconnaissance)]
        public void GetOperationalMissionOdds_EquivalentAgentsAtOwnedOrigins_ReusesForecast(
            string missionType
        )
        {
            AITurnContext context = CreateAgentForecastContext(
                out SpecialForces first,
                out SpecialForces second
            );
            MissionContext request = new()
            {
                MissionTypeID = missionType,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant> { first },
            };

            MissionOdds firstOdds = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );
            request.MainParticipants[0] = second;
            MissionOdds secondOdds = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );

            Assert.That(firstOdds, Is.Not.Null);
            Assert.That(secondOdds, Is.SameAs(firstOdds));
        }

        [Test]
        public void GetOperationalMissionOdds_DifferentAgentRatings_KeepsForecastsSeparate()
        {
            AITurnContext context = CreateAgentForecastContext(
                out SpecialForces first,
                out SpecialForces second
            );
            second.Ratings[SkillRating.Espionage] += 1;
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant> { first },
            };

            MissionOdds firstOdds = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );
            request.MainParticipants[0] = second;
            MissionOdds secondOdds = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );

            Assert.That(firstOdds, Is.Not.Null);
            Assert.That(secondOdds, Is.Not.Null.And.Not.SameAs(firstOdds));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GetOperationalMissionOdds_EquivalentAgentTeams_ReusesForecast(bool hasDecoy)
        {
            AITurnContext context = CreateAgentTeamForecastContext(out SpecialForces[] agents);
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant> { agents[0], agents[1] },
                DecoyParticipants = hasDecoy
                    ? new List<IMissionParticipant> { agents[4] }
                    : new List<IMissionParticipant>(),
            };

            MissionOdds first = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );
            request.MainParticipants = new List<IMissionParticipant> { agents[2], agents[3] };
            MissionOdds second = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.SameAs(first));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GetOperationalMissionOdds_DuplicateAgentRoles_DoesNotReuseValidTeamForecast(
            bool duplicatesDecoy
        )
        {
            AITurnContext context = CreateAgentTeamForecastContext(out SpecialForces[] agents);
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant> { agents[0], agents[1] },
                DecoyParticipants = new List<IMissionParticipant> { agents[4] },
            };
            MissionOdds valid = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );
            request.MainParticipants[1] = duplicatesDecoy ? agents[4] : agents[0];

            MissionOdds invalid = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );

            Assert.That(valid, Is.Not.Null);
            Assert.That(invalid, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GetMissionOdds_EquivalentAgentWithSameOfficer_ReusesForecast(bool officerFirst)
        {
            AITurnContext context = CreateAgentTeamForecastContext(out SpecialForces[] agents);
            Officer officer = context.Game.GetSceneNodeByInstanceID<Officer>("spy");
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = officerFirst
                    ? new List<IMissionParticipant> { officer, agents[0] }
                    : new List<IMissionParticipant> { agents[0], officer },
            };
            MissionOdds first = context.Assessment.GetMissionOdds(context.MissionQueries, request);
            request.MainParticipants[officerFirst ? 1 : 0] = agents[1];

            MissionOdds second = context.Assessment.GetMissionOdds(context.MissionQueries, request);

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.SameAs(first));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GetMissionOdds_EquivalentDecoyAgents_ReusesForecast(bool officerLed)
        {
            AITurnContext context = CreateAgentTeamForecastContext(out SpecialForces[] agents);
            IMissionParticipant leader = officerLed
                ? context.Game.GetSceneNodeByInstanceID<Officer>("spy")
                : agents[0];
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant> { leader },
                DecoyParticipants = new List<IMissionParticipant> { agents[1] },
            };
            MissionOdds first = context.Assessment.GetMissionOdds(context.MissionQueries, request);
            request.DecoyParticipants[0] = agents[2];

            MissionOdds second = context.Assessment.GetMissionOdds(context.MissionQueries, request);
            AITurnContext fresh = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            MissionOdds uncached = fresh.Assessment.GetMissionOdds(fresh.MissionQueries, request);

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.SameAs(first));
            Assert.That(
                second.ObjectiveSuccessProbability,
                Is.EqualTo(uncached.ObjectiveSuccessProbability)
            );
            Assert.That(second.FoilProbability, Is.EqualTo(uncached.FoilProbability));
            Assert.That(
                second.PersonnelLossProbability,
                Is.EqualTo(uncached.PersonnelLossProbability)
            );
        }

        [Test]
        public void GetOperationalMissionOdds_DifferentDecoyRatings_KeepsForecastsSeparate()
        {
            AITurnContext context = CreateAgentTeamForecastContext(out SpecialForces[] agents);
            agents[2].Ratings[SkillRating.Espionage] += 1;
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant> { agents[0] },
                DecoyParticipants = new List<IMissionParticipant> { agents[1] },
            };
            MissionOdds first = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );
            request.DecoyParticipants[0] = agents[2];

            MissionOdds second = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null.And.Not.SameAs(first));
        }

        [Test]
        public void GetOperationalMissionOdds_UnqualifiedDecoy_DoesNotReuseEligibleForecast()
        {
            AITurnContext context = CreateAgentTeamForecastContext(out SpecialForces[] agents);
            agents[2].AllowedMissionTypeIDs.Remove(MissionTypeIDs.Espionage);
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant> { agents[0] },
                DecoyParticipants = new List<IMissionParticipant> { agents[1] },
            };
            MissionOdds first = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );
            request.DecoyParticipants[0] = agents[2];

            MissionOdds second = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GetOperationalMissionOdds_DuplicateDecoyRoles_DoesNotReuseValidForecast(
            bool duplicatesMain
        )
        {
            AITurnContext context = CreateAgentTeamForecastContext(out SpecialForces[] agents);
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant> { agents[0] },
                DecoyParticipants = new List<IMissionParticipant> { agents[1], agents[2] },
            };
            MissionOdds first = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );
            request.DecoyParticipants[1] = duplicatesMain ? agents[0] : agents[1];

            MissionOdds second = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Null);
        }

        [Test]
        public void GetMissionOddsAfterArrival_EquivalentInboundAgents_ReusesProjectedForecast()
        {
            AssertEquivalentProjectedAgentsShareForecast(true);
        }

        [Test]
        public void GetMissionOddsFromDeparture_EquivalentAgents_ReusesProjectedForecast()
        {
            AssertEquivalentProjectedAgentsShareForecast(false);
        }

        [Test]
        public void GetMissionOddsAfterArrival_AgentHeadingElsewhere_DoesNotReuseCorrectArrival()
        {
            AITurnContext context = CreateAgentForecastContext(
                out SpecialForces first,
                out SpecialForces second
            );
            Planet departure = first.GetParentOfType<Planet>();
            first.Movement = TestMovement.Create(transitTicks: 13);
            second.Movement = TestMovement.Create(transitTicks: 8);
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant> { first },
            };
            MissionOdds valid = context.Assessment.GetMissionOddsAfterArrival(
                context.MissionQueries,
                request,
                departure,
                true
            );
            request.MainParticipants[0] = second;

            MissionOdds invalid = context.Assessment.GetMissionOddsAfterArrival(
                context.MissionQueries,
                request,
                departure,
                true
            );
            MissionOdds relocated = context.Assessment.GetMissionOddsFromDeparture(
                context.MissionQueries,
                request,
                departure,
                true
            );

            Assert.That(valid, Is.Not.Null);
            Assert.That(invalid, Is.Null);
            Assert.That(relocated, Is.Not.Null);
        }

        [Test]
        public void GetOperationalMissionOdds_AgentTeamAtDifferentOrigins_DoesNotReuseCoLocatedForecast()
        {
            AITurnContext context = CreateAgentTeamForecastContext(out SpecialForces[] agents);
            context.Game.MoveNode(
                agents[3],
                context.Game.GetSceneNodeByInstanceID<Planet>("second-origin")
            );
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant> { agents[0], agents[1] },
            };
            MissionOdds valid = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );
            request.MainParticipants = new List<IMissionParticipant> { agents[2], agents[3] };

            MissionOdds invalid = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );

            Assert.That(valid, Is.Not.Null);
            Assert.That(invalid, Is.Null);
        }

        [Test]
        public void GetOperationalMissionOdds_UnqualifiedAgent_DoesNotReuseEligibleForecast()
        {
            AITurnContext context = CreateAgentForecastContext(
                out SpecialForces first,
                out SpecialForces second
            );
            second.AllowedMissionTypeIDs.Remove(MissionTypeIDs.Espionage);
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant> { first },
            };

            MissionOdds firstOdds = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );
            request.MainParticipants[0] = second;
            MissionOdds secondOdds = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );

            Assert.That(firstOdds, Is.Not.Null);
            Assert.That(secondOdds, Is.Null);
        }

        [Test]
        public void GetOperationalMissionOdds_UnsupportedRepeat_DoesNotReuseOneShotForecast()
        {
            AITurnContext context = CreateAgentForecastContext(
                out SpecialForces first,
                out SpecialForces second
            );
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Reconnaissance,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant> { first },
            };
            MissionOdds valid = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );
            request.MainParticipants[0] = second;
            request.IsRepeating = true;

            MissionOdds invalid = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );

            Assert.That(valid, Is.Not.Null);
            Assert.That(invalid, Is.Null);
        }

        [Test]
        public void GetOperationalMissionOdds_AgentAtEnemyOrigin_KeepsForecastsSeparate()
        {
            AITurnContext context = CreateAgentForecastContext(
                out SpecialForces first,
                out SpecialForces second
            );
            Planet origin = second.GetParentOfType<Planet>();
            origin.OwnerInstanceID = context
                .Game.GetSceneNodeByInstanceID<Planet>("target")
                .OwnerInstanceID;
            AITestSceneBuilder.RevealPlanet(context.Game, context.Faction, origin);
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant> { first },
            };

            MissionOdds firstOdds = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );
            request.MainParticipants[0] = second;
            MissionOdds secondOdds = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );

            Assert.That(firstOdds, Is.Not.Null);
            Assert.That(secondOdds, Is.Not.Null.And.Not.SameAs(firstOdds));
        }

        [Test]
        public void TryGetOperationalMissionOdds_RepeatedRejectedFloor_PreservesUnboundedForecast()
        {
            AITurnContext context = CreateAgentForecastContext(out SpecialForces first, out _);
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant> { first },
            };

            bool firstAccepted = context.Assessment.TryGetOperationalMissionOdds(
                context.MissionQueries,
                request,
                101,
                out _
            );
            bool secondAccepted = context.Assessment.TryGetOperationalMissionOdds(
                context.MissionQueries,
                request,
                101,
                out _
            );
            MissionOdds unbounded = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );

            Assert.That(firstAccepted, Is.False);
            Assert.That(secondAccepted, Is.False);
            Assert.That(unbounded, Is.Not.Null);
        }

        [Test]
        public void GetOperationalMissionOdds_AfterCompleteEvaluation_KeepsPersonnelLossOmitted()
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(100);
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant>
                {
                    context.Game.GetSceneNodeByInstanceID<Officer>("spy"),
                },
            };

            MissionOdds complete = context.Assessment.GetMissionOdds(
                context.MissionQueries,
                request
            );
            MissionOdds operational = context.Assessment.GetOperationalMissionOdds(
                context.MissionQueries,
                request
            );

            Assert.AreNotSame(complete, operational);
            Assert.Greater(complete.PersonnelLossProbability, 0);
            Assert.AreEqual(0, operational.PersonnelLossProbability);
            Assert.AreEqual(complete.FoilProbability, operational.FoilProbability, 0.000000001);
        }

        [TestCase(0, 0)]
        [TestCase(50, 0.5)]
        [TestCase(100, 1)]
        [TestCase(400, 1)]
        public void GetMissionOdds_AgingIntel_BlendsTowardObservedDefenseBaseline(
            int age,
            double baselineWeight
        )
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(age);
            Planet target = context.Assessment.GetKnownPlanet("target");
            Planet reference = context.Assessment.GetKnownPlanet("reference");
            Officer spy = context.Game.GetSceneNodeByInstanceID<Officer>("spy");
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = target,
                MainParticipants = new List<IMissionParticipant> { spy },
            };
            MissionOdds referenceOdds = context.MissionQueries.GetMissionOdds(
                new MissionContext
                {
                    MissionTypeID = MissionTypeIDs.Espionage,
                    Location = reference,
                    MainParticipants = new List<IMissionParticipant> { spy },
                }
            );

            MissionOdds odds = context.Assessment.GetMissionOdds(context.MissionQueries, request);

            Assert.AreEqual(
                referenceOdds.FoilProbability * baselineWeight,
                odds.FoilProbability,
                0.001
            );
            Assert.AreEqual(
                referenceOdds.PersonnelLossProbability * baselineWeight,
                odds.PersonnelLossProbability,
                0.001
            );
        }

        [Test]
        public void GetMissionOdds_MissingDefenderIntel_UsesBaselineDespiteRecentPlanetReport()
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(0);
            Planet target = context.Assessment.GetKnownPlanet("target");
            PlanetSector sector = target.GetParentOfType<PlanetSector>();
            context
                .Faction
                .Fog
                .Snapshots[sector.InstanceID]
                .Planets[target.InstanceID]
                .RevealedCategories = PlanetIntelligenceCategory.Planet;
            Officer spy = context.Game.GetSceneNodeByInstanceID<Officer>("spy");

            MissionOdds odds = context.Assessment.GetMissionOdds(
                context.MissionQueries,
                new MissionContext
                {
                    MissionTypeID = MissionTypeIDs.Espionage,
                    Location = target,
                    MainParticipants = new List<IMissionParticipant> { spy },
                }
            );

            Assert.Greater(odds.PersonnelLossProbability, 0);
        }

        [Test]
        public void GetMissionOdds_HiddenReinforcements_DoNotLeakIntoBaseline()
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(100);
            Planet target = context.Assessment.GetKnownPlanet("target");
            Officer spy = context.Game.GetSceneNodeByInstanceID<Officer>("spy");
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = target,
                MainParticipants = new List<IMissionParticipant> { spy },
            };
            MissionOdds before = context.Assessment.GetMissionOdds(context.MissionQueries, request);
            Planet liveReference = context.Game.GetSceneNodeByInstanceID<Planet>("reference");
            context.Game.AttachNode(
                new Regiment
                {
                    InstanceID = "hidden",
                    OwnerInstanceID = liveReference.OwnerInstanceID,
                    DetectionRating = 1000,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                liveReference
            );
            AIAssessment refreshed = AITestSceneBuilder
                .CreateContext(context.Game, context.Faction)
                .Assessment;

            MissionOdds after = refreshed.GetMissionOdds(context.MissionQueries, request);

            Assert.AreEqual(before.FoilProbability, after.FoilProbability, 0.001);
            Assert.AreEqual(before.PersonnelLossProbability, after.PersonnelLossProbability, 0.001);
        }

        [Test]
        public void GetMissionOdds_BaselineHasCommander_UsesReferenceCommanderRatings()
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(100);
            Planet liveReference = context.Game.GetSceneNodeByInstanceID<Planet>("reference");
            foreach (Regiment regiment in liveReference.GetChildren<Regiment>())
                regiment.DetectionRating = 0;
            Officer commander = EntityFactory.CreateOfficer(
                "general",
                liveReference.OwnerInstanceID
            );
            commander.CurrentRank = OfficerRank.General;
            commander.SetBaseRating(SkillRating.Espionage, 100);
            context.Game.AttachNode(commander, liveReference);
            context.Game.Config.ProbabilityTables.Mission.Foil = new Dictionary<int, int>
            {
                { -1000, 100 },
                { 20, 0 },
            };
            context.Game.CurrentTick = 0;
            AITestSceneBuilder.RevealPlanet(context.Game, context.Faction, liveReference);
            context.Game.CurrentTick = 100;
            AIAssessment assessment = AITestSceneBuilder
                .CreateContext(context.Game, context.Faction)
                .Assessment;

            MissionOdds odds = assessment.GetMissionOdds(
                context.MissionQueries,
                new MissionContext
                {
                    MissionTypeID = MissionTypeIDs.Espionage,
                    Location = assessment.GetKnownPlanet("target"),
                    MainParticipants = new List<IMissionParticipant>
                    {
                        context.Game.GetSceneNodeByInstanceID<Officer>("spy"),
                    },
                }
            );

            Assert.AreEqual(100, odds.FoilProbability, 0.001);
        }

        [Test]
        public void GetMissionOdds_NoObservedDefenses_UsesConfiguredFallback()
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(100);
            Planet reference = context.Game.GetSceneNodeByInstanceID<Planet>("reference");
            foreach (Regiment regiment in reference.GetChildren<Regiment>().ToList())
                context.Game.DetachNode(regiment);
            AITestSceneBuilder.RevealPlanet(context.Game, context.Faction, reference);
            context.Game.Config.AI.MissionPlanning.MissionDefenseFallbackGroundDetectorCount = 2;
            AIAssessment assessment = AITestSceneBuilder
                .CreateContext(context.Game, context.Faction)
                .Assessment;

            MissionOdds odds = assessment.GetMissionOdds(
                context.MissionQueries,
                new MissionContext
                {
                    MissionTypeID = MissionTypeIDs.Espionage,
                    Location = assessment.GetKnownPlanet("target"),
                    MainParticipants = new List<IMissionParticipant>
                    {
                        context.Game.GetSceneNodeByInstanceID<Officer>("spy"),
                    },
                }
            );

            Assert.Greater(odds.PersonnelLossProbability, 0);
        }

        [Test]
        public void GetMissionOdds_SpecialForcesDecoy_ReducesUncertainOfficerRisk()
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(100);
            Officer spy = context.Game.GetSceneNodeByInstanceID<Officer>("spy");
            SpecialForces decoy = new()
            {
                InstanceID = "decoy",
                OwnerInstanceID = context.Faction.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new List<string> { MissionTypeIDs.Espionage },
            };
            context.Game.AttachNode(decoy, spy.GetParent());
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant> { spy },
            };
            MissionOdds unprotected = context.Assessment.GetMissionOdds(
                context.MissionQueries,
                request
            );
            request.DecoyParticipants = new List<IMissionParticipant> { decoy };

            MissionOdds protectedTeam = context.Assessment.GetMissionOdds(
                context.MissionQueries,
                request
            );

            Assert.Less(
                protectedTeam.PersonnelLossProbability,
                unprotected.PersonnelLossProbability
            );
        }

        [Test]
        public void GetMissionOdds_WeakerBaseline_DoesNotReduceObservedRisk()
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(100);
            Planet target = context.Game.GetSceneNodeByInstanceID<Planet>("target");
            for (int index = 0; index < 3; index++)
                context.Game.AttachNode(
                    new Regiment
                    {
                        InstanceID = "target-" + index,
                        OwnerInstanceID = target.OwnerInstanceID,
                        DetectionRating = 0,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    target
                );
            context.Game.CurrentTick = 0;
            AITestSceneBuilder.RevealPlanet(context.Game, context.Faction, target);
            context.Game.CurrentTick = 100;
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant>
                {
                    context.Game.GetSceneNodeByInstanceID<Officer>("spy"),
                },
            };
            MissionOdds observed = context.MissionQueries.GetMissionOdds(
                request,
                context.Assessment.GetMissionDetectors
            );

            MissionOdds adjusted = context.Assessment.GetMissionOdds(
                context.MissionQueries,
                request
            );

            Assert.AreEqual(observed.FoilProbability, adjusted.FoilProbability, 0.001);
            Assert.AreEqual(
                observed.PersonnelLossProbability,
                adjusted.PersonnelLossProbability,
                0.001
            );
        }

        [Test]
        public void GetMissionOdds_AnotherEnemyHasStrongerDefenses_UsesTargetFactionBaseline()
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(100);
            Officer spy = context.Game.GetSceneNodeByInstanceID<Officer>("spy");
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant> { spy },
            };
            MissionOdds before = context.Assessment.GetMissionOdds(context.MissionQueries, request);
            context.Game.GetFactions().Add(new Faction { InstanceID = "other-faction" });
            Planet otherEnemy = AITestSceneBuilder.AddPlanet(
                context.Game,
                context.Game.GetSceneNodeByInstanceID<PlanetSector>("sector"),
                "other-enemy",
                "other-faction"
            );
            for (int index = 0; index < 8; index++)
                context.Game.AttachNode(
                    new Regiment
                    {
                        InstanceID = "other-" + index,
                        OwnerInstanceID = otherEnemy.OwnerInstanceID,
                        DetectionRating = 100,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    otherEnemy
                );
            AITestSceneBuilder.RevealPlanet(context.Game, context.Faction, otherEnemy);
            AIAssessment assessment = AITestSceneBuilder
                .CreateContext(context.Game, context.Faction)
                .Assessment;

            MissionOdds after = assessment.GetMissionOdds(context.MissionQueries, request);

            Assert.AreEqual(before.FoilProbability, after.FoilProbability, 0.001);
            Assert.AreEqual(before.PersonnelLossProbability, after.PersonnelLossProbability, 0.001);
        }

        [Test]
        public void GetMissionOdds_SeparateGroundAndOrbitalReferences_UsesBothDefenseGroups()
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(100);
            Planet target = context.Game.GetSceneNodeByInstanceID<Planet>("target");
            Planet orbitalReference = AITestSceneBuilder.AddPlanet(
                context.Game,
                context.Game.GetSceneNodeByInstanceID<PlanetSector>("sector"),
                "orbital-reference",
                target.OwnerInstanceID
            );
            Fleet fleet = EntityFactory.CreateFleet("reference-fleet", target.OwnerInstanceID);
            context.Game.AttachNode(fleet, orbitalReference);
            CapitalShip detector = AITestSceneBuilder.CreateCapitalShip(
                "orbital-detector",
                target.OwnerInstanceID
            );
            detector.DetectionRating = 100;
            context.Game.AttachNode(detector, fleet);
            context.Game.CurrentTick = 0;
            AITestSceneBuilder.RevealPlanet(context.Game, context.Faction, target);
            AITestSceneBuilder.RevealPlanet(context.Game, context.Faction, orbitalReference);
            context.Game.CurrentTick = 100;
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);

            MissionOdds odds = context.Assessment.GetMissionOdds(
                context.MissionQueries,
                new MissionContext
                {
                    MissionTypeID = MissionTypeIDs.Espionage,
                    Location = context.Assessment.GetKnownPlanet("target"),
                    MainParticipants = new List<IMissionParticipant>
                    {
                        context.Game.GetSceneNodeByInstanceID<Officer>("spy"),
                    },
                }
            );

            Assert.AreEqual(87.5, odds.FoilProbability, 0.001);
        }

        [TestCase("planet", "empire", true)]
        [TestCase("elsewhere", "empire", false)]
        [TestCase("planet", "rebels", false)]
        public void Constructor_HoldPositionConditions_RestrictsOnlyMatchingOfficersDestination(
            string requiredParent,
            string requiredOwner,
            bool held
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction owner, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", owner.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", owner.InstanceID);
            Officer other = EntityFactory.CreateOfficer("other", owner.InstanceID);
            game.AttachNode(officer, planet);
            game.AttachNode(other, planet);
            Dictionary<string, OfficerAIBehavior> behaviors = new()
            {
                [officer.InstanceID] = new OfficerAIBehavior
                {
                    Conditionals = new List<GameConditional>
                    {
                        new HasParentConditional
                        {
                            UnitInstanceID = officer.InstanceID,
                            ParentInstanceID = requiredParent,
                        },
                        new IsOwnedConditional
                        {
                            PlanetInstanceID = planet.InstanceID,
                            FactionInstanceID = requiredOwner,
                        },
                    },
                },
            };

            Planet remote = AITestSceneBuilder.AddPlanet(game, sector, "remote", owner.InstanceID);
            AIAssessment assessment = new(game, owner, game.Galaxy, behaviors);

            Assert.AreEqual(held, assessment.IsOfficerHoldingPosition(officer));
            CollectionAssert.Contains(assessment.AvailableMissionParticipants, officer);
            Assert.IsTrue(assessment.CanAssignOfficerToPlanet(officer, planet));
            Assert.AreEqual(!held, assessment.CanAssignOfficerToPlanet(officer, remote));
            CollectionAssert.Contains(assessment.AvailableMissionParticipants, other);
        }

        [Test]
        public void Constructor_HoldConditionsStopMatching_ReleasesOfficerOnNextAssessment()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction owner, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", owner.InstanceID);
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "destination",
                owner.InstanceID
            );
            Officer officer = EntityFactory.CreateOfficer("officer", owner.InstanceID);
            game.AttachNode(officer, origin);
            Dictionary<string, OfficerAIBehavior> behaviors = new()
            {
                [officer.InstanceID] = new OfficerAIBehavior
                {
                    Conditionals = new List<GameConditional>
                    {
                        new HasParentConditional
                        {
                            UnitInstanceID = officer.InstanceID,
                            ParentInstanceID = origin.InstanceID,
                        },
                    },
                },
            };
            Assert.IsTrue(
                new AIAssessment(game, owner, game.Galaxy, behaviors).IsOfficerHoldingPosition(
                    officer
                )
            );
            game.MoveNode(officer, destination);

            AIAssessment next = new(game, owner, game.Galaxy, behaviors);

            CollectionAssert.Contains(next.AvailableMissionParticipants, officer);
        }

        [Test]
        public void Constructor_HoldBehaviorForAnotherFaction_DoesNotClaimForeignOfficer()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction owner, out Faction enemy);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", enemy.InstanceID);
            Officer officer = EntityFactory.CreateOfficer("officer", enemy.InstanceID);
            game.AttachNode(officer, planet);
            Dictionary<string, OfficerAIBehavior> behaviors = new()
            {
                [officer.InstanceID] = new OfficerAIBehavior(),
            };

            AIAssessment assessment = new(game, owner, game.Galaxy, behaviors);

            Assert.IsFalse(assessment.IsOfficerHoldingPosition(officer));
        }

        [Test]
        public void Constructor_WithMixedPlanetOwnership_BuildsOwnershipLists()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Planet neutral = AITestSceneBuilder.AddPlanet(game, system, "neutral", null);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            AITestSceneBuilder.RevealPlanet(game, empire, neutral);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIAssessment assessment = context.Assessment;

            CollectionAssert.Contains(assessment.OwnedPlanets, owned);
            CollectionAssert.Contains(
                assessment.EnemyPlanets.Select(planet => planet.InstanceID),
                enemy.InstanceID
            );
            CollectionAssert.Contains(
                assessment.NeutralPlanets.Select(planet => planet.InstanceID),
                neutral.InstanceID
            );
        }

        [Test]
        public void Constructor_WithEconomicState_CachesTurnValues()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Starfighter starfighter = AITestSceneBuilder.CreateStarfighter(
                "fighter",
                empire.InstanceID,
                maintenanceCost: 7
            );
            game.AttachNode(starfighter, planet);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIAssessment assessment = context.Assessment;
            int cachedHeadroom = assessment.ProjectedMaintenanceHeadroom;
            int cachedSupply = assessment.RefinedMaterialSupply;
            int cachedStockpile = assessment.RefinedMaterialStockpile;
            starfighter.MaintenanceCost = 11;
            empire.RefinedMaterialStockpile++;

            Assert.AreEqual(empire.MaintenanceCapacity, assessment.MaintenanceCapacity);
            Assert.AreEqual(cachedHeadroom, assessment.ProjectedMaintenanceHeadroom);
            Assert.AreNotEqual(
                empire.ProjectedMaintenanceHeadroom,
                assessment.ProjectedMaintenanceHeadroom
            );
            Assert.AreEqual(cachedSupply, assessment.RefinedMaterialSupply);
            Assert.AreEqual(cachedStockpile, assessment.RefinedMaterialStockpile);
        }

        [Test]
        public void Constructor_HardAiMaintenanceBonus_CachesEffectiveCapacity()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Summary = new GameSummary { Difficulty = GameDifficulty.Hard };
            game.SetFactionController(empire.InstanceID, "EMPIRE-AI", PlayerControllerType.AI);
            game.Config.DifficultyModifiers[GameDifficulty.Hard] = new DifficultyModifiers
            {
                MaintenanceCapacityPercent = 150,
            };
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "owned",
                empire.InstanceID,
                rawResourceNodes: 1
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "mine",
                BuildingType.Mine,
                ManufacturingType.Building
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "refinery",
                BuildingType.Refinery,
                ManufacturingType.Building
            );

            AIAssessment assessment = new AIAssessment(game, empire, game.Galaxy);

            Assert.AreEqual(75, assessment.MaintenanceCapacity);
            Assert.AreEqual(75, assessment.ProjectedMaintenanceHeadroom);
            Assert.AreEqual(75, assessment.ProjectedEconomyMaintenanceHeadroom);
        }

        [Test]
        public void Constructor_MaximumEconomyModifiers_SaturatesCachedValues()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Summary = new GameSummary { Difficulty = GameDifficulty.Hard };
            game.SetFactionController(empire.InstanceID, "EMPIRE-AI", PlayerControllerType.AI);
            game.Config.DifficultyModifiers[GameDifficulty.Hard] = new DifficultyModifiers
            {
                MaintenanceCapacityPercent = int.MaxValue,
            };
            empire.Settings.ResourceProcessingPointsPerFacility = int.MaxValue;
            empire.Settings.RefinementMultiplier = int.MaxValue;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "owned",
                empire.InstanceID,
                rawResourceNodes: 2
            );
            for (int index = 0; index < 2; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"mine-{index}",
                    BuildingType.Mine,
                    ManufacturingType.Building
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"refinery-{index}",
                    BuildingType.Refinery,
                    ManufacturingType.Building
                );
            }

            AIAssessment assessment = new AIAssessment(game, empire, game.Galaxy);

            Assert.AreEqual(int.MaxValue, assessment.MaintenanceCapacity);
            Assert.AreEqual(int.MaxValue, assessment.ProjectedMaintenanceHeadroom);
            Assert.AreEqual(int.MaxValue, assessment.ProjectedEconomyMaintenanceHeadroom);
            Assert.AreEqual(int.MaxValue, assessment.RefinedMaterialSupply);
        }

        [Test]
        public void GetAvailableProductionLaneCount_WithPartiallyUsedStack_ReturnsFreeLanes()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "production-world",
                empire.InstanceID
            );
            for (int index = 0; index < 3; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"construction-facility-{index}",
                    BuildingType.ConstructionFacility,
                    ManufacturingType.Building
                );
            }

            Building queued = AITestSceneBuilder.CreateBuildingTemplate(
                "queued-building",
                BuildingType.Defense
            );
            queued.OwnerInstanceID = empire.InstanceID;
            queued.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(queued, planet);
            planet.AddToManufacturingQueue(queued);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(
                2,
                assessment.GetAvailableProductionLaneCount(ManufacturingType.Building)
            );
        }

        [Test]
        public void GetOffensiveSupportLeverage_WithFavoredTargetAndExposedPlanet_ReturnsBoth()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.SupportShift.OwnershipTransferThreshold = 60;
            game.Config.SupportShift.GarrisonRemovalSupportShift = 10;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, 55);
            target.SetPopularSupport(rebels.InstanceID, 45);
            Planet exposed = AITestSceneBuilder.AddPlanet(game, system, "exposed", null);
            exposed.SetPopularSupport(empire.InstanceID, 55);
            exposed.SetPopularSupport(rebels.InstanceID, 45);
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITestSceneBuilder.RevealPlanet(game, empire, exposed);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(2, assessment.GetOffensiveSupportLeverage(target));
        }

        [Test]
        public void GetDefensiveSupportRisk_WithFavoredEnemyAndExposedPlanet_ReturnsBoth()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.SupportShift.OwnershipTransferThreshold = 60;
            game.Config.SupportShift.GarrisonRemovalSupportShift = 10;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", empire.InstanceID);
            target.SetPopularSupport(empire.InstanceID, 45);
            target.SetPopularSupport(rebels.InstanceID, 55);
            Planet exposed = AITestSceneBuilder.AddPlanet(game, system, "exposed", null);
            exposed.SetPopularSupport(empire.InstanceID, 45);
            exposed.SetPopularSupport(rebels.InstanceID, 55);
            AITestSceneBuilder.RevealPlanet(game, empire, exposed);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(2, assessment.GetDefensiveSupportRisk(target));
        }

        [Test]
        public void Constructor_WithUnobservedForeignPlanet_DoesNotExposePlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet hidden = AITestSceneBuilder.AddPlanet(game, system, "hidden", rebels.InstanceID);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            CollectionAssert.DoesNotContain(
                assessment.KnownColonizedPlanets.Select(planet => planet.InstanceID),
                hidden.InstanceID
            );
            Assert.AreEqual(hidden.InstanceID, assessment.UnexploredPlanets.Single().InstanceID);
            Assert.IsNull(assessment.GetKnownPlanet(hidden.InstanceID));
        }

        [Test]
        public void Constructor_WithObservedUncolonizedPlanet_ExposesColonizationTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            AITestSceneBuilder.RevealPlanet(game, empire, target);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(
                target.InstanceID,
                assessment.KnownUncolonizedPlanets.Single().InstanceID
            );
            Assert.AreEqual(
                target.InstanceID,
                assessment.GetKnownPlanet(target.InstanceID).InstanceID
            );
        }

        [Test]
        public void Constructor_WithStaleUncolonizedSnapshot_DoesNotRevealCurrentColonization()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            target.IsColonized = true;
            game.ChangeOwnership(target, rebels.InstanceID);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;
            Planet knownTarget = assessment.GetKnownPlanet(target.InstanceID);

            Assert.IsFalse(knownTarget.IsColonized);
            Assert.IsNull(knownTarget.GetOwnerInstanceID());
            Assert.AreEqual(
                target.InstanceID,
                assessment.KnownUncolonizedPlanets.Single().InstanceID
            );
        }

        [Test]
        public void Constructor_WithEnemyOfficer_BuildsTargetableEnemyOfficerTargets()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Officer target = EntityFactory.CreateOfficer("target", rebels.InstanceID);
            game.AttachNode(target, enemy);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(1, assessment.TargetableEnemyOfficerMissionTargets.Count);
            Assert.AreEqual(
                enemy.InstanceID,
                assessment.TargetableEnemyOfficerMissionTargets[0].Planet.InstanceID
            );
            Assert.AreEqual(
                target.InstanceID,
                assessment.TargetableEnemyOfficerMissionTargets[0].TargetOfficer.InstanceID
            );
            Assert.AreNotSame(
                target,
                assessment.TargetableEnemyOfficerMissionTargets[0].TargetOfficer
            );
        }

        [Test]
        public void Constructor_WithEnemyOfficerAboardFleet_BuildsTargetableEnemyOfficerTargets()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", rebels.InstanceID);
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip("ship", rebels.InstanceID);
            Officer target = EntityFactory.CreateOfficer("target", rebels.InstanceID);
            game.AttachNode(fleet, enemy);
            game.AttachNode(ship, fleet);
            game.AttachNode(target, ship);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            (Planet Planet, Officer TargetOfficer) candidate =
                assessment.TargetableEnemyOfficerMissionTargets.Single();
            Assert.AreEqual(enemy.InstanceID, candidate.Planet.InstanceID);
            Assert.AreEqual(target.InstanceID, candidate.TargetOfficer.InstanceID);
            Assert.AreNotSame(target, candidate.TargetOfficer);
        }

        [Test]
        public void Constructor_WithStaleSnapshot_UsesLastObservedEnemyState()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Building observed = AITestSceneBuilder.CreateBuildingTemplate(
                "observed",
                BuildingType.Mine
            );
            observed.OwnerInstanceID = rebels.InstanceID;
            observed.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(observed, enemy);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);

            Building hidden = AITestSceneBuilder.CreateBuildingTemplate(
                "hidden",
                BuildingType.Mine
            );
            hidden.OwnerInstanceID = rebels.InstanceID;
            hidden.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(hidden, enemy);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;
            Planet knownEnemy = assessment.GetKnownPlanet(enemy.InstanceID);

            Assert.AreEqual(1, assessment.GetPlanetBuildingCount(knownEnemy));
        }

        [Test]
        public void Constructor_WithStaleSnapshot_UsesDetachedRecordedEntityCopies()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Building observed = AITestSceneBuilder.CreateBuildingTemplate(
                "observed",
                BuildingType.Mine
            );
            observed.OwnerInstanceID = rebels.InstanceID;
            observed.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(observed, enemy);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Building recorded = empire
                .Fog.Snapshots[system.InstanceID]
                .Planets[enemy.InstanceID]
                .Buildings.Single();

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;
            Planet knownEnemy = assessment.GetKnownPlanet(enemy.InstanceID);

            Building knownBuilding = knownEnemy.GetChildren<Building>().Single();
            Assert.AreNotSame(recorded, knownBuilding);
            Assert.AreEqual(recorded.InstanceID, knownBuilding.InstanceID);
            Assert.AreSame(knownEnemy, knownBuilding.GetParentOfType<Planet>());
        }

        [Test]
        public void CanFleetDepartHeadquarters_WithOnlyLocalFleet_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, headquarters);
            game.AttachNode(AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID), fleet);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.IsFalse(
                AITestSceneBuilder.CreateContext(game, empire).StrategicPlan.CanFleetDepart(fleet)
            );
        }

        [Test]
        public void CanFleetDepartHeadquarters_WithThreatBeyondRemainingDefense_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Fleet departingFleet = EntityFactory.CreateFleet("departing", empire.InstanceID);
            game.AttachNode(departingFleet, headquarters);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "departing-ship",
                    empire.InstanceID,
                    combatStrength: 3000
                ),
                departingFleet
            );
            Fleet remainingFleet = EntityFactory.CreateFleet("remaining", empire.InstanceID);
            game.AttachNode(remainingFleet, headquarters);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "remaining-ship",
                    empire.InstanceID,
                    combatStrength: 1000
                ),
                remainingFleet
            );
            Fleet hostileFleet = EntityFactory.CreateFleet("hostile", rebels.InstanceID);
            game.AttachNode(hostileFleet, headquarters);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "hostile-ship",
                    rebels.InstanceID,
                    combatStrength: 2000
                ),
                hostileFleet
            );

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.IsFalse(
                AITestSceneBuilder
                    .CreateContext(game, empire)
                    .StrategicPlan.CanFleetDepart(departingFleet)
            );
        }

        [Test]
        public void GetRequiredHeadquartersDefenseStrength_WithKnownHostileFleet_UsesAffordableShare()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Fleet friendlyFleet = EntityFactory.CreateFleet("friendly", empire.InstanceID);
            game.AttachNode(friendlyFleet, headquarters);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "friendly-ship",
                    empire.InstanceID,
                    combatStrength: 4000
                ),
                friendlyFleet
            );
            Planet enemyPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "enemy",
                rebels.InstanceID
            );
            Fleet hostileFleet = EntityFactory.CreateFleet("hostile", rebels.InstanceID);
            game.AttachNode(hostileFleet, enemyPlanet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "hostile-ship",
                    rebels.InstanceID,
                    combatStrength: 2000
                ),
                hostileFleet
            );
            AITestSceneBuilder.RevealPlanet(game, empire, enemyPlanet);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.AreEqual(
                1400,
                context.StrategicPlan.GetHeadquartersDefenseStrength(headquarters)
            );
        }

        [Test]
        public void GetCommittedHeadquartersDefenseStrength_Default_SumsLocalAndInboundFleets()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Fleet localFleet = EntityFactory.CreateFleet("local", empire.InstanceID);
            game.AttachNode(localFleet, headquarters);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "local-ship",
                    empire.InstanceID,
                    combatStrength: 700
                ),
                localFleet
            );
            Fleet inboundFleet = EntityFactory.CreateFleet("inbound", empire.InstanceID);
            inboundFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                TargetPlanetId = headquarters.InstanceID,
            };
            inboundFleet.Movement = TestMovement.Create(transitTicks: 5);
            game.AttachNode(inboundFleet, staging);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "inbound-ship",
                    empire.InstanceID,
                    combatStrength: 600
                ),
                inboundFleet
            );

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(1300, assessment.GetCommittedHeadquartersDefenseStrength(headquarters));
        }

        [Test]
        public void GetRequiredHeadquartersDefenseStrength_WithUnknownRemoteAttackOrder_UsesMinimumStrength()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Planet enemyPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "enemy",
                rebels.InstanceID
            );
            Fleet hostileFleet = EntityFactory.CreateFleet("hostile", rebels.InstanceID);
            hostileFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                TargetPlanetId = headquarters.InstanceID,
            };
            game.AttachNode(hostileFleet, enemyPlanet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "hostile-ship",
                    rebels.InstanceID,
                    combatStrength: 2000
                ),
                hostileFleet
            );
            AITestSceneBuilder.RevealPlanet(game, empire, enemyPlanet);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.AreEqual(
                1000,
                context.StrategicPlan.GetHeadquartersDefenseStrength(headquarters)
            );
        }

        [Test]
        public void CanFleetDepartHeadquarters_AtUndefendedCapturedEnemyHeadquarters_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "captured-headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            rebels.HQInstanceID = headquarters.InstanceID;
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, headquarters);
            game.AttachNode(AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID), fleet);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.IsFalse(
                AITestSceneBuilder.CreateContext(game, empire).StrategicPlan.CanFleetDepart(fleet)
            );
        }

        [Test]
        public void CanFleetDepartHeadquarters_WhenEitherFleetLeavesEnoughDefense_ReturnsTrue()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Fleet strongest = EntityFactory.CreateFleet("strongest", empire.InstanceID);
            Fleet other = EntityFactory.CreateFleet("other", empire.InstanceID);
            game.AttachNode(strongest, headquarters);
            game.AttachNode(other, headquarters);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "strongest-ship",
                    empire.InstanceID,
                    combatStrength: 1500
                ),
                strongest
            );
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "other-ship",
                    empire.InstanceID,
                    combatStrength: 1200
                ),
                other
            );
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            AIStrategicPlan plan = AITestSceneBuilder.CreateContext(game, empire).StrategicPlan;
            Assert.IsTrue(plan.CanFleetDepart(strongest));
            Assert.IsTrue(plan.CanFleetDepart(other));
        }

        [Test]
        public void CanFleetDepartHeadquarters_FromUnstableOwnedPlanet_ReturnsFalseUntilThreshold()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Garrison.SupportThreshold = 50;
            game.Config.SupportShift.OwnershipTransferThreshold = 60;
            game.Config.SupportShift.GarrisonRemovalSupportShift = 10;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "captured",
                empire.InstanceID
            );
            Planet exposed = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "exposed",
                empire.InstanceID
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, planet);
            planet.SetPopularSupport(empire.InstanceID, 49);
            planet.SetPopularSupport(rebels.InstanceID, 51);
            exposed.SetPopularSupport(rebels.InstanceID, 55);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.IsFalse(
                AITestSceneBuilder.CreateContext(game, empire).StrategicPlan.CanFleetDepart(fleet)
            );

            planet.SetPopularSupport(empire.InstanceID, 50);

            Assert.IsTrue(
                AITestSceneBuilder.CreateContext(game, empire).StrategicPlan.CanFleetDepart(fleet)
            );
        }

        [Test]
        public void CanFleetDepartHeadquarters_FromLowLeverageEnemyDominatedSystem_ReturnsTrue()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Garrison.SupportThreshold = 50;
            game.Config.SupportShift.OwnershipTransferThreshold = 60;
            game.Config.SupportShift.GarrisonRemovalSupportShift = 10;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet captured = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "captured",
                empire.InstanceID
            );
            AITestSceneBuilder.AddPlanet(game, sector, "enemy-1", rebels.InstanceID);
            AITestSceneBuilder.AddPlanet(game, sector, "enemy-2", rebels.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, captured);
            captured.SetPopularSupport(empire.InstanceID, 10);
            captured.SetPopularSupport(rebels.InstanceID, 90);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(1, assessment.GetDefensiveSupportRisk(captured));
            Assert.IsTrue(
                AITestSceneBuilder.CreateContext(game, empire).StrategicPlan.CanFleetDepart(fleet)
            );
        }

        [Test]
        public void CanFleetDepartHeadquarters_FromLowLeverageFriendlySystem_ReturnsTrue()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Garrison.SupportThreshold = 50;
            game.Config.SupportShift.OwnershipTransferThreshold = 60;
            game.Config.SupportShift.GarrisonRemovalSupportShift = 10;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet captured = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "captured",
                empire.InstanceID
            );
            Planet secure = AITestSceneBuilder.AddPlanet(game, sector, "secure", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, captured);
            captured.SetPopularSupport(empire.InstanceID, 10);
            captured.SetPopularSupport(rebels.InstanceID, 90);
            secure.SetPopularSupport(empire.InstanceID, 90);
            secure.SetPopularSupport(rebels.InstanceID, 20);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(1, assessment.GetDefensiveSupportRisk(captured));
            Assert.IsTrue(
                AITestSceneBuilder.CreateContext(game, empire).StrategicPlan.CanFleetDepart(fleet)
            );
        }

        [Test]
        public void CanFleetDepartHeadquarters_FromHostilePlanet_AllowsImmediateEvacuation()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Garrison.SupportThreshold = 50;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "enemy", rebels.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, planet);
            planet.SetPopularSupport(empire.InstanceID, 0);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.IsTrue(
                AITestSceneBuilder.CreateContext(game, empire).StrategicPlan.CanFleetDepart(fleet)
            );
        }

        [Test]
        public void CanFleetDepartHeadquarters_FromUnstableFullyShieldedPlanet_ReturnsTrue()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out _);
            game.Config.AI.Garrison.SupportThreshold = 50;
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "captured",
                empire.InstanceID
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, planet);
            planet.SetPopularSupport(empire.InstanceID, 0);
            AddShield(game, planet, "shield-1", empire.InstanceID, 5);
            AddShield(game, planet, "shield-2", empire.InstanceID, 5);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.IsTrue(
                AITestSceneBuilder.CreateContext(game, empire).StrategicPlan.CanFleetDepart(fleet)
            );
        }

        [Test]
        public void GetProjectedFleetCombatValue_WithCommittedUnits_IncludesReadyAndPendingCombat()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            CapitalShip readyShip = AITestSceneBuilder.CreateCapitalShip(
                "ready-ship",
                empire.InstanceID,
                combatStrength: 100,
                starfighterCapacity: 1
            );
            Starfighter readyFighter = new Starfighter
            {
                InstanceID = "ready-fighter",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                LaserCannon = 100,
                MaxSquadronSize = 10,
                CurrentSquadronSize = 10,
            };
            CapitalShip inboundShip = AITestSceneBuilder.CreateCapitalShip(
                "inbound-ship",
                empire.InstanceID,
                combatStrength: 300,
                starfighterCapacity: 1
            );
            inboundShip.Movement = TestMovement.Create(transitTicks: 10);
            Starfighter inboundFighter = new Starfighter
            {
                InstanceID = "inbound-fighter",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                LaserCannon = 50,
                MaxSquadronSize = 10,
                CurrentSquadronSize = 10,
            };
            CapitalShip buildingShip = AITestSceneBuilder.CreateCapitalShip(
                "building-ship",
                empire.InstanceID,
                combatStrength: 200
            );
            buildingShip.ManufacturingStatus = ManufacturingStatus.Building;

            game.AttachNode(fleet, planet);
            game.AttachNode(readyShip, fleet);
            game.AttachNode(readyFighter, readyShip);
            game.AttachNode(inboundShip, fleet);
            game.AttachNode(inboundFighter, inboundShip);
            game.AttachNode(buildingShip, fleet);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(200, assessment.GetReadyFleetCombatValue(fleet));
            Assert.AreEqual(750, assessment.GetProjectedFleetCombatValue(fleet));
        }

        [Test]
        public void GetReadyFleetCombatValueAgainstCapitalShips_WithPartialSquadron_ReturnsSurvivingFighterStrength()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                "carrier",
                empire.InstanceID,
                combatStrength: 0
            );
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "fighter",
                empire.InstanceID,
                laserCannon: 10
            );
            fighter.CurrentSquadronSize = 6;
            game.AttachNode(fleet, planet);
            game.AttachNode(carrier, fleet);
            game.AttachNode(fighter, carrier);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(5, assessment.GetReadyFleetCombatValueAgainstCapitalShips(fleet));
        }

        [Test]
        public void GetProjectedFleetCombatValueAgainstCapitalShips_WithPendingSquadron_ReturnsFullFighterStrength()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                "carrier",
                empire.InstanceID,
                combatStrength: 0
            );
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "fighter",
                empire.InstanceID,
                laserCannon: 10
            );
            fighter.CurrentSquadronSize = 0;
            fighter.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(fleet, planet);
            game.AttachNode(carrier, fleet);
            game.AttachNode(fighter, carrier);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(10, assessment.GetProjectedFleetCombatValueAgainstCapitalShips(fleet));
        }

        [Test]
        public void GetReadyFleetFirepowerAgainstStarfighters_WithTorpedoes_ExcludesTorpedoStrength()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                "carrier",
                empire.InstanceID,
                combatStrength: 0
            );
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "fighter",
                empire.InstanceID,
                laserCannon: 10
            );
            fighter.IonCannon = 5;
            fighter.Torpedoes = 20;
            fighter.CurrentSquadronSize = 6;
            game.AttachNode(fleet, planet);
            game.AttachNode(carrier, fleet);
            game.AttachNode(fighter, carrier);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(60, assessment.GetReadyFleetFirepowerAgainstStarfighters(fleet));
        }

        [Test]
        public void GetReadyFleetFirepowerAgainstCapitalShips_WithIonCannonsAndTorpedoes_UsesIonCannons()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                "carrier",
                empire.InstanceID,
                combatStrength: 0
            );
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "fighter",
                empire.InstanceID,
                laserCannon: 6
            );
            fighter.IonCannon = 6;
            fighter.Torpedoes = 100;
            fighter.CurrentSquadronSize = 12;
            game.AttachNode(fleet, planet);
            game.AttachNode(carrier, fleet);
            game.AttachNode(fighter, carrier);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(700, assessment.GetReadyFleetFirepowerAgainstCapitalShips(fleet));
        }

        [Test]
        public void GetStrongestHostileFleetFirepower_WithVisibleFleet_ReturnsKnownComposition()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("hostile-fleet", rebels.InstanceID);
            CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                "hostile-carrier",
                rebels.InstanceID,
                combatStrength: 100
            );
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "hostile-fighter",
                rebels.InstanceID,
                laserCannon: 10
            );
            fighter.CurrentSquadronSize = 6;
            game.AttachNode(fleet, target);
            game.AttachNode(carrier, fleet);
            game.AttachNode(fighter, carrier);
            AITestSceneBuilder.RevealPlanet(game, empire, target);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.Greater(
                assessment.GetStrongestHostileFleetFirepowerAgainstCapitalShips(target),
                0
            );
            Assert.Greater(
                assessment.GetStrongestHostileFleetFirepowerAgainstStarfighters(target),
                0
            );
            Assert.AreEqual(1, assessment.GetStrongestHostileFleetStarfighterCount(target));
        }

        [Test]
        public void GetProjectedFleetRegimentAttackStrength_WithCommittedRegiments_IncludesPendingStrength()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            CapitalShip readyShip = AITestSceneBuilder.CreateCapitalShip(
                "ready-ship",
                empire.InstanceID,
                regimentCapacity: 2
            );
            CapitalShip inboundShip = AITestSceneBuilder.CreateCapitalShip(
                "inbound-ship",
                empire.InstanceID
            );
            inboundShip.Movement = TestMovement.Create(transitTicks: 10);
            Regiment readyRegiment = AITestSceneBuilder.CreateRegiment(
                "ready-regiment",
                empire.InstanceID,
                attackRating: 10
            );
            Regiment inboundRegiment = AITestSceneBuilder.CreateRegiment(
                "inbound-regiment",
                empire.InstanceID,
                attackRating: 20
            );
            Regiment buildingRegiment = AITestSceneBuilder.CreateRegiment(
                "building-regiment",
                empire.InstanceID,
                attackRating: 30
            );
            buildingRegiment.ManufacturingStatus = ManufacturingStatus.Building;

            game.AttachNode(fleet, planet);
            game.AttachNode(readyShip, fleet);
            game.AttachNode(readyRegiment, readyShip);
            game.AttachNode(inboundShip, fleet);
            game.AttachNode(inboundRegiment, inboundShip);
            game.AttachNode(buildingRegiment, readyShip);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(10, assessment.GetReadyFleetRegimentAttackStrength(fleet));
            Assert.AreEqual(60, assessment.GetProjectedFleetRegimentAttackStrength(fleet));
        }

        [Test]
        public void GetFleetBombardmentStrength_WithDamagedUnitsAndAdmiral_MatchesCombatRules()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.Combat.Bombardment.AttackerLeadershipDivisor = 10;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID);
            ship.Bombardment = 20;
            ship.CurrentHullStrength = 50;
            Starfighter fighter = new Starfighter
            {
                InstanceID = "fighter",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Bombardment = 10,
                MaxSquadronSize = 10,
                CurrentSquadronSize = 5,
            };
            Officer admiral = EntityFactory.CreateOfficer("admiral", empire.InstanceID);
            admiral.CurrentRank = OfficerRank.Admiral;
            admiral.Ratings[SkillRating.Leadership] = 20;
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.AttachNode(fighter, ship);
            game.AttachNode(admiral, ship);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(45, assessment.GetFleetBombardmentStrength(fleet));
        }

        [Test]
        public void IsFleetReadyToAttack_ShieldedTarget_RequiresShieldPenetration()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 0;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 0;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AddShield(game, target, "shield-1", rebels.InstanceID, 50);
            AddShield(game, target, "shield-2", rebels.InstanceID, 50);

            Fleet blockedFleet = CreateAssaultFleet(game, origin, "blocked", empire.InstanceID, 0);
            Fleet readyFleet = CreateAssaultFleet(game, origin, "ready", empire.InstanceID, 0);
            Starfighter bomber = new Starfighter
            {
                InstanceID = "bomber",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Bombardment = 1,
                MaxSquadronSize = 1,
                CurrentSquadronSize = 1,
            };
            game.AttachNode(bomber, readyFleet.GetChildren<CapitalShip>().Single());
            AITestSceneBuilder.RevealPlanet(game, empire, target);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.AreEqual(
                context.Assessment.GetBombardmentShieldStrength(target) + 1,
                context.GetAttackDemand(target).BombardmentStrength
            );
            Assert.Zero(context.Assessment.GetFleetBombardmentStrength(blockedFleet));
            Assert.AreEqual(1, context.Assessment.GetFleetBombardmentStrength(readyFleet));
        }

        [Test]
        public void IsFleetReadyToAttack_BombardmentCanRemoveDefenders_RequiresOccupationForce()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 6;
            game.Config.AI.Garrison.SupportThreshold = 60;
            game.Config.AI.Garrison.GarrisonDivisor = 10;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 0;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 0;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, 0);
            for (int index = 0; index < 3; index++)
            {
                game.AttachNode(
                    AITestSceneBuilder.CreateRegiment(
                        $"defender-{index}",
                        rebels.InstanceID,
                        defenseRating: 100
                    ),
                    target
                );
            }
            Fleet groundFleet = CreateAssaultFleet(game, origin, "ground", empire.InstanceID, 0);
            Fleet bombardmentFleet = CreateAssaultFleet(
                game,
                origin,
                "bombardment",
                empire.InstanceID,
                1
            );
            AITestSceneBuilder.RevealPlanet(game, empire, target);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.Greater(context.GetAttackDemand(target).RegimentCount, 1);
            Assert.AreEqual(6, context.GetAttackDemand(target).OccupationRegimentCount);
            Assert.Zero(context.Assessment.GetFleetBombardmentStrength(groundFleet));
            Assert.AreEqual(1, context.Assessment.GetFleetBombardmentStrength(bombardmentFleet));
        }

        [Test]
        public void IsFleetReadyToAttack_TransportOnlyFleet_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            Fleet fleet = EntityFactory.CreateFleet("transport", empire.InstanceID);
            CapitalShip transport = AITestSceneBuilder.CreateCapitalShip(
                "transport-ship",
                empire.InstanceID,
                combatStrength: 0
            );
            game.AttachNode(fleet, origin);
            game.AttachNode(transport, fleet);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("transport-regiment", empire.InstanceID),
                transport
            );

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.Zero(context.Assessment.GetReadyFleetCombatValue(fleet));
        }

        [Test]
        public void IsFleetReadyToAttack_SingleTroopFacingCertainDefenseFire_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 0;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 0;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.Combat.PlanetaryAssault.DefenseFireDivisor = 5;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            Building battery = AITestSceneBuilder.CreateBuildingTemplate(
                "battery",
                BuildingType.Weapon
            );
            battery.OwnerInstanceID = rebels.InstanceID;
            battery.DefenseWeaponEffect = DefenseWeaponEffect.ShieldDamage;
            battery.WeaponPower = 500;
            game.AttachNode(battery, target);
            Fleet fleet = CreateAssaultFleet(game, origin, "attacker", empire.InstanceID, 0);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.Less(
                context.Assessment.GetPlanetaryAssaultSuccessPercent(fleet, target),
                game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultSuccessPercent
            );
        }

        [Test]
        public void IsFleetReadyToAttack_ReserveTroopSurvivesCertainDefenseFire_ReturnsTrue()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 0;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 0;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.Combat.PlanetaryAssault.DefenseFireDivisor = 5;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            Building battery = AITestSceneBuilder.CreateBuildingTemplate(
                "battery",
                BuildingType.Weapon
            );
            battery.OwnerInstanceID = rebels.InstanceID;
            battery.DefenseWeaponEffect = DefenseWeaponEffect.ShieldDamage;
            battery.WeaponPower = 500;
            game.AttachNode(battery, target);
            Fleet fleet = CreateAssaultFleet(game, origin, "attacker", empire.InstanceID, 0);
            fleet.GetChildren<CapitalShip>().Single().RegimentCapacity = 2;
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("attacker-reserve", empire.InstanceID),
                fleet.GetChildren<CapitalShip>().Single()
            );

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.GreaterOrEqual(
                context.Assessment.GetPlanetaryAssaultSuccessPercent(fleet, target),
                game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultSuccessPercent
            );
        }

        [Test]
        public void GetRequiredAttackRegimentCount_BombardmentCanRemoveDefenders_RequiresStableOccupation()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 6;
            game.Config.AI.Garrison.SupportThreshold = 60;
            game.Config.AI.Garrison.GarrisonDivisor = 10;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, 20);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("defender", rebels.InstanceID),
                target
            );
            Fleet fleet = CreateAssaultFleet(game, origin, "attacker", empire.InstanceID, 4);
            AITestSceneBuilder.RevealPlanet(game, empire, target);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.AreEqual(4, context.GetAttackDemand(target).OccupationRegimentCount);
        }

        [Test]
        public void GetRequiredAttackRegimentCount_StabilityExceedsLandingCapacity_CapsOccupation()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 3;
            game.Config.AI.Garrison.SupportThreshold = 100;
            game.Config.AI.Garrison.GarrisonDivisor = 1;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, 0);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("defender", rebels.InstanceID),
                target
            );
            Fleet fleet = CreateAssaultFleet(game, origin, "attacker", empire.InstanceID, 3);
            AITestSceneBuilder.RevealPlanet(game, empire, target);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.AreEqual(3, context.GetAttackDemand(target).OccupationRegimentCount);
        }

        [Test]
        public void GetRequiredAttackRegimentCount_WithoutDefenders_RequiresStableOccupation()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 6;
            game.Config.AI.Garrison.SupportThreshold = 60;
            game.Config.AI.Garrison.GarrisonDivisor = 10;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, 20);
            Fleet fleet = CreateAssaultFleet(game, origin, "attacker", empire.InstanceID, 4);
            AITestSceneBuilder.RevealPlanet(game, empire, target);

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.AreEqual(4, context.GetAttackDemand(target).RegimentCount);
        }

        [Test]
        public void GetDefendingRegimentDefenseStrength_WithDifferentBombardmentDefense_ReturnsEqualStrength()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet lowBombardmentDefense = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "low",
                rebels.InstanceID
            );
            Planet highBombardmentDefense = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "high",
                rebels.InstanceID
            );
            Regiment low = AITestSceneBuilder.CreateRegiment("low-regiment", rebels.InstanceID);
            Regiment high = AITestSceneBuilder.CreateRegiment("high-regiment", rebels.InstanceID);
            low.BombardmentDefense = 0;
            high.BombardmentDefense = 1000;
            game.AttachNode(low, lowBombardmentDefense);
            game.AttachNode(high, highBombardmentDefense);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(
                assessment.GetDefendingRegimentDefenseStrength(lowBombardmentDefense),
                assessment.GetDefendingRegimentDefenseStrength(highBombardmentDefense)
            );
        }

        [Test]
        public void GetRequiredAttackRegimentCount_WithDefendersAndLowSupport_AddsOccupationForce()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 2;
            game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 6;
            game.Config.AI.Garrison.SupportThreshold = 60;
            game.Config.AI.Garrison.GarrisonDivisor = 10;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, 0);

            for (int index = 0; index < 3; index++)
            {
                Regiment defender = AITestSceneBuilder.CreateRegiment(
                    $"defender-{index}",
                    rebels.InstanceID
                );
                game.AttachNode(defender, enemy);
            }

            Fleet attackerFleet = EntityFactory.CreateFleet("attacker", empire.InstanceID);
            CapitalShip attackerShip = AITestSceneBuilder.CreateCapitalShip(
                "attacker-ship",
                empire.InstanceID
            );
            game.AttachNode(attackerFleet, enemy);
            game.AttachNode(attackerShip, attackerFleet);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("attacker-regiment", empire.InstanceID),
                attackerShip
            );

            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            Assert.AreEqual(10, context.GetAttackDemand(enemy).RegimentCount);
        }

        [Test]
        public void GetRequiredAttackPackage_WithMultipleEnemyPlanets_UsesEachTargetsFleet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 6;
            game.Config.AI.Garrison.SupportThreshold = 60;
            game.Config.AI.Garrison.GarrisonDivisor = 10;
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet firstEnemy = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "enemy-1",
                rebels.InstanceID
            );
            Planet secondEnemy = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "enemy-2",
                rebels.InstanceID
            );
            firstEnemy.SetPopularSupport(empire.InstanceID, 0);
            secondEnemy.SetPopularSupport(empire.InstanceID, 0);
            Fleet firstDefenseFleet = EntityFactory.CreateFleet("defense-1", rebels.InstanceID);
            Fleet secondDefenseFleet = EntityFactory.CreateFleet("defense-2", rebels.InstanceID);
            game.AttachNode(firstDefenseFleet, firstEnemy);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "defense-ship-1",
                    rebels.InstanceID,
                    combatStrength: 200
                ),
                firstDefenseFleet
            );
            game.AttachNode(secondDefenseFleet, secondEnemy);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "defense-ship-2",
                    rebels.InstanceID,
                    combatStrength: 300
                ),
                secondDefenseFleet
            );
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment(
                    "defender-1",
                    rebels.InstanceID,
                    defenseRating: 10
                ),
                firstEnemy
            );
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment(
                    "defender-2",
                    rebels.InstanceID,
                    defenseRating: 10
                ),
                firstEnemy
            );
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment(
                    "defender-3",
                    rebels.InstanceID,
                    defenseRating: 20
                ),
                secondEnemy
            );
            AddShield(game, firstEnemy, "shield-1", rebels.InstanceID, 5);
            AddShield(game, firstEnemy, "shield-2", rebels.InstanceID, 5);
            AITestSceneBuilder.RevealPlanet(game, empire, firstEnemy);
            AITestSceneBuilder.RevealPlanet(game, empire, secondEnemy);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIAssessment assessment = context.Assessment;
            Planet firstTarget = assessment.GetKnownPlanet(firstEnemy.InstanceID);
            Planet secondTarget = assessment.GetKnownPlanet(secondEnemy.InstanceID);

            Assert.AreEqual(200, context.GetAttackDemand(firstTarget).CombatStrength);
            Assert.AreEqual(9, context.GetAttackDemand(firstTarget).RegimentCount);
            Assert.AreEqual(20, context.GetAttackDemand(firstTarget).RegimentStrength);
            Assert.AreEqual(11, context.GetAttackDemand(firstTarget).BombardmentStrength);
            Assert.AreEqual(300, context.GetAttackDemand(secondTarget).CombatStrength);
            Assert.AreEqual(8, context.GetAttackDemand(secondTarget).RegimentCount);
            Assert.AreEqual(20, context.GetAttackDemand(secondTarget).RegimentStrength);
            Assert.AreEqual(0, context.GetAttackDemand(secondTarget).BombardmentStrength);
        }

        [Test]
        public void GetSabotageTargetPriorityBonus_MixedTargets_UsesTacticalPriorityOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Building shield = AddShield(game, target, "shield", rebels.InstanceID, 5);
            Building battery = AITestSceneBuilder.CreateBuildingTemplate(
                "battery",
                BuildingType.Weapon
            );
            battery.OwnerInstanceID = rebels.InstanceID;
            game.AttachNode(battery, target);
            Regiment regiment = AITestSceneBuilder.CreateRegiment("regiment", rebels.InstanceID);
            game.AttachNode(regiment, target);
            Starfighter starfighter = AITestSceneBuilder.CreateStarfighter(
                "starfighter",
                rebels.InstanceID
            );
            game.AttachNode(starfighter, target);
            Building shipyard = AITestSceneBuilder.AddProductionFacility(
                game,
                target,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            double shieldPriority = AIMissionProposalScorer.GetSabotageTargetValue(
                context,
                target,
                shield
            );
            double batteryPriority = AIMissionProposalScorer.GetSabotageTargetValue(
                context,
                target,
                battery
            );
            double regimentPriority = AIMissionProposalScorer.GetSabotageTargetValue(
                context,
                target,
                regiment
            );
            double starfighterPriority = AIMissionProposalScorer.GetSabotageTargetValue(
                context,
                target,
                starfighter
            );
            double infrastructurePriority = AIMissionProposalScorer.GetSabotageTargetValue(
                context,
                target,
                shipyard
            );

            Assert.Greater(shieldPriority, batteryPriority);
            Assert.Greater(regimentPriority, batteryPriority);
            Assert.Greater(batteryPriority, starfighterPriority);
            Assert.Greater(starfighterPriority, infrastructurePriority);
        }

        /// <summary>
        /// Creates assault fleet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="bombardment">The bombardment.</param>
        /// <returns>The created assault fleet.</returns>
        private static Fleet CreateAssaultFleet(
            GameRoot game,
            Planet planet,
            string instanceId,
            string ownerInstanceId,
            int bombardment
        )
        {
            Fleet fleet = EntityFactory.CreateFleet(instanceId, ownerInstanceId);
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                $"{instanceId}-ship",
                ownerInstanceId
            );
            ship.Bombardment = bombardment;
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment($"{instanceId}-regiment", ownerInstanceId),
                ship
            );
            return fleet;
        }

        /// <summary>
        /// Adds shield.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="shieldStrength">The shield strength.</param>
        /// <returns>The result of add shield.</returns>
        private static Building AddShield(
            GameRoot game,
            Planet planet,
            string instanceId,
            string ownerInstanceId,
            int shieldStrength
        )
        {
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                instanceId,
                BuildingType.Defense
            );
            shield.OwnerInstanceID = ownerInstanceId;
            shield.ShieldStrength = shieldStrength;
            game.AttachNode(shield, planet);
            return shield;
        }

        [Test]
        public void GetAttackTargetPlanet_EnemyAttackOrder_ReturnsTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                TargetPlanetId = enemy.InstanceID,
            };
            game.AttachNode(fleet, owned);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Planet target = assessment.GetAttackTargetPlanet(fleet);

            Assert.AreEqual(enemy.InstanceID, target.InstanceID);
            Assert.AreEqual(rebels.InstanceID, target.GetOwnerInstanceID());
        }

        [Test]
        public void GetAttackTargetPlanet_StaleEnemySnapshot_ReturnsKnownTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                TargetPlanetId = enemy.InstanceID,
            };
            game.AttachNode(fleet, owned);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            enemy.OwnerInstanceID = null;
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Planet target = assessment.GetAttackTargetPlanet(fleet);

            Assert.IsNotNull(target);
            Assert.AreEqual(rebels.InstanceID, target.GetOwnerInstanceID());
        }

        [Test]
        public void GetAttackTargetPlanet_NonEnemyAttackOrder_ReturnsNull()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Planet neutral = AITestSceneBuilder.AddPlanet(game, system, "neutral", null);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, owned);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                TargetPlanetId = enemy.InstanceID,
            };
            Assert.IsNull(assessment.GetAttackTargetPlanet(fleet));

            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                TargetPlanetId = owned.InstanceID,
            };
            Assert.IsNull(assessment.GetAttackTargetPlanet(fleet));

            fleet.Order.TargetPlanetId = neutral.InstanceID;
            Assert.IsNull(assessment.GetAttackTargetPlanet(fleet));

            fleet.Order.TargetPlanetId = "missing";
            Assert.IsNull(assessment.GetAttackTargetPlanet(fleet));
        }

        [Test]
        public void GetKnownIncomingHostileFleetStrength_StaleSnapshot_DoesNotPolluteFleetCombatCache()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "owned", empire.InstanceID);
            Fleet inboundFleet = EntityFactory.CreateFleet("inbound", rebels.InstanceID);
            inboundFleet.Movement = TestMovement.Create(transitTicks: 10);
            CapitalShip inboundShip = AITestSceneBuilder.CreateCapitalShip(
                "inbound-ship",
                rebels.InstanceID,
                combatStrength: 100
            );
            game.AttachNode(inboundFleet, planet);
            game.AttachNode(inboundShip, inboundFleet);
            AITestSceneBuilder.RevealPlanet(game, empire, planet);
            inboundShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser][0] = 300;

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(100, assessment.GetKnownIncomingHostileFleetStrength(planet));
            Assert.Greater(assessment.GetFleetCombatValue(inboundFleet), 100);
        }

        [Test]
        public void GetKnownIncomingHostileFleetStrength_MultipleSameOwnerFleets_ReturnsCombinedStrength()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "owned", empire.InstanceID);
            Fleet firstFleet = EntityFactory.CreateFleet("first-inbound", rebels.InstanceID);
            firstFleet.Movement = TestMovement.Create(transitTicks: 10);
            game.AttachNode(firstFleet, planet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "first-inbound-ship",
                    rebels.InstanceID,
                    combatStrength: 60
                ),
                firstFleet
            );
            Fleet secondFleet = EntityFactory.CreateFleet("second-inbound", rebels.InstanceID);
            secondFleet.Movement = TestMovement.Create(transitTicks: 20);
            game.AttachNode(secondFleet, planet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "second-inbound-ship",
                    rebels.InstanceID,
                    combatStrength: 70
                ),
                secondFleet
            );
            AITestSceneBuilder.RevealPlanet(game, empire, planet);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(130, assessment.GetKnownIncomingHostileFleetStrength(planet));
        }

        /// <summary>
        /// Creates co-located agents with interchangeable mission qualifications.
        /// </summary>
        /// <param name="agents">Receives the available agents in stable identity order.</param>
        /// <returns>The faction turn containing the complete team.</returns>
        private static AITurnContext CreateAgentTeamForecastContext(out SpecialForces[] agents)
        {
            AITurnContext context = CreateAgentForecastContext(
                out SpecialForces first,
                out SpecialForces second
            );
            Planet origin = first.GetParentOfType<Planet>();
            context.Game.MoveNode(second, origin);
            agents = new SpecialForces[] { first, second, null, null, null };
            for (int index = 2; index < agents.Length; index++)
            {
                SpecialForces agent = (SpecialForces)first.CreateCopy();
                agent.InstanceID = "team-agent-" + index;
                context.Game.AttachNode(agent, origin);
                agents[index] = agent;
            }
            return AITestSceneBuilder.CreateContext(context.Game, context.Faction);
        }

        /// <summary>
        /// Checks forecast reuse while preserving each projected agent's live movement.
        /// </summary>
        /// <param name="requireArrival">Whether the agents must already be traveling to the departure.</param>
        private static void AssertEquivalentProjectedAgentsShareForecast(bool requireArrival)
        {
            AITurnContext context = CreateAgentForecastContext(
                out SpecialForces first,
                out SpecialForces second
            );
            Planet departure = first.GetParentOfType<Planet>();
            if (requireArrival)
                context.Game.MoveNode(second, departure);
            first.Movement = TestMovement.Create(transitTicks: 13);
            second.Movement = TestMovement.Create(transitTicks: 8);
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            MissionContext request = new()
            {
                MissionTypeID = MissionTypeIDs.Espionage,
                Location = context.Assessment.GetKnownPlanet("target"),
                MainParticipants = new List<IMissionParticipant> { first },
            };
            MissionOdds firstOdds = requireArrival
                ? context.Assessment.GetMissionOddsAfterArrival(
                    context.MissionQueries,
                    request,
                    departure,
                    true
                )
                : context.Assessment.GetMissionOddsFromDeparture(
                    context.MissionQueries,
                    request,
                    departure,
                    true
                );
            request.MainParticipants[0] = second;

            MissionOdds secondOdds = requireArrival
                ? context.Assessment.GetMissionOddsAfterArrival(
                    context.MissionQueries,
                    request,
                    departure,
                    true
                )
                : context.Assessment.GetMissionOddsFromDeparture(
                    context.MissionQueries,
                    request,
                    departure,
                    true
                );

            Assert.That(firstOdds, Is.Not.Null);
            Assert.That(secondOdds, Is.SameAs(firstOdds));
            Assert.That(first.Movement, Is.Not.Null);
            Assert.That(second.Movement, Is.Not.Null);
        }

        /// <summary>
        /// Creates equivalent agents at two owned departure planets.
        /// </summary>
        /// <param name="first">Receives the first available agent.</param>
        /// <param name="second">Receives the second available agent.</param>
        /// <returns>The faction turn containing the observed objective and agents.</returns>
        private static AITurnContext CreateAgentForecastContext(
            out SpecialForces first,
            out SpecialForces second
        )
        {
            AITurnContext context = AITestSceneBuilder.CreateMissionDefenseContext(100);
            Planet origin = context.Game.GetSceneNodeByInstanceID<Planet>("origin");
            Planet secondOrigin = AITestSceneBuilder.AddPlanet(
                context.Game,
                origin.GetParentOfType<PlanetSector>(),
                "second-origin",
                context.Faction.InstanceID
            );
            first = AITestSceneBuilder.CreateSpecialForces(
                "agent",
                context.Faction.InstanceID,
                MissionTypeIDs.Espionage,
                MissionTypeIDs.Reconnaissance
            );
            first.InstanceID = "first-agent";
            first.Ratings[SkillRating.Espionage] = 50;
            second = (SpecialForces)first.CreateCopy();
            second.InstanceID = "second-agent";
            context.Game.AttachNode(first, origin);
            context.Game.AttachNode(second, secondOrigin);
            return AITestSceneBuilder.CreateContext(context.Game, context.Faction);
        }

        /// <summary>
        /// Arranges an unfinished defense and its session-owned priority.
        /// </summary>
        /// <param name="status">The manufacturing status to inspect.</param>
        /// <param name="order">Receives the queued defense.</param>
        /// <param name="producer">Receives the construction planet.</param>
        /// <param name="destination">Receives the full destination planet.</param>
        /// <returns>A faction turn containing the arranged manufacturing queue.</returns>
        private static AITurnContext CreateProductionQueueContext(
            ManufacturingStatus status,
            out Building order,
            out Planet producer,
            out Planet destination
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction enemy);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            producer = AITestSceneBuilder.AddPlanet(game, sector, "producer", faction.InstanceID);
            destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "destination",
                faction.InstanceID,
                energyCapacity: 1
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                producer,
                "yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            order = AITestSceneBuilder.CreateBuildingTemplate("shield", BuildingType.Defense);
            order.OwnerInstanceID = faction.InstanceID;
            order.ProducerPlanetID = producer.InstanceID;
            order.ManufacturingStatus = status;
            game.AttachNode(order, destination);
            producer.AddToManufacturingQueue(order);
            return AITestSceneBuilder.CreateContext(game, faction);
        }

        /// <summary>
        /// Invokes the requested fleet capability through its query contract.
        /// </summary>
        /// <param name="assessment">The current faction assessment.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="query">The capability query under test.</param>
        /// <returns>The measured capability.</returns>
        private static int MeasureFleetCapability(
            AIAssessment assessment,
            Fleet fleet,
            string query
        ) =>
            query switch
            {
                nameof(AIAssessment.GetReadyFleetCombatValueAgainstCapitalShips) =>
                    assessment.GetReadyFleetCombatValueAgainstCapitalShips(fleet),
                nameof(AIAssessment.GetProjectedFleetCombatValueAgainstCapitalShips) =>
                    assessment.GetProjectedFleetCombatValueAgainstCapitalShips(fleet),
                nameof(AIAssessment.GetReadyFleetRegimentCount) =>
                    assessment.GetReadyFleetRegimentCount(fleet),
                nameof(AIAssessment.GetReadyFleetRegimentCapacity) =>
                    assessment.GetReadyFleetRegimentCapacity(fleet),
                nameof(AIAssessment.GetReadyFleetRegimentAttackStrength) =>
                    assessment.GetReadyFleetRegimentAttackStrength(fleet),
                nameof(AIAssessment.GetProjectedFleetRegimentAttackStrength) =>
                    assessment.GetProjectedFleetRegimentAttackStrength(fleet),
                _ => throw new ArgumentOutOfRangeException(nameof(query)),
            };
    }
}
