namespace LostTech.Stack.WindowManagement {
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using WindowsDesktop;
    using JetBrains.Annotations;
    using LostTech.Stack.Utils;
    using LostTech.Stack.WindowManagement.WinApi;
    using PInvoke;
    using static PInvoke.User32;
    using Win32Exception = System.ComponentModel.Win32Exception;
    using Rect = System.Drawing.RectangleF;

    [DebuggerDisplay("{" + nameof(Title) + "}")]
    public sealed class Win32Window : IAppWindow, IEquatable<Win32Window>
    {
        readonly Lazy<bool> excludeFromMargin;
        public IntPtr Handle { get; }
        public bool SuppressSystemMargin { get; set; }

        public Win32Window(IntPtr handle, bool suppressSystemMargin) {
            this.Handle = handle;
            this.SuppressSystemMargin = suppressSystemMargin;
            this.excludeFromMargin = new Lazy<bool>(this.GetExcludeFromMargin);
        }

        const bool RepaintOnMove = true;
        public Task Move(Rect targetBounds) => Task.Run(async () => {
            var windowPlacement = WINDOWPLACEMENT.Create();
            if (GetWindowPlacement(this.Handle, ref windowPlacement) &&
                windowPlacement.showCmd.HasFlag(WindowShowStyle.SW_MAXIMIZE)) {
                ShowWindow(this.Handle, WindowShowStyle.SW_RESTORE);
            }

            if (this.SuppressSystemMargin && !this.excludeFromMargin.Value) {
                RECT systemMargin = GetSystemMargin(this.Handle);
#if DEBUG
                Debug.WriteLine($"{this.Title} compensating system margin {systemMargin.left},{systemMargin.top},{systemMargin.right},{systemMargin.bottom}");
#endif
                targetBounds.X -= systemMargin.left;
                targetBounds.Y -= systemMargin.top;
                targetBounds.Width = Math.Max(0, targetBounds.Width + systemMargin.left + systemMargin.right);
                targetBounds.Height = Math.Max(0, targetBounds.Height + systemMargin.top + systemMargin.bottom);
            }

            if (!MoveWindow(this.Handle, (int)targetBounds.Left, (int)targetBounds.Top,
                (int)targetBounds.Width, (int)targetBounds.Height, bRepaint: RepaintOnMove)) {
                var exception = this.GetLastError();
                if (exception is Win32Exception win32Exception
                    && win32Exception.NativeErrorCode == (int)WinApiErrorCode.ERROR_ACCESS_DENIED)
                    throw new UnauthorizedAccessException("Not enough privileges to move window", inner: exception);
                else
                    throw exception;
            } else {
                // TODO: option to not activate on move
                await Task.Yield();
#if DEBUG
                Debug.WriteLine($"{this.Title} final rect: {targetBounds}");
#endif
                MoveWindow(this.Handle, (int)targetBounds.Left, (int)targetBounds.Top, (int)targetBounds.Width,
                    (int)targetBounds.Height, bRepaint: RepaintOnMove);
            }
        });

        public Task<bool?> Close() => Task.Run(() => {
            IntPtr result = SendMessage(this.Handle, WindowMessage.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            return (bool?)null;
        });

        public bool CanMove =>
            PostMessage(this.Handle, WindowMessage.WM_USER, IntPtr.Zero, IntPtr.Zero)
            || Marshal.GetLastWin32Error() != (int)WinApiErrorCode.ERROR_ACCESS_DENIED;

        /// <summary>
        /// Non-WPF coordinates
        /// </summary>
        public Rect Bounds {
            get {
                if (!Win32.GetWindowInfo(this.Handle, out var info))
                    throw this.GetLastError();

                var bounds = new Rect(info.rcWindow.left, info.rcWindow.top,
                    info.rcWindow.right - info.rcWindow.left,
                    info.rcWindow.bottom - info.rcWindow.top);

                if (this.SuppressSystemMargin && !this.excludeFromMargin.Value) {
                    RECT systemMargin = GetSystemMargin(this.Handle);
                    bounds.X += systemMargin.left;
                    bounds.Y += systemMargin.top;
                    bounds.Width = Math.Max(0, bounds.Width - (systemMargin.left + systemMargin.right));
                    bounds.Height = Math.Max(0, bounds.Height - (systemMargin.top + systemMargin.bottom));
                }

                return bounds;
            }
        }

        public Task<Rect> GetBounds() => Task.Run(() => this.Bounds);

        public string Title {
            get {
                try {
                    return GetWindowText(this.Handle);
                } catch (PInvoke.Win32Exception) {
                    return null;
                }
            }
        }

        public string Class {
            get {
                try {
                    return GetClassName(this.Handle);
                } catch (PInvoke.Win32Exception) {
                    return null;
                }
            }
        }

        public bool IsMinimized => IsIconic(this.Handle);
        public bool IsVisible {
            get {
                if (!IsWindowVisible(this.Handle))
                    return false;
                if (!VirtualDesktop.HasMinimalSupport)
                    return true;
                Guid? desktopId = null;

                var timer = Stopwatch.StartNew();
                COMException e = null;
                while (timer.Elapsed < this.ShellUnresposivenessTimeout) {
                    try {
                        desktopId = VirtualDesktop.IdFromHwnd(this.Handle);
                        e = null;
                        break;
                    } catch (COMException ex) {
                        e = ex;
                    }
                }

                if (desktopId == null) {
                    if (e != null)
                        throw new ShellUnresponsiveException(e);

                    this.Closed?.Invoke(this, EventArgs.Empty);
                    throw new WindowNotFoundException();
                }
                return desktopId != null && desktopId != Guid.Empty;
            }
        }

        public bool IsValid => IsWindow(this.Handle);
        public bool IsOnCurrentDesktop {
            get {
                if (!VirtualDesktop.HasMinimalSupport)
                    return true;

                try {
                    return VirtualDesktopHelper.IsCurrentVirtualDesktop(this.Handle);
                } catch (COMException e)
                    when (WinApi.HResult.TYPE_E_ELEMENTNOTFOUND.EqualsCode(e.HResult)) {
                    this.Closed?.Invoke(this, EventArgs.Empty);
                    throw new WindowNotFoundException(innerException: e);
                } catch (COMException e) {
                    e.ReportAsWarning();
                    return true;
                } catch (Win32Exception e) {
                    e.ReportAsWarning();
                    return true;
                } catch (ArgumentException e) {
                    e.ReportAsWarning();
                    return true;
                }
            }
        }

        [Obsolete("This API may not be supported in this version")]
        public bool IsVisibleOnAllDesktops {
            get {
                if (!VirtualDesktop.IsSupported)
                    return false;

                try {
                    return VirtualDesktop.IsPinnedWindow(this.Handle);
                } catch (COMException e)
                    when (WinApi.HResult.TYPE_E_ELEMENTNOTFOUND.EqualsCode(e.HResult)) {
                    this.Closed?.Invoke(this, EventArgs.Empty);
                    throw new WindowNotFoundException(innerException: e);
                } catch (COMException e) {
                    e.ReportAsWarning();
                    return false;
                } catch (Win32Exception e) {
                    e.ReportAsWarning();
                    return false;
                } catch (ArgumentException e) {
                    e.ReportAsWarning();
                    return false;
                }
            }
        }

        public bool IsResizable =>
            ((WindowStyles)GetWindowLong(this.Handle, WindowLongIndexFlags.GWL_STYLE))
            .HasFlag(WindowStyles.WS_SIZEFRAME);

        public Task<Exception> Activate() {
            Exception error = this.EnsureNotMinimized();
            return Task.FromResult(
                SetForegroundWindow(this.Handle) ? error : this.GetLastError());
        }

        public Task<Exception> BringToFront() {
            Exception issue = this.EnsureNotMinimized();
            if (!SetWindowPos(this.Handle, GetForegroundWindow(), 0, 0, 0, 0,
                              SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOACTIVATE |
                              SetWindowPosFlags.SWP_NOSIZE))
                issue = new Win32Exception();
            return Task.FromResult(issue);
        }

        public Task<Exception> SendToBottom() {
            Exception issue = this.EnsureNotMinimized();
            if (!SetWindowPos(this.Handle, HWND_BOTTOM, 0, 0, 0, 0,
                SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOACTIVATE |
                SetWindowPosFlags.SWP_NOSIZE))
                issue = new Win32Exception();
            return Task.FromResult(issue);
        }

        /// <summary>
        /// Unreliable, may fire multiple times
        /// </summary>
        public event EventHandler Closed;

        [MustUseReturnValue]
        Exception EnsureNotMinimized() {
            if (!IsIconic(this.Handle))
                return null;

            return ShowWindow(this.Handle, WindowShowStyle.SW_RESTORE) ? null : this.GetLastError();
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

        bool GetExcludeFromMargin() {
            GetWindowThreadProcessId(this.Handle, lpdwProcessId: out int processID);
            if (processID == 0)
                return false;
            try {
                Process process = Process.GetProcessById(processID);
                switch (process.ProcessName) {
                case "explorer":
                case "Everything":
                    return true;
                default:
                    return false;
                }
            } catch (ArgumentException) { } catch (InvalidOperationException) { }
            catch (Exception e) {
                Debug.WriteLine(e);
                return false;
            }
            return false;
        }

        static RECT GetSystemMargin(IntPtr handle) {
            PInvoke.HResult success = DwmGetWindowAttribute(handle, DwmApi.DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS,
                out var withMargin, Marshal.SizeOf<RECT>());
            if (!success.Succeeded) {
                Debug.WriteLine($"DwmGetWindowAttribute: {success.GetException()}");
                return new RECT();
            }

            if (!GetWindowRect(handle, out var noMargin)) {
                Debug.WriteLine($"GetWindowRect: {new Win32Exception()}");
                return new RECT();
            }

            return new RECT {
                left = withMargin.left - noMargin.left,
                top = withMargin.top - noMargin.top,
                right = noMargin.right - withMargin.right,
                bottom = noMargin.bottom - withMargin.bottom,
            };
        }

        public override int GetHashCode() => this.Handle.GetHashCode();

        [MustUseReturnValue]
        Exception GetLastError() {
            var exception = new Win32Exception();
            if (exception.NativeErrorCode == (int)WinApiErrorCode.ERROR_INVALID_WINDOW_HANDLE) {
                this.Closed?.Invoke(this, EventArgs.Empty);
                return new WindowNotFoundException(innerException: exception);
            } else
                return exception;
        }

        [DllImport("User32.dll", SetLastError = true)]
        static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);
        [DllImport("Dwmapi.dll")]
        static extern PInvoke.HResult DwmGetWindowAttribute(IntPtr hwnd, DwmApi.DWMWINDOWATTRIBUTE attribute, out RECT value, int valueSize);

        [DllImport("User32.dll")]
        static extern bool IsIconic(IntPtr hwnd);

        // ReSharper disable InconsistentNaming
        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        static readonly IntPtr HWND_TOP = IntPtr.Zero;
        static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        // ReSharper restore InconsistentNaming

        public TimeSpan ShellUnresposivenessTimeout { get; set; } = TimeSpan.FromMilliseconds(300);
    }
}
