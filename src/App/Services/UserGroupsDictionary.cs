#nullable enable
namespace LostTech.Stack.Services {
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using LostTech.Stack.Extensibility.Filters;
    using LostTech.Stack.Extensibility.Services;
    using LostTech.Stack.Models;
    using LostTech.Stack.WindowManagement;

    class UserGroupsDictionary : IStringDictionary<IFilter<IAppWindow>> {
        readonly IEnumerable<WindowGroup> groups;
        public UserGroupsDictionary(IEnumerable<WindowGroup> groups) {
            this.groups = groups ?? throw new ArgumentNullException(nameof(groups));
        }

        public bool TryGet(string key, out IFilter<IAppWindow>? value) {
            value = this.groups.FirstOrDefault(g => g.Name == key);
            return value != null;
        }
    }
}
