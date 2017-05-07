﻿namespace LostTech.Stack.Views
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using LostTech.Stack.Models.Filters;

    /// <summary>
    /// Interaction logic for WindowFiltersEditor.xaml
    /// </summary>
    public partial class WindowFiltersEditor : UserControl
    {
        public WindowFiltersEditor()
        {
            this.InitializeComponent();
        }

        public ICollection<WindowFilter> Filters {
            get => (ICollection<WindowFilter>)this.GetValue(FiltersProperty);
            set => this.SetValue(FiltersProperty, value);
        }

        // Using a DependencyProperty as the backing store for Filters.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FiltersProperty =
            DependencyProperty.Register(nameof(Filters), typeof(ICollection<WindowFilter>),
                typeof(WindowFiltersEditor), new PropertyMetadata(null));


    }
}
