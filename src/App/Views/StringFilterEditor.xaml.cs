namespace LostTech.Stack.Views
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using LostTech.Stack.Extensibility.Filters;

    /// <summary>
    /// Interaction logic for StringFilterEditor.xaml
    /// </summary>
    public partial class StringFilterEditor : UserControl
    {
        public StringFilterEditor()
        {
            this.InitializeComponent();
        }

        public CommonStringMatchFilter Filter {
            get => (CommonStringMatchFilter)this.GetValue(FilterProperty);
            set => this.SetValue(FilterProperty, value);
        }

        // Using a DependencyProperty as the backing store for Filter.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FilterProperty =
            DependencyProperty.Register(nameof(Filter), typeof(CommonStringMatchFilter),
                typeof(StringFilterEditor), new PropertyMetadata(null));


    }
}
