using System.Collections.Generic;

namespace Rebellion.Util.Extensions
{
    public static class IDictionaryExtensions
    {
        /// <summary>
        /// Returns the value for the given key, inserting the provided default value first if the key is absent.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="source"></param>
        /// <param name="key"></param>
        /// <param name="value">The value to insert if the key is not present.</param>
        /// <returns>The existing or newly inserted value.</returns>
        public static TValue GetOrAddValue<TKey, TValue>(
            this IDictionary<TKey, TValue> source,
            TKey key,
            TValue value
        )
        {
            if (!source.ContainsKey(key))
            {
                source.Add(key, value);
            }

            return source[key];
        }
    }
}
