namespace LostTech.Windows
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media;
    using LostTech.Stack.Compat;
    using LostTech.Windows.Win32;
    using PInvoke;
    using static System.FormattableString;
    using static LostTech.Windows.Win32.DisplayDevice;
    using FormsScreen = System.Windows.Forms.Screen;

    public sealed class Win32Screen: INotifyPropertyChanged
    {
        DisplayDevice displayDevice;
        Rect workingArea;

        void EnsureUpToDate()
        {
            if (!this.dirty)
                return;

            this.detectorWindow.Left = this.WorkingArea.Left;
            this.detectorWindow.Top = this.WorkingArea.Top;
            this.detectorWindow.Show();
            this.detectorWindow.Hide();
            this.dirty = false;
        }

        bool dirty;
        readonly Window detectorWindow;
        readonly PresentationSource presentationSource;

        internal Win32Screen(DisplayDevice displayDevice)
        {
            this.displayDevice = displayDevice;
            this.detectorWindow = new Window {
                Left = this.WorkingArea.Left,
                Top = this.WorkingArea.Top,
                ShowInTaskbar = false,
                Title = this.DeviceName,
                WindowStyle = WindowStyle.None,
                Width = 1,
                Height = 1,
            };
            this.detectorWindow.Show();
            try {
                this.presentationSource = PresentationSource.FromVisual(this.detectorWindow);
            }
            finally {
                this.detectorWindow.Hide();
            }
            var window = (HwndSource) this.presentationSource;
            WtsApi.WTSRegisterSessionNotification(window.Handle, NotifySessionFlags.ThisSessionOnly);
            window.AddHook(this.OnWindowMessage);
            this.workingArea = this.GetWorkingArea();
        }

        IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch ((User32.WindowMessage)msg) {
            case User32.WindowMessage.WM_DPICHANGED:
            case User32.WindowMessage.WM_SETTINGCHANGE:
            case User32.WindowMessage.WM_DISPLAYCHANGE:
                var oldDeviceInfo = this.displayDevice;
                this.displayDevice = Win32ScreenProvider.GetDisplayDevices()
                    .FirstOrDefault(device => device.Name == this.DeviceName);
                if (!this.displayDevice.IsValid) {
                    this.displayDevice = oldDeviceInfo;
                    this.displayDevice.StateFlags &= ~(DisplayDeviceStateFlags.AttachedToDesktop |
                                                       DisplayDeviceStateFlags.PrimaryDevice);
                }
                if (oldDeviceInfo.IsActive != this.displayDevice.IsActive)
                    this.OnPropertyChanged(nameof(this.IsActive));
                if (oldDeviceInfo.IsPrimary != this.displayDevice.IsPrimary)
                    this.OnPropertyChanged(nameof(this.IsPrimary));

                var newWorkingArea = this.GetWorkingArea();
                if (!this.workingArea.Equals(newWorkingArea)) {
                    this.workingArea = newWorkingArea;
                    this.OnPropertyChanged(nameof(this.WorkingArea));
                }
                this.dirty = true;
                break;
            default:
                return IntPtr.Zero;
            }
            return IntPtr.Zero;
        }

        public Matrix TransformFromDevice
        {
            get {
                this.EnsureUpToDate();
                return this.presentationSource.CompositionTarget.TransformFromDevice;
            }
        }
        public bool IsActive => this.displayDevice.IsActive;
        public string ID => this.DeviceName.Replace(@"\\.\DISPLAY", "");
        internal string DeviceName => this.displayDevice.Name;
        public override string ToString() => Invariant($"{this.ID} ({this.WorkingArea.Width}x{this.WorkingArea.Height})");
        public bool IsPrimary => this.displayDevice.StateFlags.HasFlag(DisplayDeviceStateFlags.PrimaryDevice);

        public Rect WorkingArea => this.GetWorkingArea();

        Rect GetWorkingArea()
        {
            return FormsScreen.AllScreens
                       .FirstOrDefault(s => s.DeviceName == this.DeviceName)
                       ?.WorkingArea.ToWPF()
                   ?? new Rect();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string propertyName)
            => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
