namespace LostTech.Stack.Utils {
    using System;
    using System.Collections.Generic;
    using JetBrains.Annotations;

    static class EnumerationUtils {
        public static bool MinBy<T, TProp>(
            [NotNull] this IEnumerable<T> enumerable,
            [NotNull] Func<T, TProp> propertyGetter,
            out T result)
        where TProp: IComparable<TProp> {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            if (propertyGetter == null) throw new ArgumentNullException(nameof(propertyGetter));

            result = default(T);
            var min = default(TProp);
            bool atLeastOne = false;
            foreach (T item in enumerable) {
                if (!atLeastOne) {
                    min = propertyGetter(item);
                    result = item;
                    atLeastOne = true;
                } else {
                    var currentProp = propertyGetter(item);
                    if (min.CompareTo(currentProp) > 0) {
                        min = currentProp;
                        result = item;
                    }
                }
            }

            return atLeastOne;
        }

        public static T MinByOrDefault<T, TProp>(
            [NotNull] this IEnumerable<T> enumerable,
            [NotNull] Func<T, TProp> propertyGetter)
        where TProp: IComparable<TProp> {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            if (propertyGetter == null) throw new ArgumentNullException(nameof(propertyGetter));
            MinBy(enumerable, propertyGetter, out var result);
            return result;
        }

        public static IEnumerable<T> OrEmpty<T>(this IEnumerable<T>? enumerable)
            => enumerable ?? Array.Empty<T>();
    }
}
