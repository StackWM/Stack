namespace LostTech.Windows
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;
    using LostTech.Windows.Win32;
    using PInvoke;
    using Win32Exception = System.ComponentModel.Win32Exception;

    public sealed class Win32ScreenProvider: IScreenProvider, IDisposable
    {
        readonly Window detectorWindow;
        readonly HwndSource hwndSource;
        readonly ObservableCollection<Win32Screen> screens = new ObservableCollection<Win32Screen>();

        public ReadOnlyObservableCollection<Win32Screen> Screens { get; }

        public Win32ScreenProvider()
        {
            this.detectorWindow = new Window {
                ShowInTaskbar = false,
                Title = nameof(Win32ScreenProvider),
                WindowStyle = WindowStyle.None,
                Width = 1,
                Height = 1,
            };
            this.detectorWindow.Show();
            try {
                this.hwndSource = (HwndSource) PresentationSource.FromVisual(this.detectorWindow);
                if (this.hwndSource == null)
                    throw new CanNotFindDesktopException();
            } catch (Win32Exception e) {
                throw new CanNotFindDesktopException(e);
            } finally
            {
                this.detectorWindow.Hide();
            }
            this.Screens = new ReadOnlyObservableCollection<Win32Screen>(this.screens);
            this.hwndSource.AddHook(this.OnWindowMessage);
            this.UpdateScreens();
        }

        IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch ((User32.WindowMessage) msg) {
            case User32.WindowMessage.WM_DISPLAYCHANGE:
                this.UpdateScreens();
                break;
            }
            return IntPtr.Zero;
        }

        internal static IEnumerable<DisplayDevice> GetDisplayDevices()
        {
            var device = new DisplayDevice {Size = Marshal.SizeOf<DisplayDevice>()};
            for (int i = 0;
                DisplayDevice.EnumDisplayDevices(null, i, ref device, DisplayDevice.EnumDisplayDevicesFlags.None);
                i++)
                yield return device;
        }

        void UpdateScreens()
        {
            var knownScreens = new List<string>();
            foreach(var device in GetDisplayDevices()) {
                knownScreens.Add(device.Name);
                var screen = this.screens.FirstOrDefault(s => s.DeviceName == device.Name);
                if (screen == null) {
                    screen = new Win32Screen(device);
                    this.screens.Add(screen);
                }
                //Debug.WriteLine(
                //    $"name: {device.Name}; str: {device.String}; flags: {device.StateFlags}; ID: {device.ID}; key: {device.Key}");
            }

            for (int i = 0; i < this.screens.Count;) {
                var screen = this.screens[i];
                if (!knownScreens.Contains(screen.DeviceName))
                    this.screens.RemoveAt(i);
                else
                    i++;
            }
        }

        public void Dispose()
        {
            this.hwndSource.RemoveHook(this.OnWindowMessage);
            this.detectorWindow.Close();
        }
    }
}
