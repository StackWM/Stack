namespace LostTech.Stack.Utils {
    using System;
    using System.Runtime.ExceptionServices;
    using System.Threading.Tasks;
    using JetBrains.Annotations;

    public static class ExceptionExtensions {
        [Obsolete]
        public static Exception Capture(this Exception exception) =>
            ExceptionDispatchInfo.Capture(exception).SourceException;

        public static Task IgnoreUnobservedExceptions([NotNull] this Task task) {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            task.ContinueWith(new Action<Task>(t => t.Exception?.GetType()));
            return task;
        }

        public static Task<T> IgnoreUnobservedExceptions<T>([NotNull] this Task<T> task) {
            IgnoreUnobservedExceptions((Task)task);
            return task;
        }
    }
}
