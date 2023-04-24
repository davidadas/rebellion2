using System.Collections.Generic;
using System;

namespace IDictionaryExtensions
{
    public static class IDictionaryExtensions
    {
        /// <summary>
        ///
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="source"></param>
        /// <param name="key"></param>
        /// <returns></returns>
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
