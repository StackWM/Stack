namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;

    using LostTech.Stack.Models;

    public partial class ScreenLayoutSelector
    {
        public ScreenLayoutSelector()
        {
            this.InitializeComponent();
        }

        public LayoutLoader LayoutLoader {
            get => (LayoutLoader)this.GetValue(LayoutLoaderProperty);
            set => this.SetValue(LayoutLoaderProperty, value);
        }
        public static readonly DependencyProperty LayoutLoaderProperty =
            DependencyProperty.Register(nameof(LayoutLoader), typeof(LayoutLoader), 
                typeof(ScreenLayoutSelector), new PropertyMetadata(null));
    }
}
