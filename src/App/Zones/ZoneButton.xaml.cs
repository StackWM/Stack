namespace LostTech.Stack.Zones
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;

    using LostTech.Stack.Models;

    /// <summary>
    /// Interaction logic for ZoneButton.xaml
    /// </summary>
    public partial class ZoneButton : UserControl
    {
        public ZoneButton()
        {
            this.InitializeComponent();
        }

        public Zone Zone => this.DataContext as Zone;

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
