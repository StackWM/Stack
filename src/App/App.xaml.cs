namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Configuration;
    using System.Diagnostics;
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
    using LostTech.Stack.Zones;
    using PInvoke;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        IKeyboardMouseEvents hook;
        WindowDragOperation dragOperation;
        ICollection<ScreenLayout> screenLayouts;
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.StartLayout();
            // TODO: MIT license for MouseKeyboardActivityMonitor
            this.hook = Hook.GlobalEvents();
            this.hook.MouseDownExt += this.GlobalMouseDown;
            this.hook.MouseUpExt += this.GlobalMouseUp;

            //this.MainWindow = new MyPos();
            //this.MainWindow.Show();
        }

        private void GlobalMouseUp(object sender, MouseEventExtArgs @event)
        {
            if (@event.Button != MouseButtons.Middle || this.dragOperation == null)
                return;

            var dx = @event.Location.X - this.dragOperation.StartLocation.X;
            var dy = @event.Location.Y - this.dragOperation.StartLocation.Y;
            var dropPoint = @event.Location.ToWPF();
            var window = this.dragOperation.Window;
            this.dragOperation = null;
            foreach (var screenLayout in this.screenLayouts) {
                screenLayout.Hide();
            }
            User32.SetForegroundWindow(window);
            if (Math.Abs(dx) < DragThreshold || Math.Abs(dy) < DragThreshold)
                return;

            var screen = this.screenLayouts.SingleOrDefault(layout => layout.GetPhysicalBounds().Contains(dropPoint));
            if (screen == null)
                return;
            var relativeDropPoint = screen.PointFromScreen(dropPoint);
            var zone = screen.GetZone(relativeDropPoint);
            if (zone == null)
                return;
            Rect targetBounds = zone.GetPhysicalBounds();
            if (!User32.MoveWindow(window, (int) targetBounds.Left, (int) targetBounds.Top, (int) targetBounds.Width,
                (int) targetBounds.Height, false))
                throw new System.ComponentModel.Win32Exception();
        }

        void GlobalMouseDown(object sender, MouseEventExtArgs @event)
        {
            if (@event.Button != MouseButtons.Middle)
                return;

            this.dragOperation = DragStart(@event.Location);
        }

        private WindowDragOperation DragStart(System.Drawing.Point location)
        {
            var desktop = User32.GetDesktopWindow();
            var child = User32.ChildWindowFromPointEx(desktop, new POINT {x = location.X, y = location.Y},
                User32.ChildWindowFromPointExFlags.CWP_SKIPINVISIBLE);
            if (child == IntPtr.Zero)
                return null;
            foreach (var screenLayout in this.screenLayouts) {
                screenLayout.Show();
                screenLayout.Activate();
            }
            return new WindowDragOperation(child, location);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            this.hook.Dispose();

            base.OnExit(e);
        }

        void StartLayout()
        {
            var layoutDirectory = AppData.CreateSubdirectory(@"Layouts\Default");
            var defaultLayout = new Lazy<FrameworkElement>(() => this.LoadLayoutOrDefault(layoutDirectory, "Default.xaml"));
            var primary = Screen.Primary;
            var screens = Screen.AllScreens.ToArray();
            var layouts = Enumerable.Range(0, screens.Length)
                .Select(screenIndex => LoadLayoutOrDefault(layoutDirectory, Invariant($"{screenIndex:D3}.xaml")))
                .ToArray();
            var screenLayouts = new List<ScreenLayout>();
            for (var screenIndex = 0; screenIndex < screens.Length; screenIndex++) {
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
        }

        private FrameworkElement LoadLayoutOrDefault(DirectoryInfo layoutDirectory, string fileName)
        {
            // TODO: SEC: untrusted XAML https://msdn.microsoft.com/en-us/library/ee856646(v=vs.110).aspx

            if (layoutDirectory == null)
                throw new ArgumentNullException(nameof(layoutDirectory));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentNullException(nameof(fileName));

            if (!layoutDirectory.Exists)
                return this.MakeDefaultLayout();

            if (Path.GetInvalidFileNameChars().Any(fileName.Contains))
                throw new ArgumentException();

            var fullName = Path.Combine(layoutDirectory.FullName, fileName);
            if (!File.Exists(fullName))
                return this.MakeDefaultLayout();

            using (var xmlReader = XmlReader.Create(fullName)) {
                var layout = (FrameworkElement) XamlReader.Load(xmlReader);
                Debug.WriteLine($"loaded layout {fileName}");
                return layout;
            }
        }

        FrameworkElement MakeDefaultLayout() => new Grid {
            Children = {
                new Zone {
                    Background = Brushes.Gray,
                },
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
