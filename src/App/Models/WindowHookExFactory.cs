namespace LostTech.Stack.Models {
    using System.Threading;
    using EventHook.Hooks;

    class WindowHookExFactory {
        readonly ThreadLocal<WindowHookEx> hook = new ThreadLocal<WindowHookEx>(() => new WindowHookEx());

        public WindowHookEx GetHook() => this.hook.Value;

        public static WindowHookExFactory Instance { get; } = new WindowHookExFactory();

        /// <summary>
        /// Needs to be called for each participating thread.
        /// </summary>
        public void Shutdown() => this.hook.Value.Dispose();
    }
}
