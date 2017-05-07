namespace LostTech.Stack.Models.Filters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    static class GroupListHelper
    {
        public static bool Contains(this IEnumerable<string> groupList, IEnumerable<WindowGroup> groups,
            IntPtr windowHandle)
        {
            foreach (string groupName in groupList)
            {
                WindowGroup group = groups.FirstOrDefault(g => g.Name == groupName);
                if (true.Equals(group?.Filters?.Any(f => f.Matches(windowHandle))))
                    return true;
            }
            return false;
        }
    }
}
