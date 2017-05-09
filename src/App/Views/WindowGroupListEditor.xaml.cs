namespace LostTech.Stack.Views
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using LostTech.Stack.Models;
    using Prism.Commands;

    /// <summary>
    /// Interaction logic for WindowGroupListEditor.xaml
    /// </summary>
    public partial class WindowGroupListEditor : UserControl
    {
        public WindowGroupListEditor()
        {
            this.AddGroupCommand = new DelegateCommand<WindowGroup>(group => this.Groups.Add(group.Name));

            this.InitializeComponent();

            NameScope.SetNameScope(this.AvaiableGroupsMenu, NameScope.GetNameScope(this));
        }

        public ICommand AddGroupCommand { get; }

        public IEnumerable<WindowGroup> AvailableGroups {
            get => (IEnumerable<WindowGroup>)this.GetValue(AvailableGroupsProperty);
            set => this.SetValue(AvailableGroupsProperty, value);
        }



        public ICollection<string> Groups {
            get => (ICollection<string>)this.GetValue(GroupsProperty);
            set => this.SetValue(GroupsProperty, value);
        }

        void AddGroupClick(object sender, RoutedEventArgs e)
        {
            var addButton = (Button)sender;
            addButton.ContextMenu.IsOpen = true;
        }

        void RemoveGroupClick(object sender, RoutedEventArgs e)
            => this.Groups.Remove((string)this.GroupList.SelectedValue);

        // Using a DependencyProperty as the backing store for Groups.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty GroupsProperty =
            DependencyProperty.Register(nameof(Groups), typeof(ICollection<string>),
                typeof(WindowGroupListEditor), new PropertyMetadata(new ObservableCollection<string>()));



        // Using a DependencyProperty as the backing store for AvailableGroups.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AvailableGroupsProperty =
            DependencyProperty.Register(nameof(AvailableGroups), typeof(IEnumerable<WindowGroup>),
                typeof(WindowGroupListEditor), new PropertyMetadata(null));
    }
}
