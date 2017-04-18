﻿namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media;
    using LostTech.Stack.Zones;
    using LostTech.Windows;
    using PInvoke;

    /// <summary>
    /// Interaction logic for ScreenLayout.xaml
    /// </summary>
    public partial class ScreenLayout : Window
    {
        public ScreenLayout()
        {
            InitializeComponent();

            this.Loaded += OnLoaded;
        }

        void OnLoaded(object sender, RoutedEventArgs routedEventArgs) {
            var handle = (HwndSource)PresentationSource.FromVisual(this);
            handle.AddHook(this.OnWindowMessage);
        }

        private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch ((User32.WindowMessage) msg) {
            case User32.WindowMessage.WM_SETTINGCHANGE:
                this.AdjustToScreenWhenIdle();
                break;
            }
            return IntPtr.Zero;
        }

        public Win32Screen Screen {
            get => (Win32Screen) this.DataContext;
            set => this.DataContext = value;
        }

        public void AdjustToClientArea(Win32Screen screen)
        {
            if (screen == null)
                throw new ArgumentNullException(nameof(screen));

            Debug.WriteLine(screen.WorkingArea);
            var transformFromDevice = screen.TransformFromDevice;
            var topLeft = transformFromDevice.Transform(screen.WorkingArea.TopLeft);
            this.Left = topLeft.X;
            this.Top = topLeft.Y;

            var size = new Vector(screen.WorkingArea.Width, screen.WorkingArea.Height);
            var dimensions = transformFromDevice.Transform(size);
            this.Width = dimensions.X;
            this.Height = dimensions.Y;
        }

        public void AdjustToClientArea()
        {
            if (this.DataContext is Win32Screen screen)
                this.AdjustToClientArea(screen);
            else
                throw new InvalidOperationException();
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);

            this.AdjustToScreenWhenIdle();
        }

        Task idleAdjustDelay;
        async void AdjustToScreenWhenIdle()
        {
            var delay = Task.Delay(millisecondsDelay: 2000);
            this.idleAdjustDelay = delay;
            await delay;
            if (delay == this.idleAdjustDelay)
                this.AdjustToScreen();
        }

        void AdjustToScreen()
        {
            if (this.Screen == null)
                return;

            var opacity = this.Opacity;
            var visibility = this.Visibility;
            this.Opacity = 0;
            this.AdjustToClientArea(this.Screen);
            this.Show();
            this.Opacity = opacity;
            this.Visibility = visibility;
        }

        public IEnumerable<Zone> Zones
        {
            get {
                var queue = new Queue<DependencyObject>();
                queue.Enqueue(this);
                while (queue.Count > 0) {
                    var element = queue.Dequeue();
                    if (element is Zone zone)
                        yield return zone;

                    int childrenCount = VisualTreeHelper.GetChildrenCount(element);
                    for (int child = 0; child < childrenCount; child++) {
                        queue.Enqueue(VisualTreeHelper.GetChild(element, child));
                    }
                }
            }
        }

        internal Zone GetZone(Point dropPoint)
        {
            Zone result = null;
            VisualTreeHelper.HitTest(this,
                target => {
                    result = target as Zone;
                    return result == null ? HitTestFilterBehavior.Continue : HitTestFilterBehavior.Stop;
                },
                _ => HitTestResultBehavior.Stop,
                new PointHitTestParameters(dropPoint));
            return result;
        }
    }
}
