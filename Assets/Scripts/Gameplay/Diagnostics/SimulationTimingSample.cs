namespace Rebellion.Gameplay
{
    /// <summary>
    /// Stores scalar elapsed work for one synchronous session boundary.
    /// </summary>
    public readonly struct SimulationTimingSample
    {
        /// <summary>
        /// Gets the game day associated with the measured work.
        /// </summary>
        public int Tick { get; }

        /// <summary>
        /// Gets the measured synchronous work boundary.
        /// </summary>
        public SimulationPhase Phase { get; }

        /// <summary>
        /// Gets elapsed monotonic clock ticks, excluding time outside the measured call.
        /// </summary>
        public long ElapsedTimestampTicks { get; }

        /// <summary>
        /// Creates a scalar sample without retaining any game graph references.
        /// </summary>
        /// <param name="tick">The measured game day.</param>
        /// <param name="phase">The synchronous work boundary.</param>
        /// <param name="elapsedTimestampTicks">The elapsed monotonic clock ticks.</param>
        internal SimulationTimingSample(int tick, SimulationPhase phase, long elapsedTimestampTicks)
        {
            Tick = tick;
            Phase = phase;
            ElapsedTimestampTicks = elapsedTimestampTicks;
        }
    }
}
