namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Forms;
    using System.Windows.Interop;
    using System.Windows.Markup;
    using System.Windows.Threading;
    using System.Xml;
    using Gma.System.MouseKeyHook;
    using LostTech.App;
    using LostTech.Stack.Behavior;
    using LostTech.Stack.InternalExtensions;
    using LostTech.Stack.Models;
    using LostTech.Stack.Utils;
    using LostTech.Stack.Windows;
    using LostTech.Stack.Zones;
    using LostTech.Windows;
    using Microsoft.HockeyApp;
    using PCLStorage;
    using PInvoke;
    using Application = System.Windows.Application;
    using DragAction = System.Windows.DragAction;
    using FileAccess = PCLStorage.FileAccess;
    using KeyEventArgs = System.Windows.Forms.KeyEventArgs;
    using static PInvoke.User32;
    using MessageBox = System.Windows.MessageBox;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        IKeyboardMouseEvents hook;
        WindowDragOperation dragOperation;
        ICollection<ScreenLayout> screenLayouts;
        NotifyIcon trayIcon;
        IFolder localSettingsFolder, roamingSettingsFolder;
        SettingsSet<ScreenLayouts, ScreenLayouts> screenLayoutSettings;
        readonly Window winApiHandler = new Window {
            Opacity = 0,
            AllowsTransparency = true,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Width=0,Height=0,
            Title = nameof(winApiHandler),
        };
        DragHook dragHook;
        KeyboardArrowBehavior keyboardArrowBehavior;
        DispatcherTimer updateTimer;
        readonly IScreenProvider screenProvider = new Win32ScreenProvider();
        ObservableDirectory layoutsDirectory;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Contains("--jit-debugging"))
                EnableJitDebugging();

            await EnableHockeyApp();

            StopRunningInstances();

            this.MainWindow = this.winApiHandler;
            this.winApiHandler.Show();

            this.BeginCheckForUpdates();
            this.updateTimer = new DispatcherTimer(DispatcherPriority.Background) {Interval = TimeSpan.FromDays(1), IsEnabled = true};
            this.updateTimer.Tick += (_, __) => this.BeginCheckForUpdates();

            this.localSettingsFolder = await FileSystem.Current.GetFolderFromPathAsync(AppData.FullName);
            this.roamingSettingsFolder = await FileSystem.Current.GetFolderFromPathAsync(RoamingAppData.FullName);
            var localSettings = XmlSettings.Create(this.localSettingsFolder);
            try {
                screenLayoutSettings = await localSettings.LoadOrCreate<ScreenLayouts, ScreenLayouts>("LayoutMap.xml");
            }
            catch (Exception) {
                var brokenFile = await this.localSettingsFolder.GetFileAsync("LayoutMap.xml");
                await brokenFile.DeleteAsync();
                this.screenLayoutSettings = await localSettings.LoadOrCreate<ScreenLayouts, ScreenLayouts>("LayoutMap.xml");
                this.screenLayoutSettings.ScheduleSave();
            }
            screenLayoutSettings.Autosave = true;
            var settings = new StackSettings {LayoutMap = screenLayoutSettings.Value};

            this.SetupScreenHooks();

            this.winApiHandler.Closed += (sender, args) => this.BeginShutdown();

            await this.StartLayout(settings);

            // this must be the last, so that mouse won't lag while we are loading
            this.BindHandlers();

            //this.MainWindow = new MyPos();
            //this.MainWindow.Show();
        }

        void BeginCheckForUpdates()
        {
            HockeyClient.Current.CheckForUpdatesAsync(autoShowUi: true, shutdownActions: () => {
                this.BeginShutdown();
                return true;
            }).GetAwaiter();
        }

        static void StopRunningInstances()
        {
            var currentWindow = IntPtr.Zero;
            while (true) {
                currentWindow = FindWindowEx(IntPtr.Zero, currentWindow, null, nameof(winApiHandler));
                if (currentWindow != IntPtr.Zero)
                    PostMessage(currentWindow, WindowMessage.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                else
                    break;
            }

            var selfID = Process.GetCurrentProcess().Id;
            foreach(var instance in Process.GetProcessesByName("Stack")) {
                if (instance.Id == selfID)
                    continue;

                if (!instance.WaitForExit(5000)) {
                    MessageBox.Show($"Failed to stop Stack instance with process ID {instance.Id}. Will exit.",
                        "Other instance is still running", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(-1);
                }
                else {
                    Debug.WriteLine($"Stopped [{instance.Id}] Stack.exe");
                }
            }
        }

        static Task EnableHockeyApp()
        {
#if DEBUG
            HockeyClient.Current.Configure("be80a4a0381c4c37bc187d593ac460f9 ");
            ((HockeyClient)HockeyClient.Current).OnHockeySDKInternalException += (sender, args) =>
            {
                if (Debugger.IsAttached) { Debugger.Break(); }
            };
#else
            HockeyClient.Current.Configure("6037e69fa4944acc9d83ef7682e60732");
#endif
            return HockeyClient.Current.SendCrashesAsync();
        }

        static void EnableJitDebugging()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, args) => Debugger.Launch();
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
            var currentPosition = location;

            var screen = this.screenLayouts.FirstOrDefault(layout => layout.GetPhysicalBounds().Contains(currentPosition));
            if (screen == null) {
                if (this.dragOperation.CurrentZone != null) {
                    this.dragOperation.CurrentZone.IsDragMouseOver = false;
                }
                return;
            }


            var relativeDropPoint = screen.PointFromScreen(currentPosition);
            var zone = screen.GetZone(relativeDropPoint)?.GetFinalTarget();
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
            }
            this.dragOperation.Activated = true;
        }

        private void OnDragEnd(object sender, DragHookEventArgs @event)
        {
            if (this.dragOperation == null)
                return;

            var location = GetCursorPos();
            var dropPoint = location;
            var window = this.dragOperation.Window;
            this.StopDrag(window);

            var screen = this.screenLayouts.SingleOrDefault(layout => layout.GetPhysicalBounds().Contains(dropPoint));
            if (screen == null)
                return;
            var relativeDropPoint = screen.PointFromScreen(dropPoint);
            var zone = screen.GetZone(relativeDropPoint)?.GetFinalTarget();
            if (zone == null)
                return;
            this.Move(window, zone);
        }

        async void Move(IntPtr window, Zone zone)
        {
            Rect targetBounds = zone.Target.GetPhysicalBounds();
            if (!MoveWindow(window, (int) targetBounds.Left, (int) targetBounds.Top, (int) targetBounds.Width,
                (int) targetBounds.Height, true)) {
                this.trayIcon.BalloonTipIcon = ToolTipIcon.Error;
                this.trayIcon.BalloonTipTitle = "Can't move";
                this.trayIcon.BalloonTipText = new System.ComponentModel.Win32Exception().Message;
                this.trayIcon.ShowBalloonTip(1000);
            }
            else {
                // TODO: option to not activate on move
                SetForegroundWindow(window);
                await Task.Yield();
                MoveWindow(window, (int)targetBounds.Left, (int)targetBounds.Top, (int)targetBounds.Width,
                    (int)targetBounds.Height, true);
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
            SetForegroundWindow(this.dragOperation.OriginalActiveWindow);
            this.dragOperation = null;
        }

        void OnDragStart(object sender, DragHookEventArgs @event)
        {
            this.dragOperation = DragStart();
            @event.Handled = this.dragOperation != null;
        }

        void OnDragStartPreview(object sender, DragHookEventArgs args)
        {
            args.Handled = DragStart() != null;
        }

        static WindowDragOperation DragStart()
        {
            //var point = new POINT { x = (int)location.X, y = (int)location.Y };
            User32.GetCursorPos(out var point);
            var desktop = GetDesktopWindow();
            var child = ChildWindowFromPointEx(desktop, point, ChildWindowFromPointExFlags.CWP_SKIPINVISIBLE);
            try {
                if (child == IntPtr.Zero || true.Equals(GetWindowText(child)
                        ?.EndsWith("Remote Desktop Connection")))
                    return null;
            }
            catch (Win32Exception) {
                return null;
            }
            return new WindowDragOperation(child) {
                OriginalActiveWindow = GetForegroundWindow(),
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

                Debug.WriteLine("settings written");
                //await Task.Delay(5000);
                //Debug.WriteLine("delayed message");
            }

            this.Shutdown();
        }

        protected override void OnExit(ExitEventArgs exitArgs)
        {
            base.OnExit(exitArgs);
            HockeyClient.Current.Flush();
            Thread.Sleep(1000);
        }

        async Task StartLayout(StackSettings stackSettings)
        {
            var layoutsDirectory = await this.roamingSettingsFolder.CreateFolderAsync("Layouts", CreationCollisionOption.OpenIfExists);
            if ((await layoutsDirectory.GetFilesAsync()).Count == 0)
                await this.InstallDefaultLayouts(layoutsDirectory);

            this.layoutsDirectory = new ObservableDirectory(layoutsDirectory.Path);

            var screens = this.screenProvider.Screens;
            FrameworkElement[] layouts = await Task.WhenAll(screens
                .Select(screen => GetLayoutForScreen(screen, stackSettings, layoutsDirectory))
                .ToArray());
            var screenLayouts = new List<ScreenLayout>();
            for (var screenIndex = 0; screenIndex < screens.Count; screenIndex++)
            {
                var screen = screens[screenIndex];
                var layout = new ScreenLayout{Opacity = 0};
                layout.Closed += (sender, args) => this.BeginShutdown();
                layout.QueryContinueDrag += (sender, args) => args.Action = DragAction.Cancel;
                // windows must be visible before calling AdjustToClientArea,
                // otherwise final position is unpredictable
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

            this.trayIcon = (await TrayIcon.StartTrayIcon(layoutsDirectory, this.layoutsDirectory, stackSettings, this.screenProvider)).Icon;
        }

        internal static readonly string OutOfBoxLayoutsResourcePrefix = typeof(App).Namespace + ".OOBLayouts.";

        async Task InstallDefaultLayouts(IFolder destination)
        {
            var resourceContainer = GetResourceContainer();
            foreach (var resource in resourceContainer.GetManifestResourceNames()
                                                      .Where(name => name.StartsWith(OutOfBoxLayoutsResourcePrefix))) {
                var name = resource.Substring(OutOfBoxLayoutsResourcePrefix.Length);
                using (var stream = resourceContainer.GetManifestResourceStream(resource)) {
                    var file = await destination.CreateFileAsync(name, CreationCollisionOption.FailIfExists).ConfigureAwait(false);
                    using (var targetStream = await file.OpenAsync(FileAccess.ReadAndWrite).ConfigureAwait(false)) {
                        await stream.CopyToAsync(targetStream).ConfigureAwait(false);
                        targetStream.Close();
                    }
                }
            }
        }

        internal static Assembly GetResourceContainer() => Assembly.GetExecutingAssembly();
        async Task<FrameworkElement> GetLayoutForScreen(Win32Screen screen, StackSettings settings, IFolder layoutsDirectory)
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

        public static DirectoryInfo RoamingAppData
        {
            get {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
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
            //switch ((WindowMessage)msg) {}
            return IntPtr.Zero;
        }

        public static Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        public int DragThreshold { get; private set; } = 40;
    }
}
