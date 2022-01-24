using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;

namespace LostTech.Stack.Extensibility.Filters;

[ContentProperty(nameof(CaptureFilter.Filters))]
public sealed class CaptureFilter: DependencyObject {
    [Obsolete("https://github.com/dotnet/wpf/issues/3813")]
    [DefaultValue(int.MaxValue)]
    public int Priority { get; set; } = int.MaxValue;
    public ObservableCollection<IFilter<IntPtr>> Filters { get; } = new();
}
