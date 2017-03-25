namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Configuration;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Media;
    using System.IO;
    using System.Reflection;
    using System.Windows.Controls;
    using System.Windows.Forms;
    using System.Xml;
    using System.Windows.Markup;
    using static System.FormattableString;
    using Gma.System.MouseKeyHook;
    using Application = System.Windows.Application;
    using Screen = LostTech.Windows.Screen;
    using LostTech.Stack.Compat;
    using LostTech.Stack.InternalExtensions;
    using LostTech.Stack.Models;
    using LostTech.Stack.Zones;
    using PCLStorage;
    using PInvoke;
    using DragDropEffects = System.Windows.DragDropEffects;
    using FileAccess = PCLStorage.FileAccess;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        IKeyboardMouseEvents hook;
        WindowDragOperation dragOperation;
        ICollection<ScreenLayout> screenLayouts;
        private NotifyIcon trayIcon;
        IFolder localSettings;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Contains("--restart")) {
                var self = Process.GetCurrentProcess();
                foreach (var process in Process.GetProcessesByName("Stack.exe")) {
                    if (process.Id != self.Id)
                        process.CloseMainWindow();
                }
            }

            this.localSettings = await FileSystem.Current.GetFolderFromPathAsync(AppData.FullName);
            var settings = await StackSettings.Load(this.localSettings);

            await this.StartLayout(settings);

            //this.MainWindow = new MyPos();
            //this.MainWindow.Show();
        }

        private void GlobalKeyDown(object sender, KeyEventArgs @event)
        {
            if (@event.KeyData == Keys.Escape && this.dragOperation != null) {
                StopDrag(this.dragOperation.Window);
                return;
            }
        }

        private void GlobalMouseMove(object sender, MouseEventExtArgs @event)
        {
            if (this.dragOperation == null)
                return;

            var location = GetCursorPos();
            var dx = location.X - this.dragOperation.StartLocation.X;
            var dy = location.Y - this.dragOperation.StartLocation.Y;
            var currentPosition = location;
            if (Math.Abs(dx) < DragThreshold && Math.Abs(dy) < DragThreshold)
                return;

            var screen = this.screenLayouts.FirstOrDefault(layout => layout.GetPhysicalBounds().Contains(currentPosition));
            if (screen == null) {
                if (this.dragOperation.CurrentZone != null)
                    this.dragOperation.CurrentZone.IsDragMouseOver = false;
                return;
            }
            var relativeDropPoint = screen.PointFromScreen(currentPosition);
            var zone = screen.GetZone(relativeDropPoint);
            if (zone == null) {
                if (this.dragOperation.CurrentZone != null)
                    this.dragOperation.CurrentZone.IsDragMouseOver = false;
                return;
            }

            if (zone == this.dragOperation.CurrentZone) {
                this.dragOperation.CurrentZone.IsDragMouseOver = true;
                return;
            }

            if (this.dragOperation.CurrentZone != null)
                this.dragOperation.CurrentZone.IsDragMouseOver = false;
            zone.IsDragMouseOver = true;
            this.dragOperation.CurrentZone = zone;
        }

        private void GlobalMouseUp(object sender, MouseEventExtArgs @event)
        {
            if (@event.Button != MouseButtons.Middle || this.dragOperation == null)
                return;

            var location = GetCursorPos();
            var dx = location.X - this.dragOperation.StartLocation.X;
            var dy = location.Y - this.dragOperation.StartLocation.Y;
            var dropPoint = location;
            var window = this.dragOperation.Window;
            this.StopDrag(window);
            if (Math.Abs(dx) < DragThreshold && Math.Abs(dy) < DragThreshold)
                return;

            var screen = this.screenLayouts.SingleOrDefault(layout => layout.GetPhysicalBounds().Contains(dropPoint));
            if (screen == null)
                return;
            var relativeDropPoint = screen.PointFromScreen(dropPoint);
            var zone = screen.GetZone(relativeDropPoint);
            if (zone == null)
                return;
            Rect targetBounds = zone.Target.GetPhysicalBounds();
            if (!User32.MoveWindow(window, (int) targetBounds.Left, (int) targetBounds.Top, (int) targetBounds.Width,
                (int) targetBounds.Height, true))
                throw new System.ComponentModel.Win32Exception();
        }

        static Point GetCursorPos()
        {
            if (!User32.GetCursorPos(out var cursorPos))
                throw new System.ComponentModel.Win32Exception();
            return new Point(cursorPos.x, cursorPos.y);
        }

        void StopDrag(IntPtr window)
        {
            if (this.dragOperation.CurrentZone != null)
                this.dragOperation.CurrentZone.IsDragMouseOver = false;
            foreach (var screenLayout in this.screenLayouts) {
                screenLayout.Hide();
            }
            User32.SetForegroundWindow(this.dragOperation.OriginalActiveWindow);
            this.dragOperation = null;
        }

        void GlobalMouseDown(object sender, MouseEventExtArgs @event)
        {
            if (@event.Button != MouseButtons.Middle)
                return;

            this.dragOperation = DragStart(GetCursorPos());
        }

        private WindowDragOperation DragStart(Point location)
        {
            var desktop = User32.GetDesktopWindow();
            var point = new POINT {x = (int)location.X, y = (int)location.Y};
            var child = User32.ChildWindowFromPointEx(desktop, point,
                User32.ChildWindowFromPointExFlags.CWP_SKIPINVISIBLE);
            //var child = User32.WindowFromPhysicalPoint(point);
            if (child == IntPtr.Zero || true.Equals(User32.GetWindowText(child)?.EndsWith("Remote Desktop Connection")))
                return null;
            foreach (var screenLayout in this.screenLayouts) {
                screenLayout.Show();
                screenLayout.Activate();
            }
            return new WindowDragOperation(child, location) {
                OriginalActiveWindow = User32.GetForegroundWindow(),
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            this.hook.Dispose();
            this.trayIcon?.Dispose();

            base.OnExit(e);
        }

        async Task StartLayout(StackSettings stackSettings)
        {
            this.BindHandlers();

            var layoutsDirectory = await this.localSettings.CreateFolderAsync("Layouts", CreationCollisionOption.OpenIfExists);

            var defaultLayout = new Lazy<FrameworkElement>(() => this.LoadLayoutOrDefault(layoutsDirectory, "Default.xaml").Result);
            var primary = Screen.Primary;
            var screens = Screen.AllScreens.ToArray();
            var layouts = screens
                .Select(screen => GetLayoutForScreen(screen, stackSettings, layoutsDirectory))
                .ToArray();
            var screenLayouts = new List<ScreenLayout>();
            for (var screenIndex = 0; screenIndex < screens.Length; screenIndex++)
            {
                var screen = screens[screenIndex];
                var layout = new ScreenLayout();
                // windows must be visible before calling AdjustToClientArea,
                // otherwise final position is unpredictable
                layout.Show();
                layout.AdjustToClientArea(screen);
                layout.Content = layouts[screenIndex];
                layout.Title = $"{screenIndex}:{layout.Left}x{layout.Top}";
                layout.Closed += (sender, args) => this.Shutdown();
                layout.DataContext = screen;
                this.MainWindow = layout;
                layout.Hide();
                screenLayouts.Add(layout);
            }
            this.screenLayouts = screenLayouts;

            this.trayIcon = await TrayIcon.StartTrayIcon(layoutsDirectory, stackSettings);
        }

        async Task<FrameworkElement> GetLayoutForScreen(Screen screen, StackSettings settings, IFolder layoutsDirectory)
        {
            var layout = settings.LayoutMap.GetPreferredLayout(screen);
            if (layout == null)
                return this.MakeDefaultLayout();
            return await LoadLayoutOrDefault(layoutsDirectory, layout);
        }

        private void BindHandlers()
        {
            // TODO: MIT license for MouseKeyboardActivityMonitor
            this.hook = Hook.GlobalEvents();
            this.hook.MouseDownExt += this.GlobalMouseDown;
            this.hook.MouseUpExt += this.GlobalMouseUp;
            this.hook.MouseMoveExt += this.GlobalMouseMove;
            this.hook.KeyDown += this.GlobalKeyDown;
        }

        private async Task<FrameworkElement> LoadLayoutOrDefault(IFolder layoutDirectory, string fileName)
        {
            // TODO: SEC: untrusted XAML https://msdn.microsoft.com/en-us/library/ee856646(v=vs.110).aspx

            if (layoutDirectory == null)
                throw new ArgumentNullException(nameof(layoutDirectory));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentNullException(nameof(fileName));


            if (Path.GetInvalidFileNameChars().Any(fileName.Contains))
                throw new ArgumentException();

            var file = await layoutDirectory.GetFileOrNull(fileName);
            if (file == null)
                return this.MakeDefaultLayout();

            using (var stream = await file.OpenAsync(FileAccess.Read))
            using (var xmlReader = XmlReader.Create(stream)) {
                var layout = (FrameworkElement)XamlReader.Load(xmlReader);
                Debug.WriteLine($"loaded layout {fileName}");
                return layout;
            }
        }

        FrameworkElement MakeDefaultLayout() => new Grid {
            Children = {
                new Zone {},
            }
        };

        public static DirectoryInfo AppData {
            get {
                // TODO: implement roaming
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var path = Path.Combine(appData, "Lost Tech LLC", nameof(LostTech.Stack));
                return Directory.CreateDirectory(path);
            }
        }

        public static Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        public int DragThreshold { get; private set; } = 40;
    }
}
