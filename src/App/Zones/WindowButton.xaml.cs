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

    /// <summary>
    /// Interaction logic for WindowButton.xaml
    /// </summary>
    public partial class WindowButton : UserControl
    {
        public WindowButton() {
            this.InitializeComponent();

            this.foregroundTracker = new ForegroundTracker(this,
                handle => this.Window?.Equals(new Win32Window(handle)) == true,
                IsForegroundPropertyKey);
            this.foregroundTracker.Hook.TextChanged += this.HookOnTextChanged;
            this.titleBinding = this.TitleText.GetBindingExpression(TextBlock.TextProperty);
        }

        public IAppWindow Window => this.DataContext as IAppWindow;

        void Window_OnClick(object sender, RoutedEventArgs e) => this.Window?.Activate();

        public bool IsForeground => (bool)this.GetValue(IsForegroundPropertyKey.DependencyProperty);
        public static readonly DependencyPropertyKey IsForegroundPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsForeground), typeof(bool), typeof(WindowButton), new PropertyMetadata(false));

        readonly ForegroundTracker foregroundTracker;
        readonly BindingExpression titleBinding;

        void HookOnTextChanged(object sender, WindowEventArgs windowEventArgs) {
            if (new Win32Window(windowEventArgs.Handle).Equals(this.Window))
                this.titleBinding.UpdateTarget();
        }
    }
}
