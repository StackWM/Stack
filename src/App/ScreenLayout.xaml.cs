namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Media;
    using LostTech.Stack.Zones;

    /// <summary>
    /// Interaction logic for ScreenLayout.xaml
    /// </summary>
    public partial class ScreenLayout
    {
        public ScreenLayout()
        {
            this.InitializeComponent();
            this.Show();
            this.Hide();
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

    static class ScreenLayoutExtensions
    {
        public static IEnumerable<ScreenLayout> Active(this IEnumerable<ScreenLayout> layouts)
            => layouts.Where(layout => layout.Screen.IsActive);
    }
}
