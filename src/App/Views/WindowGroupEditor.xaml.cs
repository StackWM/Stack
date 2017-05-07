namespace LostTech.Stack.Views
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using LostTech.Stack.Models;

    /// <summary>
    /// Interaction logic for WindowGroupEditor.xaml
    /// </summary>
    public partial class WindowGroupEditor : UserControl
    {
        public WindowGroupEditor()
        {
            this.InitializeComponent();
        }

        public void StartEdititng()
        {
            this.NameEditor.Focus();
            this.NameEditor.SelectAll();
        }


        public WindowGroup WindowGroup {
            get => (WindowGroup)this.GetValue(WindowGroupProperty);
            set => this.SetValue(WindowGroupProperty, value);
        }

        // Using a DependencyProperty as the backing store for WindowGroup.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty WindowGroupProperty =
            DependencyProperty.Register(nameof(WindowGroup), typeof(WindowGroup), typeof(WindowGroupEditor),
                new PropertyMetadata(null));
    }
}
