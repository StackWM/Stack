namespace LostTech.Stack.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;

    static class EnumUtils
    {
        class EnumHelper<T> where T : struct, IConvertible
        {
            static EnumHelper()
            {
                if (!typeof(T).IsEnum)
                    throw new ArgumentException("Must be System.Enum", paramName: nameof(T));
            }

            [Conditional("DEBUG")]
            public static void AssertIsEnum() { }
        }

        class FlagsHelper<T> where T : struct, IConvertible
        {
            static FlagsHelper()
            {
                if (typeof(T).GetCustomAttribute(typeof(FlagsAttribute)) == null)
                    throw new ArgumentException("Must be have System.FlagsAttribute", paramName: nameof(T));
            }

            public static void AssertIsFlags() { }
        }

        public static void ForEach<T>(this T value, Action<T> action)
            where T : struct, IConvertible
        {
            FlagsHelper<T>.AssertIsFlags();

            long bits = value.ToInt64(null);
            for (int i = 0; i < 64; i++) {
                long bit = 1L << i;
                if ((bits & bit) != 0)
                    action((T)(object)bit);
            }
        }

        public static int BitIndex<T>(this T value)
            where T : struct, IConvertible
        {
            FlagsHelper<T>.AssertIsFlags();

            long bits = value.ToInt64(null);
            for (int i = 0; i < 64; i++) {
                long bit = 1L << i;
                if ((bits & bit) != 0)
                    return i;
            }
            return -1;
        }

        public static IEnumerable<T> GetFlags<T>(this T value)
            where T : struct, IConvertible
        {
            long bits = value.ToInt64(null);
            for (int i = 0; i < 64; i++)
            {
                long bit = 1L << i;
                if ((bits & bit) != 0)
                    yield return (T)Enum.ToObject(typeof(T), bit);
            }
        }

        public static int BitCount<T>(this T value) where T : struct, IConvertible
            => value.GetFlags().Count();

        public static int SingleBitIndex<T>(this T value)
            where T : struct, IConvertible
        {
            FlagsHelper<T>.AssertIsFlags();

            long bits = value.ToInt64(null);
            int result = -1;
            for (int i = 0; i < 64; i++)
            {
                long bit = 1L << i;
                if ((bits & bit) != 0) {
                    if (result >= 0)
                        throw new InvalidOperationException("More than 1 bit is set");
                    result = i;
                }
            }
            return result;
        }
    }
}
