using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class GameResultBusTests
    {
        [Test]
        public void Publish_MatchingResults_InvokesOnlyMatchingHandlersInRegistrationOrder()
        {
            GameResultBus processor = new GameResultBus();
            List<string> calls = new List<string>();
            int unrelatedCalls = 0;

            processor.Subscribe<PlanetUprisingStartedResult>(_ =>
            {
                unrelatedCalls++;
                return new List<GameResult>();
            });
            processor.Subscribe<PlanetGarrisonChangedResult>(_ =>
            {
                calls.Add("first");
                return new List<GameResult>();
            });
            processor.Subscribe<PlanetGarrisonChangedResult>(_ =>
            {
                calls.Add("second");
                return new List<GameResult>();
            });

            processor.Publish(new GameResult[] { null, new PlanetGarrisonChangedResult() });

            CollectionAssert.AreEqual(new[] { "first", "second" }, calls);
            Assert.AreEqual(0, unrelatedCalls);
        }

        [Test]
        public void Publish_ReactionResults_ProcessesBreadthFirstWavesInRegistrationOrder()
        {
            GameResultBus processor = new GameResultBus();
            List<string> calls = new List<string>();

            processor.Subscribe<MissionCompletedResult>(_ =>
            {
                calls.Add("mission");
                return new List<GameResult> { new ForceExperienceResult() };
            });
            processor.Subscribe<PlanetGarrisonChangedResult>(_ =>
            {
                calls.Add("first garrison");
                return new List<GameResult> { new PlanetUprisingStartedResult() };
            });
            processor.Subscribe<PlanetUprisingStartedResult>(_ =>
            {
                calls.Add("uprising");
                return new List<GameResult>();
            });
            processor.Subscribe<PlanetGarrisonChangedResult>(_ =>
            {
                calls.Add("second garrison");
                return new List<GameResult> { new MissionCompletedResult() };
            });
            processor.Subscribe<ForceExperienceResult>(_ =>
            {
                calls.Add("force");
                return new List<GameResult>();
            });

            List<GameResult> results = processor.Publish(
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
        public void Publish_Observers_ReceiveMatchingResultsAfterAllReactionWaves()
        {
            GameResultBus processor = new GameResultBus();
            List<string> calls = new List<string>();
            IReadOnlyList<PlanetUprisingStartedResult> observedResults = null;
            int unrelatedObservations = 0;

            processor.Subscribe<PlanetGarrisonChangedResult>(_ =>
            {
                calls.Add("handler");
                return new List<GameResult> { null, new PlanetUprisingStartedResult() };
            });
            processor.Observe<PlanetUprisingStartedResult>(results =>
            {
                calls.Add("observer");
                observedResults = results;
            });
            processor.Observe<MissionCompletedResult>(_ => unrelatedObservations++);

            List<GameResult> results = processor.Publish(
                new GameResult[] { new PlanetGarrisonChangedResult() }
            );

            CollectionAssert.AreEqual(new[] { "handler", "observer" }, calls);
            Assert.AreEqual(1, observedResults.Count);
            Assert.AreSame(results[1], observedResults[0]);
            Assert.AreEqual(0, unrelatedObservations);
            Assert.AreEqual(2, results.Count);
        }

        [Test]
        public void Publish_MultipleMatchingResults_DeliversOneOrderedBatch()
        {
            GameResultBus processor = new GameResultBus();
            PlanetGarrisonChangedResult first = new PlanetGarrisonChangedResult();
            PlanetGarrisonChangedResult second = new PlanetGarrisonChangedResult();
            List<IReadOnlyList<PlanetGarrisonChangedResult>> batches = new();
            processor.Subscribe<PlanetGarrisonChangedResult>(results =>
            {
                batches.Add(results);
                return null;
            });

            processor.Publish(new GameResult[] { first, new MissionCompletedResult(), second });

            Assert.AreEqual(1, batches.Count);
            CollectionAssert.AreEqual(new[] { first, second }, batches[0]);
        }

        [Test]
        public void Publish_ReactionsFromMultipleHandlers_CombinesNextWave()
        {
            GameResultBus processor = new GameResultBus();
            PlanetUprisingStartedResult first = new PlanetUprisingStartedResult();
            PlanetUprisingStartedResult second = new PlanetUprisingStartedResult();
            List<IReadOnlyList<PlanetUprisingStartedResult>> batches = new();
            processor.Subscribe<PlanetGarrisonChangedResult>(_ => new List<GameResult> { first });
            processor.Subscribe<PlanetGarrisonChangedResult>(_ => new List<GameResult> { second });
            processor.Subscribe<PlanetUprisingStartedResult>(results =>
            {
                batches.Add(results);
                return null;
            });

            processor.Publish(new[] { new PlanetGarrisonChangedResult() });

            Assert.AreEqual(1, batches.Count);
            CollectionAssert.AreEqual(new[] { first, second }, batches[0]);
        }

        [Test]
        public void Publish_HandlerThrows_PropagatesOriginalException()
        {
            GameResultBus processor = new GameResultBus();
            InvalidOperationException failure = new InvalidOperationException("reaction failed");
            processor.Subscribe<PlanetGarrisonChangedResult>(_ => throw failure);

            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
                processor.Publish(new[] { new PlanetGarrisonChangedResult() })
            );

            Assert.AreSame(failure, thrown);
        }

        [Test]
        public void Publish_HandlerThrows_StopsCurrentDelivery()
        {
            GameResultBus processor = new GameResultBus();
            List<string> calls = new();
            processor.Subscribe<PlanetGarrisonChangedResult>(_ =>
                throw new InvalidOperationException("reaction failed")
            );
            processor.Subscribe<PlanetGarrisonChangedResult>(_ =>
            {
                calls.Add("handler");
                return new List<GameResult>();
            });
            processor.Observe<PlanetGarrisonChangedResult>(_ => calls.Add("observer"));

            Assert.Throws<InvalidOperationException>(() =>
                processor.Publish(new[] { new PlanetGarrisonChangedResult() })
            );

            Assert.IsEmpty(calls);
        }

        [Test]
        public void Publish_ObserverThrows_PropagatesOriginalException()
        {
            GameResultBus processor = new GameResultBus();
            InvalidOperationException failure = new InvalidOperationException("observer failed");
            processor.Observe<PlanetGarrisonChangedResult>(_ => throw failure);

            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
                processor.Publish(new[] { new PlanetGarrisonChangedResult() })
            );

            Assert.AreSame(failure, thrown);
        }

        [Test]
        public void Publish_AfterFailedDelivery_ProcessesOnlyNewBatch()
        {
            GameResultBus processor = new GameResultBus();
            List<IReadOnlyList<PlanetGarrisonChangedResult>> batches = new();
            processor.Subscribe<PlanetGarrisonChangedResult>(results =>
            {
                batches.Add(results);
                if (batches.Count == 1)
                    throw new InvalidOperationException("reaction failed");
                return null;
            });
            Assert.Throws<InvalidOperationException>(() =>
                processor.Publish(new[] { new PlanetGarrisonChangedResult() })
            );
            PlanetGarrisonChangedResult next = new PlanetGarrisonChangedResult();

            processor.Publish(new[] { next });

            Assert.AreEqual(2, batches.Count);
            CollectionAssert.AreEqual(new[] { next }, batches[1]);
        }

        [Test]
        public void Publish_DuringReaction_DeliversFollowUpAfterCurrentWave()
        {
            GameResultBus bus = new();
            List<string> calls = new();
            bus.Subscribe<PlanetGarrisonChangedResult>(_ =>
            {
                calls.Add("first");
                bus.Publish(new PlanetUprisingStartedResult());
                calls.Add("first finished");
            });
            bus.Subscribe<PlanetGarrisonChangedResult>(_ => calls.Add("second"));
            bus.Subscribe<PlanetUprisingStartedResult>(_ => calls.Add("uprising"));

            bus.Publish(new PlanetGarrisonChangedResult());

            CollectionAssert.AreEqual(
                new[] { "first", "first finished", "second", "uprising" },
                calls
            );
        }

        [Test]
        public void Publish_DuringMultipleReactions_CombinesFollowUpBatch()
        {
            GameResultBus bus = new();
            PlanetUprisingStartedResult first = new();
            PlanetUprisingStartedResult second = new();
            List<IReadOnlyList<PlanetUprisingStartedResult>> batches = new();
            bus.Subscribe<PlanetGarrisonChangedResult>(_ =>
            {
                bus.Publish(first);
            });
            bus.Subscribe<PlanetGarrisonChangedResult>(_ =>
            {
                bus.Publish(second);
            });
            bus.Subscribe<PlanetUprisingStartedResult>(results => batches.Add(results));

            bus.Publish(new PlanetGarrisonChangedResult());

            Assert.AreEqual(1, batches.Count);
            CollectionAssert.AreEqual(new[] { first, second }, batches[0]);
        }

        [Test]
        public void Publish_DuringObservation_DeliversFollowUpAfterCurrentObservers()
        {
            GameResultBus bus = new();
            List<string> calls = new();
            bus.Observe<PlanetGarrisonChangedResult>(_ =>
            {
                calls.Add("first observer");
                bus.Publish(new PlanetUprisingStartedResult());
                calls.Add("first observer finished");
            });
            bus.Observe<PlanetGarrisonChangedResult>(_ => calls.Add("second observer"));
            bus.Subscribe<PlanetUprisingStartedResult>(_ => calls.Add("uprising"));
            bus.Observe<PlanetUprisingStartedResult>(_ => calls.Add("uprising observer"));

            bus.Publish(new PlanetGarrisonChangedResult());

            CollectionAssert.AreEqual(
                new[]
                {
                    "first observer",
                    "first observer finished",
                    "second observer",
                    "uprising",
                    "uprising observer",
                },
                calls
            );
        }

        [Test]
        public void Subscribe_DisposedHandle_DetachesOnlyItsCallback()
        {
            GameResultBus bus = new();
            List<string> calls = new();
            IDisposable subscription = bus.Subscribe<PlanetGarrisonChangedResult>(_ =>
                calls.Add("removed")
            );
            bus.Subscribe<PlanetGarrisonChangedResult>(_ => calls.Add("retained"));

            subscription.Dispose();
            subscription.Dispose();
            bus.Publish(new PlanetGarrisonChangedResult());

            CollectionAssert.AreEqual(new[] { "retained" }, calls);
        }

        [Test]
        public void Observe_DisposedHandle_DetachesObserver()
        {
            GameResultBus bus = new();
            int observations = 0;
            IDisposable subscription = bus.Observe<PlanetGarrisonChangedResult>(_ =>
                observations++
            );

            subscription.Dispose();
            bus.Publish(new PlanetGarrisonChangedResult());

            Assert.AreEqual(0, observations);
        }

        [Test]
        public void Publish_SubscriptionDisposedDuringDelivery_SkipsDetachedSubscriber()
        {
            GameResultBus bus = new();
            List<string> calls = new();
            IDisposable removed = null;
            bus.Subscribe<PlanetGarrisonChangedResult>(_ => removed.Dispose());
            removed = bus.Subscribe<PlanetGarrisonChangedResult>(_ => calls.Add("removed"));
            bus.Subscribe<PlanetGarrisonChangedResult>(_ => calls.Add("retained"));

            bus.Publish(new PlanetGarrisonChangedResult());

            CollectionAssert.AreEqual(new[] { "retained" }, calls);
        }

        [Test]
        public void Publish_SeparateBus_DoesNotNotifyOtherGame()
        {
            GameResultBus first = new();
            GameResultBus second = new();
            int calls = 0;
            first.Subscribe<PlanetGarrisonChangedResult>(_ => calls++);

            second.Publish(new PlanetGarrisonChangedResult());

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Publish_HandlerThrowsAfterFollowUpQueued_DiscardsUnfinishedDelivery()
        {
            GameResultBus bus = new();
            int uprisingCalls = 0;
            bus.Subscribe<PlanetGarrisonChangedResult>(_ =>
            {
                bus.Publish(new PlanetUprisingStartedResult());
                throw new InvalidOperationException("reaction failed");
            });
            bus.Subscribe<PlanetUprisingStartedResult>(_ => uprisingCalls++);
            Assert.Throws<InvalidOperationException>(() =>
                bus.Publish(new PlanetGarrisonChangedResult())
            );

            bus.Publish(new MissionCompletedResult());

            Assert.AreEqual(0, uprisingCalls);
        }

        [Test]
        public void Publish_InputEnumerationThrows_DoesNotRetainPartialBatch()
        {
            GameResultBus bus = new();
            int garrisonCalls = 0;
            bus.Subscribe<PlanetGarrisonChangedResult>(_ => garrisonCalls++);
            Assert.Throws<InvalidOperationException>(() => bus.Publish(CreateFailedBatch()));

            bus.Publish(new MissionCompletedResult());

            Assert.AreEqual(0, garrisonCalls);
        }

        /// <summary>
        /// Produces an input batch that fails after yielding its first fact.
        /// </summary>
        /// <returns>The partially enumerable result batch.</returns>
        private static IEnumerable<GameResult> CreateFailedBatch()
        {
            yield return new PlanetGarrisonChangedResult();
            throw new InvalidOperationException("input enumeration failed");
        }
    }
}
