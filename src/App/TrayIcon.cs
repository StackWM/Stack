namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Forms;
    using LostTech.Stack.Models;
    using LostTech.Windows;
    using Microsoft.VisualBasic;
    using PCLStorage;
    using PInvoke;
    using Application = System.Windows.Application;
    using FileAccess = PCLStorage.FileAccess;
    using FontStyle = System.Drawing.FontStyle;
    using MessageBox = System.Windows.MessageBox;

    class TrayIcon
    {
        public NotifyIcon Icon { get; }
        readonly StackSettings stackSettings;
        readonly IFolder layoutsFolder;

        TrayIcon(NotifyIcon trayIcon, StackSettings stackSettings, IFolder layoutsFolder)
        {
            this.Icon = trayIcon;
            this.stackSettings = stackSettings;
            this.layoutsFolder = layoutsFolder ?? throw new ArgumentNullException(nameof(layoutsFolder));
        }

        public static async Task<TrayIcon> StartTrayIcon(IFolder layoutsFolder, StackSettings stackSettings, IScreenProvider screenProvider)
        {
            var contextMenu = new ContextMenuStrip();
            var bitmap = new Bitmap(32, 32);

            var layouts = (await layoutsFolder.GetFilesAsync())
                .Where(layoutFile => Path.GetExtension(layoutFile.Name) == ".xaml")
                .ToArray();

            var trayIcon = new TrayIcon(new NotifyIcon
            {
                ContextMenuStrip = contextMenu,
                Icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon()),
                Text = nameof(LostTech.Stack),
                Visible = true,
            }, stackSettings, layoutsFolder);

            trayIcon.CreateScreensMenu(layouts, stackSettings, screenProvider, contextMenu);
            trayIcon.CreateLayoutsMenu(layoutsFolder, layouts, contextMenu);

            contextMenu.Items.Add(new ToolStripMenuItem("Exit", image: null,
                onClick: (_, __) => ((App)Application.Current).BeginShutdown()));

            return trayIcon;
        }

        void CreateLayoutsMenu(IFolder layoutsFolder, IEnumerable<IFile> layouts, ToolStrip contextMenu)
        {
            var layoutsMenu = new ToolStripMenuItem("Edit Layout");

            layoutsMenu.DropDownItems.Add(new ToolStripMenuItem("Use text editor") {Enabled = false});

            foreach (var layoutFile in layouts) {
                var name = Path.GetFileNameWithoutExtension(layoutFile.Name);
                var layoutMenu = new ToolStripMenuItem(name, null, EditLayoutClick) { Tag = layoutFile };
                layoutsMenu.DropDownItems.Add(layoutMenu);
            }

            layoutsMenu.DropDownItems.Add(new ToolStripSeparator());
            layoutsMenu.DropDownItems.Add(new ToolStripMenuItem("New...", null, this.CreateNewLayout));
            layoutsMenu.DropDownItems.Add(new ToolStripMenuItem(
                "Open Layouts Folder", null,
                (_, __) => Process.Start(layoutsFolder.Path)));

            contextMenu.Items.Add(layoutsMenu);
            contextMenu.Items.Add(new ToolStripSeparator());
        }

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
            string sampleResourceName = App.OutOfBoxLayoutsResourcePrefix + "Sample.xaml";
            var resourceContainer = App.GetResourceContainer();
            using (var sampleStream = resourceContainer.GetManifestResourceStream(sampleResourceName))
            using (var layoutStream = await layoutFile.OpenAsync(FileAccess.ReadAndWrite).ConfigureAwait(false)) {
                await sampleStream.CopyToAsync(layoutStream).ConfigureAwait(false);
                layoutStream.Close();
            }
        }

        void CreateScreensMenu(ICollection<IFile> layouts, StackSettings stackSettings, IScreenProvider screenProvider,
            ToolStrip contextMenu)
        {
            var font = contextMenu.Font;
            var boldFont = new Font(font, FontStyle.Bold);
            foreach (var screen in screenProvider.Screens)
            {
                var menu = new ToolStripMenuItem(screen.ToString());
                screen.OnChange(s => s.IsPrimary, val => menu.Font = val ? boldFont : font);
                screen.OnChange(s => s.IsActive, val => menu.Visible = val);

                foreach (var file in layouts)
                {
                    var name = Path.GetFileNameWithoutExtension(file.Name);
                    var switchToThatLayout = new ToolStripMenuItem(name, null, this.SwitchLayoutClick)
                    {
                        Tag = new KeyValuePair<Win32Screen, string>(screen, name),
                        CheckOnClick = true,
                        Checked = stackSettings.LayoutMap.GetPreferredLayout(screen) == file.Name,
                        Font = font,
                    };
                    menu.DropDownItems.Add(switchToThatLayout);
                }
                contextMenu.Items.Add(menu);
            }
            contextMenu.Items.Add(new ToolStripSeparator());
        }

        void SwitchLayoutClick(object sender, EventArgs eventArgs)
        {
            var menuItem = (ToolStripMenuItem) sender;
            foreach (var item in ((ToolStripMenuItem) menuItem.OwnerItem).DropDownItems.OfType<ToolStripMenuItem>()) {
                item.Checked = false;
            }

            var mapping = (KeyValuePair<Win32Screen, string>) menuItem.Tag;
            var entryIndex = this.stackSettings.LayoutMap.GetPreferredLayoutIndex(mapping.Key);
            var newMapping = new MutableKeyValuePair<string, string>(mapping.Key.ID, mapping.Value + ".xaml");
            if (entryIndex < 0)
                this.stackSettings.LayoutMap.Map.Add(newMapping);
            else
                this.stackSettings.LayoutMap.Map[entryIndex] = newMapping;
            Process.Start(Process.GetCurrentProcess().MainModule.FileName);
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

    static class Binder
    {
        public static void OnChange<TPropertyType, TSource>(this TSource dataSource, Expression<Func<TSource, TPropertyType>> property, Action<TPropertyType> action)
            where TSource : INotifyPropertyChanged
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (dataSource == null)
                throw new ArgumentNullException(nameof(dataSource));
            if (property == null)
                throw new ArgumentNullException(nameof(property));
            if (!(property.Body is MemberExpression sourceMember))
                throw new ArgumentException(message: "Lambda must be a property access expression", paramName: nameof(property));

            var getter = property.Compile();
            string propertyName = sourceMember.Member.Name;
            dataSource.PropertyChanged += (_, args) => {
                if (args.PropertyName == propertyName)
                    action(getter(dataSource));
            };
            action(getter(dataSource));
        }
    }
}
