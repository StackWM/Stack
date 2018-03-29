namespace LostTech.Stack.Zones
{
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
    /// Interaction logic for Zone.xaml
    /// </summary>
    public partial class Zone : UserControl
    {
        public Zone() {
            this.ViewModel.Windows = this.Windows;
            this.InitializeComponent();
            this.AllowDrop = true;
        }

        public bool IsDragMouseOver {
            get => (bool)this.GetValue(IsDragMouseOverProperty);
            set => this.SetValue(IsDragMouseOverProperty, value);
        }
        public static readonly DependencyProperty IsDragMouseOverProperty =
            DependencyProperty.Register(nameof(IsDragMouseOver), typeof(bool), typeof(Zone), new PropertyMetadata(false));

        public Zone Target {
            get => (Zone)this.GetValue(TargetProperty) ?? this;
            set => this.SetValue(TargetProperty, value);
        }
        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register(nameof(Target), typeof(Zone), typeof(Zone), new PropertyMetadata(null));

        public string Role {
            get => (string)this.GetValue(RoleDependencyProperty);
            set => this.SetValue(RoleDependencyProperty, value);
        }
        public static readonly DependencyProperty RoleDependencyProperty =
            DependencyProperty.Register(nameof(Role), typeof(string), typeof(Zone));
        
        public ItemsPanelTemplate Layout {
            get => (ItemsPanelTemplate)this.GetValue(LayoutProperty);
            set {
                if (!App.IsUwp)
                    return;

                this.SetValue(LayoutProperty, value);
            }
        }

        static readonly ItemsPanelTemplate DefaultItemsPanelTemplate = new ItemsPanelTemplate { VisualTree = new FrameworkElementFactory(typeof(Grid)) };
        public static readonly DependencyProperty LayoutProperty =
            DependencyProperty.Register(nameof(Layout), typeof(ItemsPanelTemplate),
                ownerType: typeof(Zone),
                typeMetadata: new PropertyMetadata(
                    defaultValue: DefaultItemsPanelTemplate,
                    propertyChangedCallback: null,
                    coerceValueCallback: CoerceLayout));

        static object CoerceLayout(DependencyObject d, object baseValue) {
            if (App.IsUwp)
                return baseValue;

            var zone = d as Zone;
            ErrorEventArgs error = ExtraFeatures.PaidFeature("Zone Layouts");
            zone?.NonFatalErrorOccurred?.Invoke(zone, error);
            zone?.loadProblems.Add(error.GetException().Message);
            return DefaultItemsPanelTemplate;
        }

        readonly List<string> loadProblems = new List<string>();
        public IEnumerable<string> LoadProblems => new ReadOnlyCollection<string>(this.loadProblems);
        public ObservableCollection<AppWindowViewModel> Windows { get; } = new ObservableCollection<AppWindowViewModel>();

        public event EventHandler<ErrorEventArgs> NonFatalErrorOccurred;

        public string Id { get => this.ViewModel.Id; set => this.ViewModel.Id = value; }
        public ZoneViewModel ViewModel { get; } = new ZoneViewModel();

        public Zone GetFinalTarget() {
            var result = this;
            while (result.Target != null && !result.Equals(result.Target)) {
                result = result.Target;
            }
            return result;
        }

        void Host_NonFatalErrorOccurred(object sender, ErrorEventArgs e) =>
            this.NonFatalErrorOccurred?.Invoke(sender, e);
    }
}
