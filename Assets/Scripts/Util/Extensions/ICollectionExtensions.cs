using System;
using System.Collections.Generic;

namespace ICollectionExtensions
{
    public static class ICollectionExtensions
    {
        /// <summary>
        /// Adds all elements from an enumerable to the current collection.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="enumerables"></param>
        public static void AddAll<T>(
            this ICollection<T> source,
            params IEnumerable<T>[] enumerables
        )
        {
            foreach (IEnumerable<T> enumerable in enumerables)
            {
                foreach (T t in enumerable)
                {
                    source.Add(t);
                }
            }
        }
    }
}
