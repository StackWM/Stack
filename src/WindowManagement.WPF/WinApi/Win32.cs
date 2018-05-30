namespace LostTech.Stack.WindowManagement.WinApi {
    using System;
    using System.Runtime.InteropServices;
    using PInvoke;

    public static class Win32 {
        public struct WINDOWINFO
        {
            uint cbSize;
            public RECT rcWindow;
            public RECT rcClient;
            uint dwStyle;
            uint dwExStyle;
            uint dwWindowStatus;
            uint cxWindowBorders;
            uint cyWindowBorders;
            ushort atomWindowType;
            uint wCreatorVersion;
        }

        [DllImport("User32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowInfo(IntPtr hwnd, out WINDOWINFO pwi);
    }
}
