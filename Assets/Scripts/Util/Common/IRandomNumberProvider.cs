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
        double NextDouble();

        /// <summary>
        /// Returns a random integer in the range [min, max).
        /// </summary>
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

        public double NextDouble()
        {
            CallCount++;
            return _rng.NextDouble();
        }

        public int NextInt(int min, int max)
        {
            CallCount++;
            return _rng.Next(min, max);
        }
    }

    /// <summary>
    /// Test implementation returning fixed sequence of values.
    /// Wraps around when exhausted.
    /// </summary>
    public class FixedRandomProvider : IRandomNumberProvider
    {
        private readonly double[] _values;
        private int _index;

        /// <summary>
        /// Creates a fixed random provider with the specified value sequence.
        /// </summary>
        /// <param name="fixedValues">Array of values to return in sequence.</param>
        public FixedRandomProvider(double[] fixedValues)
        {
            _values = fixedValues;
        }

        public double NextDouble() => _values[_index++ % _values.Length];

        public int NextInt(int min, int max) => (int)(NextDouble() * (max - min)) + min;
    }
}
