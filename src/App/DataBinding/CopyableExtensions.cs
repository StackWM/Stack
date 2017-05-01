namespace LostTech.Stack.DataBinding
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using LostTech.App;

    public static class CopyableExtensions
    {
        public static T Copy<T>(this T obj)
            where T: class
        {
            if (obj == null)
                return null;
            if (obj is ICopyable<T> copyable)
                return copyable.Copy();

            throw new NotSupportedException();
        }
    }
}
