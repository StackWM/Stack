using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;

using LostTech.Stack.Extensibility.Filters;

namespace LostTech.Stack.Extensibility;

[ContentProperty(nameof(Filter.Filters))]
public sealed class Filter: DependencyObject {
    [Obsolete("https://github.com/dotnet/wpf/issues/3813")]
    [DefaultValue(int.MaxValue)]
    public int Priority { get; set; } = int.MaxValue;
    public ObservableCollection<IFilter<IntPtr>> Filters { get; } = new();
}
