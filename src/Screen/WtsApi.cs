namespace LostTech.Windows
{
    using System;
    using System.Runtime.InteropServices;

    static class WtsApi
    {
        [DllImport("Wtsapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WTSRegisterSessionNotification(IntPtr hwnd, NotifySessionFlags flags);
    }

    enum NotifySessionFlags
    {
        ThisSessionOnly = 0,
        AllSessions = 1,
    }
}
