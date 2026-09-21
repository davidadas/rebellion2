using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class MessageCommandsTests
    {
        /// <summary>Verifies that null batch input keeps its existing rejection contract.</summary>
        [Test]
        public void Deliver_NullBatch_ThrowsArgumentNullException()
        {
            MessageCommands commands = new(
                new GameRoot(TestConfig.Create()),
                new MessageFactory(null)
            );

            Assert.Throws<ArgumentNullException>(() => commands.Deliver(null));
        }

        /// <summary>Verifies that all authored templates resolve before any message is persisted.</summary>
        [Test]
        public void Deliver_LaterTemplateFails_DoesNotDeliverEarlierMessage()
        {
            GameRoot game = new(TestConfig.Create());
            Faction faction = new() { InstanceID = "faction" };
            game.GetFactions().Add(faction);
            MessageFactory factory = new(null);
            MessageCommands commands = new(game, factory);

            Assert.Throws<InvalidOperationException>(() =>
                commands.Deliver(
                    CreateDeliveries(
                        factory,
                        (faction, "Valid", null, null),
                        (faction, "{unknown}", null, null)
                    )
                )
            );

            Assert.IsTrue(faction.Messages.Values.All(messages => messages.Count == 0));
        }

        /// <summary>Verifies that failed batch preparation does not mutate a supplied combat report.</summary>
        [Test]
        public void Deliver_LaterTemplateFails_PreservesSuppliedReport()
        {
            GameRoot game = new(TestConfig.Create());
            Faction faction = new() { InstanceID = "faction" };
            game.GetFactions().Add(faction);
            CombatReport report = new() { Title = "Before", Body = "Original" };
            MessageFactory factory = new(null);
            MessageCommands commands = new(game, factory);

            Assert.Throws<InvalidOperationException>(() =>
                commands.Deliver(
                    CreateDeliveries(
                        factory,
                        (faction, "After", "Replacement", report),
                        (faction, "{unknown}", null, null)
                    )
                )
            );

            Assert.AreEqual("Before", report.Title);
            Assert.AreEqual("Original", report.Body);
        }

        /// <summary>Verifies that failed attachment stops delivery before a later report is modified.</summary>
        [Test]
        public void Deliver_FirstAttachmentFails_PreservesLaterReport()
        {
            GameRoot game = new(TestConfig.Create());
            Faction firstRecipient = new() { InstanceID = "first" };
            firstRecipient.Messages[MessageType.Conflict] = null;
            Faction secondRecipient = new() { InstanceID = "second" };
            CombatReport report = new()
            {
                Title = "Before",
                Body = "Original",
                CreatedTick = 17,
            };
            MessageFactory factory = new(null);
            MessageCommands commands = new(game, factory);

            Assert.Throws<NullReferenceException>(() =>
                commands.Deliver(
                    CreateDeliveries(
                        factory,
                        (firstRecipient, "First", null, null),
                        (secondRecipient, "After", "Replacement", report)
                    )
                )
            );

            Assert.AreEqual("Before", report.Title);
            Assert.AreEqual("Original", report.Body);
            Assert.AreEqual(17, report.CreatedTick);
        }

        /// <summary>Verifies that delivery retains event provenance and uses the current game tick.</summary>
        [Test]
        public void DeliverAuthored_AuthoredMessage_PreservesProvenanceAndCurrentTick()
        {
            GameRoot game = new(TestConfig.Create()) { CurrentTick = 42 };
            Faction faction = new() { InstanceID = "faction" };
            game.GetFactions().Add(faction);
            MessageCommands commands = new(game, new MessageFactory(null));

            MessageDeliveredResult delivery = (MessageDeliveredResult)
                commands
                    .DeliverAuthored(
                        new MessageDefinition
                        {
                            MessageType = MessageType.Mission,
                            Subject = "Authored",
                        },
                        faction,
                        sourceEventInstanceID: "event"
                    )
                    .Single();

            Assert.AreEqual("event", delivery.SourceEventInstanceID);
            Assert.AreEqual(42, delivery.Tick);
            Assert.AreEqual(42, delivery.Message.CreatedTick);
        }

        /// <summary>Verifies that a supplied combat report is the delivered message.</summary>
        [Test]
        public void Deliver_WithCombatReport_DeliversReportAsMessage()
        {
            GameRoot game = new(new GameConfig()) { CurrentTick = 42 };
            Faction faction = new() { InstanceID = "alliance" };
            game.GetFactions().Add(faction);
            CombatReport report = new()
            {
                CombatType = CombatReportType.SpaceBattle,
                PlanetName = "Yavin",
            };
            MessageFactory factory = new(null);
            MessageCommands commands = new(game, factory);

            List<GameResult> results = commands.Deliver(
                CreateDeliveries(factory, (faction, "Battle at Yavin", "Victory", report))
            );

            Message deliveredMessage = faction.Messages[MessageType.Conflict].Single();
            Assert.AreSame(report, deliveredMessage);
            Assert.AreSame(report, ((MessageDeliveredResult)results.Single()).Message);
            Assert.AreEqual("Battle at Yavin", report.Title);
            Assert.AreEqual("Victory", report.Body);
            Assert.AreEqual(42, report.CreatedTick);
        }

        /// <summary>
        /// Verifies messages older than retention removes expired messages.
        /// </summary>
        [Test]
        public void ProcessTick_MessagesOlderThanRetention_RemovesExpiredMessages()
        {
            GameConfig config = TestConfig.Create();
            config.Messages.RetentionTicks = 300;
            GameRoot game = new GameRoot(config) { CurrentTick = 401 };
            Faction faction = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(faction);
            Message expired = new StatusMessage(MessageType.Conflict, "Expired")
            {
                CreatedTick = 100,
            };
            Message retained = new StatusMessage(MessageType.Conflict, "Retained")
            {
                CreatedTick = 101,
            };
            faction.AddMessage(expired);
            faction.AddMessage(retained);
            MessageCommands messageSystem = new MessageCommands(
                game,
                new MessageFactory(new List<MessageDefinition>())
            );

            messageSystem.ProcessTick();

            CollectionAssert.AreEqual(new[] { retained }, faction.Messages[MessageType.Conflict]);
        }

        /// <summary>Prepares authored deliveries lazily so commands must finish enumeration before attachment.</summary>
        /// <param name="factory">The production template factory.</param>
        /// <param name="items">The recipients, text, and optional reports to prepare.</param>
        /// <returns>The prepared deliveries in input order.</returns>
        private static IEnumerable<MessageDelivery> CreateDeliveries(
            MessageFactory factory,
            params (Faction Recipient, string Subject, string Body, Message Report)[] items
        )
        {
            foreach (var item in items)
            {
                MessageDelivery delivery = factory.CreateAuthoredMessage(
                    new MessageDefinition
                    {
                        MessageType = MessageType.Conflict,
                        Subject = item.Subject,
                        Body = item.Body,
                    },
                    item.Recipient
                );
                delivery.ExistingMessage = item.Report;
                yield return delivery;
            }
        }
    }
}
