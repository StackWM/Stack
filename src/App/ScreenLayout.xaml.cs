namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
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
    public partial class ScreenLayout
    {
        HwndSource handle;

        public ScreenLayout()
        {
            this.InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.handle = (HwndSource)PresentationSource.FromVisual(this);
            // ReSharper disable once PossibleNullReferenceException
            this.handle.AddHook(this.OnWindowMessage);
        }

        protected override void OnClosed(EventArgs e)
        {
            this.handle?.RemoveHook(this.OnWindowMessage);
            this.handle = null;
            base.OnClosed(e);
        }

        protected bool IsHandleInitialized => this.handle != null;

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

        public static void AdjustToClientArea(Window window, Win32Screen screen)
        {
            if (screen == null)
                throw new ArgumentNullException(nameof(screen));

            Debug.WriteLine(screen.WorkingArea);
            var transformFromDevice = screen.TransformFromDevice;
            var topLeft = transformFromDevice.Transform(screen.WorkingArea.TopLeft);
            window.Left = topLeft.X;
            window.Top = topLeft.Y;

            var size = new Vector(screen.WorkingArea.Width, screen.WorkingArea.Height);
            var dimensions = transformFromDevice.Transform(size);
            window.Width = dimensions.X;
            window.Height = dimensions.Y;
            Debug.WriteLine($"{screen.ID} WPF: {window.Width}x{window.Height}");
        }

        public void AdjustToClientArea()
        {
            if (this.DataContext is Win32Screen screen)
                AdjustToClientArea(this, screen);
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
            if (delay == this.idleAdjustDelay && this.IsLoaded)
                this.AdjustToScreen();
        }

        async void AdjustToScreen()
        {
            for (int retry = 0; retry < 8; retry++) {
                if (this.Screen == null || !this.IsHandleInitialized)
                    return;

                var opacity = this.Opacity;
                var visibility = this.Visibility;
                this.Opacity = 0;
                AdjustToClientArea(this, this.Screen);
                try {
                    this.Show();
                } catch (InvalidOperationException) {
                    await Task.Delay(400);
                    continue;
                }
                this.Visibility = visibility;
                this.Opacity = opacity;
                return;
            }
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

    static class ScreenLayoutExtensions
    {
        public static IEnumerable<ScreenLayout> Active(this IEnumerable<ScreenLayout> layouts)
            => layouts.Where(layout => layout.Screen.IsActive);
    }
}
