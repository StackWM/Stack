namespace LostTech.Stack.DataBinding
{
    using System;
    using System.Globalization;
    using ValueConverters;

    public sealed class PercentageConverter : ConverterBase
    {
        protected override object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (targetType != typeof(double))
                throw new NotSupportedException();

            double doubleVal = System.Convert.ToDouble(value);
            double multiplier = System.Convert.ToDouble(parameter);
            return doubleVal * multiplier / 100.0;
        }
    }
}
