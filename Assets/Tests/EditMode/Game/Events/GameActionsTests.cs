using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameActionsTests
    {
        private GameRoot BuildGame(out Planet empPlanet, out Planet rebelPlanet)
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            empPlanet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            game.AttachNode(empPlanet, system);
            rebelPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(rebelPlanet, system);
            return game;
        }

        [Test]
        public void ResolveOfficerEncounter_ValidIDs_EmitsRequest()
        {
            GameRoot game = BuildGame(out Planet empPlanet, out Planet rebelPlanet);
            Officer attacker = EntityFactory.CreateOfficer("a1", "empire");
            Officer defender = EntityFactory.CreateOfficer("d1", "rebels");
            game.AttachNode(attacker, empPlanet);
            game.AttachNode(defender, rebelPlanet);

            ResolveOfficerEncounterAction action = new ResolveOfficerEncounterAction
            {
                EncounteredOfficerInstanceID = "a1",
                OpposingOfficerInstanceID = "d1",
            };

            List<GameResult> results = action.Execute(game);

            OfficerEncounterRequestedResult request = results
                .OfType<OfficerEncounterRequestedResult>()
                .Single();
            Assert.AreSame(attacker, request.EncounteredOfficer);
            Assert.AreSame(defender, request.OpposingOfficer);
        }

        [Test]
        public void NarrativeMessage_RecipientFromSubject_EmitsResolvedResult()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.DisplayName = "Luke Skywalker";
            game.AttachNode(luke, rebelPlanet);
            NarrativeMessageAction action = new NarrativeMessageAction
            {
                SubjectInstanceID = luke.InstanceID,
                MessageType = MessageType.Advice,
                TitleTemplate = "A message for {subject}",
                BodyTemplate = "Report from {location}",
                VoicePath = "Audio/Luke/dialogue",
            };

            NarrativeMessageResult result = action
                .Execute(game)
                .OfType<NarrativeMessageResult>()
                .Single();

            Assert.AreEqual("rebels", result.Recipient.InstanceID);
            Assert.AreSame(luke, result.Subject);
            Assert.AreSame(rebelPlanet, result.Location);
            Assert.AreEqual("Audio/Luke/dialogue", result.VoicePath);
        }

        [Test]
        public void GameEvent_NestedRandomTrigger_UsesOneProviderAndPreservesChildSource()
        {
            GameRoot game = BuildGame(out _, out _);
            GameEvent child = new GameEvent
            {
                InstanceID = "child",
                Actions = new List<GameAction>
                {
                    new NarrativeMessageAction
                    {
                        RecipientFactionInstanceID = "rebels",
                        TitleTemplate = "Child",
                    },
                },
            };
            GameEvent root = new GameEvent
            {
                InstanceID = "root",
                Actions = new List<GameAction>
                {
                    new RandomOutcomeAction
                    {
                        Probability = 1,
                        Actions = new List<GameAction>
                        {
                            new TriggerEventAction { EventInstanceID = child.InstanceID },
                        },
                    },
                },
            };
            game.EventPool.Add(child);
            game.EventPool.Add(root);

            NarrativeMessageResult result = root.Execute(
                    game,
                    new FixedRandomProvider(new[] { 0d })
                )
                .OfType<NarrativeMessageResult>()
                .Single();

            Assert.AreEqual(child.InstanceID, result.SourceEventInstanceID);
        }

        [Test]
        public void ConditionalAction_EventVariable_SelectsBranchAndPersistsMutation()
        {
            GameRoot game = BuildGame(out _, out _);
            game.SetEventVariable("luke.stage", 2);
            ConditionalAction action = new ConditionalAction
            {
                Conditionals = new List<GameConditional>
                {
                    new EventVariableConditional
                    {
                        Key = "luke.stage",
                        Comparison = EventVariableComparison.GreaterThanOrEqual,
                        Value = 2,
                    },
                },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction
                    {
                        Key = "luke.stage",
                        Operation = EventVariableOperation.Add,
                        Value = 1,
                    },
                },
                ElseActions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "wrong", Value = 1 },
                },
            };

            EventVariableChangedResult result = action
                .Execute(game, new FixedRandomProvider(new[] { 0d }))
                .OfType<EventVariableChangedResult>()
                .Single();

            Assert.AreEqual(2, result.PreviousValue);
            Assert.AreEqual(3, result.CurrentValue);
            Assert.AreEqual(3, game.GetEventVariable("luke.stage"));
            Assert.AreEqual(0, game.GetEventVariable("wrong"));
        }

        [Test]
        public void RequestMovement_ValidReferences_EmitsAuthoritativeRequest()
        {
            GameRoot game = BuildGame(out Planet destination, out Planet origin);
            Officer officer = EntityFactory.CreateOfficer("traveler", "rebels");
            game.AttachNode(officer, origin);
            RequestMovementAction action = new RequestMovementAction
            {
                UnitInstanceID = officer.InstanceID,
                DestinationInstanceID = destination.InstanceID,
            };

            UnitMovementRequestedResult result = action
                .Execute(game)
                .OfType<UnitMovementRequestedResult>()
                .Single();

            Assert.AreSame(officer, result.Unit);
            Assert.AreSame(destination, result.Destination);
            Assert.AreSame(origin, officer.GetParent());
        }

        [Test]
        public void StartScriptedTraining_ValidTrainee_EmitsConfiguredRequest()
        {
            GameRoot game = BuildGame(out _, out Planet origin);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, origin);
            StartScriptedTrainingAction action = new StartScriptedTrainingAction
            {
                TraineeInstanceID = luke.InstanceID,
                DurationTicks = 100,
                CompletionBonusPercent = 60,
                CompletionVariableKey = "luke.dagobah.completed",
                CompletionVariableValue = 1,
                DisplayName = "Journey to Dagobah",
            };

            ScriptedTrainingRequestedResult result = action
                .Execute(game)
                .OfType<ScriptedTrainingRequestedResult>()
                .Single();

            Assert.AreSame(luke, result.Trainee);
            Assert.AreEqual(100, result.DurationTicks);
            Assert.AreEqual(60, result.CompletionBonusPercent);
            Assert.AreEqual("luke.dagobah.completed", result.CompletionVariableKey);
        }

        [Test]
        public void StartStoryCapture_ValidTarget_EmitsConfiguredRequest()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer han = EntityFactory.CreateOfficer("han", "rebels");
            game.AttachNode(han, rebelPlanet);
            StartStoryCaptureAction action = new StartStoryCaptureAction
            {
                TargetOfficerInstanceID = han.InstanceID,
                DurationTicks = 1,
                CanEscape = false,
                DisplayName = "Bounty Hunters",
            };

            StoryCaptureRequestedResult result = action
                .Execute(game)
                .OfType<StoryCaptureRequestedResult>()
                .Single();

            Assert.AreSame(han, result.Target);
            Assert.AreEqual(1, result.DurationTicks);
            Assert.IsFalse(result.CanEscape);
            Assert.AreEqual("Bounty Hunters", result.DisplayName);
        }

        [Test]
        public void BountyAttack_ValidOfficer_EmitsBountyResult()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer han = EntityFactory.CreateOfficer("han", "rebels");
            game.AttachNode(han, rebelPlanet);

            BountyAttackResult result = new BountyAttackAction
            {
                OfficerInstanceID = han.InstanceID,
            }
                .Execute(game)
                .OfType<BountyAttackResult>()
                .Single();

            Assert.AreSame(han, result.Officer);
        }

        [Test]
        public void StartStoryRescue_ResolvesAuthoredOfficerReferences()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer han = EntityFactory.CreateOfficer("han", "rebels");
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(han, rebelPlanet);
            game.AttachNode(luke, rebelPlanet);

            StoryRescueRequestedResult result = new StartStoryRescueAction
            {
                CaptiveOfficerInstanceID = han.InstanceID,
                RescuerOfficerInstanceIDs = new List<string> { luke.InstanceID },
                DurationTicks = 1,
                RatingDivisor = 3,
                SuccessCombatBonus = 1,
                SuccessEspionageBonus = 1,
                CaptureRescuerOnFailure = true,
            }
                .Execute(game)
                .OfType<StoryRescueRequestedResult>()
                .Single();

            Assert.AreSame(han, result.Captive);
            Assert.AreSame(luke, result.Rescuers.Single());
            Assert.AreEqual(3, result.RatingDivisor);
            Assert.IsTrue(result.CaptureRescuerOnFailure);
        }

        [Test]
        public void StartStoryPickup_ResolvesCollectorAndPrisonerLocation()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out Planet rebelPlanet);
            Officer vader = EntityFactory.CreateOfficer("vader", "empire");
            Officer han = EntityFactory.CreateOfficer("han", "rebels");
            game.AttachNode(vader, empirePlanet);
            game.AttachNode(han, rebelPlanet);

            List<GameResult> results = new StartStoryPickupAction
            {
                CollectorOfficerInstanceID = vader.InstanceID,
                LocationOfficerInstanceID = han.InstanceID,
                CaptiveFactionInstanceID = "rebels",
                DurationTicks = 1,
                CaptivesCanEscapeAfterPickup = true,
            }.Execute(game);

            StoryPickupRequestedResult request = results
                .OfType<StoryPickupRequestedResult>()
                .Single();
            Assert.AreSame(vader, request.Collector);
            Assert.AreSame(rebelPlanet, request.Location);
            Assert.IsTrue(request.CaptivesCanEscapeAfterPickup);
            Assert.IsTrue(results.OfType<OfficerPickupResult>().Single().InProgress);
        }

        [Test]
        public void StartStoryFinalBattle_ResolvesAuthoredParticipantsAndRules()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            Officer vader = EntityFactory.CreateOfficer("vader", "empire");
            Officer palpatine = EntityFactory.CreateOfficer("palpatine", "empire");
            game.AttachNode(luke, rebelPlanet);
            game.AttachNode(vader, empirePlanet);
            game.AttachNode(palpatine, empirePlanet);

            StoryFinalBattleRequestedResult request = new StartStoryFinalBattleAction
            {
                LukeOfficerInstanceID = luke.InstanceID,
                VaderOfficerInstanceID = vader.InstanceID,
                PalpatineOfficerInstanceID = palpatine.InstanceID,
                CaptorFactionInstanceID = "empire",
                DurationTicks = 1,
                VictoryForceRank = 100,
                MinimumFailureInjury = 1,
                MaximumFailureInjury = 200,
                CaptivesCanEscapeOnVictory = true,
            }
                .Execute(game)
                .OfType<StoryFinalBattleRequestedResult>()
                .Single();

            Assert.AreSame(luke, request.Luke);
            Assert.AreSame(vader, request.Vader);
            Assert.AreSame(palpatine, request.Palpatine);
            Assert.AreEqual(100, request.VictoryForceRank);
            Assert.AreEqual(1, request.MinimumFailureInjury);
            Assert.AreEqual(200, request.MaximumFailureInjury);
        }

        [Test]
        public void IncreaseOfficerForce_RankGapReward_UsesOriginalMaximumFormula()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.ForceValue = 40;
            Officer vader = EntityFactory.CreateOfficer("vader", "empire");
            vader.ForceValue = 100;
            game.AttachNode(luke, rebelPlanet);
            game.AttachNode(vader, empirePlanet);
            IncreaseOfficerForceAction action = new IncreaseOfficerForceAction
            {
                OfficerInstanceID = luke.InstanceID,
                ReferenceOfficerInstanceID = vader.InstanceID,
                MinimumIncrease = 1,
                PositiveRankGapPercent = 25,
                SuppressRankChangeMessage = true,
            };

            ForceExperienceResult result = action
                .Execute(game)
                .OfType<ForceExperienceResult>()
                .Single();

            Assert.AreEqual(15, result.ExperienceGained);
            Assert.AreEqual(55, luke.ForceValue);
            Assert.IsTrue(result.SuppressRankChangeMessage);
        }

        [Test]
        public void ApplyOfficerInjury_InclusiveRange_AppliesRolledSeverity()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);
            ApplyOfficerInjuryAction action = new ApplyOfficerInjuryAction
            {
                OfficerInstanceID = luke.InstanceID,
                MinimumInjury = 1,
                MaximumInjury = 100,
            };

            OfficerInjuredResult result = action
                .Execute(game, new FixedRandomProvider(new[] { 0.49 }))
                .OfType<OfficerInjuredResult>()
                .Single();

            Assert.AreEqual(50, result.Severity);
            Assert.AreEqual(50, luke.InjuryPoints);
        }
    }
}
