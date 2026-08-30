using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rebellion.Game.Requests;
using Rebellion.Game.Results;
using Rebellion.Systems;
using UnityEngine;
using UnityEngine.TestTools;

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
        public void Process_UnregisteredRequest_ReturnsNoResults()
        {
            GameRequestDispatcher dispatcher = new GameRequestDispatcher();
            LogAssert.Expect(
                LogType.Error,
                new Regex("Event 'unknown' request 'TestRequest' failed:")
            );

            List<GameResult> results = dispatcher.Process(new[] { new TestRequest() });

            Assert.IsEmpty(results);
        }

        [Test]
        public void Process_HandlerThrows_ProcessesRemainingRequests()
        {
            GameRequestDispatcher dispatcher = new GameRequestDispatcher();
            dispatcher.Subscribe(new TestRequestHandler());
            LogAssert.Expect(
                LogType.Error,
                new Regex("Event 'failed-event' request 'TestRequest' failed:")
            );

            List<GameResult> results = dispatcher.Process(
                new[]
                {
                    new TestRequest
                    {
                        SourceEventInstanceID = "failed-event",
                        Throws = true,
                    },
                    new TestRequest { SourceEventInstanceID = "successful-event" },
                }
            );

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("successful-event", results[0].SourceEventInstanceID);
        }

        private sealed class TestRequest : GameRequest
        {
            public bool Throws { get; set; }
        }

        private sealed class TestRequestHandler : IGameRequestHandler<TestRequest>
        {
            /// <summary>
            /// Produces one factual result for each test request.
            /// </summary>
            public List<GameResult> HandleRequests(IReadOnlyList<TestRequest> requests)
            {
                if (requests[0].Throws)
                    throw new InvalidOperationException("test failure");

                return new List<GameResult> { new PlanetStatChangedResult() };
            }
        }
    }
}
