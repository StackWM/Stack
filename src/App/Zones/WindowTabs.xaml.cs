namespace LostTech.Stack.Zones
{
    using System;
    using System.Collections;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// Interaction logic for WindowTabs.xaml
    /// </summary>
    public partial class WindowTabs : UserControl
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
                new PropertyMetadata(new ArrayList()));
        
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
    }
}
