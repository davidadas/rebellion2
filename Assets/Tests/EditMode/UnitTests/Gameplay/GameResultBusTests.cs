using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
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
        public void ObserveAll_CompletedReactionWaves_ReceivesCompleteSettledBatch()
        {
            GameResultBus bus = new GameResultBus();
            IReadOnlyList<GameResult> observedResults = null;
            bus.Subscribe<PlanetGarrisonChangedResult>(_ => new List<GameResult>
            {
                new PlanetUprisingStartedResult(),
            });
            bus.ObserveAll(results => observedResults = results);

            bus.Publish(new PlanetGarrisonChangedResult());

            CollectionAssert.AreEqual(
                new[] { typeof(PlanetGarrisonChangedResult), typeof(PlanetUprisingStartedResult) },
                observedResults.Select(result => result.GetType())
            );
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
            processor.SubscribeAfterReactions<PlanetGarrisonChangedResult>(_ =>
                calls.Add("after reactions")
            );
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

        [Test]
        public void Publish_AfterReactions_WaitsForOrdinaryFollowUps()
        {
            GameResultBus bus = new();
            PlanetGarrisonChangedResult initial = new();
            PlanetUprisingStartedResult followUp = new();
            List<string> calls = new();
            List<IReadOnlyList<GameResult>> completedBatches = new();
            bus.Subscribe<PlanetGarrisonChangedResult>(_ =>
            {
                calls.Add("initial reaction");
                return new List<GameResult> { followUp };
            });
            bus.Subscribe<PlanetUprisingStartedResult>(_ => calls.Add("follow-up reaction"));
            bus.SubscribeAfterReactions<GameResult>(results =>
            {
                calls.Add("after reactions");
                completedBatches.Add(results);
            });
            bus.ObserveAll(_ => calls.Add("observer"));

            List<GameResult> results = bus.Publish(initial);

            CollectionAssert.AreEqual(
                new[] { "initial reaction", "follow-up reaction", "after reactions", "observer" },
                calls
            );
            Assert.AreEqual(1, completedBatches.Count);
            CollectionAssert.AreEqual(new GameResult[] { initial, followUp }, completedBatches[0]);
            CollectionAssert.AreEqual(new GameResult[] { initial, followUp }, results);
        }

        [Test]
        public void Publish_AfterReactionCallbacks_CombinesFollowUpsAfterBothFinish()
        {
            GameResultBus bus = new();
            MissionCompletedResult first = new();
            ForceExperienceResult second = new();
            List<string> calls = new();
            List<IReadOnlyList<GameResult>> ordinaryBatches = new();
            IReadOnlyList<GameResult> nestedResults = null;
            bus.Subscribe<GameResult>(results => ordinaryBatches.Add(results));
            bus.SubscribeAfterReactions<PlanetGarrisonChangedResult>(_ =>
            {
                calls.Add("first callback");
                nestedResults = bus.Publish(first);
                calls.Add("first callback finished");
            });
            bus.SubscribeAfterReactions<PlanetGarrisonChangedResult>(_ =>
            {
                calls.Add("second callback");
                return new List<GameResult> { second };
            });
            bus.Subscribe<MissionCompletedResult>(_ => calls.Add("first follow-up"));
            bus.Subscribe<ForceExperienceResult>(_ => calls.Add("second follow-up"));
            bus.ObserveAll(_ => calls.Add("observer"));

            bus.Publish(new PlanetGarrisonChangedResult());

            CollectionAssert.AreEqual(
                new[]
                {
                    "first callback",
                    "first callback finished",
                    "second callback",
                    "first follow-up",
                    "second follow-up",
                    "observer",
                },
                calls
            );
            Assert.IsEmpty(nestedResults);
            Assert.AreEqual(2, ordinaryBatches.Count);
            CollectionAssert.AreEqual(new GameResult[] { first, second }, ordinaryBatches[1]);
        }

        [Test]
        public void Publish_AfterReactionFollowUps_DeliversOnlyNewFactsToCompletionPhase()
        {
            GameResultBus bus = new();
            PlanetGarrisonChangedResult initial = new();
            MissionCompletedResult followUp = new();
            ForceExperienceResult ordinaryFollowUp = new();
            List<IReadOnlyList<GameResult>> completedBatches = new();
            List<IReadOnlyList<GameResult>> observations = new();
            List<GameResult> ordinaryDeliveries = new();
            bus.Subscribe<GameResult>(results => ordinaryDeliveries.AddRange(results));
            bus.Subscribe<MissionCompletedResult>(_ => new List<GameResult> { ordinaryFollowUp });
            bus.SubscribeAfterReactions<GameResult>(results => completedBatches.Add(results));
            bus.SubscribeAfterReactions<PlanetGarrisonChangedResult>(_ => new List<GameResult>
            {
                followUp,
            });
            bus.ObserveAll(results => observations.Add(results));

            List<GameResult> results = bus.Publish(initial);

            Assert.AreEqual(2, completedBatches.Count);
            CollectionAssert.AreEqual(new[] { initial }, completedBatches[0]);
            CollectionAssert.AreEqual(
                new GameResult[] { followUp, ordinaryFollowUp },
                completedBatches[1]
            );
            GameResult[] expected = { initial, followUp, ordinaryFollowUp };
            CollectionAssert.AreEqual(expected, ordinaryDeliveries);
            CollectionAssert.AreEqual(expected, results);
            Assert.AreEqual(1, observations.Count);
            CollectionAssert.AreEqual(expected, observations[0]);
        }

        [Test]
        public void Publish_ObserverPublishesFollowUp_CompletesNewRoundWithoutRedeliveringOriginal()
        {
            GameResultBus bus = new();
            List<string> calls = new();
            bus.SubscribeAfterReactions<PlanetGarrisonChangedResult>(_ =>
                calls.Add("initial complete")
            );
            bus.Observe<PlanetGarrisonChangedResult>(_ =>
            {
                calls.Add("first observer");
                bus.Publish(new MissionCompletedResult());
            });
            bus.Observe<PlanetGarrisonChangedResult>(_ => calls.Add("second observer"));
            bus.Subscribe<MissionCompletedResult>(_ => calls.Add("follow-up reaction"));
            bus.SubscribeAfterReactions<MissionCompletedResult>(_ =>
                calls.Add("follow-up complete")
            );
            bus.Observe<MissionCompletedResult>(_ => calls.Add("follow-up observer"));

            bus.Publish(new PlanetGarrisonChangedResult());

            CollectionAssert.AreEqual(
                new[]
                {
                    "initial complete",
                    "first observer",
                    "second observer",
                    "follow-up reaction",
                    "follow-up complete",
                    "follow-up observer",
                },
                calls
            );
        }

        [Test]
        public void SubscribeAfterReactions_DisposedHandle_DetachesOnlyItsCallback()
        {
            GameResultBus bus = new();
            List<string> calls = new();
            IDisposable removed = bus.SubscribeAfterReactions<PlanetGarrisonChangedResult>(_ =>
                calls.Add("removed")
            );
            bus.SubscribeAfterReactions<PlanetGarrisonChangedResult>(_ => calls.Add("retained"));
            bus.SubscribeAfterReactions<MissionCompletedResult>(_ => calls.Add("unrelated"));

            removed.Dispose();
            removed.Dispose();
            bus.Publish(new PlanetGarrisonChangedResult());

            CollectionAssert.AreEqual(new[] { "retained" }, calls);
        }

        [Test]
        public void Publish_AfterReactionSubscriptionDisposedDuringDelivery_SkipsDetachedCallback()
        {
            GameResultBus bus = new();
            List<string> calls = new();
            IDisposable removed = null;
            bus.SubscribeAfterReactions<PlanetGarrisonChangedResult>(_ => removed.Dispose());
            removed = bus.SubscribeAfterReactions<PlanetGarrisonChangedResult>(_ =>
                calls.Add("removed")
            );
            bus.SubscribeAfterReactions<PlanetGarrisonChangedResult>(_ => calls.Add("retained"));

            bus.Publish(new PlanetGarrisonChangedResult());

            CollectionAssert.AreEqual(new[] { "retained" }, calls);
        }

        [Test]
        public void Publish_AfterReactionThrows_DiscardsQueuedFactsAndResetsDelivery()
        {
            GameResultBus bus = new();
            InvalidOperationException failure = new("completion failed");
            List<GameResult> delivered = new();
            List<GameResult> completed = new();
            List<GameResult> observed = new();
            bus.Subscribe<GameResult>(results => delivered.AddRange(results));
            bus.SubscribeAfterReactions<PlanetGarrisonChangedResult>(_ =>
            {
                bus.Publish(new PlanetUprisingStartedResult());
                throw failure;
            });
            bus.SubscribeAfterReactions<GameResult>(results => completed.AddRange(results));
            bus.ObserveAll(results => observed.AddRange(results));

            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
                bus.Publish(new PlanetGarrisonChangedResult())
            );
            MissionCompletedResult next = new();
            List<GameResult> results = bus.Publish(next);

            Assert.AreSame(failure, thrown);
            Assert.AreEqual(2, delivered.Count);
            Assert.AreSame(next, delivered[1]);
            CollectionAssert.AreEqual(new[] { next }, completed);
            CollectionAssert.AreEqual(new[] { next }, observed);
            CollectionAssert.AreEqual(new[] { next }, results);
        }

        [Test]
        public void SubscribeAfterReactions_NullCallback_RejectsRegistration()
        {
            GameResultBus bus = new();

            Assert.Throws<ArgumentNullException>(() =>
                bus.SubscribeAfterReactions<GameResult>((Action<IReadOnlyList<GameResult>>)null)
            );
            Assert.Throws<ArgumentNullException>(() =>
                bus.SubscribeAfterReactions<GameResult>(
                    (Func<IReadOnlyList<GameResult>, List<GameResult>>)null
                )
            );
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
