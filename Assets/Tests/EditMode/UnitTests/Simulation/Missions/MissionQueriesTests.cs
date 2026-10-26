using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;

/// <summary>
/// Verifies get mission odds default combines known detectors and assigned decoys.
/// </summary>
namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public sealed class MissionQueriesTests
    {
        [TestCase(0)]
        [TestCase(2)]
        [TestCase(8)]
        public void GetMissionSuccessUpperBound_InboundTeam_NeverUnderstatesArrivalSuccess(
            int defenderCount
        )
        {
            (MissionQueries queries, MissionContext context) = CreateMixedFleetMission(
                defenderCount,
                2,
                false,
                1
            );
            context.MainParticipants[0].Movement = new MovementState { TransitTicks = 13 };
            Planet departure = context.MainParticipants[0].GetParentOfType<Planet>();
            MissionOdds exact = queries.GetMissionOddsAfterArrival(context, departure, null, false);

            double? upperBound = queries.GetMissionSuccessUpperBound(
                context,
                null,
                exact.OverallSuccessProbability,
                departure
            );

            Assert.IsNotNull(upperBound);
            Assert.GreaterOrEqual(upperBound.Value, exact.OverallSuccessProbability);
            Assert.IsNotNull(context.MainParticipants[0].Movement);
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(8)]
        public void GetMissionOddsAfterArrival_ExactSuccessFloor_PreservesQualifyingOdds(
            int defenderCount
        )
        {
            (MissionQueries queries, MissionContext context) = CreateMixedFleetMission(
                defenderCount,
                2,
                false,
                1
            );
            context.Game.Config.ProbabilityTables.Mission.Espionage = new Dictionary<int, int>
            {
                { -1000, 67 },
            };
            context.MainParticipants[0].Movement = new MovementState { TransitTicks = 13 };
            Planet departure = context.MainParticipants[0].GetParentOfType<Planet>();
            MissionOdds expected = queries.GetMissionOddsAfterArrival(
                context,
                departure,
                null,
                false
            );

            MissionOdds bounded = queries.GetMissionOddsAfterArrival(
                context,
                departure,
                null,
                false,
                expected.OverallSuccessProbability
            );

            Assert.IsNotNull(bounded);
            Assert.AreEqual(expected.OverallSuccessProbability, bounded.OverallSuccessProbability);
        }

        [Test]
        public void GetMissionOddsAfterArrival_InboundPersonnel_MatchesActualArrivalWithoutMovingUnits()
        {
            (MissionQueries queries, MissionContext context) = CreateMixedFleetMission(
                4,
                2,
                false,
                1
            );
            Planet departure = context.MainParticipants[0].GetParentOfType<Planet>();
            MovementState movement = new() { TransitTicks = 13 };
            context.MainParticipants[0].Movement = movement;

            MissionOdds projected = queries.GetMissionOddsAfterArrival(
                context,
                departure,
                null,
                true
            );

            Assert.IsNotNull(projected);
            Assert.AreSame(movement, context.MainParticipants[0].Movement);
            Assert.IsFalse(queries.CanCreateMission(context));
            context.MainParticipants[0].Movement = null;
            MissionOdds arrived = queries.GetMissionOdds(context);
            Assert.AreEqual(
                arrived.ObjectiveSuccessProbability,
                projected.ObjectiveSuccessProbability
            );
            Assert.AreEqual(arrived.FoilProbability, projected.FoilProbability);
            Assert.AreEqual(arrived.PersonnelLossProbability, projected.PersonnelLossProbability);
        }

        [Test]
        public void GetMissionOddsAfterArrival_ValidProjection_DoesNotConsumeGameplayIdentifiers()
        {
            (MissionQueries queries, MissionContext context) = CreateMixedFleetMission(
                4,
                2,
                false,
                1
            );
            Planet departure = context.MainParticipants[0].GetParentOfType<Planet>();
            context.MainParticipants[0].Movement = new MovementState { TransitTicks = 13 };
            try
            {
                BaseGameEntity.SetInstanceIdSeed(1234);
                string expected = new BaseGameEntity().InstanceID;
                BaseGameEntity.SetInstanceIdSeed(1234);

                queries.GetMissionOddsAfterArrival(context, departure, null, true);

                Assert.AreEqual(expected, new BaseGameEntity().InstanceID);
            }
            finally
            {
                BaseGameEntity.SetInstanceIdSeed(null);
            }
        }

        [Test]
        public void TryCreateMissionAfterArrival_TeammateHeadingElsewhere_ReturnsFalse()
        {
            (MissionQueries queries, MissionContext context) = CreateMixedFleetMission(
                4,
                2,
                false,
                1
            );
            Planet departure = context.MainParticipants[0].GetParentOfType<Planet>();
            Planet elsewhere = new()
            {
                InstanceID = "elsewhere",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            context.Game.AttachNode(elsewhere, departure.GetParent());
            context.Game.MoveNode(context.MainParticipants[0], elsewhere);
            context.MainParticipants[0].Movement = new MovementState { TransitTicks = 13 };

            Assert.IsFalse(queries.TryCreateMissionAfterArrival(context, departure, out _));
        }

        /// <summary>
        /// Verifies probability lookups are refreshed when configuration changes between evaluations.
        /// </summary>
        [Test]
        public void GetMissionOdds_ChangedProbabilityTable_MatchesFreshQueries()
        {
            (MissionQueries queries, MissionContext context) = CreateMixedFleetMission(
                16,
                4,
                true,
                1
            );
            queries.GetMissionOdds(context);
            context.Game.Config.ProbabilityTables.Mission.Foil.Clear();
            context.Game.Config.ProbabilityTables.Mission.Foil.Add(-1000, 75);
            context.Game.Config.ProbabilityTables.Mission.Evasion.Clear();
            context.Game.Config.ProbabilityTables.Mission.Evasion.Add(-1000, 25);

            MissionOdds expected = new MissionQueries(context.Game).GetMissionOdds(context);
            MissionOdds actual = queries.GetMissionOdds(context);

            Assert.AreEqual(expected.FoilProbability, actual.FoilProbability);
            Assert.AreEqual(expected.PersonnelLossProbability, actual.PersonnelLossProbability);
        }

        [Test]
        public void TryGetOperationalMissionOdds_ExactSuccessFloor_AcceptsEveryQualifyingTeam(
            [Values(1, 2, 4, 8, 16)] int defenderCount,
            [Values(0, 1, 2, 4)] int decoyCount
        )
        {
            (MissionQueries queries, MissionContext context) = CreateMixedFleetMission(
                defenderCount,
                decoyCount,
                false,
                1
            );
            context.Game.Config.ProbabilityTables.Mission.Espionage = new Dictionary<int, int>
            {
                { -1000, 67 },
            };
            MissionOdds exact = queries.GetOperationalMissionOdds(context);

            bool accepted = queries.TryGetOperationalMissionOdds(
                context,
                null,
                exact.OverallSuccessProbability,
                out MissionOdds bounded
            );

            Assert.IsTrue(accepted);
            Assert.AreEqual(exact.OverallSuccessProbability, bounded.OverallSuccessProbability);
        }

        [Test]
        public void GetMissionOdds_ValidRequest_DoesNotConsumeGameplayIdentifiers()
        {
            (MissionQueries queries, MissionContext context) = CreateMixedFleetMission(
                4,
                2,
                false,
                1
            );
            try
            {
                BaseGameEntity.SetInstanceIdSeed(1234);
                string expected = new BaseGameEntity().InstanceID;
                BaseGameEntity.SetInstanceIdSeed(1234);

                queries.GetMissionOdds(context);
                queries.GetMissionSuccessUpperBound(context, null, 0);
                string actual = new BaseGameEntity().InstanceID;

                Assert.AreEqual(expected, actual);
            }
            finally
            {
                BaseGameEntity.SetInstanceIdSeed(null);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        public void GetMissionSuccessUpperBound_DecoySubsets_NeverUnderstatesSuccess(int decoyCount)
        {
            (MissionQueries queries, MissionContext context) = CreateMixedFleetMission(
                16,
                4,
                true,
                1
            );
            context.Game.Config.ProbabilityTables.Mission.DefaultSuccessProbability = 100;
            context.Game.Config.ProbabilityTables.Mission.Espionage = new Dictionary<int, int>
            {
                { -1000, 100 },
            };
            double? bound = queries.GetMissionSuccessUpperBound(context, null, 0);
            context.DecoyParticipants = context.DecoyParticipants.Take(decoyCount).ToList();

            MissionOdds exact = queries.GetOperationalMissionOdds(context);

            Assert.IsTrue(bound.HasValue);
            Assert.GreaterOrEqual(bound.Value + 0.000000001, exact.OverallSuccessProbability);
        }

        [TestCase(0d)]
        [TestCase(10d)]
        [TestCase(35d)]
        [TestCase(100d)]
        [TestCase(null)]
        public void TryGetOperationalMissionOdds_SuccessFloor_ReturnsOnlyCompleteQualifyingOdds(
            double? configuredFloor
        )
        {
            (MissionQueries queries, MissionContext context) = CreateMixedFleetMission(
                16,
                4,
                false,
                1
            );
            context.Game.Config.ProbabilityTables.Mission.DefaultSuccessProbability = 100;
            context.Game.Config.ProbabilityTables.Mission.Espionage = new Dictionary<int, int>
            {
                { -1000, 100 },
            };
            MissionOdds complete = queries.GetOperationalMissionOdds(context);
            double floor = configuredFloor ?? complete.OverallSuccessProbability;

            bool accepted = queries.TryGetOperationalMissionOdds(
                context,
                null,
                floor,
                out MissionOdds bounded
            );

            Assert.AreEqual(complete.OverallSuccessProbability >= floor, accepted);
            if (accepted)
                Assert.AreEqual(complete.FoilProbability, bounded.FoilProbability, 0.000000001);
            else
                Assert.IsNull(bounded);
        }

        [TestCase(2, 0, false, 1, 57.749999999999993, 11.549999999999995)]
        [TestCase(2, 0, false, 2, 57.749999999999993, 20.789999999999988)]
        [TestCase(2, 0, true, 1, 57.749999999999993, 11.549999999999995)]
        [TestCase(2, 0, true, 2, 57.749999999999993, 20.789999999999988)]
        [TestCase(2, 1, false, 1, 51.800000000000004, 10.359999999999998)]
        [TestCase(2, 1, false, 2, 51.800000000000004, 18.647999999999996)]
        [TestCase(2, 1, true, 1, 51.800000000000004, 53.180000000000007)]
        [TestCase(2, 1, true, 2, 51.800000000000004, 57.324)]
        [TestCase(2, 2, false, 1, 44.379999999999995, 8.8759999999999977)]
        [TestCase(2, 2, false, 2, 44.379999999999995, 15.976799999999994)]
        [TestCase(2, 2, true, 1, 44.379999999999995, 58.388000000000019)]
        [TestCase(2, 2, true, 2, 44.379999999999995, 61.0984)]
        [TestCase(2, 4, false, 1, 31.6225, 6.3244999999999978)]
        [TestCase(2, 4, false, 2, 31.6225, 11.384099999999993)]
        [TestCase(2, 4, true, 1, 31.6225, 40.335552500000013)]
        [TestCase(2, 4, true, 2, 31.6225, 42.7729945)]
        [TestCase(4, 0, false, 1, 82.149374999999992, 28.752281249999996)]
        [TestCase(4, 0, false, 2, 82.149374999999992, 43.436482031249987)]
        [TestCase(4, 0, true, 1, 82.149374999999992, 28.752281249999996)]
        [TestCase(4, 0, true, 2, 82.149374999999992, 43.436482031249987)]
        [TestCase(4, 1, false, 1, 79.3975, 28.680224999999997)]
        [TestCase(4, 1, false, 2, 79.3975, 42.896065625)]
        [TestCase(4, 1, true, 1, 79.3975, 64.418512499999991)]
        [TestCase(4, 1, true, 2, 79.3975, 71.5096328125)]
        [TestCase(4, 2, false, 1, 73.678675, 27.287741250000003)]
        [TestCase(4, 2, false, 2, 73.678675, 40.57642990625)]
        [TestCase(4, 2, true, 1, 73.678675, 78.757555500000038)]
        [TestCase(4, 2, true, 2, 73.678675, 82.504423450000019)]
        [TestCase(4, 4, false, 1, 53.815338226562496, 18.901254935937498)]
        [TestCase(4, 4, false, 2, 53.815338226562481, 27.63065287718749)]
        [TestCase(4, 4, true, 1, 53.815338226562496, 73.279927553212474)]
        [TestCase(4, 4, true, 2, 53.815338226562481, 75.205216266123941)]
        [TestCase(8, 0, false, 1, 96.81355187109375, 41.145759545214851)]
        [TestCase(8, 0, false, 2, 96.81355187109375, 60.629486859272461)]
        [TestCase(8, 0, true, 1, 96.81355187109375, 41.145759545214851)]
        [TestCase(8, 0, true, 2, 96.81355187109375, 60.629486859272461)]
        [TestCase(8, 1, false, 1, 96.317921984375, 41.695197351171878)]
        [TestCase(8, 1, false, 2, 96.317921984375, 61.093676560917977)]
        [TestCase(8, 1, true, 1, 96.317921984375, 70.98541425758593)]
        [TestCase(8, 1, true, 2, 96.317921984375, 80.638606024258991)]
        [TestCase(8, 2, false, 1, 95.154456758593753, 41.965368158180475)]
        [TestCase(8, 2, false, 2, 95.154456758593753, 61.246867211650681)]
        [TestCase(8, 2, true, 1, 95.154456758593753, 84.6965395707463)]
        [TestCase(8, 2, true, 2, 95.154456758593753, 89.786228211865634)]
        [TestCase(8, 4, false, 1, 79.542675935162833, 34.270340451469956)]
        [TestCase(8, 4, false, 2, 79.542675935162819, 49.597118569753533)]
        [TestCase(8, 4, true, 1, 79.542675935162833, 94.668398062823229)]
        [TestCase(8, 4, true, 2, 79.542675935162819, 95.45826043451379)]
        public void GetMissionOdds_MixedFleetCommanders_PreservesEncounterProbabilities(
            int detectorCount,
            int decoyCount,
            bool officerDecoys,
            int mainCount,
            double expectedFoil,
            double expectedLoss
        )
        {
            (MissionQueries queries, MissionContext context) = CreateMixedFleetMission(
                detectorCount,
                decoyCount,
                officerDecoys,
                mainCount
            );

            MissionOdds odds = queries.GetMissionOdds(context);

            Assert.IsNotNull(odds);
            Assert.AreEqual(expectedFoil, odds.FoilProbability, 0.000000001);
            Assert.AreEqual(expectedLoss, odds.PersonnelLossProbability, 0.000000001);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GetOperationalMissionOdds_LargerMixedFleet_MatchesCompleteOdds(
            bool officerDecoys
        )
        {
            (MissionQueries queries, MissionContext context) = CreateMixedFleetMission(
                16,
                4,
                officerDecoys,
                2
            );

            MissionOdds complete = queries.GetMissionOdds(context);
            MissionOdds operational = queries.GetOperationalMissionOdds(context);

            Assert.AreEqual(complete.FoilProbability, operational.FoilProbability, 0.000000001);
        }

        [Test]
        public void TryCreateMission_NullContext_ReturnsFalseAndNoMission()
        {
            MissionQueries queries = new MissionQueries(TestGame.Create(TestConfig.Create()));

            bool created = queries.TryCreateMission(null, out Mission mission);

            Assert.IsFalse(created);
            Assert.IsNull(mission);
        }

        [Test]
        public void GetObjectiveSuccessProbability_NullMission_ThrowsArgumentNullException()
        {
            MissionQueries queries = new MissionQueries(TestGame.Create(TestConfig.Create()));

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

        [Test]
        public void GetMissionOdds_AIControlledFaction_AppliesFoilDifficultyModifier()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _) = BuildDetectionScene();
            planet.AddVisitor("empire");
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 50 } });
            game.Summary.Difficulty = GameDifficulty.Hard;
            game.Summary.PlayerFactionID = "rebels";
            game.Config.DifficultyModifiers[GameDifficulty.Hard] = new DifficultyModifiers
            {
                MissionFoilChancePoints = -15,
            };
            MissionQueries system = new MissionQueries(game);

            MissionOdds odds = system.GetMissionOdds(
                CreateContext(EspionageMission.MissionTypeID, spy, planet)
            );

            Assert.IsNotNull(odds);
            Assert.AreEqual(35, odds.FoilProbability, 0.001);
        }

        [Test]
        public void GetMissionOdds_HardAiDetector_AppliesFractionalDetectionRatingMultiplier()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender) = BuildDetectionScene();
            Regiment detector = planet.GetChildren<Regiment>().Single();
            spy.SetBaseRating(SkillRating.Espionage, 100);
            defender.SetBaseRating(SkillRating.Espionage, 0);
            detector.DetectionRating = 40;
            planet.AddVisitor("empire");
            game.Config.ProbabilityTables.Mission.FoilDefenderScalingPercent = 0;
            game.Config.ProbabilityTables.Mission.FoilFlatScoreAdjustment = 0;
            SetFoilTable(game, new Dictionary<int, int> { { 40, 100 }, { 41, 0 } });
            game.Summary.Difficulty = GameDifficulty.Hard;
            game.Config.DifficultyModifiers[GameDifficulty.Hard] = new DifficultyModifiers
            {
                DetectionRatingMultiplier = 1.5,
            };
            MissionQueries system = new MissionQueries(game);

            MissionOdds aiDetectorOdds = system.GetMissionOdds(
                CreateContext(EspionageMission.MissionTypeID, spy, planet)
            );
            game.SetFactionController("rebels", "human", PlayerControllerType.Human);
            MissionOdds humanDetectorOdds = system.GetMissionOdds(
                CreateContext(EspionageMission.MissionTypeID, spy, planet)
            );

            Assert.AreEqual(100, aiDetectorOdds.FoilProbability, 0.001);
            Assert.AreEqual(0, humanDetectorOdds.FoilProbability, 0.001);
        }

        [Test]
        public void GetDecoyProbability_HardAiDetector_AppliesFractionalDetectionRatingMultiplier()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender) = BuildDetectionScene();
            Regiment detector = planet.GetChildren<Regiment>().Single();
            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            decoy.SetBaseRating(SkillRating.Espionage, 100);
            game.AttachNode(decoy, spy.GetParent());
            defender.SetBaseRating(SkillRating.Espionage, 0);
            detector.DetectionRating = 40;
            planet.AddVisitor("empire");
            game.Config.ProbabilityTables.Mission.DecoyDefenderScalingPercent = 0;
            SetDecoyTable(game, new Dictionary<int, int> { { 40, 0 }, { 41, 100 } });
            game.Summary.Difficulty = GameDifficulty.Hard;
            game.Config.DifficultyModifiers[GameDifficulty.Hard] = new DifficultyModifiers
            {
                DetectionRatingMultiplier = 1.5,
            };
            MissionQueries queries = new MissionQueries(game);
            Assert.IsTrue(
                queries.TryCreateMission(
                    CreateContext(
                        EspionageMission.MissionTypeID,
                        new List<IMissionParticipant> { spy },
                        new List<IMissionParticipant> { decoy },
                        planet
                    ),
                    out Mission mission
                )
            );

            double aiDetectorProbability = queries.GetDecoyProbability(mission, decoy, detector);
            game.SetFactionController("rebels", "human", PlayerControllerType.Human);
            double humanDetectorProbability = queries.GetDecoyProbability(mission, decoy, detector);

            Assert.AreEqual(0, aiDetectorProbability, 0.0001);
            Assert.AreEqual(100, humanDetectorProbability, 0.0001);
        }

        [Test]
        public void GetFoilProbability_MaximumDetectionWithCommander_SaturatesNegativeScore()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender) = BuildDetectionScene();
            Regiment detector = planet.GetChildren<Regiment>().Single();
            planet.AddVisitor("empire");
            game.Config.ProbabilityTables.Mission.FoilDefenderScalingPercent = 100;
            SetFoilTable(game, new Dictionary<int, int> { { int.MinValue, 100 }, { 0, 0 } });
            MissionQueries queries = new MissionQueries(game);
            Assert.IsTrue(
                queries.TryCreateMission(
                    CreateContext(EspionageMission.MissionTypeID, spy, planet),
                    out Mission mission
                )
            );
            spy.SetBaseRating(SkillRating.Espionage, 0);
            defender.SetBaseRating(SkillRating.Espionage, 50);
            detector.DetectionRating = int.MaxValue;

            int probability = queries.GetFoilProbability(
                mission,
                detector,
                0,
                new[] { spy },
                planet
            );

            Assert.AreEqual(100, probability);
        }

        [Test]
        public void GetDecoyProbability_MaximumDetectionWithCommander_SaturatesNegativeScore()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender) = BuildDetectionScene();
            Regiment detector = planet.GetChildren<Regiment>().Single();
            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            game.AttachNode(decoy, spy.GetParent());
            planet.AddVisitor("empire");
            game.Config.ProbabilityTables.Mission.DecoyDefenderScalingPercent = 100;
            SetDecoyTable(game, new Dictionary<int, int> { { int.MinValue, 100 }, { 0, 0 } });
            MissionQueries queries = new MissionQueries(game);
            Assert.IsTrue(
                queries.TryCreateMission(
                    CreateContext(
                        EspionageMission.MissionTypeID,
                        new List<IMissionParticipant> { spy },
                        new List<IMissionParticipant> { decoy },
                        planet
                    ),
                    out Mission mission
                )
            );
            decoy.SetBaseRating(SkillRating.Espionage, 0);
            defender.SetBaseRating(SkillRating.Espionage, 50);
            detector.DetectionRating = int.MaxValue;

            int probability = queries.GetDecoyProbability(mission, decoy, detector, planet);

            Assert.AreEqual(100, probability);
        }

        [Test]
        public void GetMissionOdds_GroundDetector_OnlyChecksBeforeObjective()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _) = BuildDetectionScene();
            Regiment detector = planet.GetChildren<Regiment>().Single();
            planet.AddVisitor("empire");
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 50 } });
            MissionQueries system = new MissionQueries(game);

            MissionOdds odds = system.GetMissionOdds(
                CreateContext(EspionageMission.MissionTypeID, spy, planet),
                new List<ISceneNode> { detector }
            );

            Assert.IsNotNull(odds);
            Assert.AreEqual(50, odds.FoilProbability, 0.001);
        }

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
            Assert.AreEqual(100d * 19 / 36, odds.FoilProbability, 0.001);
        }

        [Test]
        public void GetMissionOdds_OmittedPersonnelLoss_MatchesCompleteSuccessAndFoilOdds()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _) = BuildDetectionScene();
            Regiment secondDetector = CreateCompletedRegiment("r2", "rebels");
            secondDetector.DetectionRating = 100;
            game.AttachNode(secondDetector, planet);
            Officer weakDecoy = EntityFactory.CreateOfficer("weak-decoy", "empire");
            Officer strongDecoy = EntityFactory.CreateOfficer("strong-decoy", "empire");
            weakDecoy.SetBaseRating(SkillRating.Espionage, 0);
            strongDecoy.SetBaseRating(SkillRating.Espionage, 200);
            game.AttachNode(weakDecoy, spy.GetParent());
            game.AttachNode(strongDecoy, spy.GetParent());
            planet.AddVisitor("empire");
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 50 } });
            SetDecoyTable(game, new Dictionary<int, int> { { -50, 0 }, { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 50 } });
            MissionQueries system = new MissionQueries(game);
            MissionContext context = CreateContext(
                EspionageMission.MissionTypeID,
                new List<IMissionParticipant> { spy },
                new List<IMissionParticipant> { weakDecoy, strongDecoy },
                planet
            );

            MissionOdds complete = system.GetMissionOdds(context);
            MissionOdds operational = system.GetMissionOdds(context, includePersonnelLoss: false);

            Assert.IsNotNull(complete);
            Assert.IsNotNull(operational);
            Assert.AreEqual(
                complete.ObjectiveSuccessProbability,
                operational.ObjectiveSuccessProbability,
                0.001
            );
            Assert.AreEqual(complete.FoilProbability, operational.FoilProbability, 0.001);
            Assert.AreEqual(0, operational.PersonnelLossProbability);
        }

        [TestCase((int)MissionEncounterPhase.DepartureStart, false)]
        [TestCase((int)MissionEncounterPhase.DepartureComplete, false)]
        [TestCase((int)MissionEncounterPhase.Arrival, false)]
        [TestCase((int)MissionEncounterPhase.PreObjective, false)]
        [TestCase((int)MissionEncounterPhase.PreObjective, true)]
        public void GetDetectors_MixedDefenders_AgreesWithObservedAiCheckpoint(
            int checkpoint,
            bool orbitalTarget
        )
        {
            MissionEncounterPhase phase = (MissionEncounterPhase)checkpoint;
            (GameRoot game, Planet planet, Officer _, Officer _) = BuildDetectionScene();
            Regiment groundRegiment = planet.GetChildren<Regiment>().Single();
            Fleet fleet = planet.GetChildren<Fleet>().Single();
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "detector-ship",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 1,
                StarfighterCapacity = 1,
            };
            Starfighter carriedFighter = new Starfighter
            {
                InstanceID = "carried-fighter",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Starfighter groundFighter = new Starfighter
            {
                InstanceID = "ground-fighter",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(ship, fleet);
            game.AttachNode(carriedFighter, ship);
            game.AttachNode(groundFighter, planet);
            game.AttachNode(CreateCompletedRegiment("carried-regiment", "rebels"), ship);
            Mission mission = orbitalTarget
                ? new SabotageMission
                {
                    OwnerInstanceID = "empire",
                    LocationInstanceID = planet.InstanceID,
                    SabotageTargetInstanceID = ship.InstanceID,
                }
                : new StubMission("empire", planet.InstanceID);
            ISceneNode objective = orbitalTarget ? ship : planet;
            MissionQueries queries = new MissionQueries(game);
            AIAssessment assessment = new AIAssessment(
                game,
                game.GetFactions().Single(faction => faction.InstanceID == "empire"),
                game.Galaxy
            );
            ISceneNode[] expected =
                phase == MissionEncounterPhase.DepartureStart
                || phase == MissionEncounterPhase.PreObjective && !orbitalTarget
                    ? new ISceneNode[] { groundRegiment }
                    : new ISceneNode[] { ship, carriedFighter };

            CollectionAssert.AreEqual(expected, queries.GetDetectors(mission, planet, phase));
            CollectionAssert.AreEqual(
                expected,
                assessment.GetMissionDetectors(mission, planet, phase, objective)
            );
        }

        [Test]
        public void GetMissionOdds_Default_IncludesStationaryFleetDetectors()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _) = BuildDetectionScene();
            game.DeleteNode(planet.GetChildren<Regiment>().Single());
            Fleet fleet = planet.GetChildren<Fleet>().Single();
            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "rebels",
                StarfighterCapacity = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Starfighter starfighter = new Starfighter
            {
                InstanceID = "fighter",
                OwnerInstanceID = "rebels",
                DetectionRating = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(capitalShip, fleet);
            game.AttachNode(starfighter, capitalShip);
            planet.AddVisitor("empire");
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            MissionQueries system = new MissionQueries(game);

            MissionOdds odds = system.GetMissionOdds(
                CreateContext(EspionageMission.MissionTypeID, spy, planet)
            );

            Assert.IsNotNull(odds);
            Assert.AreEqual(100, odds.FoilProbability, 0.001);
        }

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

        [TestCase(false, 100, 25)]
        [TestCase(false, 200, 25)]
        [TestCase(false, 50, 12.5)]
        [TestCase(false, 0, 0)]
        [TestCase(true, 100, 0)]
        public void GetMissionOdds_OfficerEscapes_IncludesApplicableInjuryDeathRisk(
            bool mainCharacter,
            int injuryChance,
            double expectedLoss
        )
        {
            (GameRoot game, Planet planet, Officer spy, Officer _) = BuildDetectionScene();
            spy.IsMain = mainCharacter;
            spy.SetBaseRating(SkillRating.Combat, 0);
            game.Config.DuelResolution.MinimumInjuryChance = 0;
            game.Config.DuelResolution.CaptureEvasionInjuryBaseChance = injuryChance;
            game.Config.Assassination.KillProbability = 25;
            planet.AddVisitor("empire");
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 100 } });

            MissionOdds odds = new MissionQueries(game).GetMissionOdds(
                CreateContext(EspionageMission.MissionTypeID, spy, planet)
            );

            Assert.AreEqual(expectedLoss, odds.PersonnelLossProbability, 0.001);
        }

        [TestCase(0, 0, 0, 100)]
        [TestCase(50, 100, 25, 12.5)]
        [TestCase(100, 0, 100, 0)]
        public void GetMissionOdds_OfficerDecoyWithoutFoil_IncludesConfrontationLossRisk(
            int diversionChance,
            int evasionChance,
            int deathChance,
            double expectedLoss
        )
        {
            (GameRoot game, Planet planet, Officer spy, Officer _) = BuildDetectionScene();
            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            decoy.SetBaseRating(SkillRating.Combat, 0);
            game.AttachNode(decoy, spy.GetParent());
            game.Config.DuelResolution.CaptureEvasionInjuryBaseChance = 100;
            game.Config.Assassination.KillProbability = deathChance;
            planet.AddVisitor("empire");
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 0 } });
            SetDecoyTable(game, new Dictionary<int, int> { { -1000, diversionChance } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, evasionChance } });

            MissionOdds odds = new MissionQueries(game).GetMissionOdds(
                CreateContext(
                    EspionageMission.MissionTypeID,
                    new List<IMissionParticipant> { spy },
                    new List<IMissionParticipant> { decoy },
                    planet
                )
            );

            Assert.AreEqual(0, odds.FoilProbability);
            Assert.AreEqual(expectedLoss, odds.PersonnelLossProbability, 0.001);
        }

        [Test]
        public void GetMissionOdds_DecoyAndMainOfficerAtRisk_DoesNotDoubleCountSharedOutcomes()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _) = BuildDetectionScene();
            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            game.AttachNode(decoy, spy.GetParent());
            planet.AddVisitor("empire");
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            SetDecoyTable(game, new Dictionary<int, int> { { -1000, 0 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 50 } });

            MissionOdds odds = new MissionQueries(game).GetMissionOdds(
                CreateContext(
                    EspionageMission.MissionTypeID,
                    new List<IMissionParticipant> { spy },
                    new List<IMissionParticipant> { decoy },
                    planet
                )
            );

            Assert.AreEqual(75, odds.PersonnelLossProbability, 0.001);
        }

        [Test]
        public void GetMissionOdds_DecoyFacesMultipleDetectors_CombinesDistinctLossOutcomes()
        {
            (GameRoot game, Planet planet, Officer spy, Officer _) = BuildDetectionScene();
            game.AttachNode(CreateCompletedRegiment("second", "rebels"), planet);
            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            game.AttachNode(decoy, spy.GetParent());
            planet.AddVisitor("empire");
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 0 } });
            SetDecoyTable(game, new Dictionary<int, int> { { -1000, 50 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -1000, 0 } });

            MissionOdds odds = new MissionQueries(game).GetMissionOdds(
                CreateContext(
                    EspionageMission.MissionTypeID,
                    new List<IMissionParticipant> { spy },
                    new List<IMissionParticipant> { decoy },
                    planet
                )
            );

            Assert.AreEqual(75, odds.PersonnelLossProbability, 0.001);
        }

        [Test]
        public void GetMissionOdds_DecoyDivertsDetector_UsesRemainingDetectorForPersonnelLoss()
        {
            (GameRoot game, Planet planet, Officer spy, Officer defender) = BuildDetectionScene();
            Regiment regiment = planet.GetChildren<Regiment>().Single();
            regiment.DetectionRating = 0;
            defender.SetBaseRating(SkillRating.Combat, 0);
            spy.SetBaseRating(SkillRating.Combat, 50);

            Fleet fleet = planet.GetChildren<Fleet>().Single();
            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "rebels",
                StarfighterCapacity = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Starfighter starfighter = new Starfighter
            {
                InstanceID = "fighter",
                OwnerInstanceID = "rebels",
                DetectionRating = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Officer commander = EntityFactory.CreateOfficer("commander", "rebels");
            commander.CurrentRank = OfficerRank.Commander;
            commander.SetBaseRating(SkillRating.Combat, 100);
            game.AttachNode(capitalShip, fleet);
            game.AttachNode(starfighter, capitalShip);
            game.AttachNode(commander, capitalShip);

            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            decoy.SetBaseRating(SkillRating.Espionage, 50);
            game.AttachNode(decoy, spy.GetParent());
            planet.AddVisitor("empire");
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 100 } });
            SetDecoyTable(game, new Dictionary<int, int> { { -50, 0 }, { 0, 100 } });
            SetEvasionTable(game, new Dictionary<int, int> { { -100, 0 }, { 0, 100 } });
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
            Assert.AreEqual(100, odds.FoilProbability, 0.001);
            Assert.AreEqual(100, odds.PersonnelLossProbability, 0.001);
        }

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

        [Test]
        public void GetMissionOdds_Default_DoesNotExposeHiddenBetrayalState()
        {
            (
                GameRoot game,
                Planet origin,
                Planet target,
                Officer loyalDiplomat,
                MissionQueries missions
            ) = BuildMissionOddsScene("empire");
            game.Config.ProbabilityTables.Mission.Diplomacy = new Dictionary<int, int>
            {
                { -100, 50 },
            };
            Officer betrayalDiplomat = EntityFactory.CreateOfficer(
                "betrayal-diplomat",
                "empire",
                canBetray: true,
                loyalty: 0
            );
            game.AttachNode(betrayalDiplomat, origin);

            MissionOdds loyalOdds = missions.GetMissionOdds(
                CreateContext(DiplomacyMission.MissionTypeID, loyalDiplomat, target)
            );
            MissionOdds betrayalOdds = missions.GetMissionOdds(
                CreateContext(DiplomacyMission.MissionTypeID, betrayalDiplomat, target)
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

        [Test]
        public void GetMissionOdds_Assassination_AppliesOneKillCheckAfterAnyParticipantHits()
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
            Assert.AreEqual(37.5, odds.ObjectiveSuccessProbability, 0.001);
            Assert.AreEqual(37.5, odds.OverallSuccessProbability, 0.001);
        }

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

        [Test]
        public void GetAvailableMissionOptions_ReconnaissanceSpecialForces_ReturnsReconnaissanceOption()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
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

        [Test]
        public void GetMissionOdds_InciteStrongGarrison_ReturnsZeroForBothRiskDetailLevels()
        {
            (GameRoot game, Planet _, Planet target, Officer officer, MissionQueries queries) =
                BuildMissionOddsScene("rebels");
            game.Config.ProbabilityTables.Mission.InciteUprising = new Dictionary<int, int>
            {
                { -1000, 100 },
            };
            for (int index = 0; index < 20; index++)
                game.AttachNode(CreateCompletedRegiment($"guard-{index}", "rebels"), target);
            MissionContext context = CreateContext(
                InciteUprisingMission.MissionTypeID,
                officer,
                target
            );

            MissionOdds displayed = queries.GetMissionOdds(context);
            MissionOdds operational = queries.GetMissionOdds(context, includePersonnelLoss: false);

            Assert.AreEqual(0, displayed.ObjectiveSuccessProbability);
            Assert.AreEqual(
                displayed.ObjectiveSuccessProbability,
                operational.ObjectiveSuccessProbability
            );
        }

        [Test]
        public void GetMissionOdds_IncitePartialControlChance_CombinesSkillAndIncidentProbabilities()
        {
            (GameRoot game, Planet _, Planet target, Officer officer, MissionQueries queries) =
                BuildMissionOddsScene("rebels");
            game.Config.ProbabilityTables.Mission.InciteUprising = new Dictionary<int, int>
            {
                { -1000, 50 },
            };
            game.Config.ProbabilityTables.Mission.Foil = new Dictionary<int, int> { { -1000, 0 } };
            game.Config.AI.Garrison.SupportThreshold = 60;
            game.Config.Uprising.DiceRange = 1;
            game.Config.Uprising.DiceAddend = 0;
            game.Config.Uprising.MissionLeadershipDivisor = 10;
            game.Config.Uprising.InciteMissionSupportShift = -10;
            game.Config.Uprising.PrimaryConsequenceTable = new Dictionary<int, int> { { 10, 2 } };
            game.Config.Uprising.SecondaryConsequenceTable.Clear();
            officer.SetBaseRating(SkillRating.Leadership, 100);
            target.PopularSupport = new Dictionary<string, int>
            {
                { "rebels", 60 },
                { "empire", 40 },
            };
            game.AttachNode(CreateCompletedRegiment("guard", "rebels"), target);

            MissionOdds odds = queries.GetMissionOdds(
                CreateContext(InciteUprisingMission.MissionTypeID, officer, target)
            );

            Assert.AreEqual(37.5, odds.ObjectiveSuccessProbability, 0.000001);
            Assert.AreEqual(37.5, odds.OverallSuccessProbability, 0.000001);
        }

        [Test]
        public void GetMissionOdds_ObservedGarrison_DoesNotReadHiddenLiveRegiments()
        {
            (GameRoot game, Planet _, Planet target, Officer officer, MissionQueries queries) =
                BuildMissionOddsScene("rebels");
            game.Config.ProbabilityTables.Mission.InciteUprising = new Dictionary<int, int>
            {
                { -1000, 100 },
            };
            target.PopularSupport = new Dictionary<string, int>
            {
                { "rebels", 60 },
                { "empire", 40 },
            };
            game.Config.Uprising.InciteMissionSupportShift = -10;
            game.Config.SupportShift.OwnershipTransferThreshold = 60;
            for (int index = 0; index < 20; index++)
                game.AttachNode(CreateCompletedRegiment($"guard-{index}", "rebels"), target);
            Planet observed = (Planet)target.CreateCopy();
            observed.SetParent(target.GetParent());

            MissionOdds visible = queries.GetMissionOdds(
                CreateContext(InciteUprisingMission.MissionTypeID, officer, observed)
            );
            MissionOdds live = queries.GetMissionOdds(
                CreateContext(InciteUprisingMission.MissionTypeID, officer, target)
            );

            Assert.AreEqual(100, visible.ObjectiveSuccessProbability);
            Assert.AreEqual(0, live.ObjectiveSuccessProbability);
            Assert.AreEqual(20, target.GetRegimentCount());
            Assert.AreEqual(60, target.GetPopularSupport("rebels"));
        }

        [TestCase(1, 50)]
        [TestCase(2, 75)]
        public void GetMissionOdds_InciteTeam_CombinesIndependentSkillRolls(
            int participantCount,
            double expected
        )
        {
            (GameRoot game, Planet origin, Planet target, Officer officer, MissionQueries queries) =
                BuildMissionOddsScene("rebels");
            game.Config.ProbabilityTables.Mission.InciteUprising = new Dictionary<int, int>
            {
                { -1000, 50 },
            };
            game.Config.Uprising.InciteMissionSupportShift = -10;
            target.PopularSupport = new Dictionary<string, int>
            {
                { "rebels", 60 },
                { "empire", 40 },
            };
            game.Config.SupportShift.OwnershipTransferThreshold = 60;
            List<IMissionParticipant> participants = new List<IMissionParticipant> { officer };
            if (participantCount == 2)
            {
                Officer second = EntityFactory.CreateOfficer("second", "empire");
                game.AttachNode(second, origin);
                participants.Add(second);
            }

            MissionOdds odds = queries.GetMissionOdds(
                CreateContext(
                    InciteUprisingMission.MissionTypeID,
                    participants,
                    new List<IMissionParticipant>(),
                    target
                )
            );

            Assert.AreEqual(expected, odds.ObjectiveSuccessProbability, 0.000001);
        }

        [Test]
        public void GetMissionOdds_NullContext_ReturnsNull()
        {
            MissionQueries queries = new MissionQueries(TestGame.Create(TestConfig.Create()));

            Assert.IsNull(queries.GetMissionOdds(null));
        }

        [Test]
        public void GetMissionOdds_UnstartedInciteProposal_DoesNotConsumeInstanceIdentifiers()
        {
            (GameRoot game, Planet _, Planet target, Officer officer, MissionQueries queries) =
                BuildMissionOddsScene("rebels");
            target.PopularSupport = new Dictionary<string, int>
            {
                { "rebels", 60 },
                { "empire", 40 },
            };
            game.Config.ProbabilityTables.Mission.InciteUprising = new Dictionary<int, int>
            {
                { -1000, 100 },
            };
            MissionContext context = CreateContext(
                InciteUprisingMission.MissionTypeID,
                officer,
                target
            );
            try
            {
                BaseGameEntity.SetInstanceIdSeed(12345);
                string expected = new Regiment().InstanceID;
                BaseGameEntity.SetInstanceIdSeed(12345);

                Assert.IsNotNull(queries.GetMissionOdds(context));
                string actual = new Regiment().InstanceID;

                Assert.AreEqual(expected, actual);
            }
            finally
            {
                BaseGameEntity.SetInstanceIdSeed(null);
            }
        }

        [Test]
        public void GetMissionOdds_SharedPlanetAssessment_KeepsEachTeamsSkillProbability()
        {
            (GameRoot game, Planet origin, Planet target, Officer first, MissionQueries queries) =
                BuildMissionOddsScene("rebels");
            game.Config.ProbabilityTables.Mission.InciteUprising = new Dictionary<int, int>
            {
                { -1000, 50 },
            };
            game.Config.ProbabilityTables.Mission.Foil = new Dictionary<int, int> { { -1000, 0 } };
            game.Config.Uprising.DiceRange = 1;
            game.Config.Uprising.DiceAddend = 0;
            game.Config.Uprising.MissionLeadershipDivisor = 10;
            game.Config.Uprising.InciteMissionSupportShift = -10;
            game.Config.Uprising.PrimaryConsequenceTable = new Dictionary<int, int> { { 10, 2 } };
            game.Config.Uprising.SecondaryConsequenceTable.Clear();
            game.Config.AI.Garrison.SupportThreshold = 60;
            game.Config.SupportShift.OwnershipTransferThreshold = 60;
            first.SetBaseRating(SkillRating.Leadership, 100);
            Officer second = EntityFactory.CreateOfficer("second", "empire");
            second.SetBaseRating(SkillRating.Leadership, 100);
            game.AttachNode(second, origin);
            target.PopularSupport = new Dictionary<string, int>
            {
                { "rebels", 60 },
                { "empire", 40 },
            };
            game.AttachNode(CreateCompletedRegiment("guard", "rebels"), target);
            Mission.MissionEvaluationState state = new Mission.MissionEvaluationState(game, target);

            MissionOdds single = queries.GetMissionOdds(
                CreateContext(InciteUprisingMission.MissionTypeID, first, target),
                (_, _, _, _) => Array.Empty<ISceneNode>(),
                includePersonnelLoss: false,
                evaluationState: state
            );
            MissionOdds team = queries.GetMissionOdds(
                CreateContext(
                    InciteUprisingMission.MissionTypeID,
                    new List<IMissionParticipant> { first, second },
                    new List<IMissionParticipant>(),
                    target
                ),
                (_, _, _, _) => Array.Empty<ISceneNode>(),
                includePersonnelLoss: false,
                evaluationState: state
            );

            Assert.AreEqual(37.5, single.ObjectiveSuccessProbability, 0.000001);
            Assert.AreEqual(56.25, team.ObjectiveSuccessProbability, 0.000001);
        }

        /// <summary>
        /// Prevents defender-count growth from multiplying commander scene-graph scans.
        /// </summary>
        [Test]
        public void GetMissionOdds_MoreDefenders_DoesNotRepeatCommanderContainerTraversals()
        {
            int small = MeasureCommanderTraversals(8);
            int large = MeasureCommanderTraversals(128);

            Assert.That(small, Is.GreaterThan(0));
            Assert.That(large, Is.EqualTo(small));
        }

        [TestCase(OfficerRank.None, 0)]
        [TestCase(OfficerRank.General, 1)]
        public void GetMissionOdds_CommanderBecomesIneligible_RefreshesNextEvaluation(
            OfficerRank rank,
            int injury
        )
        {
            (GameRoot game, Planet planet, Officer spy, Officer general) = BuildDetectionScene();
            planet.AddVisitor("empire");
            spy.SetBaseRating(SkillRating.Espionage, 100);
            general.SetBaseRating(SkillRating.Espionage, 80);
            Regiment detector = planet.GetChildren<Regiment>().Single();
            detector.DetectionRating = 20;
            game.Config.ProbabilityTables.Mission.FoilFlatScoreAdjustment = 0;
            game.Config.ProbabilityTables.Mission.FoilDefenderScalingPercent = 100;
            SetFoilTable(game, new Dictionary<int, int> { { -1000, 90 }, { 25, 10 } });
            MissionQueries queries = new MissionQueries(game);
            MissionContext context = CreateContext(EspionageMission.MissionTypeID, spy, planet);
            ISceneNode[] detectors = { detector };
            MissionOdds before = queries.GetMissionOdds(context, detectors);

            general.CurrentRank = rank;
            general.InjuryPoints = injury;
            MissionOdds after = queries.GetMissionOdds(context, detectors);
            MissionOdds fresh = new MissionQueries(game).GetMissionOdds(context, detectors);

            Assert.Greater(before.FoilProbability, after.FoilProbability);
            Assert.AreEqual(fresh.FoilProbability, after.FoilProbability);
            Assert.AreEqual(fresh.PersonnelLossProbability, after.PersonnelLossProbability);
        }

        /// <summary>
        /// Checks indexed fleet and planetary commanders against direct commander resolution.
        /// </summary>
        [Test]
        public void GetMissionOdds_DifferentCommanderScopes_MatchesAnalyticalRisk()
        {
            (GameRoot game, Planet planet, Officer spy, Officer general) = BuildDetectionScene();
            planet.AddVisitor("empire");
            spy.SetBaseRating(SkillRating.Combat, 75);
            spy.SetBaseRating(SkillRating.Espionage, 80);
            general.SetBaseRating(SkillRating.Combat, 10);
            general.SetBaseRating(SkillRating.Espionage, 20);
            SetFoilTable(
                game,
                new Dictionary<int, int>
                {
                    { -1000, 95 },
                    { -30, 70 },
                    { 0, 40 },
                    { 30, 5 },
                }
            );
            SetEvasionTable(
                game,
                new Dictionary<int, int>
                {
                    { -1000, 5 },
                    { -30, 20 },
                    { 0, 50 },
                    { 30, 80 },
                    { 60, 98 },
                }
            );
            Fleet firstFleet = planet.GetChildren<Fleet>().Single();
            Fleet secondFleet = new Fleet
            {
                InstanceID = "second-fleet",
                OwnerInstanceID = "rebels",
            };
            game.AttachNode(secondFleet, planet);
            CapitalShip firstShip = new CapitalShip
            {
                InstanceID = "first-ship",
                OwnerInstanceID = "rebels",
                DetectionRating = 30,
                StarfighterCapacity = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            CapitalShip secondShip = new CapitalShip
            {
                InstanceID = "second-ship",
                OwnerInstanceID = "rebels",
                DetectionRating = 45,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Starfighter fighter = new Starfighter
            {
                InstanceID = "fighter",
                OwnerInstanceID = "rebels",
                DetectionRating = 55,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(firstShip, firstFleet);
            game.AttachNode(secondShip, secondFleet);
            game.AttachNode(fighter, firstShip);
            Officer unavailable = EntityFactory.CreateOfficer("unavailable-admiral", "rebels");
            unavailable.CurrentRank = OfficerRank.Admiral;
            unavailable.InjuryPoints = 1;
            game.AttachNode(unavailable, firstShip);
            Officer admiral = EntityFactory.CreateOfficer("first-admiral", "rebels");
            admiral.CurrentRank = OfficerRank.Admiral;
            admiral.SetBaseRating(SkillRating.Combat, 60);
            admiral.SetBaseRating(SkillRating.Espionage, 50);
            game.AttachNode(admiral, firstShip);
            Officer commander = EntityFactory.CreateOfficer("fighter-commander", "rebels");
            commander.CurrentRank = OfficerRank.Commander;
            commander.SetBaseRating(SkillRating.Combat, 20);
            commander.SetBaseRating(SkillRating.Espionage, 10);
            game.AttachNode(commander, firstShip);
            Officer secondAdmiral = EntityFactory.CreateOfficer("second-admiral", "rebels");
            secondAdmiral.CurrentRank = OfficerRank.Admiral;
            secondAdmiral.SetBaseRating(SkillRating.Combat, 95);
            secondAdmiral.SetBaseRating(SkillRating.Espionage, 80);
            game.AttachNode(secondAdmiral, secondShip);
            ISceneNode[] detectors =
            {
                planet.GetChildren<Regiment>().Single(),
                firstShip,
                secondShip,
                fighter,
            };
            MissionQueries queries = new MissionQueries(game);
            MissionContext context = CreateContext(EspionageMission.MissionTypeID, spy, planet);
            Assert.That(queries.TryCreateMission(context, out Mission mission), Is.True);
            double expectedFoil =
                1
                - detectors.Aggregate(
                    1d,
                    (value, detector) =>
                        value * (1 - queries.GetFoilProbability(mission, detector) / 100d)
                );
            double expectedEvasion = detectors.Average(detector =>
                Math.Clamp(
                    queries.GetEvasionProbability(
                        spy.GetEffectiveRating(SkillRating.Combat)
                            - (
                                mission
                                    .FindDetectorCommander(detector)
                                    ?.GetEffectiveRating(SkillRating.Combat)
                                ?? 0
                            )
                    ) / 100d,
                    0,
                    1
                )
            );

            MissionOdds odds = queries.GetMissionOdds(
                context,
                (_, _, phase, _) =>
                    phase == MissionEncounterPhase.PreObjective
                        ? detectors
                        : Array.Empty<ISceneNode>()
            );

            Assert.That(odds.FoilProbability, Is.EqualTo(expectedFoil * 100).Within(1e-9));
            Assert.That(
                odds.PersonnelLossProbability,
                Is.EqualTo(expectedFoil * (1 - expectedEvasion) * 100).Within(1e-9)
            );
        }

        /// <summary>
        /// Measures commander-container enumeration independently of wall-clock timing.
        /// </summary>
        /// <param name="defenderCount">The number of ships whose odds are requested.</param>
        /// <returns>The number of traversals of their shared fleet container.</returns>
        private int MeasureCommanderTraversals(int defenderCount)
        {
            (GameRoot game, Planet planet, Officer spy, Officer _) = BuildDetectionScene();
            planet.AddVisitor("empire");
            CountingFleet fleet = new CountingFleet
            {
                InstanceID = "counted-fleet",
                OwnerInstanceID = "rebels",
            };
            game.AttachNode(fleet, planet);
            List<ISceneNode> detectors = new List<ISceneNode>();
            for (int index = 0; index < defenderCount; index++)
            {
                CapitalShip ship = new CapitalShip
                {
                    InstanceID = "counted-ship-" + index,
                    OwnerInstanceID = "rebels",
                    DetectionRating = 30,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                game.AttachNode(ship, fleet);
                detectors.Add(ship);
            }
            MissionQueries queries = new MissionQueries(game);
            MissionContext context = CreateContext(EspionageMission.MissionTypeID, spy, planet);
            Assert.That(queries.TryCreateMission(context, out _), Is.True);
            fleet.ChildEnumerationCount = 0;

            Assert.That(queries.GetMissionOdds(context, detectors), Is.Not.Null);

            return fleet.ChildEnumerationCount;
        }

        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <param name="factionOwnsPlanet">Whether faction owns planet.</param>
        /// <returns>The constructed scene.</returns>
        private (GameRoot game, Planet planet, Officer officer) BuildScene(bool factionOwnsPlanet)
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
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
        /// Creates heterogeneous naval defenders and a valid unstarted espionage team.
        /// </summary>
        /// <param name="detectorCount">The number of separately commanded defending ships.</param>
        /// <param name="decoyCount">The number of supporting decoys.</param>
        /// <param name="officerDecoys">Whether decoys are officers rather than special forces.</param>
        /// <param name="mainCount">The primary-officer count.</param>
        /// <returns>The public query and mission context for the fixed scenario.</returns>
        private static (MissionQueries Queries, MissionContext Context) CreateMixedFleetMission(
            int detectorCount,
            int decoyCount,
            bool officerDecoys,
            int mainCount
        )
        {
            GameRoot game = new GameRoot();
            GameConfig config = new GameConfig();
            game.SetConfig(config);
            config.ProbabilityTables.Mission.Foil = new Dictionary<int, int> { { -1000, 35 } };
            config.ProbabilityTables.Mission.FleetDecoy = new Dictionary<int, int>
            {
                { -1000, 20 },
                { 0, 50 },
                { 40, 80 },
            };
            config.ProbabilityTables.Mission.PlanetaryDecoy = config
                .ProbabilityTables
                .Mission
                .FleetDecoy;
            config.ProbabilityTables.Mission.Evasion = new Dictionary<int, int>
            {
                { -200, 5 },
                { -40, 20 },
                { 0, 50 },
                { 40, 80 },
                { 200, 95 },
            };
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
                InstanceID = "target",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(origin, sector);
            game.AttachNode(target, sector);
            target.AddVisitor("empire");
            List<IMissionParticipant> mainParticipants = new List<IMissionParticipant>();
            List<IMissionParticipant> decoys = new List<IMissionParticipant>();
            for (int i = 0; i < mainCount; i++)
            {
                Officer officer = CreateMissionOfficer(
                    "main-" + i,
                    "empire",
                    55 + i * 10,
                    60 + i * 15
                );
                game.AttachNode(officer, origin);
                mainParticipants.Add(officer);
            }
            for (int i = 0; i < decoyCount; i++)
            {
                IMissionParticipant unit = officerDecoys
                    ? (IMissionParticipant)CreateMissionOfficer(
                        "decoy-" + i,
                        "empire",
                        15 + i * 30,
                        35 + i * 10
                    )
                    : new SpecialForces
                    {
                        InstanceID = "decoy-" + i,
                        OwnerInstanceID = "empire",
                        AllowedMissionTypeIDs = new List<string> { EspionageMission.MissionTypeID },
                        Ratings = new Dictionary<SkillRating, int>
                        {
                            { SkillRating.Espionage, 15 + i * 30 },
                            { SkillRating.Combat, 35 + i * 10 },
                        },
                    };
                game.AttachNode(unit, origin);
                decoys.Add(unit);
            }
            for (int i = 0; i < detectorCount; i++)
            {
                Fleet fleet = new Fleet { InstanceID = "fleet-" + i, OwnerInstanceID = "rebels" };
                game.AttachNode(fleet, target);
                CapitalShip ship = new CapitalShip
                {
                    InstanceID = "ship-" + i,
                    OwnerInstanceID = "rebels",
                    DetectionRating = 25 + i % 3 * 25,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                game.AttachNode(ship, fleet);
                Officer commander = CreateMissionOfficer(
                    "admiral-" + i,
                    "rebels",
                    50,
                    i * 17 % 101
                );
                commander.CurrentRank = OfficerRank.Admiral;
                game.AttachNode(commander, ship);
            }
            return (
                new MissionQueries(game),
                new MissionContext
                {
                    Game = game,
                    OwnerInstanceId = "empire",
                    MissionTypeID = EspionageMission.MissionTypeID,
                    Location = target,
                    MainParticipants = mainParticipants,
                    DecoyParticipants = decoys,
                }
            );
        }

        /// <summary>
        /// Creates an officer with deterministic mission ratings.
        /// </summary>
        /// <param name="id">The instance identifier.</param>
        /// <param name="owner">The faction identifier.</param>
        /// <param name="espionage">The Espionage rating.</param>
        /// <param name="combat">The Combat rating.</param>
        /// <returns>The detached officer.</returns>
        private static Officer CreateMissionOfficer(
            string id,
            string owner,
            int espionage,
            int combat
        ) =>
            new Officer
            {
                InstanceID = id,
                DisplayName = id,
                OwnerInstanceID = owner,
                Ratings = new Dictionary<SkillRating, int>
                {
                    { SkillRating.Diplomacy, 50 },
                    { SkillRating.Espionage, espionage },
                    { SkillRating.Combat, combat },
                    { SkillRating.Leadership, 50 },
                },
            };

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
            GameRoot game = TestGame.Create(config);
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

            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "rebels" };
            game.AttachNode(fleet, planet);

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
            GameRoot game = TestGame.Create(TestConfig.Create());
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
            GameRoot game = TestGame.Create(config);
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

        /// <summary>
        /// Counts container traversal without adding timing instrumentation to production code.
        /// </summary>
        private sealed class CountingFleet : Fleet
        {
            public int ChildEnumerationCount { get; set; }

            /// <summary>
            /// Creates a copy that preserves the fleet's diagnostic subtype.
            /// </summary>
            /// <returns>An empty counting fleet.</returns>
            protected override BaseSceneNode CreateNodeCopy() => new CountingFleet();

            /// <summary>
            /// Counts enumeration calls before using the standard fleet children.
            /// </summary>
            /// <returns>The fleet's direct child nodes.</returns>
            protected override IEnumerable<ISceneNode> EnumerateChildren()
            {
                ChildEnumerationCount++;
                return base.EnumerateChildren();
            }
        }
    }
}
