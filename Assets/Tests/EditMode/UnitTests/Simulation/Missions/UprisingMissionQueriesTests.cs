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

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public sealed class UprisingMissionQueriesTests
    {
        [TestCase(0, 20, false)]
        [TestCase(0, 60, false)]
        [TestCase(0, 100, false)]
        [TestCase(1, 20, false)]
        [TestCase(1, 60, false)]
        [TestCase(2, 20, false)]
        [TestCase(6, 20, false)]
        [TestCase(20, 0, true)]
        [TestCase(1, 20, true)]
        public void GetCompletionProbability_InciteDiceOutcomes_MatchesResolvedSuccessFrequency(
            int troops,
            int support,
            bool core
        )
        {
            GameConfig config = CreateConfig();
            (GameRoot game, Planet planet, Mission mission) = BuildScene(
                config,
                troops,
                support,
                core
            );
            double probability = new UprisingMissionQueries(game).GetCompletionProbability(
                mission,
                planet
            );
            int successes = 0;
            for (int first = 0; first <= config.Uprising.DiceRange; first++)
            {
                for (int second = 0; second <= config.Uprising.DiceRange; second++)
                {
                    (GameRoot outcomeGame, Planet _, Mission outcomeMission) = BuildScene(
                        config,
                        troops,
                        support,
                        core
                    );
                    UprisingResolver resolver = CreateResolver(
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
        public void GetCompletionProbability_TwoRegimentConsequences_CountsBothLosses(
            bool primaryEnabled
        )
        {
            GameConfig config = CreateConfig();
            config.Uprising.PrimaryConsequenceTable = new Dictionary<int, int>
            {
                { -1000, primaryEnabled ? 2 : 0 },
            };
            config.Uprising.SecondaryConsequenceTable = new Dictionary<int, int> { { -1000, 2 } };
            (GameRoot game, Planet planet, Mission mission) = BuildScene(config, 2, 20);

            double probability = new UprisingMissionQueries(game).GetCompletionProbability(
                mission,
                planet
            );
            UprisingResolver resolver = CreateResolver(game, new QueueRNG(0));
            resolver.TryExecuteMission(mission, out List<GameResult> results);

            Assert.AreEqual(primaryEnabled ? 1 : 0, probability);
            Assert.AreEqual(
                primaryEnabled ? MissionOutcome.Success : MissionOutcome.Failed,
                results.OfType<MissionCompletedResult>().Single().Outcome
            );
        }

        [TestCase(0, 20, false)]
        [TestCase(1, 20, false)]
        [TestCase(2, 40, false)]
        [TestCase(2, 40, true)]
        [TestCase(1, 55, false)]
        public void GetCompletionProbability_SubdueSupportOutcomes_MatchesResolvedSuccessFrequency(
            int troops,
            int support,
            bool core
        )
        {
            GameConfig config = CreateConfig();
            (GameRoot game, Planet planet, Mission mission) = BuildScene(
                config,
                troops,
                support,
                core,
                subdue: true
            );
            double probability = new UprisingMissionQueries(game).GetCompletionProbability(
                mission,
                planet
            );
            int successes = 0;
            for (int roll = 0; roll <= config.Uprising.SubdueOwnedSupportRange; roll++)
            {
                (GameRoot outcomeGame, Planet _, Mission outcomeMission) = BuildScene(
                    config,
                    troops,
                    support,
                    core,
                    subdue: true
                );
                UprisingResolver resolver = CreateResolver(
                    outcomeGame,
                    new QueueRNG(0, (roll + 0.5) / (config.Uprising.SubdueOwnedSupportRange + 1))
                );
                resolver.TryExecuteMission(outcomeMission, out List<GameResult> results);
                if (
                    results.OfType<MissionCompletedResult>().Single().Outcome
                    == MissionOutcome.Success
                )
                    successes++;
            }
            Assert.AreEqual(
                successes / (config.Uprising.SubdueOwnedSupportRange + 1d),
                probability,
                0.000001
            );
        }

        [Test]
        public void GetCompletionProbability_ObservedSnapshot_DoesNotMutateSupportGarrisonOrTimers()
        {
            (GameRoot game, Planet planet, Mission mission) = BuildScene(CreateConfig(), 1, 60);
            Dictionary<string, int> support = new Dictionary<string, int>(planet.PopularSupport);
            Regiment regiment = planet.GetAllRegiments().Single();

            new UprisingMissionQueries(game).GetCompletionProbability(mission, planet);

            CollectionAssert.AreEquivalent(support, planet.PopularSupport);
            Assert.AreSame(regiment, planet.GetAllRegiments().Single());
            Assert.IsFalse(planet.IsInUprising);
            Assert.AreEqual(0, planet.NextUprisingIncidentTick);
            Assert.AreSame(planet, mission.GetParent());
        }

        /// <summary>
        /// Loads configured uprising rules and guarantees the isolated participant skill roll.
        /// </summary>
        /// <returns>The scenario configuration.</returns>
        private static GameConfig CreateConfig()
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
        private static (GameRoot game, Planet planet, Mission mission) BuildScene(
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
        private static UprisingResolver CreateResolver(GameRoot game, QueueRNG rng)
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
