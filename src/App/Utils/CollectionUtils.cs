namespace LostTech.Stack.Utils
{
    using System;
    using System.Collections.Generic;
    using JetBrains.Annotations;

    static class CollectionUtils
    {
        public static void AddRange<T>([NotNull] this ICollection<T> collection, [NotNull] IEnumerable<T> items) {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            foreach (T item in items)
                collection.Add(item);
        }
    }
}
