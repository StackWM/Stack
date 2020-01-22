namespace LostTech.Stack.Utils {
    using System;
    using System.Threading.Tasks;
    using LostTech.App;
    static class WarningExtensions {
        public static void ReportAsWarning(this Exception exception, string prefix = "Warning: ")
            => WarningsService.Default.ReportAsWarning(exception, prefix);
        public static void ReportAsWarning(this Task<Exception> potentiallyFailingTask, string prefix = "Warning: ")
            => WarningsService.Default.ReportAsWarning(potentiallyFailingTask, prefix);
        public static void ReportAsWarning(this Task potentiallyFailingTask, string prefix = "Warning: ")
            => WarningsService.Default.ReportAsWarning(potentiallyFailingTask, prefix);
     }
}
