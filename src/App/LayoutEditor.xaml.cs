﻿namespace LostTech.Stack
{
    using System;
    using System.Linq;
    using System.Windows;
    using System.Diagnostics;
    using System.Windows.Media;
    using LostTech.Stack.Compat;
    using LostTech.Stack.Layouts;
    using LostTech.Windows;

    /// <summary>
    /// Interaction logic for LayoutEditor.xaml
    /// </summary>
    public partial class LayoutEditor : Window
    {
        public LayoutEditor()
        {
            InitializeComponent();
        }

        public void AdjustToClientArea(Screen screen)
        {
            if (screen  == null)
                throw new ArgumentNullException(nameof(screen));

            Debug.WriteLine(screen.WorkingArea);
            var transformFromDevice = screen.PresentationSource.CompositionTarget.TransformFromDevice;
            var topLeft = transformFromDevice.Transform(screen.WorkingArea.TopLeft);
            this.Left = topLeft.X;
            this.Top = topLeft.Y;

            var size = new Vector(screen.WorkingArea.Width, screen.WorkingArea.Height);
            var dimensions = transformFromDevice.Transform(size);
            this.Width = dimensions.X;
            this.Height = dimensions.Y;
        }
    }
}
