using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public sealed class GameResultPipelineTests
    {
        private GameResultBus _bus;
        private MessageCommands _commands;
        private GameRoot _game;
        private MessageObserver _messages;
        private GameResultPipeline _pipeline;
        private Faction _recipient;

        /// <summary>
        /// Creates a delivery pipeline with no authored automatic messages.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestConfig.Create());
            _recipient = new Faction { InstanceID = "recipient" };
            _game.GetFactions().Add(_recipient);
            _bus = new GameResultBus();
            MessageFactory factory = new MessageFactory(null);
            _commands = new MessageCommands(_game, factory);
            _messages = new MessageObserver(_game, factory, _commands);
            _pipeline = new GameResultPipeline(() => _bus, () => _messages);
        }

        [Test]
        public void ProcessResults_ReactionProduced_NotifiesSettledBatch()
        {
            PlanetGarrisonChangedResult initial = new PlanetGarrisonChangedResult();
            PlanetUprisingStartedResult reaction = new PlanetUprisingStartedResult();
            _bus.Subscribe<PlanetGarrisonChangedResult>(_ => new List<GameResult> { reaction });
            IReadOnlyList<GameResult> observed = null;
            _pipeline.ResultsResolved += results => observed = results;

            _pipeline.ProcessResults(new[] { initial }, false);

            CollectionAssert.AreEqual(new GameResult[] { initial, reaction }, observed);
        }

        [Test]
        public void ProcessResults_MessagesDeferred_PreservesPresentationOrder()
        {
            List<string> calls = new List<string>();
            _pipeline.ResultsResolved += _ => calls.Add("results");
            _pipeline.PlanetaryAssaultsResolved += _ => calls.Add("assaults");
            _pipeline.VictoriesResolved += _ => calls.Add("victories");
            _pipeline.HeadquartersLost += _ => calls.Add("headquarters");
            _pipeline.VictoryDeclared += _ => calls.Add("victory");
            _pipeline.MessageDelivered += _ => calls.Add("message");

            _pipeline.ProcessResults(
                new GameResult[]
                {
                    new PlanetaryAssaultResult(),
                    new HeadquartersCapturedResult(),
                    new VictoryResult(),
                },
                false
            );

            CollectionAssert.AreEqual(
                new[] { "results", "assaults", "victories", "headquarters", "victory" },
                calls
            );
        }

        [Test]
        public void ProcessResults_PresentationThrows_SkipsLaterNotifications()
        {
            InvalidOperationException failure = new InvalidOperationException(
                "Presentation failed."
            );
            _pipeline.ResultsResolved += _ => throw failure;
            int victories = 0;
            _pipeline.VictoryDeclared += _ => victories++;

            Assert.Throws<InvalidOperationException>(() =>
                _pipeline.ProcessResults(new[] { new VictoryResult() }, false)
            );

            Assert.AreEqual(0, victories);
        }

        [Test]
        public void ProcessResults_BusReplaced_UsesCurrentBus()
        {
            int calls = 0;
            _bus = new GameResultBus();
            _bus.Subscribe<PlanetGarrisonChangedResult>(_ =>
            {
                calls++;
            });

            _pipeline.ProcessResults(new[] { new PlanetGarrisonChangedResult() }, false);

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void ProcessImmediate_Bombardment_NotifiesAfterSettledResults()
        {
            List<string> calls = new List<string>();
            _pipeline.ResultsResolved += _ => calls.Add("results");
            _pipeline.BombardmentCompleted += _ => calls.Add("bombardment");

            _pipeline.ProcessImmediate(
                new[] { new BombardmentResult { SourceEventInstanceID = "authored" } }
            );

            CollectionAssert.AreEqual(new[] { "results", "bombardment" }, calls);
        }

        [Test]
        public void ProcessResults_EmptyBatch_DoesNotNotifyPresentation()
        {
            int calls = 0;
            _pipeline.ResultsResolved += _ => calls++;
            _pipeline.PlanetaryAssaultsResolved += _ => calls++;
            _pipeline.VictoriesResolved += _ => calls++;
            _pipeline.HeadquartersLost += _ => calls++;
            _pipeline.VictoryDeclared += _ => calls++;
            _pipeline.MessageDelivered += _ => calls++;

            _pipeline.ProcessResults(Array.Empty<GameResult>());

            Assert.AreEqual(0, calls);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ProcessResults_DirectDelivery_NotifiesExactlyOnce(bool processMessages)
        {
            MessageDeliveredResult delivery = DeliverDirectMessage("direct");
            List<MessageDeliveredResult> observed = new List<MessageDeliveredResult>();
            _pipeline.MessageDelivered += observed.Add;

            _pipeline.ProcessResults(new[] { delivery }, processMessages);

            CollectionAssert.AreEqual(new[] { delivery }, observed);
            Assert.AreSame(delivery.Message, _recipient.Messages[MessageType.Chat].Single());
        }

        [Test]
        public void ProcessResults_DirectDelivery_NotifiesAfterSettledResults()
        {
            List<string> calls = new List<string>();
            MessageDeliveredResult delivery = DeliverDirectMessage("direct");
            _pipeline.ResultsResolved += _ => calls.Add("results");
            _pipeline.MessageDelivered += _ => calls.Add("message");

            _pipeline.ProcessResults(new[] { delivery });

            CollectionAssert.AreEqual(new[] { "results", "message" }, calls);
        }

        [Test]
        public void ProcessResults_AutomaticDelivery_NotifiesExactlyOnce()
        {
            ManufacturingIdleResult source = ConfigureAutomaticMessage();
            List<MessageDeliveredResult> observed = new List<MessageDeliveredResult>();
            _pipeline.MessageDelivered += observed.Add;

            _pipeline.ProcessResults(new[] { source });

            Assert.AreEqual(1, observed.Count);
            Assert.AreSame(
                _recipient.Messages[MessageType.Manufacturing].Single(),
                observed[0].Message
            );
        }

        [Test]
        public void ProcessResults_MixedDeliveries_NotifiesDirectThenAutomaticWithoutDuplicates()
        {
            ManufacturingIdleResult source = ConfigureAutomaticMessage();
            MessageDeliveredResult direct = DeliverDirectMessage("direct");
            List<MessageDeliveredResult> observed = new List<MessageDeliveredResult>();
            _pipeline.MessageDelivered += observed.Add;

            _pipeline.ProcessResults(new GameResult[] { direct, source });

            Assert.AreEqual(2, observed.Count);
            Assert.AreSame(direct, observed[0]);
            Assert.AreSame(
                _recipient.Messages[MessageType.Manufacturing].Single(),
                observed[1].Message
            );
            Assert.AreEqual(1, _recipient.Messages[MessageType.Chat].Count);
        }

        [Test]
        public void ProcessResults_ReactionCreatesDirectDelivery_NotifiesSettledDeliveryOnce()
        {
            MessageDeliveredResult direct = null;
            _bus.Subscribe<PlanetGarrisonChangedResult>(_ =>
            {
                direct = DeliverDirectMessage("reaction");
                return new List<GameResult> { direct };
            });
            List<MessageDeliveredResult> observed = new List<MessageDeliveredResult>();
            _pipeline.MessageDelivered += observed.Add;

            _pipeline.ProcessResults(new[] { new PlanetGarrisonChangedResult() });

            CollectionAssert.AreEqual(new[] { direct }, observed);
            Assert.AreEqual(1, _recipient.Messages[MessageType.Chat].Count);
        }

        [Test]
        public void ProcessResults_DirectDeliveryReactionCreatesAutomaticFact_NotifiesEachDeliveryOnce()
        {
            ManufacturingIdleResult automaticSource = ConfigureAutomaticMessage();
            MessageDeliveredResult direct = DeliverDirectMessage("direct");
            int observedByBus = 0;
            _bus.Subscribe<MessageDeliveredResult>(deliveries =>
            {
                observedByBus += deliveries.Count;
                return deliveries.Any(delivery => delivery.Message.Type == MessageType.Chat)
                    ? new List<GameResult> { automaticSource }
                    : new List<GameResult>();
            });
            List<MessageDeliveredResult> observed = new List<MessageDeliveredResult>();
            _pipeline.MessageDelivered += observed.Add;

            _pipeline.ProcessResults(new[] { direct });

            Assert.AreEqual(2, observedByBus);
            Assert.AreEqual(2, observed.Count);
            Assert.AreSame(direct, observed[0]);
            Assert.AreEqual(MessageType.Manufacturing, observed[1].Message.Type);
        }

        [Test]
        public void ProcessResults_DirectNotificationThrows_DoesNotCreateLaterAutomaticMessage()
        {
            ManufacturingIdleResult automaticSource = ConfigureAutomaticMessage();
            MessageDeliveredResult direct = DeliverDirectMessage("direct");
            _pipeline.MessageDelivered += _ =>
                throw new InvalidOperationException("Notification failed.");

            Assert.Throws<InvalidOperationException>(() =>
                _pipeline.ProcessResults(new GameResult[] { direct, automaticSource })
            );

            Assert.AreEqual(1, _recipient.Messages[MessageType.Chat].Count);
            Assert.IsEmpty(_recipient.Messages[MessageType.Manufacturing]);
        }

        /// <summary>
        /// Attaches one direct chat message before handing its factual delivery to the pipeline.
        /// </summary>
        /// <param name="id">The delivered message identity.</param>
        /// <returns>The direct delivery result.</returns>
        private MessageDeliveredResult DeliverDirectMessage(string id)
        {
            return _commands
                .Deliver(
                    new[]
                    {
                        new MessageDelivery
                        {
                            Recipient = _recipient,
                            Message = new StatusMessage(MessageType.Chat, id, id)
                            {
                                InstanceID = id,
                            },
                        },
                    }
                )
                .OfType<MessageDeliveredResult>()
                .Single();
        }

        /// <summary>
        /// Selects a minimal automatic production message without unrelated authored behavior.
        /// </summary>
        /// <returns>The factual result that produces exactly one automatic message.</returns>
        private ManufacturingIdleResult ConfigureAutomaticMessage()
        {
            MessageFactory factory = new MessageFactory(
                new[]
                {
                    new MessageDefinition
                    {
                        ResultType = MessageResultType.ManufacturingIdle,
                        MessageType = MessageType.Manufacturing,
                        ManufacturingType = ManufacturingType.Ship,
                        Subject = "Idle production",
                        Body = "The selected production lane is idle.",
                    },
                }
            );
            _commands = new MessageCommands(_game, factory);
            _messages = new MessageObserver(_game, factory, _commands);
            return new ManufacturingIdleResult
            {
                Faction = _recipient,
                ManufacturingType = ManufacturingType.Ship,
                ProductionPlanet = new Planet { InstanceID = "producer", DisplayName = "Producer" },
            };
        }
    }
}
