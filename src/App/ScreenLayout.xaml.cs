using System.Windows.Media;

namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Diagnostics;
    using System.Windows.Controls;
    using LostTech.Stack.Zones;
    using LostTech.Windows;

    /// <summary>
    /// Interaction logic for ScreenLayout.xaml
    /// </summary>
    public partial class ScreenLayout : Window
    {
        public ScreenLayout()
        {
            InitializeComponent();
        }

        public Screen Screen
        { get { return (Screen) this.DataContext; } set { this.DataContext = value; } }

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

        public void AdjustToClientArea()
        {
            if (this.DataContext is Screen screen)
                this.AdjustToClientArea(screen);
            else
                throw new InvalidOperationException();
        }

        public IEnumerable<Zone> Zones
        {
            get {
                var queue = new Queue<DependencyObject>();
                queue.Enqueue(this);
                while (queue.Count > 0) {
                    var element = queue.Dequeue();
                    if (element is Zone zone)
                        yield return zone;

                    int childrenCount = VisualTreeHelper.GetChildrenCount(element);
                    for (int child = 0; child < childrenCount; child++) {
                        queue.Enqueue(VisualTreeHelper.GetChild(element, child));
                    }
                }
            }
        }

        internal Zone GetZone(Point dropPoint)
        {
            Zone result = null;
            VisualTreeHelper.HitTest(this,
                target => {
                    result = target as Zone;
                    return result == null ? HitTestFilterBehavior.Continue : HitTestFilterBehavior.Stop;
                },
                _ => HitTestResultBehavior.Stop,
                new PointHitTestParameters(dropPoint));
            return result;
        }
    }
}
