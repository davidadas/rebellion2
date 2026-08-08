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
        public void Execute_ValidIDs_PopulatesAttackersAndDefenders()
        {
            GameRoot game = BuildGame(out Planet empPlanet, out Planet rebelPlanet);
            Officer attacker = EntityFactory.CreateOfficer("a1", "empire");
            Officer defender = EntityFactory.CreateOfficer("d1", "rebels");
            game.AttachNode(attacker, empPlanet);
            game.AttachNode(defender, rebelPlanet);

            TriggerDuelAction action = new TriggerDuelAction
            {
                AttackerInstanceIDs = new List<string> { "a1" },
                DefenderInstanceIDs = new List<string> { "d1" },
            };

            List<GameResult> results = action.Execute(game);

            DuelTriggeredResult duel = results.OfType<DuelTriggeredResult>().First();
            Assert.AreEqual(1, duel.Attackers.Count);
            Assert.AreEqual("a1", duel.Attackers[0].InstanceID);
            Assert.AreEqual(1, duel.Defenders.Count);
            Assert.AreEqual("d1", duel.Defenders[0].InstanceID);
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
    }
}
