using System;

namespace Rebellion.Util.Common
{
    /// <summary>
    /// Provides random number generation for simulation systems.
    /// Allows deterministic testing via fixed sequences.
    /// </summary>
    public interface IRandomNumberProvider
    {
        /// <summary>
        /// Returns a random double in the range [0.0, 1.0).
        /// </summary>
        /// <returns>The result of next double.</returns>
        double NextDouble();

        /// <summary>
        /// Returns a random integer in the range [min, max).
        /// </summary>
        /// <param name="min">The min.</param>
        /// <param name="max">The max.</param>
        /// <returns>The result of next int.</returns>
        int NextInt(int min, int max);
    }

    /// <summary>
    /// Production implementation using System.Random with seeded initialization.
    /// Tracks the number of calls so a save can record where the stream is and a
    /// load can fast-forward back to the same position.
    /// </summary>
    public class SystemRandomProvider : IRandomNumberProvider
    {
        private readonly Random _rng;

        /// <summary>
        /// The number of times this provider has been called. Persist this with the
        /// seed to resume an RNG stream at the same position after a save/load.
        /// </summary>
        public long CallCount { get; private set; }

        /// <summary>
        /// Creates a random provider with the specified seed, optionally fast-forwarded
        /// to the given call position.
        /// </summary>
        /// <param name="seed">Seed value for deterministic output.</param>
        /// <param name="advanceTo">Number of underlying draws to discard before use, restoring a previous call position.</param>
        public SystemRandomProvider(int seed, long advanceTo = 0)
        {
            _rng = new Random(seed);
            for (long i = 0; i < advanceTo; i++)
                _rng.Next();
            CallCount = advanceTo;
        }

        /// <summary>
        /// Generates the next random floating-point value.
        /// </summary>
        /// <returns>A random value greater than or equal to zero and less than one.</returns>
        public double NextDouble()
        {
            CallCount++;
            return _rng.NextDouble();
        }

        /// <summary>
        /// Generates the next random integer within the requested range.
        /// </summary>
        /// <param name="min">The inclusive lower bound.</param>
        /// <param name="max">The exclusive upper bound.</param>
        /// <returns>A random integer within the requested range.</returns>
        public int NextInt(int min, int max)
        {
            CallCount++;
            return _rng.Next(min, max);
        }
    }
}
