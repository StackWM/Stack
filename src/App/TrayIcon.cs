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
    using LostTech.Stack.Models;
    using PCLStorage;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Forms;
    using LostTech.Windows;
    using Application = System.Windows.Application;
    using MessageBox = System.Windows.MessageBox;

    class TrayIcon
    {
        public NotifyIcon Icon { get; }
        StackSettings stackSettings;

        TrayIcon(NotifyIcon trayIcon, StackSettings stackSettings)
        {
            this.Icon = trayIcon;
            this.stackSettings = stackSettings;
        }

        public static async Task<TrayIcon> StartTrayIcon(IFolder layoutsFolder, StackSettings stackSettings, IScreenProvider screenProvider)
        {
            var contextMenu = new ContextMenuStrip();
            var bitmap = new System.Drawing.Bitmap(32, 32);
            var font = contextMenu.Font;
            var boldFont = new Font(font, System.Drawing.FontStyle.Bold);

            var trayIcon = new TrayIcon(new NotifyIcon
            {
                ContextMenuStrip = contextMenu,
                Icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon()),
                Text = nameof(LostTech.Stack),
                Visible = true,
            }, stackSettings);

            foreach (var screen in screenProvider.Screens) {
                var menu = new ToolStripMenuItem(screen.ToString());
                Binder.Bind(val => menu.Font = val ? boldFont : font, screen, s => s.IsPrimary);
                Binder.Bind(val => menu.Visible = val, screen, s => s.IsActive);

                foreach (var file in await layoutsFolder.GetFilesAsync()) {
                    if (Path.GetExtension(file.Name) != ".xaml")
                        continue;
                    var name = Path.GetFileNameWithoutExtension(file.Name);
                    var switchToThatLayout = new ToolStripMenuItem(name, null, trayIcon.SwitchLayoutClick) {
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
            contextMenu.Items.Add(new ToolStripMenuItem("Exit", image: null,
                onClick: (_, __) => ((App)Application.Current).BeginShutdown()));

            return trayIcon;
        }

        void SwitchLayoutClick(object sender, EventArgs eventArgs)
        {
            var menuItem = (ToolStripMenuItem) sender;
            foreach (var item in ((ToolStripMenuItem) menuItem.OwnerItem).DropDownItems.OfType<ToolStripMenuItem>()) {
                item.Checked = false;
            }

            var mapping = (KeyValuePair<Win32Screen, string>) menuItem.Tag;
            var entryIndex = stackSettings.LayoutMap.GetPreferredLayoutIndex(mapping.Key);
            var newMapping = new MutableKeyValuePair<string, string>(mapping.Key.ID, mapping.Value + ".xaml");
            if (entryIndex < 0)
                this.stackSettings.LayoutMap.Map.Add(newMapping);
            else
                this.stackSettings.LayoutMap.Map[entryIndex] = newMapping;
            Process.Start(Process.GetCurrentProcess().MainModule.FileName);
        }

        class Binder
        {
            public static void Bind<TDest, TPropertyType, TSource>(TDest dest, Action<TDest, TPropertyType> setter,
                TSource source, Expression<Func<TSource, TPropertyType>> sourceProperty)
                where TSource : INotifyPropertyChanged
            {
                if (dest == null)
                    throw new ArgumentNullException(nameof(dest));
                if (setter == null)
                    throw new ArgumentNullException(nameof(setter));
                if (source == null)
                    throw new ArgumentNullException(nameof(source));
                if (sourceProperty == null)
                    throw new ArgumentNullException(nameof(sourceProperty));
                if (!(sourceProperty.Body is MemberExpression sourceMember))
                    throw new ArgumentException(message: "Lambda must be a property access expression", paramName: nameof(sourceProperty));

                var getter = sourceProperty.Compile();
                string propertyName = sourceMember.Member.Name;
                source.PropertyChanged += (_, args) => {
                    if (args.PropertyName == propertyName)
                        setter(dest, getter(source));
                };
                setter(dest, getter(source));
            }

            public static void Bind<TPropertyType, TSource>(Action<TPropertyType> setter,
                TSource source, Expression<Func<TSource, TPropertyType>> sourceProperty)
                where TSource : INotifyPropertyChanged
            {
                if (setter == null)
                    throw new ArgumentNullException(nameof(setter));
                if (source == null)
                    throw new ArgumentNullException(nameof(source));
                if (sourceProperty == null)
                    throw new ArgumentNullException(nameof(sourceProperty));
                if (!(sourceProperty.Body is MemberExpression sourceMember))
                    throw new ArgumentException(message: "Lambda must be a property access expression", paramName: nameof(sourceProperty));

                var getter = sourceProperty.Compile();
                string propertyName = sourceMember.Member.Name;
                source.PropertyChanged += (_, args) => {
                    if (args.PropertyName == propertyName)
                        setter(getter(source));
                };
                setter(getter(source));
            }
        }
    }
}
