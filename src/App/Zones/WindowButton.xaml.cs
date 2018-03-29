namespace LostTech.Stack.Zones
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using EventHook.Hooks;
    using LostTech.Stack.Models;
    using LostTech.Stack.ViewModels;

    /// <summary>
    /// Interaction logic for WindowButton.xaml
    /// </summary>
    public partial class WindowButton : UserControl
    {
        readonly Win32WindowFactory win32WindowFactory = new Win32WindowFactory();

        public WindowButton() {
            this.InitializeComponent();

            this.foregroundTracker = new ForegroundTracker(this,
                window => this.Window?.Equals(window) == true,
                IsForegroundPropertyKey);
            this.foregroundTracker.Hook.TextChanged += this.HookOnTextChanged;
            this.titleBinding = this.TitleText.GetBindingExpression(TextBlock.TextProperty);
        }

        public AppWindowViewModel ViewModel => (AppWindowViewModel)this.DataContext;
        public IAppWindow Window => this.ViewModel?.Window;

        void Window_OnClick(object sender, RoutedEventArgs e) => this.Window?.Activate();

        public bool IsForeground => (bool)this.GetValue(IsForegroundPropertyKey.DependencyProperty);
        public static readonly DependencyPropertyKey IsForegroundPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsForeground), typeof(bool), typeof(WindowButton), new PropertyMetadata(false));

        readonly ForegroundTracker foregroundTracker;
        readonly BindingExpression titleBinding;

        void HookOnTextChanged(object sender, WindowEventArgs windowEventArgs) {
            if (this.win32WindowFactory.Create(windowEventArgs.Handle).Equals(this.Window))
                this.titleBinding.UpdateTarget();
        }
    }
}
