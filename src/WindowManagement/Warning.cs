namespace LostTech.Stack.Utils
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.Threading.Tasks;
    using JetBrains.Annotations;
    using Microsoft.HockeyApp;

    public static class WarningExtensions
    {
        public static void ReportAsWarning(this Exception exception, string prefix = "Warning: ") {
            if (exception != null) {
                string message = prefix + exception.Message;
                HockeyClient.Current.TrackTrace(message, SeverityLevel.Warning, properties: new SortedDictionary<string, string> {
                    [nameof(exception.StackTrace)] = exception.StackTrace,
                    [nameof(exception.Source)] = exception.Source,
                    [nameof(exception.HResult)] = exception.HResult.ToString(CultureInfo.InvariantCulture),
                    [nameof(exception.InnerException)] = exception.InnerException?.ToString(),
                });
            }
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
