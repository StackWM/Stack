namespace LostTech.Stack.Utils
{
    using System;
    using System.ComponentModel;
    using System.Threading.Tasks;
    using JetBrains.Annotations;
    using Microsoft.HockeyApp;

    public static class WarningExtensions
    {
        public static void ReportAsWarning(this Exception exception, string prefix = "Warning: ") {
            if (exception != null)
                HockeyClient.Current.TrackException(new WarningException(prefix + exception.Message, exception));
        }

        public static void ReportAsWarning([NotNull] this Task<Exception> potentiallyFailingTask, string prefix = "Warning: ") {
            if (potentiallyFailingTask == null)
                throw new ArgumentNullException(nameof(potentiallyFailingTask));
            potentiallyFailingTask.ContinueWith(t => {
                if (t.IsFaulted)
                    t.Exception.ReportAsWarning(prefix);
                if (t.IsCompleted)
                    t.Result.ReportAsWarning(prefix);
            });
        }

        public static void ReportAsWarning([NotNull] this Task potentiallyFailingTask, string prefix = "Warning: ")
        {
            if (potentiallyFailingTask == null)
                throw new ArgumentNullException(nameof(potentiallyFailingTask));
            potentiallyFailingTask.ContinueWith(t => {
                if (t.IsFaulted)
                    t.Exception.ReportAsWarning(prefix);
            });
        }
    }
}
