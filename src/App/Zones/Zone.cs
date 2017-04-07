namespace LostTech.Stack.Zones
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Documents;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Navigation;
    using System.Windows.Shapes;
    using static DebugHelper;

    /// <summary>
    /// Follow steps 1a or 1b and then 2 to use this custom control in a XAML file.
    ///
    /// Step 1a) Using this custom control in a XAML file that exists in the current project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:LostTech.Stack.Zones"
    ///
    ///
    /// Step 1b) Using this custom control in a XAML file that exists in a different project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:LostTech.Stack.Zones;assembly=LostTech.Stack.Zones"
    ///
    /// You will also need to add a project reference from the project where the XAML file lives
    /// to this project and Rebuild to avoid compilation errors:
    ///
    ///     Right click on the target project in the Solution Explorer and
    ///     "Add Reference"->"Projects"->[Browse to and select this project]
    ///
    ///
    /// Step 2)
    /// Go ahead and use your control in the XAML file.
    ///
    ///     <MyNamespace:Zone/>
    ///
    /// </summary>
    public class Zone : ContentControl
    {
        static Zone()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Zone), new FrameworkPropertyMetadata(typeof(Zone)));
        }

        public Zone()
        {
            this.AllowDrop = true;
        }

        public bool IsDragMouseOver {
            get { return (bool)GetValue(IsDragMouseOverProperty); }
            set { SetValue(IsDragMouseOverProperty, value); }
        }

        public static readonly DependencyProperty IsDragMouseOverProperty =
            DependencyProperty.Register("IsDragMouseOver", typeof(bool), typeof(Zone), new PropertyMetadata(false));

        public Zone Target
        {
            get { return (Zone)GetValue(TargetProperty) ?? this; }
            set { SetValue(TargetProperty, value); }
        }
        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register(nameof(Target), typeof(Zone), typeof(Zone), new PropertyMetadata(null));

        public Zone GetFinalTarget()
        {
            var result = this;
            while (result.Target != null && !result.Equals(result.Target)) {
                result = result.Target;
            }
            return result;
        }
    }
}
