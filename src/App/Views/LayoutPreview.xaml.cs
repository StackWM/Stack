namespace LostTech.Stack.Views
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using LostTech.Stack.Models;

    public partial class LayoutPreview
    {
        public LayoutPreview()
        {
            this.InitializeComponent();
            this.DataContextChanged += this.OnDataContextChanged;
        }

        internal LayoutLoader LayoutLoader { get; set; }

        async void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs) {
            this.Viewbox.Child = dependencyPropertyChangedEventArgs.NewValue is string layout 
                ? await this.LayoutLoader.LoadLayoutOrDefault(layout) : null;
        }
    }
}
