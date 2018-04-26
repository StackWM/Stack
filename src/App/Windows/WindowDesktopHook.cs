namespace LostTech.Stack.Windows
{
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Windows.Threading;

    using JetBrains.Annotations;
    using Microsoft.HockeyApp;
    using WindowsDesktop;
    using LostTech.Stack.Models;

    sealed class WindowDesktopHook: IDisposable, INotifyPropertyChanged
    {
        readonly DispatcherTimer timer;
        readonly bool ownsTimer;
        readonly IntPtr windowHandle;
        VirtualDesktop desktop;

        public VirtualDesktop Desktop {
            get => this.desktop;
            private set {
                if (value?.Id == this.desktop?.Id)
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
                this.Desktop = VirtualDesktop.FromHwnd(this.windowHandle);
                this.strikes = 0;
            } catch (Win32Exception e) {
                HockeyClient.Current.TrackException(e);
                this.strikes++;
            } catch (ArgumentException e) {
                HockeyClient.Current.TrackException(e);
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
