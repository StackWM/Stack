﻿namespace LostTech.Stack.Views
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
            get => (ICollection<WindowGroup>)this.GetValue(ItItemsSourceProperty);
            set => this.SetValue(ItItemsSourceProperty, value);
        }

        // Using a DependencyProperty as the backing store for ItItemsSource.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(ICollection<WindowGroup>),
                typeof(WindowGroupsEditor), new PropertyMetadata(null));


    }
}
