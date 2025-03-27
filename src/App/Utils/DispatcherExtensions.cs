namespace LostTech.Stack.Utils;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using System;

static class DispatcherExtensions {
    public static DispatcherAwaiter GetAwaiter(this Dispatcher dispatcher) => new(dispatcher);

    public class DispatcherAwaiter(Dispatcher dispatcher): INotifyCompletion {
        private readonly Dispatcher dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        public bool IsCompleted => this.dispatcher.CheckAccess();

        public void GetResult() { }

        public void OnCompleted(Action continuation) {
            this.dispatcher.BeginInvoke(continuation);
        }
    }
}