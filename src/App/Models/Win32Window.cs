﻿namespace LostTech.Stack.Models
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using System.Windows;
    using static PInvoke.User32;
    using Win32Exception = System.ComponentModel.Win32Exception;

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
                return new Win32Exception();
            } else {
                // TODO: option to not activate on move
                await Task.Yield();
                MoveWindow(this.Handle, (int)targetBounds.Left, (int)targetBounds.Top, (int)targetBounds.Width,
                    (int)targetBounds.Height, true);
                return null;
            }
        }

        public string Title => GetWindowText(this.Handle);

        public Task<Exception> Activate() {
            this.EnsureNotMinimized();
            return Task.FromResult(
                SetForegroundWindow(this.Handle) ? null : (Exception)new Win32Exception());
        }

        public Task<Exception> BringToFront() {
            this.EnsureNotMinimized();
            Exception issue = null;
            if (!SetWindowPos(this.Handle, GetForegroundWindow(), 0, 0, 0, 0,
                              SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOACTIVATE |
                              SetWindowPosFlags.SWP_NOSIZE))
                issue = new Win32Exception();
            return Task.FromResult(issue);
        }

        Exception EnsureNotMinimized() {
            if (!IsIconic(this.Handle))
                return null;

            return ShowWindow(this.Handle, WindowShowStyle.SW_RESTORE) ? null : new Win32Exception();
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

        [DllImport("User32.dll")]
        static extern bool IsIconic(IntPtr hwnd);

        static readonly IntPtr HWND_TOP = IntPtr.Zero;
        static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    }
}
