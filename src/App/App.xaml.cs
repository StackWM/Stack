namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Forms;
    using System.Windows.Input;
    using System.Windows.Interop;
    using System.Windows.Markup;
    using System.Xml;
    using Gma.System.MouseKeyHook;
    using LostTech.App;
    using LostTech.Stack.Behavior;
    using LostTech.Stack.InternalExtensions;
    using LostTech.Stack.Models;
    using LostTech.Stack.Utils;
    using LostTech.Stack.Windows;
    using LostTech.Stack.Zones;
    using PCLStorage;
    using PInvoke;
    using Application = System.Windows.Application;
    using DragAction = System.Windows.DragAction;
    using FileAccess = PCLStorage.FileAccess;
    using KeyEventArgs = System.Windows.Forms.KeyEventArgs;
    using Screen = LostTech.Windows.Screen;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        IKeyboardMouseEvents hook;
        WindowDragOperation dragOperation;
        ICollection<ScreenLayout> screenLayouts;
        private NotifyIcon trayIcon;
        IFolder localSettingsFolder;
        SettingsSet<ScreenLayouts, ScreenLayouts> screenLayoutSettings;
        readonly Window winApiHandler = new Window {
            Opacity = 0,
            AllowsTransparency = true,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        private bool dirty;
        DragHook dragHook;
        KeyboardArrowBehavior keyboardArrowBehavior;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.MainWindow = this.winApiHandler;

            this.localSettingsFolder = await FileSystem.Current.GetFolderFromPathAsync(AppData.FullName);
            var localSettings = XmlSettings.Create(this.localSettingsFolder);
            try {
                screenLayoutSettings = await localSettings.LoadOrCreate<ScreenLayouts, ScreenLayouts>("LayoutMap.xml");
            }
            catch (Exception) {
                var brokenFile = await this.localSettingsFolder.GetFileAsync("LayoutMap.xml");
                await brokenFile.DeleteAsync();
                this.screenLayoutSettings = await localSettings.LoadOrCreate<ScreenLayouts, ScreenLayouts>("LayoutMap.xml");
            }
            screenLayoutSettings.Autosave = true;
            var settings = new StackSettings {LayoutMap = screenLayoutSettings.Value};

            await this.StartLayout(settings);

            this.SetupScreenHooks();

            this.winApiHandler.Closed += (sender, args) => this.BeginShutdown();

            //this.MainWindow = new MyPos();
            //this.MainWindow.Show();
        }

        private void OnDragMove(object sender, DragHookEventArgs @event)
        {
            if (this.dragOperation == null) {
                return;
            }

            if (!this.dragOperation.Activated) {
                @event.Handled = true;
                this.ShowLayoutGrid();
            }

            var location = GetCursorPos();
            var dx = location.X - this.dragOperation.StartLocation.X;
            var dy = location.Y - this.dragOperation.StartLocation.Y;
            var currentPosition = location;
            if (Math.Abs(dx) < DragThreshold && Math.Abs(dy) < DragThreshold)
                return;

            var screen = this.screenLayouts.FirstOrDefault(layout => layout.GetPhysicalBounds().Contains(currentPosition));
            if (screen == null) {
                if (this.dragOperation.CurrentZone != null) {
                    this.dragOperation.CurrentZone.IsDragMouseOver = false;
                }
                return;
            }


            var relativeDropPoint = screen.PointFromScreen(currentPosition);
            var zone = screen.GetZone(relativeDropPoint);
            if (zone == null) {
                if (this.dragOperation.CurrentZone != null) {
                    this.dragOperation.CurrentZone.IsDragMouseOver = false;
                }
                return;
            }

            if (zone == this.dragOperation.CurrentZone) {
                this.dragOperation.CurrentZone.IsDragMouseOver = true;
                return;
            }

            if (this.dragOperation.CurrentZone != null) {
                this.dragOperation.CurrentZone.IsDragMouseOver = false;
            }
            zone.IsDragMouseOver = true;
            this.dragOperation.CurrentZone = zone;
        }

        void ShowLayoutGrid()
        {
            foreach (var screenLayout in this.screenLayouts) {
                screenLayout.Show();
                if (this.dirty || true) {
                    screenLayout.AdjustToClientArea();
                }
            }
            this.dirty = false;
            this.dragOperation.Activated = true;
        }

        private void OnDragEnd(object sender, DragHookEventArgs @event)
        {
            if (this.dragOperation == null)
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
            this.Move(window, zone);
        }

        void Move(IntPtr window, Zone zone)
        {
            Rect targetBounds = zone.Target.GetPhysicalBounds();
            if (!User32.MoveWindow(window, (int) targetBounds.Left, (int) targetBounds.Top, (int) targetBounds.Width,
                (int) targetBounds.Height, true)) {
                this.trayIcon.BalloonTipIcon = ToolTipIcon.Error;
                this.trayIcon.BalloonTipTitle = "Can't move";
                this.trayIcon.BalloonTipText = new System.ComponentModel.Win32Exception().Message;
                this.trayIcon.ShowBalloonTip(1000);
            }
            else {
                // TODO: option to not activate on move
                User32.SetForegroundWindow(window);
            }
        }

        static Point GetCursorPos()
        {
            if (!User32.GetCursorPos(out var cursorPos))
                throw new System.ComponentModel.Win32Exception();
            return new Point(cursorPos.x, cursorPos.y);
        }

        private void GlobalKeyDown(object sender, KeyEventArgs @event)
        {
            if (@event.KeyData == Keys.Escape && this.dragOperation != null) {
                @event.Handled = true;
                StopDrag(this.dragOperation.Window);
                return;
            }
        }

        void StopDrag(IntPtr window)
        {
            if (this.dragOperation.CurrentZone != null) {
                this.dragOperation.CurrentZone.IsDragMouseOver = false;
            }
            foreach (var screenLayout in this.screenLayouts) {
                screenLayout.Hide();
            }
            User32.SetForegroundWindow(this.dragOperation.OriginalActiveWindow);
            this.dragOperation = null;
        }

        void OnDragStart(object sender, DragHookEventArgs @event)
        {
            this.dragOperation = DragStart(new Point(@event.X, @event.Y));
            @event.Handled = this.dragOperation != null;
        }

        void OnDragStartPreview(object sender, DragHookEventArgs args)
        {
            args.Handled = DragStart(new Point(args.X, args.Y)) != null;
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
            return new WindowDragOperation(child, location) {
                OriginalActiveWindow = User32.GetForegroundWindow(),
            };
        }

        public async void BeginShutdown()
        {
            Debug.WriteLine("shutdown requested");
            this.hook.Dispose();
            this.dragHook.Dispose();
            this.keyboardArrowBehavior.Dispose();
            this.trayIcon?.Dispose();

            if (this.screenLayoutSettings != null)
            {
                this.screenLayoutSettings.ScheduleSave();
                var settings = this.screenLayoutSettings;
                this.screenLayoutSettings = null;
                await settings.DisposeAsync();

                //await Task.Delay(5000);
                //Debug.WriteLine("delayed message");
            }

            this.Shutdown();
        }

        async Task StartLayout(StackSettings stackSettings)
        {
            var layoutsDirectory = await this.localSettingsFolder.CreateFolderAsync("Layouts", CreationCollisionOption.OpenIfExists);

            var defaultLayout = new Lazy<FrameworkElement>(() => this.LoadLayoutOrDefault(layoutsDirectory, "Default.xaml").Result);
            var primary = Screen.Primary;
            var screens = Screen.AllScreens.ToArray();
            FrameworkElement[] layouts = await Task.WhenAll(screens
                .Select(screen => GetLayoutForScreen(screen, stackSettings, layoutsDirectory))
                .ToArray());
            var screenLayouts = new List<ScreenLayout>();
            for (var screenIndex = 0; screenIndex < screens.Length; screenIndex++)
            {
                var screen = screens[screenIndex];
                var layout = new ScreenLayout();
                layout.Closed += (sender, args) => this.BeginShutdown();
                layout.QueryContinueDrag += (sender, args) => args.Action = DragAction.Cancel;
                // windows must be visible before calling AdjustToClientArea,
                // otherwise final position is unpredictable
                layout.Opacity = 0;
                layout.Show();
                layout.AdjustToClientArea(screen);
                layout.Content = layouts[screenIndex];
                layout.Title = $"{screenIndex}:{layout.Left}x{layout.Top}";
                layout.DataContext = screen;
                layout.Hide();
                layout.Opacity = 0.7;
                screenLayouts.Add(layout);
            }
            this.screenLayouts = screenLayouts;

            this.BindHandlers();

            this.trayIcon = (await TrayIcon.StartTrayIcon(layoutsDirectory, stackSettings)).Icon;
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
            this.keyboardArrowBehavior = new KeyboardArrowBehavior(this.hook, this.screenLayouts, this.Move);
            this.dragHook = new DragHook(MouseButtons.Middle, this.hook);
            this.dragHook.DragStartPreview += this.OnDragStartPreview;
            this.dragHook.DragStart += this.OnDragStart;
            this.dragHook.DragEnd += this.OnDragEnd;
            this.dragHook.DragMove += this.OnDragMove;
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
            if (file == null) {
                Debug.WriteLine($"layout {fileName} was not found. loading default");
                return this.MakeDefaultLayout();
            }

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

        private void SetupScreenHooks()
        {
            this.winApiHandler.Show();
            var hwnd = (HwndSource)PresentationSource.FromVisual(this.winApiHandler);
            hwnd.AddHook(this.OnWindowMessage);
            this.winApiHandler.Hide();
        }

        private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch ((User32.WindowMessage)msg) {
            case User32.WindowMessage.WM_DISPLAYCHANGE:
                this.dirty = true;
                return IntPtr.Zero;
            }
            return IntPtr.Zero;
        }

        public static Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        public int DragThreshold { get; private set; } = 40;
    }
}
