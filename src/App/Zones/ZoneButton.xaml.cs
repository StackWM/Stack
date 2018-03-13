namespace LostTech.Stack.Zones
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;

    using LostTech.Stack.Models;
    using LostTech.Stack.ViewModels;

    /// <summary>
    /// Interaction logic for ZoneButton.xaml
    /// </summary>
    public partial class ZoneButton : UserControl
    {
        public ZoneButton()
        {
            this.InitializeComponent();

            this.foregroundTracker = new ForegroundTracker(this,
                handle => this.Zone?.Windows?.Any(new Win32Window(handle).Equals) == true,
                IsForegroundPropertyKey);
        }

        public ZoneViewModel Zone => this.DataContext as ZoneViewModel;

        public bool IsForeground => (bool)this.GetValue(IsForegroundPropertyKey.DependencyProperty);
        public static readonly DependencyPropertyKey IsForegroundPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsForeground), typeof(bool),
                typeof(ZoneButton), new PropertyMetadata(false));

        readonly ForegroundTracker foregroundTracker;

        void Zone_OnClick(object sender, RoutedEventArgs e) {
            if (this.Zone == null)
                return;

            foreach (IAppWindow window in this.Zone.Windows) {
                window.BringToFront();
            }

            this.Zone.Windows.FirstOrDefault()?.Activate();
        }
    }
}
