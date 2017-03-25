namespace LostTech.Windows
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using LostTech.Stack.Compat;
    using FormsScreen = System.Windows.Forms.Screen;
    using static System.FormattableString;

    public sealed class Screen
    {
        readonly FormsScreen screen;

        Screen(FormsScreen screen)
        {
            this.screen = screen ?? throw new ArgumentNullException(nameof(screen));
            var detectorWindow = new Window {
                Left = this.screen.WorkingArea.Left,
                Top = this.screen.WorkingArea.Top,
                ShowInTaskbar = false,
                Title = screen.DeviceName,
                WindowStyle = WindowStyle.None,
                Width = 1,
                Height = 1,
            };
            detectorWindow.Show();
            try {
                this.PresentationSource = PresentationSource.FromVisual(detectorWindow);
            }
            finally {
                detectorWindow.Hide();
            }
        }

        // TODO: track updates
        public PresentationSource PresentationSource { get; }
        public string Name => Invariant($"{Array.IndexOf(AllScreens.ToArray(), this):D3}");

        public static Screen Primary => AllScreens.Single(screen => screen.screen.Primary);
        public static IEnumerable<Screen> AllScreens { get; } =
            new ReadOnlyCollection<Screen>(FormsScreen.AllScreens.Select(formsScreen => new Screen(formsScreen)).ToArray());

        public Rect WorkingArea => this.screen.WorkingArea.ToWPF();
    }
}
