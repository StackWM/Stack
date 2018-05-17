namespace LostTech.Stack.Models {
    using System.Threading;
    using EventHook.Hooks;

    class WindowHookExFactory {
        readonly ThreadLocal<WindowHookEx> hook = new ThreadLocal<WindowHookEx>(() => new WindowHookEx());

        public WindowHookEx GetHook() => this.hook.Value;

        public static WindowHookExFactory Instance { get; } = new WindowHookExFactory();
    }
}
