namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Forms;
    using LostTech.App;
    using LostTech.Stack.DataBinding;
    using LostTech.Stack.Models;
    using LostTech.Stack.Settings;
    using LostTech.Windows;
    using Microsoft.VisualBasic;
    using PCLStorage;
    using Application = System.Windows.Application;
    using FileAccess = PCLStorage.FileAccess;
    using FileSystem = PCLStorage.FileSystem;
    using FontStyle = System.Drawing.FontStyle;
    using MessageBox = System.Windows.MessageBox;

    class TrayIcon
    {
        public NotifyIcon Icon { get; }
        readonly StackSettings stackSettings;
        readonly IFolder layoutsFolder;
        readonly About aboutWindow = new About();

        TrayIcon(NotifyIcon trayIcon, StackSettings stackSettings, IFolder layoutsFolder)
        {
            this.Icon = trayIcon;
            this.stackSettings = stackSettings;
            this.layoutsFolder = layoutsFolder ?? throw new ArgumentNullException(nameof(layoutsFolder));
        }

        public static async Task<TrayIcon> StartTrayIcon(IFolder layoutsFolder, ObservableDirectory layoutsDirectory, StackSettings stackSettings, IScreenProvider screenProvider, SettingsWindow settingsWindow)
        {
            var contextMenu = new ContextMenuStrip();

            var trayIcon = new TrayIcon(new NotifyIcon
            {
                ContextMenuStrip = contextMenu,
                Icon = new Icon(Application.GetResourceStream(new Uri("pack://application:,,,/StackTrayIcon.ico")).Stream),
                Text = nameof(Stack),
                Visible = true,
            }, stackSettings, layoutsFolder);

            contextMenu.Items.Add("Settings", image: null, onClick: (_, __) => settingsWindow.Show())
                .Font = new Font(contextMenu.Font, FontStyle.Bold);
            trayIcon.Icon.DoubleClick += delegate { settingsWindow.Show(); };

            contextMenu.Items.Add(trayIcon.CreateHelpMenu());
            ToolStripMenuItem feedback = new DesktopBridge.Helpers().IsRunningAsUwp()
                ? Link(text: "Leave Feedback / Rate", url: "ms-windows-store:REVIEW?PFN=LostTechLLC.Zones_kdyhxf5sz30e2")
                : Link(text: "Feedback...", url: "http://bit.ly/2o7Rxvr");
            contextMenu.Items.Add(feedback);

            var keyboardMovement = new ToolStripMenuItem("Override Win key + arrows") {
                Checked = stackSettings.Behaviors.KeyboardMove.Enabled,
            };
            keyboardMovement.Click += delegate {
                keyboardMovement.Checked = stackSettings.Behaviors.KeyboardMove.Enabled = !keyboardMovement.Checked;
                App.Restart();
            };

            var middleMouseMovement = new ToolStripMenuItem("Middle mouse drag") {
                Checked = stackSettings.Behaviors.MouseMove.Enabled,
            };
            middleMouseMovement.Click += delegate {
                middleMouseMovement.Checked = stackSettings.Behaviors.MouseMove.Enabled = !middleMouseMovement.Checked;
                App.Restart();
            };
            contextMenu.Items.Add(keyboardMovement);
            contextMenu.Items.Add(middleMouseMovement);
            contextMenu.Items.Add(new ToolStripSeparator());

            trayIcon.CreateScreensMenu(layoutsDirectory, screenProvider, contextMenu);
            trayIcon.CreateLayoutsMenu(layoutsDirectory, contextMenu);

            contextMenu.Items.Add(new ToolStripMenuItem("Restart", image: null,
                onClick: (_, __) => App.Restart()) {
                DisplayStyle = ToolStripItemDisplayStyle.Text
            });
            if (!new DesktopBridge.Helpers().IsRunningAsUwp())
            {
                contextMenu.Items.Add(new ToolStripMenuItem("Restart as Admin", image: null,
                    onClick: (_, __) => App.RestartAsAdmin()) {
                    DisplayStyle = ToolStripItemDisplayStyle.Text,
                });
            }
#if DEBUG
            contextMenu.Items.Add(new ToolStripMenuItem("D: THROW", image: null,
                onClick: (_, __) => throw new Exception("Requested from context menu")) {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
            });
#endif

            contextMenu.Items.Add(new ToolStripMenuItem("Exit", image: null,
                onClick: (_, __) => ((App)Application.Current).BeginShutdown()) {
                DisplayStyle = ToolStripItemDisplayStyle.Text
            });

            return trayIcon;
        }

        ToolStripMenuItem CreateHelpMenu() {
            var help = new ToolStripMenuItem("Help", image: null) {DisplayStyle = ToolStripItemDisplayStyle.Text};
            help.DropDownItems.Add(Link("Blog", "http://stack.blogs.losttech.software/"));
            help.DropDownItems.Add(Link("Telegram Community","https://t.me/joinchat/HCVquw4yDSmwxky5pxxKZw"));
            help.DropDownItems.Add(Link("Ask a Question", "https://www.allanswered.com/community/s/stack-wm/"));
            help.DropDownItems.Add(Link("What's New", "https://losttech.software/stack-whatsnew.html"));
            help.DropDownItems.Add("About", image: null, onClick: (_, __) => this.aboutWindow.Show());
            return help;
        }

        static ToolStripMenuItem Link(string text, string url) => 
            new ToolStripMenuItem(text, image: null, onClick: (_,__) => Process.Start(url));

        void CreateLayoutsMenu(ObservableDirectory layoutsDirectory, ToolStrip contextMenu)
        {
            var layoutsMenu = new ToolStripMenuItem("Edit Layout"){DisplayStyle = ToolStripItemDisplayStyle.Text};

            layoutsMenu.DropDownItems.Add(new ToolStripMenuItem("Use text editor") {Enabled = false});

            ToolStripMenuItem MakeLayoutMenuItem(ObservableFile file)
            {
                if (!IsLayoutFileName(file.FullName))
                    return null;

                var name = Path.GetFileNameWithoutExtension(file.FullName);
                var layoutFile = FileSystem.Current.GetFileFromPathAsync(file.FullName).Result;
                var layoutMenu = new ToolStripMenuItem(name, null, EditLayoutClick) { Tag = layoutFile };
                file.OnChange(f => f.FullName,
                    newName => {
                        if (!IsLayoutFileName(newName)) {
                            layoutMenu.DropDownItems.Remove(layoutMenu);
                            return;
                        }

                        layoutMenu.Text = Path.GetFileNameWithoutExtension(newName);
                        layoutMenu.Tag = FileSystem.Current.GetFileFromPathAsync(newName).Result;
                    });
                return layoutMenu;
            }

            foreach (var layoutFile in layoutsDirectory.Files) {
                var menuItem = MakeLayoutMenuItem(layoutFile);
                if (menuItem != null)
                    layoutsMenu.DropDownItems.Add(menuItem);
            }

            layoutsDirectory.Files.OnChange<ObservableFile>(
                onAdd: file => {
                    var menuItem = MakeLayoutMenuItem(file);
                    if (menuItem != null)
                        layoutsMenu.DropDownItems.Insert(1, menuItem);
                }, onRemove: file => {
                    var layoutMenu = layoutsMenu.DropDownItems.OfType<ToolStripMenuItem>()
                        .FirstOrDefault(item => ((IFile) item.Tag)?.Path == file.FullName);
                    if (layoutMenu != null)
                        layoutsMenu.DropDownItems.Remove(layoutMenu);
                });

            layoutsMenu.DropDownItems.Add(new ToolStripSeparator());
            layoutsMenu.DropDownItems.Add(new ToolStripMenuItem("New...", null, this.CreateNewLayout));
            layoutsMenu.DropDownItems.Add(new ToolStripMenuItem(
                "Open Layouts Folder", null,
                (_, __) => Process.Start(this.layoutsFolder.Path)));

            contextMenu.Items.Add(layoutsMenu);
            contextMenu.Items.Add(new ToolStripSeparator());
        }

        static bool IsLayoutFileName(string fileName) => Path.GetExtension(fileName) == ".xaml";

        static void EditLayoutClick(object sender, EventArgs e) => EditLayout((IFile)((ToolStripItem) sender).Tag);

        async void CreateNewLayout(object sender, EventArgs e)
        {
            string layoutName = Interaction.InputBox("Enter new layout name", "New Layout");
            if (string.IsNullOrEmpty(layoutName))
                return;

            char[] invalidChars = Path.GetInvalidFileNameChars();
            if (layoutName.Any(invalidChars.Contains)) {
                MessageBox.Show("Invalid file name", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.CreateNewLayout(sender, e);
                return;
            }

            layoutName += ".xaml";
            IFile layoutFile;
            try {
                layoutFile =
                    await this.layoutsFolder.CreateFileAsync(layoutName, CreationCollisionOption.FailIfExists);
            }
            catch (IOException) {
                MessageBox.Show($"Can not create file {layoutName}. Perhaps it already exists?",
                    "Can not create layout", MessageBoxButton.OK);
                return;
            }

            await WriteSampleLayoutTo(layoutFile);

            EditLayout(layoutFile);
        }

        static void EditLayout(IFile layoutFile)
        {
            ShowOpenWithDialog(layoutFile.Path);
        }

        static void ShowOpenWithDialog(string path)
        {
            SHOpenWithDialog(IntPtr.Zero, new OpenAsInfo {
                FileName = path,
                FileTypeDescription = "WPF Layout",
            });
        }

        static async Task WriteSampleLayoutTo(IFile layoutFile)
        {
            string sampleResourceName = App.OutOfBoxLayoutsResourcePrefix + "OOB Horizontal.xaml";
            var resourceContainer = App.GetResourceContainer();
            using (var sampleStream = resourceContainer.GetManifestResourceStream(sampleResourceName))
            using (var layoutStream = await layoutFile.OpenAsync(FileAccess.ReadAndWrite).ConfigureAwait(false)) {
                await sampleStream.CopyToAsync(layoutStream).ConfigureAwait(false);
                layoutStream.Close();
            }
        }

        void CreateScreensMenu(ObservableDirectory layoutsDirectory, IScreenProvider screenProvider, ToolStrip contextMenu)
        {
            var font = contextMenu.Font;
            var boldFont = new Font(font, FontStyle.Bold);
            foreach (var screen in screenProvider.Screens)
            {
                var menu = new ToolStripMenuItem(ScreenLayouts.GetDesignation(screen)){DisplayStyle = ToolStripItemDisplayStyle.Text};
                screen.OnChange(s => s.IsPrimary, val => menu.Font = val ? boldFont : font);
                screen.OnChange(s => s.IsActive, val => menu.Visible = val);

                void SetupFileRenameHandling(ObservableFile observableFile, ToolStripMenuItem layout) =>
                    observableFile.OnChange(f => f.FullName, fullName => {
                        if (!IsLayoutFileName(fullName)) {
                            menu.DropDownItems.Remove(layout);
                            return;
                        }

                        var tag = (KeyValuePair<Win32Screen, string>) layout.Tag;
                        string name = Path.GetFileNameWithoutExtension(fullName);
                        layout.Tag = new KeyValuePair<Win32Screen, string>(tag.Key, name);
                        layout.Text = Path.GetFileNameWithoutExtension(fullName);
                        layout.Checked = this.stackSettings.LayoutMap.GetPreferredLayout(screen) == Path.GetFileName(fullName);
                    });

                foreach (var file in layoutsDirectory.Files) {
                    var switchToThatLayout = this.SwitchToLayoutMenuItem(file, screen, font);
                    if (switchToThatLayout != null) {
                        SetupFileRenameHandling(file, switchToThatLayout);
                        menu.DropDownItems.Add(switchToThatLayout);
                    }
                }

                layoutsDirectory.Files.OnChange<ObservableFile>(
                    onAdd: file => {
                        var switchToThatLayout = this.SwitchToLayoutMenuItem(file, screen, font);
                        if (switchToThatLayout != null) {
                            SetupFileRenameHandling(file, switchToThatLayout);
                            menu.DropDownItems.Insert(0, switchToThatLayout);
                        }
                    },
                    onRemove: file => {
                        if (!IsLayoutFileName(file.FullName))
                            return;

                        var name = Path.GetFileNameWithoutExtension(file.FullName);
                        var layoutMenu = menu.DropDownItems.OfType<ToolStripMenuItem>()
                            .FirstOrDefault(item => item.Text == name);
                        if (layoutMenu != null)
                            menu.DropDownItems.Remove(layoutMenu);
                    });

                contextMenu.Items.Add(menu);
            }
            contextMenu.Items.Add(new ToolStripSeparator());
        }

        ToolStripMenuItem SwitchToLayoutMenuItem(ObservableFile file, Win32Screen screen, Font font)
        {
            if (!IsLayoutFileName(file.FullName))
                return null;
            var name = Path.GetFileNameWithoutExtension(file.FullName);
            var switchToThatLayout = new ToolStripMenuItem(name, null, this.SwitchLayoutClick) {
                Tag = new KeyValuePair<Win32Screen, string>(screen, name),
                CheckOnClick = true,
                Checked = this.stackSettings.LayoutMap.GetPreferredLayout(screen) == Path.GetFileName(file.FullName),
                Font = font,
            };
            return switchToThatLayout;
        }

        void SwitchLayoutClick(object sender, EventArgs eventArgs)
        {
            var menuItem = (ToolStripMenuItem) sender;
            foreach (var item in ((ToolStripMenuItem) menuItem.OwnerItem).DropDownItems.OfType<ToolStripMenuItem>()) {
                item.Checked = false;
            }
            menuItem.Checked = true;

            var mapping = (KeyValuePair<Win32Screen, string>) menuItem.Tag;
            this.stackSettings.LayoutMap.SetPreferredLayout(mapping.Key, mapping.Value + ".xaml");
        }

        [DllImport("shell32.dll")]
        static extern int SHOpenWithDialog(IntPtr parent, [In] OpenAsInfo openAs);
        [StructLayout(LayoutKind.Sequential)]
        class OpenAsInfo
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string FileName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string FileTypeDescription;
            public OpenAsFlags Flags = OpenAsFlags.Exec;
        }

        enum OpenAsFlags
        {
            Exec = 0x00000004,
        }
    }
}
