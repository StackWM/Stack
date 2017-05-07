namespace LostTech.Stack.Views
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using LostTech.Stack.Models;

    /// <summary>
    /// Interaction logic for WindowGroupsEditor.xaml
    /// </summary>
    public partial class WindowGroupsEditor : UserControl
    {
        public WindowGroupsEditor()
        {
            this.InitializeComponent();
        }

        public ICollection<WindowGroup> ItemsSource {
            get => (ICollection<WindowGroup>)this.GetValue(ItemsSourceProperty);
            set => this.SetValue(ItemsSourceProperty, value);
        }

        // Using a DependencyProperty as the backing store for ItItemsSource.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(ICollection<WindowGroup>),
                typeof(WindowGroupsEditor), new PropertyMetadata(null));

        void AddGroupClick(object sender, RoutedEventArgs e)
        {
            this.ItemsSource.Add(new WindowGroup {Name = "New Group"});
            this.GroupsView.SelectedIndex = this.ItemsSource.Count - 1;
            this.NameEditor.Focus();
            this.NameEditor.SelectAll();
        }

        void RemoveGroupClick(object sender, RoutedEventArgs e)
        {
            this.ItemsSource.Remove((WindowGroup)this.GroupsView.SelectedItem);
        }
    }
}
