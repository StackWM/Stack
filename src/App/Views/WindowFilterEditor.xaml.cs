namespace LostTech.Stack.Views
{
    using System.Windows;
    using System.Windows.Controls;
    using LostTech.Stack.Extensibility.Filters;

    /// <summary>
    /// Interaction logic for WindowFilterEditor.xaml
    /// </summary>
    public partial class WindowFilterEditor : UserControl
    {
        public WindowFilterEditor()
        {
            this.InitializeComponent();
        }


        public WindowFilter Filter {
            get => (WindowFilter)this.GetValue(FilterProperty);
            set => this.SetValue(FilterProperty, value);
        }

        // Using a DependencyProperty as the backing store for Filter.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FilterProperty =
            DependencyProperty.Register(nameof(Filter), typeof(WindowFilter), typeof(WindowFilterEditor),
                new PropertyMetadata(null));


    }
}
