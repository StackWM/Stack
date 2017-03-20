namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Media;
    using LostTech.Windows;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.StartLayoutEditor();
            //this.MainWindow = new MyPos();
            //this.MainWindow.Show();
        }

        void StartLayoutEditor()
        {
            var primary = Screen.Primary;
            var backgrounds = new Brush[] {Brushes.Red, Brushes.Green, Brushes.Blue};
            var screens = Screen.AllScreens.ToArray();
            foreach (var screen in screens) {
                var editor = new LayoutEditor();
                editor.Show();
                editor.AdjustToClientArea(screen);
                editor.Title = $"{editor.Left}x{editor.Top}";
                editor.Background = backgrounds[Array.IndexOf(screens, screen) % backgrounds.Length];
                editor.Closed += (sender, args) => this.Shutdown();
                this.MainWindow = editor;
            }
        }
    }
}
