namespace LostTech.Stack
{
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;

    class DebugHelper
    {
        [Conditional("DEBUG")]
        public static void Called([CallerFilePath] string callerFile = null,
            [CallerMemberName] string callerMember = null)
        {
            Debug.WriteLine($"called {Path.GetFileNameWithoutExtension(callerFile)}.{callerMember}");
        }
    }
}
