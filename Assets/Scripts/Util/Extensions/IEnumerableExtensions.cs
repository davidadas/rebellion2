using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Util.Common;

namespace Rebellion.Util.Extensions
{
    public static class IEnumerableExtensions
    {
        /// <summary>
        /// Randomizes the current collection and returns the results in a new IEnumerable.
        /// Uses the Fisher-Yates Shuffle algorithm for efficiency and uniform randomness.
        /// </summary>
        /// <typeparam name="T">The type of elements in the collection.</typeparam>
        /// <param name="source">The source collection to shuffle.</param>
        /// <param name="provider">Random number provider for shuffle operation.</param>
        /// <returns>A new IEnumerable containing the shuffled elements.</returns>
        /// <exception cref="ArgumentException">Thrown when the collection is null.</exception>
        public static IEnumerable<T> Shuffle<T>(
            this IEnumerable<T> source,
            IRandomNumberProvider provider
        )
        {
            if (source == null)
                throw new ArgumentException(nameof(source), "Source collection cannot be null.");

            List<T> buffer = source.ToList();

            for (int i = 0; i < buffer.Count; i++)
            {
                int j = provider.NextInt(i, buffer.Count);

                // Swap elements i and j.
                T temp = buffer[i];
                buffer[i] = buffer[j];
                buffer[j] = temp;
            }
            return buffer;
        }

        /// <summary>
        /// Selects a random element from the given enumerable collection.
        /// </summary>
        /// <typeparam name="T">The type of elements in the collection.</typeparam>
        /// <param name="source">The source collection to select a random element from.</param>
        /// <param name="provider">Random number provider for element selection.</param>
        /// <returns>A randomly selected element from the collection.</returns>
        /// <exception cref="ArgumentException">Thrown when the collection is null or empty.</exception>
        public static T RandomElement<T>(this IEnumerable<T> source, IRandomNumberProvider provider)
        {
            if (source?.Any() != true)
            {
                throw new ArgumentException("Source collection cannot be null or empty.");
            }

            int index = provider.NextInt(0, source.Count());
            return source.ElementAt(index);
        }

        /// <summary>
        /// Randomizes the current collection using System.Random.
        /// NOTE: This overload is for non-simulation code (generation, utilities).
        /// Simulation code should use Shuffle(IRandomNumberProvider) for determinism.
        /// </summary>
        /// <typeparam name="T">The type of elements in the collection.</typeparam>
        /// <param name="source">The source collection to shuffle.</param>
        /// <returns>A new IEnumerable containing the shuffled elements.</returns>
        /// <exception cref="ArgumentException">Thrown when the collection is null.</exception>
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            Random localRandom = new Random();
            IRandomNumberProvider provider = new SystemRandomProvider(localRandom.Next());
            return Shuffle(source, provider);
        }

        /// <summary>
        /// Selects a random element using System.Random.
        /// NOTE: This overload is for non-simulation code (generation, utilities).
        /// Simulation code should use RandomElement(IRandomNumberProvider) for determinism.
        /// </summary>
        /// <typeparam name="T">The type of elements in the collection.</typeparam>
        /// <param name="source">The source collection to select a random element from.</param>
        /// <returns>A randomly selected element from the collection.</returns>
        /// <exception cref="ArgumentException">Thrown when the collection is null or empty.</exception>
        public static T RandomElement<T>(this IEnumerable<T> source)
        {
            Random localRandom = new Random();
            IRandomNumberProvider provider = new SystemRandomProvider(localRandom.Next());
            return RandomElement(source, provider);
        }
    }
}
