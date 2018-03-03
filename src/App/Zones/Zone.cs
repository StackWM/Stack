namespace LostTech.Stack.Zones
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using LostTech.Stack.Models;

    public class Zone : ContentControl
    {
        static Zone()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Zone), new FrameworkPropertyMetadata(typeof(Zone)));
        }

        public Zone()
        {
            this.AllowDrop = true;
            this.Windows.CollectionChanged += this.OnWindowCollectionChanged;
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

        protected virtual void OnWindowCollectionChanged(object sender, NotifyCollectionChangedEventArgs change) {
            var uiThread = TaskScheduler.FromCurrentSynchronizationContext();
            foreach (IAppWindow window in change.NewItems ?? new IAppWindow[0]) {
                Rect bounds = this.GetPhysicalBounds();
                window.Move(bounds).ContinueWith(error => {
                    if (error.Result != null)
                        this.NonFatalErrorOccurred?.Invoke(this, new ErrorEventArgs(error.Result));
                }, uiThread);
            }
        }

        public EventHandler<ErrorEventArgs> NonFatalErrorOccurred;

        public string Id { get; set; }

        public Zone GetFinalTarget()
        {
            var result = this;
            while (result.Target != null && !result.Equals(result.Target)) {
                result = result.Target;
            }
            return result;
        }
    }
}
