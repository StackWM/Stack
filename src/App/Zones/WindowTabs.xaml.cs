namespace LostTech.Stack.Zones
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using LostTech.Stack.Licensing;
    using LostTech.Stack.Models;

    /// <summary>
    /// Interaction logic for WindowTabs.xaml
    /// </summary>
    public partial class WindowTabs : UserControl, IObjectWithProblems
    {
        public WindowTabs()
        {
            this.InitializeComponent();
        }

        public ICollection ItemsSource {
            get => (ICollection)this.GetValue(ItemsSourceProperty);
            set => this.SetValue(ItemsSourceProperty, value);
        }
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(ICollection), typeof(WindowTabs),
                new PropertyMetadata(new ArrayList()) { CoerceValueCallback = CoerceSource });

        static object CoerceSource(DependencyObject d, object baseValue) {
            if (App.IsUwp)
                return baseValue;

            var tabs = d as WindowTabs;
            ErrorEventArgs error = ExtraFeatures.PaidFeature("Tabs");
            tabs?.ProblemOccurred?.Invoke(tabs, error);
            tabs?.problems.Add(error.GetException().Message);
            return null;
        }

        #region Visibility Condition
        public VisibilityConditions VisibilityCondition {
            get => (VisibilityConditions)this.GetValue(VisibilityConditionProperty);
            set => this.SetValue(VisibilityConditionProperty, value);
        }
        public static readonly DependencyProperty VisibilityConditionProperty =
            DependencyProperty.Register(nameof(VisibilityCondition), 
                typeof(VisibilityConditions), typeof(WindowTabs),
                new PropertyMetadata(VisibilityConditions.MultipleItems));

        public enum VisibilityConditions
        {
            AlwaysVisible,
            MultipleItems,
            OneItem,
        }
        #endregion

        readonly List<string> problems = new List<string>();
        public IList<string> Problems => new ReadOnlyCollection<string>(this.problems);
        public event EventHandler<ErrorEventArgs> ProblemOccurred;
    }
}
