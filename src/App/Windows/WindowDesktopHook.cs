namespace LostTech.Stack.Windows
{
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Windows.Threading;

    using JetBrains.Annotations;
    using WindowsDesktop;
    using LostTech.Stack.Utils;
    using LostTech.Stack.WindowManagement;
    using Microsoft.AppCenter.Crashes;

    sealed class WindowDesktopHook: IDisposable, INotifyPropertyChanged
    {
        readonly DispatcherTimer timer;
        readonly bool ownsTimer;
        readonly IntPtr windowHandle;
        Guid? desktop;

        public Guid? DesktopID {
            get => this.desktop;
            private set {
                if (value == this.desktop)
                    return;

                this.desktop = value;
                this.OnPropertyChanged();
            }
        }

        // limit the number of consequent failures to determine window's desktop
        const int MaxStrikes = 5;
        int strikes;
        void TimerOnTick(object sender, EventArgs _) {
            try {
                var error = VirtualDesktop.TryGetIdFromHwnd(this.windowHandle, out var desktop);
                if (error.Succeeded) {
                    this.DesktopID = VirtualDesktop.IdFromHwnd(this.windowHandle);
                    this.strikes = 0;
                } else {
                    error.GetException().ReportAsWarning();
                    this.strikes++;
                }
            } catch (Win32Exception e) {
                Crashes.TrackError(e);
                this.strikes++;
            } catch (ArgumentException e) {
                Crashes.TrackError(e);
                this.strikes++;
            } catch (COMException e) {
                e.ReportAsWarning();
                this.strikes++;
            }

            if (this.strikes > MaxStrikes)
                this.Dispose();
        }

        WindowDesktopHook(DispatcherTimer timer, IntPtr windowHandle) {
            if (windowHandle == IntPtr.Zero)
                throw new ArgumentNullException(nameof(windowHandle));

            this.timer = timer ?? throw new ArgumentNullException(nameof(timer));
            this.windowHandle = windowHandle;
            this.timer.Tick += this.TimerOnTick;
            this.TimerOnTick(this, EventArgs.Empty);
        }

        WindowDesktopHook(IntPtr windowHandle) : this(CreateTimer(), windowHandle) {
            this.ownsTimer = true;
        }
        WindowDesktopHook(Win32Window window):
            this((window ?? throw new ArgumentNullException(nameof(window))).Handle) { }
        public WindowDesktopHook(IAppWindow window): this((Win32Window)window) { }

        public void Dispose() {
            this.timer.Tick -= this.TimerOnTick;
            if (this.ownsTimer)
                this.timer.Stop();
        }

        static DispatcherTimer CreateTimer() {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Start();
            return timer;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        void OnPropertyChanged([CallerMemberName] string propertyName = null) => 
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
