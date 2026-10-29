using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Contains a stopped capture suitable for analysis and export outside simulation execution.
    /// </summary>
    public sealed class SimulationTimingCapture
    {
        /// <summary>
        /// Gets the retained samples in completion order, including nested work boundaries.
        /// </summary>
        public IReadOnlyList<SimulationTimingSample> Samples { get; }

        /// <summary>
        /// Gets the number of completed samples omitted after the capture buffer became full.
        /// </summary>
        public long DroppedSamples { get; }

        /// <summary>
        /// Gets the explicit maximum number of retained samples.
        /// </summary>
        public int Capacity { get; }

        /// <summary>
        /// Gets the clock ticks per second used by every elapsed sample.
        /// </summary>
        public long TimestampFrequency => Stopwatch.Frequency;

        /// <summary>
        /// Owns an isolated completed sample array after recording has stopped.
        /// </summary>
        /// <param name="samples">The copied retained samples.</param>
        /// <param name="droppedSamples">The number omitted because the buffer was full.</param>
        /// <param name="capacity">The requested recording capacity.</param>
        internal SimulationTimingCapture(
            SimulationTimingSample[] samples,
            long droppedSamples,
            int capacity
        )
        {
            Samples = Array.AsReadOnly(samples);
            DroppedSamples = droppedSamples;
            Capacity = capacity;
        }
    }
}
