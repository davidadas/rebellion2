using System;
using System.Collections.Generic;

namespace IEnumerableExtensions
{
    public static class IEnumerableExtensions
    {
        private static Random _random = new Random();

        /// <summary>
        /// Randomizes the current collection and returns the results in a new IEnumerable.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            List<T> buffer = new List<T>(source);

            for (int i = 0; i < buffer.Count; i++)
            {
                int j = _random.Next(i, buffer.Count);
                yield return buffer[j];

                buffer[j] = buffer[i];
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static T RandomElement<T>(this List<T> list)
        {
            if (list == null || list.Count == 0)
                throw new InvalidOperationException(
                    "Cannot select a random element from an empty list."
                );

            int index = _random.Next(list.Count);
            return list[index];
        }
    }
}
