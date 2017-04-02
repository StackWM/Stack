namespace LostTech.Windows
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows;
    using System.Windows.Interop;
    using LostTech.Stack.Compat;
    using FormsScreen = System.Windows.Forms.Screen;
    using static System.FormattableString;

    public sealed class Screen
    {
        FormsScreen CurrentScreen
        {
            get {
                if (this.dirty) {
                    var newScreen = FormsScreen.AllScreens.FirstOrDefault(screen => screen.DeviceName == this.device);
                    if (newScreen != null && newScreen != this.currentScreen) {
                        this.currentScreen = newScreen;
                        this.detectorWindow.Left = this.currentScreen.WorkingArea.Left;
                        this.detectorWindow.Top = this.currentScreen.WorkingArea.Top;
                        this.detectorWindow.Show();
                        this.detectorWindow.Hide();
                        this.dirty = false;
                    }
                }
                return this.currentScreen;
            }
        }

        readonly string device;
        FormsScreen currentScreen;
        bool dirty = false;
        readonly Window detectorWindow;

        Screen(FormsScreen screen)
        {
            this.currentScreen = screen ?? throw new ArgumentNullException(nameof(screen));
            this.device = screen.DeviceName;
            this.detectorWindow = new Window {
                Left = this.CurrentScreen.WorkingArea.Left,
                Top = this.CurrentScreen.WorkingArea.Top,
                ShowInTaskbar = false,
                Title = screen.DeviceName,
                WindowStyle = WindowStyle.None,
                Width = 1,
                Height = 1,
            };
            this.detectorWindow.Show();
            try {
                this.PresentationSource = PresentationSource.FromVisual(this.detectorWindow);
            }
            finally {
                this.detectorWindow.Hide();
            }
            var window = ((HwndSource) this.PresentationSource);
            WtsApi.WTSRegisterSessionNotification(window.Handle, NotifySessionFlags.ThisSessionOnly);
            window.AddHook(OnWindowMessage);
        }

        IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_WTSSESSION_CHANGE = 0x02B1;
            const int WM_DPICHANGED = 0x02E0;

            switch (msg) {
            case WM_WTSSESSION_CHANGE:
            case WM_DPICHANGED:
                this.dirty = true;
                break;
            }
            return IntPtr.Zero;
        }

        // TODO: track updates
        public PresentationSource PresentationSource { get; }
        public string ID => Invariant($"{Array.IndexOf(AllScreens.ToArray(), this):D3}");
        public string Name => Invariant($"{this.ID} ({this.CurrentScreen.Bounds.Width}x{this.CurrentScreen.Bounds.Height})");
        public bool IsPrimary => this.CurrentScreen.Primary;

        public static Screen Primary => AllScreens.Single(screen => screen.CurrentScreen.Primary);
        public static IEnumerable<Screen> AllScreens { get; } =
            new ReadOnlyCollection<Screen>(FormsScreen.AllScreens.Select(formsScreen => new Screen(formsScreen)).ToArray());

        public Rect WorkingArea => this.CurrentScreen.WorkingArea.ToWPF();
    }
}
