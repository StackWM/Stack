namespace LostTech.Stack.Utils
{
    using System;
    using System.Windows;
    using System.Windows.Interop;
    using JetBrains.Annotations;
    using LostTech.Stack.Models;
    using Microsoft.HockeyApp;

    static class WindowUtils
    {
        static readonly Win32WindowFactory win32WindowFactory = new Win32WindowFactory();
        [NotNull]
        public static Win32Window GetNativeWindow(this Window window) {
            if (window == null)
                throw new ArgumentNullException(nameof(window));
            IntPtr handle = new WindowInteropHelper(window).Handle;
            return win32WindowFactory.Create(handle);
        }
        [CanBeNull]
        public static Win32Window TryGetNativeWindow(this Window window) {
            IntPtr handle = window.TryGetHandle();
            return handle == IntPtr.Zero ? null : win32WindowFactory.Create(handle);
        }
        public static IntPtr TryGetHandle(this Window window) {
            if (window == null)
                throw new ArgumentNullException(nameof(window));
            try {
                return new WindowInteropHelper(window).Handle;
            } catch(Exception e) {
                HockeyClient.Current.TrackException(new Warning($"Can't get window handle: {e.Message}", e));
                return IntPtr.Zero;
            }
        }
    }
}
