using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Requests;
using Rebellion.Game.Results;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    /// <summary>
    /// Verifies authoritative request routing independently from factual result reactions.
    /// </summary>
    [TestFixture]
    public sealed class GameRequestDispatcherTests
    {
        [Test]
        public void Process_RegisteredRequest_ReturnsFactsWithSourceEvent()
        {
            GameRequestDispatcher dispatcher = new GameRequestDispatcher();
            dispatcher.Subscribe(new TestRequestHandler());

            List<GameResult> results = dispatcher.Process(
                new[] { new TestRequest { SourceEventInstanceID = "source-event" } }
            );

            Assert.AreEqual("source-event", results[0].SourceEventInstanceID);
        }

        [Test]
        public void Process_UnregisteredRequest_ThrowsInvalidOperationException()
        {
            GameRequestDispatcher dispatcher = new GameRequestDispatcher();

            TestDelegate process = () => dispatcher.Process(new[] { new TestRequest() });

            Assert.Throws<InvalidOperationException>(process);
        }

        private sealed class TestRequest : GameRequest { }

        private sealed class TestRequestHandler : IGameRequestHandler<TestRequest>
        {
            /// <summary>
            /// Produces one factual result for each test request.
            /// </summary>
            public List<GameResult> HandleRequests(IReadOnlyList<TestRequest> requests) =>
                new List<GameResult> { new PlanetStatChangedResult() };
        }
    }
}
