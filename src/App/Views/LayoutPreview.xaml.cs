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

        public LayoutLoader LayoutLoader {
            get => (LayoutLoader)this.GetValue(LayoutLoaderProperty);
            set => this.SetValue(LayoutLoaderProperty, value);
        }
        public static readonly DependencyProperty LayoutLoaderProperty =
            DependencyProperty.Register(nameof(LayoutLoader), typeof(LayoutLoader),
                                        typeof(LayoutPreview), new PropertyMetadata(null) {
                                            PropertyChangedCallback = OnLayoutPreviewChanged,
                                        });

        static void OnLayoutPreviewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((LayoutPreview)d).UpdateView();
        }

        void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs) {
            this.UpdateView();
        }

        async void UpdateView() {
            if (this.LayoutLoader != null && this.DataContext is string layoutName) {
                var layout = await this.LayoutLoader.LoadLayoutOrDefault(layoutName + ".xaml");
                if (double.IsNaN(layout.Width))
                    layout.Width = 1024;
                if (double.IsNaN(layout.Height))
                    layout.Height = 1024;
                this.Width = this.Height * layout.Width / layout.Height;
                this.Viewbox.Child = layout;
            } else
                this.Viewbox.Child = null;
        }
    }
}
