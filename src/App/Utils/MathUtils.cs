﻿namespace LostTech.Stack.Utils
{
    using System;

    static class MathUtils
    {
        public static bool IsBetween<T>(this T a, T min, T max) where T : IComparable<T> {
            return a.CompareTo(min) >= 0 && a.CompareTo(max) <= 0;
        }

        public static T AtLeast<T>(this T a, T min) where T : IComparable<T> => a.CompareTo(min) >= 0 ? a : min;
    }
}
