namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using LostTech.Stack.Models;
    using PCLStorage;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Application = System.Windows.Application;
    using Screen = LostTech.Windows.Screen;

    class TrayIcon
    {
        public static async Task<NotifyIcon> StartTrayIcon(IFolder layoutsFolder, StackSettings stackSettings)
        {
            var contextMenu = new ContextMenu();

            foreach (var screen in Screen.AllScreens) {
                var menu = new MenuItem(screen.Name);
                foreach (var file in await layoutsFolder.GetFilesAsync()) {
                    if (Path.GetExtension(file.Name) != ".xaml")
                        continue;
                    var name = Path.GetFileNameWithoutExtension(file.Name);
                    var switchToThatLayout = new MenuItem(name, SwitchLayoutClick) {
                        Tag = new KeyValuePair<Screen, string>(screen, name),
                        RadioCheck = true,
                        Checked = stackSettings.LayoutMap.GetPreferredLayout(screen) == file.Name,
                    };
                    menu.MenuItems.Add(switchToThatLayout);
                }
                contextMenu.MenuItems.Add(menu);
            }

            contextMenu.MenuItems.Add(new MenuItem("-"));
            contextMenu.MenuItems.Add(new MenuItem("Exit", onClick: (_, __) => Application.Current.Shutdown()));

            var bitmap = new System.Drawing.Bitmap(32, 32);
            return new NotifyIcon {
                ContextMenu = contextMenu,
                Icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon()),
                Text = nameof(LostTech.Stack),
                Visible = true,
            };
        }

        static void SwitchLayoutClick(object sender, EventArgs eventArgs) { throw new NotImplementedException(); }
    }
}
