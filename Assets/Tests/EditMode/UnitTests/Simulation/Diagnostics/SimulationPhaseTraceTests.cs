using System;
using NUnit.Framework;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    public sealed class SimulationPhaseTraceTests
    {
        /// <summary>
        /// Rejects unbounded or invalid recording capacity before recording starts.
        /// </summary>
        [TestCase(0)]
        [TestCase(-1)]
        public void Start_NonpositiveCapacity_RejectsCapture(int capacity)
        {
            SimulationPhaseTrace trace = new SimulationPhaseTrace();

            Assert.Throws<ArgumentOutOfRangeException>(() => trace.Start(capacity));

            Assert.That(trace.IsRecording, Is.False);
        }

        /// <summary>
        /// Preserves an active capture when another start is requested.
        /// </summary>
        [Test]
        public void Start_AlreadyRecording_RejectsRestartWithoutDiscardingSamples()
        {
            SimulationPhaseTrace trace = new SimulationPhaseTrace();
            trace.Start(1);
            using (trace.Measure(SimulationPhase.Maintenance, 12)) { }

            Assert.Throws<InvalidOperationException>(() => trace.Start(20));

            SimulationTimingCapture capture = trace.Stop();
            Assert.That(capture.Capacity, Is.EqualTo(1));
            Assert.That(capture.Samples.Count, Is.EqualTo(1));
            Assert.That(capture.Samples[0].Tick, Is.EqualTo(12));
        }

        /// <summary>
        /// Prevents reading an unstopped or never-started capture through a synthetic result.
        /// </summary>
        [Test]
        public void Stop_NotRecording_RejectsCapture()
        {
            SimulationPhaseTrace trace = new SimulationPhaseTrace();

            Assert.Throws<InvalidOperationException>(() => trace.Stop());
        }

        /// <summary>
        /// Retains bounded ordered samples and accounts for every overflowed measurement.
        /// </summary>
        [Test]
        public void Stop_BufferOverflow_PreservesCapacityAndReportsDroppedSamples()
        {
            SimulationPhaseTrace trace = new SimulationPhaseTrace();
            trace.Start(2);
            using (trace.Measure(SimulationPhase.Movement, 1601)) { }
            using (trace.Measure(SimulationPhase.SpaceCombat, 1601)) { }
            using (trace.Measure(SimulationPhase.KnowledgeAfterMovement, 1601)) { }

            SimulationTimingCapture capture = trace.Stop();

            Assert.That(trace.IsRecording, Is.False);
            Assert.That(capture.Capacity, Is.EqualTo(2));
            Assert.That(capture.DroppedSamples, Is.EqualTo(1));
            Assert.That(capture.Samples.Count, Is.EqualTo(2));
            Assert.That(capture.Samples[0].Phase, Is.EqualTo(SimulationPhase.Movement));
            Assert.That(capture.Samples[1].Phase, Is.EqualTo(SimulationPhase.SpaceCombat));
            Assert.That(capture.Samples[0].Tick, Is.EqualTo(1601));
            Assert.That(capture.Samples[0].ElapsedTimestampTicks, Is.GreaterThanOrEqualTo(0));
            Assert.That(capture.TimestampFrequency, Is.GreaterThan(0));
        }

        /// <summary>
        /// Does not attach disabled work to a later enabled capture.
        /// </summary>
        [Test]
        public void Measure_DisabledScopeDisposedDuringCapture_DoesNotRecord()
        {
            SimulationPhaseTrace trace = new SimulationPhaseTrace();
            SimulationPhaseTrace.Scope disabled = trace.Measure(SimulationPhase.Missions, 15);
            trace.Start(1);

            disabled.Dispose();

            Assert.That(trace.Stop().Samples, Is.Empty);
        }

        /// <summary>
        /// Ignores scopes from a stopped generation and leaves exported captures unchanged.
        /// </summary>
        [Test]
        public void Start_AfterStoppedCapture_IgnoresPreviousGenerationScopes()
        {
            SimulationPhaseTrace trace = new SimulationPhaseTrace();
            trace.Start(2);
            SimulationPhaseTrace.Scope previous = trace.Measure(SimulationPhase.Missions, 10);
            SimulationTimingCapture first = trace.Stop();
            trace.Start(3);

            previous.Dispose();
            using (trace.Measure(SimulationPhase.Research, 11)) { }
            SimulationTimingCapture second = trace.Stop();

            Assert.That(first.Samples, Is.Empty);
            Assert.That(second.Samples.Count, Is.EqualTo(1));
            Assert.That(second.Samples[0].Phase, Is.EqualTo(SimulationPhase.Research));
            Assert.That(second.Samples[0].Tick, Is.EqualTo(11));
            Assert.That(second.DroppedSamples, Is.Zero);
        }

        /// <summary>
        /// Preserves nested completion order without discarding either work boundary.
        /// </summary>
        [Test]
        public void Measure_NestedScopes_RecordsCompletionOrder()
        {
            SimulationPhaseTrace trace = new SimulationPhaseTrace();
            trace.Start(2);

            using (trace.Measure(SimulationPhase.Missions, 20))
            {
                using (trace.Measure(SimulationPhase.ResultDelivery, 20)) { }
            }

            SimulationTimingCapture capture = trace.Stop();
            Assert.That(capture.Samples[0].Phase, Is.EqualTo(SimulationPhase.ResultDelivery));
            Assert.That(capture.Samples[1].Phase, Is.EqualTo(SimulationPhase.Missions));
            Assert.That(
                capture.Samples[1].ElapsedTimestampTicks,
                Is.GreaterThanOrEqualTo(capture.Samples[0].ElapsedTimestampTicks)
            );
        }
    }
}
