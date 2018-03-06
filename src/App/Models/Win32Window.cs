namespace LostTech.Stack.Models
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using System.Windows;
    using static PInvoke.User32;

    class Win32Window : IAppWindow, IEquatable<Win32Window>
    {
        public IntPtr Handle { get; }

        public Win32Window(IntPtr handle) {
            this.Handle = handle;
        }

        public async Task<Exception> Move(Rect targetBounds) {
            var windowPlacement = WINDOWPLACEMENT.Create();
            if (GetWindowPlacement(this.Handle, ref windowPlacement) &&
                windowPlacement.showCmd.HasFlag(WindowShowStyle.SW_MAXIMIZE)) {
                ShowWindow(this.Handle, WindowShowStyle.SW_RESTORE);
            }

            if (!MoveWindow(this.Handle, (int)targetBounds.Left, (int)targetBounds.Top,
                (int)targetBounds.Width, (int)targetBounds.Height, bRepaint: true)) {
                return new System.ComponentModel.Win32Exception();
            } else {
                // TODO: option to not activate on move
                SetForegroundWindow(this.Handle);
                await Task.Yield();
                MoveWindow(this.Handle, (int)targetBounds.Left, (int)targetBounds.Top, (int)targetBounds.Width,
                    (int)targetBounds.Height, true);
                return null;
            }
        }

        public bool Equals(Win32Window other) {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return this.Handle.Equals(other.Handle);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return this.Equals((Win32Window) obj);
        }

        public override int GetHashCode() => this.Handle.GetHashCode();

        [DllImport("User32.dll", SetLastError = true)]
        static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);
    }
}
