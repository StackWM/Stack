namespace LostTech.Stack.Zones
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using LostTech.Stack.Licensing;
    using LostTech.Stack.Models;
    using LostTech.Stack.ViewModels;

    /// <summary>
    /// Interaction logic for ZoneButton.xaml
    /// </summary>
    public partial class ZoneButton: IObjectWithProblems, IDisposable
    {
        public ZoneButton()
        {
            this.InitializeComponent();

            this.foregroundTracker = new ForegroundTracker(this,
                window => this.Zone?.Windows?.Any(vm => window.Equals(vm.Window)) == true,
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
            AppWindowViewModel first = this.Zone?.Windows.FirstOrDefault();
            if (first == null)
                return;

            if (!App.IsUwp) {
                ErrorEventArgs error = ExtraFeatures.PaidFeature("Tabs: Zone Buttons");
                this.problems.Add(error.GetException().Message);
                this.ProblemOccurred?.Invoke(this, error);
            }

            await first.Window.Activate();

            await Task.WhenAll(this.Zone.Windows.Select(window => window.Window.BringToFront()));
        }

        readonly List<string> problems = new List<string>();
        public IList<string> Problems => new ReadOnlyCollection<string>(this.problems);
        public event EventHandler<ErrorEventArgs> ProblemOccurred;

        void ZoneButton_OnUnloaded(object sender, RoutedEventArgs e) {
            this.foregroundTracker.Dispose();
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent) {
            base.OnVisualParentChanged(oldParent);

            if (this.VisualParent == null)
                this.Dispose();
        }

        public void Dispose() => this.foregroundTracker.Dispose();
    }
}
