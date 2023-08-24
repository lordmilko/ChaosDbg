using System;
using System.Collections.Concurrent;

namespace ChaosDbg
{
    static class ConcurrentDictionaryExtensions
    {
        public static bool TryGetValueSafe<TKey, TValue>(this ConcurrentDictionary<TKey, Lazy<TValue>> dict, TKey key, out TValue value)
        {
            if (dict.TryGetValue(key, out var raw))
            {
                value = raw.Value;
                return true;
            }

            value = default;
            return false;
        }

        public static TValue GetOrAddSafe<TKey, TValue>(this ConcurrentDictionary<TKey, Lazy<TValue>> dict, TKey key, Func<TValue> getValue) =>
            dict.GetOrAdd(key, v => new Lazy<TValue>(getValue)).Value;
    }
}
