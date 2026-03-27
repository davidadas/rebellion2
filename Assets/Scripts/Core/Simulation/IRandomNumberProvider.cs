using System;

namespace Rebellion.Core.Simulation
{
    /// <summary>
    /// Provides random number generation for simulation systems.
    /// Allows deterministic testing via fixed sequences.
    ///
    /// NOTE: This is a DESIGNED interface for testability, NOT verified
    /// against the original game's RNG usage patterns.
    /// </summary>
    public interface IRandomNumberProvider
    {
        /// <summary>
        /// Returns a random double in the range [0.0, 1.0).
        /// </summary>
        double NextDouble();

        /// <summary>
        /// Returns a random integer in the range [min, max).
        /// </summary>
        int NextInt(int min, int max);
    }

    /// <summary>
    /// Production implementation using System.Random with seeded initialization.
    /// </summary>
    public class SystemRandomProvider : IRandomNumberProvider
    {
        private readonly Random rng;

        /// <summary>
        /// Creates a random provider with the specified seed.
        /// </summary>
        /// <param name="seed">Seed value for deterministic output.</param>
        public SystemRandomProvider(int seed)
        {
            rng = new Random(seed);
        }

        public double NextDouble() => rng.NextDouble();

        public int NextInt(int min, int max) => rng.Next(min, max);
    }

    /// <summary>
    /// Test implementation returning fixed sequence of values.
    /// Wraps around when exhausted.
    /// </summary>
    public class FixedRandomProvider : IRandomNumberProvider
    {
        private readonly double[] values;
        private int index = 0;

        /// <summary>
        /// Creates a fixed random provider with the specified value sequence.
        /// </summary>
        /// <param name="fixedValues">Array of values to return in sequence.</param>
        public FixedRandomProvider(double[] fixedValues)
        {
            values = fixedValues;
        }

        public double NextDouble() => values[index++ % values.Length];

        public int NextInt(int min, int max) => (int)(NextDouble() * (max - min)) + min;
    }
}
