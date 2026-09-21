using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;

/// <summary>Verifies get mission odds default combines known detectors and assigned decoys.</summary>
namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public sealed class MissionQueriesTests
    {
        /// <summary>Verifies that an absent context cannot create a preview mission.</summary>
        [Test]
        public void TryCreateMission_NullContext_ReturnsFalseAndNoMission()
        {
            MissionQueries queries = new MissionQueries(new GameRoot(TestConfig.Create()));

            bool created = queries.TryCreateMission(null, out Mission mission);

            Assert.IsFalse(created);
            Assert.IsNull(mission);
        }

        /// <summary>Verifies that objective odds require a mission to evaluate.</summary>
        [Test]
        public void GetObjectiveSuccessProbability_NullMission_ThrowsArgumentNullException()
        {
            MissionQueries queries = new MissionQueries(new GameRoot(TestConfig.Create()));

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                queries.GetObjectiveSuccessProbability(null, Array.Empty<IMissionParticipant>())
            );

            Assert.AreEqual("mission", exception.ParamName);
        }

        [Test]
        public void GetMissionOdds_Default_CombinesKnownDetectorsAndAssignedDecoys()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _) = BuildDetectionScene();
            Regiment secondDetector = CreateCompletedRegiment("r2", "rebels");
            game.AttachNode(secondDetector, planet);
            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            game.AttachNode(decoy, spy.GetParent());
            planet.AddVisitor("empire");
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 50 } });
            SetDecoyTable(game, new Dictionary<int, int> { { -1000, 50 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 0 } });
            MissionQueries system = new MissionQueries(game);

            MissionOdds odds = system.GetMissionOdds(
                CreateContext(
                    EspionageMission.MissionTypeID,
                    new List<IMissionParticipant> { spy },
                    new List<IMissionParticipant> { decoy },
                    planet
                )
            );

            Assert.IsNotNull(odds);
            Assert.AreEqual(50, odds.FoilProbability, 0.001);
        }

        /// <summary>
        /// Verifies that an explicitly empty observation does not reveal live planetary detectors.
        /// </summary>
        [Test]
        public void GetMissionOdds_EmptyObservedDetectors_DoesNotUseLiveDetectors()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _) = BuildDetectionScene();
            planet.AddVisitor("empire");
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            MissionQueries system = new MissionQueries(game);
            MissionContext context = CreateContext(EspionageMission.MissionTypeID, spy, planet);

            MissionOdds observed = system.GetMissionOdds(context, Array.Empty<ISceneNode>());
            MissionOdds live = system.GetMissionOdds(context);

            Assert.IsNotNull(observed);
            Assert.IsNotNull(live);
            Assert.AreEqual(0, observed.FoilProbability);
            Assert.AreEqual(100, live.FoilProbability);
        }

        /// <summary>
        /// Verifies that previewing a valid mission leaves its participants and graph unchanged.
        /// </summary>
        [Test]
        public void GetMissionOdds_ValidMission_DoesNotStartMission()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _) = BuildDetectionScene();
            planet.AddVisitor("empire");
            ISceneNode origin = spy.GetParent();
            string returnParent = spy.MissionReturnParentInstanceID;
            string returnLocation = spy.MissionReturnLocationInstanceID;
            MissionQueries system = new MissionQueries(game);

            MissionOdds odds = system.GetMissionOdds(
                CreateContext(EspionageMission.MissionTypeID, spy, planet)
            );

            Assert.IsNotNull(odds);
            Assert.IsEmpty(game.GetSceneNodesByType<Mission>());
            Assert.AreSame(origin, spy.GetParent());
            Assert.IsNull(spy.Movement);
            Assert.AreEqual(returnParent, spy.MissionReturnParentInstanceID);
            Assert.AreEqual(returnLocation, spy.MissionReturnLocationInstanceID);
        }

        /// <summary>Verifies get mission odds default tracks which decoy survives each detector.</summary>
        [Test]
        public void GetMissionOdds_Default_TracksWhichDecoySurvivesEachDetector()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _) = BuildDetectionScene();
            Regiment secondDetector = CreateCompletedRegiment("r2", "rebels");
            secondDetector.DetectionRating = 100;
            game.AttachNode(secondDetector, planet);
            Officer weakDecoy = EntityFactory.CreateOfficer("weak-decoy-1", "empire");
            Officer secondWeakDecoy = EntityFactory.CreateOfficer("weak-decoy-2", "empire");
            Officer strongDecoy = EntityFactory.CreateOfficer("strong-decoy", "empire");
            weakDecoy.SetBaseRating(SkillRating.Espionage, 0);
            secondWeakDecoy.SetBaseRating(SkillRating.Espionage, 0);
            strongDecoy.SetBaseRating(SkillRating.Espionage, 200);
            game.AttachNode(weakDecoy, spy.GetParent());
            game.AttachNode(secondWeakDecoy, spy.GetParent());
            game.AttachNode(strongDecoy, spy.GetParent());
            planet.AddVisitor("empire");
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 50 } });
            SetDecoyTable(game, new Dictionary<int, int> { { -50, 0 }, { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 0 } });
            MissionQueries system = new MissionQueries(game);

            MissionOdds odds = system.GetMissionOdds(
                CreateContext(
                    EspionageMission.MissionTypeID,
                    new List<IMissionParticipant> { spy },
                    new List<IMissionParticipant> { weakDecoy, secondWeakDecoy, strongDecoy },
                    planet
                )
            );

            Assert.IsNotNull(odds);
            Assert.AreEqual(52.777, odds.FoilProbability, 0.001);
        }

        /// <summary>Verifies get mission odds default includes stationary fleet detectors.</summary>
        [Test]
        public void GetMissionOdds_Default_IncludesStationaryFleetDetectors()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _) = BuildDetectionScene();
            game.DeleteNode(planet.GetChildren<Regiment>().Single());
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "rebels" };
            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "rebels",
                DetectionRating = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(capitalShip, fleet);
            planet.AddVisitor("empire");
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            MissionQueries system = new MissionQueries(game);

            MissionOdds odds = system.GetMissionOdds(
                CreateContext(EspionageMission.MissionTypeID, spy, planet)
            );

            Assert.IsNotNull(odds);
            Assert.AreEqual(100, odds.FoilProbability, 0.001);
        }

        /// <summary>Verifies get mission odds with multiple officers combines personnel loss probability.</summary>
        [Test]
        public void GetMissionOdds_WithMultipleOfficers_CombinesPersonnelLossProbability()
        {
            (GameRoot game, Planet planet, Officer firstOfficer, Officer _) = BuildDetectionScene();
            Officer secondOfficer = EntityFactory.CreateOfficer("second-officer", "empire");
            game.AttachNode(secondOfficer, firstOfficer.GetParent());
            planet.AddVisitor("empire");
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 50 } });
            MissionQueries system = new MissionQueries(game);

            MissionOdds odds = system.GetMissionOdds(
                CreateContext(
                    EspionageMission.MissionTypeID,
                    new List<IMissionParticipant> { firstOfficer, secondOfficer },
                    new List<IMissionParticipant>(),
                    planet
                )
            );

            Assert.IsNotNull(odds);
            Assert.AreEqual(100, odds.FoilProbability, 0.001);
            Assert.AreEqual(75, odds.PersonnelLossProbability, 0.001);
        }

        /// <summary>Verifies get mission odds diplomacy uses observed planet support.</summary>
        [Test]
        public void GetMissionOdds_Diplomacy_UsesObservedPlanetSupport()
        {
            (GameRoot game, Planet _, Planet target, Officer diplomat, MissionQueries missions) =
                BuildMissionOddsScene("empire");
            target.PopularSupport["empire"] = 90;
            Planet observedPlanet = target.CreateCopy() as Planet;
            target.PopularSupport["empire"] = 10;
            game.Config.ProbabilityTables.Mission.Diplomacy = new Dictionary<int, int>
            {
                { -100, 10 },
                { 0, 50 },
                { 40, 90 },
            };

            MissionOdds odds = missions.GetMissionOdds(
                CreateContext(DiplomacyMission.MissionTypeID, diplomat, observedPlanet)
            );

            Assert.IsNotNull(odds);
            Assert.AreEqual(90, odds.ObjectiveSuccessProbability, 0.001);
        }

        /// <summary>Verifies get mission odds default does not expose hidden betrayal state.</summary>
        [Test]
        public void GetMissionOdds_Default_DoesNotExposeHiddenBetrayalState()
        {
            (GameRoot game, Planet _, Planet target, Officer diplomat, MissionQueries missions) =
                BuildMissionOddsScene("empire");
            game.Config.ProbabilityTables.Mission.Diplomacy = new Dictionary<int, int>
            {
                { -100, 50 },
            };

            diplomat.CanBetray = false;
            diplomat.Loyalty = 100;
            MissionOdds loyalOdds = missions.GetMissionOdds(
                CreateContext(DiplomacyMission.MissionTypeID, diplomat, target)
            );
            diplomat.CanBetray = true;
            diplomat.Loyalty = 0;
            MissionOdds betrayalOdds = missions.GetMissionOdds(
                CreateContext(DiplomacyMission.MissionTypeID, diplomat, target)
            );

            Assert.IsNotNull(loyalOdds);
            Assert.IsNotNull(betrayalOdds);
            Assert.AreEqual(
                loyalOdds.OverallSuccessProbability,
                betrayalOdds.OverallSuccessProbability,
                0.001
            );
            Assert.AreEqual(loyalOdds.FoilProbability, betrayalOdds.FoilProbability, 0.001);
        }

        /// <summary>Verifies get mission odds reconnaissance uses its guaranteed completion rule.</summary>
        [Test]
        public void GetMissionOdds_Reconnaissance_UsesItsGuaranteedCompletionRule()
        {
            (GameRoot game, Planet origin, Planet target, Officer _, MissionQueries missions) =
                BuildMissionOddsScene("rebels");
            target.VisitingFactionIDs.Clear();
            SpecialForces probe = new SpecialForces
            {
                InstanceID = "probe",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new List<string> { ReconnaissanceMission.MissionTypeID },
            };
            game.AttachNode(probe, origin);
            game.Config.ProbabilityTables.Mission.DefaultSuccessProbability = 0;

            MissionOdds odds = missions.GetMissionOdds(
                CreateContext(ReconnaissanceMission.MissionTypeID, probe, target)
            );

            Assert.IsNotNull(odds);
            Assert.AreEqual(100, odds.ObjectiveSuccessProbability, 0.001);
            Assert.AreEqual(100, odds.OverallSuccessProbability, 0.001);
        }

        /// <summary>Verifies get mission odds officer target mission uses observed target rating.</summary>
        /// <param name="missionTypeId">The mission type id.</param>
        /// <param name="expectedProbability">The expected probability.</param>
        [TestCase(AbductionMission.MissionTypeID, 80)]
        [TestCase(AssassinationMission.MissionTypeID, 20)]
        public void GetMissionOdds_OfficerTargetMission_UsesObservedTargetRating(
            string missionTypeId,
            double expectedProbability
        )
        {
            (
                GameRoot game,
                Planet _,
                Planet targetPlanet,
                Officer attacker,
                MissionQueries missions
            ) = BuildMissionOddsScene("rebels");
            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.IsMain = false;
            target.SetBaseRating(SkillRating.Combat, 0);
            game.AttachNode(target, targetPlanet);
            Planet observedPlanet = targetPlanet.CreateCopy(recursive: true) as Planet;
            Officer observedTarget = observedPlanet.GetChildren<Officer>().Single();
            target.SetBaseRating(SkillRating.Combat, 100);
            game.Config.ProbabilityTables.Mission.Abduction = new Dictionary<int, int>
            {
                { -100, 10 },
                { 0, 80 },
            };
            game.Config.ProbabilityTables.Mission.Assassination = new Dictionary<int, int>
            {
                { -100, 10 },
                { 0, 80 },
            };
            game.Config.Assassination.KillProbability = 25;

            MissionOdds odds = missions.GetMissionOdds(
                CreateContext(
                    missionTypeId,
                    attacker,
                    observedPlanet,
                    targetOfficer: observedTarget
                )
            );

            Assert.IsNotNull(odds);
            Assert.AreEqual(expectedProbability, odds.ObjectiveSuccessProbability, 0.001);
        }

        /// <summary>Verifies get mission odds assassination of main character cannot report success.</summary>
        [Test]
        public void GetMissionOdds_AssassinationOfMainCharacter_CannotReportSuccess()
        {
            (
                GameRoot game,
                Planet _,
                Planet targetPlanet,
                Officer attacker,
                MissionQueries missions
            ) = BuildMissionOddsScene("rebels");
            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.IsMain = true;
            target.SetBaseRating(SkillRating.Combat, 0);
            game.AttachNode(target, targetPlanet);
            game.Config.ProbabilityTables.Mission.Assassination = new Dictionary<int, int>
            {
                { -100, 100 },
            };
            game.Config.Assassination.KillProbability = 100;

            MissionOdds odds = missions.GetMissionOdds(
                CreateContext(AssassinationMission.MissionTypeID, attacker, targetPlanet, target)
            );

            Assert.IsNotNull(odds);
            Assert.AreEqual(0, odds.ObjectiveSuccessProbability, 0.001);
            Assert.AreEqual(0, odds.OverallSuccessProbability, 0.001);
        }

        /// <summary>Verifies get mission odds assassination combines hit and kill checks per participant.</summary>
        [Test]
        public void GetMissionOdds_Assassination_CombinesHitAndKillChecksPerParticipant()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer firstAttacker,
                MissionQueries missions
            ) = BuildMissionOddsScene("rebels");
            Officer secondAttacker = EntityFactory.CreateOfficer("second-attacker", "empire");
            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            target.IsMain = false;
            game.AttachNode(secondAttacker, origin);
            game.AttachNode(target, targetPlanet);
            game.Config.ProbabilityTables.Mission.Assassination = new Dictionary<int, int>
            {
                { -100, 50 },
            };
            game.Config.Assassination.KillProbability = 50;

            MissionOdds odds = missions.GetMissionOdds(
                CreateContext(
                    AssassinationMission.MissionTypeID,
                    new List<IMissionParticipant> { firstAttacker, secondAttacker },
                    new List<IMissionParticipant>(),
                    targetPlanet,
                    target
                )
            );

            Assert.IsNotNull(odds);
            Assert.AreEqual(43.75, odds.ObjectiveSuccessProbability, 0.001);
            Assert.AreEqual(43.75, odds.OverallSuccessProbability, 0.001);
        }

        /// <summary>Verifies get available mission options own planet research returns research options.</summary>
        [Test]
        public void GetAvailableMissionOptions_OwnPlanetResearch_ReturnsResearchOptions()
        {
            (GameRoot game, Planet planet, Officer officer) = BuildScene(factionOwnsPlanet: true);
            officer.ShipResearch = 1;
            officer.TroopResearch = 1;
            officer.FacilityResearch = 1;
            AddResearchFacilities(game, planet);
            MissionQueries missions = new MissionQueries(game);

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateContext(null, officer, planet)
            );

            MissionOption[] researchOptions = options
                .Where(option => option.MissionTypeID == ResearchMission.MissionTypeID)
                .ToArray();
            Assert.AreEqual(3, researchOptions.Length);
            CollectionAssert.AreEqual(
                new[]
                {
                    ResearchDiscipline.ShipDesign,
                    ResearchDiscipline.TroopTraining,
                    ResearchDiscipline.FacilityDesign,
                },
                researchOptions.Select(option => option.Discipline).ToArray()
            );
        }

        /// <summary>Verifies get available mission options research with single matching rating returns matching research option.</summary>
        [Test]
        public void GetAvailableMissionOptions_ResearchWithSingleMatchingRating_ReturnsMatchingResearchOption()
        {
            (GameRoot game, Planet planet, Officer officer) = BuildScene(factionOwnsPlanet: true);
            officer.ShipResearch = 1;
            AddResearchFacilities(game, planet);
            MissionQueries missions = new MissionQueries(game);

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateContext(null, officer, planet)
            );

            MissionOption[] researchOptions = options
                .Where(option => option.MissionTypeID == ResearchMission.MissionTypeID)
                .ToArray();
            Assert.AreEqual(1, researchOptions.Length);
            Assert.AreEqual(ResearchDiscipline.ShipDesign, researchOptions.Single().Discipline);
        }

        /// <summary>Verifies get available mission options exhausted research excludes research option.</summary>
        [Test]
        public void GetAvailableMissionOptions_ExhaustedResearch_ExcludesResearchOption()
        {
            (GameRoot game, Planet planet, Officer officer) = BuildScene(factionOwnsPlanet: true);
            officer.ShipResearch = 1;
            AddResearchFacilities(game, planet);
            game.GetFactions().Single().ResearchCatalog[ResearchDiscipline.ShipDesign] =
                new List<ResearchCatalogEntry>();
            MissionQueries missions = new MissionQueries(game);

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateContext(null, officer, planet)
            );

            Assert.IsFalse(
                options.Any(option =>
                    option.MissionTypeID == ResearchMission.MissionTypeID
                    && option.Discipline == ResearchDiscipline.ShipDesign
                )
            );
        }

        /// <summary>Verifies get available mission options troop training without facility excludes research option.</summary>
        [Test]
        public void GetAvailableMissionOptions_TroopTrainingWithoutFacility_ExcludesResearchOption()
        {
            (GameRoot game, Planet planet, Officer officer) = BuildScene(factionOwnsPlanet: true);
            officer.TroopResearch = 1;
            MissionQueries missions = new MissionQueries(game);

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateContext(null, officer, planet)
            );

            Assert.IsFalse(
                options.Any(option => option.MissionTypeID == ResearchMission.MissionTypeID)
            );
        }

        /// <summary>Verifies get available mission options research without matching rating excludes research options.</summary>
        [Test]
        public void GetAvailableMissionOptions_ResearchWithoutMatchingRating_ExcludesResearchOptions()
        {
            (GameRoot game, Planet planet, Officer officer) = BuildScene(factionOwnsPlanet: true);
            MissionQueries missions = new MissionQueries(game);

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateContext(null, officer, planet)
            );

            Assert.IsFalse(
                options.Any(option => option.MissionTypeID == ResearchMission.MissionTypeID)
            );
        }

        /// <summary>Verifies get available mission options disallowed research excludes research options.</summary>
        [Test]
        public void GetAvailableMissionOptions_DisallowedResearch_ExcludesResearchOptions()
        {
            (GameRoot game, Planet planet, Officer officer) = BuildScene(factionOwnsPlanet: true);
            officer.ShipResearch = 1;
            officer.TroopResearch = 1;
            officer.FacilityResearch = 1;
            AddResearchFacilities(game, planet);
            game.GetFactions().Single().DisallowedMissionTypeIDs.Add(ResearchMission.MissionTypeID);
            MissionQueries missions = new MissionQueries(game);

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateContext(null, officer, planet)
            );

            Assert.IsFalse(
                options.Any(option => option.MissionTypeID == ResearchMission.MissionTypeID)
            );
        }

        /// <summary>Verifies get available mission options enemy planet recruitment excludes recruitment option.</summary>
        [Test]
        public void GetAvailableMissionOptions_EnemyPlanetRecruitment_ExcludesRecruitmentOption()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionQueries missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            participant.IsMain = true;
            game.GetUnrecruitedOfficers()
                .Add(
                    new Officer
                    {
                        InstanceID = "unrecruited",
                        RecruitingFactionInstanceIDs = new List<string> { "empire" },
                    }
                );

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateContext(null, participant, targetPlanet)
            );

            Assert.IsFalse(
                options.Any(option => option.MissionTypeID == RecruitmentMission.MissionTypeID)
            );
        }

        /// <summary>Verifies get available mission options planet only sabotage target excludes sabotage option.</summary>
        [Test]
        public void GetAvailableMissionOptions_PlanetOnlySabotageTarget_ExcludesSabotageOption()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionQueries missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Regiment regiment = CreateCompletedRegiment("r1", "rebels");
            game.AttachNode(regiment, targetPlanet);

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateContext(null, participant, targetPlanet)
            );

            Assert.IsFalse(
                options.Any(option => option.MissionTypeID == SabotageMission.MissionTypeID)
            );
        }

        /// <summary>Verifies get available mission options manufacturable sabotage target returns sabotage option.</summary>
        [Test]
        public void GetAvailableMissionOptions_ManufacturableSabotageTarget_ReturnsSabotageOption()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionQueries missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Regiment regiment = CreateCompletedRegiment("r1", "rebels");
            game.AttachNode(regiment, targetPlanet);

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateContext(null, participant, targetPlanet, selectedTarget: regiment)
            );

            Assert.IsTrue(
                options.Any(option => option.MissionTypeID == SabotageMission.MissionTypeID)
            );
        }

        /// <summary>Verifies get available mission options selected trainer without student excludes jedi training option.</summary>
        [Test]
        public void GetAvailableMissionOptions_SelectedTrainerWithoutStudent_ExcludesJediTrainingOption()
        {
            (GameRoot game, Planet planet, Officer officer) = BuildScene(factionOwnsPlanet: true);
            officer.IsForceSensitive = true;
            officer.IsJediTrainer = true;
            officer.IsForceEligible = true;
            officer.ForceValue = 120;
            MissionQueries missions = new MissionQueries(game);

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateContext(null, officer, planet)
            );

            Assert.IsFalse(
                options.Any(option => option.MissionTypeID == JediTrainingMission.MissionTypeID)
            );
        }

        /// <summary>Verifies get available mission options reconnaissance special forces returns reconnaissance option.</summary>
        [Test]
        public void GetAvailableMissionOptions_ReconnaissanceSpecialForces_ReturnsReconnaissanceOption()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sector, game.Galaxy);

            Planet origin = new Planet
            {
                InstanceID = "origin",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            Planet target = new Planet
            {
                InstanceID = "target",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(origin, sector);
            game.AttachNode(target, sector);

            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new List<string> { ReconnaissanceMission.MissionTypeID },
            };
            game.AttachNode(specialForces, origin);

            MissionQueries missions = new MissionQueries(game);

            List<MissionOption> options = missions.GetAvailableMissionOptions(
                CreateContext(null, specialForces, target)
            );

            Assert.AreEqual(1, options.Count);
            Assert.AreEqual(ReconnaissanceMission.MissionTypeID, options.Single().MissionTypeID);
        }

        /// <summary>Verifies can create mission stale completed view target returns true.</summary>
        [Test]
        public void CanCreateMission_StaleCompletedViewTarget_ReturnsTrue()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionQueries missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Regiment liveRegiment = EntityFactory.CreateRegiment("regiment", "rebels");
            liveRegiment.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(liveRegiment, targetPlanet);

            Planet viewPlanet = new Planet { InstanceID = targetPlanet.InstanceID };
            Regiment viewRegiment = EntityFactory.CreateRegiment(liveRegiment.InstanceID, "rebels");
            viewRegiment.ManufacturingStatus = ManufacturingStatus.Complete;
            viewRegiment.SetParent(viewPlanet);

            bool canCreate = missions.CanCreateMission(
                CreateContext(
                    SabotageMission.MissionTypeID,
                    participant,
                    viewPlanet,
                    selectedTarget: viewRegiment
                )
            );

            Assert.IsTrue(canCreate);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        /// <summary>Verifies can create mission stale stationary officer view with live transit returns true.</summary>
        [Test]
        public void CanCreateMission_StaleStationaryOfficerViewWithLiveTransit_ReturnsTrue()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionQueries missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            Planet viewPlanet = new Planet { InstanceID = targetPlanet.InstanceID };
            Officer viewTarget = EntityFactory.CreateOfficer(target.InstanceID, "rebels");
            viewTarget.SetParent(viewPlanet);
            target.Movement = new MovementState();

            bool canCreate = missions.CanCreateMission(
                CreateContext(
                    AbductionMission.MissionTypeID,
                    participant,
                    viewPlanet,
                    selectedTarget: viewTarget
                )
            );

            Assert.IsTrue(canCreate);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        /// <summary>Verifies can create mission inactive officer returns false.</summary>
        [Test]
        public void CanCreateMission_InactiveOfficer_ReturnsFalse()
        {
            (
                GameRoot game,
                Planet origin,
                Planet targetPlanet,
                Officer participant,
                Officer target,
                MissionQueries missions
            ) = BuildOfficerTargetMissionScene(friendlyTarget: false, capturedTarget: false);
            participant.IsEnabled = false;

            bool canCreate = missions.CanCreateMission(
                CreateContext(
                    AbductionMission.MissionTypeID,
                    participant,
                    targetPlanet,
                    selectedTarget: target
                )
            );

            Assert.IsFalse(canCreate);
            Assert.AreEqual(0, game.GetSceneNodesByType<Mission>().Count);
        }

        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <param name="factionOwnsPlanet">Whether faction owns planet.</param>
        /// <returns>The constructed scene.</returns>
        private (GameRoot game, Planet planet, Officer officer) BuildScene(bool factionOwnsPlanet)
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction faction = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(faction);

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sector, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                TypeID = "home-planet",
                OwnerInstanceID = factionOwnsPlanet ? "empire" : null,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };
            game.AttachNode(planet, sector);

            Officer officer = new Officer
            {
                InstanceID = "o1",
                OwnerInstanceID = "empire",
                Movement = null,
                MissionReturnParentInstanceID = planet.InstanceID,
                MissionReturnLocationInstanceID = planet.InstanceID,
            };
            // Parent to planet so IsOnMission() = false and IsMovable() = true.
            game.AttachNode(officer, planet);

            return (game, planet, officer);
        }

        /// <summary>
        /// Sets foil table.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="table">The table.</param>
        private static void SetFoilTable(GameRoot game, Dictionary<int, int> table)
        {
            game.Config.ProbabilityTables.Mission.Foil = table;
        }

        /// <summary>
        /// Sets decoy table.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="table">The table.</param>
        private static void SetDecoyTable(GameRoot game, Dictionary<int, int> table)
        {
            game.Config.ProbabilityTables.Mission.PlanetaryDecoy = table;
            game.Config.ProbabilityTables.Mission.FleetDecoy = table;
        }

        /// <summary>
        /// Sets evasion table.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="table">The table.</param>
        private static void SetEvasionTable(GameRoot game, Dictionary<int, int> table)
        {
            game.Config.ProbabilityTables.Mission.Evasion = table;
        }

        /// <summary>
        /// Creates completed regiment.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="ownerInstanceID">The owner instance id.</param>
        /// <returns>The created completed regiment.</returns>
        private static Regiment CreateCompletedRegiment(string id, string ownerInstanceID)
        {
            return new Regiment
            {
                InstanceID = id,
                OwnerInstanceID = ownerInstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        /// <summary>
        /// Adds research facilities.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        private static void AddResearchFacilities(GameRoot game, Planet planet)
        {
            planet.EnergyCapacity = 10;
            game.AttachNode(
                new Building
                {
                    InstanceID = "shipyard",
                    OwnerInstanceID = planet.OwnerInstanceID,
                    ProductionType = ManufacturingType.Ship,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            game.AttachNode(
                new Building
                {
                    InstanceID = "training-facility",
                    OwnerInstanceID = planet.OwnerInstanceID,
                    ProductionType = ManufacturingType.Troop,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            game.AttachNode(
                new Building
                {
                    InstanceID = "construction",
                    OwnerInstanceID = planet.OwnerInstanceID,
                    ProductionType = ManufacturingType.Building,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
        }

        /// <summary>
        /// Creates request.
        /// </summary>
        /// <param name="missionTypeId">The mission type id.</param>
        /// <param name="participant">The participant.</param>
        /// <param name="target">The target.</param>
        /// <param name="targetOfficer">The target officer.</param>
        /// <param name="discipline">The discipline.</param>
        /// <param name="selectedTarget">The selected target.</param>
        /// <returns>The created request.</returns>
        private static MissionContext CreateContext(
            string missionTypeId,
            IMissionParticipant participant,
            ISceneNode target,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null,
            ISceneNode selectedTarget = null
        )
        {
            return CreateContext(
                missionTypeId,
                new List<IMissionParticipant> { participant },
                new List<IMissionParticipant>(),
                target,
                targetOfficer,
                discipline,
                selectedTarget
            );
        }

        /// <summary>
        /// Creates request.
        /// </summary>
        /// <param name="missionTypeId">The mission type id.</param>
        /// <param name="mainParticipants">The main participants.</param>
        /// <param name="decoyParticipants">The decoy participants.</param>
        /// <param name="target">The target.</param>
        /// <param name="targetOfficer">The target officer.</param>
        /// <param name="discipline">The discipline.</param>
        /// <param name="selectedTarget">The selected target.</param>
        /// <returns>The created request.</returns>
        private static MissionContext CreateContext(
            string missionTypeId,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            ISceneNode target,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null,
            ISceneNode selectedTarget = null
        )
        {
            return new MissionContext
            {
                MissionTypeID = missionTypeId,
                Location = target,
                Discipline = discipline,
                SelectedTarget = targetOfficer ?? selectedTarget,
                MainParticipants = mainParticipants,
                DecoyParticipants = decoyParticipants,
            };
        }

        /// <summary>
        /// Builds detection scene.
        /// </summary>
        /// <returns>The constructed detection scene.</returns>
        private (GameRoot game, Planet planet, Officer spy, Officer defender) BuildDetectionScene()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sector, game.Galaxy);

            Planet homePlanet = new Planet
            {
                InstanceID = "empire-home",
                TypeID = "empire-home",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = -100,
                PositionY = 0,
            };
            game.AttachNode(homePlanet, sector);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "rebels", 50 } },
            };
            game.AttachNode(planet, sector);

            Officer spy = EntityFactory.CreateOfficer("spy", "empire");
            spy.MissionReturnParentInstanceID = homePlanet.InstanceID;
            spy.MissionReturnLocationInstanceID = homePlanet.InstanceID;
            game.AttachNode(spy, homePlanet);
            Officer defender = EntityFactory.CreateOfficer("defender", "rebels");
            defender.CurrentRank = OfficerRank.General;
            game.AttachNode(defender, planet);

            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                OwnerInstanceID = "rebels",
                DetectionRating = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, planet);

            return (game, planet, spy, defender);
        }

        /// <summary>
        /// Builds mission odds scene.
        /// </summary>
        /// <param name="targetOwnerInstanceId">The target owner instance id.</param>
        /// <returns>The constructed mission odds scene.</returns>
        private (
            GameRoot game,
            Planet origin,
            Planet target,
            Officer participant,
            MissionQueries missions
        ) BuildMissionOddsScene(string targetOwnerInstanceId)
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            Planet origin = new Planet
            {
                InstanceID = "origin",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            Planet target = new Planet
            {
                InstanceID = "target-planet",
                OwnerInstanceID = targetOwnerInstanceId,
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { targetOwnerInstanceId, 50 } },
            };
            target.AddVisitor("empire");
            game.AttachNode(origin, sector);
            game.AttachNode(target, sector);
            Officer participant = EntityFactory.CreateOfficer("participant", "empire");
            game.AttachNode(participant, origin);
            MissionQueries missions = new MissionQueries(game);
            return (game, origin, target, participant, missions);
        }

        /// <summary>
        /// Builds officer target mission scene.
        /// </summary>
        /// <param name="friendlyTarget">Whether friendly target.</param>
        /// <param name="capturedTarget">Whether captured target.</param>
        /// <returns>The constructed officer target mission scene.</returns>
        private (
            GameRoot game,
            Planet origin,
            Planet targetPlanet,
            Officer participant,
            Officer target,
            MissionQueries missions
        ) BuildOfficerTargetMissionScene(bool friendlyTarget, bool capturedTarget)
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sector, game.Galaxy);

            Planet origin = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            Planet targetPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                EnergyCapacity = 5,
                PositionX = 100,
                PositionY = 0,
            };
            game.AttachNode(origin, sector);
            game.AttachNode(targetPlanet, sector);

            Officer participant = EntityFactory.CreateOfficer("participant", "empire");
            game.AttachNode(participant, origin);

            Officer target = EntityFactory.CreateOfficer(
                "target",
                friendlyTarget ? "empire" : "rebels"
            );
            target.IsCaptured = capturedTarget;
            target.CaptorInstanceID = capturedTarget ? "rebels" : null;
            game.AttachNode(target, targetPlanet);

            MissionQueries missions = new MissionQueries(game);
            return (game, origin, targetPlanet, participant, target, missions);
        }
    }
}
