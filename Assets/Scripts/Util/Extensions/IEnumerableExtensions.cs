using System;
using System.Collections.Generic;
using System.Linq;

namespace IEnumerableExtensions
{
    public static class IEnumerableExtensions
    {
        private static readonly Random random = new Random();

        /// <summary>
        /// Randomizes the current collection and returns the results in a new IEnumerable.
        /// Uses the Fisher-Yates Shuffle algorithm for efficiency and uniform randomness.
        /// </summary>
        /// <typeparam name="T">The type of elements in the collection.</typeparam>
        /// <param name="source">The source collection to shuffle.</param>
        /// <returns>A new IEnumerable containing the shuffled elements.</returns>
        /// <exception cref="ArgumentException">Thrown when the collection is null.</exception>
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            if (source == null)
                throw new ArgumentException(nameof(source), "Source collection cannot be null.");

            List<T> buffer = source.ToList();

            for (int i = 0; i < buffer.Count; i++)
            {
                int j = random.Next(i, buffer.Count);

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
        /// <returns>A randomly selected element from the collection.</returns>
        /// <exception cref="ArgumentException">Thrown when the collection is null or empty.</exception>
        public static T RandomElement<T>(this IEnumerable<T> source)
        {
            if (source == null || !source.Any())
            {
                throw new ArgumentException("Source collection cannot be null or empty.");
            }

            int index = random.Next(source.Count());
            return source.ElementAt(index);
        }
    }
}
