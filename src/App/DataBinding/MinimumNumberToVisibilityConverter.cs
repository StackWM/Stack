namespace LostTech.Stack.DataBinding
{
    using System;
    using System.Collections;
    using System.Globalization;
    using System.Windows;
    using ValueConverters;

    public sealed class MinimumNumberToVisibilityConverter : ConverterBase
    {
        public int Minimum { get; set; } = 1;

        protected override object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value == null)
                return Visibility.Collapsed;

            int integerValue = value is ICollection collection
                ? collection.Count
                : System.Convert.ToInt32(value);
            return integerValue >= this.Minimum ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
