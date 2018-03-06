namespace LostTech.Stack.Zones
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Collections;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.IO;
    using LostTech.Stack.Models;

    /// <summary>
    /// Interaction logic for Zone.xaml
    /// </summary>
    public partial class Zone : UserControl
    {
        public Zone() {
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

        public ObservableCollection<IAppWindow> Windows { get; } = new ObservableCollection<IAppWindow>();

        public event EventHandler<ErrorEventArgs> NonFatalErrorOccurred;

        public string Id { get; set; }

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
