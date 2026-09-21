using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public sealed class GameResultPipelineTests
    {
        private GameResultBus _bus;
        private MessageObserver _messages;
        private GameResultPipeline _pipeline;

        /// <summary>Creates a delivery pipeline with no authored automatic messages.</summary>
        [SetUp]
        public void SetUp()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            _bus = new GameResultBus();
            MessageFactory factory = new MessageFactory(null);
            _messages = new MessageObserver(game, factory, new MessageCommands(game, factory));
            _pipeline = new GameResultPipeline(() => _bus, () => _messages);
        }

        /// <summary>Verifies that settled presentation receives the entire reaction batch.</summary>
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

        /// <summary>Verifies the existing presentation order when messages are withheld.</summary>
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

        /// <summary>Verifies that a presentation exception prevents later notifications.</summary>
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

        /// <summary>Verifies that replacement is read at delivery time rather than construction time.</summary>
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

        /// <summary>Verifies that immediate bombardment notification follows result presentation.</summary>
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

        /// <summary>Verifies that processing an empty batch does not invent presentation callbacks.</summary>
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
    }
}
