namespace LostTech.Stack
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media;
    using LostTech.Stack.ViewModels;
    using LostTech.Stack.Utils;
    using LostTech.Stack.Zones;
    using LostTech.Windows;
    using MahApps.Metro.Controls;
    using Microsoft.Win32;
    using PInvoke;

    /// <summary>
    /// Interaction logic for ScreenLayout.xaml
    /// </summary>
    public partial class ScreenLayout
    {
        public ScreenLayout()
        {
            this.InitializeComponent();
            this.Show();
        }

        public IEnumerable<Zone> Zones => this.FindChildren<Zone>();

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

        internal new ScreenLayoutViewModel ViewModel {
            get => (ScreenLayoutViewModel)base.ViewModel;
            set => base.ViewModel = value;
        }
    }

    static class ScreenLayoutExtensions
    {
        public static IEnumerable<ScreenLayout> Active(this IEnumerable<ScreenLayout> layouts)
            => layouts.Where(layout => layout.Screen.IsActive);
    }
}
