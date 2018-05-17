namespace LostTech.Stack.Utils {
    using System;
    using System.Runtime.ExceptionServices;

    public static class ExceptionExtensions {
        [Obsolete]
        public static Exception Capture(this Exception exception) =>
            ExceptionDispatchInfo.Capture(exception).SourceException;
    }
}
