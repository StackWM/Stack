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
    using DragDropEffects = System.Windows.DragDropEffects;

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
            Rect targetBounds = zone.GetPhysicalBounds();
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
            this.dragOperation = null;
            foreach (var screenLayout in this.screenLayouts) {
                screenLayout.Hide();
            }
            User32.SetForegroundWindow(window);
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
            this.BindHandlers();

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
                //layout.Hide();
                screenLayouts.Add(layout);
            }
            this.screenLayouts = screenLayouts;
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
