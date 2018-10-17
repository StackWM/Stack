namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Forms;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Threading;
    using DesktopNotifications;
    using global::Windows.UI.Notifications;
    using Gma.System.MouseKeyHook;
    using JetBrains.Annotations;
    using LostTech.App;
    using LostTech.Stack.Behavior;
    using LostTech.Stack.Compat;
    using LostTech.Stack.DataBinding;
    using LostTech.Stack.Models;
    using LostTech.Stack.Extensibility.Filters;
    using LostTech.Stack.Licensing;
    using LostTech.Stack.Settings;
    using LostTech.Stack.Utils;
    using LostTech.Stack.ViewModels;
    using LostTech.Stack.WindowManagement;
    using LostTech.Stack.Windows;
    using LostTech.Stack.Zones;
    using LostTech.Windows;
    using MahApps.Metro.Controls;
    using Microsoft.HockeyApp;
    using Microsoft.Toolkit.Uwp.Notifications;
    using Nito.AsyncEx;
    using PCLStorage;
    using PInvoke;
    using Application = System.Windows.Application;
    using Control = System.Windows.Controls.Control;
    using DragAction = System.Windows.DragAction;
    using FileAccess = PCLStorage.FileAccess;
    using KeyEventArgs = System.Windows.Forms.KeyEventArgs;
    using static System.FormattableString;
    using static PInvoke.User32;
    using Brushes = System.Windows.Media.Brushes;
    using Color = System.Windows.Media.Color;
    using MessageBox = System.Windows.MessageBox;
    using XmlDocument = global::Windows.Data.Xml.Dom.XmlDocument;
    using Rect = System.Drawing.RectangleF;
    using Point = System.Drawing.PointF;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, ILayoutsViewModel
    {
        IKeyboardMouseEvents hook;
        WindowDragOperation dragOperation;
        ICollection<ScreenLayout> screenLayouts;
        IEnumerable<ScreenLayout> ILayoutsViewModel.ScreenLayouts => this.screenLayouts;
        TrayIcon trayIcon;
        IFolder localSettingsFolder, roamingSettingsFolder;

        readonly Window stackInstanceWindow = new Window {
            Opacity = 0,
            AllowsTransparency = true,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Width=0,Height=0,
            ResizeMode = ResizeMode.NoResize,
            Title = nameof(stackInstanceWindow),
        };

        Lazy<SettingsWindow> settingsWindow;

        DragHook dragHook;
        StackSettings stackSettings;
        KeyboardArrowBehavior keyboardArrowBehavior;
        HotkeyBehavior hotkeyBehavior;
        MoveToZoneHotkeyBehavior moveToZoneBehavior;
        AutoCaptureBehavior autoCaptureBehavior;
        LayoutManager layoutManager;
        DispatcherTimer updateTimer;
        readonly IScreenProvider screenProvider = new Win32ScreenProvider();
        ObservableDirectory layoutsDirectory;
        IFolder layoutsFolder;
        LayoutLoader layoutLoader;
        LayoutMappingViewModel layoutMapping;
        int zoneIndex;
        readonly Win32WindowFactory win32WindowFactory = new Win32WindowFactory();

        public event EventHandler<EventArgs<ScreenLayout>> LayoutLoaded;

        internal static readonly bool IsUwp = new DesktopBridge.Helpers().IsRunningAsUwp();

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (e.Args.Contains("--jit-debugging"))
                EnableJitDebugging();

            await EnableHockeyApp();

            StopRunningInstances();

            this.MainWindow = this.stackInstanceWindow;
            this.stackInstanceWindow.Show();
            this.stackInstanceWindow.Hide();

            if (!IsUwp) {
#if !PROFILE
                this.BeginCheckForUpdates();
                this.updateTimer = new DispatcherTimer(DispatcherPriority.Background) {
                    Interval = TimeSpan.FromDays(1),
                    IsEnabled = true,
                };
                this.updateTimer.Tick += (_, __) => this.BeginCheckForUpdates();
#endif
            } else {
                DesktopNotificationManagerCompat.RegisterActivator<UrlNotificationActivator>();
            }


            if (await Expiration.HasExpired()) {
                this.Shutdown(2);
                return;
            }

            this.localSettingsFolder = await FileSystem.Current.GetFolderFromPathAsync(AppData.FullName);
            this.roamingSettingsFolder = await FileSystem.Current.GetFolderFromPathAsync(RoamingAppData.FullName);

            bool migrating = await SettingsMigration.Migrate(this.localSettingsFolder);

            this.localSettings = XmlSettings.Create(this.localSettingsFolder);
            var settings = new StackSettings {
                LayoutMap = await this.InitializeSettingsSet<ScreenLayouts>("LayoutMap.xml"),
                Behaviors = await this.InitializeSettingsSet<Behaviors>("Behaviors.xml"),
                Notifications = await this.InitializeSettingsSet<NotificationSettings>("Notifications.xml"),
                WindowGroups = await this.InitializeSettingsSet<CopyableObservableCollection<WindowGroup>>("WindowGroups.xml"),
            };
            this.stackSettings = settings;
            if (settings.WindowGroups.Count == 0
                && settings.Behaviors.MouseMove.WindowGroupIgnoreList.Count == 0
                && settings.Behaviors.KeyboardMove.WindowGroupIgnoreList.Count == 0) {
                const string remoteControlGroupName = "Remote Control Applications";
                settings.WindowGroups.Add(new WindowGroup {
                    Name = remoteControlGroupName,
                    Filters = {
                        new WindowFilter { TitleFilter = new CommonStringMatchFilter {
                            Value = "Remote Desktop Connection",
                            Match = CommonStringMatchFilter.MatchOption.Suffix,
                        }},
                    },
                });
                settings.Behaviors.MouseMove.WindowGroupIgnoreList.Add(remoteControlGroupName);
                settings.Behaviors.KeyboardMove.WindowGroupIgnoreList.Add(remoteControlGroupName);
            }
            settings.Behaviors.AddMissingBindings();
            this.win32WindowFactory.SuppressSystemMargin = settings.Behaviors.General.SuppressSystemMargin;
            settings.Behaviors.General.OnChange(s => s.SuppressSystemMargin,
                suppress => this.win32WindowFactory.SuppressSystemMargin = suppress);

            bool termsVersionMismatch = settings.Notifications.AcceptedTerms != LicenseTermsAcceptance.GetTermsAndConditionsVersion();
            if (termsVersionMismatch) {
                var termsWindow = new LicenseTermsAcceptance();
                if (!true.Equals(termsWindow.ShowDialog())) {
                    this.Shutdown();
                    return;
                }
                termsWindow.Close();
                settings.Notifications.AcceptedTerms = LicenseTermsAcceptance.GetTermsAndConditionsVersion();
            }

            string version = Invariant($"{Version.Major}.{Version.Minor}");
            if (settings.Notifications.WhatsNewVersionSeen != version) {
                this.ShowNotification(title: "What's New in Stack Widgets Update (v2.1)", 
                    message: "You have received a Stack update. See what's new",
                    navigateTo: new Uri("https://losttech.software/stack-whatsnew.html"));
            }
            settings.Notifications.WhatsNewVersionSeen = version;

            if (!this.stackInstanceWindow.IsLoaded) {
                if (termsVersionMismatch)
                    Restart();
                return;
            }

            this.settingsWindow = new Lazy<SettingsWindow>(() => new SettingsWindow{ DataContext = settings }, isThreadSafe: false);

            this.stackInstanceWindow.Closed += (sender, args) => this.BeginShutdown();

            EnableCustomWidgetLoading();

            await this.StartLayout(settings);

            this.trayIcon = await this.StartTrayIcon(settings);

            if (this.layoutLoader.Problems.Count > 0) {
                this.trayIcon.Icon.BalloonTipTitle = "Some layouts were not loaded";
                this.trayIcon.Icon.BalloonTipText = string.Join("\n", this.layoutLoader.Problems);
                this.trayIcon.Icon.BalloonTipIcon = ToolTipIcon.Error;
                this.trayIcon.Icon.ShowBalloonTip(30);
            }

            if (WindowsDesktop.VirtualDesktop.IsPresent && !WindowsDesktop.VirtualDesktop.HasMinimalSupport) {
                WindowsDesktop.VirtualDesktop.InitializationException.ReportAsWarning();
                this.NonCriticalErrorHandler(this, new ErrorEventArgs(new Exception(
                    message: "Your OS has Virtual Desktops, but this version of API is not supported. You might notice Stack behaving weird when using Virtual Desktops.",
                    innerException: WindowsDesktop.VirtualDesktop.InitializationException)));
            }

            // this must be the last, so that mouse won't lag while we are loading
            this.BindHandlers(settings);
        }

        async Task<T> InitializeSettingsSet<T>(string fileName)
            where T: class, new()
        {
            SettingsSet<T, T> settingsSet;
            try {
                settingsSet = await this.localSettings.LoadOrCreate<T>(fileName);
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
                settingsSet = await this.localSettings.LoadOrCreate<T>(fileName);
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
                currentWindow = FindWindowEx(IntPtr.Zero, currentWindow, null, nameof(stackInstanceWindow));
                if (currentWindow != IntPtr.Zero)
                    PostMessage(currentWindow, WindowMessage.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                else
                    break;
            }

            var selfID = Process.GetCurrentProcess().Id;
            foreach(var instance in Process.GetProcessesByName("Stack")) {
                if (instance.Id == selfID)
                    continue;

                if (!instance.WaitForExit(13000)) {
                    MessageBox.Show($"Failed to stop Stack instance with process ID {instance.Id}. Will exit.",
                        "Other instance is still running", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(-1);
                }
                else {
                    Debug.WriteLine($"Stopped [{instance.Id}] Stack.exe");
                }
            }
        }

        static readonly DateTimeOffset BootTime = DateTimeOffset.UtcNow;
        static TimeSpan Uptime => DateTimeOffset.UtcNow - BootTime;
#if DEBUG
        const int HeartbeatIntervalMinutes = 30;
#else
        const int HeartbeatIntervalMinutes = 60*3;
#endif
        static readonly DispatcherTimer HeartbeatTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(HeartbeatIntervalMinutes) };
        static async Task EnableHockeyApp()
        {
#if DEBUG
            HockeyClient.Current.Configure("be80a4a0381c4c37bc187d593ac460f9");
            ((HockeyClient)HockeyClient.Current).OnHockeySDKInternalException += (sender, args) =>
            {
                if (Debugger.IsAttached) { Debugger.Break(); }
            };
#else
            HockeyClient.Current.Configure("6037e69fa4944acc9d83ef7682e60732");
#endif
            try
            {
                await HockeyClient.Current.SendCrashesAsync().ConfigureAwait(false);
            }
            catch (IOException e) when ((e.HResult ^ unchecked((int)0x8007_0000)) == (int) Win32ErrorCode.ERROR_NO_MORE_FILES) {}

            HeartbeatTimer.Tick += TelemetryHeartbeat;
            HeartbeatTimer.Start();
            TelemetryHeartbeat(HeartbeatTimer, EventArgs.Empty);

            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
        }

        static void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e) {
            foreach (Exception exception in e.Exception.Flatten().InnerExceptions)
                HockeyClient.Current.TrackException(exception, properties: new Dictionary<string, string>{["unobserved"] = "true"});
        }

        static void TelemetryHeartbeat(object sender, EventArgs e) {
#if !PROFILE
            string domainName = Expiration.GetDomainName();
            HockeyClient.Current.TrackEvent("Heartbeat", new Dictionary<string, string> {
                [nameof(HeartbeatIntervalMinutes)] = Invariant($"{HeartbeatIntervalMinutes}"),
                [nameof(Expiration.IsDomainUser)] = Invariant($"{Expiration.IsDomainUser(domainName)}"),
                ["Domain"] = domainName ?? "",
                [nameof(Version)] = Invariant($"{Version}"),
                [nameof(Uptime)] = Invariant($"{Uptime}"),
                [nameof(IsUwp)] = Invariant($"{IsUwp}"),
            });
#endif
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
                this.dragOperation.Activated = true;
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


            var relativeDropPoint = screen.PointFromScreen(currentPosition.ToWPF());
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

        static readonly SolidColorBrush LayoutBackground = new SolidColorBrush(Color.FromArgb(
            a: 0x80, r: 0xFF, g: 0xFF, b: 0xFF));

        volatile bool disableDragHandler = false;
        void ShowLayoutGrid() {
            foreach (ScreenLayout screenLayout in this.screenLayouts.Active()) {
                if (screenLayout.ViewModel != null)
                    screenLayout.ViewModel.ShowHints = true;
                if (screenLayout.Layout == null || Layout.GetVersion(screenLayout.Layout) < Layout.Version.Min.PermanentlyVisible)
                    screenLayout.Show();
                screenLayout.Background = LayoutBackground;
                screenLayout.Opacity = 0.7;

                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => {
                    // for some reason Topmost below is unreliable
                    // so we have to manually bring the grid up. See Bug #287
                    screenLayout.TryGetNativeWindow()?.BringToFront();

                    screenLayout.Topmost = true;
                }));
            }
        }

        async Task StealFocus() {
            var first = this.screenLayouts.Active().FirstOrDefault();
            if (first == null)
                return;

            await Task.Yield();

            var layoutCenter = first.GetPhysicalBounds().Center();
            this.disableDragHandler = true;
            Debug.WriteLine("forcing focus; drag handler disabled");

            SendMouseInput(MOUSEEVENTF.MOUSEEVENTF_MIDDLEDOWN, (int)layoutCenter.X, (int)layoutCenter.Y);
            SendMouseInput(MOUSEEVENTF.MOUSEEVENTF_MIDDLEUP, (int)layoutCenter.X, (int)layoutCenter.Y);

            await Task.Yield();

            this.disableDragHandler = false;
            Debug.WriteLine("reenabled drag handler");
        }

        static void SendMouseInput(User32.MOUSEEVENTF eventType, int x, int y) {
            User32.SendInput(1, new[] {
                new User32.INPUT {
                    type = User32.InputType.INPUT_MOUSE,
                    Inputs = new User32.INPUT.InputUnion {
                        mi = new User32.MOUSEINPUT {
                            dwFlags = eventType | User32.MOUSEEVENTF.MOUSEEVENTF_ABSOLUTE,
                            dx = x,
                            dy = y,
                        }
                    }
                }
            }, Marshal.SizeOf<User32.INPUT>());
        }

        void HideLayoutGrid() {
            foreach (var screenLayout in this.screenLayouts) {
                if (screenLayout.Layout == null || Layout.GetVersion(screenLayout.Layout) < Layout.Version.Min.PermanentlyVisible)
                    screenLayout.Hide();
                screenLayout.Topmost = false;
                if (screenLayout.ViewModel != null)
                    screenLayout.ViewModel.ShowHints = false;
                screenLayout.Background = Brushes.Transparent;
                screenLayout.Opacity = 1;
                //screenLayout.TryDisableGlassEffect();
                screenLayout.TryGetNativeWindow()?.SendToBottom();
            }
        }

        async void OnDragEnd(object sender, DragHookEventArgs @event) {
            if (this.disableDragHandler)
                return;
            if (this.dragOperation == null)
                return;

            var location = GetCursorPos();
            var dropPoint = location;
            var window = this.dragOperation.Window;
            await this.StopDrag(window);

            var screen = this.screenLayouts.Active()
                .FirstOrDefault(layout => layout.GetPhysicalBounds().Contains(dropPoint));
            if (screen == null) {
                Debug.WriteLine("can't drop: no screen at the target point");
                return;
            }
            var relativeDropPoint = screen.PointFromScreen(dropPoint.ToWPF());
            var zone = screen.GetZone(relativeDropPoint)?.GetFinalTarget();
            if (zone == null) {
                Debug.WriteLine("can't drop: no zone at the target point");
                return;
            }

            this.Move(window, zone);
        }

        async void Move(IntPtr windowHandle, Zone zone)
        {
            var window = this.win32WindowFactory.Create(windowHandle);
            Exception problem = await window.Activate();
            if (problem is WindowNotFoundException)
                return;
            if (problem != null)
                this.NonCriticalErrorHandler(this, new ErrorEventArgs(problem));
            this.layoutManager.Move(window, zone);
        }

        void ShowNotification(string title, string message, Uri navigateTo, TimeSpan? duration = null) {
            var content = new ToastContent {
                Launch = navigateTo.ToString(),

                Header = title == null ? null : new ToastHeader(title, title, navigateTo.ToString()),

                Visual = new ToastVisual {
                    BindingGeneric = new ToastBindingGeneric {
                        Children = { new AdaptiveText{Text = message} },
                    }
                }
            };

            var contentXml = new XmlDocument();
            contentXml.LoadXml(content.GetContent());
            var toast = new ToastNotification(contentXml) {
                // DTO + null == null
                ExpirationTime = DateTimeOffset.Now + duration,
            };
            try {
                DesktopNotificationManagerCompat.CreateToastNotifier().Show(toast);
            } catch (Exception e) {
                string retrying = this.trayIcon != null ? "retrying" : "no retry";
                e.ReportAsWarning(prefix: $"Notification failed, {retrying}: ");

                if (this.trayIcon == null)
                    return;

                this.trayIcon.Icon.BalloonTipIcon = ToolTipIcon.None;
                this.trayIcon.Icon.BalloonTipTitle = title;
                this.trayIcon.Icon.BalloonTipText = message;
                this.trayIcon.Icon.ShowBalloonTip(1000);
            }
        }

        void NonCriticalErrorHandler(object sender, ErrorEventArgs error) {
            #if !DEBUG
            if (error.GetException() is WindowNotFoundException)
                return;
            #endif

            HockeyClient.Current.TrackException(error.GetException(), properties: new Dictionary<string, string> {
                ["warning"] = "true",
                ["user-visible"] = "true",
            });

            this.trayIcon.Icon.BalloonTipIcon = ToolTipIcon.Error;
            this.trayIcon.Icon.BalloonTipTitle = "Stack";
            this.trayIcon.Icon.BalloonTipText = error.GetException().Message;
            this.trayIcon.Icon.ShowBalloonTip(1000);
        }

        static Point GetCursorPos()
        {
            if (!User32.GetCursorPos(out var cursorPos))
                throw new System.ComponentModel.Win32Exception();
            return new Point(cursorPos.x, cursorPos.y);
        }

        async void GlobalKeyDown(object sender, KeyEventArgs @event)
        {
            if (@event.KeyData == Keys.Escape && this.dragOperation != null) {
                @event.Handled = true;
                var windowToReactivate = this.dragOperation.OriginalActiveWindow;
                await this.StopDrag(this.dragOperation.Window);
                Debug.Assert(this.dragOperation == null);
                SetForegroundWindow(windowToReactivate);
                return;
            }
        }

        async Task StopDrag(IntPtr window)
        {
            if (this.dragOperation.CurrentZone != null) {
                this.dragOperation.CurrentZone.IsDragMouseOver = false;
            }

            // this allows Stack to bring dragged window to front
            await this.StealFocus();

            this.HideLayoutGrid();

            this.dragOperation = null;
        }

        void OnDragStart(object sender, DragHookEventArgs @event)
        {
            if (!this.stackSettings.Behaviors.MouseMove.Enabled
             || this.dragOperation != null || this.disableDragHandler)
                return;
            this.dragOperation = this.DragStart();
            @event.Handled = this.dragOperation != null;
        }

        void OnDragStartPreview(object sender, DragHookEventArgs args) {
            if (!this.stackSettings.Behaviors.MouseMove.Enabled 
             || this.dragOperation != null || this.disableDragHandler)
                return;

            args.Handled = this.DragStart() != null;
        }

        WindowDragOperation DragStart()
        {
            User32.GetCursorPos(out var point);
            var child = WindowFromPoint(point);
            child = GetAncestor(child, GetAncestorFlags.GA_ROOT);
            if (child == IntPtr.Zero)
                return null;
            var win32Window = this.win32WindowFactory.Create(child);
            if (win32Window.Equals(this.win32WindowFactory.Desktop)
                || win32Window.Equals(this.win32WindowFactory.Shell))
                return null;
            if (this.stackSettings.Behaviors.MouseMove.TitleOnly) {
                var clientArea = win32Window.GetClientBounds().Result;
                var bounds = win32Window.Bounds;
                if (Math.Abs(bounds.Height - clientArea.Height) < 3) {
                    double? screenScale = this.screenProvider.Screens
                        .FirstOrDefault(s => s.IsActive && s.WorkingArea.Contains(point.x, point.y))
                        ?.TransformToDevice.M11;
                    clientArea.Y += 32 * (float)(screenScale ?? 1.25);
                }

                if (clientArea.Contains(point.x, point.y))
                    return null;
            }

            try {
                if (this.stackSettings.Behaviors.MouseMove.WindowGroupIgnoreList.Contains(
                        this.stackSettings.WindowGroups, child))
                    return null;

                if (this.screenLayouts.Any(layout => new WindowInteropHelper(layout).Handle == child)) {
                    Debug.WriteLine("don't drag self");
                    return null;
                }
            }
            catch (Win32Exception) {
                return null;
            }
            Debug.WriteLine($"started dragging {GetWindowText(child)}");
            return new WindowDragOperation(child) {
                OriginalActiveWindow = GetForegroundWindow(),
            };
        }

        async Task DisposeAsync()
        {
            this.layoutManager?.Dispose();
            this.layoutManager = null;
            this.hook?.Dispose();
            this.dragHook?.Dispose();
            this.dragHook = null;
            this.keyboardArrowBehavior?.Dispose();
            this.hotkeyBehavior?.Dispose();
            this.moveToZoneBehavior?.Dispose();
            this.autoCaptureBehavior?.Dispose();
            this.trayIcon?.Dispose();

            WindowHookExFactory.Instance.Shutdown();

            LostTech.App.Settings settings = this.localSettings;
            if (settings != null)
            {
                settings.ScheduleSave();
                await settings.DisposeAsync();
                this.localSettings = null;
                Debug.WriteLine("settings written");
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
            this.layoutsFolder = await this.roamingSettingsFolder.CreateFolderAsync("Layouts", CreationCollisionOption.OpenIfExists);
            await this.InstallDefaultLayouts(this.layoutsFolder);
            this.layoutLoader = new LayoutLoader(this.layoutsFolder);

            this.layoutsDirectory = new ObservableDirectory(this.layoutsFolder.Path);

            var screens = this.screenProvider.Screens;
            var layoutNameCollection = new TransformObservableCollection<string, ObservableFile,
                ReadOnlyObservableCollection<ObservableFile>>(
                this.layoutsDirectory.Files,
                file => Path.GetFileNameWithoutExtension(file.FullName));
            this.layoutMapping = new LayoutMappingViewModel(settings.LayoutMap,
                layoutNameCollection, this.layoutLoader, this.screenProvider);

            this.screenLayouts = new ObservableCollection<ScreenLayout>();

            async Task AddLayoutForScreen(Win32Screen screen)
            {
                var layout = new ScreenLayout {
                    ViewModel = new ScreenLayoutViewModel{Screen = screen},
                    Title = $"{screen.ID}: {ScreenLayouts.GetDesignation(screen)}"
                };
                layout.Closed += this.OnLayoutClosed;
                layout.QueryContinueDrag += (sender, args) => args.Action = DragAction.Cancel;
                layout.SizeChanged += LayoutBoundsChanged;
                layout.LocationChanged += LayoutBoundsChanged;

                this.screenLayouts.Add(layout);
            }

            var layoutBounds = new Dictionary<ScreenLayout, Rect>();
            void RemoveLayoutForScreen(Win32Screen screen) {
                ScreenLayout layout = this.screenLayouts.FirstOrDefault(l => l.Screen?.ID == screen.ID);
                if (layout != null) {
                    foreach (Zone zone in layout.Zones)
                        zone.ProblemOccurred -= this.NonCriticalErrorHandler;
                    layout.Closed -= this.OnLayoutClosed;
                    try {
                        layout.Close();
                    } catch (InvalidOperationException) { }
                    layoutBounds.Remove(layout);
                    this.screenLayouts.Remove(layout);
                }
            }

            var changeGroupTasks = new Dictionary<ScreenLayout, Task>();
            async void LayoutBoundsChanged(object sender, EventArgs e) {
                var layout = (ScreenLayout)sender;
                Task delay = Task.Delay(millisecondsDelay: 15);
                changeGroupTasks[layout] = delay;
                await delay;
                if (changeGroupTasks.TryGetValue(layout, out var changeGroupTask) && delay.Equals(changeGroupTask)
                    && layout.IsLoaded) {
                    changeGroupTasks.Remove(layout);
                    Rect? newBounds = layout.TryGetPhysicalBounds();
                    if (newBounds == null)
                        return;
                    if (layoutBounds.TryGetValue(layout, out var bounds) && newBounds.Value.Equals(bounds))
                        return;
                    layoutBounds[layout] = newBounds.Value;
                    await this.ReloadLayout(layout);
                    layout.TryGetNativeWindow()?.SendToBottom().ReportAsWarning();
                } else
                    Debug.WriteLine("grouped updates!");
            }

            async void ScreenPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
                var screen = (Win32Screen)sender;
                switch (e.PropertyName) {
                case nameof(Win32Screen.WorkingArea):
                case nameof(Win32Screen.IsActive):
                    if (!ScreenExtensions.IsValidScreen(screen))
                        RemoveLayoutForScreen(screen);
                    else {
                        var layout = this.screenLayouts.FirstOrDefault(l => l.Screen.ID == screen.ID);
                        if (layout == null)
                            await AddLayoutForScreen(screen);
                    }
                    return;
                }
            }

            foreach (Win32Screen screen in screens) {
                screen.PropertyChanged += ScreenPropertyChanged;
                if (ScreenExtensions.IsValidScreen(screen)) {
                    await AddLayoutForScreen(screen);

                    if (settings.LayoutMap.NeedsUpdate(screen))
                        this.layoutMapping.ShowLayoutSelector(screen);
                }
            }

            screens.OnChange<Win32Screen>(onAdd: async s => {
                s.PropertyChanged += ScreenPropertyChanged;
                if (ScreenExtensions.IsValidScreen(s))
                    await AddLayoutForScreen(s);
            }, onRemove: s => {
                s.PropertyChanged -= ScreenPropertyChanged;
                RemoveLayoutForScreen(s);
            });

            settings.LayoutMap.Map.CollectionChanged += this.MapOnCollectionChanged;
        }

        async Task<TrayIcon> StartTrayIcon(StackSettings settings) {
            var trayIcon = await TrayIcon.StartTrayIcon(this.layoutsFolder, this.layoutsDirectory, settings, this.screenProvider, this.settingsWindow);
            trayIcon.Icon.BalloonTipClicked += (sender, args) => MessageBox.Show(this.trayIcon.Icon.BalloonTipText,
                trayIcon.Icon.BalloonTipTitle,
                MessageBoxButton.OK,
                trayIcon.Icon.BalloonTipIcon == ToolTipIcon.Error
                    ? MessageBoxImage.Error
                    : MessageBoxImage.Information);
            
            if (!settings.Notifications.IamInTrayDone) {
                settings.Notifications.IamInTrayDone = true;
                trayIcon.Icon.BalloonTipTitle = "Stack";
                trayIcon.Icon.BalloonTipText = "You can now move windows around using middle mouse button or Win+Arrow";
                trayIcon.Icon.BalloonTipIcon = ToolTipIcon.Info;
                trayIcon.Icon.ShowBalloonTip(30);
            }

            return trayIcon;
        }

        async void MapOnCollectionChanged(object o, NotifyCollectionChangedEventArgs change) {
            switch (change.Action) {
            case NotifyCollectionChangedAction.Add:
            case NotifyCollectionChangedAction.Replace when change.OldItems.Count == 1:
                var newRecord = change.NewItems.OfType<MutableKeyValuePair<string, string>>().Single();
                var layoutToUpdate = this.screenLayouts.FirstOrDefault(
                    layout => layout.Screen?.ID == newRecord.Key
                    || layout.Screen != null && ScreenLayouts.GetDesignation(layout.Screen) == newRecord.Key);
                if (layoutToUpdate != null)
                    await this.ReloadLayout(layoutToUpdate);
                break;
            default:
                return;
            }
        }
        
        public async Task ReloadLayout([NotNull] ScreenLayout screenLayout) {
            if (screenLayout == null)
                throw new ArgumentNullException(nameof(screenLayout));

            this.layoutLoader.ProblemOccurred += this.NonCriticalErrorHandler;
            try {
                FrameworkElement element = await this.GetLayoutForScreen(screenLayout.Screen);
                var readiness = new TaskCompletionSource<bool>();
                Layout.SetReady(element, readiness.Task);
                element.Loaded += delegate { this.LayoutLoaded?.Invoke(this, Args.Create(screenLayout)); };
                screenLayout.SetLayout(element).ContinueWith(readiness.TryCompleteFromCompletedTask).ReportAsWarning();

                foreach (var troublemaker in screenLayout.FindChildren<Control>().OfType<IObjectWithProblems>())
                    troublemaker.ProblemOccurred += this.NonCriticalErrorHandler;

                foreach (Zone zone in screenLayout.Zones)
                    zone.Id = zone.Id ?? $"{this.zoneIndex++}";

                int version = Layout.GetVersion(element);
                if (screenLayout.IsHandleInitialized)
                    if (version < Layout.Version.Min.PermanentlyVisible)
                        screenLayout.TryHide();
                    else
                        screenLayout.TryShow();

                if (version < Layout.Version.Current)
                {
                    // TODO: remember warning state per layout
                    this.ShowNotification(title: $"Outdated layout {Layout.GetSource(element)}",
                        message: $"Layout {Layout.GetSource(element)} is outdated. Upgrade it to v2.",
                        navigateTo: new Uri("https://www.allanswered.com/post/kgnoz/how-do-i-upgrade-my-layouts-to-v2/"));
                }
            } finally {
                this.layoutLoader.ProblemOccurred -= this.NonCriticalErrorHandler;
            }
        }

        void OnLayoutClosed(object sender, EventArgs args) { this.BeginShutdown(); }

        internal static readonly string OutOfBoxLayoutsResourcePrefix = typeof(App).Namespace + ".OOBLayouts.";
        LostTech.App.Settings localSettings;

        async Task InstallDefaultLayouts(IFolder destination) {
            IList<IFile> layoutFiles = await this.layoutsFolder.GetFilesAsync().ConfigureAwait(false);

            DateTime appTimestamp = File.GetLastWriteTimeUtc(Assembly.GetExecutingAssembly().Location);

            var resourceContainer = GetResourceContainer();
            foreach (var resource in resourceContainer.GetManifestResourceNames()
                                                      .Where(name => name.StartsWith(OutOfBoxLayoutsResourcePrefix))) {
                var name = resource.Substring(OutOfBoxLayoutsResourcePrefix.Length);
                IFile existing = layoutFiles.FirstOrDefault(file => Path.GetFullPath(file.Name) == Path.GetFullPath(name));
                if (existing != null && File.GetLastWriteTimeUtc(existing.Path) > appTimestamp)
                    continue;

                using (var stream = resourceContainer.GetManifestResourceStream(resource)) {
                    IFile file = await destination.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting).ConfigureAwait(false);
                    using (var targetStream = await file.OpenAsync(FileAccess.ReadAndWrite).ConfigureAwait(false)) {
                        await stream.CopyToAsync(targetStream).ConfigureAwait(false);
                        targetStream.Close();
                    }
                }
            }
        }

        internal static Assembly GetResourceContainer() => Assembly.GetExecutingAssembly();
        async Task<FrameworkElement> GetLayoutForScreen(Win32Screen screen)
        {
            string layoutFileName = this.layoutMapping.GetPreferredLayoutFileName(screen);
            FrameworkElement layout = await this.layoutLoader.LoadLayoutOrDefault(layoutFileName);
            Layout.SetSource(layout, layoutFileName);
            return layout;
        }

        private void BindHandlers(StackSettings settings)
        {
            this.hook = Hook.GlobalEvents();

            this.layoutManager = new LayoutManager(this.screenLayouts, this.win32WindowFactory);

            this.keyboardArrowBehavior = new KeyboardArrowBehavior(
                this.hook, this.screenLayouts, this.layoutManager, settings.Behaviors.KeyBindings,
                settings.Behaviors.KeyboardMove,
                settings.WindowGroups,
                this.win32WindowFactory);

            this.hotkeyBehavior = new HotkeyBehavior(this.hook, settings.Behaviors.KeyBindings,
                this, this.screenProvider, this.win32WindowFactory,
                this.layoutManager, this.layoutMapping);
            this.moveToZoneBehavior = new MoveToZoneHotkeyBehavior(this.hook, this.layoutManager, this.win32WindowFactory);

            this.autoCaptureBehavior = new AutoCaptureBehavior(
                layoutManager: this.layoutManager,
                settings: settings.Behaviors.General,
                layouts: this,
                keyBindings: settings.Behaviors.KeyBindings,
                keyboardHook: this.hook,
                win32WindowFactory: this.win32WindowFactory);

            this.dragHook = new DragHook(settings.Behaviors.MouseMove.DragButton, this.hook);
            settings.Behaviors.MouseMove.OnChange(s => s.DragButton, newButton => this.dragHook.SetButton(newButton));
            this.dragHook.DragStartPreview += this.OnDragStartPreview;
            this.dragHook.DragStart += this.OnDragStart;
            this.dragHook.DragEnd += this.OnDragEnd;
            this.dragHook.DragMove += this.OnDragMove;
            this.hook.KeyDown += this.GlobalKeyDown;
        }

        public static DirectoryInfo AppData {
            get {
                string path;
                if (IsUwp) {
                    path = GetUwpAppData();
                } else {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    path = Path.Combine(appData, "Lost Tech LLC", nameof(Stack));
#if PROFILE
                    path = Path.Combine(path, "PROFILING");
#endif
                }
                return Directory.CreateDirectory(path);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string GetUwpAppData() => global::Windows.Storage.ApplicationData.Current.LocalFolder.Path;

        public static DirectoryInfo RoamingAppData
        {
            get {
                string path;
                if (IsUwp) {
                    path = GetUwpRoamingAppData();
                } else {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    path = Path.Combine(appData, "Lost Tech LLC", nameof(Stack));
#if PROFILE
                    path = Path.Combine(path, "PROFILING");
#endif
                }
                return Directory.CreateDirectory(path);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string GetUwpRoamingAppData() => global::Windows.Storage.ApplicationData.Current.RoamingFolder.Path;

        public static Version Version => IsUwp
            ? GetUwpVersion()
            : Assembly.GetExecutingAssembly().GetName().Version;
        [MethodImpl(MethodImplOptions.NoInlining)]
        static Version GetUwpVersion() => global::Windows.ApplicationModel.Package.Current.Id.Version.ToVersion();

        public int DragThreshold { get; private set; } = 40;
        public static void Restart() { Process.Start(Process.GetCurrentProcess().MainModule.FileName); }

        public static void RestartAsAdmin()
        {
            var startInfo = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName) { Verb = "runas" };
            Process.Start(startInfo);
        }

        static void EnableCustomWidgetLoading() {
            AppDomain.CurrentDomain.AssemblyResolve += CustomWidgetsLoader;
        }

        static Assembly CustomWidgetsLoader(object sender, ResolveEventArgs args) {
            string fileName = $"{args.Name}.dll";
            if (Path.GetFileName(fileName) != fileName)
                return null;

            fileName = Path.Combine(RoamingAppData.FullName, "CustomWidgets", fileName);
            if (!File.Exists(fileName))
                return null;

            return Assembly.LoadFrom(fileName);
        }
    }
}
