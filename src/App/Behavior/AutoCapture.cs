using System.Collections.ObjectModel;
using System.Windows;

using LostTech.Stack.Extensibility.Filters;
using LostTech.Stack.Zones;

namespace LostTech.Stack.Behavior;

public static class AutoCapture {
    public static readonly DependencyProperty CaptureFiltersProperty =
        DependencyProperty.RegisterAttached("CaptureFilters", typeof(ObservableCollection<CaptureFilter>), typeof(Zone));

    public static readonly DependencyProperty PriorityProperty =
        DependencyProperty.RegisterAttached("Priority", typeof(int), typeof(CaptureFilter),
            new PropertyMetadata(defaultValue: int.MaxValue));

    public static ObservableCollection<CaptureFilter> GetCaptureFilters(DependencyObject target) {
        if (target.GetValue(CaptureFiltersProperty) is ObservableCollection<CaptureFilter> v)
            return v;
        v = new ObservableCollection<CaptureFilter>();
        target.SetValue(CaptureFiltersProperty, v);
        return v;
    }

    public static int GetPriority(DependencyObject target)
        => target.GetValue(PriorityProperty) is int v ? v : int.MaxValue;
}
