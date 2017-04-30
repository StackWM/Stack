namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
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
    using LostTech.Stack.DataBinding;
    using LostTech.Stack.InternalExtensions;
    using LostTech.Stack.Settings;
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

        readonly Window winApiHandler = new Window {
            Opacity = 0,
            AllowsTransparency = true,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Width=0,Height=0,
            Title = nameof(winApiHandler),
        };

        SettingsWindow SettingsWindow { get; set; }

        DragHook dragHook;
        KeyboardArrowBehavior keyboardArrowBehavior;
        DispatcherTimer updateTimer;
        readonly IScreenProvider screenProvider = new Win32ScreenProvider();
        ObservableDirectory layoutsDirectory;
        readonly StringBuilder layoutLoadProblems = new StringBuilder();

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

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
            this.localSettings = XmlSettings.Create(this.localSettingsFolder);
            var settings = new StackSettings {
                LayoutMap = await this.InitializeSettingsSet<ScreenLayouts>("LayoutMap.xml"),
                Behaviors = await this.InitializeSettingsSet<Behaviors>("Behaviors.xml"),
                Notifications = await this.InitializeSettingsSet<NotificationSettings>("Notifications.xml"),
            };
            settings.Behaviors.AddMissingBindings();

            if (settings.Notifications.AcceptedTerms != LicenseTermsAcceptance.GetTermsAndConditionsVersion()) {
                var termsWindow = new LicenseTermsAcceptance();
                if (!true.Equals(termsWindow.ShowDialog())) {
                    this.Shutdown();
                    return;
                }
                termsWindow.Close();
                settings.Notifications.AcceptedTerms = LicenseTermsAcceptance.GetTermsAndConditionsVersion();
            }

            if (!this.winApiHandler.IsLoaded)
                return;

            this.SettingsWindow = new SettingsWindow{ DataContext = settings };

            this.SetupScreenHooks();

            this.winApiHandler.Closed += (sender, args) => this.BeginShutdown();

            await this.StartLayout(settings);

            // this must be the last, so that mouse won't lag while we are loading
            this.BindHandlers(settings);
        }

        async Task<T> InitializeSettingsSet<T>(string fileName)
            where T: class, new()
        {
            SettingsSet<T, T> settingsSet;
            try {
                settingsSet = await this.localSettings.LoadOrCreate<T, T>(fileName);
            }
            catch (Exception settingsError) {
                var errorFile = await this.localSettingsFolder.CreateFileAsync(
                    $"{fileName}.err", CreationCollisionOption.ReplaceExisting);
                Debug.WriteLine(settingsError.ToString());
                await errorFile.WriteAllTextAsync(settingsError.ToString());
                var brokenFile = await this.localSettingsFolder.GetFileAsync(fileName);
                await brokenFile.MoveAsync(
                    Path.Combine(this.localSettingsFolder.Path, $"Err.{fileName}"),
                    NameCollisionOption.ReplaceExisting);
                settingsSet = await this.localSettings.LoadOrCreate<T, T>(fileName);
                settingsSet.ScheduleSave();
            }
            settingsSet.Autosave = true;
            return settingsSet.Value;
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

            var screen = this.screenLayouts.Active()
                .FirstOrDefault(layout => layout.GetPhysicalBounds().Contains(currentPosition));
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
            foreach (ScreenLayout screenLayout in this.screenLayouts.Active()) {
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

            var screen = this.screenLayouts.Active()
                .FirstOrDefault(layout => layout.GetPhysicalBounds().Contains(dropPoint));
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
            try {
                if (GetWindowPlacement(window).showCmd.HasFlag(WindowShowStyle.SW_MAXIMIZE)) {
                    ShowWindow(window, WindowShowStyle.SW_RESTORE);
                }
            }
            catch (PInvoke.Win32Exception) { }

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
                this.StopDrag(this.dragOperation.Window);
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

        async Task DisposeAsync()
        {
            this.hook.Dispose();
            this.dragHook?.Dispose();
            this.dragHook = null;
            this.keyboardArrowBehavior?.Dispose();
            this.trayIcon?.Dispose();

            if (this.localSettings != null)
            {
                this.localSettings.ScheduleSave();
                await this.localSettings.DisposeAsync();
                this.localSettings = null;

                Debug.WriteLine("settings written");
                //await Task.Delay(5000);
                //Debug.WriteLine("delayed message");
            }
        }

        public async void BeginShutdown()
        {
            Debug.WriteLine("shutdown requested");

            await this.DisposeAsync();

            this.Shutdown();
        }

        protected override void OnExit(ExitEventArgs exitArgs)
        {
            base.OnExit(exitArgs);
            HockeyClient.Current.Flush();
            Thread.Sleep(1000);
        }

        async Task StartLayout(StackSettings settings)
        {
            var layoutsDirectory = await this.roamingSettingsFolder.CreateFolderAsync("Layouts", CreationCollisionOption.OpenIfExists);
            if ((await layoutsDirectory.GetFilesAsync()).Count == 0)
                await this.InstallDefaultLayouts(layoutsDirectory);

            this.layoutsDirectory = new ObservableDirectory(layoutsDirectory.Path);

            var screens = this.screenProvider.Screens;
            FrameworkElement[] layouts = await Task.WhenAll(screens
                .Select(screen => this.GetLayoutForScreen(screen, settings, layoutsDirectory))
                .ToArray());
            this.screenLayouts = new ObservableCollection<ScreenLayout>();

            async Task AddLayoutForScreen(Win32Screen screen)
            {
                var layout = new ScreenLayout { Opacity = 0 };
                layout.Closed += this.OnLayoutClosed;
                layout.QueryContinueDrag += (sender, args) => args.Action = DragAction.Cancel;
                // windows must be visible before calling AdjustToClientArea,
                // otherwise final position is unpredictable
                layout.Show();
                layout.AdjustToClientArea(screen);
                layout.Content = await this.GetLayoutForScreen(screen, settings, layoutsDirectory);
                layout.Title = $"{screen.ID}:{layout.Left}x{layout.Top}";
                layout.DataContext = screen;
                layout.Hide();
                layout.Opacity = 0.7;
                this.screenLayouts.Add(layout);
            }

            void RemoveLayoutForScreen(Win32Screen screen)
            {
                ScreenLayout layout = this.screenLayouts.FirstOrDefault(l => l.Screen == screen);
                if (layout != null) {
                    layout.Closed -= this.OnLayoutClosed;
                    layout.Close();
                    this.screenLayouts.Remove(layout);
                }
            }

            foreach (Win32Screen screen in screens)
                await AddLayoutForScreen(screen);

            screens.OnChange<Win32Screen>(onAdd: s => AddLayoutForScreen(s), onRemove: RemoveLayoutForScreen);

            this.trayIcon = (await TrayIcon.StartTrayIcon(layoutsDirectory, this.layoutsDirectory, settings, this.screenProvider, this.SettingsWindow)).Icon;
            if (this.layoutLoadProblems.Length > 0) {
                this.trayIcon.BalloonTipTitle = "Some layouts were not loaded";
                this.trayIcon.BalloonTipText = this.layoutLoadProblems.ToString();
                this.trayIcon.BalloonTipIcon = ToolTipIcon.Error;
                this.trayIcon.ShowBalloonTip(30);
            }
            if (!settings.Notifications.IamInTrayDone) {
                settings.Notifications.IamInTrayDone = true;
                this.trayIcon.BalloonTipTitle = "Stack";
                this.trayIcon.BalloonTipText = "Find me in the system tray!";
                this.trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                this.trayIcon.ShowBalloonTip(30);
            }
        }

        void OnLayoutClosed(object sender, EventArgs args) { this.BeginShutdown(); }

        internal static readonly string OutOfBoxLayoutsResourcePrefix = typeof(App).Namespace + ".OOBLayouts.";
        LostTech.App.Settings localSettings;

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
            return await this.LoadLayoutOrDefault(layoutsDirectory, layout);
        }

        private void BindHandlers(StackSettings settings)
        {
            this.hook = Hook.GlobalEvents();
            if (settings.Behaviors.KeyboardMove.Enabled)
                this.keyboardArrowBehavior = new KeyboardArrowBehavior(
                    this.hook, this.screenLayouts, settings.Behaviors.KeyBindings, this.Move);

            if (settings.Behaviors.MouseMove.Enabled) {
                this.dragHook = new DragHook(MouseButtons.Middle, this.hook);
                this.dragHook.DragStartPreview += this.OnDragStartPreview;
                this.dragHook.DragStart += this.OnDragStart;
                this.dragHook.DragEnd += this.OnDragEnd;
                this.dragHook.DragMove += this.OnDragMove;
                this.hook.KeyDown += this.GlobalKeyDown;
            }
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
                try {
                    var layout = (FrameworkElement) XamlReader.Load(xmlReader);
                    Debug.WriteLine($"loaded layout {fileName}");
                    return layout;
                }
                catch (XamlParseException e) {
                    this.layoutLoadProblems.AppendLine($"{file.Name}: {e.Message}");
                    return this.MakeDefaultLayout();
                }
            }
        }

        FrameworkElement MakeDefaultLayout() => new Grid {
            Children = {
                new Zone {},
            }
        };

        public static DirectoryInfo AppData {
            get {
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
            var hwnd = (HwndSource) PresentationSource.FromVisual(this.winApiHandler);
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
        public static void Restart() { Process.Start(Process.GetCurrentProcess().MainModule.FileName); }
    }
}
