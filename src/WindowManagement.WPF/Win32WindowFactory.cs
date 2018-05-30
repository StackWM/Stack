namespace LostTech.Stack.WindowManagement {
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using JetBrains.Annotations;
    using LostTech.Stack.Utils;
    using static PInvoke.User32;
    using Win32Exception = System.ComponentModel.Win32Exception;

    public sealed class Win32WindowFactory
    {
        public bool SuppressSystemMargin { get; set; } = true;
        [NotNull]
        public Win32Window Create(IntPtr handle) => new Win32Window(handle, this.SuppressSystemMargin);
        [CanBeNull]
        public Win32Window Foreground => this.CreateIfNotNull(GetForegroundWindow());
        [CanBeNull]
        public Win32Window Desktop => this.CreateIfNotNull(GetDesktopWindow());
        [CanBeNull]
        public Win32Window Shell => this.CreateIfNotNull(GetShellWindow());

        public Exception ForEachTopLevel(Action<Win32Window> action) {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var enumerator = new WNDENUMPROC((hwnd, param) => {
                Win32Window window = this.Create(hwnd);
                action(window);
                return true;
            });
            bool done = EnumWindows(enumerator, IntPtr.Zero);
            GC.KeepAlive(enumerator);
            return done ? null : new Win32Exception();
        }

        public bool DisplayInSwitchToList([NotNull] Win32Window window) {
            if (window == null) throw new ArgumentNullException(nameof(window));

            if (!window.IsVisible)
                return false;

            IntPtr rootOwner = GetAncestor(window.Handle, GetAncestorFlags.GA_ROOTOWNER);

            IntPtr activePopup = rootOwner;
            IntPtr innerPopup = IntPtr.Zero, newInnerPopup;

            while ((newInnerPopup = GetLastActivePopup(activePopup)) != innerPopup) {
                innerPopup = newInnerPopup;
                if (IsWindowVisible(newInnerPopup))
                    break;
                activePopup = newInnerPopup;
            }
            return activePopup == window.Handle;
        }

        Win32Window CreateIfNotNull(IntPtr handle) =>
            handle == IntPtr.Zero ? null : new Win32Window(handle, this.SuppressSystemMargin);

        [DllImport("user32.dll")]
        static extern IntPtr GetLastActivePopup(IntPtr handle);
    }
}
