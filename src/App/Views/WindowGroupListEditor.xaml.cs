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
            this.AddGroupCommand = new DelegateCommand<WindowGroup>(group => {
                    this.Groups.Add(@group.Name);
                    this.AddGroupCommand.RaiseCanExecuteChanged();
                },
                canExecuteMethod: group => group != null && !this.Groups.Contains(group.Name));

            this.InitializeComponent();

            NameScope.SetNameScope(this.AvaiableGroupsList, NameScope.GetNameScope(this));
        }

        public DelegateCommand<WindowGroup> AddGroupCommand { get; }

        public IEnumerable<WindowGroup> AvailableGroups {
            get => (IEnumerable<WindowGroup>)this.GetValue(AvailableGroupsProperty);
            set => this.SetValue(AvailableGroupsProperty, value);
        }

        public ICollection<string> Groups {
            get => (ICollection<string>)this.GetValue(GroupsProperty);
            set => this.SetValue(GroupsProperty, value);
        }

        void RemoveGroupClick(object sender, RoutedEventArgs e) {
            this.Groups.Remove((string)this.GroupList.SelectedValue);
            this.AddGroupCommand.RaiseCanExecuteChanged();
        }

        // Using a DependencyProperty as the backing store for Groups.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty GroupsProperty =
            DependencyProperty.Register(nameof(Groups), typeof(ICollection<string>),
                typeof(WindowGroupListEditor), new PropertyMetadata(new ObservableCollection<string>()));


        // Using a DependencyProperty as the backing store for AvailableGroups.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AvailableGroupsProperty =
            DependencyProperty.Register(nameof(AvailableGroups), typeof(IEnumerable<WindowGroup>),
                typeof(WindowGroupListEditor), new PropertyMetadata(null));

        void AvaiableGroupsList_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            this.AddGroupCommand.RaiseCanExecuteChanged();
        }
    }
}
