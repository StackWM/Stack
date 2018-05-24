namespace LostTech.Stack.Zones {
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Windows;
    using System.Windows.Controls;
    using LostTech.Stack.Licensing;
    using LostTech.Stack.Models;
    using LostTech.Stack.ViewModels;

    /// <summary>
    /// Interaction logic for WindowButton.xaml
    /// </summary>
    public partial class WindowButton : UserControl, IObjectWithProblems, IDisposable
    {
        readonly Win32WindowFactory win32WindowFactory = new Win32WindowFactory();

        public WindowButton() {
            this.InitializeComponent();

            this.foregroundTracker = new ForegroundTracker(this,
                window => this.Window?.Equals(window) == true,
                IsForegroundPropertyKey);
        }

        public AppWindowViewModel ViewModel => this.DataContext as AppWindowViewModel;
        public IAppWindow Window => this.ViewModel?.Window;

        void Window_OnClick(object sender, RoutedEventArgs e) {
            if (App.IsUwp) {
                this.Window?.Activate();
                return;
            }

            ErrorEventArgs error = ExtraFeatures.PaidFeature("Tabs: Window Buttons");
            this.problems.Add(error.GetException().Message);
            this.ProblemOccurred?.Invoke(this, error);
        }

        public bool IsForeground => (bool)this.GetValue(IsForegroundPropertyKey.DependencyProperty);
        public static readonly DependencyPropertyKey IsForegroundPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsForeground), typeof(bool), typeof(WindowButton), new PropertyMetadata(false));

        readonly ForegroundTracker foregroundTracker;

        readonly List<string> problems = new List<string>();
        public IList<string> Problems => new ReadOnlyCollection<string>(this.problems);
        public event EventHandler<ErrorEventArgs> ProblemOccurred;

        void WindowButton_OnUnloaded(object sender, RoutedEventArgs e) {
            this.Dispose();
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent) {
            base.OnVisualParentChanged(oldParent);

            if (this.VisualParent == null)
                this.Dispose();
        }

        public void Dispose() {
            this.foregroundTracker.Dispose();
        }
    }
}
