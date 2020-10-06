namespace LostTech.Stack.Services {
    using System;
    using System.Collections;
    using System.Collections.Generic;

    using LostTech.Stack.WindowManagement;

    class TopLevelWindowsEnumerable : IEnumerable<IAppWindow> {
        readonly Win32WindowFactory windowFactory;
        public TopLevelWindowsEnumerable(Win32WindowFactory windowFactory) {
            this.windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
        }

        public IEnumerator<IAppWindow> GetEnumerator() {
            var items = new List<IAppWindow>();
            this.windowFactory.ForEachTopLevel(items.Add);
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
