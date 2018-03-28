namespace LostTech.Stack.Utils
{
    using System;
    using System.Collections.Generic;

    static class DictUtils
    {
        public static TValue GetOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
            where TValue : new() {
            if (dict == null)
                throw new ArgumentNullException(nameof(dict));

            if (dict.TryGetValue(key, out var value))
                return value;

            value = new TValue();
            dict.Add(key, value);
            return value;
        }
    }
}
