using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class InciteUprisingMissionTests
    {
        [Test]
        public void RollParticipantSuccess_GarrisonedRegiment_DoesNotAffectScore()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.SetPopularSupport("empire", 40);
            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
                DefenseRating = 500,
            };
            game.AttachNode(regiment, enemyPlanet);

            Mission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.Config.ProbabilityTables.Mission.InciteUprising = new Dictionary<int, int>
            {
                { -11, 0 },
                { -10, 100 },
            };
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            Assert.IsTrue(mission.RollParticipantSuccess(officer, new FixedRNG(0), game));
        }

        [Test]
        public void TryCreate_PlanetAlreadyInUprising_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();
            enemyPlanet.BeginUprising();

            Assert.IsNull(
                CreateInciteUprisingMission(
                    "empire",
                    enemyPlanet,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>()
                )
            );
        }

        [Test]
        public void ShouldRepeatAfterCompletion_NeutralPlanetWithoutFriendlyTroops_ReturnsFalse()
        {
            var (game, _, enemyPlanet, officer, _) = MissionSceneBuilder.Build();
            Mission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            enemyPlanet.OwnerInstanceID = null;

            Assert.IsFalse(mission.ShouldRepeatAfterCompletion(game));
        }

        [Test]
        public void ShouldRepeatAfterCompletion_EnemyControlsPlanet_ReturnsTrue()
        {
            var (game, _, enemyPlanet, officer, _) = MissionSceneBuilder.Build();
            Mission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);

            Assert.IsTrue(mission.ShouldRepeatAfterCompletion(game));
        }

        [Test]
        public void ShouldRepeatAfterCompletion_NeutralPlanetWithFriendlyTroops_ReturnsTrue()
        {
            var (game, _, enemyPlanet, officer, _) = MissionSceneBuilder.Build();
            enemyPlanet.OwnerInstanceID = "empire";
            Regiment regiment = EntityFactory.CreateRegiment("friendly-regiment", "empire");
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(regiment, enemyPlanet);
            enemyPlanet.OwnerInstanceID = "rebels";
            Mission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            enemyPlanet.OwnerInstanceID = null;

            Assert.IsTrue(mission.ShouldRepeatAfterCompletion(game));
        }

        [Test]
        public void TryCreate_OwnedPlanetTarget_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();

            Assert.IsNull(
                CreateInciteUprisingMission(
                    "empire",
                    empirePlanet,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>()
                ),
                "TryCreate should return null when target planet is owned by the mission owner"
            );
        }

        [Test]
        public void TryCreate_NeutralPlanetTarget_ReturnsNull()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();

            enemyPlanet.OwnerInstanceID = null;

            Assert.IsNull(
                CreateInciteUprisingMission(
                    "empire",
                    enemyPlanet,
                    new List<IMissionParticipant> { officer },
                    new List<IMissionParticipant>()
                ),
                "TryCreate should return null when target planet is neutral (no owner to revolt against)"
            );
        }

        [Test]
        public void DisplayName_Default_IsHumanReadable()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();

            Mission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            Assert.AreEqual("Incite Uprising", mission.DisplayName);
        }

        [Test]
        public void GetAbortReason_UprisingAlreadyStarted_DoesNotAbort()
        {
            (
                GameRoot game,
                Planet empirePlanet,
                Planet enemyPlanet,
                Officer officer,
                FogOfWarCommands fog
            ) = MissionSceneBuilder.Build();

            Mission mission = CreateInciteUprisingMission(
                "empire",
                enemyPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            game.AttachNode(mission, enemyPlanet);
            mission.Initiate(0);

            enemyPlanet.BeginUprising();

            Assert.IsNull(mission.GetAbortReason(game));
        }

        [Test]
        public void Serialize_RoundTrip_PreservesData()
        {
            Mission mission = new InciteUprisingMission
            {
                InstanceID = "MISSION1",
                OwnerInstanceID = "FACTION1",
                ConfigKey = "InciteUprising",
                DisplayName = "Incite Uprising",
                LocationInstanceID = "PLANET1",
                ParticipantRating = SkillRating.Diplomacy,
                HasInitiated = false,
                MaxProgress = 20,
                CurrentProgress = 0,
            };

            string xml = SerializationHelper.Serialize(mission);
            Mission deserialized = SerializationHelper.Deserialize<Mission>(xml);

            Assert.AreEqual("MISSION1", deserialized.InstanceID);
            Assert.AreEqual("InciteUprising", deserialized.ConfigKey);
            Assert.AreEqual("PLANET1", deserialized.LocationInstanceID);
            Assert.AreEqual(SkillRating.Diplomacy, deserialized.ParticipantRating);
            Assert.IsFalse(deserialized.HasInitiated);
            Assert.AreEqual(20, deserialized.MaxProgress);
        }

        [TestCase(0, 20, false)]
        [TestCase(0, 60, false)]
        [TestCase(0, 100, false)]
        [TestCase(1, 20, false)]
        [TestCase(1, 60, false)]
        [TestCase(2, 20, false)]
        [TestCase(6, 20, false)]
        [TestCase(20, 0, true)]
        [TestCase(1, 20, true)]
        public void GetCompletionSuccessProbability_InciteDiceOutcomes_MatchesResolvedSuccessFrequency(
            int troops,
            int support,
            bool core
        )
        {
            GameConfig config = CreateCompletionConfig();
            (GameRoot game, Planet planet, Mission mission) = BuildCompletionScene(
                config,
                troops,
                support,
                core
            );
            double probability =
                mission.GetCompletionSuccessProbability(mission.GetMainParticipants(), game, planet)
                / 100d;
            int successes = 0;
            for (int first = 0; first <= config.Uprising.DiceRange; first++)
            {
                for (int second = 0; second <= config.Uprising.DiceRange; second++)
                {
                    (GameRoot outcomeGame, Planet _, Mission outcomeMission) = BuildCompletionScene(
                        config,
                        troops,
                        support,
                        core
                    );
                    UprisingResolver resolver = CreateCompletionResolver(
                        outcomeGame,
                        new QueueRNG(
                            0,
                            (first + 0.5) / (config.Uprising.DiceRange + 1),
                            (second + 0.5) / (config.Uprising.DiceRange + 1)
                        )
                    );
                    Assert.IsTrue(
                        resolver.TryExecuteMission(outcomeMission, out List<GameResult> results)
                    );
                    if (
                        results.OfType<MissionCompletedResult>().Single().Outcome
                        == MissionOutcome.Success
                    )
                        successes++;
                }
            }
            Assert.AreEqual(
                successes / ((config.Uprising.DiceRange + 1d) * (config.Uprising.DiceRange + 1d)),
                probability,
                0.000001
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GetCompletionSuccessProbability_TwoRegimentConsequences_CountsBothLosses(
            bool primaryEnabled
        )
        {
            GameConfig config = CreateCompletionConfig();
            config.Uprising.PrimaryConsequenceTable = new Dictionary<int, int>
            {
                { -1000, primaryEnabled ? 2 : 0 },
            };
            config.Uprising.SecondaryConsequenceTable = new Dictionary<int, int> { { -1000, 2 } };
            (GameRoot game, Planet planet, Mission mission) = BuildCompletionScene(config, 2, 20);

            double probability =
                mission.GetCompletionSuccessProbability(mission.GetMainParticipants(), game, planet)
                / 100d;
            UprisingResolver resolver = CreateCompletionResolver(game, new QueueRNG(0));
            resolver.TryExecuteMission(mission, out List<GameResult> results);

            Assert.AreEqual(primaryEnabled ? 1 : 0, probability);
            Assert.AreEqual(
                primaryEnabled ? MissionOutcome.Success : MissionOutcome.Failed,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
        }

        [Test]
        public void GetCompletionSuccessProbability_ObservedSnapshot_DoesNotMutateSupportGarrisonOrTimers()
        {
            (GameRoot game, Planet planet, Mission mission) = BuildCompletionScene(
                CreateCompletionConfig(),
                1,
                60
            );
            Dictionary<string, int> support = new Dictionary<string, int>(planet.PopularSupport);
            Regiment regiment = planet.GetAllRegiments().Single();

            mission.GetCompletionSuccessProbability(mission.GetMainParticipants(), game, planet);

            CollectionAssert.AreEquivalent(support, planet.PopularSupport);
            Assert.AreSame(regiment, planet.GetAllRegiments().Single());
            Assert.IsFalse(planet.IsInUprising);
            Assert.AreEqual(0, planet.NextUprisingIncidentTick);
            Assert.AreSame(planet, mission.GetParent());
        }

        /// <summary>
        /// Creates incite uprising mission.
        /// </summary>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="target">The target.</param>
        /// <param name="mainParticipants">The main participants.</param>
        /// <param name="decoyParticipants">The decoy participants.</param>
        /// <returns>The created incite uprising mission.</returns>
        private Mission CreateInciteUprisingMission(
            string ownerInstanceId,
            Planet target,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants
        )
        {
            return MissionTestFactory.TryCreate(
                InciteUprisingMission.MissionTypeID,
                null,
                ownerInstanceId,
                target,
                mainParticipants,
                decoyParticipants
            );
        }

        /// <summary>
        /// Loads configured uprising rules and guarantees the isolated participant skill roll.
        /// </summary>
        /// <returns>The scenario configuration.</returns>
        private static GameConfig CreateCompletionConfig()
        {
            GameConfig config = TestConfig.Create();
            config.ProbabilityTables.Mission.InciteUprising = new Dictionary<int, int>
            {
                { -1000, 100 },
            };
            config.ProbabilityTables.Mission.SubdueUprising = new Dictionary<int, int>
            {
                { -1000, 100 },
            };
            return config;
        }

        /// <summary>
        /// Builds an initiated mission with stationary personnel and a configured garrison.
        /// </summary>
        /// <param name="config">The shared immutable scenario configuration.</param>
        /// <param name="troops">The controller's stationary regiment count.</param>
        /// <param name="support">Popular support for the controller.</param>
        /// <param name="core">Whether the destination is in a core sector.</param>
        /// <param name="subdue">Whether to create a subdue mission instead of an incite mission.</param>
        /// <returns>The game, destination, and active mission.</returns>
        private static (GameRoot game, Planet planet, Mission mission) BuildCompletionScene(
            GameConfig config,
            int troops,
            int support,
            bool core = false,
            bool subdue = false
        )
        {
            GameRoot game = TestGame.Create(config);
            Faction empire = new Faction { InstanceID = "empire" };
            empire.Settings.UprisingResistance = 2;
            empire.Settings.GarrisonEfficiency = 2;
            game.GetFactions().Add(empire);
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            PlanetSector sector = new PlanetSector
            {
                InstanceID = "sector",
                SectorType = core ? PlanetSectorType.Core : PlanetSectorType.OuterRim,
            };
            game.AttachNode(sector, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "target",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int>
                {
                    { "empire", support },
                    { "rebels", 100 - support },
                },
            };
            game.AttachNode(planet, sector);
            for (int index = 0; index < troops; index++)
                game.AttachNode(
                    new Regiment
                    {
                        InstanceID = $"regiment-{index}",
                        OwnerInstanceID = "empire",
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    planet
                );
            Officer officer = EntityFactory.CreateOfficer(
                "participant",
                subdue ? "empire" : "rebels"
            );
            officer.SetBaseRating(SkillRating.Leadership, 100);
            if (subdue)
                planet.BeginUprising();
            Mission mission = MissionTestFactory.TryCreate(
                subdue ? SubdueUprisingMission.MissionTypeID : InciteUprisingMission.MissionTypeID,
                game,
                officer.OwnerInstanceID,
                planet,
                new List<IMissionParticipant> { officer }
            );
            Assert.IsNotNull(mission);
            game.AttachNode(mission, planet);
            game.AttachNode(officer, mission);
            mission.Initiate(1);
            return (game, planet, mission);
        }

        /// <summary>
        /// Creates the mission resolver without ticking unrelated systems.
        /// </summary>
        /// <param name="game">The isolated scenario game.</param>
        /// <param name="rng">The controlled participant and consequence rolls.</param>
        /// <returns>The uprising resolver.</returns>
        private static UprisingResolver CreateCompletionResolver(GameRoot game, QueueRNG rng)
        {
            return new UprisingResolver(
                game,
                rng,
                new PlanetaryControlCommands(
                    game,
                    new PlanetaryControlQueries(game),
                    new FogOfWarQueries(game)
                )
            );
        }
    }
}
