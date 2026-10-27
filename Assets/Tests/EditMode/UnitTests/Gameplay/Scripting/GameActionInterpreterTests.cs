using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Scripting;
using Rebellion.Game.Scripting.Events;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Gameplay.Scripting;
using Rebellion.Util.Random;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rebellion.Tests.Gameplay.Scripting
{
    [TestFixture]
    public class GameActionInterpreterTests
    {
        [Test]
        public void ExecuteAction_IfActionEventVariable_SelectsBranchAndPersistsMutation()
        {
            GameRoot game = BuildGame(out _, out _);
            game.EventRuntime.SetVariable("luke.stage", 2);
            IfAction action = new IfAction
            {
                Conditionals = new List<GameConditional>
                {
                    new EvaluateEventVariableConditional
                    {
                        Key = "luke.stage",
                        Comparison = ComparisonOperator.GreaterThanOrEqual,
                        CompareTo = 2,
                    },
                },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction
                    {
                        Key = "luke.stage",
                        Operation = EventVariableOperation.Add,
                        Operand = 1,
                    },
                },
                Else = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "wrong", Operand = 1 },
                },
            };

            List<GameResult> results = action.Execute(game, new FixedRandomProvider(new[] { 0d }));

            Assert.IsEmpty(results);
            Assert.AreEqual(3, game.EventRuntime.GetVariable("luke.stage"));
            Assert.AreEqual(0, game.EventRuntime.GetVariable("wrong"));
        }

        [Test]
        public void ExecuteAction_PerformSkillCheckSuccessfulRoll_ExecutesSuccessActions()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.SetBaseRating(SkillRating.Combat, 50);
            game.AttachNode(luke, rebelPlanet);
            luke.IsEnabled = false;
            game.Config.ProbabilityTables.Mission.Rescue = new Dictionary<int, int> { [50] = 60 };
            PerformSkillCheckAction action = new PerformSkillCheckAction
            {
                OfficerInstanceID = luke.InstanceID,
                Rating = SkillRating.Combat,
                ProbabilityTable = RescueMission.MissionTypeID,
                OnSuccess = new List<GameAction>
                {
                    new SetEventVariableAction
                    {
                        Key = "result",
                        Operation = EventVariableOperation.Set,
                        Operand = 1,
                    },
                },
                OnFailure = new List<GameAction>
                {
                    new SetEventVariableAction
                    {
                        Key = "result",
                        Operation = EventVariableOperation.Set,
                        Operand = -1,
                    },
                },
            };

            action.Execute(game, new FixedRNG(0.59));

            Assert.AreEqual(1, game.EventRuntime.GetVariable("result"));
        }

        [Test]
        public void ExecuteAction_PerformSkillCheckFailedRoll_ExecutesFailureActions()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.SetBaseRating(SkillRating.Combat, 50);
            game.AttachNode(luke, rebelPlanet);
            game.Config.ProbabilityTables.Mission.Rescue = new Dictionary<int, int> { [50] = 60 };
            PerformSkillCheckAction action = new PerformSkillCheckAction
            {
                OfficerInstanceID = luke.InstanceID,
                Rating = SkillRating.Combat,
                ProbabilityTable = RescueMission.MissionTypeID,
                OnSuccess = new List<GameAction>(),
                OnFailure = new List<GameAction>
                {
                    new SetEventVariableAction
                    {
                        Key = "result",
                        Operation = EventVariableOperation.Set,
                        Operand = -1,
                    },
                },
            };

            action.Execute(game, new FixedRNG(0.60));

            Assert.AreEqual(-1, game.EventRuntime.GetVariable("result"));
        }

        [Test]
        public void ExecuteAction_PerformSkillCheckInjuredOfficer_UsesEffectiveRating()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.SetBaseRating(SkillRating.Combat, 50);
            luke.InjuryPoints = 20;
            game.AttachNode(luke, rebelPlanet);
            game.Config.ProbabilityTables.Mission.Rescue = new Dictionary<int, int>
            {
                [0] = 0,
                [50] = 100,
            };
            PerformSkillCheckAction action = new PerformSkillCheckAction
            {
                OfficerInstanceID = luke.InstanceID,
                Rating = SkillRating.Combat,
                ProbabilityTable = RescueMission.MissionTypeID,
                OnFailure = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "failed", Operand = 1 },
                },
            };

            action.Execute(game, new FixedRNG(0.5));

            Assert.AreEqual(1, game.EventRuntime.GetVariable("failed"));
        }

        [Test]
        public void ExecuteAction_PerformSkillCheckNegativeRatingMultiplier_UsesScaledScore()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer han = EntityFactory.CreateOfficer("han", "rebels");
            han.SetBaseRating(SkillRating.Combat, 50);
            game.AttachNode(han, rebelPlanet);
            game.Config.ProbabilityTables.Mission.Abduction = new Dictionary<int, int>
            {
                [-51] = 0,
                [-50] = 100,
            };
            PerformSkillCheckAction action = new PerformSkillCheckAction
            {
                OfficerInstanceID = han.InstanceID,
                Rating = SkillRating.Combat,
                ProbabilityTable = AbductionMission.MissionTypeID,
                RatingMultiplier = -1,
                OnSuccess = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "succeeded", Operand = 1 },
                },
            };

            List<GameResult> results = action.Execute(game, new FixedRNG(0.99));

            Assert.AreEqual(1, game.EventRuntime.GetVariable("succeeded"));
            Assert.IsEmpty(results);
        }

        [Test]
        public void ExecuteAction_PerformSkillCheckMissingOfficer_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out _, out _);
            PerformSkillCheckAction action = new PerformSkillCheckAction
            {
                OfficerInstanceID = "missing",
                Rating = SkillRating.Combat,
                ProbabilityTable = RescueMission.MissionTypeID,
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            StringAssert.Contains("could not resolve officer", exception.Message);
        }

        [Test]
        public void ExecuteAction_PerformSkillCheckMissingProbabilityTable_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);
            PerformSkillCheckAction action = new PerformSkillCheckAction
            {
                OfficerInstanceID = luke.InstanceID,
                Rating = SkillRating.Combat,
                ProbabilityTable = "missing",
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            StringAssert.Contains("could not resolve probability table", exception.Message);
        }

        [Test]
        public void ExecuteAction_RollChanceRolledProbability_ExecutesActionsOnSuccess()
        {
            GameRoot game = BuildGame(out _, out _);
            RollChanceAction action = new RollChanceAction
            {
                RollDouble = new RollDouble { Minimum = 0.7, Maximum = 0.8 },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "success", Operand = 1 },
                },
            };

            action.Execute(game, new SequenceRNG(doubleValues: new[] { 0.5, 0.6 }));

            Assert.AreEqual(1, game.EventRuntime.GetVariable("success"));
        }

        [Test]
        public void ExecuteAction_RollChanceNaNProbability_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out _, out _);
            RollChanceAction action = new RollChanceAction { Probability = double.NaN };

            TestDelegate execute = () => action.Execute(game);

            Assert.Throws<InvalidOperationException>(execute);
        }

        [Test]
        public void ExecuteAction_RollChanceFailedProbability_DoesNotExecuteActions()
        {
            GameRoot game = BuildGame(out _, out _);
            RollChanceAction action = new RollChanceAction
            {
                Probability = 0.25,
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "failure", Operand = 1 },
                },
            };

            action.Execute(game, new SequenceRNG(doubleValues: new[] { 0.5 }));

            Assert.Zero(game.EventRuntime.GetVariable("failure"));
        }

        [Test]
        public void ExecuteAction_RollOutcomeWeightedSelection_ExecutesEveryActionInSelectedOutcome()
        {
            GameRoot game = BuildGame(out _, out _);
            RollOutcomeAction action = new RollOutcomeAction
            {
                Outcomes = new List<RandomOutcome>
                {
                    new RandomOutcome
                    {
                        Weight = 1,
                        Actions = new List<GameAction>
                        {
                            new SetEventVariableAction { Key = "wrong", Operand = 1 },
                        },
                    },
                    new RandomOutcome
                    {
                        Weight = 3,
                        Actions = new List<GameAction>
                        {
                            new SetEventVariableAction { Key = "first", Operand = 1 },
                            new SetEventVariableAction { Key = "second", Operand = 2 },
                        },
                    },
                },
            };

            action.Execute(game, new SequenceRNG(new[] { 3 }));

            Assert.Zero(game.EventRuntime.GetVariable("wrong"));
            Assert.AreEqual(1, game.EventRuntime.GetVariable("first"));
            Assert.AreEqual(2, game.EventRuntime.GetVariable("second"));
        }

        [Test]
        public void ExecuteActions_ActionThrows_ExecutesRemainingActions()
        {
            GameRoot game = new GameRoot();
            GameEvent gameEvent = new GameEvent { InstanceID = "test-event" };
            GameEvaluationContext evaluation = new GameEvaluationContext(
                gameEvent?.InstanceID,
                sourceEventInstanceID: gameEvent?.InstanceID,
                sourceKind: "Event"
            );
            GameActionContext context = new GameActionContext(game, game.Random, evaluation);
            SetEventVariableAction firstAction = new SetEventVariableAction
            {
                Key = "first",
                Operand = 1,
            };
            SetEventVariableAction finalAction = new SetEventVariableAction
            {
                Key = "last",
                Operand = 1,
            };
            List<GameAction> actions = new List<GameAction>
            {
                firstAction,
                new SetEventVariableAction { Key = "invalid" },
                finalAction,
            };
            LogAssert.Expect(
                LogType.Error,
                new Regex("Event 'test-event' action 'SetEventVariableAction' failed:")
            );

            GameActionInterpreter.ExecuteActions(
                actions,
                context,
                new GameActionCommands(context.Game, context.Random)
            );

            Assert.AreEqual(1, game.EventRuntime.GetVariable("first"));
            Assert.AreEqual(1, game.EventRuntime.GetVariable("last"));
        }

        [Test]
        public void ExecuteActions_NullAction_ExecutesFollowingAction()
        {
            GameRoot game = new GameRoot();
            GameActionContext context = new GameActionContext(game, game.Random);
            LogAssert.Expect(LogType.Error, new Regex("Event 'unknown' action 'null' failed:"));

            GameActionInterpreter.ExecuteActions(
                new GameAction[]
                {
                    null,
                    new SetEventVariableAction { Key = "after", Operand = 1 },
                },
                context,
                new GameActionCommands(context.Game, context.Random)
            );

            Assert.AreEqual(1, game.EventRuntime.GetVariable("after"));
        }

        [Test]
        public void ExecuteActions_LaterTargetThrows_PreservesEarlierMutation()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer idle = EntityFactory.CreateOfficer("idle", planet.OwnerInstanceID);
            Officer assigned = EntityFactory.CreateOfficer("assigned", planet.OwnerInstanceID);
            DiplomacyMission mission = new DiplomacyMission
            {
                InstanceID = "mission",
                OwnerInstanceID = planet.OwnerInstanceID,
                LocationInstanceID = planet.InstanceID,
            };
            game.AttachNode(idle, planet);
            game.AttachNode(mission, planet);
            game.AttachNode(assigned, mission);
            mission.Initiate(100);
            GameActionContext context = new GameActionContext(game, game.Random);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Event 'unknown' action 'SetNodeStateAction' failed:")
            );

            GameActionInterpreter.ExecuteActions(
                new[]
                {
                    new SetNodeStateAction
                    {
                        State = SceneNodeState.Inactive,
                        Selectors = new List<GameSelector>
                        {
                            new SelectOfficers { InstanceID = idle.InstanceID },
                            new SelectOfficers { InstanceID = assigned.InstanceID },
                        },
                    },
                },
                context,
                new GameActionCommands(context.Game, context.Random)
            );

            Assert.IsFalse(idle.IsEnabled);
            Assert.IsTrue(assigned.IsEnabled);
        }

        [Test]
        public void ExecuteActions_NestedActions_RecordsResultBeforeNextRoll()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet { InstanceID = "planet" };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            GameEvent gameEvent = new GameEvent { InstanceID = "nested" };
            GameEvaluationContext evaluation = new GameEvaluationContext(
                gameEvent?.InstanceID,
                sourceEventInstanceID: gameEvent?.InstanceID,
                sourceKind: "Event"
            );
            RecordingRandom provider = new RecordingRandom(evaluation);
            GameActionContext context = new GameActionContext(game, provider, evaluation);

            GameActionInterpreter.ExecuteActions(
                new[]
                {
                    new IfAction
                    {
                        Actions = new List<GameAction>
                        {
                            new ChangeRawResourceNodesAction
                            {
                                PlanetInstanceID = planet.InstanceID,
                                Amount = 1,
                            },
                            new RollChanceAction { Probability = 0.5 },
                        },
                    },
                },
                context,
                new GameActionCommands(context.Game, context.Random)
            );

            Assert.AreEqual(1, provider.ResultsAtRoll);
        }

        /// <summary>
        /// Builds game.
        /// </summary>
        /// <param name="empirePlanet">Receives the empire planet.</param>
        /// <param name="rebelPlanet">Receives the rebel planet.</param>
        /// <returns>The constructed game.</returns>
        private GameRoot BuildGame(out Planet empirePlanet, out Planet rebelPlanet)
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.Galaxy);
            empirePlanet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            game.AttachNode(empirePlanet, sector);
            rebelPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(rebelPlanet, sector);
            return game;
        }

        private sealed class RecordingRandom : IRandomNumberProvider
        {
            private readonly GameEvaluationContext _evaluation;
            public int ResultsAtRoll { get; private set; } = -1;

            /// <summary>
            /// Captures the evaluation whose local result visibility is observed.
            /// </summary>
            /// <param name="evaluation">The active event evaluation.</param>
            internal RecordingRandom(GameEvaluationContext evaluation)
            {
                _evaluation = evaluation;
            }

            /// <summary>
            /// Records the current local result count before returning the fixed roll.
            /// </summary>
            /// <returns>The fixed probability sample.</returns>
            public double NextDouble()
            {
                ResultsAtRoll = _evaluation.Results.Count;
                return 0;
            }

            /// <summary>
            /// Rejects unexpected integer rolls in this action sequence.
            /// </summary>
            /// <param name="min">The lower bound requested.</param>
            /// <param name="max">The upper bound requested.</param>
            /// <returns>No value; an unexpected roll fails the test.</returns>
            public int NextInt(int min, int max) =>
                throw new InvalidOperationException("Unexpected integer roll.");
        }
    }
}
