using System.Collections.Generic;
using System;

namespace IEnumerableExtensions
{
    public static class IEnumerableExtensions
    {
        /// <summary>
        /// Randomizes the current collection and returns the results in a new IEnumerable.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            List<T> buffer = new List<T>(source);
            Random random = new Random();

            for (int i = 0; i < buffer.Count; i++)
            {
                int j = random.Next(i, buffer.Count);
                yield return buffer[j];

                buffer[j] = buffer[i];
            }
        }
    }
}
