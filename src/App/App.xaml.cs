namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Media;
    using LostTech.Windows;
    using System.IO;
    using System.Reflection;
    using System.Windows.Controls;
    using System.Xml;
    using System.Windows.Markup;
    using static System.FormattableString;
    using Gma.System.MouseKeyHook;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        IKeyboardMouseEvents hook;
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.StartLayout();
            // TODO: MIT license for MouseKeyboardActivityMonitor
            this.hook = Hook.GlobalEvents();
            this.hook.MouseDownExt += this.GlobalMouseDown;

            //this.MainWindow = new MyPos();
            //this.MainWindow.Show();
        }

        void GlobalMouseDown(object sender, MouseEventExtArgs mouseEventExtArgs)
        {

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
                this.MainWindow = layout;
            }
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

        FrameworkElement MakeDefaultLayout() => new Grid();

        public static DirectoryInfo AppData {
            get {
                // TODO: implement roaming
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var path = Path.Combine(appData, "Lost Tech LLC", nameof(LostTech.Stack));
                return Directory.CreateDirectory(path);
            }
        }

        public static Version Version => Assembly.GetExecutingAssembly().GetName().Version;
    }
}
