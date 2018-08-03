﻿namespace LostTech.Stack
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
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Forms;
    using System.Windows.Interop;
    using System.Windows.Threading;
    using Gma.System.MouseKeyHook;
    using LostTech.App;
    using LostTech.Stack.Behavior;
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
    using Microsoft.HockeyApp;
    using PCLStorage;
    using PInvoke;
    using Application = System.Windows.Application;
    using DragAction = System.Windows.DragAction;
    using FileAccess = PCLStorage.FileAccess;
    using KeyEventArgs = System.Windows.Forms.KeyEventArgs;
    using MessageBox = System.Windows.MessageBox;
    using Point = System.Drawing.PointF;
    using static System.FormattableString;
    using static PInvoke.User32;

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
        StackSettings stackSettings;
        KeyboardArrowBehavior keyboardArrowBehavior;
        LayoutManager layoutManager;
        DispatcherTimer updateTimer;
        readonly IScreenProvider screenProvider = new Win32ScreenProvider();
        ObservableDirectory layoutsDirectory;
        IFolder layoutsFolder;
        LayoutLoader layoutLoader;

        static readonly bool IsUwp = new DesktopBridge.Helpers().IsRunningAsUwp();

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (e.Args.Contains("--jit-debugging"))
                EnableJitDebugging();

            await EnableHockeyApp();

            this.InitializeNotifications();

            StopRunningInstances();

            this.MainWindow = this.winApiHandler;
            this.winApiHandler.Show();

            if (!IsUwp) {
                this.BeginCheckForUpdates();
                this.updateTimer = new DispatcherTimer(DispatcherPriority.Background) {
                    Interval = TimeSpan.FromDays(1),
                    IsEnabled = true,
                };
                this.updateTimer.Tick += (_, __) => this.BeginCheckForUpdates();
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
                this.ShowNotification(title: "What's New in Stack v" + version, 
                    message: "You have received a Stack update. See what's new",
                    navigateTo: new Uri("https://losttech.software/stack-whatsnew-free.html"));
            }
            settings.Notifications.WhatsNewVersionSeen = version;

            if (!this.winApiHandler.IsLoaded) {
                if (termsVersionMismatch)
                    Restart();
                return;
            }

            this.SettingsWindow = new SettingsWindow{ DataContext = settings };

            this.winApiHandler.Closed += (sender, args) => this.BeginShutdown();

            await this.StartLayout(settings);

            await TrayIcon.InitializeMenu(this.trayIcon, this.layoutsFolder, this.layoutsDirectory, settings, this.screenProvider, this.SettingsWindow);
            if (this.layoutLoader.Problems.Length > 0) {
                this.trayIcon.BalloonTipTitle = "Some layouts were not loaded";
                this.trayIcon.BalloonTipText = this.layoutLoader.Problems;
                this.trayIcon.BalloonTipIcon = ToolTipIcon.Error;
                this.trayIcon.ShowBalloonTip(30);
            }
            if (!settings.Notifications.IamInTrayDone) {
                settings.Notifications.IamInTrayDone = true;
                this.trayIcon.BalloonTipTitle = "Stack";
                this.trayIcon.BalloonTipText = "You can now move windows around using middle mouse button or Win+Arrow";
                this.trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                this.trayIcon.ShowBalloonTip(30);
            }

            this.SuggestUpgrade();

            // this must be the last, so that mouse won't lag while we are loading
            this.BindHandlers(settings);
        }

        void InitializeNotifications() {
            this.trayIcon = TrayIcon.CreateTrayIcon();
            this.trayIcon.BalloonTipClicked += delegate {
                if (!(this.trayIcon.Tag is Uri uri))
                    return;

                if (uri.Scheme == null)
                    return;

                if (uri.Scheme.ToLowerInvariant().Any(c => c < 'a' || c > 'z'))
                    return;

                Process.Start(uri.ToString());
            };
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

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(HeartbeatIntervalMinutes) };
            timer.Tick += TelemetryHeartbeat;
            timer.Start();
            TelemetryHeartbeat(timer, EventArgs.Empty);
        }

        static void TelemetryHeartbeat(object sender, EventArgs e) {
            HockeyClient.Current.TrackEvent("Heartbeat", new Dictionary<string, string> {
                [nameof(HeartbeatIntervalMinutes)] = Invariant($"{HeartbeatIntervalMinutes}"),
                [nameof(Expiration.IsDomainUser)] = Invariant($"{Expiration.IsDomainUser()}"),
                [nameof(Version)] = Invariant($"{Version}"),
                [nameof(Uptime)] = Invariant($"{Uptime}"),
                [nameof(IsUwp)] = Invariant($"{IsUwp}"),
            });
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
            var relativeDropPoint = screen.PointFromScreen(dropPoint.ToWPF());
            var zone = screen.GetZone(relativeDropPoint)?.GetFinalTarget();
            if (zone == null)
                return;
            this.Move(window, zone);
        }

        void Move(IntPtr windowHandle, Zone zone)
        {
            var window = new Win32Window(windowHandle, suppressSystemMargin: false);
            this.layoutManager.Move(window, zone);
        }

        void ShowNotification(string title, string message, Uri navigateTo, TimeSpan? duration = null, ToolTipIcon icon = ToolTipIcon.None) {
            int timeout = (int)duration.GetValueOrDefault(TimeSpan.FromSeconds(1)).TotalMilliseconds;
            this.trayIcon.Tag = navigateTo;
            this.trayIcon.ShowBalloonTip(tipTitle: title, tipText: message, timeout: timeout, tipIcon: icon);
        }

        void NonCriticalErrorHandler(object sender, ErrorEventArgs error) {
            this.ShowNotification(title: "Stack error", message: error.GetException().Message, navigateTo: null, icon: ToolTipIcon.Warning);
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
            if (!this.stackSettings.Behaviors.MouseMove.Enabled)
                return;
            this.dragOperation = this.DragStart();
            @event.Handled = this.dragOperation != null;
        }

        void OnDragStartPreview(object sender, DragHookEventArgs args)
        {
            if (!this.stackSettings.Behaviors.MouseMove.Enabled)
                return;

            args.Handled = this.DragStart() != null;
        }

        WindowDragOperation DragStart()
        {
            //var point = new POINT { x = (int)location.X, y = (int)location.Y };
            User32.GetCursorPos(out var point);
            var desktop = GetDesktopWindow();
            var child = ChildWindowFromPointEx(desktop, point, ChildWindowFromPointExFlags.CWP_SKIPINVISIBLE);
            try {
                if (child == IntPtr.Zero)
                    return null;

                if (this.stackSettings.Behaviors.MouseMove.WindowGroupIgnoreList.Contains(
                        this.stackSettings.WindowGroups, child))
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
            this.layoutManager?.Dispose();
            this.layoutManager = null;
            this.hook?.Dispose();
            this.dragHook?.Dispose();
            this.dragHook = null;
            this.keyboardArrowBehavior?.Dispose();
            this.trayIcon?.Dispose();

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
            FrameworkElement[] layouts = await Task.WhenAll(screens
                .Select(screen => this.GetLayoutForScreen(screen, settings, this.layoutsFolder))
                .ToArray());
            this.screenLayouts = new ObservableCollection<ScreenLayout>();
            int zoneIndex = 0;

            async Task AddLayoutForScreen(Win32Screen screen)
            {
                var layoutTask = this.GetLayoutForScreen(screen, settings, this.layoutsFolder);
                var layout = new ScreenLayout {
                    Opacity = 0.7,
                    ViewModel = new ScreenLayoutViewModel{Screen = screen},
                    Title = $"{screen.ID}: {ScreenLayouts.GetDesignation(screen)}"
                };
                layout.Closed += this.OnLayoutClosed;
                layout.QueryContinueDrag += (sender, args) => args.Action = DragAction.Cancel;
                layout.SizeChanged += LayoutBoundsChanged;
                layout.LocationChanged += LayoutBoundsChanged;
                // windows must be visible before calling AdjustToClientArea,
                // otherwise final position is unpredictable
                foreach (Zone zone in layout.Zones) {
                    zone.NonFatalErrorOccurred += this.NonCriticalErrorHandler;
                    zone.Id = zone.Id ?? $"{zoneIndex++}";
                }
                this.screenLayouts.Add(layout);
                try {
                    await layout.SetLayout(await layoutTask);
                } catch (OperationCanceledException) {}
            }

            void RemoveLayoutForScreen(Win32Screen screen) {
                ScreenLayout layout = this.screenLayouts.FirstOrDefault(l => l.Screen?.ID == screen.ID);
                if (layout != null) {
                    foreach (Zone zone in layout.Zones)
                        zone.NonFatalErrorOccurred -= this.NonCriticalErrorHandler;
                    layout.Closed -= this.OnLayoutClosed;
                    try {
                        layout.Close();
                    } catch (InvalidOperationException) { }
                    this.screenLayouts.Remove(layout);
                }
            }

            Task changeGroupTask;
            async void LayoutBoundsChanged(object sender, EventArgs e) {
                var layout = (ScreenLayout)sender;
                Task delay = Task.Delay(millisecondsDelay: 15);
                changeGroupTask = delay;
                await delay;
                if (delay.Equals(changeGroupTask))
                    try {
                        await layout.SetLayout(await this.GetLayoutForScreen(layout.Screen,
                            settings, this.layoutsFolder));
                    } catch (OperationCanceledException) { }
            }

            async void ScreenPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
                var screen = (Win32Screen)sender;
                switch (e.PropertyName) {
                case nameof(Win32Screen.WorkingArea):
                case nameof(Win32Screen.IsActive):
                    if (!IsValidScreen(screen))
                        RemoveLayoutForScreen(screen);
                    else {
                        var layout = this.screenLayouts.FirstOrDefault(l => l.Screen.ID == screen.ID);
                        if (layout == null)
                            await AddLayoutForScreen(screen);
                    }
                    return;
                }
            }

            var layoutsCollection = new TransformObservableCollection<string, ObservableFile,
                ReadOnlyObservableCollection<ObservableFile>>(
                this.layoutsDirectory.Files,
                file => Path.GetFileNameWithoutExtension(file.FullName));

            var layoutLaunches = new List<Task>();
            foreach (Win32Screen screen in screens) {
                if (IsValidScreen(screen)) {
                    layoutLaunches.Add(AddLayoutForScreen(screen));

                    if (settings.LayoutMap.NeedsUpdate(screen))
                    {
                        string defaultOption = settings.LayoutMap.GetPreferredLayout(screen)
                                               ?? this.GetSuggestedLayout(screen);
                        defaultOption = Path.GetFileNameWithoutExtension(defaultOption);
                        settings.LayoutMap.SetPreferredLayout(screen, fileName: $"{defaultOption}.xaml");
                        var selectorViewModel = new LayoutSelectorViewModel {
                            Layouts = layoutsCollection,
                            Screen = screen,
                            ScreenName = ScreenLayouts.GetDesignation(screen),
                            Selected = defaultOption,
                            Settings = settings.LayoutMap,
                        };
                        var selector = new ScreenLayoutSelector {
                            LayoutLoader = this.layoutLoader,
                            DataContext = selectorViewModel,
                        };
                        selector.Show();
                        selector.FitToMargin(screen);
                        selector.UpdateLayout();
                        selector.ScrollToSelection();
                    }
                }
                screen.PropertyChanged += ScreenPropertyChanged;
            }

            await Task.WhenAll(layoutLaunches);

            screens.OnChange<Win32Screen>(onAdd: async s => {
                if (IsValidScreen(s))
                    await AddLayoutForScreen(s);
                s.PropertyChanged += ScreenPropertyChanged;
            }, onRemove: s => {
                s.PropertyChanged -= ScreenPropertyChanged;
                RemoveLayoutForScreen(s);
            });

            settings.LayoutMap.Map.CollectionChanged += this.MapOnCollectionChanged;
        }

        string GetSuggestedLayout(Win32Screen screen) {
            if (!screen.WorkingArea.IsHorizontal())
                return "V Top+Rest";

            string[] screens = this.screenProvider.Screens
                               .Where(IsValidScreen)
                               .OrderBy(s => s.WorkingArea.Left)
                               .Select(s => s.ID).ToArray();
            bool isOnTheRight = screens.Length > 1 && screens.Last() == screen.ID;
            bool isBig = screen.TransformFromDevice.Transform(screen.WorkingArea.Size.AsWPFVector()).X > 2000;
            bool isWide = screen.WorkingArea.Width > 2.1 * screen.WorkingArea.Height;
            string leftOrRight = isOnTheRight ? "Right" : "Left";
            string kind = isWide ? "Wide" : isBig ? "Large Horizontal" : "Small Horizontal";
            return $"{kind} {leftOrRight}";
        }

        static bool IsValidScreen(Win32Screen screen) => screen.IsActive && screen.WorkingArea.Width > 1 && screen.WorkingArea.Height > 1;

        async void MapOnCollectionChanged(object o, NotifyCollectionChangedEventArgs change) {
            switch (change.Action) {
            case NotifyCollectionChangedAction.Add:
            case NotifyCollectionChangedAction.Replace when change.OldItems.Count == 1:
                var newRecord = change.NewItems.OfType<MutableKeyValuePair<string, string>>().Single();
                var layoutToUpdate = this.screenLayouts.FirstOrDefault(
                    layout => layout.Screen?.ID == newRecord.Key
                    || layout.Screen != null && ScreenLayouts.GetDesignation(layout.Screen) == newRecord.Key);
                layoutToUpdate?.SetLayout(await this.GetLayoutForScreen(layoutToUpdate.Screen, this.stackSettings, this.layoutsFolder));
                break;
            default:
                return;
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
        async Task<FrameworkElement> GetLayoutForScreen(Win32Screen screen, StackSettings settings, IFolder layoutsDirectory)
        {
            string layout = settings.LayoutMap.GetPreferredLayout(screen)
                          ?? $"{this.GetSuggestedLayout(screen)}.xaml";
            return await this.layoutLoader.LoadLayoutOrDefault(layout);
        }

        private void BindHandlers(StackSettings settings)
        {
            this.hook = Hook.GlobalEvents();

            this.layoutManager = new LayoutManager(this.screenLayouts);

            this.keyboardArrowBehavior = new KeyboardArrowBehavior(
                this.hook, this.screenLayouts, this.layoutManager, settings.Behaviors.KeyBindings,
                settings.Behaviors.KeyboardMove,
                settings.WindowGroups,
                this.Move);

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

        readonly DispatcherTimer upgradeOfferTimer = new DispatcherTimer {
            Interval =
#if DEBUG
                TimeSpan.FromSeconds(15),
#else
                TimeSpan.FromHours(1),
#endif

        };
        void SuggestUpgrade() {
            string osVersion = Environment.OSVersion.Version.ToString();
            var notifications = this.stackSettings.Notifications;
            if (notifications.LastUpgradeOffer?.AddMonths(3) > DateTimeOffset.Now
                && notifications.OsVersionUpgradeSuggested == osVersion)
                return;

            if (!OSInfo.SupportsDesktopBridge())
                return;

            this.upgradeOfferTimer.Tick += delegate {
                this.SuggestUpgradeNow();
                this.upgradeOfferTimer.Stop();
            };
            this.upgradeOfferTimer.Start();
        }

        void SuggestUpgradeNow() {
            var upgradeOffer = new UpgradeOffer();
            upgradeOffer.Show();
        }
    }
}
