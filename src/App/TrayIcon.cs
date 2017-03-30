namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using LostTech.Stack.Models;
    using PCLStorage;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Forms;
    using Application = System.Windows.Application;
    using MessageBox = System.Windows.MessageBox;
    using Screen = LostTech.Windows.Screen;

    class TrayIcon
    {
        public NotifyIcon Icon { get; }
        StackSettings stackSettings;

        private TrayIcon(NotifyIcon trayIcon, StackSettings stackSettings)
        {
            this.Icon = trayIcon;
            this.stackSettings = stackSettings;
        }

        public static async Task<TrayIcon> StartTrayIcon(IFolder layoutsFolder, StackSettings stackSettings)
        {
            var contextMenu = new ContextMenu();
            var bitmap = new System.Drawing.Bitmap(32, 32);

            var trayIcon = new TrayIcon(new NotifyIcon
            {
                ContextMenu = contextMenu,
                Icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon()),
                Text = nameof(LostTech.Stack),
                Visible = true,
            }, stackSettings);

            foreach (var screen in Screen.AllScreens) {
                var menu = new MenuItem(screen.Name) { DefaultItem = screen.IsPrimary };
                foreach (var file in await layoutsFolder.GetFilesAsync()) {
                    if (Path.GetExtension(file.Name) != ".xaml")
                        continue;
                    var name = Path.GetFileNameWithoutExtension(file.Name);
                    var switchToThatLayout = new MenuItem(name, trayIcon.SwitchLayoutClick) {
                        Tag = new KeyValuePair<Screen, string>(screen, name),
                        RadioCheck = true,
                        Checked = stackSettings.LayoutMap.GetPreferredLayout(screen) == file.Name,
                    };
                    menu.MenuItems.Add(switchToThatLayout);
                }
                contextMenu.MenuItems.Add(menu);
            }

            contextMenu.MenuItems.Add(new MenuItem("-"));
            contextMenu.MenuItems.Add(new MenuItem("Exit", onClick: (_, __) => ((App)Application.Current).BeginShutdown()));

            return trayIcon;
        }

        void SwitchLayoutClick(object sender, EventArgs eventArgs)
        {
            var menuItem = (MenuItem) sender;
            menuItem.Checked = true;
            var mapping = (KeyValuePair<Screen, string>) menuItem.Tag;
            var entryIndex = stackSettings.LayoutMap.GetPreferredLayoutIndex(mapping.Key);
            var newMapping = new MutableKeyValuePair<string, string>(mapping.Key.ID, mapping.Value + ".xaml");
            if (entryIndex < 0)
                this.stackSettings.LayoutMap.Map.Add(newMapping);
            else
                this.stackSettings.LayoutMap.Map[entryIndex] = newMapping;
            MessageBox.Show(caption: "Restart required", messageBoxText: "To apply changes, restart the app",
                button: MessageBoxButton.OK, icon: MessageBoxImage.Warning);
        }
    }
}
