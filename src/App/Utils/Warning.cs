namespace LostTech.Stack.Utils
{
    using System;
    using System.Threading.Tasks;
    using JetBrains.Annotations;
    using Microsoft.HockeyApp;

    public class Warning: Exception
    {
        public Warning(string message) : base(message) { }
        public Warning(string message, Exception innerException): base(message, innerException) { }
    }

    public static class WarningExtensions
    {
        public static void ReportAsWarning(this Exception exception, string prefix = "Warning: ") {
            if (exception != null)
                HockeyClient.Current.TrackException(new Warning(prefix + exception.Message, exception));
        }

        public static void ReportAsWarning([NotNull] this Task<Exception> potentiallyFailingTask, string prefix = "Warning: ") {
            if (potentiallyFailingTask == null)
                throw new ArgumentNullException(nameof(potentiallyFailingTask));
            potentiallyFailingTask.ContinueWith(t => {
                if (t.IsFaulted)
                    t.Exception.ReportAsWarning(prefix);
            });
        }
    }
}
