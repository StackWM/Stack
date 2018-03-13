namespace LostTech.Stack.Zones
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using LostTech.Stack.Models;
    using LostTech.Stack.ViewModels;

    /// <summary>
    /// Interaction logic for ZoneButton.xaml
    /// </summary>
    public partial class ZoneButton
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

        // ReSharper disable once NotAccessedField.Local
        readonly ForegroundTracker foregroundTracker;

        async void Zone_OnClick(object sender, RoutedEventArgs e) {
            IAppWindow first = this.Zone?.Windows.FirstOrDefault();
            if (first == null)
                return;

            await first.Activate();

            await Task.WhenAll(this.Zone.Windows.Select(window => window.BringToFront()));
        }
    }
}
