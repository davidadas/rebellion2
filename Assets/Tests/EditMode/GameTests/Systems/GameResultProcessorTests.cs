using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class GameResultProcessorTests
    {
        [Test]
        public void Process_MatchingResults_InvokesOnlyMatchingHandlersInRegistrationOrder()
        {
            GameResultProcessor processor = new GameResultProcessor();
            List<string> calls = new List<string>();
            int unrelatedCalls = 0;

            processor.Subscribe(
                new RecordingHandler<PlanetUprisingStartedResult>(_ =>
                {
                    unrelatedCalls++;
                    return new List<GameResult>();
                })
            );
            processor.Subscribe(
                new RecordingHandler<PlanetGarrisonChangedResult>(_ =>
                {
                    calls.Add("first");
                    return new List<GameResult>();
                })
            );
            processor.Subscribe(
                new RecordingHandler<PlanetGarrisonChangedResult>(_ =>
                {
                    calls.Add("second");
                    return new List<GameResult>();
                })
            );

            processor.Process(new GameResult[] { null, new PlanetGarrisonChangedResult() });

            CollectionAssert.AreEqual(new[] { "first", "second" }, calls);
            Assert.AreEqual(0, unrelatedCalls);
        }

        [Test]
        public void Process_ReactionResults_ProcessesBreadthFirstWavesInRegistrationOrder()
        {
            GameResultProcessor processor = new GameResultProcessor();
            List<string> calls = new List<string>();

            processor.Subscribe(
                new RecordingHandler<MissionCompletedResult>(_ =>
                {
                    calls.Add("mission");
                    return new List<GameResult> { new ForceExperienceResult() };
                })
            );
            processor.Subscribe(
                new RecordingHandler<PlanetGarrisonChangedResult>(_ =>
                {
                    calls.Add("first garrison");
                    return new List<GameResult> { new PlanetUprisingStartedResult() };
                })
            );
            processor.Subscribe(
                new RecordingHandler<PlanetUprisingStartedResult>(_ =>
                {
                    calls.Add("uprising");
                    return new List<GameResult>();
                })
            );
            processor.Subscribe(
                new RecordingHandler<PlanetGarrisonChangedResult>(_ =>
                {
                    calls.Add("second garrison");
                    return new List<GameResult> { new MissionCompletedResult() };
                })
            );
            processor.Subscribe(
                new RecordingHandler<ForceExperienceResult>(_ =>
                {
                    calls.Add("force");
                    return new List<GameResult>();
                })
            );

            List<GameResult> results = processor.Process(
                new GameResult[] { new PlanetGarrisonChangedResult() }
            );

            CollectionAssert.AreEqual(
                new[] { "first garrison", "second garrison", "mission", "uprising", "force" },
                calls
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    typeof(PlanetGarrisonChangedResult),
                    typeof(PlanetUprisingStartedResult),
                    typeof(MissionCompletedResult),
                    typeof(ForceExperienceResult),
                },
                results.Select(result => result.GetType())
            );
        }

        [Test]
        public void Process_Observers_ReceiveMatchingResultsAfterAllReactionWaves()
        {
            GameResultProcessor processor = new GameResultProcessor();
            List<string> calls = new List<string>();
            IReadOnlyList<PlanetUprisingStartedResult> observedResults = null;
            int unrelatedObservations = 0;

            processor.Subscribe(
                new RecordingHandler<PlanetGarrisonChangedResult>(_ =>
                {
                    calls.Add("handler");
                    return new List<GameResult> { null, new PlanetUprisingStartedResult() };
                })
            );
            processor.Observe<PlanetUprisingStartedResult>(results =>
            {
                calls.Add("observer");
                observedResults = results;
            });
            processor.Observe<MissionCompletedResult>(_ => unrelatedObservations++);

            List<GameResult> results = processor.Process(
                new GameResult[] { new PlanetGarrisonChangedResult() }
            );

            CollectionAssert.AreEqual(new[] { "handler", "observer" }, calls);
            Assert.AreEqual(1, observedResults.Count);
            Assert.AreSame(results[1], observedResults[0]);
            Assert.AreEqual(0, unrelatedObservations);
            Assert.AreEqual(2, results.Count);
        }

        private sealed class RecordingHandler<T> : IGameResultHandler<T>
            where T : GameResult
        {
            private readonly Func<IReadOnlyList<T>, List<GameResult>> _handle;

            internal RecordingHandler(Func<IReadOnlyList<T>, List<GameResult>> handle)
            {
                _handle = handle;
            }

            public List<GameResult> HandleResults(IReadOnlyList<T> results)
            {
                return _handle(results);
            }
        }
    }
}
